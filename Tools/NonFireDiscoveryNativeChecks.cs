using System;
using System.IO;
using MagicMR;
using Perception;
using UnityEditor;
using UnityEngine;

// Copied into the owned isolated project's Editor folder only for this check.
// It does not edit or save any scene, asset, model profile or weight.
public static class NonFireDiscoveryNativeChecks {
    [Serializable] public class Result {
        public string status,scope,error;
        public int frames,positiveFrames;
        public float positiveSpanSeconds;
        public bool unityJsonUtilityPassed,unvalidatedAutomaticSelectionRejected;
        public bool sharedInterfaceRegressionPassed,productionModelActivated,deviceValidated;
    }
    static string Argument(string name) {
        var args=Environment.GetCommandLineArgs();
        for(int i=0;i+1<args.Length;i++)if(args[i]==name)return args[i+1];
        throw new ArgumentException("Missing explicit check argument: "+name);
    }
    public static void Run() {
        string output=Argument("-nonFireResult");
        var result=new Result{status="RUNNING",scope="Native Unity JsonUtility and full production discovery policy using original YOLO discovery JSON; existing shared UI regression. No real gestures, TCP/PICO transport, scene art or device acceptance."};
        try {
            var lines=File.ReadAllLines(Argument("-nonFireInput"));
            if(lines.Length!=4)throw new Exception("Expected three original positive frames and one negative frame");
            var policy=new DiscoverySelection();
            long first=0;
            for(int i=0;i<lines.Length;i++) {
                var frame=JsonUtility.FromJson<DetectionFrameMessage>(lines[i]);
                if(frame==null || frame.status!="OK" || frame.scene!="discovery" || frame.model_validation!="tree:UNVALIDATED")throw new Exception("Actual discovery envelope was not retained by native JSON parsing");
                if(frame.pose_available || frame.pose_model_ready)throw new Exception("This 2D diagnostic must not fabricate pose readiness");
                if(i==0)first=frame.timestamp;
                float now=1f+(frame.timestamp-first)/1000f;
                foreach(var d in frame.detections) {
                    if(d.class_id!=553 || d.@class!="Tree" || d.class_name!="Tree" || d.target_scene!="tree" || d.bbox==null || d.bbox.Length!=4)throw new Exception("Native wire DTO lost a Tree field");
                }
                if(policy.Update(frame,now,false)!=null || policy.LockedId!=null)throw new Exception("Unvalidated non-fire model automatically selected in native Unity");
                if(i<3) {
                    if(policy.Candidates.Count!=1)throw new Exception("This regression must exercise validation rejection, not ambiguity/confidence rejection");
                    result.positiveFrames++;
                    result.positiveSpanSeconds=now-1f;
                }else if(policy.Candidates.Count!=0)throw new Exception("Negative current frame retained a candidate");
                result.frames++;
            }
            if(result.positiveSpanSeconds<policy.stableSeconds || result.positiveFrames<policy.minimumFrames)throw new Exception("Actual producer timing did not reach the existing automatic-selection window");
            result.unityJsonUtilityPassed=true;
            result.unvalidatedAutomaticSelectionRejected=true;
            Debug.Log("NONFIRE_NATIVE_JSON_POLICY_PASS: actual discovery messages rejected after native JsonUtility parsing; no model activation");
            SharedInterfaceChecks.Run();
            result.sharedInterfaceRegressionPassed=true;
            result.status="NATIVE_NONFIRE_VALIDATION_CHECKS_PASSED";
            File.WriteAllText(output,JsonUtility.ToJson(result,true));
            Debug.Log("NONFIRE_NATIVE_CHECK_PASS: native JSON policy and existing shared interface checks");
            EditorApplication.Exit(0);
        }catch(Exception error) {
            result.status="FAILED";result.error=error.ToString();
            File.WriteAllText(output,JsonUtility.ToJson(result,true));
            Debug.LogException(error);
            EditorApplication.Exit(2);
        }
    }
}
