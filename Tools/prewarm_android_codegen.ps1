#Requires -Version 7.0
[CmdletBinding()]
param(
    [string]$ProjectPath=(Join-Path $PSScriptRoot '..'),
    [Parameter(Mandatory)] [string]$DagPath,
    [string]$UnityPath='D:/Unity/Hub/Editor/6000.0.33f1/Editor/Unity.exe',
    [ValidateRange(64,4096)] [int]$MaximumDiskGrowthMiB=512,
    [string]$LogPath
)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'build_process_ownership.ps1')
. (Join-Path $PSScriptRoot 'build_process_job.ps1')
$project=(Resolve-Path -LiteralPath $ProjectPath).Path
$dag=if([IO.Path]::IsPathRooted($DagPath)){[IO.Path]::GetFullPath($DagPath)}else{[IO.Path]::GetFullPath((Join-Path $project $DagPath))}
if(-not $dag.StartsWith($project+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'DAG must remain inside this project'}
if(-not(Test-Path -LiteralPath $dag -PathType Leaf) -or -not(Test-Path -LiteralPath ($dag+'.json') -PathType Leaf)){throw 'Generated DAG/JSON pair is missing'}
if((Get-Item -LiteralPath ($dag+'.json')).Length -gt 16MB){throw 'DAG JSON exceeds the 16 MiB inspection budget'}
$graph=Get-Content -LiteralPath ($dag+'.json') -Raw|ConvertFrom-Json -Depth 100
$nodes=@($graph.Nodes)
if($nodes.Count -gt 50000){throw 'DAG node inspection budget exceeded'}
$codegenIds=@(for($i=0;$i -lt $nodes.Count;$i++){if($nodes[$i].Annotation -like 'IL2CPP_CodeGen *'){$i}})
if($codegenIds.Count -ne 1){throw 'Expected exactly one generated IL2CPP_CodeGen node'}
$seen=[Collections.Generic.HashSet[int]]::new();$pending=[Collections.Generic.Stack[int]]::new();$pending.Push($codegenIds[0])
while($pending.Count){
    $index=$pending.Pop();if(-not $seen.Add($index)){continue}
    if($index -lt 0 -or $index -ge $nodes.Count){throw 'DAG dependency index is invalid'}
    if($nodes[$index].ActionType -eq 'RunRPCCommand'){throw 'Codegen closure requires Editor RPC; refusing offline execution'}
    foreach($dependency in (@($nodes[$index].ToBuildDependencies)+@($nodes[$index].ToUseDependencies))){if($null -ne $dependency){$pending.Push([int]$dependency)}}
}
$outputs=@($nodes[$codegenIds[0]].Outputs)
if($outputs.Count -ne 1){throw 'Expected one auditable codegen output target'}
$target=if([IO.Path]::IsPathRooted($outputs[0])){[IO.Path]::GetFullPath($outputs[0])}else{[IO.Path]::GetFullPath((Join-Path $project $outputs[0]))}
if(-not $target.StartsWith($project+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Codegen output escapes project'}
$backend=Join-Path ([IO.Path]::GetDirectoryName($UnityPath)) 'Data/bee_backend.exe'
if(-not(Test-Path -LiteralPath $backend -PathType Leaf)){throw 'Unity Bee backend missing'}
$existing=@(Get-CimInstance Win32_Process|Where-Object {($_.Name -eq 'Unity.exe' -and $_.ExecutablePath -and [IO.Path]::GetFullPath($_.ExecutablePath) -eq [IO.Path]::GetFullPath($UnityPath)) -or $_.Name -in @('il2cpp.exe','bee_backend.exe','clang++.exe')})
if($existing.Count){throw 'Editor/native build is live; offline codegen must run serially'}
if((Get-CimInstance Win32_OperatingSystem).FreePhysicalMemory/1MB -lt 6){throw 'Need at least 6 GiB free RAM before offline codegen'}
$drive=[IO.DriveInfo]::new([IO.Path]::GetPathRoot($project));if($drive.AvailableFreeSpace -lt 20GB){throw 'Project disk below 20 GiB'}
if(-not $LogPath){$LogPath=Join-Path $project '.codex-tmp/android-codegen'}
$LogPath=[IO.Path]::GetFullPath($LogPath);New-Item -ItemType Directory -Force -Path ([IO.Path]::GetDirectoryName($LogPath))|Out-Null
$stdout=$LogPath+'.stdout.log';$stderr=$LogPath+'.stderr.log'
$dagSha=(Get-FileHash -LiteralPath $dag -Algorithm SHA256).Hash.ToLowerInvariant()
$worker=$null;$job=$null;$guardStop=$null;$savedBee=$env:BEE_BUILD_THREADS
$initialDisk=$drive.AvailableFreeSpace;$started=Get-Date
$samples=[Collections.Generic.List[object]]::new()
try{
    $env:BEE_BUILD_THREADS='1'
    $worker=Start-Process -FilePath $backend -ArgumentList @('--threads=1',('--dagfile="'+$dag+'"'),'--stats',('"'+$target+'"')) -WorkingDirectory $project -RedirectStandardOutput $stdout -RedirectStandardError $stderr -WindowStyle Hidden -PassThru
    $job=[MagicMRBuildJob]::Attach($worker)
    while(-not $worker.WaitForExit(5000)){
        $free=(Get-CimInstance Win32_OperatingSystem).FreePhysicalMemory/1MB
        $owned=@(Get-OwnedBuildProcesses -Processes @(Get-CimInstance Win32_Process) -RootProcessId $worker.Id -RootCreationTime $worker.StartTime)
        $rssMiB=($owned|Measure-Object WorkingSetSize -Sum).Sum/1MB
        $growthMiB=($initialDisk-$drive.AvailableFreeSpace)/1MB
        $samples.Add([pscustomobject]@{elapsed=[math]::Round(((Get-Date)-$started).TotalSeconds,1);freeRAMGiB=[math]::Round($free,3);ownedRSSMiB=[math]::Round($rssMiB,2);diskGrowthMiB=[math]::Round($growthMiB,2)})
        if($free -lt 3){$guardStop='Host RAM below 3 GiB'}
        elseif($rssMiB -gt 8192){$guardStop='Owned codegen RSS above 8 GiB'}
        elseif($drive.AvailableFreeSpace -lt 20GB){$guardStop='Project disk below 20 GiB'}
        elseif($growthMiB -gt $MaximumDiskGrowthMiB){$guardStop='Offline codegen additional disk budget exceeded'}
        elseif(((Get-Item -LiteralPath $stdout,$stderr)|Measure-Object Length -Sum).Sum -gt 16MB){$guardStop='Offline codegen log budget exceeded'}
        elseif(((Get-Date)-$started).TotalSeconds -gt 1200){$guardStop='20-minute codegen budget exhausted'}
        if($guardStop){$worker.Kill($true);break}
    }
    $worker.WaitForExit();$worker.Refresh()
    $log=Get-Content -LiteralPath $stdout -Raw
    $refreshRequired=$worker.ExitCode -eq 4 -and $log -match '(?m)^\*\*\* Tundra requires additional run' -and $log -match '(?m)^\[\s*\d+/\s*\d+[^\]]*\]\s+IL2CPP_CodeGen '
    $outcome=if($refreshRequired){'CODEGEN_COMPLETE_DAG_REFRESH_REQUIRED_NOT_APK'}elseif($worker.ExitCode -eq 0){'CODEGEN_TARGET_UP_TO_DATE_OR_COMPLETE_NOT_APK'}else{'CODEGEN_NOT_COMPLETED'}
    [pscustomobject]@{exitCode=$worker.ExitCode;guardStop=$guardStop;outcome=$outcome;dag=$dag;dagSha256=$dagSha;target=$target;dependencyNodes=$seen.Count;editorRpcNodes=0;maximumAllowedDiskGrowthMiB=$MaximumDiskGrowthMiB;minimumSampledFreeRAMGiB=($samples|Measure-Object freeRAMGiB -Minimum).Minimum;maximumSampledOwnedRSSMiB=($samples|Measure-Object ownedRSSMiB -Maximum).Maximum;maximumSampledDiskGrowthMiB=($samples|Measure-Object diskGrowthMiB -Maximum).Maximum;samplingScope='5-second host/process samples, not continuous peaks; no Editor or APK acceptance';samples=$samples}|ConvertTo-Json -Depth 6|Set-Content -LiteralPath ($LogPath+'.resources.json') -Encoding utf8
    if($guardStop){throw $guardStop}
    if($worker.ExitCode -ne 0 -and -not $refreshRequired){throw 'Offline codegen target did not complete'}
    Write-Output ('ANDROID_CODEGEN_OUTCOME='+$outcome)
}finally{
    if($job){$job.Dispose()}elseif($worker){$worker.Refresh();if(-not $worker.HasExited){$worker.Kill($true);$worker.WaitForExit()}}
    $env:BEE_BUILD_THREADS=$savedBee
}
