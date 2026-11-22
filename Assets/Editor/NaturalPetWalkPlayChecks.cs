using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MagicMR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class NaturalPetWalkPlayChecks
{
    const string Key="MagicMR.PetWalkReview";
    static readonly string Output=Path.GetFullPath(".codex-tmp/pet-walk-play");
    static ParkController controller;static ParkSharedInterface ui;static Camera camera;static ParkActor cat,dog;
    static int stage,errors,frames;static double next,started;static Vector3 root;static Quaternion rotation;static Vector3[][] initial;
    static float catFloor,maximumPlayPenetration;static int contactSamples;static double nextContact;
    static ParkActor mealActor;static NaturalPetRig mealRig;static float mealFloor,mealJawMaximum,mealHeadDrop,mealFootPenetration;
    static Vector3 mealHeadRest;static int mealConsumed,mealLastFrame;static double mealStarted;static Transform mealJaw;
    static Quaternion mealNeutralJaw;static bool mealStartCaptured,mealEmptyCaptured;
    static readonly float[] morselApproach=new float[3];static bool mealBiteCaptured;
    static readonly List<string> meals=new List<string>();
    static ParkActor restingPet;static NaturalPetRig restingRig;static Transform restingBody,restingTurn;
    static Quaternion restTurnAtStart;
    static float restFloor,restHeight,restStandingSize,restMaximumDown,restPenetration,restContactGap,breathLow,breathHigh;
    static double restStart,settledAt;static int settledFrames,restFrame;
    static readonly List<string> restMotion=new List<string>();
    static NaturalPetWalkPlayChecks(){EditorApplication.update+=Tick;Application.logMessageReceived+=Log;}
    static void Log(string message,string trace,LogType type){if(SessionState.GetBool(Key,false)&&(type==LogType.Error||type==LogType.Exception||type==LogType.Assert))errors++;}
    static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
    public static void Run()
    {
        BicolorCatAssetBuilder.Install();NaturalDogAssetBuilder.Install();NaturalCatWalkChecks.Run();NaturalPetModelChecks.Run();NaturalDogModelChecks.Run();NaturalPetMotionFrames.Run();
        RunUIOnly();
    }
    public static void RunUIOnly()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Pet UI check requires idle editor");
        Directory.CreateDirectory(Output);File.WriteAllText(Path.Combine(Output,"result.txt"),"RUNNING\n");
        SessionState.SetString(Key+".Scene",UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);SessionState.SetInt(Key+".Exit",-1);SessionState.SetBool(Key,true);
        EditorSceneManager.OpenScene("Assets/Scenes/ParkPet.unity");EditorApplication.EnterPlaymode();
    }
    static void Click(string name)
    {
        var button=camera.GetComponentsInChildren<Button>(false).Single(b=>b.name==name);var rect=button.GetComponent<RectTransform>();
        var center=rect.TransformPoint(rect.rect.center);Require(ParkSharedInterface.TryButtonRayHit(rect,new Ray(camera.transform.position,center-camera.transform.position),out _),"Pet UI ray cannot reach "+name);button.onClick.Invoke();
    }
    static void Later(float seconds){stage++;next=EditorApplication.timeSinceStartup+seconds;}
    static void FixedRoot(ParkActor actor){Require(actor.transform.position==root&&actor.transform.rotation==rotation,"Walk preview moved tracking root");}
    static void StartMealCheck(ParkActor actor)
    {
        mealActor=actor;mealRig=actor.GetComponentInChildren<NaturalPetRig>();mealFloor=NaturalDogAssetBuilder.Geometry(actor.gameObject).min.y;
        mealHeadRest=mealRig.Head.position;mealJawMaximum=mealHeadDrop=mealFootPenetration=0;mealConsumed=0;mealLastFrame=-1;
        mealJaw=mealRig.GetComponentsInChildren<Transform>().Single(t=>t.name==(actor.species=="Dog"?"Jaw1":"Wolf_Head_JawSHJnt_12"));
        mealStartCaptured=mealEmptyCaptured=mealBiteCaptured=false;
        for(int i=0;i<morselApproach.Length;i++)morselApproach[i]=float.PositiveInfinity;
        mealStarted=EditorApplication.timeSinceStartup;
        Require(controller.Dispatch(ParkAction.Feed,actor.transform.position,true)&&!actor.PetWalking,"Feeding did not interrupt walking");
    }
    static void ObserveMeal()
    {
        if(!mealActor||!mealActor.PetFeeding||mealLastFrame==Time.frameCount)return;
        mealLastFrame=Time.frameCount;FixedRoot(mealActor);
        Require(mealRig.Animator.speed==0,"Meal pose did not hold the neutral stance");
        var tray=mealActor.GetComponentsInChildren<Transform>(true).Single(x=>x.name=="Virtual meal dish");
        Require(tray.gameObject.activeInHierarchy&&tray.childCount==5,"Bounded meal props missing");
        Require(tray.GetComponentsInChildren<Transform>(false).Count(t=>t.name.StartsWith("Morsel "))==3-mealRig.ConsumedMorsels,"Meal props disagree with consumption");
        if(!mealStartCaptured)
        {
            Require(mealRig.ConsumedMorsels==0&&mealRig.MealJawAngle<1,"Meal opening evidence missed the neutral jaw");
            mealNeutralJaw=mealJaw.localRotation;mealStartCaptured=true;CapturePetDetail(mealActor,mealRig,mealActor.species+"-Meal-Approach",tray);
        }
        mealConsumed=Mathf.Max(mealConsumed,mealRig.ConsumedMorsels);
        float jawAngle=Quaternion.Angle(mealNeutralJaw,mealJaw.localRotation);
        for(int i=mealRig.ConsumedMorsels;i<morselApproach.Length;i++)
        {
            var morsel=tray.GetChild(i+2);
            float gap=Vector3.Distance(morsel.position,mealRig.MealMuzzlePosition);
            morselApproach[i]=Mathf.Min(morselApproach[i],gap);
            if(!mealBiteCaptured&&gap<.01f&&jawAngle>2)
            {
                mealBiteCaptured=true;Capture(mealActor.species+"-Feeding");CapturePetDetail(mealActor,mealRig,mealActor.species+"-Meal-Bite",tray);
            }
        }
        if(mealConsumed==3&&!mealEmptyCaptured){mealEmptyCaptured=true;CapturePetDetail(mealActor,mealRig,mealActor.species+"-Meal-Empty",tray);}
        mealJawMaximum=Mathf.Max(mealJawMaximum,jawAngle);
        mealHeadDrop=Mathf.Max(mealHeadDrop,mealHeadRest.y-mealRig.Head.position.y);
        mealFootPenetration=Mathf.Max(mealFootPenetration,mealFloor-NaturalDogAssetBuilder.Geometry(mealActor.gameObject).min.y);
    }
    static bool FinishMealCheck()
    {
        Require(EditorApplication.timeSinceStartup-mealStarted<9,"Meal failed to complete");
        if(mealActor.Busy)return false;
        Require(mealStartCaptured&&mealBiteCaptured&&mealEmptyCaptured&&mealConsumed==3&&mealJawMaximum>7&&mealHeadDrop>.006f,"In-game meal did not show all bites/head motion: "+mealActor.species+" head="+mealHeadDrop);
        Require(morselApproach.Max()<.012f,"Morsel disappeared before it reached the muzzle: "+morselApproach.Max());
        Require(mealFootPenetration<.004f&&!mealActor.PetFeeding&&mealRig.Animator.speed>0,"Meal changed the ground contact or animation recovery");
        Require(!mealActor.GetComponentsInChildren<Transform>(false).Any(x=>x.name=="Virtual meal dish"),"Finished meal left the dish visible");
        Require(mealActor.Affection==1,"Meal changed affection more than once");
        meals.Add(FormattableString.Invariant($"{mealActor.species}: eaten={mealConsumed} head_drop_m={mealHeadDrop} jaw_angle_deg={mealJawMaximum} max_morsel_gap_m={morselApproach.Max()} max_floor_penetration_m={mealFootPenetration}"));
        mealActor=null;mealRig=null;return true;
    }
    static void StartRestProbe(ParkActor actor)
    {
        restingPet=actor;restingRig=actor.GetComponentInChildren<NaturalPetRig>();
        var bones=restingRig.GetComponentsInChildren<Transform>();
        restingBody=bones.Single(t=>t.name==(actor.species=="Dog"?"spine2":"Wolf_Spine_04SHJnt_18"));
        restingTurn=bones.Single(t=>t.name==(actor.species=="Dog"?"wolfhound":"Wolf_ROOTSHJnt_38"));
        restTurnAtStart=restingTurn.rotation;
        var bounds=NaturalDogAssetBuilder.Geometry(actor.gameObject);
        restFloor=bounds.min.y;restStandingSize=bounds.size.y;
        restHeight=restingBody.position.y;
        restMaximumDown=restPenetration=restContactGap=0;settledFrames=0;restFrame=-1;
        breathLow=float.PositiveInfinity;breathHigh=float.NegativeInfinity;
        settledAt=-1;restStart=EditorApplication.timeSinceStartup;
        Click("Sleep");Require(actor.PetRestRequested,"Sleep control did not target the virtual "+actor.species);
    }
    static bool RestEvidenceReady()
    {
        FixedRoot(restingPet);
        double now=EditorApplication.timeSinceStartup;
        Require(now-restStart<10,"Sleep transition timed out: "+restingPet.species);
        if(restFrame==Time.frameCount)return false;
        restFrame=Time.frameCount;
        Require(!restingRig.Animator.enabled,"The locomotion Animator retained the rest pose bones");
        restMaximumDown=Mathf.Max(restMaximumDown,restHeight-restingBody.position.y);
        var bounds=NaturalDogAssetBuilder.Geometry(restingPet.gameObject);
        restPenetration=Mathf.Max(restPenetration,restFloor-bounds.min.y);
        if(restingRig.RestAmount<.999f)return false;
        if(settledAt<0)
        {
            settledAt=now;
            Capture(restingPet.species+"-Sleep-Scene");
            CapturePetDetail(restingPet,restingRig,restingPet.species+"-Sleep-Closeup");
        }
        settledFrames++;
        restContactGap=Mathf.Max(restContactGap,bounds.min.y-restFloor);
        float height=restingBody.position.y;
        breathLow=Mathf.Min(breathLow,height);breathHigh=Mathf.Max(breathHigh,height);
        if(now-settledAt<2.9f)return false;
        float excursion=breathHigh-breathLow;
        float sideAngle=Quaternion.Angle(restTurnAtStart,restingTurn.rotation);
        float relativeHeight=bounds.size.y/restStandingSize;
        restMotion.Add(FormattableString.Invariant($"{restingPet.species}: chest_drop_m={restMaximumDown} side_angle_deg={sideAngle} breath_m={excursion} floor_penetration_m={restPenetration} max_floor_gap_m={restContactGap} height_ratio={relativeHeight} samples={settledFrames}"));
        File.WriteAllLines(Path.Combine(Output,"rest-motion.txt"),restMotion);
        Require(relativeHeight<.72f&&sideAngle>75f&&sideAngle<88f&&excursion>.0006f&&excursion<.006f&&restPenetration<.004f&&restContactGap<.007f&&settledFrames>=20,
            "Rest body/feet/breath outside bounds: "+restMotion.Last());
        return true;
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false))return;
        if(!EditorApplication.isPlaying)
        {
            int code=SessionState.GetInt(Key+".Exit",-1);if(code<0)return;SessionState.SetBool(Key,false);string previous=SessionState.GetString(Key+".Scene","");if(!string.IsNullOrEmpty(previous))EditorSceneManager.OpenScene(previous);EditorApplication.Exit(code);return;
        }
        try
        {
            if(stage==0)
            {
                started=EditorApplication.timeSinceStartup;camera=Camera.main;controller=UnityEngine.Object.FindFirstObjectByType<ParkController>();ui=UnityEngine.Object.FindFirstObjectByType<ParkSharedInterface>();
                Require(camera&&controller&&ui&&ui.controller==controller&&!controller.simulation,"Actual ParkPet wiring invalid");Application.targetFrameRate=60;Later(1.2f);return;
            }
            Require(EditorApplication.timeSinceStartup-started<65,"Pet walking UI timed out");
            ObserveMeal();
            if(cat&&(stage>=3&&stage<=5||stage==60)&&EditorApplication.timeSinceStartup>=nextContact)
            {
                nextContact=EditorApplication.timeSinceStartup+1.0/60;contactSamples++;
                maximumPlayPenetration=Mathf.Max(maximumPlayPenetration,catFloor-NaturalDogAssetBuilder.Geometry(cat.gameObject).min.y);FixedRoot(cat);
            }
            if(EditorApplication.timeSinceStartup<next)return;
            switch(stage)
            {
                case 1:Click("Virtual test");cat=controller.selected;Require(cat&&cat.species=="Cat","Virtual pet did not start with cat");Later(.8f);break;
                case 2:
                    Require(new[]{"Preview Cat","Preview Dog","Walk in place"}.All(n=>camera.GetComponentsInChildren<Button>(false).Any(b=>b.name==n)),"Pet controls hidden in virtual Pet");
                    root=cat.transform.position;rotation=cat.transform.rotation;catFloor=NaturalDogAssetBuilder.Geometry(cat.gameObject).min.y;Click("Walk in place");Require(cat.PetWalking,"Walk callback did not request animation");Later(.4f);break;
                case 3:
                    FixedRoot(cat);Require(cat.GetComponentInChildren<NaturalPetRig>().Animator.GetCurrentAnimatorStateInfo(0).IsName("Walk in place"),"Cat Animator never entered walk");
                    initial=cat.GetComponentsInChildren<SkinnedMeshRenderer>().Select(NaturalDogAssetBuilder.Vertices).ToArray();Capture("Cat-Walk");Later(.5f);break;
                case 4:
                    FixedRoot(cat);var points=cat.GetComponentsInChildren<SkinnedMeshRenderer>().Select(NaturalDogAssetBuilder.Vertices).ToArray();float motion=0;for(int n=0;n<points.Length;n++)for(int i=0;i<points[n].Length;i++)motion=Mathf.Max(motion,Vector3.Distance(points[n][i],initial[n][i]));
                    Require(motion>.002f,"Actual Cat walk did not deform skin");Capture("Cat-Mid");Click("Walk in place");Later(.4f);break;
                case 5:
                    FixedRoot(cat);Require(!cat.PetWalking&&cat.GetComponentInChildren<NaturalPetRig>().Animator.GetCurrentAnimatorStateInfo(0).IsName("Restrained neutral idle"),"Cat walk did not stop to idle");Capture("Cat-Idle");
                    Click("Walk in place");cat.SetTracked(false);Require(!cat.PetWalking&&!cat.TryPreviewPetWalk(true),"Lost pet retained/accepted walking");cat.SetTracked(true);Require(!cat.PetWalking,"Tracking recovery restarted walk without input");
                    cat.virtualTarget=false;Require(!cat.TryPreviewPetWalk(true),"Physical target accepted virtual walking");cat.virtualTarget=true;
                    Require(controller.Dispatch(ParkAction.Pull,cat.transform.position,true),"Cat dream could not open after walking");Later(1.1f);break;
                case 6:
                    Click("Walk in place");Require(cat.PetWalking,"Dream cat could not preview walk");stage=59;Later(.4f);break;
                case 60:
                    Require(cat.GetComponentInChildren<NaturalPetRig>().Animator.GetCurrentAnimatorStateInfo(0).IsName("Walk in place"),"Feed interruption was not tested from active walk");
                    StartMealCheck(cat);
                    Click("Preview Dog");Require(controller.selected==cat&&ui.Feedback.Contains("Wait"),"Species switched during feed coroutine");stage=6;Later(.35f);break;
                case 7:
                    if(!FinishMealCheck())return;
                    StartRestProbe(cat);stage=79;Later(.25f);break;
                case 80:
                    if(!RestEvidenceReady())return;
                    Click("Sleep");Require(!cat.PetRestRequested&&restingRig.RestAmount>.99f,"Wake skipped the visual return");Later(.15f);break;
                case 81:
                    Require(EditorApplication.timeSinceStartup-restStart<11,"Wake did not restore standing");
                    if(restingRig.RestAmount>0)return;
                    Require(Mathf.Abs(restingBody.position.y-restHeight)<.003f&&restingRig.Animator.speed>0,"Wake did not restore the cat's neutral height");
                    CapturePetDetail(cat,restingRig,"Cat-Wake");
                    stage=7;
                    Click("Preview Dog");dog=controller.selected;Require(dog&&dog!=cat&&dog.species=="Dog"&&controller.InstanceCount==1,"Dog selection did not replace virtual cat");Later(.8f);break;
                case 8:
                    Require(!cat&&dog.EntryProgress>.99f,"Species switch leaked old pet");root=dog.transform.position;rotation=dog.transform.rotation;Click("Walk in place");Later(.4f);break;
                case 9:
                    FixedRoot(dog);Require(dog.PetWalking&&dog.GetComponentInChildren<NaturalPetRig>().Animator.GetCurrentAnimatorStateInfo(0).IsName("Walk in place"),"Dog UI did not play source walk");Capture("Dog-Walk");
                    dog.Undo();Require(!dog.PetWalking,"Undo retained walking");
                    Require(controller.Dispatch(ParkAction.Pull,dog.transform.position,true),"Dog dream could not open");stage=69;Later(1.1f);break;
                case 70:StartMealCheck(dog);Later(.35f);break;
                case 71:
                    if(!FinishMealCheck())return;
                    dog.Undo();Require(dog.Affection==0,"Undo retained feeding affection");Later(.4f);break;
                case 72:
                    StartMealCheck(dog);mealActor=null;mealRig=null;Later(.9f);break;
                case 73:
                    Require(dog.PetFeeding,"Dog tracking-loss test missed an active meal");dog.SetTracked(false);
                    Require(!dog.PetFeeding&&!dog.GetComponentsInChildren<Transform>(false).Any(t=>t.name=="Virtual meal dish"),"Lost tracking retained the virtual meal");
                    dog.SetTracked(true);Require(!dog.PetFeeding,"Tracking recovery restarted an interrupted meal");
                    StartRestProbe(dog);stage=89;Later(.25f);break;
                case 90:
                    if(!RestEvidenceReady())return;
                    Click("Stop");Require(!dog.PetRestRequested&&restingRig.RestAmount==0,"Stop retained the sleep pose");Later(.8f);break;
                case 91:
                    Click("Sleep");Require(dog.PetRestRequested,"Virtual dog could not rest again");Later(.6f);break;
                case 92:
                    Require(restingRig.RestAmount>.1f,"Sleep tracking-loss test missed the lowering phase");
                    dog.SetTracked(false);Require(restingRig.RestAmount==0&&!dog.PetRestRequested&&!dog.TryPreviewPetRest(true),"Loss retained or restarted a sleep pose");
                    dog.SetTracked(true);Require(!dog.PetRestRequested,"Sleep resumed without a new request");
                    Click("Walk in place");Click("Stop");Require(!dog.PetWalking,"Stop retained walking");stage=9;Later(.8f);break;
                case 10:
                    Require(!dog.Busy,"Pet Stop never finished");Click("Walk in place");Require(dog.PetWalking,"Dog walk could not restart");Click("Reset");Require(!dog.PetWalking&&dog.IsExiting&&!controller.selected,"Reset retained walk or selected actor");Later(.8f);break;
                case 11:
                    Require(!dog&&controller.InstanceCount==0,"Walk Reset leaked actor");Click("Virtual test");Later(.2f);break;
                case 12:
                    Require(!controller.simulation&&!camera.GetComponentsInChildren<Button>(false).Any(b=>b.name=="Preview Cat"||b.name=="Preview Dog"||b.name=="Walk in place"||b.name=="Sleep"),"Pet preview controls remained active in live mode");
                    ui.Command("Preview Dog");ui.Command("Walk in place");ui.Command("Sleep");Require(controller.InstanceCount==0,"Live mode accepted virtual pet command");
                    Require(UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Length==0&&errors==0,"Walking UI leaked audio or logged runtime errors: "+errors);
                    Require(contactSamples>=20&&maximumPlayPenetration<.001f,"Actual Animator walk/idle transition ground penetration: "+maximumPlayPenetration+" samples="+contactSamples);
                    File.WriteAllLines(Path.Combine(Output,"meal-motion.txt"),meals);
                    File.WriteAllText(Path.Combine(Output,"result.txt"),"PASS: actual saved ParkPet, original camera/input; Cat and Dog walk, eat and rest; lowered rig, breathing and wake; fixed tracking root; idle/Stop/Undo/Reset; tracking loss and physical/live gates; exit/audio cleanup; "+frames+" scene captures. No physical input or headset acceptance.\n");
                    Debug.Log("NATURAL_PET_WALK_UI_PASS: actual scene/buttons, both walk controllers, feed/idle/Undo/Stop/Reset/loss/exit/live gates; "+frames+" captures; actual Animator contact samples="+contactSamples+" max_ground_penetration_m="+maximumPlayPenetration);Finish(0);break;
            }
        }
        catch(Exception error){Debug.LogException(error);File.WriteAllText(Path.Combine(Output,"result.txt"),"FAIL: "+error);Finish(1);}
    }
    static void Finish(int code){SessionState.SetInt(Key+".Exit",code);EditorApplication.ExitPlaymode();}
    static void CapturePetDetail(ParkActor actor,NaturalPetRig rig,string name,Transform supplement=null)
    {
        var bounds=NaturalDogAssetBuilder.Geometry(actor.gameObject);
        if(supplement)bounds.Encapsulate(supplement.position);
        var direction=rig.transform.TransformDirection(new Vector3(.85f,.28f,-1)).normalized;
        var position=camera.transform.position;var rotation=camera.transform.rotation;
        bool orthographic=camera.orthographic;float size=camera.orthographicSize;
        var visibleCanvases=camera.GetComponentsInChildren<Canvas>(false).Where(c=>c.enabled).ToArray();
        try
        {
            foreach(var canvas in visibleCanvases)canvas.enabled=false;
            camera.orthographic=true;
            camera.orthographicSize=Mathf.Max(bounds.size.y*.7f,Mathf.Max(bounds.size.x,bounds.size.z)*.53f);
            camera.transform.position=bounds.center+direction*Mathf.Max(1,bounds.extents.magnitude*4);
            camera.transform.rotation=Quaternion.LookRotation(bounds.center-camera.transform.position);
            Capture(name);
        }
        finally
        {
            camera.transform.SetPositionAndRotation(position,rotation);
            camera.orthographic=orthographic;camera.orthographicSize=size;
            foreach(var canvas in visibleCanvases)canvas.enabled=true;
        }
    }
    static void Capture(string name)
    {
        var target=new RenderTexture(1000,800,24);var pixels=new Texture2D(1000,800,TextureFormat.RGB24,false);var old=RenderTexture.active;
        try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,1000,800),0,0);pixels.Apply();File.WriteAllBytes(Path.Combine(Output,name+".png"),pixels.EncodeToPNG());frames++;}
        finally{camera.targetTexture=null;RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(pixels);}
    }
}
