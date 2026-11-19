param([Parameter(Mandatory=$true)][ValidatePattern('^[a-z0-9][a-z0-9-]{0,45}$')][string]$RunId,
      [ValidatePattern('^[A-Za-z0-9._-]{1,50}$')][string]$Distro='Ubuntu-24.04',
      [ValidateSet('None','Cancel','LeaseExpiry','LogLimit')][string]$Probe='None',
      [switch]$PlanOnly)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$probeCode=@{None='none';Cancel='cancel';LeaseExpiry='lease-expiry';LogLimit='log-limit'}[$Probe]
$reportGroup=if($Probe -eq 'None'){'f2g-model-admission-20261119'}else{'f2g-model-faults-20261119'}
$output=Join-Path $root ('Reports/'+$reportGroup+'/'+$RunId)
if(Test-Path -LiteralPath $output){throw 'Preserve existing evidence; use a new RunId'}
[void][IO.Directory]::CreateDirectory($output)
$nonce=[guid]::NewGuid().ToString('N')
$unit='magicmr-f2g-'+$RunId+'-'+$nonce+'.service'
$runRoot=Join-Path $root ('.codex-tmp/f2g-model-guard/'+$RunId+'/'+$nonce)
$limits=[ordered]@{startRAMGiB=8;freeRAMFloorGiB=3;freeCGiB=20;freeDGiB=25;seconds=150;growthMiB=16;logMiB=2;windowsRSSMiB=256;gpuStartFreeMiB=4800;linuxServiceSeconds=120;linuxMemoryMiB=4096;allocatorMiB=4608}
$utf8=New-Object System.Text.UTF8Encoding($false)
function Write-ModelJson([string]$Path,$Value){
    $temporary=$Path+'.tmp'
    [IO.File]::WriteAllText($temporary,($Value|ConvertTo-Json -Depth 15),$utf8)
    if(Test-Path -LiteralPath $Path){[IO.File]::Replace($temporary,$Path,[System.Management.Automation.Language.NullString]::Value)}
    else{[IO.File]::Move($temporary,$Path)}
}
function Get-ModelResources {
    [ordered]@{freeRAMGiB=(Get-CimInstance Win32_OperatingSystem).FreePhysicalMemory/1MB;freeCGiB=(Get-PSDrive C).Free/1GB;freeDGiB=(Get-PSDrive D).Free/1GB}
}
$initial=Get-ModelResources
$names=@('Tools/f2g_model_guard.py','Tools/f2g_service_guard.py','Tools/check_f2g_inference.py','Tools/run_f2g_model_bounded.ps1','Tools/build_process_job.ps1','Tools/setup_f2g_runtime.py')
$inputs=@(foreach($name in $names){[ordered]@{path=$name;sha256=(Get-FileHash -LiteralPath (Join-Path $root $name) -Algorithm SHA256).Hash.ToLowerInvariant()}})
if($root -match '^([A-Za-z]):\\([^"\r\n]*)$'){$linuxRoot='/mnt/'+$Matches[1].ToLowerInvariant()+'/'+($Matches[2] -replace '\\','/')}else{throw 'Need a reviewed Windows drive path'}
$arguments=@('-d',$Distro,'-u','root','--exec','/usr/bin/python3','-B',('"'+$linuxRoot+'/Tools/f2g_model_guard.py"'),'run','--run-id',$RunId,'--nonce',$nonce)
$canStart=$initial.freeRAMGiB -ge $limits.startRAMGiB -and $initial.freeCGiB -ge $limits.freeCGiB -and $initial.freeDGiB -ge $limits.freeDGiB
$plan=[ordered]@{mode='OFFLINE_ROPE_MODEL_CHECK';probe=$probeCode;createdAtUtc=(Get-Date).ToUniversalTime().ToString('o');unit=$unit;nonce=$nonce;distro=$Distro;runRoot=$runRoot;limits=$limits;initialResources=$initial;resourceGateAllowsStart=$canStart;arguments=$arguments;inputs=$inputs;scope='Existing official RGB-D teapot only; no production server/fire/YOLO training/headset/download'}
Write-ModelJson (Join-Path $output 'plan.json') $plan
if($PlanOnly){Write-ModelJson (Join-Path $output 'result.json') ([ordered]@{status='PLAN_ONLY';modelVerified=$false});return}
if(!$canStart){Write-ModelJson (Join-Path $output 'result.json') ([ordered]@{status='PREFLIGHT_REFUSED';modelVerified=$false;reason='RAM or disk floor'});throw 'Need 8 GiB free RAM and C>=20/D>=25 GiB; nothing launched'}
$busy=@(Get-CimInstance Win32_Process -Filter "Name='Unity.exe' OR Name='powershell.exe' OR Name='python.exe'"|Where-Object {
    $_.ProcessId -ne $PID -and ($_.Name -eq 'Unity.exe' -or $_.CommandLine -match 'restore-integration-validation|build_android_bounded\.ps1|run_hci_shared_warm_bounded\.ps1|run_yolo_training_bounded\.ps1|train_.*_bounded\.ps1|run_f2g_.*_bounded\.ps1|smoke_f2g_windows\.py')
})
if($busy.Count){Write-ModelJson (Join-Path $output 'result.json') ([ordered]@{status='PREFLIGHT_REFUSED';modelVerified=$false;reason='Another heavy task/editor'});throw 'Another heavy task/editor exists; nothing launched'}
$job=$null;$process=$null;$lock=$null;$failure=$null;$sequence=0;$launched=$false;$linuxResult=$null;$ownsRunRoot=$false;$probeTriggered=$false
$samples=New-Object 'System.Collections.Generic.List[object]'
$clock=[Diagnostics.Stopwatch]::StartNew()
try{
    $lock=[IO.File]::Open((Join-Path $root '.codex-tmp/f2g-service-host.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
    if(Test-Path -LiteralPath $runRoot){throw 'Refusing an existing owned folder'}
    [void][IO.Directory]::CreateDirectory($runRoot);$ownsRunRoot=$true
    Write-ModelJson (Join-Path $runRoot 'host.json') ([ordered]@{unit=$unit;nonce=$nonce;inputs=$inputs;probe=$probeCode})
    Write-ModelJson (Join-Path $runRoot 'lease.json') ([ordered]@{nonce=$nonce;seq=$sequence;cancel=$false})
    . (Join-Path $root 'Tools/build_process_job.ps1')
    $beforeLaunch=Get-ModelResources
    if($beforeLaunch.freeRAMGiB -lt $limits.startRAMGiB -or $beforeLaunch.freeCGiB -lt $limits.freeCGiB -or $beforeLaunch.freeDGiB -lt $limits.freeDGiB){throw 'Resource gate changed before launch'}
    $process=Start-Process -FilePath (Join-Path $env:WINDIR 'System32/wsl.exe') -ArgumentList $arguments -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $output 'stdout.txt') -RedirectStandardError (Join-Path $output 'stderr.txt')
    $launched=$true;$job=[MagicMRBuildJob]::Attach($process)
    if(!$job.Contains($process)){throw 'WSL client is outside the owned job'}
    $nextSample=0
    while(!$process.HasExited){
        $residentPath=Join-Path $runRoot 'model-resident.json'
        if(!$probeTriggered -and $probeCode -ne 'none' -and (Test-Path -LiteralPath $residentPath)){
            if((Get-Item -LiteralPath $residentPath).Length -gt 16384){throw 'Oversized resident marker'}
            $resident=Get-Content -LiteralPath $residentPath -Raw|ConvertFrom-Json
            if($resident.nonce -ne $nonce -or $resident.probe -ne $probeCode -or !$resident.firstRealPredictionCompleted -or !$resident.rotationSanityPassed -or $resident.cudaAllocatedMiB -le 0){throw 'Unverified resident model marker'}
            $generatedBytes=if($probeCode -eq 'log-limit'){[int](2*1MB+1)}else{0}
            Write-ModelJson (Join-Path $runRoot 'probe-trigger.json') ([ordered]@{nonce=$nonce;probe=$probeCode;modelPid=$resident.pid;modelStartTicks=$resident.startTicks;generatedBytes=$generatedBytes;hostElapsedSeconds=$clock.Elapsed.TotalSeconds})
            if($probeCode -eq 'log-limit'){
                $fixturePath=Join-Path $runRoot 'limit-probe.log'
                if(Test-Path -LiteralPath $fixturePath){throw 'Refusing an existing log fixture'}
                [IO.File]::WriteAllBytes($fixturePath,(New-Object byte[] $generatedBytes))
            }
            $probeTriggered=$true
        }
        if(!$probeTriggered -or $probeCode -ne 'lease-expiry'){
            $sequence++;Write-ModelJson (Join-Path $runRoot 'lease.json') ([ordered]@{nonce=$nonce;seq=$sequence;cancel=($probeTriggered -and $probeCode -eq 'cancel')})
        }
        if($clock.Elapsed.TotalSeconds -gt $limits.seconds){throw 'Host deadline'}
        if($clock.Elapsed.TotalSeconds -ge $nextSample){
            $sample=Get-ModelResources;$process.Refresh()
            $rss=if($process.HasExited){0}else{$process.WorkingSet64/1MB}
            $files=@(Get-ChildItem -LiteralPath $output,$runRoot -File -Recurse)
            $growth=($files|Measure-Object Length -Sum).Sum/1MB
            $sample.elapsedSeconds=[math]::Round($clock.Elapsed.TotalSeconds,2);$sample.windowsClientRSSMiB=$rss;$sample.taskFilesMiB=$growth
            $samples.Add($sample);$nextSample=$clock.Elapsed.TotalSeconds+1
            if($sample.freeRAMGiB -lt $limits.freeRAMFloorGiB -or $sample.freeCGiB -lt $limits.freeCGiB -or $sample.freeDGiB -lt $limits.freeDGiB){throw 'Host RAM/disk floor crossed'}
            if($rss -gt $limits.windowsRSSMiB -or $growth -gt $limits.growthMiB){throw 'Owned task RSS/files exceeded'}
            if(@($files|Where-Object {$_.Name -in @('stdout.txt','stderr.txt','model.log') -and $_.Length -gt ($limits.logMiB*1MB)}).Count){throw 'Task log exceeded'}
        }
        Start-Sleep -Milliseconds 200;$process.Refresh()
    }
    $process.WaitForExit()
    if($process.ExitCode -ne 0){throw ('Linux model check exited '+$process.ExitCode)}
    $resultPath=Join-Path $runRoot 'model-check-result.json'
    if(!(Test-Path -LiteralPath $resultPath) -or (Get-Item -LiteralPath $resultPath).Length -gt 16384){throw 'Missing/oversized model check result'}
    $linuxResult=Get-Content -LiteralPath $resultPath -Raw|ConvertFrom-Json
    if(!$linuxResult.passed -or $linuxResult.mode -ne 'OFFLINE_ROPE_MODEL_CHECK' -or $linuxResult.unit -ne $unit -or $linuxResult.invocation -notmatch '^[0-9a-f]{32}$' -or !$linuxResult.allOwnedNotRunnable -or $linuxResult.probe -ne $probeCode){throw 'Model result failed ownership/scope checks'}
    if($probeCode -eq 'none'){
        if(!$linuxResult.modelResult.passed){throw 'Normal model check failed'}
    }elseif(!$probeTriggered -or !$linuxResult.expectedFaultObserved -or !$linuxResult.independentControlSurvivedServiceCleanup -or $linuxResult.modelResult.passed){throw 'Resident model probe or independent control check failed'}
    foreach($binding in $inputs){if((Get-FileHash -LiteralPath (Join-Path $root $binding.path) -Algorithm SHA256).Hash.ToLowerInvariant() -ne $binding.sha256){throw 'Source changed during model check'}}
}catch{$failure=$_.Exception.Message}
finally{
    if($ownsRunRoot){try{$sequence++;Write-ModelJson (Join-Path $runRoot 'lease.json') ([ordered]@{nonce=$nonce;seq=$sequence;cancel=$true})}catch{if(!$failure){$failure='Owned lease cancellation failed'}}}
    if($process -and !$process.HasExited){[void]$process.WaitForExit(8000)}
    if($job){$job.Dispose()}elseif($process -and !$process.HasExited){$process.Kill()}
    if($lock){$lock.Dispose()}
    Write-ModelJson (Join-Path $output 'resources.json') ([ordered]@{scope='Host/client snapshots, not continuous peaks or entire Linux memory';limits=$limits;samples=$samples.ToArray()})
}
$status=if($failure){'FAILED'}elseif($probeCode -ne 'none'){'PASS_RESIDENT_MODEL_GUARD_PROBE'}else{'PASS_OFFLINE_ROPE_MODEL_CHECK'}
Write-ModelJson (Join-Path $output 'result.json') ([ordered]@{status=$status;error=$failure;wslClientLaunched=$launched;unit=$unit;probe=$probeCode;probeTriggered=$probeTriggered;modelVerified=(!$failure -and $probeCode -eq 'none');residentProbeVerified=(!$failure -and $probeCode -ne 'none');linuxResult=$linuxResult;productionOrPicoVerified=$false;scope='Official teapot only; no distro termination/fire code/model changes'})
if($failure){throw $failure}
Write-Output ('Bounded official F2G check passed: '+$output)
