using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using MagicMR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;

[InitializeOnLoad]
public static class NaturalDogGaitReview
{
    const string Key="MagicMR.DogGaitReview";
    static readonly string Output=Path.GetFullPath(".codex-tmp/dog-gait-review");
    static readonly string[] Feet={"FrontToe_L","FrontToe_R","BackToe_L","BackToe_R"};
    static ParkController controller;static ParkActor dog;static NaturalPetRig rig;static Camera original,review;
    static SkinnedMeshRenderer skin;static int[][] groups;static int stage,frames,rows,unsupported;static double started,next;
    static float floor,height,tolerance,phaseStart,phaseLast,minGap=float.PositiveInfinity,maxGap=float.NegativeInfinity,maxPenetration;
    static Vector3 rootPosition,rootScale;static Quaternion rootRotation;static Vector3[] lastFeet;static float[] footTravel=new float[4];
    static int[] supportCounts=new int[4];static GameObject ground;static readonly List<GameObject> markers=new List<GameObject>();
    static Material groundMaterial;static readonly List<Material> markerMaterials=new List<Material>();
    static bool originalMatrixRecalculation,originalOffscreen;static float maximumBakeError;static Mesh baked;
    static RenderTexture reviewTarget;static string pendingCapture;static bool idleCaptureRequested;
    static bool requireAuthored,requireInstalled;static AnimatorOverrideController candidateOverride;static int minimumSupports=4;
    static Quaternion[] previousRotations;static float previousRotationTime,maximumBoneSpeed;
    static readonly Dictionary<GameObject,int> layers=new Dictionary<GameObject,int>();
    static readonly List<string> trajectory=new List<string>();
    [Serializable] sealed class Summary
    {
        public int rows,unsupportedRows,frames;public float measuredCycles,floorY,standingHeight,contactTolerance,minimumMeshGap,maximumMeshGap,maximumPenetration,maximumBakedMeshWorldError;
        public string[] footNames;public int[] footVertexCounts,supportRows;public float[] footPathLength;
        public string geometryMethod="Unity BakeMesh(mesh,true), then renderer.TransformPoint; manual four-weight LBS discrepancy recorded separately, not used as actual support geometry.";
        public string rendererSkinQuality,projectSkinWeights;
        public bool authoredOverrideUsed;public int minimumSimultaneousSupport;
        public bool savedAuthoredControllerUsed;public float maximumConsecutiveBoneDegreesPerSecond;
        public string scope="Actual saved ParkPet virtual Dog; original Walk button callback; two source animation cycles and world-space linear-blend-skinned paw groups. Fixed tracking root. Contact is geometry proximity, not force sensing or calibrated physical contact. Independent side camera and temporary floor/markers for review only. No physical input, YOLO, PICO or naturalness acceptance.";
    }
    static NaturalDogGaitReview(){EditorApplication.update+=Tick;RenderPipelineManager.endCameraRendering+=CameraFinished;}
    static void Require(bool value,string message){if(!value)throw new Exception(message);}
    static string F(float x)=>x.ToString("R",CultureInfo.InvariantCulture);
    public static void Run()=>Begin(false,false);
    public static void RunAuthored()=>Begin(true,false);
    public static void RunInstalled()=>Begin(true,true);
    static void Begin(bool authored,bool installed)
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode,"Idle Editor required");
        SessionState.SetString(Key+".Scene",UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);
        SessionState.SetBool(Key+".Authored",authored);
        SessionState.SetBool(Key+".Installed",installed);
        SessionState.SetInt(Key+".Exit",-1);SessionState.SetBool(Key,true);
        Directory.CreateDirectory(Output);EditorSceneManager.OpenScene("Assets/Scenes/ParkPet.unity");EditorApplication.EnterPlaymode();
    }
    static void Click(string name)
    {
        var button=original.GetComponentsInChildren<Button>(false).Single(b=>b.name==name);
        var rect=button.GetComponent<RectTransform>();var center=rect.TransformPoint(rect.rect.center);
        Require(ParkSharedInterface.TryButtonRayHit(rect,new Ray(original.transform.position,center-original.transform.position),out _),"Unreachable original button "+name);
        button.onClick.Invoke();
    }
    static void Later(double seconds){stage++;next=EditorApplication.timeSinceStartup+seconds;}
    static void FixedRoot()
    {
        Require(dog.transform.position==rootPosition&&dog.transform.rotation==rootRotation&&dog.transform.localScale==rootScale,"Gait moved recognition anchor or fit");
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false))return;
        if(!EditorApplication.isPlaying){int code=SessionState.GetInt(Key+".Exit",-1);if(code<0)return;SessionState.SetBool(Key,false);var previous=SessionState.GetString(Key+".Scene","");if(!string.IsNullOrEmpty(previous))EditorSceneManager.OpenScene(previous);EditorApplication.Exit(code);return;}
        try
        {
            if(stage==0){requireAuthored=SessionState.GetBool(Key+".Authored",false);requireInstalled=SessionState.GetBool(Key+".Installed",false);started=EditorApplication.timeSinceStartup;original=Camera.main;controller=UnityEngine.Object.FindFirstObjectByType<ParkController>();Require(original&&controller&&!controller.simulation,"Original ParkPet wiring invalid");Application.targetFrameRate=60;Later(1.2);return;}
            Require(EditorApplication.timeSinceStartup-started<40,"Dog gait review timed out");
            if(EditorApplication.timeSinceStartup<next)return;
            switch(stage)
            {
                case 1:Click("Virtual test");Later(.5);break;
                case 2:Click("Preview Dog");Later(1);break;
                case 3:
                    dog=controller.selected;Require(dog&&dog.species=="Dog"&&dog.EntryProgress>.99f,"Dog entry did not settle");
                    rig=dog.GetComponentInChildren<NaturalPetRig>();Require(rig,"Dog rig missing");skin=NaturalPetModelChecks.SourceSkins(rig).Single();
                    if(requireAuthored){var authored=AssetDatabase.LoadAssetAtPath<AnimationClip>(NaturalDogWalkAuthor.ClipPath);Require(authored,"Authored candidate missing");if(requireInstalled)Require(rig.Animator.runtimeAnimatorController.animationClips.Contains(authored),"Saved dog controller does not reference authored walk");else{var source=rig.Animator.runtimeAnimatorController.animationClips.Single(c=>c.name.EndsWith("Wolfhound_WalkSource",StringComparison.Ordinal));candidateOverride=new AnimatorOverrideController(rig.Animator.runtimeAnimatorController);candidateOverride[source.name]=authored;rig.Animator.runtimeAnimatorController=candidateOverride;rig.Animator.Play("Base Layer.Restrained neutral idle",0,0);rig.Animator.Update(0);}}
                    rootPosition=dog.transform.position;rootRotation=dog.transform.rotation;rootScale=dog.transform.localScale;
                    baked=new Mesh();var points=CurrentVertices();floor=points.Min(p=>p.y);height=points.Max(p=>p.y)-floor;Require(height>.1f&&height<1,"Implausible dog scale; inspect baked conversion");tolerance=Mathf.Max(.002f,height*.02f);
                    groups=Feet.Select(name=>FootVertices(name)).ToArray();lastFeet=FootPoints(points);
                    SetupReview(points);Capture("00-idle");Later(0);break;
                case 4:
                    if(pendingCapture!=null)return;Click("Walk in place");Later(.4);break;
                case 5:
                    Require(rig.IsWalking&&rig.Animator.GetCurrentAnimatorStateInfo(0).IsName("Walk in place"),"Original walk did not start");
                    if(rig.Animator.IsInTransition(0)){next=EditorApplication.timeSinceStartup+.05;return;}
                    phaseStart=rig.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime;phaseLast=phaseStart;
                    trajectory.Add("time,normalized,meshGap,frontLeft,frontRight,backLeft,backRight,supportCount,rootX,rootY,rootZ");Later(0);break;
                case 6:
                    FixedRoot();Require(rig.IsWalking&&!rig.Animator.IsInTransition(0),"Walk interrupted during sampling");
                    float phase=rig.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
                    if(phase-phaseStart>=2&&pendingCapture==null){Require(rows>=80,"Insufficient live Animator samples");Click("Walk in place");Later(.4);}break;
                case 7:
                    FixedRoot();Require(!rig.IsWalking&&rig.Animator.GetCurrentAnimatorStateInfo(0).IsName("Restrained neutral idle"),"Walk did not return to idle");
                    if(!idleCaptureRequested){Capture("07-return-idle");idleCaptureRequested=true;return;}if(pendingCapture!=null)return;
                    Require(footTravel.All(x=>x>.001f),"A foot group never moved");
                    if(requireAuthored){Require(unsupported==0&&minimumSupports>=2,"Authored walk lost ground support");Require(maxGap<tolerance&&maxPenetration<.002f,"Authored walk ground gap/penetration failed");Require(supportCounts.All(x=>x>rows*.5f),"A paw failed to spend most of the cycle in support");Require(maximumBoneSpeed<900,"Abrupt bone rotation in slow authored walk: "+maximumBoneSpeed);}
                    var summary=new Summary{rows=rows,unsupportedRows=unsupported,frames=frames,measuredCycles=phaseLast-phaseStart,floorY=floor,standingHeight=height,contactTolerance=tolerance,minimumMeshGap=minGap,maximumMeshGap=maxGap,maximumPenetration=maxPenetration,maximumBakedMeshWorldError=maximumBakeError,footNames=Feet,footVertexCounts=groups.Select(g=>g.Length).ToArray(),supportRows=supportCounts,footPathLength=footTravel,rendererSkinQuality=skin.quality.ToString(),projectSkinWeights=QualitySettings.skinWeights.ToString(),authoredOverrideUsed=requireAuthored&&!requireInstalled,minimumSimultaneousSupport=minimumSupports,savedAuthoredControllerUsed=requireInstalled,maximumConsecutiveBoneDegreesPerSecond=maximumBoneSpeed};
                    File.WriteAllLines(Path.Combine(Output,"trajectory.csv"),trajectory);File.WriteAllText(Path.Combine(Output,"summary.json"),JsonUtility.ToJson(summary,true));
                    File.WriteAllText(Path.Combine(Output,"result.txt"),(requireAuthored?"PASS: actual saved ParkPet authored four-beat walk;":"PASS: actual saved ParkPet dog gait measured;")+" original virtual Dog/Walk callbacks, two full live Animator cycles, four skinned paw groups, fixed recognition root and return idle. "+(requireInstalled?"Saved authored controller, no runtime clip override.":"Runtime clip override only in authored candidate mode.")+" No visual or physical-contact acceptance from assertions alone.\n");Finish(0);break;
            }
        }
        catch(Exception error){Debug.LogException(error);File.WriteAllText(Path.Combine(Output,"result.txt"),"FAIL: "+error);Finish(1);}
    }
    static int[] FootVertices(string toe)
    {
        string leg=toe.Replace("Toe","Leg3");var bones=skin.bones;
        var indices=new HashSet<int>(Enumerable.Range(0,bones.Length).Where(i=>bones[i].name==toe||bones[i].name==leg));
        Require(indices.Count==2,"Paw chain not found: "+toe);
        var weights=skin.sharedMesh.boneWeights;var selected=new List<int>();
        for(int i=0;i<weights.Length;i++){var w=weights[i];float sum=(indices.Contains(w.boneIndex0)?w.weight0:0)+(indices.Contains(w.boneIndex1)?w.weight1:0)+(indices.Contains(w.boneIndex2)?w.weight2:0)+(indices.Contains(w.boneIndex3)?w.weight3:0);if(sum>=.5f)selected.Add(i);}
        Require(selected.Count>=4,"Too few paw vertices: "+toe);return selected.ToArray();
    }
    static Vector3[] FootPoints(Vector3[] points)=>groups.Select(g=>g.Select(i=>points[i]).OrderBy(p=>p.y).First()).ToArray();
    static Vector3[] CurrentVertices()
    {
        skin.BakeMesh(baked,true);var actual=baked.vertices;var manual=NaturalDogAssetBuilder.Vertices(skin);
        Require(actual.Length==manual.Length,"Baked geometry topology changed");
        for(int i=0;i<actual.Length;i++){actual[i]=skin.transform.TransformPoint(actual[i]);Require(float.IsFinite(actual[i].x)&&float.IsFinite(actual[i].y)&&float.IsFinite(actual[i].z),"Nonfinite baked skin");maximumBakeError=Mathf.Max(maximumBakeError,Vector3.Distance(actual[i],manual[i]));}
        return actual;
    }
    static void Measure(float phase)
    {
        var rotations=skin.bones.Select(b=>b.localRotation).ToArray();float now=Time.unscaledTime;
        if(previousRotations!=null&&now>previousRotationTime)for(int i=0;i<rotations.Length;i++)maximumBoneSpeed=Mathf.Max(maximumBoneSpeed,Quaternion.Angle(previousRotations[i],rotations[i])/(now-previousRotationTime));previousRotations=rotations;previousRotationTime=now;
        var points=CurrentVertices();var feet=FootPoints(points);float meshGap=points.Min(p=>p.y)-floor;
        minGap=Mathf.Min(minGap,meshGap);maxGap=Mathf.Max(maxGap,meshGap);maxPenetration=Mathf.Max(maxPenetration,-meshGap);
        int support=0;for(int i=0;i<4;i++){if(feet[i].y-floor<=tolerance){support++;supportCounts[i]++;}footTravel[i]+=Vector3.Distance(lastFeet[i],feet[i]);markers[i].transform.position=feet[i];}lastFeet=feet;
        if(support==0)unsupported++;minimumSupports=Math.Min(minimumSupports,support);rows++;
        trajectory.Add(string.Join(",",new[]{Time.unscaledTime,phase,meshGap,feet[0].y-floor,feet[1].y-floor,feet[2].y-floor,feet[3].y-floor,(float)support,rootPosition.x,rootPosition.y,rootPosition.z}.Select(F)));
    }
    static Material Flat(Color color){var material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));material.SetColor("_BaseColor",color);return material;}
    static void SetupReview(Vector3[] points)
    {
        originalMatrixRecalculation=skin.forceMatrixRecalculationPerRender;originalOffscreen=skin.updateWhenOffscreen;
        skin.forceMatrixRecalculationPerRender=true;skin.updateWhenOffscreen=true;
        var bounds=new Bounds(points[0],Vector3.zero);foreach(var p in points)bounds.Encapsulate(p);
        foreach(var t in dog.GetComponentsInChildren<Transform>(true)){layers[t.gameObject]=t.gameObject.layer;t.gameObject.layer=29;}
        ground=GameObject.CreatePrimitive(PrimitiveType.Plane);ground.name="Temporary gait reference floor";ground.layer=29;ground.transform.position=new Vector3(bounds.center.x,floor-.0002f,bounds.center.z);ground.transform.localScale=Vector3.one*height*.7f;groundMaterial=Flat(new Color(.19f,.22f,.24f));ground.GetComponent<Renderer>().sharedMaterial=groundMaterial;
        for(int i=0;i<4;i++){var marker=GameObject.CreatePrimitive(PrimitiveType.Sphere);marker.name="Measured paw "+Feet[i];marker.layer=29;marker.transform.position=lastFeet[i];marker.transform.localScale=Vector3.one*height*.035f;var material=Flat(new[]{Color.red,Color.yellow,Color.cyan,Color.magenta}[i]);marker.GetComponent<Renderer>().sharedMaterial=material;markerMaterials.Add(material);markers.Add(marker);}
        review=new GameObject("Independent dog gait side camera").AddComponent<Camera>();review.orthographic=true;review.orthographicSize=Mathf.Max(bounds.size.y*.85f,bounds.size.z*.48f);review.clearFlags=CameraClearFlags.SolidColor;review.backgroundColor=new Color(.09f,.11f,.14f);review.cullingMask=1<<29;review.nearClipPlane=.01f;review.farClipPlane=10;review.transform.position=bounds.center+Vector3.right*1.5f+Vector3.up*height*.05f;review.transform.LookAt(bounds.center);
        reviewTarget=new RenderTexture(960,640,24,RenderTextureFormat.ARGB32);reviewTarget.Create();review.targetTexture=reviewTarget;review.gameObject.AddComponent<NaturalDogGaitLateProbe>();
    }
    public static void LateSample()
    {
        if(!SessionState.GetBool(Key,false)||!review||!skin)return;
        if(stage==6){var phase=rig.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime;if(phase!=phaseLast){phaseLast=phase;Measure(phase);if(frames<7&&pendingCapture==null&&phase-phaseStart>=(frames-1)/3f)Capture("walk-"+(frames-1).ToString("00"));}}
        else{var feet=FootPoints(CurrentVertices());for(int i=0;i<4;i++)markers[i].transform.position=feet[i];}
    }
    static void Capture(string name)
    {
        Require(pendingCapture==null,"Previous normal-frame capture still pending");pendingCapture=name;
    }
    static void CameraFinished(ScriptableRenderContext context,Camera camera)
    {
        if(camera!=review||pendingCapture==null)return;var previous=RenderTexture.active;var texture=new Texture2D(960,640,TextureFormat.RGB24,false);
        try{RenderTexture.active=reviewTarget;texture.ReadPixels(new Rect(0,0,960,640),0,0);texture.Apply();File.WriteAllBytes(Path.Combine(Output,pendingCapture+".png"),texture.EncodeToPNG());frames++;pendingCapture=null;}
        finally{RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(texture);}
    }
    static void Finish(int code)
    {
        if(skin){skin.forceMatrixRecalculationPerRender=originalMatrixRecalculation;skin.updateWhenOffscreen=originalOffscreen;}
        if(baked)UnityEngine.Object.DestroyImmediate(baked);
        if(candidateOverride)UnityEngine.Object.Destroy(candidateOverride);
        if(review)review.targetTexture=null;if(reviewTarget){reviewTarget.Release();UnityEngine.Object.DestroyImmediate(reviewTarget);}
        foreach(var row in layers)if(row.Key)row.Key.layer=row.Value;
        foreach(var marker in markers)if(marker)UnityEngine.Object.Destroy(marker);foreach(var material in markerMaterials)if(material)UnityEngine.Object.Destroy(material);
        if(ground)UnityEngine.Object.Destroy(ground);if(groundMaterial)UnityEngine.Object.Destroy(groundMaterial);if(review)UnityEngine.Object.Destroy(review.gameObject);
        SessionState.SetInt(Key+".Exit",code);EditorApplication.ExitPlaymode();
    }
}

public sealed class NaturalDogGaitLateProbe : MonoBehaviour
{
    void LateUpdate()=>NaturalDogGaitReview.LateSample();
}
