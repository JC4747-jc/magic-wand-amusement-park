"""Own and briefly verify the unchanged production F2G loopback server."""
import argparse
import ast
from dataclasses import dataclass
import json
import os
from pathlib import Path
import socket
import subprocess
import sys
import time

from f2g_service_guard import Policy, Lease, atomic, read_json, properties, owned_identity, stop_owned, proc_identity, same_live
from f2g_model_guard import digest, gpu_snapshot, admit_gpu, check_growth, model_inputs, observe_host_lease, MEMORY_MAX, ALLOCATOR_MIB

ROOT = Path(__file__).resolve().parents[1]
RUNTIME = Path('/opt/f2g/runtime')
PORTS = (5006, 5007)
LIGHTER_SHA = '999bca4f5bf96aeed3ac0f1047e31530e4eb4584873c85dd5bd00031fc71fb3a'
SOURCES = ('Tools/f2g_server_guard.py', 'Tools/run_f2g_server_bounded.ps1',
           'Tools/check_f2g_server_loopback.py', 'Tools/f2g_model_guard.py',
           'Tools/f2g_service_guard.py', 'Tools/build_process_job.ps1', 'Tools/setup_f2g_runtime.py',
           'object-detection-demo/pose_bridge.py', 'object-detection-demo/pose_availability.py',
           'object-detection-demo/detector_config.py')


@dataclass(frozen=True)
class ServerPolicy(Policy):
    @property
    def description(self):
        return 'MagicMR owned F2G loopback server ' + self.nonce

    def folder(self, root=ROOT):
        return root / '.codex-tmp/f2g-server-guard' / self.run_id / self.nonce


def validate_host(policy):
    host = read_json(policy.folder() / 'host.json')
    if host.get('nonce') != policy.nonce or host.get('unit') != policy.unit:
        raise ValueError('Server host ownership mismatch')
    rows = host.get('inputs', [])
    if len(rows) != len(SOURCES) or {r['path'] for r in rows} != set(SOURCES):
        raise ValueError('Unexpected fixed server source allowlist')
    for row in rows:
        if digest(ROOT / row['path']) != row['sha256']:
            raise ValueError('Server source changed: ' + row['path'])
    return session_options(host)


def session_options(host):
    seconds = host.get('sessionSeconds', 0)
    probe = host.get('probe', 'none')
    if type(seconds) is not int or not 0 <= seconds <= 600:
        raise ValueError('Session seconds must be an integer from 0 to 600')
    if probe not in ('none', 'cancel-ready') or (probe == 'cancel-ready' and seconds < 10):
        raise ValueError('Unknown probe or insufficient controlled session')
    return {'sessionSeconds': seconds, 'probe': probe}


def validate_cancel(policy, result, initial, active, trigger, admit):
    if (result.get('passed') is not False
            or result.get('error') != 'ValueError: Host lease expired during production server check'
            or result.get('childExitCode') not in (-15, -9)):
        raise ValueError('Expected owned production server cancellation was not observed')
    for item in (initial, active, trigger, admit):
        if item.get('nonce') != policy.nonce or item.get('unit') != policy.unit:
            raise ValueError('Controlled cancellation receipt ownership mismatch')
    if (initial.get('passed') is not True or active.get('successfulHealthChecks', 0) < 2
            or active.get('pid') != admit.get('pid') or trigger.get('pid') != admit.get('pid')
            or trigger.get('successfulHealthChecks') != active.get('successfulHealthChecks')):
        raise ValueError('Cancellation did not follow verified Windows health activity')


def command(policy, options=None):
    options = session_options(options or {})
    return ['systemd-run', '--unit=' + policy.unit, '--description=' + policy.description,
            '--collect', '--no-block', '--property=Type=exec', '--property=Restart=no',
            '--property=KillMode=control-group', '--property=SendSIGKILL=yes',
            '--property=TimeoutStopSec=3s', '--property=RuntimeMaxSec=' + str(120 + options['sessionSeconds']) + 's',
            '--property=MemoryAccounting=yes', '--property=MemoryMax=' + str(MEMORY_MAX),
            '--property=MemorySwapMax=0', '--property=TasksMax=64', '--property=CPUQuota=100%',
            '--property=OOMPolicy=kill', '--property=LimitFSIZE=16777216',
            '--property=StandardOutput=null', '--property=StandardError=null',
            '/usr/bin/python3', '-B', str(ROOT / 'Tools/f2g_server_guard.py'),
            'worker', '--run-id', policy.run_id, '--nonce', policy.nonce]


def verify_limits(policy):
    line = next(x for x in Path('/proc/self/cgroup').read_text().splitlines() if x.startswith('0::'))
    group = (Path('/sys/fs/cgroup') / line.split(':', 2)[2].lstrip('/')).resolve()
    if not group.is_relative_to(Path('/sys/fs/cgroup')) or group.name != policy.unit:
        raise ValueError('Server process outside its owned cgroup')
    values = {k: (group / k).read_text().strip() for k in ('memory.max', 'memory.swap.max', 'pids.max', 'cpu.max')}
    if values['memory.max'] != str(MEMORY_MAX) or values['memory.swap.max'] != '0' or values['pids.max'] != '64':
        raise ValueError('Server kernel limits missing')
    quota, period = values['cpu.max'].split()
    if quota == 'max' or not 0 < int(quota) <= int(period):
        raise ValueError('Server CPU quota missing')
    return values


def inputs():
    rows = model_inputs()
    tree = ast.parse((ROOT / 'Tools/setup_f2g_runtime.py').read_text('utf-8'))
    downloads = next(ast.literal_eval(n.value) for n in ast.walk(tree) if isinstance(n, ast.Assign)
                     and any(isinstance(t, ast.Name) and t.id == 'downloads' for t in n.targets))
    pins = {name: expected for url, expected, name in downloads}
    extra = [(RUNTIME / 'weights/sam_vit_b_01ec64.pth', pins['sam_vit_b_01ec64.pth']),
             (ROOT / 'object-detection-demo/weights/best_yolo26n_assgn2_v6_50e.pt', LIGHTER_SHA)]
    for path, expected in extra:
        maximum = 16 * 1024**2 if expected == LIGHTER_SHA else 512 * 1024**2
        if not 0 < path.stat().st_size <= maximum:
            raise ValueError('Unexpected existing server weight size: ' + path.name)
        actual = digest(path)
        if actual != expected:
            raise ValueError('Existing server weight differs from pinned identity: ' + path.name)
        rows.append({'path': str(path), 'resolvedPath': str(path.resolve()), 'bytes': path.stat().st_size,
                     'mtimeNs': path.stat().st_mtime_ns, 'sha256': actual})
    if len(rows) > 256 or sum(r['bytes'] for r in rows) > 2 * 1024**3:
        raise ValueError('Unexpected server input footprint')
    return rows


def ports_owned(text, owned_inodes):
    found = {port: set() for port in PORTS}
    for line in text.splitlines()[1:]:
        fields = line.split()
        if len(fields) < 10 or fields[3] != '0A':
            continue
        address, raw_port = fields[1].split(':')
        port = int(raw_port, 16)
        if port not in found:
            continue
        if address != '0100007F':
            raise ValueError('Production test listener is not loopback-only')
        found[port].add(fields[9])
    return all(len(inodes) == 1 and inodes.issubset(owned_inodes) for inodes in found.values())


def child_owns_ports(child):
    owned = set()
    for path in (Path('/proc') / str(child.pid) / 'fd').iterdir():
        try:
            link = os.readlink(path)
        except OSError:
            continue
        if link.startswith('socket:['):
            owned.add(link[8:-1])
    return ports_owned(Path('/proc/net/tcp').read_text(), owned)


def reclaim_warmup_cache(torch_module):
    """Release only idle allocator cache after synchronous combined warmup."""
    cuda = torch_module.cuda
    cuda.synchronize()
    before_allocated = cuda.memory_allocated()
    before_reserved = cuda.memory_reserved()
    cuda.empty_cache()
    after_allocated = cuda.memory_allocated()
    after_reserved = cuda.memory_reserved()
    if after_allocated != before_allocated or after_reserved > before_reserved:
        raise RuntimeError('Warmup cache reclamation changed active allocations or grew the pool')
    return {'allocatedBytesBefore': before_allocated, 'allocatedBytesAfter': after_allocated,
            'reservedBytesBefore': before_reserved, 'reservedBytesAfter': after_reserved,
            'releasedReservedMiB': (before_reserved - after_reserved) / 1024**2,
            'activeAllocationsUnchanged': True,
            'scope': 'One idle-cache release after original warmup; no weights/dtype/forward/routing changes'}


def validate_health(health):
    if health.get('version') != 2 or health.get('image_ready') is not True or health.get('pose_ready') is not True:
        raise ValueError('Actual server model readiness missing')
    expected = {'sha256': LIGHTER_SHA, 'conf': .399, 'imgsz': 640, 'iou': .7}
    detector = health.get('detector', {})
    if any(detector.get(k) != v for k, v in expected.items()):
        raise ValueError('Actual server detector identity/options mismatch')


def health():
    with socket.create_connection(('127.0.0.1', 5007), timeout=.15) as client:
        client.settimeout(.15)
        with client.makefile('rb') as stream:
            body = stream.readline(16385)
    if len(body) > 16384 or not body.endswith(b'\n'):
        raise ValueError('Unbounded/incomplete server health message')
    value = json.loads(body)
    validate_health(value)
    return value


def serve(policy):
    validate_host(policy)
    verify_limits(policy)
    folder = policy.folder()
    bound = read_json(folder / 'runtime-inputs.json', cap=131072)
    for row in bound:
        stat = Path(row['path']).stat()
        if stat.st_size != row['bytes'] or stat.st_mtime_ns != row['mtimeNs']:
            raise ValueError('Server input changed after hashing')
    admit_gpu(gpu_snapshot())
    import torch
    import runpy
    total = torch.cuda.get_device_properties(0).total_memory
    torch.cuda.set_per_process_memory_fraction(ALLOCATOR_MIB * 1024**2 / total, device=0)
    import f2g_pose
    origin = str(Path(f2g_pose.__file__).resolve())
    if origin not in {row['resolvedPath'] for row in bound}:
        raise ValueError('Actual F2G server implementation is outside its bound inputs')
    atomic(folder / 'backend.json', {'python': sys.version, 'torch': torch.__version__,
                                    'gpu': torch.cuda.get_device_name(0), 'allocatorMiB': ALLOCATOR_MIB,
                                    'f2gOrigin': origin,
                                    'scope': 'PyTorch allocator, not all CUDA allocations; shared loopback namespace'})
    entry = ROOT / 'object-detection-demo/pose_bridge.py'
    sys.path.insert(0, str(entry.parent))
    sys.argv = [str(entry), '--host', '127.0.0.1', '--port', '5006', '--health-port', '5007',
                '--checkpoint', str(RUNTIME / 'weights/f2g-pose.pt'),
                '--sam', str(RUNTIME / 'weights/sam_vit_b_01ec64.pth'),
                '--native-bottom-up', '--warmup-sample', str(RUNTIME / 'F2G-Pose/examples/rope')]
    # Execute the original main and models; only the warmup return releases idle cache.
    namespace = runpy.run_path(str(entry), run_name='magicmr_owned_pose_bridge')
    original_warmup = namespace['Models'].warmup

    def warmup_and_reclaim(model, *args, **kwargs):
        value = original_warmup(model, *args, **kwargs)
        first = reclaim_warmup_cache(torch)
        # Exercise the unchanged three-model forward path after cache reclamation.
        value = original_warmup(model, *args, **kwargs)
        final = reclaim_warmup_cache(torch)
        atomic(folder / 'warm-cache.json', {
            'passes': [first, final], 'activeAllocationsUnchanged': True,
            'originalCombinedWarmupCalls': 2, 'postReclaimOriginalForwardRegression': True,
            'scope': 'Original warmup, idle-cache release, original warmup regression, final idle-cache release'})
        return value

    namespace['Models'].warmup = warmup_and_reclaim
    namespace['main']()


def worker(policy):
    folder = policy.folder()
    child = None
    samples = []
    sample_count = 0
    minimum_gpu_free = None
    result = {'passed': False, 'mode': 'BOUNDED_PRODUCTION_LOOPBACK_CHECK'}
    phase = 'preflight'
    try:
        options = validate_host(policy)
        result['kernelLimits'] = verify_limits(policy)
        atomic(folder / 'worker.json', proc_identity(os.getpid()))
        # Refuse occupied Linux ports; do not stop existing listeners.
        probes = []
        try:
            for port in PORTS:
                probe = socket.socket()
                probes.append(probe)
                probe.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
                probe.bind(('127.0.0.1', port))
                probe.listen(1)
        finally:
            for probe in probes:
                probe.close()
        atomic(folder / 'runtime-inputs.json', inputs())
        lease = Lease(policy.nonce)
        if not observe_host_lease(folder / 'lease.json', lease, time.monotonic()):
            raise ValueError('Host lease expired before server admission')
        result['gpuAdmission'] = gpu_snapshot()
        admit_gpu(result['gpuAdmission'])
        env = dict(os.environ, OMP_NUM_THREADS='1', MKL_NUM_THREADS='1', MAX_JOBS='1',
                   HF_HUB_OFFLINE='1', TRANSFORMERS_OFFLINE='1', PYTHONDONTWRITEBYTECODE='1',
                   CUDA_CACHE_DISABLE='1', HF_HOME=str(folder / 'hf-cache'),
                   F2G_RADIO_REPOSITORY=str(RUNTIME / 'RADIO'),
                   F2G_RADIO_CHECKPOINT=str(RUNTIME / 'weights/radio-v2.5-l_half.pth.tar'))
        with (folder / 'server.log').open('wb') as log:
            child = subprocess.Popen(['/opt/f2g/venv/bin/python', '-B', str(ROOT / 'Tools/f2g_server_guard.py'),
                                      'serve', '--run-id', policy.run_id, '--nonce', policy.nonce],
                                     stdout=log, stderr=subprocess.STDOUT, env=env)
            atomic(folder / 'child.json', proc_identity(child.pid))
            start = time.monotonic()
            samples = []
            sample_count = 0
            minimum_gpu_free = None
            while True:
                phase = 'owned-child-status'
                if child.poll() is not None:
                    raise RuntimeError('Actual production server exited ' + str(child.returncode))
                phase = 'host-lease'
                if not observe_host_lease(folder / 'lease.json', lease, time.monotonic()):
                    raise ValueError('Host lease expired during production server check')
                phase = 'owned-file-growth-snapshot'
                check_growth(folder)
                phase = 'gpu-observation'
                gpu = gpu_snapshot()
                samples.append({'elapsedSeconds': round(time.monotonic() - start, 2), **gpu})
                sample_count += 1
                minimum_gpu_free = gpu['freeMiB'] if minimum_gpu_free is None else min(minimum_gpu_free, gpu['freeMiB'])
                samples = samples[-24:]
                if gpu['freeMiB'] < 256:
                    raise ValueError('GPU free memory floor crossed')
                phase = 'listener-readiness'
                if not (folder / 'ready.json').exists() and child_owns_ports(child):
                    actual_health = health()
                    atomic(folder / 'ready.json', {'nonce': policy.nonce, 'unit': policy.unit,
                                                   'portsOwnedByChild': list(PORTS), 'health': actual_health})
                phase = 'windows-client-receipt'
                client = folder / 'windows-client.json'
                if client.exists():
                    value = read_json(client)
                    if value.get('nonce') != policy.nonce or value.get('unit') != policy.unit or value.get('passed') is not True:
                        raise ValueError('Windows client ownership/readiness receipt missing')
                    if (value.get('sessionSeconds') != options['sessionSeconds']
                            or value.get('observedWatchSeconds', -1) < options['sessionSeconds']):
                        raise ValueError('Windows client ended before its requested session')
                    if not child_owns_ports(child):
                        raise ValueError('Server lost listener ownership')
                    result.update(passed=True, windowsClient=value, finalHealth=health())
                    break
                if time.monotonic() - start > 90 + options['sessionSeconds']:
                    raise TimeoutError('Bounded server/client deadline')
                time.sleep(.5)
            result['gpuSamples'] = samples
            result['gpuSampleCount'] = sample_count
            result['minimumSampledGPUFreeMiB'] = minimum_gpu_free
    except Exception as error:
        result['error'] = type(error).__name__ + ': ' + str(error)
        result['errorPhase'] = phase
    finally:
        if child is not None and child.poll() is None:
            child.terminate()
            try:
                child.wait(timeout=2)
            except subprocess.TimeoutExpired:
                child.kill()
                child.wait(timeout=2)
        if child is not None:
            result['childExitCode'] = child.poll()
        result['gpuSamples'] = samples
        result['gpuSampleCount'] = sample_count
        result['minimumSampledGPUFreeMiB'] = minimum_gpu_free
        atomic(folder / 'worker-result.json', result)


def run(policy):
    if os.geteuid() != 0 or Path('/proc/1/comm').read_text().strip() != 'systemd':
        raise RuntimeError('Existing Linux root/systemd required')
    options = validate_host(policy)
    invocation = None
    result = None
    control = None
    control_survived = False
    try:
        if options['probe'] == 'cancel-ready':
            control = subprocess.Popen(['/usr/bin/python3', '-B', '-c',
                                         'import time; time.sleep(' + str(120 + options['sessionSeconds']) + ')',
                                         'MagicMR-control-' + policy.nonce])
            identity = proc_identity(control.pid)
            identity['cgroup'] = (Path('/proc') / str(control.pid) / 'cgroup').read_text().strip()
            if policy.unit in identity['cgroup']:
                raise ValueError('Independent control belongs to the server service')
            atomic(policy.folder() / 'control.json', identity)
        subprocess.run(command(policy, options), check=True, timeout=8)
        end = time.monotonic() + 115 + options['sessionSeconds']
        while True:
            state = properties(policy)
            if state.get('ActiveState') == 'active':
                invocation = owned_identity(policy, state, invocation)
            path = policy.folder() / 'worker-result.json'
            if path.exists():
                result = read_json(path)
                break
            if invocation and state.get('ActiveState') not in ('active', 'activating'):
                raise RuntimeError('Owned server service ended before its receipt')
            if time.monotonic() > end:
                raise TimeoutError('Owned server service deadline')
            time.sleep(.2)
        if not invocation:
            raise RuntimeError('Owned server invocation was not observed')
        if options['probe'] == 'cancel-ready':
            if result.get('error') != 'ValueError: Host lease expired during production server check':
                raise RuntimeError('Server probe failed before verified cancellation: ' + str(result.get('error')))
            validate_cancel(policy, result,
                            read_json(policy.folder() / 'windows-initial.json'),
                            read_json(policy.folder() / 'windows-active.json'),
                            read_json(policy.folder() / 'probe-trigger.json'),
                            read_json(policy.folder() / 'client-admit.json'))
        elif not result.get('passed'):
            raise RuntimeError('Production loopback worker failed: ' + str(result.get('error')))
    finally:
        try:
            if invocation:
                stop_owned(policy, invocation)
        finally:
            if control is not None:
                control_survived = control.poll() is None
                if control_survived:
                    control.terminate()
                    try:
                        control.wait(timeout=2)
                    except subprocess.TimeoutExpired:
                        control.kill()
                        control.wait(timeout=2)
    if options['probe'] == 'cancel-ready' and not control_survived:
        raise RuntimeError('Independent control did not survive server cleanup')
    identities = [read_json(policy.folder() / (n + '.json')) for n in ('worker', 'child')]
    if control is not None:
        identities.append(read_json(policy.folder() / 'control.json'))
    end = time.monotonic() + 4
    while any(same_live(i) for i in identities):
        if time.monotonic() > end:
            raise RuntimeError('Server process survived service cleanup')
        time.sleep(.1)
    final = {'passed': True, 'mode': 'BOUNDED_PRODUCTION_LOOPBACK_CHECK', 'unit': policy.unit,
             'invocation': invocation, 'ownedIdentities': identities, 'allOwnedNotRunnable': True,
             'worker': result, 'sessionSeconds': options['sessionSeconds'],
             'expectedCancellation': options['probe'] == 'cancel-ready',
             'independentControlSurvivedServiceCleanup': control_survived, 'picoVerified': False}
    atomic(policy.folder() / 'server-result.json', final)
    print(json.dumps({'passed': True, 'unit': policy.unit}), flush=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('mode', choices=('run', 'worker', 'serve'))
    parser.add_argument('--run-id', required=True)
    parser.add_argument('--nonce', required=True)
    args = parser.parse_args()
    policy = ServerPolicy(args.run_id, args.nonce)
    {'run': run, 'worker': worker, 'serve': serve}[args.mode](policy)


if __name__ == '__main__':
    main()
