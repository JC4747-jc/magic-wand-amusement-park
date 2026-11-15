using System;
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
                    Require(controller.Dispatch(ParkAction.Feed,cat.transform.position,true)&&!cat.PetWalking,"Feeding did not interrupt walk");
                    Click("Preview Dog");Require(controller.selected==cat&&ui.Feedback.Contains("Wait"),"Species switched during feed coroutine");stage=6;Later(1.1f);break;
                case 7:
                    Require(cat.GetComponentInChildren<NaturalPetRig>().Animator.GetCurrentAnimatorStateInfo(0).IsName("Brief sniff"),"Walk/Feed did not reach sniff");Capture("Cat-Feed");
                    Click("Preview Dog");dog=controller.selected;Require(dog&&dog!=cat&&dog.species=="Dog"&&controller.InstanceCount==1,"Dog selection did not replace virtual cat");Later(.8f);break;
                case 8:
                    Require(!cat&&dog.EntryProgress>.99f,"Species switch leaked old pet");root=dog.transform.position;rotation=dog.transform.rotation;Click("Walk in place");Later(.4f);break;
                case 9:
                    FixedRoot(dog);Require(dog.PetWalking&&dog.GetComponentInChildren<NaturalPetRig>().Animator.GetCurrentAnimatorStateInfo(0).IsName("Walk in place"),"Dog UI did not play source walk");Capture("Dog-Walk");
                    dog.Undo();Require(!dog.PetWalking,"Undo retained walking");Click("Walk in place");Click("Stop");Require(!dog.PetWalking,"Stop retained walking");Later(.8f);break;
                case 10:
                    Require(!dog.Busy,"Pet Stop never finished");Click("Walk in place");Require(dog.PetWalking,"Dog walk could not restart");Click("Reset");Require(!dog.PetWalking&&dog.IsExiting&&!controller.selected,"Reset retained walk or selected actor");Later(.8f);break;
                case 11:
                    Require(!dog&&controller.InstanceCount==0,"Walk Reset leaked actor");Click("Virtual test");Later(.2f);break;
                case 12:
                    Require(!controller.simulation&&!camera.GetComponentsInChildren<Button>(false).Any(b=>b.name=="Preview Cat"||b.name=="Preview Dog"||b.name=="Walk in place"),"Pet preview controls remained active in live mode");
                    ui.Command("Preview Dog");ui.Command("Walk in place");Require(controller.InstanceCount==0,"Live mode accepted virtual pet command");
                    Require(UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Length==0&&errors==0,"Walking UI leaked audio or logged runtime errors: "+errors);
                    Require(contactSamples>=20&&maximumPlayPenetration<.001f,"Actual Animator walk/idle transition ground penetration: "+maximumPlayPenetration+" samples="+contactSamples);
                    File.WriteAllText(Path.Combine(Output,"result.txt"),"PASS: actual saved ParkPet, original camera/input; real Cat/Dog/Walk button callbacks and ray geometry; actual skinned walk; fixed tracking root; idle/Stop/Undo/Reset; tracking loss and physical/live gates; Feed interrupts walking and species busy gate; exit/audio cleanup; "+frames+" scene captures. No real pointer/hand/native/headset acceptance.\n");
                    Debug.Log("NATURAL_PET_WALK_UI_PASS: actual scene/buttons, both walk controllers, feed/idle/Undo/Stop/Reset/loss/exit/live gates; "+frames+" captures; actual Animator contact samples="+contactSamples+" max_ground_penetration_m="+maximumPlayPenetration);Finish(0);break;
            }
        }
        catch(Exception error){Debug.LogException(error);File.WriteAllText(Path.Combine(Output,"result.txt"),"FAIL: "+error);Finish(1);}
    }
    static void Finish(int code){SessionState.SetInt(Key+".Exit",code);EditorApplication.ExitPlaymode();}
    static void Capture(string name)
    {
        var target=new RenderTexture(1000,800,24);var pixels=new Texture2D(1000,800,TextureFormat.RGB24,false);var old=RenderTexture.active;
        try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,1000,800),0,0);pixels.Apply();File.WriteAllBytes(Path.Combine(Output,name+".png"),pixels.EncodeToPNG());frames++;}
        finally{camera.targetTexture=null;RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(pixels);}
    }
}
