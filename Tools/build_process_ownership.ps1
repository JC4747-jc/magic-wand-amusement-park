function Get-OwnedBuildProcesses {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [object[]]$Processes,
        [Parameter(Mandatory)] [int]$RootProcessId,
        [Parameter(Mandatory)] [datetime]$RootCreationTime
    )
    # ParentProcessId survives the parent's exit and PID reuse. A parent must
    # have been created no later than its child, including every ancestor edge.
    $root=@($Processes|Where-Object {[int]$_.ProcessId -eq $RootProcessId})
    if($root.Count -ne 1 -or -not $root[0].CreationDate){return}
    $rootUtc=([datetime]$root[0].CreationDate).ToUniversalTime()
    if([math]::Abs(($rootUtc-$RootCreationTime.ToUniversalTime()).TotalMilliseconds) -gt 10){return}
    $owned=[Collections.Generic.HashSet[int]]::new()
    $births=[Collections.Generic.Dictionary[int,datetime]]::new()
    [void]$owned.Add($RootProcessId);$births[$RootProcessId]=$rootUtc
    for($depth=0;$depth -lt 64;$depth++){
        $changed=$false
        foreach($candidate in $Processes){
            $parentId=[int]$candidate.ParentProcessId
            $childId=[int]$candidate.ProcessId
            if(-not $owned.Contains($parentId) -or $owned.Contains($childId) -or -not $candidate.CreationDate){continue}
            $childUtc=([datetime]$candidate.CreationDate).ToUniversalTime()
            if($childUtc -lt $births[$parentId]){continue}
            [void]$owned.Add($childId);$births[$childId]=$childUtc;$changed=$true
        }
        if(-not $changed){break}
    }
    $Processes|Where-Object {$owned.Contains([int]$_.ProcessId)}
}
