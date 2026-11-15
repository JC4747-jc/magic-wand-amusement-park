$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'build_process_ownership.ps1')
$born=[datetime]'2026-11-15T12:00:00Z'
function Record([int]$id,[int]$parent,[object]$time,[string]$name){[pscustomobject]@{ProcessId=$id;ParentProcessId=$parent;CreationDate=$time;Name=$name;WorkingSetSize=1MB}}
$fixture=@(
    (Record 100 99 $born 'Unity.exe'),
    (Record 101 100 $born.AddSeconds(1) 'bee_backend.exe'),
    (Record 102 101 $born.AddSeconds(2) 'clang++.exe'),
    (Record 104 100 $born.AddHours(-1) 'Existing user app with stale parent PID'),
    (Record 105 104 $born.AddSeconds(3) 'New child of unrelated existing app'),
    (Record 106 100 $born.AddSeconds(2) 'Real new child'),
    (Record 107 106 $born.AddMinutes(-10) 'Old app whose parent PID was reused'),
    (Record 109 102 $born.AddSeconds(4) 'Real grandchild'),
    (Record 110 100 $null 'Missing birth identity'),
    (Record 99 0 $born.AddDays(-1) 'Unrelated launcher')
)
$actual=@(Get-OwnedBuildProcesses -Processes $fixture -RootProcessId 100 -RootCreationTime $born)
$expected=@(100,101,102,106,109)
if(@(Compare-Object @($actual.ProcessId|Sort-Object) $expected).Count){throw 'PID reuse fixture attributed unrelated apps to the build'}
$wrongRoot=@(Get-OwnedBuildProcesses -Processes $fixture -RootProcessId 100 -RootCreationTime $born.AddMinutes(1))
if($wrongRoot.Count){throw 'Reused root PID was accepted with the wrong creation time'}
$absentRoot=@(Get-OwnedBuildProcesses -Processes $fixture -RootProcessId 999 -RootCreationTime $born)
if($absentRoot.Count){throw 'Missing root was accepted'}
$outOfOrder=@(Get-OwnedBuildProcesses -Processes @($fixture|Sort-Object ProcessId -Descending) -RootProcessId 100 -RootCreationTime $born)
if(@(Compare-Object @($outOfOrder.ProcessId|Sort-Object) $expected).Count){throw 'Process enumeration order changed ownership'}
'BUILD_PROCESS_OWNERSHIP_PASS: stale/reused parent PID, wrong or missing root birth, unknown child identity, transitive descendants and shuffled enumeration'
