# Run elevated after explicit user authorization. Never restarts Windows.
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$resultPath = Join-Path $projectRoot '.codex-tmp/wsl-prerequisites-result.json'
$logPath = Join-Path $projectRoot '.codex-tmp/wsl-prerequisites.log'
$result = @{ completed = $false; restartNeeded = $false; features = @(); error = $null }
Start-Transcript -Path $logPath -Force | Out-Null
try {
    $principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Administrator token required' }
    foreach ($featureName in @('VirtualMachinePlatform','Microsoft-Windows-Subsystem-Linux')) {
        $feature = Get-WindowsOptionalFeature -Online -FeatureName $featureName
        if ($feature.State -eq 'EnablePending') { $result.restartNeeded = $true }
        elseif ($feature.State -ne 'Enabled') {
            $installed = Enable-WindowsOptionalFeature -Online -FeatureName $featureName -All -NoRestart
            $result.restartNeeded = $result.restartNeeded -or $installed.RestartNeeded
        }
        $result.features += @{ name = $featureName; state = (Get-WindowsOptionalFeature -Online -FeatureName $featureName).State.ToString() }
    }
    $result.completed = $true
} catch { $result.error = $_.Exception.Message }
finally {
    $result | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $resultPath -Encoding UTF8
    Stop-Transcript | Out-Null
}
if ($result.error) { exit 1 }
