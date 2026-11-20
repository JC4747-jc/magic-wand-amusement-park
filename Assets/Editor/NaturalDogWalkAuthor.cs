using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class NaturalDogWalkAuthor
{
    public const string ClipPath="Assets/ThirdParty/NaturalPets/Wolfhound/WolfhoundFourBeatWalk.anim";
    const string SourcePath="Assets/ThirdParty/NaturalPets/Wolfhound/Wolfhound.fbx";
    const string Output=".codex-tmp/dog-walk-authoring";
    const int Samples=49;
    const float Duration=1.6f;
    static readonly string[] Toes={"FrontToe_L","FrontToe_R","BackToe_L","BackToe_R"};
    static readonly float[] Phases={.5f,0,.25f,.75f};
    [Serializable] sealed class Report
    {
        public string sourcePath,sourceSha256,clipPath,clipSha256,skinQuality,projectSkinWeights;
        public string solver="Analytic hip/knee IK with neutral-pose knee pole and fixed ankle/toe world orientation; no iterative CCD branch choices.";
        public int samples,bones,warmupCycles;public float duration,height,stride,clearance,bodyLowering,maximumStanceSoleError,maximumToeTargetError,maximumJointRotationFromIdle,maximumLoopClosureAngle;
        public string scope="Authored smooth four-beat in-place candidate on original licensed rig. No training, original FBX/source clips/controller untouched. Evaluated actual Unity two-bone bake at authoring samples; live interpolation, foot support, knee readability, transitions and production integration remain unaccepted.";
    }
    static void Require(bool value,string message){if(!value)throw new Exception(message);}
    static string Sha(string path){using(var f=File.OpenRead(path))using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(f)).Replace("-","").ToLowerInvariant();}
    static Vector3[] Bake(SkinnedMeshRenderer skin,Mesh mesh){skin.BakeMesh(mesh,true);return mesh.vertices.Select(skin.transform.TransformPoint).ToArray();}
    public static void Run()
    {
        Directory.CreateDirectory(Output);GameObject instance=null;Mesh baked=null;
        var scene=EditorSceneManager.NewPreviewScene();
        try
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode,"Idle authoring only");
            Require(Path.GetFullPath(Application.dataPath).Replace('\\','/').Equals("D:/MagicMRSourceCandidate/20261120/Assets",StringComparison.OrdinalIgnoreCase),"Author only in the isolated D candidate");
            var imported=AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath);Require(imported,"Original dog source missing");
            instance=UnityEngine.Object.Instantiate(imported);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(instance,scene);
            var animator=instance.GetComponentInChildren<Animator>();if(animator)animator.enabled=false;
            var idle=AssetDatabase.LoadAllAssetsAtPath(SourcePath).OfType<AnimationClip>().Single(c=>!c.name.StartsWith("__preview",StringComparison.Ordinal)&&c.name.EndsWith("Wolfhound_Idle",StringComparison.Ordinal));
            idle.SampleAnimation(instance,0);
            var skin=instance.GetComponentsInChildren<SkinnedMeshRenderer>().Single();baked=new Mesh();
            var bones=skin.bones.Distinct().ToArray();var restPosition=bones.Select(b=>b.localPosition).ToArray();var restRotation=bones.Select(b=>b.localRotation).ToArray();
            var points=Bake(skin,baked);float floor=points.Min(p=>p.y),height=points.Max(p=>p.y)-floor;
            Require(height>.3f&&height<2,"Unexpected source rig scale "+height);
            var body=bones.Single(b=>b.name=="wolfhound");var head=bones.Single(b=>b.name=="Head");
            var bounds=new Bounds(points[0],Vector3.zero);foreach(var p in points)bounds.Encapsulate(p);
            var forward=head.position-bounds.center;forward.y=0;forward.Normalize();Require(forward.sqrMagnitude>.9f,"Dog forward axis unresolved");
            var toes=Toes.Select(n=>bones.Single(b=>b.name==n)).ToArray();
            var toeRest=toes.Select(t=>t.position).ToArray();var toeRotation=toes.Select(t=>t.rotation).ToArray();
            var chains=Toes.Select(n=>new[]{n.Replace("Toe","Leg3"),n.Replace("Toe","Leg2"),n.Replace("Toe","Leg1")}.Select(s=>bones.Single(b=>b.name==s)).ToArray()).ToArray();
            Require(chains.All(c=>c[2].IsChildOf(body)),"Stance posture must include all four hips");
            var ankleRotation=chains.Select(c=>c[0].rotation).ToArray();
            var ankleOffset=Enumerable.Range(0,4).Select(i=>toeRest[i]-chains[i][0].position).ToArray();
            var poles=Enumerable.Range(0,4).Select(i=>{var c=chains[i];var direction=(c[0].position-c[2].position).normalized;var offset=c[1].position-c[2].position;var pole=offset-direction*Vector3.Dot(offset,direction);return pole.magnitude>height*.005f?pole.normalized:(i<2?-forward:forward);}).ToArray();
            var weights=skin.sharedMesh.boneWeights;
            var groups=Toes.Select(name=>{
                var indices=new HashSet<int>(Enumerable.Range(0,skin.bones.Length).Where(i=>skin.bones[i].name==name||skin.bones[i].name==name.Replace("Toe","Leg3")));
                return Enumerable.Range(0,weights.Length).Where(i=>{var w=weights[i];return (indices.Contains(w.boneIndex0)?w.weight0:0)+(indices.Contains(w.boneIndex1)?w.weight1:0)+(indices.Contains(w.boneIndex2)?w.weight2:0)+(indices.Contains(w.boneIndex3)?w.weight3:0)>=.5f;}).ToArray();
            }).ToArray();
            Require(groups.All(g=>g.Length>=4),"Unresolved skinned paw groups");
            var soleRest=groups.Select(g=>g.Min(i=>points[i].y)).ToArray();
            var rotations=new Quaternion[Samples][];var positions=new Vector3[Samples][];
            float stride=height*.14f,clearance=height*.065f,lower=height*.04f,maxSole=0,maxTarget=0,maxRotation=0;string worstFoot="";float worstPhase=0;float[] worstResiduals=Array.Empty<float>();
            const int Warmup=0;
            for(int frame=0;frame<=((Warmup+1)*(Samples-1));frame++)
            {
                for(int j=0;j<bones.Length;j++){bones[j].localPosition=restPosition[j];bones[j].localRotation=restRotation[j];}
                body.position+=Vector3.down*lower;
                int sample=frame-Warmup*(Samples-1);bool record=sample>=0;
                for(int foot=0;foot<4;foot++)
                {
                    float phase=Mathf.Repeat(frame/(float)(Samples-1)+Phases[foot],1),distance,lift;
                    Trajectory(phase,stride,clearance,out distance,out lift);
                    var target=toeRest[foot]-forward*distance+Vector3.up*(floor-soleRest[foot]+lift);
                    var residuals=new List<float>();
                    for(int correction=0;correction<20;correction++)
                    {
                        Solve(chains[foot],toes[foot],target,ankleOffset[foot],ankleRotation[foot],toeRotation[foot],poles[foot]);
                        points=Bake(skin,baked);float sole=groups[foot].Min(i=>points[i].y);
                        float error=floor+lift-sole;residuals.Add(error);if(Mathf.Abs(error)<height*.0005f)break;target.y+=error;
                    }
                    Solve(chains[foot],toes[foot],target,ankleOffset[foot],ankleRotation[foot],toeRotation[foot],poles[foot]);
                    points=Bake(skin,baked);float finalSole=groups[foot].Min(i=>points[i].y);
                    if(record&&phase<.75f&&Mathf.Abs(finalSole-floor)>maxSole){maxSole=Mathf.Abs(finalSole-floor);worstFoot=Toes[foot];worstPhase=phase;worstResiduals=residuals.ToArray();}
                    if(record)maxTarget=Mathf.Max(maxTarget,Vector3.Distance(toes[foot].position,target));
                }
                if(record){rotations[sample]=bones.Select(b=>b.localRotation).ToArray();positions[sample]=bones.Select(b=>b.localPosition).ToArray();for(int j=0;j<bones.Length;j++)maxRotation=Mathf.Max(maxRotation,Quaternion.Angle(restRotation[j],rotations[sample][j]));}
            }
            File.WriteAllText(Output+"/solver-diagnostics.json",JsonUtility.ToJson(new SolverDiagnostics{maximumSoleError=maxSole,worstFoot=worstFoot,phase=worstPhase,residuals=worstResiduals,maximumTargetError=maxTarget,maximumRotation=maxRotation},true));
            Require(maxSole<height*.002f,"Authored stance sole error "+maxSole);
            Require(maxTarget<height*.015f,"Unreachable authored paw target "+maxTarget);
            Require(maxRotation<110,"Potential leg flip relative to neutral "+maxRotation);
            float maxClosure=Enumerable.Range(0,bones.Length).Max(j=>Quaternion.Angle(rotations[0][j],rotations[Samples-1][j]));
            Require(maxClosure<2,"IK did not settle into a closed cycle: "+maxClosure);
            rotations[Samples-1]=(Quaternion[])rotations[0].Clone();positions[Samples-1]=(Vector3[])positions[0].Clone();
            var clip=new AnimationClip{name="WolfhoundFourBeatWalk",frameRate=30,wrapMode=WrapMode.Loop};
            for(int j=0;j<bones.Length;j++)
            {
                int bone=j;string path=AnimationUtility.CalculateTransformPath(bones[j],instance.transform);
                for(int frame=1;frame<Samples;frame++)if(Quaternion.Dot(rotations[frame-1][j],rotations[frame][j])<0){var q=rotations[frame][j];rotations[frame][j]=new Quaternion(-q.x,-q.y,-q.z,-q.w);}
                for(int component=0;component<4;component++){int axis=component;Curve(clip,path,"m_LocalRotation."+"xyzw"[axis],Enumerable.Range(0,Samples).Select(f=>rotations[f][bone][axis]).ToArray());}
                for(int component=0;component<3;component++){int axis=component;Curve(clip,path,"m_LocalPosition."+"xyz"[axis],Enumerable.Range(0,Samples).Select(f=>positions[f][bone][axis]).ToArray());}
            }
            clip.EnsureQuaternionContinuity();var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=true;settings.loopBlend=false;settings.keepOriginalPositionY=true;settings.keepOriginalPositionXZ=true;settings.keepOriginalOrientation=true;AnimationUtility.SetAnimationClipSettings(clip,settings);
            var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
            if(existing){EditorUtility.CopySerialized(clip,existing);EditorUtility.SetDirty(existing);AssetDatabase.SaveAssetIfDirty(existing);UnityEngine.Object.DestroyImmediate(clip);}
            else{AssetDatabase.CreateAsset(clip,ClipPath);AssetDatabase.SaveAssets();}
            var report=new Report{sourcePath=SourcePath,sourceSha256=Sha(SourcePath),clipPath=ClipPath,clipSha256=Sha(ClipPath),skinQuality=skin.quality.ToString(),projectSkinWeights=QualitySettings.skinWeights.ToString(),samples=Samples,bones=bones.Length,warmupCycles=Warmup,duration=Duration,height=height,stride=stride,clearance=clearance,bodyLowering=lower,maximumStanceSoleError=maxSole,maximumToeTargetError=maxTarget,maximumJointRotationFromIdle=maxRotation,maximumLoopClosureAngle=maxClosure};
            File.WriteAllText(Output+"/authoring.json",JsonUtility.ToJson(report,true));
            File.WriteAllText(Output+"/result.txt","PASS: authored dog four-beat candidate; original licensed FBX/controller unchanged; 49 actual baked authoring samples, smooth cyclic paw paths, stance sole/target/rotation guards. Live quality and integration not accepted.\n");
            EditorSceneManager.ClosePreviewScene(scene);if(baked)UnityEngine.Object.DestroyImmediate(baked);EditorApplication.Exit(0);
        }
        catch(Exception error){Debug.LogException(error);File.WriteAllText(Output+"/result.txt","FAIL: "+error);if(scene.IsValid())EditorSceneManager.ClosePreviewScene(scene);if(baked)UnityEngine.Object.DestroyImmediate(baked);EditorApplication.Exit(1);}
    }
    [Serializable] sealed class SolverDiagnostics{public float maximumSoleError,phase,maximumTargetError,maximumRotation;public string worstFoot;public float[] residuals;}
    static void Solve(Transform[] chain,Transform toe,Vector3 target,Vector3 ankleOffset,Quaternion ankleRotation,Quaternion toeRotation,Vector3 pole)
    {
        var hip=chain[2];var knee=chain[1];var ankle=chain[0];var origin=hip.position;var destination=target-ankleOffset;
        float upper=Vector3.Distance(origin,knee.position),lower=Vector3.Distance(knee.position,ankle.position);
        var direction=destination-origin;float length=Mathf.Clamp(direction.magnitude,Mathf.Abs(upper-lower)+.000001f,upper+lower-.000001f);direction.Normalize();
        var bend=pole-direction*Vector3.Dot(pole,direction);Require(bend.sqrMagnitude>.0001f,"Knee pole degenerate");bend.Normalize();
        float along=(upper*upper-lower*lower+length*length)/(2*length);float sideways=Mathf.Sqrt(Mathf.Max(0,upper*upper-along*along));
        var kneeTarget=origin+direction*along+bend*sideways;
        hip.rotation=Quaternion.FromToRotation(knee.position-origin,kneeTarget-origin)*hip.rotation;
        knee.rotation=Quaternion.FromToRotation(ankle.position-knee.position,destination-knee.position)*knee.rotation;
        ankle.rotation=ankleRotation;toe.rotation=toeRotation;
    }
    static void Trajectory(float phase,float stride,float clearance,out float distance,out float lift)
    {
        if(phase<.75f){distance=(phase/.75f-.5f)*stride;lift=0;return;}
        float s=(phase-.75f)/.25f,slope=.25f/.75f;
        distance=stride*(.5f+slope*s-(1+slope)*(10*s*s*s-15*s*s*s*s+6*s*s*s*s*s));lift=clearance*Mathf.Pow(Mathf.Sin(Mathf.PI*s),3);
    }
    static void Curve(AnimationClip clip,string path,string property,float[] values)
    {
        bool constant=values.All(v=>Mathf.Abs(v-values[0])<1e-7f);
        var keys=constant?new[]{new Keyframe(0,values[0])}:Enumerable.Range(0,Samples).Select(f=>new Keyframe(f*Duration/(Samples-1),values[f])).ToArray();
        var curve=new AnimationCurve(keys);
        for(int i=0;i<curve.length;i++){AnimationUtility.SetKeyLeftTangentMode(curve,i,AnimationUtility.TangentMode.ClampedAuto);AnimationUtility.SetKeyRightTangentMode(curve,i,AnimationUtility.TangentMode.ClampedAuto);}
        AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(path,typeof(Transform),property),curve);
    }
}
