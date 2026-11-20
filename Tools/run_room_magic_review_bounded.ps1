#Requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$ProjectRoot,
    [ValidatePattern('^[a-z0-9-]{4,60}$')][string]$RunId='room-current-20261120',
    [string]$EditorOverrideManifest,
    [ValidateSet('RoomMagic','RoomWindowOwnership')][string]$Check='RoomMagic'
)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$candidate=(Resolve-Path -LiteralPath $ProjectRoot).Path
$output=Join-Path $root ('Reports/room-effect-review-20261120/'+$RunId)
$method=if($Check -eq 'RoomWindowOwnership'){'RoomWindowOwnershipChecks.Run'}else{'HciRoomMagicChecks.Run'}
$resultPrefix=if($Check -eq 'RoomWindowOwnership'){'PASS: owned room window lifecycle;'}else{'PASS: saved ParkRoom;'}
$scope=if($Check -eq 'RoomWindowOwnership'){'Native repeated component/owner destruction with an external window parent; no rendering, gesture, real-room, long-duration RAM or PICO acceptance'}else{'Saved ParkRoom plus explicitly synthetic gesture/space input, actual Unity camera renders; no real YOLO/room mapping/hands/PICO or visual acceptance from assertions alone'}
$captures=Join-Path $candidate $(if($Check -eq 'RoomWindowOwnership'){'.codex-tmp/room-window-ownership'}else{'.codex-tmp/hci-room-magic'})
if(Test-Path -LiteralPath $output){throw 'Existing evidence; inspect before choosing a new run'}
if(Test-Path -LiteralPath $captures){throw 'Preserve prior captures; never overwrite another run'}
if(@(Get-Process Unity -ErrorAction SilentlyContinue).Count){throw 'Leave existing Unity processes untouched'}
if((Get-CimInstance Win32_OperatingSystem).FreePhysicalMemory -lt 6MB){throw 'Need 6 GiB available RAM'}
$drive=[IO.DriveInfo]::new([IO.Path]::GetPathRoot($candidate))
if((Get-PSDrive C).Free -lt 20GB -or $drive.AvailableFreeSpace -lt 25GB){throw 'Disk floor crossed'}
foreach($name in @('Assets','Packages','ProjectSettings')){if(!(Test-Path -LiteralPath (Join-Path $candidate $name) -PathType Container)){throw 'Configured Unity project required'}}
$metadataPath=Join-Path $candidate 'SOURCE_VALIDATION.json'
$metadata=Get-Content -LiteralPath $metadataPath -Raw -Encoding utf8|ConvertFrom-Json
$override=$null
$overrideMap=@{}
if($EditorOverrideManifest){
    $manifestPath=(Resolve-Path -LiteralPath $EditorOverrideManifest).Path
    $reportsPrefix=[IO.Path]::GetFullPath((Join-Path $root 'Reports'))+[IO.Path]::DirectorySeparatorChar
    if(!$manifestPath.StartsWith($reportsPrefix,[StringComparison]::OrdinalIgnoreCase)){throw 'Editor override must be an explicit workspace Reports receipt'}
    $override=Get-Content -LiteralPath $manifestPath -Raw -Encoding utf8|ConvertFrom-Json
    $changes=if($override.overrides){@($override.overrides)}else{@($override)}
    if($changes.Count -gt 2){throw 'At most the room fixture and RoomStyle shader may be overlaid'}
    foreach($change in $changes){
        if($change.path -notin @('Assets/Editor/HciRoomMagicChecks.cs','Assets/Resources/MagicMR/RoomStyle.shader')){throw 'Only reviewed non-fire room fixture/shader paths may be overlaid'}
        $original=@($metadata.payloadFiles|Where-Object path -eq $change.path)
        if($original.Count -ne 1 -or $original[0].sha256 -ne $change.previousSha256 -or $overrideMap.ContainsKey($change.path)){throw 'Room overlay does not bind a unique published baseline'}
        $overrideMap[$change.path]=$change.sha256
    }
}
foreach($file in $metadata.payloadFiles){
    $expected=if($overrideMap.ContainsKey($file.path)){$overrideMap[$file.path]}else{$file.sha256}
    if((Get-FileHash -LiteralPath (Join-Path $candidate $file.path)).Hash.ToLowerInvariant() -ne $expected){throw ('Candidate payload changed before room review: '+$file.path)}
}
$files=@(foreach($name in @('Assets','Packages','ProjectSettings')){
    Get-ChildItem -LiteralPath (Join-Path $candidate $name) -Recurse -File|ForEach-Object {
        @{path=$_.FullName;sha256=(Get-FileHash -LiteralPath $_.FullName).Hash.ToLowerInvariant()}
    }
})
[void][IO.Directory]::CreateDirectory($output)
$files|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $output 'source-inputs.json') -Encoding utf8
. (Join-Path $PSScriptRoot 'build_process_job.ps1')
. (Join-Path $PSScriptRoot 'build_process_ownership.ps1')
$log=Join-Path $output 'unity.log';$result=Join-Path $captures 'result.txt'
$process=$null;$job=$null;$stop=$null;$passed=$false
$samples=[Collections.Generic.List[object]]::new();$clock=[Diagnostics.Stopwatch]::StartNew()
$freeBefore=$drive.AvailableFreeSpace
try{
    $argsUnity=@('-batchmode','--burst-disable-compilation','-projectPath',('"'+$candidate+'"'),'-job-worker-count','1',
        '-executeMethod',$method,'-logFile',('"'+$log+'"'))
    $process=Start-Process -FilePath 'D:/Unity/Hub/Editor/6000.0.33f1/Editor/Unity.exe' -ArgumentList $argsUnity -WorkingDirectory $candidate -WindowStyle Hidden -PassThru
    $birth=$process.StartTime;$job=[MagicMRBuildJob]::Attach($process)
    while(!$process.HasExited){
        $ram=(Get-CimInstance Win32_OperatingSystem).FreePhysicalMemory/1MB
        $rss=0;$all=@(Get-CimInstance Win32_Process)
        foreach($owned in @(Get-OwnedBuildProcesses -Processes $all -RootProcessId $process.Id -RootCreationTime $birth)){
            $p=Get-Process -Id $owned.ProcessId -ErrorAction SilentlyContinue
            if($p -and $job.Contains($p)){$rss+=$p.WorkingSet64/1MB}
        }
        $growth=[Math]::Max(0,($freeBefore-$drive.AvailableFreeSpace)/1MB)
        $captureBytes=0
        if(Test-Path -LiteralPath $captures){$captureBytes=(Get-ChildItem -LiteralPath $captures -File|Measure-Object Length -Sum).Sum}
        $samples.Add(@{elapsed=$clock.Elapsed.TotalSeconds;freeRAMGiB=$ram;treeRSSMiB=$rss;netGrowthMiB=$growth;captureBytes=$captureBytes})
        if($ram -lt 3 -or $rss -gt 3584 -or $growth -gt 512 -or $captureBytes -gt 32MB -or $clock.Elapsed.TotalSeconds -gt 240 -or
            ((Test-Path -LiteralPath $log) -and (Get-Item -LiteralPath $log).Length -gt 8MB)){$stop='Room native review resource/deadline guard';$job.Dispose();$job=$null;break}
        Start-Sleep -Milliseconds 500;$process.Refresh()
    }
    [void]$process.WaitForExit(8000)
    if($stop -or !$process.HasExited -or $process.ExitCode -ne 0 -or !(Test-Path -LiteralPath $result)){throw 'Room review did not complete; preserve source/result/log before retrying'}
    $verdict=Get-Content -LiteralPath $result -Raw -Encoding utf8
    if(!$verdict.StartsWith($resultPrefix,[StringComparison]::Ordinal)){throw 'Current room assertions failed; do not substitute another check or an older pass'}
    foreach($file in $files){if((Get-FileHash -LiteralPath $file.path).Hash.ToLowerInvariant() -ne $file.sha256){throw ('Source changed during review: '+$file.path)}}
    $passed=$true
    Write-Output ('PASS: '+$method+'; '+$scope)
}finally{
    if($job){$job.Dispose()}
    @{checkedAtUtc=[datetime]::UtcNow.ToString('o');passed=$passed;guardStop=$stop;
      exitCode=$(if($process -and $process.HasExited){$process.ExitCode}else{$null});
      candidateRoot=$candidate;captures=$captures;check=$method;
      sourceMetadataSha256=(Get-FileHash -LiteralPath $metadataPath).Hash.ToLowerInvariant();
      editorOverride=$override;
      launcherSha256=(Get-FileHash -LiteralPath $PSCommandPath).Hash.ToLowerInvariant();
      seconds=$clock.Elapsed.TotalSeconds;samples=$samples.ToArray();
      scope=$scope}|
      ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $output 'resources.json') -Encoding utf8
}
