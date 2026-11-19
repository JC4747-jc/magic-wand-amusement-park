#Requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$ProjectRoot,
    [string]$UnityPath='D:/Unity/Hub/Editor/6000.0.33f1/Editor/Unity.exe',
    [ValidatePattern('^[a-z0-9-]{4,60}$')][string]$RunId='nonfire-gate-regression'
)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$candidate=(Resolve-Path -LiteralPath $ProjectRoot).Path
$output=Join-Path $root ('Reports/nonfire-gate-regression/'+$RunId)
if(Test-Path -LiteralPath $output){throw 'Preserve existing evidence; inspect it before choosing a fresh run ID'}
if(@(Get-Process Unity -ErrorAction SilentlyContinue).Count){throw 'An existing Unity process must be left untouched'}
if((Get-CimInstance Win32_OperatingSystem).FreePhysicalMemory -lt 6MB){throw 'Need 6 GiB free RAM'}
$drive=[IO.DriveInfo]::new([IO.Path]::GetPathRoot($candidate))
if((Get-PSDrive C).Free -lt 20GB -or $drive.AvailableFreeSpace -lt 25GB){throw 'Disk floor crossed'}
foreach($name in @('Assets','Packages','ProjectSettings')){
    if(!(Test-Path -LiteralPath (Join-Path $candidate $name) -PathType Container)){throw 'Expected an existing configured isolated Unity project'}
}
$fixtureInput=Join-Path $root 'Tools/ValidationData/nonfire-discovery-yolo-20261120.jsonl'
$source=Join-Path $root 'Tools/NonFireDiscoveryNativeChecks.cs'
$probe=[IO.Path]::GetFullPath((Join-Path $candidate 'Assets/Editor/NonFireDiscoveryNativeChecks.cs'))
$meta=$probe+'.meta'
$assetsPrefix=[IO.Path]::GetFullPath((Join-Path $candidate 'Assets'))+[IO.Path]::DirectorySeparatorChar
if(!$probe.StartsWith($assetsPrefix,[StringComparison]::OrdinalIgnoreCase)){throw 'Probe path escaped project Assets'}
if((Test-Path -LiteralPath $probe) -or (Test-Path -LiteralPath $meta)){throw 'Never overwrite an existing Editor check or GUID'}
if(!(Test-Path -LiteralPath $source) -or !(Test-Path -LiteralPath $fixtureInput)){throw 'Versioned native check/input fixture missing'}
if(@(Get-Content -LiteralPath $fixtureInput).Count -ne 4){throw 'Expected the fixed four-frame diagnostic fixture'}
[void][IO.Directory]::CreateDirectory($output)
$files=@(foreach($name in @('Assets','Packages','ProjectSettings')){
    Get-ChildItem -LiteralPath (Join-Path $candidate $name) -Recurse -File|ForEach-Object {
        @{path=$_.FullName;sha256=(Get-FileHash -LiteralPath $_.FullName).Hash.ToLowerInvariant()}
    }
})
$sourceHash=(Get-FileHash -LiteralPath $source).Hash.ToLowerInvariant()
$fixtureInputHash=(Get-FileHash -LiteralPath $fixtureInput).Hash.ToLowerInvariant()
Copy-Item -LiteralPath $source -Destination $probe
[IO.File]::WriteAllText($meta,('fileFormatVersion: 2'+"`n"+'guid: '+[guid]::NewGuid().ToString('N')+"`n"))
$metaHash=(Get-FileHash -LiteralPath $meta).Hash.ToLowerInvariant()
. (Join-Path $PSScriptRoot 'build_process_job.ps1')
. (Join-Path $PSScriptRoot 'build_process_ownership.ps1')
$log=Join-Path $output 'unity.log';$result=Join-Path $output 'native-result.json'
$process=$null;$job=$null;$stop=$null;$passed=$false;$cleanup=$false
$samples=[Collections.Generic.List[object]]::new();$clock=[Diagnostics.Stopwatch]::StartNew()
$freeBefore=$drive.AvailableFreeSpace
try{
    $argsUnity=@('-batchmode','--burst-disable-compilation','-projectPath',('"'+$candidate+'"'),'-job-worker-count','1',
        '-executeMethod','NonFireDiscoveryNativeChecks.Run','-nonFireInput',('"'+$fixtureInput+'"'),
        '-nonFireResult',('"'+$result+'"'),'-logFile',('"'+$log+'"'))
    $process=Start-Process -FilePath $UnityPath -ArgumentList $argsUnity -WorkingDirectory $candidate -WindowStyle Hidden -PassThru
    $birth=$process.StartTime;$job=[MagicMRBuildJob]::Attach($process)
    while(!$process.HasExited){
        $ram=(Get-CimInstance Win32_OperatingSystem).FreePhysicalMemory/1MB
        $rss=0;$all=@(Get-CimInstance Win32_Process)
        foreach($owned in @(Get-OwnedBuildProcesses -Processes $all -RootProcessId $process.Id -RootCreationTime $birth)){
            $p=Get-Process -Id $owned.ProcessId -ErrorAction SilentlyContinue
            if($p -and $job.Contains($p)){$rss+=$p.WorkingSet64/1MB}
        }
        $growth=[Math]::Max(0,($freeBefore-$drive.AvailableFreeSpace)/1MB)
        $samples.Add(@{elapsed=$clock.Elapsed.TotalSeconds;freeRAMGiB=$ram;treeRSSMiB=$rss;netGrowthMiB=$growth})
        if($ram -lt 3 -or $rss -gt 3584 -or $growth -gt 512 -or $clock.Elapsed.TotalSeconds -gt 300 -or
            ((Test-Path -LiteralPath $log) -and (Get-Item -LiteralPath $log).Length -gt 8MB)){$stop='Native regression resource/deadline guard';$job.Dispose();$job=$null;break}
        Start-Sleep -Milliseconds 500;$process.Refresh()
    }
    [void]$process.WaitForExit(8000)
    if($stop -or !$process.HasExited -or $process.ExitCode -ne 0 -or !(Test-Path -LiteralPath $result)){throw 'Native regression did not complete successfully'}
    $native=Get-Content -LiteralPath $result -Raw -Encoding utf8|ConvertFrom-Json
    $text=Get-Content -LiteralPath $log -Raw
    if($native.status -ne 'NATIVE_NONFIRE_VALIDATION_CHECKS_PASSED' -or $text -notmatch 'NONFIRE_NATIVE_CHECK_PASS:' -or $text -notmatch 'SHARED_INTERFACE_CHECK_PASS:'){throw 'Native JSON/policy/shared UI regression failed'}
    foreach($file in $files){if((Get-FileHash -LiteralPath $file.path).Hash.ToLowerInvariant() -ne $file.sha256){throw ('Project source changed during native check; preserve for review: '+$file.path)}}
    $passed=$true
    Write-Output 'PASS: native JsonUtility, pending non-fire rejection, shared UI; existing project sources unchanged.'
}finally{
    if($job){$job.Dispose()}
    $terminal=$true
    if($process){
        $process.Refresh()
        if(!$process.HasExited){
            # Attachment failure fallback: creation-time-checked owned processes only.
            $ownedNow=@(Get-OwnedBuildProcesses -Processes @(Get-CimInstance Win32_Process) -RootProcessId $process.Id -RootCreationTime $birth)
            foreach($owned in @($ownedNow|Sort-Object CreationDate -Descending)){
                $p=Get-Process -Id $owned.ProcessId -ErrorAction SilentlyContinue
                if($p -and [math]::Abs(($p.StartTime.ToUniversalTime()-([datetime]$owned.CreationDate).ToUniversalTime()).TotalMilliseconds) -le 10){$p.Kill()}
            }
            [void]$process.WaitForExit(8000);$process.Refresh()
        }
        $terminal=$process.HasExited
    }
    if($terminal -and (Test-Path -LiteralPath $probe) -and (Test-Path -LiteralPath $meta) -and
        (Get-FileHash -LiteralPath $probe).Hash.ToLowerInvariant() -eq $sourceHash -and
        (Get-FileHash -LiteralPath $meta).Hash.ToLowerInvariant() -eq $metaHash){
        Remove-Item -LiteralPath $probe
        Remove-Item -LiteralPath $meta
        $cleanup=$true
    }
    @{checkedAtUtc=[datetime]::UtcNow.ToString('o');passed=$passed;guardStop=$stop;
      exitCode=$(if($process -and $process.HasExited){$process.ExitCode}else{$null});
      candidateRoot=$candidate;sourceHash=$sourceHash;inputHash=$fixtureInputHash;
      sourceInventory=$files;temporaryProbeRemovedByExactHash=$cleanup;
      launcherSha256=(Get-FileHash -LiteralPath $PSCommandPath).Hash.ToLowerInvariant();
      seconds=$clock.Elapsed.TotalSeconds;samples=$samples.ToArray();
      scope='Owned-Job sampled resource guards, native JSON/policy/shared UI; no continuous peak, camera/hand/model accuracy/PICO acceptance'}|
      ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $output 'resources.json') -Encoding utf8
}
