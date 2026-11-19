param([Parameter(Mandatory=$true)][ValidatePattern('^[a-z0-9][a-z0-9-]{0,45}$')][string]$RunId,
      [ValidatePattern('^[A-Za-z0-9._-]{1,50}$')][string]$Distro='Ubuntu-24.04',
      [ValidateRange(0,600)][int]$SessionSeconds=0,
      [ValidateSet('None','CancelAfterReady')][string]$Probe='None',
      [switch]$PlanOnly)
$ErrorActionPreference='Stop'
if($Probe -ne 'None' -and $SessionSeconds -lt 10){throw 'Cancellation probe needs a 10-600 second session'}
$probeCode=if($Probe -eq 'None'){'none'}else{'cancel-ready'}
$root=Split-Path $PSScriptRoot -Parent
$group=if($SessionSeconds -gt 0 -or $Probe -ne 'None'){'Reports/f2g-controlled-service-20261119/'}else{'Reports/f2g-production-loopback-20261119/'}
$output=Join-Path $root ($group+$RunId)
if(Test-Path -LiteralPath $output){throw 'Preserve evidence; use a new RunId'}
[void][IO.Directory]::CreateDirectory($output)
$nonce=[guid]::NewGuid().ToString('N');$unit='magicmr-f2g-'+$RunId+'-'+$nonce+'.service'
$runRoot=Join-Path $root ('.codex-tmp/f2g-server-guard/'+$RunId+'/'+$nonce)
$limits=[ordered]@{startRAMGiB=8;freeRAMFloorGiB=3;freeCGiB=20;freeDGiB=25;seconds=(150+$SessionSeconds);growthMiB=16;logMiB=2;windowsRSSMiB=256;clientSeconds=(10+$SessionSeconds);linuxRuntimeSeconds=(120+$SessionSeconds);linuxMemoryMiB=4096;gpuStartFreeMiB=4800;allocatorMiB=4608}
$utf8=New-Object System.Text.UTF8Encoding($false)
function Write-ServerJson([string]$Path,$Value){
    $temporary=$Path+'.tmp';[IO.File]::WriteAllText($temporary,($Value|ConvertTo-Json -Depth 15),$utf8)
    if(Test-Path -LiteralPath $Path){[IO.File]::Replace($temporary,$Path,[System.Management.Automation.Language.NullString]::Value)}else{[IO.File]::Move($temporary,$Path)}
}
function Get-ServerResources {
    [ordered]@{freeRAMGiB=(Get-CimInstance Win32_OperatingSystem).FreePhysicalMemory/1MB;freeCGiB=(Get-PSDrive C).Free/1GB;freeDGiB=(Get-PSDrive D).Free/1GB}
}
$initial=Get-ServerResources
$names=@('Tools/f2g_server_guard.py','Tools/run_f2g_server_bounded.ps1','Tools/check_f2g_server_loopback.py','Tools/f2g_model_guard.py','Tools/f2g_service_guard.py','Tools/build_process_job.ps1','Tools/setup_f2g_runtime.py','object-detection-demo/pose_bridge.py','object-detection-demo/pose_availability.py','object-detection-demo/detector_config.py')
$inputs=@(foreach($name in $names){[ordered]@{path=$name;sha256=(Get-FileHash -LiteralPath (Join-Path $root $name) -Algorithm SHA256).Hash.ToLowerInvariant()}})
if($root -match '^([A-Za-z]):\\([^"\r\n]*)$'){$linuxRoot='/mnt/'+$Matches[1].ToLowerInvariant()+'/'+($Matches[2] -replace '\\','/')}else{throw 'Need reviewed Windows drive path'}
$arguments=@('-d',$Distro,'-u','root','--exec','/usr/bin/python3','-B',('"'+$linuxRoot+'/Tools/f2g_server_guard.py"'),'run','--run-id',$RunId,'--nonce',$nonce)
$canStart=$initial.freeRAMGiB -ge 8 -and $initial.freeCGiB -ge 20 -and $initial.freeDGiB -ge 25
Write-ServerJson (Join-Path $output 'plan.json') ([ordered]@{mode='BOUNDED_PRODUCTION_LOOPBACK_CHECK';createdAtUtc=(Get-Date).ToUniversalTime().ToString('o');unit=$unit;nonce=$nonce;runRoot=$runRoot;distro=$Distro;limits=$limits;initialResources=$initial;resourceGateAllowsStart=$canStart;sessionSeconds=$SessionSeconds;probe=$probeCode;inputs=$inputs;arguments=$arguments;scope='Unchanged production entry and existing weights, official combined warmup, real Windows monitor; no PICO/frame accuracy claim'})
if($PlanOnly){Write-ServerJson (Join-Path $output 'result.json') ([ordered]@{status='PLAN_ONLY';verified=$false});return}
if(!$canStart){Write-ServerJson (Join-Path $output 'result.json') ([ordered]@{status='PREFLIGHT_REFUSED';verified=$false;reason='RAM/disk gate'});throw 'Need 8 GiB free RAM and C>=20/D>=25 GiB; nothing launched'}
$busy=@(Get-CimInstance Win32_Process -Filter "Name='Unity.exe' OR Name='powershell.exe' OR Name='pwsh.exe' OR Name='python.exe'"|Where-Object {$_.ProcessId -ne $PID -and ($_.Name -eq 'Unity.exe' -or $_.CommandLine -match 'restore-integration-validation|build_android_bounded\.ps1|run_hci_shared_warm_bounded\.ps1|train_.*_bounded\.ps1|run_yolo_training_bounded\.ps1|run_f2g_.*_bounded\.ps1|smoke_f2g_windows\.py')})
$listeners=@(Get-NetTCPConnection -State Listen -ErrorAction SilentlyContinue|Where-Object {$_.LocalPort -in @(5006,5007)})
if($busy.Count -or $listeners.Count){Write-ServerJson (Join-Path $output 'result.json') ([ordered]@{status='PREFLIGHT_REFUSED';verified=$false;reason='Existing editor/heavy task/listener'});throw 'Existing task/listener; nothing launched or stopped'}
$clientPython='D:/MagicMRTraining/python-3.12.14-amd64/python.exe'
if(!(Test-Path -LiteralPath $clientPython)){throw 'Existing lightweight Windows Python missing; do not install implicitly'}
$lock=$null;$job=$null;$process=$null;$clientJob=$null;$client=$null;$clientClock=$null;$failure=$null;$owns=$false;$launched=$false;$sequence=0;$linuxResult=$null;$probeTriggered=$false;$activeClock=$null
$samples=New-Object 'System.Collections.Generic.List[object]';$clock=[Diagnostics.Stopwatch]::StartNew()
try{
    $lock=[IO.File]::Open((Join-Path $root '.codex-tmp/f2g-service-host.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
    if(Test-Path -LiteralPath $runRoot){throw 'Refusing an existing owned run'}
    [void][IO.Directory]::CreateDirectory($runRoot);$owns=$true
    Write-ServerJson (Join-Path $runRoot 'host.json') ([ordered]@{unit=$unit;nonce=$nonce;inputs=$inputs;sessionSeconds=$SessionSeconds;probe=$probeCode})
    Write-ServerJson (Join-Path $runRoot 'lease.json') ([ordered]@{nonce=$nonce;seq=$sequence;cancel=$false})
    . (Join-Path $root 'Tools/build_process_job.ps1')
    $now=Get-ServerResources
    if($now.freeRAMGiB -lt 8 -or $now.freeCGiB -lt 20 -or $now.freeDGiB -lt 25){throw 'Resources changed before WSL launch'}
    $process=Start-Process -FilePath (Join-Path $env:WINDIR 'System32/wsl.exe') -ArgumentList $arguments -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $output 'stdout.txt') -RedirectStandardError (Join-Path $output 'stderr.txt')
    $launched=$true;$job=[MagicMRBuildJob]::Attach($process)
    if(!$job.Contains($process)){throw 'WSL client outside owned Job'}
    $nextSample=0
    while(!$process.HasExited){
        $sequence++;Write-ServerJson (Join-Path $runRoot 'lease.json') ([ordered]@{nonce=$nonce;seq=$sequence;cancel=$probeTriggered})
        if(!$client -and (Test-Path -LiteralPath (Join-Path $runRoot 'ready.json'))){
            $clientPath=Join-Path $root 'Tools/check_f2g_server_loopback.py'
            $binding=@($inputs|Where-Object {$_.path -eq 'Tools/check_f2g_server_loopback.py'})[0]
            if((Get-FileHash -LiteralPath $clientPath -Algorithm SHA256).Hash.ToLowerInvariant() -ne $binding.sha256){throw 'Client source changed'}
            $clientArgs=@('-I','-B','-S','-X','utf8',('"'+$clientPath+'"'),'--folder',('"'+$runRoot+'"'),'--nonce',$nonce,'--unit',$unit)
            $client=Start-Process -FilePath $clientPython -ArgumentList $clientArgs -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $output 'windows-client-stdout.txt') -RedirectStandardError (Join-Path $output 'windows-client-stderr.txt')
            $clientClock=[Diagnostics.Stopwatch]::StartNew();$clientJob=[MagicMRBuildJob]::Attach($client)
            if(!$clientJob.Contains($client)){throw 'Windows monitor outside owned Job'}
            Write-ServerJson (Join-Path $runRoot 'client-admit.json') ([ordered]@{nonce=$nonce;unit=$unit;pid=$client.Id;sourceSha256=$binding.sha256;sessionSeconds=$SessionSeconds;startTimeUtc=$client.StartTime.ToUniversalTime().ToString('o')})
        }
        if($client -and !$probeTriggered -and $probeCode -eq 'cancel-ready' -and (Test-Path -LiteralPath (Join-Path $runRoot 'windows-active.json'))){
            $activePath=Join-Path $runRoot 'windows-active.json'
            if((Get-Item -LiteralPath $activePath).Length -gt 16384){throw 'Oversized active monitor receipt'}
            $active=Get-Content -LiteralPath $activePath -Raw|ConvertFrom-Json
            if($active.nonce -ne $nonce -or $active.unit -ne $unit -or $active.pid -ne $client.Id -or $active.successfulHealthChecks -lt 2){throw 'Unverified active Windows monitor'}
            if(!$activeClock){$activeClock=[Diagnostics.Stopwatch]::StartNew()}
            if($activeClock.Elapsed.TotalSeconds -ge 1){
                Write-ServerJson (Join-Path $runRoot 'probe-trigger.json') ([ordered]@{nonce=$nonce;unit=$unit;pid=$client.Id;successfulHealthChecks=$active.successfulHealthChecks;action='cancel-after-verified-ready';hostElapsedSeconds=$clock.Elapsed.TotalSeconds})
                $probeTriggered=$true
            }
        }
        if($client){$client.Refresh();if(!$client.HasExited -and $clientClock.Elapsed.TotalSeconds -gt $limits.clientSeconds){throw 'Windows client deadline'};if($client.HasExited -and $client.ExitCode -ne 0 -and !$probeTriggered){throw 'Windows production monitor failed'}}
        if($clock.Elapsed.TotalSeconds -gt $limits.seconds){throw 'Host deadline'}
        if($clock.Elapsed.TotalSeconds -ge $nextSample){
            $sample=Get-ServerResources;$process.Refresh();$rss=if($process.HasExited){0}else{$process.WorkingSet64/1MB}
            if($client){$client.Refresh();if(!$client.HasExited){$rss+=$client.WorkingSet64/1MB}}
            $files=@(Get-ChildItem -LiteralPath $output,$runRoot -File -Recurse);$growth=($files|Measure-Object Length -Sum).Sum/1MB
            $sample.elapsedSeconds=[math]::Round($clock.Elapsed.TotalSeconds,2);$sample.windowsOwnedRSSMiB=$rss;$sample.taskFilesMiB=$growth;$samples.Add($sample);$nextSample=$clock.Elapsed.TotalSeconds+1
            if($sample.freeRAMGiB -lt 3 -or $sample.freeCGiB -lt 20 -or $sample.freeDGiB -lt 25){throw 'Host RAM/disk floor crossed'}
            if($rss -gt 256 -or $growth -gt 16 -or @($files|Where-Object {$_.Extension -in @('.txt','.log') -and $_.Length -gt 2MB}).Count){throw 'Owned RSS/file/log limit crossed'}
        }
        Start-Sleep -Milliseconds 200;$process.Refresh()
    }
    $process.WaitForExit()
    if($process.ExitCode -ne 0){throw 'Linux server check failed'}
    if(!$client){throw 'Windows monitor was not started'}
    if($probeCode -eq 'none' -and (!$client.WaitForExit(2000) -or $client.ExitCode -ne 0)){throw 'Windows monitor completion missing'}
    $path=Join-Path $runRoot 'server-result.json'
    if(!(Test-Path -LiteralPath $path) -or (Get-Item -LiteralPath $path).Length -gt 16384){throw 'Missing/oversized server receipt'}
    $linuxResult=Get-Content -LiteralPath $path -Raw|ConvertFrom-Json
    if(!$linuxResult.passed -or $linuxResult.unit -ne $unit -or $linuxResult.invocation -notmatch '^[0-9a-f]{32}$' -or !$linuxResult.allOwnedNotRunnable -or $linuxResult.sessionSeconds -ne $SessionSeconds){throw 'Server ownership/client receipt incomplete'}
    if($probeCode -eq 'none'){
        if(!$linuxResult.worker.windowsClient.passed -or $linuxResult.expectedCancellation){throw 'Normal controlled session did not complete'}
    }elseif(!$probeTriggered -or !$linuxResult.expectedCancellation -or !$linuxResult.independentControlSurvivedServiceCleanup -or $linuxResult.worker.passed){throw 'Expected production cancellation/control check missing'}
    foreach($row in $inputs){if((Get-FileHash -LiteralPath (Join-Path $root $row.path) -Algorithm SHA256).Hash.ToLowerInvariant() -ne $row.sha256){throw 'Input changed during server run'}}
}catch{$failure=$_.Exception.Message}
finally{
    if($owns){try{$sequence++;Write-ServerJson (Join-Path $runRoot 'lease.json') ([ordered]@{nonce=$nonce;seq=$sequence;cancel=$true})}catch{if(!$failure){$failure='Owned lease cancellation failed'}}}
    if($process -and !$process.HasExited){[void]$process.WaitForExit(8000)}
    if($clientJob){$clientJob.Dispose()}elseif($client -and !$client.HasExited){$client.Kill()}
    if($job){$job.Dispose()}elseif($process -and !$process.HasExited){$process.Kill()}
    if($lock){$lock.Dispose()}
    Write-ServerJson (Join-Path $output 'resources.json') ([ordered]@{scope='Host/owned Windows process snapshots, not total Linux memory or continuous peaks';samples=$samples.ToArray();limits=$limits})
}
$status=if($failure){'FAILED'}elseif($probeCode -ne 'none'){'PASS_CONTROLLED_SERVER_CANCEL'}elseif($SessionSeconds -gt 0){'PASS_CONTROLLED_PRODUCTION_SESSION'}else{'PASS_CURRENT_PRODUCTION_LOOPBACK'}
Write-ServerJson (Join-Path $output 'result.json') ([ordered]@{status=$status;error=$failure;wslClientLaunched=$launched;unit=$unit;verified=(!$failure);sessionSeconds=$SessionSeconds;probe=$probeCode;probeTriggered=$probeTriggered;linuxResult=$linuxResult;picoOrRealTargetAccuracyVerified=$false})
if($failure){throw $failure}
Write-Output ('Current production loopback verified: '+$output)
