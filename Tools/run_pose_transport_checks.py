"""Compile and run the production C# socket fault checks using Unity's bundled runtime."""
import json
from pathlib import Path
import subprocess

root = Path(__file__).resolve().parents[1]
editor = Path('D:/Unity/Editor/Data')
runtime = editor/'NetCoreRuntime/shared/Microsoft.NETCore.App/6.0.21'
output = root/'.codex-tmp/pose-transport-checks'
output.mkdir(parents=True, exist_ok=True)
assembly = output/'checks.dll'
lines = ['-nologo', '-target:exe', '-out:"'+assembly.as_posix()+'"']
lines += ['-r:"'+p.as_posix()+'"' for p in runtime.glob('*.dll')
          if (p.name.startswith('System.') and '.Native.' not in p.name)
          or p.name in ('mscorlib.dll','netstandard.dll','Microsoft.CSharp.dll')]
lines += ['"'+(root/p).as_posix()+'"' for p in ['Assets/Scripts/Perception/Stereo/PoseTransport.cs', 'Tools/PoseTransportChecks.cs']]
rsp = output/'checks.rsp'
rsp.write_text('\n'.join(lines))
(output/'checks.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net6.0','framework':{'name':'Microsoft.NETCore.App','version':'6.0.21'}}}))
dotnet = str(editor/'NetCoreRuntime/dotnet.exe')
subprocess.run([dotnet,str(editor/'DotNetSdkRoslyn/csc.dll'),'@'+str(rsp)],check=True,cwd=root)
subprocess.run([dotnet,str(assembly)],check=True,cwd=root,timeout=30)
