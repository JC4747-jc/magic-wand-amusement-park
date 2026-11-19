"""Read-only post-check of explicit owned model/server runs; no signals or model load."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[1]
REPORT_ROOTS = tuple((ROOT / p).resolve() for p in (
    'Reports/f2g-model-admission-20261119', 'Reports/f2g-model-faults-20261119',
    'Reports/f2g-production-loopback-20261119', 'Reports/f2g-controlled-service-20261119'))


def read(path):
    if path.stat().st_size > 131072:
        raise ValueError('Oversized receipt')
    return json.loads(path.read_text('utf-8-sig'))


def linux_path(path, server=False):
    path = path.resolve()
    owned = '.codex-tmp/f2g-server-guard' if server else '.codex-tmp/f2g-model-guard'
    if not path.is_relative_to(ROOT.resolve() / owned):
        raise ValueError('Run folder outside the owned model root')
    value = str(path)
    if not re.match(r'^[A-Za-z]:[\\/]', value):
        raise ValueError('Need the known Windows drive path')
    return '/mnt/' + value[0].lower() + '/' + value[3:].replace('\\', '/')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--report', type=Path, action='append', required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    target = args.output.resolve()
    if target.exists() or not target.is_relative_to((ROOT / 'Reports').resolve()):
        parser.error('Use a fresh Reports output')
    requests = []
    signatures = []
    bindings = []
    for path in args.report:
        path = path.resolve()
        if not any(path.is_relative_to(base) and path.parent == base for base in REPORT_ROOTS):
            parser.error('Use explicit immediate-child model run reports')
        plan, result = read(path / 'plan.json'), read(path / 'result.json')
        server = path.parent in REPORT_ROOTS[2:]
        expected = ('PASS_CURRENT_PRODUCTION_LOOPBACK', 'PASS_CONTROLLED_PRODUCTION_SESSION', 'PASS_CONTROLLED_SERVER_CANCEL') if server else ('PASS_OFFLINE_ROPE_MODEL_CHECK', 'PASS_RESIDENT_MODEL_GUARD_PROBE')
        if result['status'] not in expected:
            raise ValueError('Run did not pass its declared scope')
        if not result['linuxResult']['allOwnedNotRunnable'] or result['unit'] != plan['unit']:
            raise ValueError('Cleanup or unit identity receipt missing')
        for row in plan['inputs']:
            with (ROOT / row['path']).open('rb') as stream:
                if hashlib.file_digest(stream, 'sha256').hexdigest() != row['sha256']:
                    raise ValueError('Current launch source differs: ' + row['path'])
        folder = Path(plan['runRoot'])
        manifest_name = 'runtime-inputs.json' if server else 'model-inputs.json'
        manifest = folder / manifest_name
        model_inputs = read(manifest)
        signatures.append(sorted((row['path'], row['sha256']) for row in model_inputs))
        bindings = model_inputs
        requests.append({'report': str(path.relative_to(ROOT)), 'folder': linux_path(folder, server),
                         'manifestName': manifest_name,
                         'unit': plan['unit'], 'identities': result['linuxResult']['ownedIdentities'],
                         'manifestSha256': hashlib.sha256(manifest.read_bytes()).hexdigest()})
    if not 1 <= len(requests) <= 8 or any(s != signatures[0] for s in signatures):
        raise ValueError('Need 1-8 runs with the same bound runtime inputs')
    if len(bindings) > 256 or sum(row['bytes'] for row in bindings) > 2 * 1024**3:
        raise ValueError('Runtime rehash exceeds the bounded inventory')
    # Stdlib only in Linux. Inspect birth identities; never send signals or stop a unit.
    code = """import json,pathlib,subprocess,hashlib
requests=json.loads(REQUESTS)
bindings=None
results=[]
for row in requests:
    state=subprocess.run(['systemctl','show',row['unit'],'--property=LoadState,ActiveState,SubState'],capture_output=True,text=True,timeout=5)
    identities=[]
    for identity in row['identities']:
        p=pathlib.Path('/proc')/str(identity['pid'])/'stat'; live=False
        if p.exists():
            fields=p.read_text().rsplit(')',1)[1].split()
            live=int(fields[19])==identity['startTicks'] and fields[0]!='Z'
        identities.append({'pid':identity['pid'],'sameOwnedProcessRunnable':live})
    group=pathlib.Path('/sys/fs/cgroup/system.slice')/row['unit']
    assert 'ActiveState=inactive' in state.stdout and not group.exists()
    assert not any(x['sameOwnedProcessRunnable'] for x in identities)
    assert row['manifestName'] in ('model-inputs.json','runtime-inputs.json')
    raw=(pathlib.Path(row['folder'])/row['manifestName']).read_bytes()
    assert len(raw)<=131072 and hashlib.sha256(raw).hexdigest()==row['manifestSha256']
    bindings=json.loads(raw)
    results.append({'report':row['report'],'unitState':state.stdout.strip(),'cgroupRemoved':True,'processes':identities,'allOwnedNotRunnable':True})
for row in bindings:
    p=pathlib.Path(row['path']); value=hashlib.sha256()
    seen=0
    with p.open('rb') as stream:
        for block in iter(lambda:stream.read(1024*1024),b''):
            seen+=len(block)
            assert seen<=row['bytes']
            value.update(block)
    assert seen==row['bytes']
    assert value.hexdigest()==row['sha256'],row['path']
print(json.dumps({'runs':results,'runtimeInputsRehashed':len(bindings),'runtimeInputBytes':sum(x['bytes'] for x in bindings),'runtimeInputsUnchanged':True,'scope':'Process birth identities, inactive services, absent cgroups and file SHA; no inference or signals'}))
"""
    code = code.replace('REQUESTS', repr(json.dumps(requests)))
    child = subprocess.run(['wsl.exe', '-d', 'Ubuntu-24.04', '-u', 'root', '--exec',
                            '/usr/bin/python3', '-B', '-c', code], capture_output=True, timeout=35)
    if child.returncode != 0 or len(child.stdout) > 131072 or len(child.stderr) > 16384:
        raise RuntimeError('Read-only Linux post-check failed; no retry/launch was performed')
    result = json.loads(child.stdout.decode('utf-8-sig'))
    target.write_text(json.dumps(result, indent=2) + '\n', 'utf-8')
    target.with_suffix('.wsl-stderr.bin').write_bytes(child.stderr)
    print(json.dumps({'runsChecked': len(requests), 'runtimeInputsRehashed': len(bindings),
                      'allOwnedNotRunnable': True, 'runtimeInputsUnchanged': True}))


if __name__ == '__main__':
    main()
