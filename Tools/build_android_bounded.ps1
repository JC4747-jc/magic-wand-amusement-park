[CmdletBinding()]
param(
    [string]$UnityPath = 'D:/Unity/Hub/Editor/6000.0.33f1/Editor/Unity.exe',
    [string]$SocketTempDirectory = 'D:/Unity/JavaSockets',
    [string]$AndroidToolchainRoot = 'D:/Unity/Hub/Editor/6000.0.26f1/Editor/Data/PlaybackEngines/AndroidPlayer',
    [ValidateSet('Main','Pose','Repair','AllScenes','Probe')] [string]$Variant='Main',
    [ValidateSet(1,2)] [int]$BuildJobs=2,
    [ValidateRange(64,4096)] [int]$MaximumDiskGrowthMiB=4096,
    [string]$LogPath
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'build_process_ownership.ps1')
. (Join-Path $PSScriptRoot 'build_process_job.ps1')
$projectDirectory = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
if ($Variant -eq 'Probe') {
    $projectDirectory = Join-Path $projectDirectory '.codex-tmp/bee-concurrency-probe'
    if (-not (Test-Path -LiteralPath (Join-Path $projectDirectory 'Assets/Editor/BeeProbeBuild.cs') -PathType Leaf)) { throw 'Bounded concurrency probe project has not been prepared.' }
}
if (-not (Test-Path -LiteralPath $UnityPath -PathType Leaf)) { throw "Unity editor missing: $UnityPath" }
foreach ($tool in @('SDK','NDK','OpenJDK')) {
    if (-not (Test-Path -LiteralPath (Join-Path $AndroidToolchainRoot $tool) -PathType Container)) { throw "Shared Android toolchain missing $tool under $AndroidToolchainRoot" }
}
if ((Get-CimInstance Win32_OperatingSystem).FreePhysicalMemory -lt 6MB) {
    throw 'Less than 6 GiB physical memory available. Finish other heavy work before building.'
}
$projectDrive = [System.IO.DriveInfo]::new([System.IO.Path]::GetPathRoot($projectDirectory))
if ($projectDrive.AvailableFreeSpace -lt 20GB) { throw 'Project drive requires at least 20 GiB free for this build.' }
foreach ($editorProcess in (Get-Process Unity -ErrorAction SilentlyContinue)) {
    if ($editorProcess.Path -and [System.IO.Path]::GetFullPath($editorProcess.Path) -eq [System.IO.Path]::GetFullPath($UnityPath)) {
        throw 'The target Unity editor is already running. Close it before starting another project build.'
    }
}
if (-not $LogPath) { $LogPath = Join-Path $projectDirectory '.codex-tmp/android-bounded-build.log' }
$LogPath = [System.IO.Path]::GetFullPath($LogPath)
$SocketTempDirectory = [System.IO.Path]::GetFullPath($SocketTempDirectory)
New-Item -ItemType Directory -Force -Path ([System.IO.Path]::GetDirectoryName($LogPath)), $SocketTempDirectory | Out-Null
$savedJdkOptions = $env:JDK_JAVA_OPTIONS
$savedBeeThreads = $env:BEE_BUILD_THREADS
$savedBuildJobs = $env:MAGICMR_BUILD_JOBS
$savedAndroidToolchain = $env:MAGICMR_ANDROID_TOOLCHAIN
$buildProcess=$null
$buildJob=$null
try {
    # Required before JVM startup in BOTH Gradle launcher and child JVMs.
    # A daemon build property arrives too late for Java NIO's initial selector pipe.
    # AGP 8.3 ApkFlinger uses ForkJoinPool.commonPool for ZIP compression;
    # Gradle's worker limit does not constrain that independent pool.
    $env:JDK_JAVA_OPTIONS = ($savedJdkOptions + ' -XX:MaxMetaspaceSize=512M -XX:ActiveProcessorCount=2 -Djava.util.concurrent.ForkJoinPool.common.parallelism=1 -Djdk.net.unixdomain.tmpdir="' + $SocketTempDirectory + '"').Trim()
    # The backend driver honors this from 6000.0.33f1. Earlier editors do not
    # provide the same guarantee; -job-worker-count is a separate scheduler.
    $env:BEE_BUILD_THREADS = [string]$BuildJobs
    $env:MAGICMR_BUILD_JOBS = [string]$BuildJobs
    $env:MAGICMR_ANDROID_TOOLCHAIN = [IO.Path]::GetFullPath($AndroidToolchainRoot)
    $versionMatch = [regex]::Match((Get-Item -LiteralPath $UnityPath).VersionInfo.ProductVersion, '^\d+\.\d+\.\d+')
    if (-not $versionMatch.Success -or [version]$versionMatch.Value -lt [version]'6000.0.33') {
        Write-Warning 'This editor predates the Bee backend concurrency fix. Native compiler count is not guaranteed by BEE_BUILD_THREADS=2; inspect resource telemetry.'
    }
    $method = @{Main='Run';Pose='RunPose';Repair='RunRepair';AllScenes='RunAllScenes'}[$Variant]
    $executeMethod = if ($Variant -eq 'Probe') { 'BeeProbeBuild.Run' } else { 'ResourceBoundedAndroidBuild.'+$method }
    $arguments = @('-batchmode', '-quit', '-projectPath', ('"' + $projectDirectory + '"'),
        '-buildTarget', 'Android', '-job-worker-count', '2', '-executeMethod', $executeMethod,
        '-logFile', ('"' + $LogPath + '"'))
    $buildProcess = Start-Process -FilePath $UnityPath -ArgumentList $arguments -WorkingDirectory $projectDirectory -WindowStyle Hidden -PassThru
    $buildJob=[MagicMRBuildJob]::Attach($buildProcess)
    $samples = [System.Collections.Generic.Queue[object]]::new()
    $minimumFreeGiB = [double]::PositiveInfinity
    $maximumCompilerCount = 0
    $backendArguments = [System.Collections.Generic.HashSet[string]]::new()
    $guardStop=$null;$started=Get-Date;$initialDisk=$projectDrive.AvailableFreeSpace
    $maximumOwnedRSSMiB=0;$maximumObservedDiskGrowthMiB=0
    while (-not $buildProcess.WaitForExit(5000)) {
        $freeGiB = (Get-CimInstance Win32_OperatingSystem).FreePhysicalMemory / 1MB
        $minimumFreeGiB = [math]::Min($minimumFreeGiB, $freeGiB)
        $processes = @(Get-CimInstance Win32_Process)
        $ownedProcesses=@(Get-OwnedBuildProcesses -Processes $processes -RootProcessId $buildProcess.Id -RootCreationTime $buildProcess.StartTime)
        $ownedRSSMiB=($ownedProcesses|Measure-Object WorkingSetSize -Sum).Sum/1MB
        $disk=$projectDrive.AvailableFreeSpace;$growthMiB=($initialDisk-$disk)/1MB
        $maximumOwnedRSSMiB=[math]::Max($maximumOwnedRSSMiB,$ownedRSSMiB)
        $maximumObservedDiskGrowthMiB=[math]::Max($maximumObservedDiskGrowthMiB,$growthMiB)
        $compilerCount = 0
        foreach ($candidate in $ownedProcesses) {
            if ($candidate.Name -notin @('clang++.exe','bee_backend.exe')) { continue }
            if ($candidate.Name -eq 'clang++.exe') { $compilerCount++ }
            elseif ($candidate.CommandLine -and $backendArguments.Count -lt 8) { [void]$backendArguments.Add($candidate.CommandLine) }
        }
        $maximumCompilerCount = [math]::Max($maximumCompilerCount, $compilerCount)
        $samples.Enqueue([pscustomobject]@{utc=(Get-Date).ToUniversalTime().ToString('o');freeMemoryGiB=[math]::Round($freeGiB,3);compilerProcesses=$compilerCount;ownedRSSMiB=[math]::Round($ownedRSSMiB,2);diskGrowthMiB=[math]::Round($growthMiB,2)})
        # Keep at most an hour of five-second samples in RAM, write once at exit.
        if ($samples.Count -gt 720) { [void]$samples.Dequeue() }
        if($freeGiB -lt 3){$guardStop='Host RAM below 3 GiB'}
        elseif($ownedRSSMiB -gt 8192){$guardStop='Owned build process RSS above 8 GiB'}
        elseif($disk -lt 20GB){$guardStop='Project drive below 20 GiB'}
        elseif($growthMiB -gt $MaximumDiskGrowthMiB){$guardStop='Additional build disk budget above '+$MaximumDiskGrowthMiB+' MiB'}
        elseif($compilerCount -gt $BuildJobs){$guardStop='Native compiler count above requested jobs'}
        elseif(((Get-Date)-$started).TotalSeconds -gt 1200){$guardStop='20-minute build budget exhausted'}
        elseif((Test-Path -LiteralPath $LogPath) -and (Get-Item -LiteralPath $LogPath).Length -gt 16MB){$guardStop='Build log budget above 16 MiB'}
        if($guardStop){$buildProcess.Kill($true);break}
    }
    $buildProcess.WaitForExit();$buildProcess.Refresh()
    [pscustomobject]@{
        unity=$UnityPath;variant=$Variant;requestedBeeThreads=$BuildJobs;requestedIl2CppJobs=$BuildJobs;exitCode=$buildProcess.ExitCode
        guardStop=$guardStop;maximumSampledOwnedRSSMiB=[math]::Round($maximumOwnedRSSMiB,2);maximumSampledDiskGrowthMiB=[math]::Round($maximumObservedDiskGrowthMiB,2)
        processOwnership='PID and creation time validated at every parent-child edge'
        childCleanup='Unnamed Windows job with KILL_ON_JOB_CLOSE; no breakaway flags'
        maximumAllowedDiskGrowthMiB=$MaximumDiskGrowthMiB
        minimumSampledFreeMemoryGiB=[math]::Round($minimumFreeGiB,3);maximumSampledCompilerProcesses=$maximumCompilerCount
        samplingScope='Five-second snapshots of descendants of this Unity build; not a continuous or complete peak measurement'
        backendArguments=@($backendArguments);samples=@($samples.ToArray())
    } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath ($LogPath+'.resources.json') -Encoding UTF8
    if($guardStop){throw $guardStop}
    if ($buildProcess.ExitCode -ne 0) { throw "Android build failed ($($buildProcess.ExitCode)); inspect $LogPath" }
    $apkName = @{Main='BridgeStereoFusion';Pose='BridgeStereoPoseFusion';Repair='BridgeStereoRepair';AllScenes='AllScenesValidation';Probe='BeeProbe'}[$Variant]
    $apkPath = Join-Path $projectDirectory ('Builds/Android/'+$apkName+'.apk')
    if (-not (Test-Path -LiteralPath $apkPath -PathType Leaf)) { throw "Unity exited without the expected APK: $apkPath" }
    $artifact = Get-Item -LiteralPath $apkPath
    [pscustomobject]@{
        FullName = $artifact.FullName
        Length = $artifact.Length
        LastWriteTime = $artifact.LastWriteTime
        SHA256 = (Get-FileHash -LiteralPath $apkPath -Algorithm SHA256).Hash
    } | Format-List
}
catch {
    # Unexpected monitoring/startup failures must not leave this build running.
    if($null -ne $buildProcess){$buildProcess.Refresh();if(-not $buildProcess.HasExited){$buildProcess.Kill($true);$buildProcess.WaitForExit()}}
    throw
}
finally { if($buildJob){$buildJob.Dispose()};$env:JDK_JAVA_OPTIONS = $savedJdkOptions; $env:BEE_BUILD_THREADS = $savedBeeThreads; $env:MAGICMR_BUILD_JOBS = $savedBuildJobs; $env:MAGICMR_ANDROID_TOOLCHAIN = $savedAndroidToolchain }
