"""No-model Linux ownership self-test using a transient systemd service.

Real-model launch is deliberately not exposed until this protection is verified.
The portable policy/lease functions can be checked on Windows without starting WSL.
"""
import argparse
from dataclasses import dataclass
import hashlib
import json
import os
from pathlib import Path
import platform
import re
import secrets
import subprocess
import sys
import time

ROOT=Path(__file__).resolve().parents[1]


@dataclass(frozen=True)
class Policy:
    run_id: str
    nonce: str

    def __post_init__(self):
        if not re.fullmatch(r'[a-z0-9][a-z0-9-]{0,45}',self.run_id):raise ValueError('Unsafe run ID')
        if not re.fullmatch(r'[0-9a-f]{32}',self.nonce):raise ValueError('Unsafe ownership nonce')

    @property
    def unit(self):return f'magicmr-f2g-{self.run_id}-{self.nonce}.service'
    @property
    def description(self):return 'MagicMR owned F2G self-test '+self.nonce
    def folder(self,root=ROOT):return root/'.codex-tmp/f2g-service-guard'/self.run_id/self.nonce


def command(policy,root):
    if not str(root).startswith('/'):raise ValueError('Need an absolute Linux project root')
    return ['systemd-run','--unit='+policy.unit,'--description='+policy.description,'--collect','--no-block',
            '--property=Type=exec','--property=Restart=no','--property=KillMode=control-group',
            '--property=SendSIGKILL=yes','--property=TimeoutStopSec=3s','--property=RuntimeMaxSec=15s',
            '--property=MemoryAccounting=yes','--property=MemoryMax=134217728','--property=MemorySwapMax=0',
            '--property=TasksMax=32','--property=CPUQuota=100%','--property=OOMPolicy=kill',
            '--property=LimitFSIZE=8388608','--property=StandardOutput=null','--property=StandardError=null',
            '/usr/bin/python3','-B',str(root/'Tools/f2g_service_guard.py'),'worker',
            '--run-id',policy.run_id,'--nonce',policy.nonce]


def owned_identity(policy,properties,invocation=None):
    if properties.get('LoadState')=='not-found':return False
    if properties.get('Id')!=policy.unit or properties.get('Transient')!='yes' or properties.get('Description')!=policy.description:
        raise ValueError('Refusing operation on a foreign/non-transient service')
    actual=properties.get('InvocationID','')
    if not re.fullmatch(r'[0-9a-f]{32}',actual):raise ValueError('Missing invocation identity')
    if invocation is not None and invocation!=actual:raise ValueError('Service invocation changed')
    return actual


class Lease:
    def __init__(self,nonce,timeout=2):self.nonce=nonce;self.timeout=timeout;self.seq=None;self.changed=None
    def observe(self,value,now):
        if value.get('nonce')!=self.nonce or type(value.get('seq')) is not int or value['seq']<0:
            raise ValueError('Invalid lease ownership/sequence')
        if self.seq is not None and value['seq']<self.seq:raise ValueError('Lease sequence moved backwards')
        if value.get('cancel') is True:return False
        if self.seq!=value['seq']:self.seq=value['seq'];self.changed=now
        return now-self.changed<self.timeout


def read_json(path,cap=16384):
    with path.open('rb') as stream:raw=stream.read(cap+1)
    if len(raw)>cap:raise ValueError('Oversized control file')
    return json.loads(raw)


def atomic(path,value):
    tmp=path.with_name(path.name+'.tmp');tmp.write_text(json.dumps(value,indent=2)+'\n','utf-8');tmp.replace(path)


def properties(policy):
    p=subprocess.run(['systemctl','show',policy.unit,'--no-pager',
        '--property=Id,LoadState,ActiveState,Description,Transient,InvocationID,ControlGroup'],capture_output=True,text=True,timeout=5)
    result=dict(line.split('=',1) for line in p.stdout.splitlines() if '=' in line)
    if not result:raise RuntimeError('Unable to inspect the exact service')
    return result


def stop_owned(policy,invocation):
    state=properties(policy)
    if owned_identity(policy,state,invocation):
        subprocess.run(['systemctl','stop',policy.unit],check=True,timeout=8)


def proc_identity(pid):
    try:
        fields=Path(f'/proc/{pid}/stat').read_text().rsplit(') ',1)[1].split()
        return {'pid':pid,'startTicks':int(fields[19]),'state':fields[0]}
    except FileNotFoundError:return None


def same_live(identity):
    now=proc_identity(identity['pid'])
    return bool(now and now['startTicks']==identity['startTicks'] and now['state']!='Z')


def verify_cgroup(policy):
    entries=Path('/proc/self/cgroup').read_text().splitlines()
    relative=next(line.split(':',2)[2] for line in entries if line.startswith('0::'))
    group=(Path('/sys/fs/cgroup')/relative.lstrip('/')).resolve()
    if not group.is_relative_to(Path('/sys/fs/cgroup')) or group.name!=policy.unit:raise ValueError('Unexpected cgroup')
    expected={'memory.max':'134217728','memory.swap.max':'0','pids.max':'32'}
    for name,value in expected.items():
        if (group/name).read_text().strip()!=value:raise ValueError('Kernel limit not applied: '+name)
    quota,period=(group/'cpu.max').read_text().split()
    if quota=='max' or not 0<int(quota)<=int(period):raise ValueError('CPU quota not applied')
    return str(group)


def fixture(folder,role):
    atomic(folder/(role+'.json'),proc_identity(os.getpid()))
    if role=='parent':subprocess.Popen([sys.executable,'-B',__file__,'fixture','--folder',str(folder),'--role','child'])
    time.sleep(30)


def worker(policy):
    folder=policy.folder();result={'mode':'NO_MODEL_SELF_TEST','nonce':policy.nonce};child=None
    try:
        invocation=os.environ.get('INVOCATION_ID','')
        deadline=time.monotonic()+5
        while not (folder/'owned.json').exists():
            if time.monotonic()>deadline:raise TimeoutError('Ownership gate not released')
            time.sleep(.05)
        owned=read_json(folder/'owned.json')
        if owned.get('unit')!=policy.unit or owned.get('nonce')!=policy.nonce or owned.get('invocation')!=invocation:
            raise ValueError('Worker invocation gate mismatch')
        result['cgroup']=verify_cgroup(policy)
        lease=Lease(policy.nonce)
        if not lease.observe(read_json(folder/'lease.json'),time.monotonic()):raise ValueError('Cancelled before start')
        atomic(folder/'worker.json',proc_identity(os.getpid()))
        child=subprocess.Popen([sys.executable,'-B',__file__,'fixture','--folder',str(folder),'--role','parent'])
        while lease.observe(read_json(folder/'lease.json'),time.monotonic()):
            if child.poll() is not None:raise RuntimeError('Owned sleep fixture exited early')
            time.sleep(.1)
        result['reason']='lease_expired_or_cancelled'
    except Exception as error:
        result['error']=type(error).__name__+': '+str(error)
    finally:
        # Remaining grandchildren are cleaned by systemd's control-group policy.
        if child and child.poll() is None:
            child.terminate()
            try:child.wait(timeout=1)
            except subprocess.TimeoutExpired:child.kill();child.wait(timeout=1)
        atomic(folder/'worker-result.json',result)
    return 1 if 'error' in result else 0


def prepare_folder(policy, host_controlled=False):
    folder = policy.folder()
    if not host_controlled:
        folder.mkdir(parents=True, exist_ok=False)
        atomic(folder/'lease.json', {'nonce': policy.nonce, 'seq': 0, 'cancel': False})
        return folder
    marker = read_json(folder/'host.json')
    if (marker.get('nonce') != policy.nonce or marker.get('unit') != policy.unit
            or marker.get('mode') != 'NO_MODEL_SELF_TEST'):
        raise ValueError('Host ownership marker mismatch')
    actual = hashlib.sha256(Path(__file__).read_bytes()).hexdigest()
    if marker.get('scriptSha256') != actual:
        raise ValueError('Host/Linux script hashes differ')
    if any((folder/name).exists() for name in (
            'owned.json', 'worker.json', 'parent.json', 'child.json',
            'self-test-result.json', 'self-test-failed.json')):
        raise ValueError('Host run directory contains prior execution')
    if not Lease(policy.nonce).observe(read_json(folder/'lease.json'), time.monotonic()):
        raise ValueError('Host cancelled before start')
    return folder


def self_test(policy, host_controlled=False):
    if platform.system()!='Linux' or os.geteuid()!=0:raise RuntimeError('Linux root self-test only')
    if Path('/proc/1/comm').read_text().strip()!='systemd':raise RuntimeError('systemd PID 1 required; no automatic restart/configuration')
    if not Path('/sys/fs/cgroup/cgroup.controllers').is_file():raise RuntimeError('Unified cgroup v2 required')
    folder=prepare_folder(policy, host_controlled)
    invocation=None;control=None;passed=False;details={}
    try:
        # Independent, task-owned control: intentionally outside the service.
        control=subprocess.Popen([sys.executable,'-B',__file__,'fixture','--folder',str(folder),'--role','control'],start_new_session=True)
        subprocess.run(command(policy,ROOT),check=True,timeout=8)
        deadline=time.monotonic()+5
        while True:
            state=properties(policy)
            if state.get('ActiveState')=='active':
                invocation=owned_identity(policy,state);break
            if time.monotonic()>deadline:raise TimeoutError('Transient service did not activate')
            time.sleep(.1)
        atomic(folder/'owned.json',{'unit':policy.unit,'nonce':policy.nonce,'invocation':invocation})
        deadline=time.monotonic()+5;seq=0
        while not (folder/'child.json').exists():
            if not host_controlled:
                seq+=1;atomic(folder/'lease.json',{'nonce':policy.nonce,'seq':seq,'cancel':False})
            if (folder/'worker-result.json').exists():raise RuntimeError('Worker failed before fixture creation')
            if time.monotonic()>deadline:raise TimeoutError('Fixture child did not start')
            time.sleep(.1)
        identities=[read_json(folder/(role+'.json')) for role in ('worker','parent','child')]
        # Stop renewing the lease to model loss of the Windows controller.
        deadline=time.monotonic()+8
        while any(same_live(i) for i in identities):
            if time.monotonic()>deadline:raise TimeoutError('Service descendants survived lease expiry')
            time.sleep(.1)
        details=read_json(folder/'worker-result.json')
        if details.get('error') or details.get('reason')!='lease_expired_or_cancelled':raise RuntimeError('Unexpected worker exit')
        if control.poll() is not None:raise RuntimeError('Independent control did not survive')
        passed=True
        result={'passed':True,'mode':'NO_MODEL_SELF_TEST','unit':policy.unit,'invocation':invocation,
                'hostControlledLease':host_controlled,
                'ownedIdentities':identities,'allOwnedNotRunnable':True,'independentControlSurvived':True,'worker':details}
    finally:
        try:
            if not host_controlled:
                atomic(folder/'lease.json',{'nonce':policy.nonce,'seq':1000000,'cancel':True})
            if invocation:stop_owned(policy,invocation)
        finally:
            # A service-inspection failure must not skip our independent control.
            if control and control.poll() is None:
                control.terminate()
                try:control.wait(timeout=2)
                except subprocess.TimeoutExpired:control.kill();control.wait(timeout=2)
            if not passed:atomic(folder/'self-test-failed.json',{'unit':policy.unit,'error':'See caller diagnostics; no model launch was exposed'})
    # Publish success only after cleanup also returns successfully.
    atomic(folder/'self-test-result.json',result)
    print(json.dumps(result),flush=True)


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('action',choices=['self-test','worker','fixture'])
    parser.add_argument('--run-id',default='ownership-check')
    parser.add_argument('--nonce')
    parser.add_argument('--host-controlled', action='store_true')
    parser.add_argument('--folder',type=Path)
    parser.add_argument('--role',choices=['parent','child','control'])
    args=parser.parse_args()
    if platform.system()!='Linux':parser.error('Execution is Linux-only; use unit tests for portable contracts')
    if args.action=='fixture':
        if not args.role or not args.folder or not args.folder.resolve().is_relative_to((ROOT/'.codex-tmp/f2g-service-guard').resolve()):parser.error('Fixture outside owned root or missing role')
        fixture(args.folder,args.role);return
    policy=Policy(args.run_id,args.nonce or secrets.token_hex(16))
    if args.action=='worker':raise SystemExit(worker(policy))
    self_test(policy, args.host_controlled)


if __name__=='__main__':main()
