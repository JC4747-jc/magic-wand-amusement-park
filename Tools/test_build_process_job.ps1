$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'build_process_job.ps1')
$folder=Join-Path ([IO.Path]::GetTempPath()) ('magicmr-job-test-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $folder|Out-Null
$marker=Join-Path $folder 'child.txt';$script=Join-Path $folder 'parent.ps1'
@'
param([string]$Marker,[string]$Shell)
Start-Sleep -Milliseconds 500
$child=Start-Process -FilePath $Shell -ArgumentList '-NoProfile -Command "Start-Sleep -Seconds 30"' -WindowStyle Hidden -PassThru
Set-Content -LiteralPath $Marker -Value $child.Id
Start-Sleep -Milliseconds 300
'@|Set-Content -LiteralPath $script -Encoding utf8
$shellPath=(Get-Process -Id $PID).Path
$parent=$null;$child=$null;$sentinel=$null;$job=$null
try{
    $sentinel=Start-Process -FilePath $shellPath -ArgumentList '-NoProfile -Command "Start-Sleep -Seconds 30"' -WindowStyle Hidden -PassThru
    $parent=Start-Process -FilePath $shellPath -ArgumentList @('-NoProfile','-File',('"'+$script+'"'),'-Marker',('"'+$marker+'"'),'-Shell',('"'+$shellPath+'"')) -WindowStyle Hidden -PassThru
    $job=[MagicMRBuildJob]::Attach($parent)
    if(-not $parent.WaitForExit(10000)){throw 'Fixture parent failed to exit'}
    if(-not(Test-Path -LiteralPath $marker)){throw 'Fixture child PID not recorded'}
    $child=Get-Process -Id ([int](Get-Content -LiteralPath $marker)) -ErrorAction Stop
    $job.Dispose();$job=$null
    if(-not $child.WaitForExit(5000)){throw 'Job close did not reap the orphan child'}
    $sentinel.Refresh();if($sentinel.HasExited){throw 'Job close killed a process outside its membership'}
    'BUILD_PROCESS_JOB_PASS: child survives parent exit, job close reaps orphan, unrelated sentinel remains alive'
}finally{
    if($job){$job.Dispose()}
    foreach($fixtureProcess in @($parent,$child,$sentinel)){if($fixtureProcess){$fixtureProcess.Refresh();if(-not $fixtureProcess.HasExited){$fixtureProcess.Kill($true);$fixtureProcess.WaitForExit()}}}
    foreach($path in @($marker,$script)){if(Test-Path -LiteralPath $path){Remove-Item -LiteralPath $path}}
    Remove-Item -LiteralPath $folder
}
