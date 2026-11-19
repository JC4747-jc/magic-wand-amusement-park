"""Snapshot a confirmed-live owned server; read-only, no model imports or signals."""
import argparse
from datetime import datetime, timezone
import json
from pathlib import Path
import socket
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / 'Tools'))
from f2g_server_guard import validate_health
from check_f2g_server_loopback import framed_health


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--report', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    report = args.report.resolve()
    if report.parent != (ROOT / 'Reports/f2g-controlled-service-20261119').resolve():
        parser.error('Use an explicit controlled server report')
    target = args.output.resolve()
    if target.exists() or not target.is_relative_to((ROOT / 'Reports').resolve()):
        parser.error('Use a fresh Reports output')
    plan = json.loads((report / 'plan.json').read_text('utf-8-sig'))
    folder = Path(plan['runRoot']).resolve()
    if not folder.is_relative_to((ROOT / '.codex-tmp/f2g-server-guard').resolve()):
        raise ValueError('Foreign server folder')
    nonce = folder.name
    linux_root = '/mnt/' + ROOT.drive[0].lower() + ROOT.as_posix()[2:]
    code = """import sys,json,pathlib,types
sys.path.insert(0,TOOLS)
from f2g_server_guard import ServerPolicy,validate_host,child_owns_ports
from f2g_service_guard import properties,owned_identity,read_json,same_live
p=ServerPolicy(RUN,NONCE)
validate_host(p)
state=properties(p); invocation=owned_identity(p,state)
assert state['ActiveState']=='active' and invocation
child=read_json(p.folder()/'child.json')
assert same_live(child) and child_owns_ports(types.SimpleNamespace(pid=child['pid']))
group=(pathlib.Path('/sys/fs/cgroup')/state['ControlGroup'].lstrip('/')).resolve()
assert group.is_relative_to(pathlib.Path('/sys/fs/cgroup')) and group.name==p.unit
metrics={}
for name in ('memory.current','memory.peak','pids.current','pids.peak'):
    f=group/name
    metrics[name]=f.read_text().strip() if f.exists() else None
print(json.dumps({'unit':p.unit,'invocation':invocation,'active':True,'childBirthIdentityLive':True,'listenerInodesOwned':True,'cgroupCounters':metrics}))
"""
    code = code.replace('TOOLS', repr(linux_root + '/Tools')).replace('RUN', repr(report.name)).replace('NONCE', repr(nonce))
    process = subprocess.run(['wsl.exe', '-d', 'Ubuntu-24.04', '-u', 'root', '--exec',
                              '/usr/bin/python3', '-B', '-c', code], capture_output=True, timeout=15)
    if process.returncode != 0 or len(process.stdout) > 16384:
        raise RuntimeError('Owned service live check failed; no launch or stop attempted')
    linux = json.loads(process.stdout.decode('utf-8-sig'))
    with socket.create_connection(('127.0.0.1', 5007), timeout=.3) as client:
        client.settimeout(.3)
        with client.makefile('rb') as stream:
            body = stream.readline(16385)
    if len(body) > 16384 or not body.endswith(b'\n'):
        raise ValueError('Incomplete health reply')
    health = json.loads(body)
    validate_health(health)
    image = framed_health()
    gpu = subprocess.run(['nvidia-smi.exe', '--id=0', '--query-gpu=memory.free',
                          '--format=csv,noheader,nounits'], capture_output=True, text=True, timeout=5)
    if gpu.returncode != 0 or not gpu.stdout.strip().isdigit():
        raise ValueError('Unknown GPU memory observation')
    result = {'checkedAtUtc': datetime.now(timezone.utc).isoformat(), 'linux': linux,
              'health': health, 'framedHealth': image, 'gpuFreeMiB': int(gpu.stdout.strip()),
              'scope': 'Current owned unit, process birth identity/listeners and health only; not a final session receipt or continuous peaks'}
    target.write_text(json.dumps(result, indent=2) + '\n', 'utf-8')
    print(json.dumps({'ownedActive': True, 'bothPortsReady': True, 'gpuFreeMiB': result['gpuFreeMiB'],
                      'cgroupMemoryMiB': round(int(linux['cgroupCounters']['memory.current']) / 1024**2, 1)}))


if __name__ == '__main__':
    main()
