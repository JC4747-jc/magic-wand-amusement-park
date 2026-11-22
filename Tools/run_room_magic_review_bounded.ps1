#Requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$ProjectRoot,
    [ValidatePattern('^[a-z0-9-]{4,60}$')][string]$RunId='room-current-20261120',
    [string]$EditorOverrideManifest,
    [string]$ProjectInputManifest,
    [ValidateSet('RoomMagic','RoomPhotoScene','RoomWindowOwnership','PlantFlow','PlantVoice','PetDream','PetOutline','SeasonTree','SeasonTreeSelection','WillowTree','WillowTreeSelection','StatueStyle','StatuePlayback','StatueGarden','BronzePlayback','BugDance','LegacyStartup','CaptureTimeline','SampleCaptureTimeline','DogGait','DogWalkAuthor','DogWalkAcceptance','DogWalkInstalled','PetWalkLifecycle','CalibratedParkReplay','CalibratedRoomChain','BridgeRoomChain','BridgeRoomLivePicf','BridgePlantLivePicf','BridgePetLivePicf','BridgeRoutes','AndroidBuildPreflight','CalibratedPlantChain','CalibratedRoomVariants','CalibratedFurnitureHoldout','CalibratedTreeRange','KenyaTreeInteractionRange','Miti360TreeSelection','Miti360SeasonTreeHci','Miti360WillowTreeHci','Miti360SeasonTreeNightFill','Miti360WillowTreeNightFill','ParkTreeDepthFeedback','BridgeTreeEntry','LighterSavedEffects')][string]$Check='RoomMagic'
)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$candidate=(Resolve-Path -LiteralPath $ProjectRoot).Path
$checks=@{
    BridgeRoutes=@{method='ParkBridgeRouteChecks.Run';pattern='^PASS: original Bridge menu opened and returned from all six Park scenes';folder='park-bridge-route-checks';group='park-bridge-routes-20261122';scope='Actual saved Bridge and Park UI buttons in the Unity Editor, clean scene entry, labeled virtual preview and return. No physical devices or live recognition'}
    LighterSavedEffects=@{method='LighterSnapshotChecks.RunSavedScene';pattern='^PASS: saved BridgeStereoFusion lighter effects,';folder='lighter-saved-effects';group='lighter-saved-effects-20261122';scope='Original saved BridgeStereoFusion target and material/flower bindings, effects and reset through synthetic controller calls and an isolated review camera. No physical inputs, audible review or PICO acceptance'}
    CalibratedRoomVariants=@{method='CalibratedRoomMagicChainChecks.RunRoomVariants';pattern='^PASS: saved ParkRoom tracked PICF replay entered portal and snow,';folder='park-room-variants-checks';group='park-room-variants-20261121';scope='Actual calibrated public PICF and C# room tracking, same target menu switches, portal seed and snow via shared synthetic hands. No headset or physical hand claim'}
    CalibratedPlantChain=@{method='CalibratedRoomMagicChainChecks.RunPlant';pattern='^PASS: saved ParkPlant actual PICF and stereo replay selected one tracked plant,';folder='park-plant-chain-checks';group='park-plant-chain-20261121';scope='Public calibrated Plants pair, saved ParkPlant real PICF parsing, stereo, tracked instance and shared A/B/C/D using test poses and hand samples; no headset or independent project accuracy claim'}
    RoomPhotoScene=@{method='RoomPhotoSceneChecks.Run';pattern='^PASS: saved ParkRoom photo transfer;';folder='room-photo-scene';group='park-room-chain-20261121';scope='Saved room photo preview, gather/hold/release, reopen, ownership and target-loss cleanup in the Editor'}
    AndroidBuildPreflight=@{method='AndroidBuildPreflightChecks.Run';pattern='^PASS: Android Editor build preflight';folder='android-preflight-checks';group='android-build-preflight-20261122';scope='Android Editor target; full build preflight checks for natural models and shared interaction; no package or PICO claim'}
    BridgeRoomChain=@{method='CalibratedRoomMagicChainChecks.RunFromBridge';pattern='^PASS: saved BridgeStereoFusion acknowledged Room,';folder='bridge-room-chain-checks';group='bridge-room-chain-20261122';scope='Saved Bridge Room buttons and original ParkRoom TCP component; public PICF and stereo, synthetic pose and gestures. No headset acceptance'}
    BridgeRoomLivePicf=@{method='CalibratedRoomMagicChainChecks.RunLiveFromBridge';pattern='^PASS: saved BridgeStereoFusion sent live Room PICF,';folder='bridge-room-chain-checks';group='bridge-room-chain-20261122';scope='Saved Bridge to ParkRoom, original Unity JPEG sender and TCP receiving live YOLO on public stereo images, with supplied pose and synthetic hands. No headset acceptance'}
    BridgePlantLivePicf=@{method='CalibratedRoomMagicChainChecks.RunLivePlantFromBridge';pattern='^PASS: saved BridgeStereoFusion sent live Plant PICF,';folder='park-plant-chain-checks';group='bridge-plant-live-20261123';scope='Saved Bridge to ParkPlant, original Unity JPEG sender and TCP receiving live YOLO and exercising both plant experiences with public stereo images and synthetic hands. No headset acceptance'}
    BridgePetLivePicf=@{method='PetLiveRecognitionChecks.Run';pattern='^PASS: saved BridgeStereoFusion sent live Pet PICF,';folder='pet-live-recognition-checks';group='bridge-live-pet';scope='Saved Bridge to ParkPet with live optical YOLO recognition, candidate continuity and capture/head pose gates. No metric placement or headset acceptance'}
    CalibratedRoomChain=@{method='CalibratedRoomMagicChainChecks.Run';pattern='^PASS: saved ParkRoom actual PICF and stereo replay selected the tracked chair,';folder='park-room-chain-checks';group='park-room-chain-20261121';scope='Saved ParkRoom with calibrated public images, original PICF responses and real C# tracking, synthetic test poses, selection/calibration and shared A/B/C/D. No headset acceptance'}
    CalibratedParkReplay=@{method='CalibratedParkReplayChecks.Run';pattern='^PASS: saved ParkRoom and ParkPlant public-image PICF replay;';folder='park-calibrated-checks';group='park-calibrated-replay-20261121';scope='Actual saved Room and Plant receiver and C# matcher, public calibrated pairs and real PICF responses, explicitly injected pending state and test poses. No headset or tracked pose acceptance'}
    CalibratedTreeRange=@{method='CalibratedTreeRangeChecks.Run';pattern='^PASS: saved ParkTree rejected outdoor targets outside calibrated interaction range;';folder='kitti-tree-range-checks';group='public-tree-kitti-stereo-v2-20261122';scope='Saved ParkTree with actual external KITTI stereo and real PICF tree replies, test capture pose and range gate; no real headset or hands'}
    KenyaTreeInteractionRange=@{method='KenyaTreeInteractionRangeChecks.Run';pattern='^PASS: ParkTree interaction range checked with calibrated Kenya stereo replies\.';folder='kenya-tree-interaction-checks';group='public-tree-kenya-v3-20261122';scope='Saved ParkTree with calibrated real tree and distance-labelled trunk pairs; production receiver with test pose and no hand input'}
    Miti360TreeSelection=@{method='Miti360TreeSelectionChecks.Run';pattern='^PASS: Miti360 real stereo reached saved ParkTree tracking and synthetic point selection\.';folder='miti360-tree-selection-checks';group='public-tree-miti360-pilot-20261122';scope='Saved ParkTree tracking and point selection with calibrated Miti360 real images, untouched PICF replies and synthetic test pose and pinch'}
    Miti360SeasonTreeHci=@{method='HciSeasonTreeChecks.RunLive';pattern='^PASS: saved ParkTree natural mode,';folder='miti360-hci-season';group='public-tree-miti360-hci-20261122';scope='Public calibrated Miti360 stereo and original PICF replies, saved ParkTree seasonal A/B/C/D with synthetic hand trajectories and pose'}
    Miti360WillowTreeHci=@{method='HciSeasonTreeChecks.RunLiveWillow';pattern='^PASS: actual ParkTree fantasy variant;';folder='miti360-hci-willow';group='public-tree-miti360-hci-20261122';scope='Public calibrated Miti360 stereo and original PICF replies, saved ParkTree fantasy willow A/B/C/D on a selected Tree, with synthetic hand trajectories and pose'}
    Miti360SeasonTreeNightFill=@{method='HciSeasonTreeChecks.RunLive';pattern='^PASS: saved ParkTree natural mode,';folder='miti360-nightfill-season';group='willow-night-contrast-20261122';scope='Four Seasons unchanged through the public Miti360 PICF and synthetic gesture regression with dark willow-only leaf fill'}
    Miti360WillowTreeNightFill=@{method='HciSeasonTreeChecks.RunLiveWillow';pattern='^PASS: actual ParkTree fantasy variant;';folder='miti360-nightfill-willow';group='willow-night-contrast-20261122';scope='Dark willow readability candidate with public Miti360 PICF, synthetic gesture trajectories, state and render captures'}
    ParkTreeDepthFeedback=@{method='ParkTreeDepthFeedbackChecks.Run';pattern='^PASS: saved ParkTree reports interaction distance and stereo quality failures\.';folder='park-tree-depth-feedback-checks';group='park-tree-depth-feedback-20261122';scope='Saved ParkTree with original calibrated Kenya, KITTI and Miti360 PICF replies, range and match-quality feedback and synthetic near-target point selection'}
    BridgeTreeEntry=@{method='BridgeTreeEntryChecks.Run';pattern='^PASS: saved BridgeStereoFusion Entry routed original discovery PICF';folder='bridge-tree-entry-checks';group='public-tree-bridge-entry-20261122';scope='Saved BridgeStereoFusion Entry, saved ParkTree discovery and actual local TCP receipt of original research PICF; manual menu choice and separation between recognition and location'}
    CalibratedFurnitureHoldout=@{method='CalibratedParkReplayChecks.RunFurniture';pattern='^PASS: saved ParkRoom furniture holdout PICF replay;';folder='park-furniture-checks';group='public-room-plant-furniture-holdout-20261122';scope='Saved Room receiver and original C# matcher with frozen public couch and chair replies, synthetic timing and poses; no PICO or deployment acceptance'}
    DogWalkInstalled=@{method='NaturalDogGaitReview.RunInstalled';pattern='^PASS: actual saved ParkPet authored four-beat walk;';folder='dog-gait-review';group='pet-effect-review-20261120';scope='Saved ParkPet original Dog/Walk callbacks and saved authored dog controller without clip override; actual TwoBones support, joint speed, root/fit and idle checks over two cycles, normal-frame review renders; no physical contact, YOLO/HCI/PICO or final art acceptance'}
    PetWalkLifecycle=@{method='NaturalPetWalkPlayChecks.RunUIOnly';pattern='^PASS: actual saved ParkPet, original camera/input;';folder='pet-walk-play';group='pet-effect-review-20261120';scope='Saved original ParkPet Cat/Dog/Walk callbacks, skin movement, idle/Stop/Undo/Reset/loss/exit/live gates; old Cat manual-LBS ground scope not upgraded to actual player two-bone contact; no physical pointer/hand/YOLO/PICO or final art acceptance'}
    DogWalkAcceptance=@{method='NaturalDogGaitReview.RunAuthored';pattern='^PASS: actual saved ParkPet authored four-beat walk;';folder='dog-gait-review';group='pet-effect-review-20261120';scope='Original ParkPet virtual Dog/Walk callbacks, temporary runtime-only authored-clip override; actual TwoBones skin support for two live cycles and return idle; normal-frame independent renders, saved controller unchanged; no physical contact/YOLO/HCI/PICO or final art acceptance'}
    DogWalkAuthor=@{method='NaturalDogWalkAuthor.Run';pattern='^PASS: authored dog four-beat candidate;';folder='dog-walk-authoring';group='pet-effect-review-20261120';scope='Generate/revise only the owned four-beat clip in isolated D candidate with actual baked stance/target/rotation guards; preserve existing meta/GUID and original FBX/controller/production/fire; live quality and integration unaccepted';generated=@('Assets/ThirdParty/NaturalPets/Wolfhound/WolfhoundFourBeatWalk.anim','Assets/ThirdParty/NaturalPets/Wolfhound/WolfhoundFourBeatWalk.anim.meta');changed=@('Assets/ThirdParty/NaturalPets/Wolfhound/WolfhoundFourBeatWalk.anim')}
    DogGait=@{method='NaturalDogGaitReview.Run';pattern='^PASS: actual saved ParkPet dog gait measured;';folder='dog-gait-review';group='pet-effect-review-20261120';scope='Saved ParkPet virtual Dog and original Walk callback, two live Animator cycles, four skinned paw groups and temporary independent side-camera renders; geometry diagnostic only, no physical contact, natural walking, YOLO/HCI/PICO or final art acceptance'}
    RoomMagic=@{method='HciRoomMagicChecks.Run';pattern='^PASS: saved ParkRoom;';folder='hci-room-magic';group='room-effect-review-20261120';scope='Saved ParkRoom plus explicitly synthetic gesture/space input, actual Unity camera renders; no real YOLO/room mapping/hands/PICO or visual acceptance from assertions alone'}
    RoomWindowOwnership=@{method='RoomWindowOwnershipChecks.Run';pattern='^PASS: owned room window lifecycle;';folder='room-window-ownership';group='room-effect-review-20261120';scope='Native repeated component/owner destruction with an external window parent; no rendering, gesture, real-room, long-duration RAM or PICO acceptance'}
    PlantFlow=@{method='HciPlantFlowChecks.Run';pattern='^PASS [0-9a-f]{32}: saved ParkPlant;';folder='hci-plant-flow';group='plant-effect-review-20261120';scope='Saved ParkPlant synthetic dimension dispatch, fit, effects and lifecycle with actual Unity camera renders; no physical gesture/YOLO/PICO or art acceptance from assertions alone'}
    PlantVoice=@{method='HciPlantVoiceChecks.Run';pattern='^PASS: saved ParkPlant; B speech clip assigned';folder='hci-plant-voice';group='plant-effect-review-20261120';scope='Saved ParkPlant synthetic B dispatch, PCM clip/mouth envelope and stop/recovery state; no microphone, audible-output, physical gesture/YOLO/PICO acceptance'}
    PetDream=@{method='HciPetDreamChecks.Run';pattern='^PASS [0-9a-f]{32}: saved ParkPet, Cat\+Dog;';folder='hci-pet-dream';group='pet-effect-review-20261120';scope='Saved ParkPet Cat/Dog synthetic action dispatch for dream, pop, affection and lifecycle, with actual Unity camera renders; no physical gestures/YOLO/PICO or final art acceptance'}
    PetOutline=@{method='HciPetOutlineChecks.Run';pattern='^PASS: saved ParkPet, Cat\+Dog;';folder='hci-pet-outline';group='pet-effect-review-20261120';scope='Saved ParkPet Cat/Dog synthetic shared joint traces, outline pixels/bone following, independent dream/pop/affection and walk state; no physical gestures/YOLO/PICO, feeding/sleeping or final art acceptance'}
    SeasonTree=@{method='HciSeasonTreeChecks.Run';pattern='^PASS: saved ParkTree natural mode,';folder='hci-season-tree';group='tree-effect-review-20261120';scope='Saved ParkTree natural experience, synthetic shared joint traces and manual fit with actual Unity renders; no physical hand/YOLO/PICO or final art acceptance'}
    SeasonTreeSelection=@{method='HciSeasonTreeChecks.RunLive';pattern='^PASS: saved ParkTree natural mode,';folder='hci-live-season-tree';group='tree-effect-review-20261120';scope='Saved ParkTree real-target-mode selection/UI exercised by synthetic Observe data and joint traces; RunLive is a test name, not physical camera/network/YOLO/PICO evidence'}
    WillowTree=@{method='HciSeasonTreeChecks.RunWillow';pattern='^PASS: actual ParkTree fantasy variant;';folder='hci-willow';group='tree-effect-review-20261120';scope='Saved ParkTree willow effects, synthetic production joint traces, fit and lifecycle with actual Unity renders; no physical hand/YOLO/PICO or final art acceptance'}
    WillowTreeSelection=@{method='HciSeasonTreeChecks.RunLiveWillow';pattern='^PASS: actual ParkTree fantasy variant;';folder='hci-live-willow';group='tree-effect-review-20261120';scope='Saved ParkTree willow real-target-mode selection/UI exercised by synthetic Observe data and joint traces; no physical camera/network/YOLO/PICO evidence'}
    StatueStyle=@{method='HciStatueStyleChecks.Run';pattern='^PASS: saved ParkStatue, standing human';folder='hci-statue-style';group='statue-effect-review-20261120';scope='Saved ParkStatue human/bronze style, synthetic A/B/C traces and fitted geometry; no physical YOLO/hands/PICO or final art acceptance'}
    StatuePlayback=@{method='HciStatuePlaybackChecks.Run';pattern='^PASS: saved ParkStatue full-body human;';folder='hci-statue-playback';group='statue-effect-review-20261120';scope='Saved ParkStatue full-body animation, synthetic A/B/D traces, fitted anchor and pose lifecycle; no physical hand/YOLO/PICO or final art acceptance'}
    StatueGarden=@{method='HciStatueGardenChecks.Run';pattern='^PASS: saved ParkStatue standing human and bronze;';folder='hci-statue-garden';group='statue-effect-review-20261120';scope='Saved ParkStatue human/bronze garden, synthetic A/D traces and confirmed manual plane; no physical room/YOLO/hands/PICO or final art acceptance'}
    BronzePlayback=@{method='HciBronzePlaybackChecks.Run';pattern='^PASS: saved ParkStatue BronzeBeast;';folder='hci-bronze-playback';group='statue-effect-review-20261120';scope='Saved ParkStatue bronze full-body coroutine, synthetic A/B/D joint traces, confirmed test plane, anchor/fit and interruption lifecycle; no physical camera/YOLO/hands/audio/PICO or final art acceptance'}
    BugDance=@{method='HciBugDanceChecks.Run';pattern='^PASS: saved ParkBugs, Cockroach/Spider natural and pixel,';folder='hci-bug-dance';group='bug-effect-review-20261120';scope='Saved ParkBugs, both species and appearances; timed synthetic production B joint traces, complete coroutine, discrete steps, IK, fit/anchor and Reset; no physical camera/YOLO/hands/PICO or final art acceptance'}
    LegacyStartup=@{method='LegacySceneStartupProbe.Run';pattern='^PROBE_COMPLETE: 8 actual scene startups; 0 error logs\.';folder='legacy-startup';group='scene-startup-review-20261120';scope='Unmodified eight saved legacy/bridge/capture scene Windows Editor startups and original-camera snapshots only; no scene maintenance, effect dispatch, real camera/YOLO/hands/PICO or final visual acceptance'}
    CaptureTimeline=@{method='CaptureDemoTimelineChecks.Run';pattern='^PASS: actual DesktopCaptureDemo bindings,';folder='capture-animation-timeline';group='scene-startup-review-20261120';scope='Original DesktopCaptureDemo own-camera snapshot and capture/card/backpack animation/reset/disable/retarget lifecycle; no backend request, physical camera/hand/YOLO/PICO or final art acceptance'}
    SampleCaptureTimeline=@{method='CaptureDemoTimelineChecks.RunSample';pattern='^PASS: actual SampleScene bindings,';folder='sample-capture-animation-timeline';group='scene-startup-review-20261120';scope='Original SampleScene own-camera snapshot and capture/card/backpack animation/reset/disable/retarget lifecycle; no AR provider/backend, physical camera/hand/YOLO/PICO or final art acceptance'}
}
$selectedCheck=$checks[$Check]
$output=Join-Path $root ('Reports/'+$selectedCheck.group+'/'+$RunId)
$method=$selectedCheck.method;$resultPattern=$selectedCheck.pattern;$scope=$selectedCheck.scope
$captures=Join-Path $candidate ('.codex-tmp/'+$selectedCheck.folder)
if(Test-Path -LiteralPath $output){throw 'Existing evidence; inspect before choosing a new run'}
if(Test-Path -LiteralPath $captures){throw 'Preserve prior captures; never overwrite another run'}
if(@(Get-Process Unity -ErrorAction SilentlyContinue).Count){throw 'Leave existing Unity processes untouched'}
if((Get-CimInstance Win32_OperatingSystem).FreePhysicalMemory -lt 6MB){throw 'Need 6 GiB available RAM'}
$drive=[IO.DriveInfo]::new([IO.Path]::GetPathRoot($candidate))
if((Get-PSDrive C).Free -lt 20GB -or $drive.AvailableFreeSpace -lt 25GB){throw 'Disk floor crossed'}
foreach($name in @('Assets','Packages','ProjectSettings')){if(!(Test-Path -LiteralPath (Join-Path $candidate $name) -PathType Container)){throw 'Configured Unity project required'}}
$metadataPath=Join-Path $candidate 'SOURCE_VALIDATION.json'
if($ProjectInputManifest){
    if($EditorOverrideManifest){throw 'A direct project input manifest already describes the final inputs'}
    $metadataPath=(Resolve-Path -LiteralPath $ProjectInputManifest).Path
    $reportsPrefix=[IO.Path]::GetFullPath((Join-Path $root 'Reports'))+[IO.Path]::DirectorySeparatorChar
    if(!$metadataPath.StartsWith($reportsPrefix,[StringComparison]::OrdinalIgnoreCase)){throw 'Use a workspace Reports input manifest'}
}
$metadata=Get-Content -LiteralPath $metadataPath -Raw -Encoding utf8|ConvertFrom-Json
$override=$null
$overrideMap=@{}
if($EditorOverrideManifest){
    $manifestPath=(Resolve-Path -LiteralPath $EditorOverrideManifest).Path
    $reportsPrefix=[IO.Path]::GetFullPath((Join-Path $root 'Reports'))+[IO.Path]::DirectorySeparatorChar
    if(!$manifestPath.StartsWith($reportsPrefix,[StringComparison]::OrdinalIgnoreCase)){throw 'Editor override must be an explicit workspace Reports receipt'}
    $override=Get-Content -LiteralPath $manifestPath -Raw -Encoding utf8|ConvertFrom-Json
    $changes=if($override.overrides){@($override.overrides)}else{@($override)}
    if($changes.Count -gt 2){throw 'At most the room fixture and RoomStyle shader may be overlaid'}
    foreach($change in $changes){
        if($change.path -notin @('Assets/Editor/HciRoomMagicChecks.cs','Assets/Resources/MagicMR/RoomStyle.shader')){throw 'Only reviewed non-fire room fixture/shader paths may be overlaid'}
        $original=@($metadata.payloadFiles|Where-Object path -eq $change.path)
        if($original.Count -ne 1 -or $original[0].sha256 -ne $change.previousSha256 -or $overrideMap.ContainsKey($change.path)){throw 'Room overlay does not bind a unique published baseline'}
        $overrideMap[$change.path]=$change.sha256
    }
}
foreach($file in $metadata.payloadFiles){
    $expected=if($overrideMap.ContainsKey($file.path)){$overrideMap[$file.path]}else{$file.sha256}
    if((Get-FileHash -LiteralPath (Join-Path $candidate $file.path)).Hash.ToLowerInvariant() -ne $expected){throw ('Candidate payload changed before room review: '+$file.path)}
}
$files=@(foreach($name in @('Assets','Packages','ProjectSettings')){
    Get-ChildItem -LiteralPath (Join-Path $candidate $name) -Recurse -File|ForEach-Object {
        @{path=$_.FullName;sha256=(Get-FileHash -LiteralPath $_.FullName).Hash.ToLowerInvariant()}
    }
})
if($ProjectInputManifest){
    if($metadata.candidateRoot -ne $candidate.Replace('\','/')){throw 'Input manifest candidate root mismatch'}
    $expectedProject=@($metadata.payloadFiles | ForEach-Object {
        $path=[IO.Path]::GetFullPath((Join-Path $candidate $_.path))
        if($_.path -cnotmatch '^(Assets|Packages|ProjectSettings)/' -or $path.Replace('\','/') -cne ($candidate.Replace('\','/')+'/'+$_.path)){
            throw ('Invalid project input path: '+$_.path)
        }
        $path
    })
    if($expectedProject.Count -ne $files.Count -or @(Compare-Object $expectedProject $files.path).Count){
        throw 'Input manifest and project file inventory differ'
    }
}
[void][IO.Directory]::CreateDirectory($output)
$files|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $output 'source-inputs.json') -Encoding utf8
. (Join-Path $PSScriptRoot 'build_process_job.ps1')
. (Join-Path $PSScriptRoot 'build_process_ownership.ps1')
$log=Join-Path $output 'unity.log';$result=Join-Path $captures 'result.txt'
$process=$null;$job=$null;$stop=$null;$passed=$false
$samples=[Collections.Generic.List[object]]::new();$clock=[Diagnostics.Stopwatch]::StartNew()
$freeBefore=$drive.AvailableFreeSpace
try{
    $argsUnity=@('-batchmode','--burst-disable-compilation','-projectPath',('"'+$candidate+'"'),'-job-worker-count','1',
        '-executeMethod',$method,'-logFile',('"'+$log+'"'))
    if($Check -eq 'AndroidBuildPreflight'){$argsUnity+=@('-buildTarget','Android')}
    $process=Start-Process -FilePath 'D:/Unity/Hub/Editor/6000.0.33f1/Editor/Unity.exe' -ArgumentList $argsUnity -WorkingDirectory $candidate -WindowStyle Hidden -PassThru
    $birth=$process.StartTime;$job=[MagicMRBuildJob]::Attach($process)
    while(!$process.HasExited){
        $ram=(Get-CimInstance Win32_OperatingSystem).FreePhysicalMemory/1MB
        $rss=0;$all=@(Get-CimInstance Win32_Process)
        foreach($owned in @(Get-OwnedBuildProcesses -Processes $all -RootProcessId $process.Id -RootCreationTime $birth)){
            $p=Get-Process -Id $owned.ProcessId -ErrorAction SilentlyContinue
            if($p -and $job.Contains($p)){$rss+=$p.WorkingSet64/1MB}
        }
        $growth=[Math]::Max(0,($freeBefore-$drive.AvailableFreeSpace)/1MB)
        $captureBytes=0
        if(Test-Path -LiteralPath $captures){$captureBytes=(Get-ChildItem -LiteralPath $captures -File|Measure-Object Length -Sum).Sum}
        $samples.Add(@{elapsed=$clock.Elapsed.TotalSeconds;freeRAMGiB=$ram;treeRSSMiB=$rss;netGrowthMiB=$growth;captureBytes=$captureBytes})
        if($ram -lt 3 -or $rss -gt 3584 -or $growth -gt 512 -or $captureBytes -gt 32MB -or $clock.Elapsed.TotalSeconds -gt 240 -or
            ((Test-Path -LiteralPath $log) -and (Get-Item -LiteralPath $log).Length -gt 8MB)){$stop='Room native review resource/deadline guard';$job.Dispose();$job=$null;break}
        Start-Sleep -Milliseconds 500;$process.Refresh()
    }
    [void]$process.WaitForExit(8000)
    if($stop -or !$process.HasExited -or $process.ExitCode -ne 0 -or !(Test-Path -LiteralPath $result)){throw 'Room review did not complete; preserve source/result/log before retrying'}
    $verdict=Get-Content -LiteralPath $result -Raw -Encoding utf8
    if($verdict -cnotmatch $resultPattern){throw 'Current scene assertions failed; do not substitute another check or an older pass'}
    $allowedChanged=@(foreach($path in $selectedCheck.changed){[IO.Path]::GetFullPath((Join-Path $candidate $path))})
    $changedAssets=@(foreach($file in $files){$actual=(Get-FileHash -LiteralPath $file.path).Hash.ToLowerInvariant();if($actual -ne $file.sha256){if($file.path -notin $allowedChanged){throw ('Source changed during review: '+$file.path)};@{path=$file.path;previousSha256=$file.sha256;sha256=$actual}}})
    $knownPaths=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach($file in $files){[void]$knownPaths.Add($file.path)}
    $newFiles=@(foreach($name in @('Assets','Packages','ProjectSettings')){Get-ChildItem -LiteralPath (Join-Path $candidate $name) -Recurse -File|Where-Object {!$knownPaths.Contains($_.FullName)}})
    $expectedGenerated=@(foreach($path in $selectedCheck.generated){$absolute=[IO.Path]::GetFullPath((Join-Path $candidate $path));if(!$knownPaths.Contains($absolute)){$absolute}})
    if($newFiles.Count -ne $expectedGenerated.Count -or ($newFiles.Count -gt 0 -and @(Compare-Object @($newFiles.FullName|Sort-Object) @($expectedGenerated|Sort-Object)).Count)){throw 'Unexpected new source files during review; preserve outputs and inspect'}
    $generatedAssets=@(foreach($file in $newFiles){if($file.Length -gt 2MB){throw 'Generated asset exceeds explicit 2 MiB budget'};@{path=$file.FullName;bytes=$file.Length;sha256=(Get-FileHash -LiteralPath $file.FullName).Hash.ToLowerInvariant()}})
    $passed=$true
    Write-Output ('PASS: '+$method+'; '+$scope)
}finally{
    if($job){$job.Dispose()}
    @{checkedAtUtc=[datetime]::UtcNow.ToString('o');passed=$passed;guardStop=$stop;
      exitCode=$(if($process -and $process.HasExited){$process.ExitCode}else{$null});
      candidateRoot=$candidate;captures=$captures;check=$method;
      sourceMetadataSha256=(Get-FileHash -LiteralPath $metadataPath).Hash.ToLowerInvariant();
      editorOverride=$override;
      generatedAssets=$generatedAssets;
      changedAssets=$changedAssets;
      launcherSha256=(Get-FileHash -LiteralPath $PSCommandPath).Hash.ToLowerInvariant();
      seconds=$clock.Elapsed.TotalSeconds;samples=$samples.ToArray();
      scope=$scope}|
      ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $output 'resources.json') -Encoding utf8
}
