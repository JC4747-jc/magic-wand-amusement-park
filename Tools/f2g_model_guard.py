"""Bounded, offline official-teapot F2G check; no production server or training."""
import argparse
import ast
from dataclasses import dataclass
import hashlib
import json
import math
import os
from pathlib import Path
import subprocess
import sys
import time

from f2g_service_guard import (Policy, Lease, atomic, read_json, properties,
                               owned_identity, stop_owned, proc_identity, same_live)

ROOT = Path(__file__).resolve().parents[1]
RUNTIME = Path('/opt/f2g/runtime')
PYTHON = '/opt/f2g/venv/bin/python'
SOURCE_PATHS = ('Tools/f2g_model_guard.py', 'Tools/f2g_service_guard.py',
                'Tools/check_f2g_inference.py', 'Tools/run_f2g_model_bounded.ps1',
                'Tools/build_process_job.ps1', 'Tools/setup_f2g_runtime.py')
MEMORY_MAX = 4 * 1024**3
GPU_ADMIT_MIB = 4800
GPU_FLOOR_MIB = 256
ALLOCATOR_MIB = 4608
FILE_MAX = 16 * 1024**2
LOG_MAX = 2 * 1024**2


@dataclass(frozen=True)
class ModelPolicy(Policy):
    @property
    def description(self):
        return 'MagicMR owned F2G model check ' + self.nonce

    def folder(self, root=ROOT):
        return root / '.codex-tmp/f2g-model-guard' / self.run_id / self.nonce


def digest(path):
    value = hashlib.sha256()
    with path.open('rb') as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b''):
            value.update(chunk)
    return value.hexdigest()


def validate_host(policy):
    host = read_json(policy.folder() / 'host.json')
    if host.get('nonce') != policy.nonce or host.get('unit') != policy.unit:
        raise ValueError('Host ownership mismatch')
    bindings = host.get('inputs', [])
    if {r['path'] for r in bindings} != set(SOURCE_PATHS) or len(bindings) != len(SOURCE_PATHS):
        raise ValueError('Unexpected source allowlist')
    for row in bindings:
        if digest(ROOT / row['path']) != row['sha256']:
            raise ValueError('Source changed: ' + row['path'])
    probe_mode(host)
    return host


def probe_mode(host):
    value = host.get('probe', 'none')
    if value not in ('none', 'cancel', 'lease-expiry', 'log-limit'):
        raise ValueError('Unknown fixed model probe')
    return value


def observe_host_lease(path, lease, now):
    try:
        value = read_json(path)
    except FileNotFoundError:
        # Windows File.Replace can be momentarily invisible through WSL's mount.
        # Never renew on a missing read: preserve the last valid two-second deadline.
        return lease.changed is not None and now - lease.changed < lease.timeout
    return lease.observe(value, now)


def validate_probe(policy, mode, result, resident, trigger, child):
    expected = {'cancel': 'ValueError: Host lease expired during model execution',
                'lease-expiry': 'ValueError: Host lease expired during model execution',
                'log-limit': 'ValueError: Task log exceeded budget'}
    if mode not in expected or result.get('passed') is not False or result.get('error') != expected[mode]:
        raise ValueError('Expected model fault was not observed')
    if result.get('childExitCode') not in (-15, -9):
        raise ValueError('Expected owned model termination was not observed')
    for receipt in (resident, trigger):
        if receipt.get('nonce') != policy.nonce or receipt.get('probe') != mode:
            raise ValueError('Probe receipt ownership mismatch')
    cuda = resident.get('cudaAllocatedMiB')
    if (type(resident.get('pid')) is not int or resident['pid'] <= 0
            or type(resident.get('startTicks')) is not int or resident['startTicks'] <= 0
            or any(resident.get(key) != child.get(key) for key in ('pid', 'startTicks'))
            or not isinstance(cuda, (int, float)) or not math.isfinite(cuda) or cuda <= 0
            or resident.get('firstRealPredictionCompleted') is not True
            or resident.get('rotationSanityPassed') is not True
            or trigger.get('modelPid') != resident.get('pid')
            or trigger.get('modelStartTicks') != resident.get('startTicks')):
        raise ValueError('Probe did not target a verified resident model')
    if mode == 'log-limit' and trigger.get('generatedBytes') != LOG_MAX + 1:
        raise ValueError('Unexpected bounded log fixture size')


def gpu_snapshot():
    result = subprocess.run(['/usr/lib/wsl/lib/nvidia-smi', '--id=0',
                             '--query-gpu=memory.total,memory.free',
                             '--format=csv,noheader,nounits'],
                            capture_output=True, text=True, timeout=5, check=True)
    return parse_gpu(result.stdout)


def parse_gpu(value):
    fields = value.strip().split(',')
    if len(fields) != 2 or not all(s.strip().isdigit() for s in fields):
        raise ValueError('Unknown/multiple GPU memory result')
    total, free = map(int, fields)
    if not 0 < total <= 131072 or not 0 <= free <= total:
        raise ValueError('Invalid GPU memory range')
    return {'totalMiB': total, 'freeMiB': free}


def admit_gpu(snapshot):
    if snapshot['freeMiB'] < GPU_ADMIT_MIB or snapshot['totalMiB'] < ALLOCATOR_MIB:
        raise ValueError('Need >=4800 MiB free GPU memory before model launch')


def model_command(policy):
    return ['systemd-run', '--unit=' + policy.unit, '--description=' + policy.description,
            '--collect', '--no-block', '--property=Type=exec', '--property=Restart=no',
            '--property=KillMode=control-group', '--property=SendSIGKILL=yes',
            '--property=TimeoutStopSec=3s', '--property=RuntimeMaxSec=120s',
            '--property=MemoryAccounting=yes', '--property=MemoryMax=' + str(MEMORY_MAX),
            '--property=MemorySwapMax=0', '--property=TasksMax=64',
            '--property=CPUQuota=100%', '--property=OOMPolicy=kill',
            '--property=LimitFSIZE=' + str(FILE_MAX), '--property=PrivateNetwork=yes',
            '--property=StandardOutput=null', '--property=StandardError=null',
            '/usr/bin/python3', '-B', str(ROOT / 'Tools/f2g_model_guard.py'), 'worker',
            '--run-id', policy.run_id, '--nonce', policy.nonce]


def verify_limits(policy):
    line = next(x for x in Path('/proc/self/cgroup').read_text().splitlines() if x.startswith('0::'))
    group = (Path('/sys/fs/cgroup') / line.split(':', 2)[2].lstrip('/')).resolve()
    if not group.is_relative_to(Path('/sys/fs/cgroup')) or group.name != policy.unit:
        raise ValueError('Worker escaped owned cgroup')
    expected = {'memory.max': str(MEMORY_MAX), 'memory.swap.max': '0', 'pids.max': '64'}
    observed = {key: (group / key).read_text().strip() for key in expected}
    if expected != observed:
        raise ValueError('Kernel limits differ')
    observed['cpu.max'] = (group / 'cpu.max').read_text().strip()
    quota, period = observed['cpu.max'].split()
    if quota == 'max' or not 0 < int(quota) <= int(period):
        raise ValueError('CPU quota missing')
    if os.readlink('/proc/self/ns/net') == os.readlink('/proc/1/ns/net'):
        raise ValueError('Offline check did not receive a private network namespace')
    return str(group), observed


def model_inputs():
    files = [RUNTIME / 'weights/f2g-pose.pt', RUNTIME / 'weights/radio-v2.5-l_half.pth.tar']
    files += [RUNTIME / 'F2G-Pose/examples/rope' / name for name in ('rgb.png', 'depth.npy', 'mask.png', 'K.txt')]
    # Bind the local implementation as well as weights/sample; no environment installation.
    package = RUNTIME / 'F2G-Pose/src/f2g_pose'
    if not package.is_dir():
        raise ValueError('Expected existing F2G implementation missing')
    files += sorted(package.rglob('*.py'))
    files += [RUNTIME / 'RADIO/hubconf.py']
    files += sorted((RUNTIME / 'RADIO/radio').rglob('*.py'))
    if len(files) > 256:
        raise ValueError('Unexpected implementation inventory')
    tree = ast.parse((ROOT / 'Tools/setup_f2g_runtime.py').read_text('utf-8'))
    downloads = next(ast.literal_eval(node.value) for node in ast.walk(tree)
                     if isinstance(node, ast.Assign) and any(isinstance(t, ast.Name) and t.id == 'downloads' for t in node.targets))
    pins = {name: expected for url, expected, name in downloads}
    rows = []
    for p in files:
        row = {'path': str(p), 'resolvedPath': str(p.resolve()), 'bytes': p.stat().st_size,
               'mtimeNs': p.stat().st_mtime_ns, 'sha256': digest(p)}
        if p.parent == RUNTIME / 'weights' and row['sha256'] != pins[p.name]:
            raise ValueError('Existing official weight pin mismatch: ' + p.name)
        rows.append(row)
    return rows


def check_growth(folder):
    files = []
    for path in folder.rglob('*'):
        try:
            if path.is_file():
                files.append((path, path.stat().st_size))
        except FileNotFoundError:
            # Owned atomic metadata may vanish between enumeration and stat.
            # Missing metadata does not renew the separate lease deadline.
            continue
        if len(files) > 128:
            raise ValueError('Task files exceeded budget')
    if sum(size for path, size in files) > FILE_MAX:
        raise ValueError('Task files exceeded budget')
    if any(size > LOG_MAX for path, size in files if path.suffix == '.log'):
        raise ValueError('Task log exceeded budget')


def infer(policy):
    # Heavy imports occur only in the admitted, cgroup-owned, network-isolated child.
    host = validate_host(policy)
    verify_limits(policy)
    folder = policy.folder()
    bindings = read_json(folder / 'model-inputs.json', cap=131072)
    for row in bindings:
        stat = Path(row['path']).stat()
        if stat.st_size != row['bytes'] or stat.st_mtime_ns != row['mtimeNs']:
            raise ValueError('Bound model input changed after hashing')
    admit_gpu(gpu_snapshot())
    import torch
    import runpy
    total_mib = torch.cuda.get_device_properties(0).total_memory / 1024**2
    torch.cuda.set_per_process_memory_fraction(ALLOCATOR_MIB / total_mib, device=0)
    mode = probe_mode(host)
    if mode != 'none':
        # Only this probe process wraps the method. The original prediction runs
        # once; then a bounded pause keeps the real CUDA model resident for the
        # controller. This does not claim interruption inside a CUDA kernel.
        from f2g_pose import PoseEstimator
        import numpy as np
        original = PoseEstimator.predict

        def predict_then_hold(self, *args, **kwargs):
            answer = original(self, *args, **kwargs)
            torch.cuda.synchronize()
            rotation = np.asarray(answer['rotation'])
            if (rotation.shape != (3, 3) or not np.isfinite(rotation).all()
                    or not np.allclose(rotation.T @ rotation, np.eye(3), atol=.015)
                    or abs(np.linalg.det(rotation) - 1) >= .015):
                raise ValueError('Probe prediction rotation failed sanity checks')
            origin = str(Path(sys.modules['f2g_pose'].__file__).resolve())
            if origin not in {row['resolvedPath'] for row in bindings}:
                raise ValueError('Probe model origin is outside bound inputs')
            atomic(folder / 'model-resident.json', {
                **proc_identity(os.getpid()), 'nonce': policy.nonce, 'probe': mode,
                'firstRealPredictionCompleted': True, 'rotationSanityPassed': True,
                'rotation': rotation.tolist(), 'modelOrigin': origin,
                'cudaAllocatedMiB': torch.cuda.memory_allocated() / 1024**2,
                'cudaReservedMiB': torch.cuda.memory_reserved() / 1024**2,
                'point': 'After one real prediction; model resident, no active-kernel claim'})
            deadline = time.monotonic() + 20
            while time.monotonic() < deadline:
                time.sleep(.1)
            raise TimeoutError('Controller failed to stop the resident model probe')

        PoseEstimator.predict = predict_then_hold
    sys.argv = [str(ROOT / 'Tools/check_f2g_inference.py'), '--root', str(RUNTIME),
                '--output', str(folder / 'inference.json')]
    runpy.run_path(str(ROOT / 'Tools/check_f2g_inference.py'), run_name='__main__')
    report = read_json(folder / 'inference.json')
    origin = str(Path(sys.modules['f2g_pose'].__file__).resolve())
    if origin not in {row['resolvedPath'] for row in bindings}:
        raise ValueError('Actual model origin is outside the bound implementation')
    report['guard'] = {'allocatorMiB': ALLOCATOR_MIB, 'networkIsolated': True,
                       'modelOrigin': origin,
                       'python': sys.executable, 'notLighterOrPicoValidation': True}
    atomic(folder / 'inference.json', report)
    for row in bindings:
        stat = Path(row['path']).stat()
        if stat.st_size != row['bytes'] or stat.st_mtime_ns != row['mtimeNs']:
            raise ValueError('Bound model input changed during inference')


def worker(policy):
    folder = policy.folder()
    child = None
    result = {'passed': False, 'mode': 'OFFLINE_ROPE_MODEL_CHECK'}
    try:
        validate_host(policy)
        group, limits = verify_limits(policy)
        atomic(folder / 'worker.json', proc_identity(os.getpid()))
        result.update(cgroup=group, kernelLimits=limits)
        bindings = model_inputs()
        atomic(folder / 'model-inputs.json', bindings)
        lease = Lease(policy.nonce)
        if not observe_host_lease(folder / 'lease.json', lease, time.monotonic()):
            raise ValueError('Host lease expired before admission')
        gpu = gpu_snapshot()
        admit_gpu(gpu)
        result['gpuAdmission'] = gpu
        env = dict(os.environ, OMP_NUM_THREADS='1', MKL_NUM_THREADS='1', MAX_JOBS='1',
                   HF_HUB_OFFLINE='1', TRANSFORMERS_OFFLINE='1', PYTHONDONTWRITEBYTECODE='1',
                   CUDA_CACHE_DISABLE='1', HF_HOME=str(folder / 'hf-cache'))
        with (folder / 'model.log').open('wb') as log:
            child = subprocess.Popen([PYTHON, '-B', str(ROOT / 'Tools/f2g_model_guard.py'),
                                      'infer', '--run-id', policy.run_id, '--nonce', policy.nonce],
                                     stdout=log, stderr=subprocess.STDOUT, env=env)
            atomic(folder / 'child.json', proc_identity(child.pid))
            started = time.monotonic()
            samples = []
            while child.poll() is None:
                if not observe_host_lease(folder / 'lease.json', lease, time.monotonic()):
                    raise ValueError('Host lease expired during model execution')
                check_growth(folder)
                gpu = gpu_snapshot()
                samples.append({'elapsedSeconds': round(time.monotonic() - started, 2), **gpu})
                if gpu['freeMiB'] < GPU_FLOOR_MIB:
                    raise ValueError('GPU free memory floor crossed')
                if time.monotonic() - started > 90:
                    raise TimeoutError('Model check deadline')
                time.sleep(.5)
            result['gpuSamples'] = samples
            if child.returncode != 0:
                raise RuntimeError('Model child exited ' + str(child.returncode))
        inference = read_json(folder / 'inference.json')
        if len(inference.get('warm_inference_ms', [])) != 4 or not inference.get('pose'):
            raise ValueError('Incomplete official inference output')
        if any(Path(r['path']).stat().st_size != r['bytes'] or
               Path(r['path']).stat().st_mtime_ns != r['mtimeNs'] for r in bindings):
            raise ValueError('Model inputs changed during execution')
        validate_host(policy)
        result.update(passed=True, inference=inference)
    except Exception as error:
        result['error'] = type(error).__name__ + ': ' + str(error)
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
        atomic(folder / 'worker-result.json', result)


def run(policy):
    if os.geteuid() != 0 or Path('/proc/1/comm').read_text().strip() != 'systemd':
        raise RuntimeError('Existing Linux systemd/root required; no restart or configuration')
    host = validate_host(policy)
    mode = probe_mode(host)
    invocation = None
    result = None
    control = None
    control_survived = False
    try:
        if mode != 'none':
            control = subprocess.Popen(['/usr/bin/python3', '-B', '-c', 'import time; time.sleep(120)'])
            identity = proc_identity(control.pid)
            identity['cgroup'] = Path('/proc') / str(control.pid) / 'cgroup'
            identity['cgroup'] = identity['cgroup'].read_text().strip()
            if policy.unit in identity['cgroup']:
                raise ValueError('Independent control unexpectedly belongs to the model service')
            atomic(policy.folder() / 'control.json', identity)
        subprocess.run(model_command(policy), timeout=8, check=True)
        deadline = time.monotonic() + 115
        while True:
            state = properties(policy)
            if state.get('ActiveState') == 'active':
                invocation = owned_identity(policy, state, invocation)
            path = policy.folder() / 'worker-result.json'
            if path.exists():
                result = read_json(path)
                break
            if invocation and state.get('ActiveState') not in ('active', 'activating'):
                raise RuntimeError('Owned service ended before publishing a result: ' + state.get('ActiveState', 'unknown'))
            if time.monotonic() > deadline:
                raise TimeoutError('Owned model service deadline')
            time.sleep(.2)
        if not invocation:
            raise RuntimeError('Owned service invocation was not observed')
        if mode != 'none':
            expected = ('ValueError: Task log exceeded budget' if mode == 'log-limit'
                        else 'ValueError: Host lease expired during model execution')
            if result.get('error') != expected:
                raise RuntimeError('Probe failed before its verified trigger: ' + str(result.get('error')))
            validate_probe(policy, mode, result,
                           read_json(policy.folder() / 'model-resident.json'),
                           read_json(policy.folder() / 'probe-trigger.json'),
                           read_json(policy.folder() / 'child.json'))
        elif not result.get('passed'):
            raise RuntimeError('Model worker failed: ' + str(result.get('error')))
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
    if mode != 'none' and not control_survived:
        raise RuntimeError('Independent control did not survive model cleanup')
    identities = [read_json(policy.folder() / (name + '.json')) for name in ('worker', 'child')]
    if mode != 'none':
        identities.append(read_json(policy.folder() / 'control.json'))
    deadline = time.monotonic() + 4
    while any(same_live(row) for row in identities):
        if time.monotonic() > deadline:
            raise TimeoutError('Owned processes survived service cleanup')
        time.sleep(.1)
    final = {'passed': True, 'mode': 'OFFLINE_ROPE_MODEL_CHECK', 'unit': policy.unit,
             'invocation': invocation, 'allOwnedNotRunnable': True, 'ownedIdentities': identities,
             'modelResult': result, 'probe': mode, 'expectedFaultObserved': mode != 'none',
             'independentControlSurvivedServiceCleanup': control_survived,
             'notProductionOrPicoValidation': True}
    atomic(policy.folder() / 'model-check-result.json', final)
    print(json.dumps({'passed': True, 'unit': policy.unit}), flush=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('mode', choices=('run', 'worker', 'infer'))
    parser.add_argument('--run-id', required=True)
    parser.add_argument('--nonce', required=True)
    args = parser.parse_args()
    policy = ModelPolicy(args.run_id, args.nonce)
    {'run': run, 'worker': worker, 'infer': infer}[args.mode](policy)


if __name__ == '__main__':
    main()
