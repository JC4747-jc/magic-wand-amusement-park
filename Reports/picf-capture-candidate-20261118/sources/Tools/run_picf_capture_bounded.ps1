[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][ValidatePattern('^[a-z0-9][a-z0-9-]{3,60}$')][string]$RunId,
    [ValidatePattern('^[A-Za-z0-9_-]{1,70}$')][string]$ReportId='picf-capture-candidate-20261118',
    [switch]$SelfTest,
    [switch]$Capture,
    [ValidateSet('lighter','tree','bugs','statue','room','plant','pet','discovery')][string]$Scene='tree',
    [ValidateSet('unknown','editor_synthetic','pico_camera_claimed')][string]$DeclaredSource='unknown',
    [string]$ListenHost='127.0.0.1',
    [string]$ExpectedPeer,
    [ValidateRange(1024,65535)][int]$Port=15055
)
$ErrorActionPreference='Stop'
if($SelfTest.IsPresent -eq $Capture.IsPresent){throw 'Choose exactly one explicit mode: -SelfTest or -Capture'}
if($Port -eq 5005){throw 'Use a separate capture port; do not replace the production detector'}
$taskRoot=Split-Path $PSScriptRoot -Parent
$taskOutput=Join-Path $taskRoot ('Reports/'+$ReportId)
$taskCaptureRoot=Join-Path $taskRoot '.codex-tmp/device-capture'
$taskSession=Join-Path $taskCaptureRoot $RunId
if(Test-Path -LiteralPath $taskSession){throw 'Capture session exists; preserve and review before a new ID'}
if(Test-Path -LiteralPath (Join-Path $taskOutput 'run-resources.json')){throw 'Run evidence exists; choose a fresh report after review'}
if((Get-CimInstance Win32_OperatingSystem).FreePhysicalMemory -lt 4MB){throw 'Need 4 GiB free RAM for bounded capture-only tooling'}
if((Get-PSDrive C).Free -lt 20GB){throw 'C disk floor crossed'}
foreach($taskAddress in @($ListenHost,$ExpectedPeer)){
    if($taskAddress -and $taskAddress -notmatch '^[0-9.]+$'){throw 'Use explicit IPv4 literals, not names or shell arguments'}
}
[void][IO.Directory]::CreateDirectory($taskOutput)
[void][IO.Directory]::CreateDirectory($taskCaptureRoot)
$taskLock=[IO.File]::Open((Join-Path $taskCaptureRoot 'capture.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
. (Join-Path $taskRoot 'Tools/build_process_job.ps1')
$taskToolSha=(Get-FileHash -LiteralPath (Join-Path $taskRoot 'Tools/capture_picf_dataset.py')).Hash.ToLowerInvariant()
$taskProtocolSha=(Get-FileHash -LiteralPath (Join-Path $taskRoot 'object-detection-demo/scene_protocol.py')).Hash.ToLowerInvariant()
$taskLauncherSha=(Get-FileHash -LiteralPath $PSCommandPath).Hash.ToLowerInvariant()
$taskArgs=@('-B','Tools/capture_picf_dataset.py','--session-id',$RunId)
if($SelfTest){$taskArgs+='--self-test'}
else {
    $taskArgs+=@('--scene',$Scene,'--declared-source',$DeclaredSource,'--host',$ListenHost,'--port',$Port.ToString())
    if($ExpectedPeer){$taskArgs+=@('--peer',$ExpectedPeer)}
}
$taskProcess=$null;$taskJob=$null;$taskStop=$null;$taskExit=$null
$taskSamples=[Collections.Generic.List[object]]::new()
$taskStarted=[datetime]::UtcNow;$taskClock=[Diagnostics.Stopwatch]::StartNew();$taskDiskBefore=(Get-PSDrive C).Free
try {
    $taskProcess=Start-Process -FilePath (Join-Path $taskRoot '.venv/Scripts/python.exe') -ArgumentList $taskArgs -WorkingDirectory $taskRoot -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $taskOutput 'run-stdout.txt') -RedirectStandardError (Join-Path $taskOutput 'run-stderr.txt')
    $taskJob=[MagicMRBuildJob]::Attach($taskProcess)
    while(!$taskProcess.HasExited){
        $taskFree=(Get-CimInstance Win32_OperatingSystem).FreePhysicalMemory/1MB
        $taskProcesses=@(Get-CimInstance Win32_Process -Filter "Name='python.exe' OR Name='pythonw.exe'" | Select-Object ProcessId,ParentProcessId)
        $taskIds=[Collections.Generic.HashSet[int]]::new();[void]$taskIds.Add($taskProcess.Id)
        do {$taskAdded=$false;foreach($child in $taskProcesses){if($taskIds.Contains([int]$child.ParentProcessId)-and $taskIds.Add([int]$child.ProcessId)){$taskAdded=$true}}} while($taskAdded)
        $taskRss=0;$taskLive=0
        foreach($taskProcessId in $taskIds){try{$taskObserved=[Diagnostics.Process]::GetProcessById($taskProcessId);$taskRss+=$taskObserved.WorkingSet64/1MB;$taskLive++}catch{}}
        $taskGrowth=$taskDiskBefore-(Get-PSDrive C).Free
        $taskSessionBytes=0
        if(Test-Path -LiteralPath $taskSession){$taskSessionBytes=(Get-ChildItem -LiteralPath $taskSession -File -Recurse | Measure-Object Length -Sum).Sum}
        $taskLogBytes=0
        foreach($taskLogName in @('run-stdout.txt','run-stderr.txt')){$taskLog=Join-Path $taskOutput $taskLogName;if(Test-Path -LiteralPath $taskLog){$taskLogBytes+=(Get-Item -LiteralPath $taskLog).Length}}
        $taskSamples.Add([ordered]@{seconds=[math]::Round($taskClock.Elapsed.TotalSeconds,3);freeRAMGiB=[math]::Round($taskFree,3);treeRSSMiB=[math]::Round($taskRss,2);liveOwnedProcesses=$taskLive;sessionBytes=$taskSessionBytes;logBytes=$taskLogBytes;diskGrowthMiB=[math]::Round($taskGrowth/1MB,2)})
        if($taskFree -lt 3){$taskStop='3 GiB host RAM floor';break}
        if($taskRss -gt 512){$taskStop='512 MiB owned process-tree RSS cap';break}
        if($taskSessionBytes -gt 34MB -or $taskLogBytes -gt 2MB -or $taskGrowth -gt 64MB -or (Get-PSDrive C).Free -lt 20GB){$taskStop='Session/log/disk limit';break}
        if($taskClock.Elapsed.TotalSeconds -gt 150){$taskStop='150 second outer timeout';break}
        Start-Sleep -Milliseconds 500
    }
    if($taskStop){$taskJob.Dispose();$taskJob=$null;$taskProcess.WaitForExit()}
    else{$taskProcess.WaitForExit();$taskProcess.Refresh();$taskExit=$taskProcess.ExitCode}
} finally {
    if($taskJob){$taskJob.Dispose()}
    $taskLock.Dispose()
    [ordered]@{startedAtUtc=$taskStarted.ToString('o');mode=$(if($SelfTest){'SYNTHETIC_LOOPBACK_SELFTEST'}else{'CAPTURE_ONLY_SOURCE_UNVERIFIED'});runId=$RunId;exitCode=$taskExit;guardStop=$taskStop;toolSha256=$taskToolSha;protocolSha256=$taskProtocolSha;launcherSha256=$taskLauncherSha;elapsedSeconds=[math]::Round($taskClock.Elapsed.TotalSeconds,3);scope='Owned Job/process-tree/host snapshots; short-lived worker may exit before a live RSS sample. Not continuous peaks or device/model acceptance.';limits=@{initialFreeRAMGiB=4;freeRAMFloorGiB=3;ownedRSSMiB=512;sessionBytes=35651584;logBytes=2097152;diskGrowthMiB=64;outerSeconds=150};samples=$taskSamples.ToArray()} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $taskOutput 'run-resources.json') -Encoding utf8
}
if($taskStop -or $taskExit -ne 0){throw ('Capture-only worker failed/stopped; preserve session/logs: '+$taskStop)}
$taskManifest=Join-Path $taskSession 'session.json'
if(!(Test-Path -LiteralPath $taskManifest)){throw 'Capture session receipt missing'}
$taskResult=Get-Content -LiteralPath $taskManifest -Raw -Encoding utf8 | ConvertFrom-Json
if($taskResult.source_hashes.recorder -ne $taskToolSha -or $taskResult.source_hashes.protocol_parser -ne $taskProtocolSha){throw 'Capture source/receipt mismatch'}
if($taskResult.device_domain_verified -or $taskResult.training_approved -or $taskResult.independent_project_test_eligible -or $taskResult.real_yolo_validated){throw 'Capture must not auto-promote provenance, training or inference claims'}
if($SelfTest){
    $taskSummary=Get-Content -LiteralPath (Join-Path $taskOutput 'run-stdout.txt') -Raw -Encoding utf8 | ConvertFrom-Json
    if(!$taskSummary.syntheticLoopbackPass -or $taskSummary.images -ne 2 -or $taskSummary.heavyImports.Count -ne 0 -or $taskResult.declared_source -ne 'editor_synthetic'){throw 'Synthetic loopback contract failed'}
}
[pscustomobject]@{mode=$(if($SelfTest){'synthetic_loopback'}else{'capture_only'});images=@($taskResult.records).Count;receivedPackets=$taskResult.received_packets;storedImageBytes=$taskResult.stored_image_bytes;realDeviceValidated=$false;realYoloValidated=$false} | ConvertTo-Json -Compress
