using System;
using System.Reflection;
using MagicMR;
using UnityEditor;
using UnityEngine;
public static class ParkRegressionChecks
{
    static void Require(bool v,string message) { if(!v)throw new Exception(message); }
    public static void RunRecorder()
    {
        var go=new GameObject("Sent capture recorder regression");go.SetActive(false);
        var capture=go.AddComponent<PicoStereoCapture>();
        string folder=System.IO.Path.Combine(Application.temporaryCachePath,"park-recorder-check-"+Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(folder);
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        void Set(string key,object value)=>typeof(PicoStereoCapture).GetField(key,flags).SetValue(capture,value);
        try {
            capture.recordStereo=true;capture.maxRecordPairs=1;Set("recordDirectory",folder);
            var pairType=typeof(PicoStereoCapture).GetNestedType("ReplayPair",BindingFlags.NonPublic);
            var pair=Activator.CreateInstance(pairType,true);
            pairType.GetField("left_ns").SetValue(pair,123L);pairType.GetField("right_ns").SetValue(pair,124L);
            Set("pendingRecord",pair);Set("pendingLeftGray",new byte[]{1,2,3,4});Set("pendingRightGray",new byte[]{4,3,2,1});Set("pendingLeftRgba",new byte[16]);
            capture.RecordAcceptedPose(10,999,Pose.identity,Pose.identity);
            Require(System.IO.Directory.GetFiles(folder).Length==0,"Recorder associated wrong capture timestamp");
            capture.RecordAcceptedPose(10,123,Pose.identity,Pose.identity);
            Require(System.IO.File.Exists(System.IO.Path.Combine(folder,"123-left.gray")),"Sent capture has no raw data");
            Require(System.IO.File.ReadAllLines(System.IO.Path.Combine(folder,"pairs.jsonl")).Length==1,"Sent capture has no pair journal");
            var poses=System.IO.File.ReadAllLines(System.IO.Path.Combine(folder,"poses.jsonl"));
            Require(poses.Length==1 && poses[0].Contains("\"frame_id\":10") && poses[0].Contains("\"capture_ns\":123"),"Sent capture pose association failed");
            Set("nextRecord",0f);capture.RecordAcceptedPose(10,123,Pose.identity,Pose.identity);capture.RecordAcceptedPose(11,123,Pose.identity,Pose.identity);
            Require(System.IO.File.ReadAllLines(System.IO.Path.Combine(folder,"poses.jsonl")).Length==1,"Recorder duplicated or exceeded limit");
            Debug.Log("STEREO_RECORDER_REGRESSION_PASS: exact sent capture association, raw/pair/pose commit, duplicate/cap rejection; synthetic I/O fixture only");
        } finally {
            UnityEngine.Object.DestroyImmediate(go);
            foreach(var file in System.IO.Directory.GetFiles(folder))System.IO.File.Delete(file);
            System.IO.Directory.Delete(folder);
        }
    }
    [MenuItem("MagicMR/Checks/Park regressions")]
    public static void Run()
    {
        RunRecorder();
        foreach(ParkFeedbackShape shape in Enum.GetValues(typeof(ParkFeedbackShape))) {
            var mesh=ParkFeedbackMeshes.Get(shape);
            Require(mesh && mesh.vertexCount>0 && mesh.vertexCount<100 && mesh.triangles.Length>0,"Invalid feedback geometry: "+shape);
            Require(mesh==ParkFeedbackMeshes.Get(shape),"Feedback geometry was not reused");
        }
        foreach(var name in ParkContentBuilder.Names) {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MagicMR/Park/"+name+".prefab"); Require(prefab,"Missing actor prefab "+name);
            var go=UnityEngine.Object.Instantiate(prefab); go.SetActive(true); var actor=go.GetComponent<ParkActor>(); actor.Build();
            try {
                Vector3 anchor=go.transform.position; int revision=actor.Revision;
                Require(!actor.TryAction(ParkAction.Style,anchor,false),"Invalid hand triggered effect");
                Require(!actor.TryAction(ParkAction.Style,anchor+Vector3.one*5,true),"Distant hand triggered effect");
                Require(actor.Revision==revision,"Rejected action changed state");
                bool originalVirtual=actor.virtualTarget;
                actor.virtualTarget=true;
                Require(actor.TryAction(ParkAction.Style,anchor+Vector3.forward,true),"Virtual test rejected reachable remote gesture");
                actor.ResetEffects();actor.virtualTarget=false;
                Require(!actor.TryAction(ParkAction.Style,anchor+Vector3.forward,true),"Virtual range leaked into real target distance gate");
                actor.virtualTarget=originalVirtual;
                if(actor.scenario==ParkScenario.Room || actor.scenario==ParkScenario.Pet) {
                    actor.enabled=false;
                    Require(!actor.TryAction(ParkAction.Stop,anchor,true),"Disabled actor accepted stop coroutine");
                    Require(actor.TryAction(ParkAction.Reset,anchor,false),"Disabled actor could not clean up through Reset");
                    actor.enabled=true;
                }
                actor.SetHighlighted(true);
                var highlight=go.GetComponentsInChildren<Transform>(true);
                Transform selectedMarker=null;foreach(var child in highlight)if(child.name=="Selected virtual target highlight")selectedMarker=child;
                Require(selectedMarker && selectedMarker.gameObject.activeSelf,"Selected target missing highlight");
                actor.SetTracked(false);Require(!selectedMarker.gameObject.activeSelf,"Lost target retained selection highlight");
                Require(!actor.TryAction(ParkAction.Style,anchor,true),"Missing target accepted action"); Require(!actor.visual.gameObject.activeSelf,"Lost target left stale representation visible"); actor.SetTracked(true); Require(actor.visual.gameObject.activeSelf,"Reacquired target remained hidden");
                if(actor.scenario==ParkScenario.Statue || actor.scenario==ParkScenario.Room)Require(!actor.TryAction(ParkAction.Reward,anchor,true),"Environment effect without confirmed plane");
                actor.ResetEffects(); Require(!actor.Busy && actor.Affection==0,"Reset retained transient state");
                if(actor.scenario==ParkScenario.Plant) {
                    var growthFlags=BindingFlags.Instance|BindingFlags.NonPublic;
                    var growing=(Transform)typeof(ParkActor).GetField("branchRoot",growthFlags).GetValue(actor);
                    Vector3 initial=growing.localScale;
                    Require(actor.TryAction(ParkAction.GrowUp,anchor,true),"Fresh Reset retained an old interaction cooldown");
                    actor.growthTime=.9f;growing.localScale=new Vector3(1,1.275f,1);
                    actor.Undo();Require(Mathf.Abs(actor.growthTime-.45f)<.001f && growing.localScale==initial,"Undo failed to restore plant growth immediately");
                    growing.localScale=Vector3.one*2;actor.ResetEffects();Require(growing.localScale==initial,"Reset left plant growth until a later update");
                }
                if(actor.scenario==ParkScenario.Tree) {
                    var windFlags=BindingFlags.Instance|BindingFlags.NonPublic;
                    var stateField=typeof(ParkActor).GetField("state",windFlags);object snapshot=stateField.GetValue(actor);
                    snapshot.GetType().GetField("wind").SetValue(snapshot,true);stateField.SetValue(actor,snapshot);
                    var branches=(System.Collections.Generic.List<Transform>)typeof(ParkActor).GetField("branches",windFlags).GetValue(actor);
                    var step=typeof(ParkActor).GetMethod("StepTreeWind",windFlags);actor.windDirection=Vector3.right;
                    step.Invoke(actor,new object[]{.25f,1f});
                    Require(branches.Count>0 && Quaternion.Angle(branches[0].localRotation,Quaternion.Euler(0,0,40))>.1f,"Tree wind did not move virtual branches");
                    actor.EndHandInteraction();step.Invoke(actor,new object[]{.3f,1f});
                    Require(Quaternion.Angle(branches[0].localRotation,Quaternion.Euler(0,0,40))<.01f,"Interrupted wind did not settle");
                    actor.ResetEffects();foreach(var branch in branches)Require(float.IsFinite(branch.localRotation.x),"Reset retained invalid branch rotation");
                    var emit=typeof(ParkActor).GetMethod("Emit",windFlags);
                    var pool=(System.Collections.Generic.List<Transform>)typeof(ParkActor).GetField("pool",windFlags).GetValue(actor);
                    for(int season=0;season<4;season++) {
                        actor.seasonTime=season*.25f;emit.Invoke(actor,new object[]{ParkAction.Wind,24});
                        int emitted=(int)typeof(ParkActor).GetField("particleCount",windFlags).GetValue(actor);
                        Require(emitted==(season==3?0:24),"Winter emitted leaves or other season lost wind feedback");
                        if(season<3)Require(pool[0].GetComponent<MeshFilter>().sharedMesh==ParkFeedbackMeshes.Get(season==0?ParkFeedbackShape.Petal:ParkFeedbackShape.Leaf),"Season wind used incorrect feedback geometry");
                    }
                    actor.ResetEffects();
                    Require(go.transform.position==anchor,"Tree wind moved tracking anchor");
                }
                if(actor.scenario==ParkScenario.Statue) {
                    var scanFlags=BindingFlags.Instance|BindingFlags.NonPublic;
                    var scan=(Transform)typeof(ParkActor).GetField("statueScan",scanFlags).GetValue(actor);
                    var progress=typeof(ParkActor).GetMethod("SetScanProgress",scanFlags);
                    progress.Invoke(actor,new object[]{.5f});
                    Require(scan && scan.gameObject.activeSelf && scan.IsChildOf(actor.visual) && scan.localPosition.y>.04f && scan.localPosition.y<.6f,"Statue scan did not remain on the virtual representation");
                    progress.Invoke(actor,new object[]{float.NaN});Require(!scan.gameObject.activeSelf,"Invalid scan progress retained effect");
                    progress.Invoke(actor,new object[]{.5f});actor.ResetEffects();Require(!scan.gameObject.activeSelf && go.transform.position==anchor,"Reset retained scan or changed its tracking anchor");
                }
                if(actor.scenario==ParkScenario.Bugs) {
                    var scoreFlags=BindingFlags.Instance|BindingFlags.NonPublic;
                    var stateField=typeof(ParkActor).GetField("state",scoreFlags);object snapshot=stateField.GetValue(actor);
                    snapshot.GetType().GetField("rewarded").SetValue(snapshot,true);stateField.SetValue(actor,snapshot);
                    Require(actor.VirtualScore==100,"Defeated virtual enemy did not expose its score");
                    int before=actor.Revision;
                    Require(!actor.TryAction(ParkAction.Reward,anchor,true) && actor.VirtualScore==100 && actor.Revision==before,"Repeated attack changed completed round score");
                    actor.ResetEffects();Require(actor.VirtualScore==0,"Reset retained the previous virtual round score");
                }
                if(actor.scenario==ParkScenario.Room) {
                    var rainFlags=BindingFlags.Instance|BindingFlags.NonPublic;
                    var surface=new GameObject("Synthetic confirmed ceiling");
                    try {
                        actor.environmentAnchor=surface.transform;actor.confirmedPlane=true;
                        var stateField=typeof(ParkActor).GetField("state",rainFlags);object snapshot=stateField.GetValue(actor);
                        snapshot.GetType().GetField("rewarded").SetValue(snapshot,true);stateField.SetValue(actor,snapshot);
                        typeof(ParkActor).GetMethod("Emit",rainFlags).Invoke(actor,new object[]{ParkAction.Reward,24});
                        typeof(ParkActor).GetField("particleStart",rainFlags).SetValue(actor,Time.unscaledTime-3f);
                        var pool=(System.Collections.Generic.List<Transform>)typeof(ParkActor).GetField("pool",rainFlags).GetValue(actor);
                        int count=pool.Count;actor.SendMessage("Update");
                        int visible=0;foreach(var particle in pool)if(particle.gameObject.activeSelf)visible++;
                        Require(visible==24 && pool.Count==count,"Candy rain ended after one burst or grew its pool");
                        var desktopRoot=new GameObject("Synthetic desktop controller");
                        try {
                            var desktop=desktopRoot.AddComponent<ParkController>();desktop.simulation=true;desktop.selected=actor;
                            typeof(ParkController).GetMethod("ResetHandState",rainFlags).Invoke(desktop,new object[]{false});
                            actor.SendMessage("Update");
                            foreach(var particle in pool.GetRange(0,24))Require(particle.gameObject.activeSelf,"No-hand desktop simulation cancelled ongoing rain");
                        } finally {UnityEngine.Object.DestroyImmediate(desktopRoot);}
                        actor.EndHandInteraction();actor.SendMessage("Update");
                        foreach(var particle in pool)Require(!particle.gameObject.activeSelf,"Hand interruption retained continuous candy rain");
                        Require(actor.confirmedPlane && actor.environmentAnchor==surface.transform,"Hand interruption discarded the confirmed ceiling");
                        snapshot=stateField.GetValue(actor);snapshot.GetType().GetField("rewarded").SetValue(snapshot,true);stateField.SetValue(actor,snapshot);
                        typeof(ParkActor).GetMethod("Emit",rainFlags).Invoke(actor,new object[]{ParkAction.Reward,24});
                        actor.SendMessage("Update");
                        Require(actor.TryAction(ParkAction.Stop,anchor,true),"Active candy rain could not stop");
                        var exiting=(Transform[])typeof(ParkActor).GetField("stoppingEffects",rainFlags).GetValue(actor);
                        Require(exiting!=null && exiting.Length==27,"Stop did not include the visible candy pool in its transition");
                        Require(actor.TryAction(ParkAction.Stop,anchor,true) && exiting==typeof(ParkActor).GetField("stoppingEffects",rainFlags).GetValue(actor),"Repeated Stop restarted the exit transition");
                        // Complete the exit explicitly, then update within the old particle lifetime.
                        // This catches candy reappearing after a successful fade.
                        typeof(ParkActor).GetMethod("FinishFantasyExit",rainFlags).Invoke(actor,null);
                        typeof(ParkActor).GetField("particleStart",rainFlags).SetValue(actor,Time.unscaledTime);
                        actor.SendMessage("Update");
                        foreach(var particle in pool)Require(!particle.gameObject.activeSelf,"Completed exit respawned old candy on the next update");
                        actor.ResetEffects();foreach(var particle in pool)Require(!particle.gameObject.activeSelf,"Reset retained candy rain");
                    } finally {UnityEngine.Object.DestroyImmediate(surface);actor.ResetEffects();}
                }
                foreach(var invalidPoint in new[]{new Vector3(float.NaN,0,0),new Vector3(0,float.PositiveInfinity,0)}) {
                    Require(!actor.TryAction(ParkAction.Style,invalidPoint,true),"Invalid hand coordinate accepted style action");
                    Require(!actor.TryAction(ParkAction.Stop,invalidPoint,true),"Invalid hand coordinate accepted stop action");
                    Require(actor.TryAction(ParkAction.Reset,invalidPoint,false),"Reset required valid hand coordinates");
                }
                if(actor.scenario==ParkScenario.Pet) {
                    var dreamFlags=BindingFlags.Instance|BindingFlags.NonPublic;
                    var dream=(Transform)typeof(ParkActor).GetField("bubble",dreamFlags).GetValue(actor);
                    var stateField=typeof(ParkActor).GetField("state",dreamFlags);
                    var step=typeof(ParkActor).GetMethod("StepDreamTransition",dreamFlags);
                    void SetDream(bool open) {object snapshot=stateField.GetValue(actor);snapshot.GetType().GetField("bubble").SetValue(snapshot,open);stateField.SetValue(actor,snapshot);}
                    actor.ResetEffects();Vector3 rest=dream.localScale;
                    SetDream(true);step.Invoke(actor,new object[]{.15f});
                    Require(dream.gameObject.activeSelf && dream.localScale.x>0 && dream.localScale.x<rest.x,"Dream opening did not animate");
                    SetDream(false);step.Invoke(actor,new object[]{.05f});
                    Require(dream.gameObject.activeSelf,"Dream closing disappeared before transition ended");
                    SetDream(true);step.Invoke(actor,new object[]{.3f});
                    Require(dream.localScale==rest,"Interrupted closing could not reopen to full size");
                    SetDream(false);step.Invoke(actor,new object[]{.3f});
                    Require(!dream.gameObject.activeSelf,"Completed dream close retained visible content");
                    SetDream(true);step.Invoke(actor,new object[]{.1f});actor.ResetEffects();
                    Require(!dream.gameObject.activeSelf && dream.localScale==rest,"Reset retained a partial dream transition");
                    bool wasVirtual=actor.virtualTarget;actor.virtualTarget=false;actor.reliableHeadPose=false;
                    Require(!actor.TryAction(ParkAction.Poke,anchor,true) && actor.Feedback.Contains("head position is not verified"),"Unverified pet head accepted proximity interaction");
                    Require(!actor.TryAction(ParkAction.Feed,anchor,true) && actor.Feedback.Contains("head position is not verified"),"Unverified pet head accepted spatial feeding");
                    actor.virtualTarget=wasVirtual;
                }
                if(actor.scenario==ParkScenario.Room || actor.scenario==ParkScenario.Pet) {
                    var fields=BindingFlags.Instance|BindingFlags.NonPublic;
                    var bubble=(Transform)typeof(ParkActor).GetField("bubble",fields).GetValue(actor);
                    Vector3 originalScale=bubble.localScale;
                    typeof(ParkActor).GetField("stoppingEffects",fields).SetValue(actor,new[]{bubble});
                    typeof(ParkActor).GetField("stoppingScales",fields).SetValue(actor,new[]{originalScale});
                    bubble.localScale=originalScale*.1f;
                    actor.ResetEffects();
                    Require(bubble.localScale==originalScale,"Reset retained partially stopped effect scale "+name);
                    Require(typeof(ParkActor).GetField("stoppingEffects",fields).GetValue(actor)==null,"Reset retained stop transition "+name);
                }
                Require(go.transform.position==anchor,"Animation/reset changed tracking anchor");
                Require(go.GetComponentsInChildren<Renderer>(true).Length<100,"Unbounded geometry");
                Require(go.GetComponentsInChildren<AudioSource>().Length==1,"Unexpected audio sources");
                Vector3 bottom=anchor+new Vector3(.02f,-.1f,.03f);
                Require(actor.FitRepresentation(bottom,bottom+Vector3.up*.35f,true),"Manual fit rejected neutral geometry "+name);
                Require(Vector3.Distance(bottom,actor.ConfirmedBottomWorld)<.0001f && Mathf.Abs(actor.ConfirmedHeight-.35f)<.0001f,"Manual fit geometry mismatch "+name);
                Require(go.transform.position==anchor,"Manual fit moved tracking anchor "+name);
                actor.ResetEffects();Require(!actor.sizeConfirmed,"Reset retained manual fit "+name);
                var visual=actor.visual; actor.enabled=false; Require(!actor.Busy,"Disable retained coroutine");
                foreach(var renderer in go.GetComponentsInChildren<Renderer>(true))Require(renderer.sharedMaterial!=null,"Missing material");
            } finally { UnityEngine.Object.DestroyImmediate(go); }
        }
        var shader=Shader.Find("MagicMR/ParkSurface"); Require(shader && !ShaderUtil.ShaderHasError(shader),"Park shader failed");
        Debug.Log("PARK_REGRESSION_PASS: missing target, invalid hand, distance, plane gate, reset/disable, anchor isolation, resource bounds");
    }
}
