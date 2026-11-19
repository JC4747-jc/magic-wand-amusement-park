#Requires -Version 7.2
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$PicoSdkPath,
    [Parameter(Mandatory=$true)][string]$UnityMcpPath,
    [Parameter(Mandatory=$true)][string]$ReportPath,
    [string]$ProjectRoot=(Split-Path $PSScriptRoot -Parent),
    [switch]$VerifyOnly
)
$ErrorActionPreference='Stop'
$root=(Resolve-Path -LiteralPath $ProjectRoot).Path.TrimEnd([char]'\')
$report=[IO.Path]::GetFullPath($(if([IO.Path]::IsPathRooted($ReportPath)){$ReportPath}else{Join-Path $root $ReportPath}))
if(!$report.StartsWith((Join-Path $root 'Reports')+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Report must be inside this project Reports directory'}
if(Test-Path -LiteralPath $report){throw 'Preserve existing evidence; choose a fresh report'}
if(!(Test-Path -LiteralPath (Join-Path $root 'ProjectSettings/ProjectVersion.txt'))){throw 'Expected Unity project root'}
if(!$VerifyOnly -and @(Get-Process Unity -ErrorAction SilentlyContinue).Count){throw 'Save and close Unity before creating dependency links; no process will be stopped'}
$pins=Get-Content -LiteralPath (Join-Path $root 'Tools/local-package-pins.json') -Raw|ConvertFrom-Json
if($pins.schema -ne 1 -or @($pins.packages).Count -ne 2){throw 'Unsupported local dependency pin set'}
$manifest=Get-Content -LiteralPath (Join-Path $root 'Packages/manifest.json') -Raw|ConvertFrom-Json
$lock=Get-Content -LiteralPath (Join-Path $root 'Packages/packages-lock.json') -Raw|ConvertFrom-Json
$sourcePaths=@{'com.unity.xr.picoxr'=$PicoSdkPath;'com.coplaydev.unity-mcp'=$UnityMcpPath}

function Get-PackageFingerprint([string]$Folder){
    $lines=[Collections.Generic.List[string]]::new();$total=[int64]0;$scanned=0
    Get-ChildItem -LiteralPath $Folder -Recurse -File -Force|ForEach-Object {
        $scanned++;if($scanned -gt 10000){throw 'Package traversal file budget exceeded'}
        $relative=$_.FullName.Substring($Folder.Length+1).Replace('\','/')
        $skip=$false
        foreach($component in $relative.Split('/')){if($component -in @($pins.excludedPathComponents)){$skip=$true;break}}
        if(!$skip){
            if($_.Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Package file link needs explicit review'}
            $total+=$_.Length
            if($lines.Count -ge $pins.limits.filesPerPackage -or $total -gt $pins.limits.bytesPerPackage){throw 'Package content budget exceeded'}
            $digest=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
            $lines.Add($relative+[char]9+[string]$_.Length+[char]9+$digest)
        }
    }
    [string[]]$ordered=$lines.ToArray();[array]::Sort($ordered,[StringComparer]::Ordinal)
    $canonical=[string]::Join([string][char]10,$ordered)+[char]10
    $bytes=[Text.UTF8Encoding]::new($false).GetBytes($canonical)
    $hash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
    [ordered]@{fileCount=$lines.Count;totalBytes=$total;contentFingerprintSha256=$hash}
}

$validated=@()
foreach($pin in $pins.packages){
    if(!$sourcePaths.ContainsKey($pin.name)){throw 'Unknown dependency pin'}
    if($manifest.dependencies.($pin.name) -ne $pin.manifestReference -or $lock.dependencies.($pin.name).version -ne $pin.manifestReference){throw 'Project manifest and lock must contain the pinned relative reference'}
    $folder=(Resolve-Path -LiteralPath $sourcePaths[$pin.name]).Path.TrimEnd([char]'\')
    $metadataPath=Join-Path $folder 'package.json'
    $metadata=Get-Content -LiteralPath $metadataPath -Raw|ConvertFrom-Json
    if($metadata.name -ne $pin.name -or $metadata.version -ne $pin.version){throw 'Installed package name/version differs from pin'}
    if((Get-FileHash -LiteralPath $metadataPath -Algorithm SHA256).Hash.ToLowerInvariant() -ne $pin.packageJsonSha256){throw 'Installed package metadata hash differs from pin'}
    $fingerprint=Get-PackageFingerprint $folder
    if($fingerprint.fileCount -ne $pin.fileCount -or $fingerprint.totalBytes -ne $pin.totalBytes -or $fingerprint.contentFingerprintSha256 -ne $pin.contentFingerprintSha256){throw 'Installed package content differs from validated pin'}
    $validated+=@{pin=$pin;folder=$folder;fingerprint=$fingerprint}
}
# Validate both targets completely before creating any link. No copy, deletion,
# SDK edits, environment installation or automatic package download occurs.
$linkRoot=Join-Path $root '.local-packages'
if(Test-Path -LiteralPath $linkRoot){
    $item=Get-Item -LiteralPath $linkRoot -Force
    if(!$item.PSIsContainer -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'Dependency link root must be an ordinary project directory'}
}elseif($VerifyOnly){throw 'Dependency link root missing'}else{[void][IO.Directory]::CreateDirectory($linkRoot)}
$records=@()
foreach($item in $validated){
    $link=Join-Path $root $item.pin.relativeDirectory
    if([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($link)) -ne $linkRoot){throw 'Unexpected link destination'}
    $created=$false
    if(Test-Path -LiteralPath $link){
        $existing=Get-Item -LiteralPath $link -Force
        if($existing.LinkType -ne 'Junction'){throw 'Existing dependency path is not a junction; leave it untouched'}
        $actual=[IO.DirectoryInfo]::new($link).ResolveLinkTarget($true).FullName.TrimEnd([char]'\')
        if(![string]::Equals($actual,$item.folder,[StringComparison]::OrdinalIgnoreCase)){throw 'Existing junction points elsewhere; leave it untouched'}
    }elseif($VerifyOnly){throw 'Dependency junction missing'}else{
        [void](New-Item -ItemType Junction -Path $link -Target $item.folder);$created=$true
    }
    $actual=[IO.DirectoryInfo]::new($link).ResolveLinkTarget($true).FullName.TrimEnd([char]'\')
    if(![string]::Equals($actual,$item.folder,[StringComparison]::OrdinalIgnoreCase)){throw 'Junction target verification failed'}
    $records+=@{name=$item.pin.name;version=$item.pin.version;reference=$item.pin.manifestReference;target=$actual;created=$created;fingerprint=$item.fingerprint}
}
[void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($report))
$value=[ordered]@{checkedAtUtc=[datetime]::UtcNow.ToString('o');status='PINNED_LOCAL_PACKAGE_LINKS_VERIFIED';verifyOnly=[bool]$VerifyOnly;packages=$records;packageBytesCopied=0;downloadedBytes=0;packageSourcesEdited=$false}
[IO.File]::WriteAllText($report,($value|ConvertTo-Json -Depth 8),[Text.UTF8Encoding]::new($false))
'VERIFIED: '+$records.Count+' pinned package junctions; no package copy/download/source edits'
