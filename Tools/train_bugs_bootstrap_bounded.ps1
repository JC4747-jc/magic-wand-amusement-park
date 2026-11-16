#Requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][ValidatePattern('^[a-z0-9][a-z0-9-]{3,60}$')][string]$RunId,
    [string]$Distro='Ubuntu-24.04'
)
$ErrorActionPreference='Stop'
$taskRoot=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if((Get-CimInstance Win32_OperatingSystem).FreePhysicalMemory -lt 6MB){throw 'Need 6 GiB free host RAM before starting WSL training'}
if((Get-PSDrive C).Free -lt 20GB -or (Get-PSDrive D).Free -lt 25GB){throw 'C/D free disk floors are 20/25 GiB'}
foreach($process in (Get-CimInstance Win32_Process -Filter "Name='Unity.exe' OR Name='blender.exe'")){
    if($process.Name -eq 'blender.exe' -or $process.CommandLine -match '(?i)[/\\]Editor[/\\]Unity.exe'){
        throw 'Finish Unity/Blender heavy work before training'
    }
}
$runner=Join-Path $taskRoot 'Tools/train_bugs_bootstrap.py'
$linuxRunner=(& wsl.exe -d $Distro --exec wslpath -a -u $runner).Trim()
if($LASTEXITCODE -ne 0 -or -not $linuxRunner.StartsWith('/')){throw 'Unable to map owned training runner to Linux'}
# A transient scope constrains only this process tree. No global WSL settings/restart or swap-file edits.
& wsl.exe -d $Distro --exec systemd-run --scope --unit=('magicmr-'+$RunId) --quiet `
    --property=MemoryAccounting=yes --property=MemoryHigh=2560M --property=MemoryMax=3G --property=MemorySwapMax=0 --property=CPUQuota=200% `
    /usr/bin/timeout --signal=TERM --kill-after=10s 960s /usr/bin/env MALLOC_ARENA_MAX=2 `
    /opt/magicmr-yolo/venv/bin/python -B $linuxRunner --run-id $RunId --epochs 20
if($LASTEXITCODE -ne 0){throw 'Training invocation failed or hit a resource guard; preserve receipt and inspect the owned run before retrying'}
