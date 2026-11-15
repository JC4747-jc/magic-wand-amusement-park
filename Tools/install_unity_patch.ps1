[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$workspaceDirectory = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$metadataPath = Join-Path $workspaceDirectory '.codex-tmp/unity-patch-downloads.json'
$release = Get-Content -LiteralPath $metadataPath -Raw | ConvertFrom-Json
if ($release.version -ne '6000.0.33f1' -or $release.revision -ne '433b0a79340b' -or $release.installers.Count -ne 2) { throw 'Unexpected fixed-release installer manifest' }
$destination = [IO.Path]::GetFullPath('D:/Unity/Hub/Editor/6000.0.33f1')
$cacheDirectory = [IO.Path]::GetFullPath('D:/Unity/Installers')
if ((Get-PSDrive D).Free -lt 30GB) { throw 'Keep at least 30 GiB free on D before patch installation' }
if ((Get-CimInstance Win32_OperatingSystem).FreePhysicalMemory -lt 6MB) { throw 'Finish other heavy jobs before patch installation' }
$verified = @()
foreach ($installer in $release.installers) {
    $path = (Resolve-Path -LiteralPath $installer.path).Path
    if ([IO.Path]::GetDirectoryName($path) -ne $cacheDirectory) { throw 'Installer is outside the fixed cache directory' }
    if ((Get-Item -LiteralPath $path).Length -ne $installer.bytes -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() -ne $installer.sha256) { throw "Installer differs from verified download: $path" }
    $signature = Get-AuthenticodeSignature -LiteralPath $path
    if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'Unity Technologies') { throw "Unity publisher signature not valid: $path status=$($signature.Status)" }
    $verified += [pscustomobject]@{path=$path;sha256=$installer.sha256;signature=$signature.Status.ToString();publisher=$signature.SignerCertificate.Subject}
}
$temporaryDirectory = Join-Path $cacheDirectory 'Extract-6000.0.33f1'
New-Item -ItemType Directory -Force -Path $temporaryDirectory | Out-Null
$savedInstallTemp = $env:TEMP
$savedInstallTmp = $env:TMP
try {
    $env:TEMP=$temporaryDirectory; $env:TMP=$temporaryDirectory
    foreach ($installer in $verified) {
        Write-Output "INSTALL_FIXED_PATCH_BEGIN $([IO.Path]::GetFileName($installer.path))"
        # Official NSIS syntax: /D is last and unquoted. Fixed path has no spaces.
        $installProcess=Start-Process -FilePath $installer.path -ArgumentList '/S',('/D='+$destination) -WindowStyle Hidden -PassThru
        $installProcess.WaitForExit();$installProcess.Refresh()
        if ($installProcess.ExitCode -ne 0) { throw "Fixed patch installer exited $($installProcess.ExitCode); no reboot is performed" }
        Write-Output "INSTALL_FIXED_PATCH_COMPLETE $([IO.Path]::GetFileName($installer.path))"
    }
    $editorPath=Join-Path $destination 'Editor/Unity.exe'
    $androidPath=Join-Path $destination 'Editor/Data/PlaybackEngines/AndroidPlayer/UnityEditor.Android.Extensions.dll'
    if (-not (Test-Path -LiteralPath $editorPath -PathType Leaf) -or -not (Test-Path -LiteralPath $androidPath -PathType Leaf)) { throw 'Patch editor or Android player missing after installation' }
    $version=(Get-Item -LiteralPath $editorPath).VersionInfo.ProductVersion
    if ($version -notmatch '^6000\.0\.33') { throw "Unexpected installed editor version: $version" }
    [pscustomobject]@{version=$version;editor=$editorPath;androidPlayer=$androidPath;verifiedInstallers=$verified;scope='Separate fixed patch; existing editor untouched; SDK/NDK/JDK not downloaded; no reboot'} |
        ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $workspaceDirectory '.codex-tmp/unity-patch-install.json') -Encoding UTF8
    Write-Output "FIXED_PATCH_INSTALLED $editorPath"
}
finally {$env:TEMP=$savedInstallTemp;$env:TMP=$savedInstallTmp}
