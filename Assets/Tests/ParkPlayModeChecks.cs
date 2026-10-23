#if UNITY_EDITOR
using System;
using System.Collections;
using MagicMR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Real Unity coroutine execution with explicit synthetic commands, never headset acceptance.
[InitializeOnLoad]
public sealed class ParkPlayModeChecks : MonoBehaviour
{
    static double deadline;
    static readonly string[] Names={"ParkTree","ParkBugs","ParkStatue","ParkRoom","ParkPlant","ParkPet"};
    static ParkPlayModeChecks() {
        if(SessionState.GetBool("MagicMR.PlayModeChecks.Active",false)) {
            deadline=SessionState.GetFloat("MagicMR.PlayModeChecks.Deadline",0);
            EditorApplication.playModeStateChanged+=OnPlayState;EditorApplication.update+=Watchdog;
        }
    }
    public static void Run() {
        Launch(false);
    }
    public static void RunSceneryReplay() {Launch(false,scenerySample:true);}
    public static void RunPetReplay() {Launch(false,petSample:true);}
    public static void RunCockroachReplay() {Launch(false,"Reports/cockroach-expanded-v3-unity-replay.jsonl",bugsSample:true);}
    public static void RunSpiderReplay() {Launch(false,"Reports/spider-joint-v3-unity-replay.jsonl",bugsSample:true);}
    public static void RunTransitionStress() {Launch(false,transitionStress:true);}
    public static void RunPlantSample() {Launch(true);}
    public static void RunPlantNegativeV4Sample() {Launch(true,"Reports/plant-negative-v4-actual-inference-replies.jsonl",2);}
    static void Launch(bool plantSample,string replyPath="Reports/plant-sample-actual-inference-replies.jsonl",int expectedCandidates=1,bool transitionStress=false,bool bugsSample=false,bool petSample=false,bool scenerySample=false) {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        SessionState.SetBool("MagicMR.PlayModeChecks.PlantSample",plantSample);
        SessionState.SetBool("MagicMR.PlayModeChecks.TransitionStress",transitionStress);
        SessionState.SetBool("MagicMR.PlayModeChecks.BugsSample",bugsSample);
        SessionState.SetBool("MagicMR.PlayModeChecks.PetSample",petSample);
        SessionState.SetBool("MagicMR.PlayModeChecks.ScenerySample",scenerySample);
        SessionState.SetString("MagicMR.PlayModeChecks.ReplyPath",replyPath);
        SessionState.SetInt("MagicMR.PlayModeChecks.ExpectedCandidates",expectedCandidates);
        deadline=EditorApplication.timeSinceStartup+120;
        SessionState.SetBool("MagicMR.PlayModeChecks.Active",true);
        SessionState.SetFloat("MagicMR.PlayModeChecks.Deadline",(float)deadline);
        EditorApplication.playModeStateChanged+=OnPlayState;
        EditorApplication.update+=Watchdog;
        EditorApplication.isPlaying=true;
    }
    static void Watchdog() {
        if(EditorApplication.timeSinceStartup>deadline) {Debug.LogError("PARK_PLAYMODE_TIMEOUT");EditorApplication.Exit(1);}
    }
    static void OnPlayState(PlayModeStateChange state) {
        if(state==PlayModeStateChange.EnteredPlayMode)new GameObject("Explicit synthetic interaction checks").AddComponent<ParkPlayModeChecks>();
    }
    void Start() {StartCoroutine(SessionState.GetBool("MagicMR.PlayModeChecks.ScenerySample",false)?CheckSceneryReplay():SessionState.GetBool("MagicMR.PlayModeChecks.PetSample",false)?CheckPetReplay():SessionState.GetBool("MagicMR.PlayModeChecks.BugsSample",false)?CheckCockroachReplay():SessionState.GetBool("MagicMR.PlayModeChecks.TransitionStress",false)?CheckTransitionStress():SessionState.GetBool("MagicMR.PlayModeChecks.PlantSample",false)?CheckPlantSample():Check());}
    static void Require(bool condition,string message) {if(!condition)throw new Exception(message);}
    static int VisibleParticles(ParkActor actor) {
        int count=0;foreach(var t in actor.GetComponentsInChildren<Transform>(true))
            if(t.name.StartsWith("Pooled feedback ",StringComparison.Ordinal) && t.gameObject.activeInHierarchy)count++;
        return count;
    }
    IEnumerator CheckSceneryReplay() {
        bool failed=false;ParkController controller=null;ParkSharedInterface ui=null;
        var blankReplay=new TextAsset("\n");string[] replies=null;
        try {
            var camera=new GameObject("Synthetic pet replay camera");camera.AddComponent<Camera>().tag="MainCamera";camera.AddComponent<AudioListener>();
            var root=new GameObject("Pet TCP replay fixture");root.SetActive(false);
            controller=root.AddComponent<ParkController>();controller.sharedInterface=true;controller.simulation=true;
            controller.registeredActors=new ParkActor[6];for(int i=0;i<6;i++)controller.registeredActors[i]=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MagicMR/Park/"+Names[i]+".prefab").GetComponent<ParkActor>();
            controller.scenario=ParkScenario.Tree;controller.actorPrefab=controller.registeredActors[0];
            ui=root.AddComponent<ParkSharedInterface>();ui.controller=controller;ui.replayMode=true;ui.replay=blankReplay;root.SetActive(true);
            replies=System.IO.File.ReadAllLines("Reports/tree-statue-unity-replay.jsonl");
        }catch(Exception e){Debug.LogException(e);failed=true;}
        yield return null;
        for(int i=0;!failed && i<replies.Length;i++) {
            if(i>0) {ui.Command("Replay");ui.Command("Replay");} // Separate recorded TCP sessions restart frame IDs.
            yield return new WaitForSecondsRealtime(ui.Selection.cooldownSeconds+.1f);
            ParkActor enemy=null;
            try {
                ParkScenario before=controller.scenario;ui.ApplyJson(replies[i],true);
                Require(controller.scenario==before,"Rejected model automatically switched scenes");
                Require(ui.Selection.Candidates.Count>0,"Real TCP response lost its pet candidate");
                ui.Choose(ui.Selection.Candidates[0].instance_id);enemy=controller.selected;
                Require(controller.scenario==(i==0?ParkScenario.Tree:ParkScenario.Statue) && enemy && enemy.virtualTarget,"Pet did not route to explicit virtual Pet preview at reply "+i+": "+ui.Feedback);
                Require(!enemy.Busy,"Detection triggered scenery animation");
                Require(!enemy.Busy && enemy.Affection==0,"Detection triggered pet interaction");
                Require(controller.Dispatch(ParkAction.Awaken,enemy.transform.position,true),"Scenery rejected simulated open-palm action");
            }catch(Exception e){Debug.LogException(e);failed=true;}
            yield return new WaitForSecondsRealtime(1.5f);
            try {
                Require(enemy && !enemy.Busy,"Dream summon did not finish");
                Require(enemy.Revision>0,"Scenery awaken did not commit");
                var scan=enemy.visual.Find("Virtual statue scan ring");
                if(enemy.scenario==ParkScenario.Statue)Require(scan && !scan.gameObject.activeSelf,"Statue scan remained active after completion");
                ui.Command("Entry");Require(!controller.selected && controller.InstanceCount==0,"Entry retained enemy ownership");
            }catch(Exception e){Debug.LogException(e);failed=true;}
            yield return new WaitForSecondsRealtime(.35f);
            try {Require(!enemy,"Entry did not reclaim old enemy");}catch(Exception e){Debug.LogException(e);failed=true;}
        }
        Debug.Log(failed?"SCENERY_REPLAY_FAILED":"SCENERY_REPLAY_PASS: recorded Tree/Statue replies -> manual scene preview -> simulated awaken/scan coroutine -> Entry reclamation; experimental auto gate retained, no PICO pose/gesture verification");
        Destroy(blankReplay);SessionState.SetBool("MagicMR.PlayModeChecks.Active",false);EditorApplication.Exit(failed?1:0);
    }
    IEnumerator CheckPetReplay() {
        bool failed=false;ParkController controller=null;ParkSharedInterface ui=null;
        var blankReplay=new TextAsset("\n");string[] replies=null;
        try {
            var camera=new GameObject("Synthetic pet replay camera");camera.AddComponent<Camera>().tag="MainCamera";camera.AddComponent<AudioListener>();
            var root=new GameObject("Pet TCP replay fixture");root.SetActive(false);
            controller=root.AddComponent<ParkController>();controller.sharedInterface=true;controller.simulation=true;
            controller.registeredActors=new ParkActor[6];for(int i=0;i<6;i++)controller.registeredActors[i]=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MagicMR/Park/"+Names[i]+".prefab").GetComponent<ParkActor>();
            controller.scenario=ParkScenario.Tree;controller.actorPrefab=controller.registeredActors[0];
            ui=root.AddComponent<ParkSharedInterface>();ui.controller=controller;ui.replayMode=true;ui.replay=blankReplay;root.SetActive(true);
            replies=System.IO.File.ReadAllLines("Reports/pet-coco-unity-replay.jsonl");
        }catch(Exception e){Debug.LogException(e);failed=true;}
        yield return null;
        for(int i=0;!failed && i<replies.Length;i++) {
            yield return new WaitForSecondsRealtime(ui.Selection.cooldownSeconds+.1f);
            ParkActor enemy=null;
            try {
                ParkScenario before=controller.scenario;ui.ApplyJson(replies[i],true);
                Require(controller.scenario==before,"Rejected model automatically switched scenes");
                Require(ui.Selection.Candidates.Count>0,"Real TCP response lost its pet candidate");
                ui.Choose(ui.Selection.Candidates[0].instance_id);enemy=controller.selected;
                Require(controller.scenario==ParkScenario.Pet && enemy && enemy.virtualTarget,"Pet did not route to explicit virtual Pet preview at reply "+i+": "+ui.Feedback);
                Require(enemy.species==(i==0?"Dog":"Cat"),"Recognized pet species lost in virtual preview");
                Require(!enemy.Busy && enemy.Affection==0,"Detection triggered pet interaction");
                Require(controller.Dispatch(ParkAction.Pull,enemy.transform.position,true),"Pet rejected simulated dream summon");
            }catch(Exception e){Debug.LogException(e);failed=true;}
            yield return new WaitForSecondsRealtime(1.5f);
            try {
                Require(enemy && !enemy.Busy,"Dream summon did not finish");
                var bubble=(Transform)typeof(ParkActor).GetField("bubble",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(enemy);
                Require(bubble.gameObject.activeInHierarchy,"Dream bubble not visible");
                enemy.SetTracked(false);enemy.SetTracked(true);
                Require(bubble.gameObject.activeInHierarchy && !enemy.Busy,"Pet recovery replayed or lost chosen dream");
                ui.Command("Entry");Require(!controller.selected && controller.InstanceCount==0,"Entry retained enemy ownership");
            }catch(Exception e){Debug.LogException(e);failed=true;}
            yield return new WaitForSecondsRealtime(.35f);
            try {Require(!enemy,"Entry did not reclaim old enemy");}catch(Exception e){Debug.LogException(e);failed=true;}
        }
        Debug.Log(failed?"PET_REPLAY_FAILED":"PET_REPLAY_PASS: recorded real Dog/Cat replies -> manual virtual preview with correct species -> simulated dream summon -> tracked-loss recovery -> Entry reclamation; no PICO pose/gesture verification");
        Destroy(blankReplay);SessionState.SetBool("MagicMR.PlayModeChecks.Active",false);EditorApplication.Exit(failed?1:0);
    }
    IEnumerator CheckCockroachReplay() {
        bool failed=false;ParkController controller=null;ParkSharedInterface ui=null;
        var blankReplay=new TextAsset("\n");string[] replies=null;
        try {
            var camera=new GameObject("Synthetic cockroach replay camera");camera.AddComponent<Camera>().tag="MainCamera";camera.AddComponent<AudioListener>();
            var root=new GameObject("Cockroach TCP replay fixture");root.SetActive(false);
            controller=root.AddComponent<ParkController>();controller.sharedInterface=true;controller.simulation=true;
            controller.registeredActors=new ParkActor[6];for(int i=0;i<6;i++)controller.registeredActors[i]=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MagicMR/Park/"+Names[i]+".prefab").GetComponent<ParkActor>();
            controller.scenario=ParkScenario.Tree;controller.actorPrefab=controller.registeredActors[0];
            ui=root.AddComponent<ParkSharedInterface>();ui.controller=controller;ui.replayMode=true;ui.replay=blankReplay;root.SetActive(true);
            replies=System.IO.File.ReadAllLines(SessionState.GetString("MagicMR.PlayModeChecks.ReplyPath","Reports/cockroach-expanded-v3-unity-replay.jsonl"));
        }catch(Exception e){Debug.LogException(e);failed=true;}
        yield return null;
        for(int i=0;!failed && i<replies.Length;i++) {
            yield return new WaitForSecondsRealtime(ui.Selection.cooldownSeconds+.1f);
            ParkActor enemy=null;
            try {
                ParkScenario before=controller.scenario;ui.ApplyJson(replies[i],true);
                Require(controller.scenario==before,"Rejected model automatically switched scenes");
                Require(ui.Selection.Candidates.Count>0,"Real TCP response lost its cockroach candidate");
                ui.Choose(ui.Selection.Candidates[0].instance_id);enemy=controller.selected;
                Require(controller.scenario==ParkScenario.Bugs && enemy && enemy.virtualTarget,"Cockroach did not route to explicit virtual Bugs preview at reply "+i+": "+ui.Feedback);
                string detectedClass=ui.Selection.Candidates[0].class_name;
                Require(enemy.species==detectedClass,"Selected bug species was replaced in preview");
                int legs=0;foreach(var part in enemy.GetComponentsInChildren<Transform>(true))if(part.name=="Pixel leg")legs++;
                Require(legs==(detectedClass=="Spider"?8:6),"Bug preview has incorrect leg count");
                Require(enemy.VirtualScore==0 && !enemy.Busy,"Detection triggered an attack without a gesture");
                Require(!controller.ConfirmVirtualAttack(null,enemy.transform.position,true) && enemy.VirtualScore==0,"Missing enemy selection caused an attack");
                Require(controller.ConfirmVirtualAttack(enemy,enemy.transform.position,true),"Selected virtual enemy rejected simulated pinch");
            }catch(Exception e){Debug.LogException(e);failed=true;}
            yield return new WaitForSecondsRealtime(1.5f);
            try {
                Require(enemy && !enemy.Busy && enemy.VirtualScore==100,"Virtual hit animation or score failed");
                Require(!controller.ConfirmVirtualAttack(enemy,enemy.transform.position,true) && enemy.VirtualScore==100,"Defeated enemy scored twice");
                ui.Command("Entry");Require(!controller.selected && controller.InstanceCount==0,"Entry retained enemy ownership");
            }catch(Exception e){Debug.LogException(e);failed=true;}
            yield return new WaitForSecondsRealtime(.35f);
            try {Require(!enemy,"Entry did not reclaim old enemy");}catch(Exception e){Debug.LogException(e);failed=true;}
        }
        Debug.Log(failed?"BUG_SPECIES_REPLAY_FAILED":"BUG_SPECIES_REPLAY_PASS: "+replies.Length+" real recorded TCP replies -> selected species/leg count -> simulated virtual attack -> single score -> Entry reclamation; model auto gate retained, no PICO pose/gesture verification");
        Destroy(blankReplay);SessionState.SetBool("MagicMR.PlayModeChecks.Active",false);EditorApplication.Exit(failed?1:0);
    }
    IEnumerator CheckTransitionStress() {
        bool failed=false;ParkController controller=null;
        var retired=new System.Collections.Generic.List<ParkActor>();
        string[] profiles={"tree","bugs","statue","room","plant","pet"};
        try {
            var camera=new GameObject("Synthetic transition camera");camera.AddComponent<Camera>().tag="MainCamera";camera.AddComponent<AudioListener>();
            var root=new GameObject("Synthetic transition controller");root.SetActive(false);
            controller=root.AddComponent<ParkController>();controller.sharedInterface=true;controller.simulation=true;
            controller.registeredActors=new ParkActor[6];for(int i=0;i<6;i++)controller.registeredActors[i]=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MagicMR/Park/"+Names[i]+".prefab").GetComponent<ParkActor>();
            controller.actorPrefab=controller.registeredActors[0];root.SetActive(true);
        }catch(Exception e){Debug.LogException(e);failed=true;}
        yield return null;
        for(int step=0;step<18 && !failed;step++) {
            try {
                var old=controller.selected;Vector3 anchor=old.transform.position;
                Require(controller.SwitchContent(profiles[step%6],"synthetic:"+step),"Rapid manual content switch rejected");
                Require(controller.selected==null && controller.InstanceCount==0,"Switch retained target ownership");
                Require(old.IsExiting && !old.tracked && !old.Busy,"Retired actor retained active tracking or action");
                Require(!old.TryAction(ParkAction.Awaken,anchor,true),"Retired actor accepted a gesture");
                Require(old.transform.position==anchor,"Exit moved anchor");
                retired.Add(old);controller.SpawnVirtual();
                Require(controller.selected && controller.selected.scenario==(ParkScenario)(step%6) && !controller.selected.IsExiting && controller.InstanceCount==1,"New scene inherited retired state");
            }catch(Exception e){Debug.LogException(e);failed=true;}
            yield return new WaitForSecondsRealtime(.035f);
        }
        yield return new WaitForSecondsRealtime(.35f);
        try {
            Require(controller.selected.EntryProgress==1,"New presentation did not finish entering");
            controller.selected.SetTracked(false);controller.selected.SetTracked(true);
            Require(controller.selected.EntryProgress==1,"Brief occlusion restarted the entrance");
            foreach(var old in retired)Require(!old,"Rapid switches leaked retired actor");
            var current=controller.selected;controller.ResetScene();
            Require(controller.InstanceCount==0 && !controller.selected,"Reset retained current target");
            retired.Add(current);
        }catch(Exception e){Debug.LogException(e);failed=true;}
        yield return new WaitForSecondsRealtime(.35f);
        try {foreach(var old in retired)Require(!old,"Reset left an actor after exit deadline");}catch(Exception e){Debug.LogException(e);failed=true;}
        Debug.Log(failed?"PARK_TRANSITION_STRESS_FAILED":"PARK_TRANSITION_STRESS_PASS: 18 switches across six scenes, immediate ownership release/input rejection, fixed anchors, timed reclamation and Reset; synthetic input only");
        SessionState.SetBool("MagicMR.PlayModeChecks.Active",false);EditorApplication.Exit(failed?1:0);
    }
    IEnumerator CheckPlantSample() {
        bool failed=false;ParkController controller=null;ParkSharedInterface ui=null;ParkActor plant=null;
        var blankReplay=new TextAsset("\n");
        try {
            new GameObject("Local plant replay camera").AddComponent<Camera>().tag="MainCamera";
            var root=new GameObject("Plant sample / explicit replay input");root.SetActive(false);
            controller=root.AddComponent<ParkController>();controller.sharedInterface=true;controller.simulation=true;
            controller.registeredActors=new ParkActor[6];for(int i=0;i<6;i++)controller.registeredActors[i]=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MagicMR/Park/"+Names[i]+".prefab").GetComponent<ParkActor>();
            controller.scenario=ParkScenario.Tree;controller.actorPrefab=controller.registeredActors[0];
            ui=root.AddComponent<ParkSharedInterface>();ui.controller=controller;ui.replayMode=true;ui.replay=blankReplay;root.SetActive(true);
        }catch(Exception e){Debug.LogException(e);failed=true;}
        yield return null;
        try {
            string replyPath=SessionState.GetString("MagicMR.PlayModeChecks.ReplyPath","Reports/plant-sample-actual-inference-replies.jsonl");
            string json=System.IO.File.ReadAllLines(replyPath)[0];ui.ApplyJson(json,true);
            Debug.Log("PLANT_SAMPLE_REPLY_SOURCE="+replyPath);
            Require(controller.scenario==ParkScenario.Tree && ui.Selection.Candidates.Count==SessionState.GetInt("MagicMR.PlayModeChecks.ExpectedCandidates",1),"Pending actual inference reply bypassed manual choice or lost candidate");
            Require(ui.CurrentFrame.model_validation.Contains("PENDING"),"Actual model validation marker lost before manual selection");
            ui.Choose(ui.Selection.Candidates[0].instance_id);plant=controller.selected;
            Require(controller.scenario==ParkScenario.Plant && plant && plant.virtualTarget,"Actual reply did not route manual selection to virtual plant preview");
            Require(!plant.label || !plant.label.gameObject.activeSelf,"Shared UI retained duplicate object status text");
            ui.Command("Test grow");Require(plant.Busy,"Shared panel did not dispatch simulated open-palm growth");
        }catch(Exception e){Debug.LogException(e);failed=true;}
        yield return new WaitForSecondsRealtime(1.5f);
        try {Require(plant.growthTime>.45f && !plant.Busy,"Plant did not finish growth from shared panel");ui.Command("Test bloom");Require(plant.Busy,"Shared panel did not dispatch simulated pinch bloom");}catch(Exception e){Debug.LogException(e);failed=true;}
        yield return new WaitForSecondsRealtime(1.5f);
        try {
            Require(plant.PlantBloomVisible && !plant.Busy,"Shared-panel bloom failed to complete");
            ui.Command("Entry");Require(controller.selected==null && controller.InstanceCount==0 && controller.recognitionLock==null,"Returning to recognition retained plant selection");
            Require(!plant.Busy && !plant.PlantBloomVisible,"Returning to recognition retained plant effects");
            Require(plant.IsExiting && !plant.TryAction(ParkAction.Bloom,plant.transform.position,true),"Exiting plant accepted a gesture");
        }catch(Exception e){Debug.LogException(e);failed=true;}
        yield return new WaitForSecondsRealtime(.35f);
        try {Require(!plant,"Exit presentation did not reclaim old actor");}catch(Exception e){Debug.LogException(e);failed=true;}
        Debug.Log(failed?"PLANT_SAMPLE_PLAYMODE_FAILED":"PLANT_SAMPLE_PLAYMODE_PASS: actual public-image YOLO/TCP reply replay -> candidate -> manual plant route -> shared-panel simulated growth -> bloom -> Entry cleanup; PENDING auto gate preserved, no physical pose or PICO gesture validation");
        Destroy(blankReplay);SessionState.SetBool("MagicMR.PlayModeChecks.Active",false);EditorApplication.Exit(failed?1:0);
    }
    IEnumerator Check() {
        var actions=new[]{ParkAction.Style,ParkAction.Reward,ParkAction.Awaken,ParkAction.Awaken,ParkAction.GrowUp,ParkAction.Pull};
        bool failed=false;
        for(int index=0;index<Names.Length;index++) {
            ParkActor actor=null;Vector3 anchor=Vector3.zero;int revision=0;
            try {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MagicMR/Park/"+Names[index]+".prefab");
                actor=Instantiate(prefab).GetComponent<ParkActor>();actor.virtualTarget=true;actor.gameObject.SetActive(true);actor.Build();
                anchor=actor.transform.position;revision=actor.Revision;
                Require(actor.TryAction(actions[index],anchor,true),"Synthetic action rejected: "+actor.scenario);
                Require(actor.Busy,"Accepted action did not start its coroutine: "+actor.scenario);
            } catch(Exception e) {Debug.LogException(e);failed=true;}
            if(failed)break;
            yield return new WaitForSecondsRealtime(.4f);
            try {Require(actor.Revision==revision+1,"Spell did not reach effect commit: "+actor.scenario);}catch(Exception e){Debug.LogException(e);failed=true;}
            yield return new WaitForSecondsRealtime(actor.transitionSeconds+.5f);
            if(actor.scenario==ParkScenario.Plant) {
                try {
                    Require(actor.growthTime>.45f,"Plant summon did not actually grow");
                    Require(actor.TryAction(ParkAction.Bloom,anchor,true),"Plant rejected synthetic confirmed pinch action");
                }catch(Exception e){Debug.LogException(e);failed=true;}
                yield return new WaitForSecondsRealtime(actor.transitionSeconds+.6f);
                try {
                    Require(actor.PlantBloomVisible && !actor.Busy,"Plant bloom coroutine failed to finish opening");
                    actor.Undo();
                }catch(Exception e){Debug.LogException(e);failed=true;}
                yield return new WaitForSecondsRealtime(.8f);
                try {Require(!actor.PlantBloomVisible,"Plant Undo retained its blossom");Require(actor.TryAction(ParkAction.Bloom,anchor,true),"Plant could not bloom again after Undo");}catch(Exception e){Debug.LogException(e);failed=true;}
                yield return new WaitForSecondsRealtime(.4f);
                actor.SetTracked(false);yield return null;
                try {Require(!actor.Busy && !actor.PlantBloomVisible,"Plant target loss retained running bloom");Require(actor.transform.position==anchor,"Plant animation moved tracking anchor");actor.SetTracked(true);Require(!actor.PlantBloomVisible,"Plant recovery replayed bloom without a gesture");}catch(Exception e){Debug.LogException(e);failed=true;}
                actor.ResetEffects();yield return null;
                try {Require(!actor.PlantBloomVisible && !actor.Busy,"Plant exit Reset retained blossom or animation");Debug.Log("PARK_PLAYMODE_PLANT_BLOOM_PASS: synthetic growth/bloom/undo/repeat/reset with actual coroutines; no YOLO or PICO acceptance");}catch(Exception e){Debug.LogException(e);failed=true;}
            }
            if(actor.scenario==ParkScenario.Room) {
                var ceiling=new GameObject("Explicit synthetic ceiling anchor");
                try {
                    actor.ResetEffects();actor.confirmedPlane=true;actor.environmentAnchor=ceiling.transform;
                    Require(!actor.TryAction(ParkAction.Reward,anchor,true) && !actor.Busy,"Room spawned unselected candy effect");
                    Require(actor.TryAction(ParkAction.Style,anchor,true),"Room rejected effect selection");
                }catch(Exception e){Debug.LogException(e);failed=true;}
                yield return new WaitForSecondsRealtime(actor.transitionSeconds+.7f);
                try {
                    Require(actor.RoomCandySelected,"Room did not select candy before summon");
                    Require(actor.TryAction(ParkAction.Awaken,anchor,true),"Selected candy did not route the common summon action");
                }catch(Exception e){Debug.LogException(e);failed=true;}
                yield return new WaitForSecondsRealtime(3f);
                try {
                    Require(!actor.Busy && VisibleParticles(actor)==24,"Actual candy rain failed to persist with bounded pool");
                    Require(actor.TryAction(ParkAction.Style,anchor,true),"Active candy rain rejected effect switch");
                }catch(Exception e){Debug.LogException(e);failed=true;}
                yield return new WaitForSecondsRealtime(actor.transitionSeconds+1f);
                try {
                    Require(!actor.Busy && !actor.RoomCandySelected && VisibleParticles(actor)==0,"Switch to jellyfish retained candy or unfinished animation");
                    Require(actor.confirmedPlane && actor.environmentAnchor==ceiling.transform,"Effect switch discarded confirmed ceiling");
                    Require(actor.TryAction(ParkAction.Style,anchor,true),"Room could not select candy a second time");
                }catch(Exception e){Debug.LogException(e);failed=true;}
                yield return new WaitForSecondsRealtime(actor.transitionSeconds+.7f);
                try {Require(actor.RoomCandySelected && actor.TryAction(ParkAction.Reward,anchor,true),"Second candy round failed after switch");}catch(Exception e){Debug.LogException(e);failed=true;}
                yield return new WaitForSecondsRealtime(3f);
                try {
                    Require(!actor.Busy && VisibleParticles(actor)==24,"Second candy round did not persist");
                    Require(actor.TryAction(ParkAction.Stop,anchor,true),"Actual candy rain rejected Stop");
                }catch(Exception e){Debug.LogException(e);failed=true;}
                yield return new WaitForSecondsRealtime(.5f);
                try {
                    Require(!actor.Busy && VisibleParticles(actor)==0,"Actual Stop failed to finish and recycle candy");
                    Debug.Log("PARK_PLAYMODE_ROOM_LIFECYCLE_PASS: synthetic ceiling, effect selection/switch/re-summon, sustained 24-object pool and completed Stop");
                }catch(Exception e){Debug.LogException(e);failed=true;}
                Destroy(ceiling);
            }
            if(actor.scenario==ParkScenario.Pet) {
                var bubble=actor.visual.Find("Dream bubble");
                try {Require(bubble && bubble.gameObject.activeInHierarchy,"Actual dream open did not display bubble");actor.SetTracked(false);}catch(Exception e){Debug.LogException(e);failed=true;}
                yield return new WaitForSecondsRealtime(.2f);
                actor.SetTracked(true);yield return null;
                try {
                    Require(bubble.gameObject.activeInHierarchy,"Brief synthetic occlusion lost dream state");
                    Require(actor.TryAction(ParkAction.Pull,anchor,true),"Recovered dream rejected close");
                }catch(Exception e){Debug.LogException(e);failed=true;}
                yield return new WaitForSecondsRealtime(actor.transitionSeconds+.6f);
                try {
                    Require(!actor.Busy && !bubble.gameObject.activeInHierarchy,"Actual recovered dream could not finish closing");
                    Debug.Log("PARK_PLAYMODE_PET_LIFECYCLE_PASS: actual open/close coroutines and synthetic tracked-loss recovery");
                }catch(Exception e){Debug.LogException(e);failed=true;}
            }
            try {
                Require(!actor.Busy && actor.transform.position==anchor,"Animation did not finish or moved tracking root: "+actor.scenario);
                if(actor.scenario==ParkScenario.Bugs)Require(actor.VirtualScore==100 && !actor.TryAction(ParkAction.Reward,anchor,true),"Virtual enemy score repeated");
                actor.ResetEffects();Require(!actor.Busy && actor.VirtualScore==0 && actor.Affection==0,"Reset retained state: "+actor.scenario);
                Debug.Log("PARK_PLAYMODE_SCENE_PASS: "+actor.scenario+" synthetic command, actual coroutine completion and Reset; no PICO gesture validation");
            } catch(Exception e){Debug.LogException(e);failed=true;}
            Destroy(actor.gameObject);yield return null;
            if(failed)break;
        }
        Debug.Log(failed?"PARK_PLAYMODE_FAILED":"PARK_PLAYMODE_PASS: six real coroutine runs; synthetic inputs, no headset validation");
        SessionState.SetBool("MagicMR.PlayModeChecks.Active",false);EditorApplication.Exit(failed?1:0);
    }
}

#endif
