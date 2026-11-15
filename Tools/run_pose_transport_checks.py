"""Compile and run the production C# socket fault checks using Unity's bundled runtime."""
import argparse
import json
import os
from pathlib import Path
import subprocess

root = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--unity-data', type=Path,
                    default=Path(os.environ.get('UNITY_EDITOR_DATA', 'D:/Unity/Editor/Data')),
                    help='Unity Editor Data folder (or set UNITY_EDITOR_DATA)')
args = parser.parse_args()
editor = args.unity_data.resolve()
runtime_root = editor/'NetCoreRuntime/shared/Microsoft.NETCore.App'
runtimes = [p for p in runtime_root.glob('6.0.*')
            if p.is_dir() and p.name.rsplit('.', 1)[-1].isdigit()]
dotnet = editor/'NetCoreRuntime/dotnet.exe'
compiler = editor/'DotNetSdkRoslyn/csc.dll'
if not dotnet.is_file() or not compiler.is_file() or not runtimes:
    parser.error(f'Unity bundled .NET 6 runtime/compiler unavailable at {editor}. '
                 'Supply --unity-data for your installation; no C# checks have run.')
runtime = max(runtimes, key=lambda p: int(p.name.rsplit('.', 1)[-1]))
output = root/'.codex-tmp/pose-transport-checks'
output.mkdir(parents=True, exist_ok=True)
assembly = output/'checks.dll'
lines = ['-nologo', '-target:exe', '-out:"'+assembly.as_posix()+'"']
lines += ['-r:"'+p.as_posix()+'"' for p in runtime.glob('*.dll')
          if (p.name.startswith('System.') and '.Native.' not in p.name)
          or p.name in ('mscorlib.dll','netstandard.dll','Microsoft.CSharp.dll')]
lines += ['"'+(root/p).as_posix()+'"' for p in ['Assets/Scripts/Perception/Stereo/PoseTransport.cs', 'Tools/PoseTransportChecks.cs']]
rsp = output/'checks.rsp'
rsp.write_text('\n'.join(lines), encoding='utf-8')
(output/'checks.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net6.0','framework':{'name':'Microsoft.NETCore.App','version':runtime.name}}}), encoding='utf-8')
subprocess.run([str(dotnet),str(compiler),'@'+str(rsp)],check=True,cwd=root)
subprocess.run([str(dotnet),str(assembly)],check=True,cwd=root,timeout=30)
