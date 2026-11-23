using System;
using System.IO;
using System.Linq;
using System.Reflection;
using MagicMR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ParkHandEvidenceChecks
{
    static readonly string Output = Path.GetFullPath(".codex-tmp/park-hand-evidence-checks");

    public static void Run()
    {
        Directory.CreateDirectory(Output);
        try {
            VerifyDisabledAndBoundReply();
            VerifyLimit("row-limit",ParkHandEvidence.MaxSamples+2,0);
            VerifyLimit("byte-limit",ParkHandEvidence.MaxSamples+2,3000);
            VerifyDeadline();
            VerifySavedSceneBindings();
            File.WriteAllText(Path.Combine(Output,"result.txt"),"PASS: Park hand evidence opt-in, recorded reply binding, valid/invalid rows, row/byte/time caps, close and saved scene bindings.\n");
            EditorApplication.Exit(0);
        }
        catch(Exception error) {Debug.LogException(error);EditorApplication.Exit(1);}
    }

    static void VerifyDisabledAndBoundReply()
    {
        string folder=Make("reply-binding");
        Require(ParkHandEvidence.TryOpen(folder,folder,0)==null,"Recorder opened without hand-resource marker");
        File.WriteAllText(Path.Combine(folder,ParkHandEvidence.EnableFile),"");
        Require(ParkHandEvidence.TryOpen(null,folder,0)==null,"Recorder opened without a stereo session");
        var camera=new GameObject("Camera recording fixture");
        var capture=camera.AddComponent<PicoStereoCapture>();
        try {
            var trace=ParkHandEvidence.TryOpen(folder,folder,Time.unscaledTime);
            Require(trace!=null&&trace.IsOpen,"Recorder did not open");
            Set(capture,"parkHandEvidence",trace);
            Set(capture,"recordDirectory",folder);
            capture.recordStereo=true;
            Set(capture,"lastRecordedFrame",17L);
            Set(capture,"pendingReplyCaptureNs",1212500000L);
            trace.Append(new ParkHandEvidence.Row {scene="room",target_distance_m=-1},Time.unscaledTime);
            capture.RecordAcceptedReply(16,"{\"frame_id\":16}");
            capture.RecordAcceptedReply(17,"{\"frame_id\":17}");
            trace.Append(new ParkHandEvidence.Row {
                scene="room",joints_fresh=true,sample_valid=true,target_before="fixture:1",target_after="fixture:1",
                target_tracked_before=true,in_reach_after=true,target_distance_m=.18f,
                pinch_m=.067f,swipe_axis=Vector3.right,extension=new Vector4(.84f,.88f,.95f,.92f),
                recognized=EditDimension.Appearance,spell_accepted=true
            },Time.unscaledTime);
            trace.Dispose();
            Require(ParkHandEvidence.Active==null,"Disposed fixture still recording");
            var rows=File.ReadAllLines(Directory.GetFiles(folder,"hand-samples-*.jsonl").Single())
                .Select(JsonUtility.FromJson<ParkHandEvidence.Row>).ToArray();
            Require(rows.Length==2&&rows[0].schema==1&&rows[0].sequence==1&&rows[1].sequence==2,"Invalid row schema or count");
            Require(!rows[0].sample_valid&&rows[0].last_recorded_reply_frame_id==-1&&
                rows[0].last_recorded_reply_age_seconds==-1,"Invalid row bound to a reply");
            Require(rows[1].sample_valid&&rows[1].spell_accepted&&rows[1].recognized==EditDimension.Appearance&&
                rows[1].last_recorded_reply_frame_id==17&&rows[1].last_recorded_reply_capture_ns==1212500000&&
                rows[1].last_recorded_reply_age_seconds>=0&&rows[1].extension.y==.88f&&rows[1].swipe_axis==Vector3.right,
                "Missing applied action, hand fields or matching reply binding");
            Require(File.ReadAllText(Path.Combine(folder,"1212500000-reply.json"))=="{\"frame_id\":17}",
                "Raw reply payload differs");
        }
        finally {UnityEngine.Object.DestroyImmediate(camera);}
    }

    static void VerifyLimit(string name,int attempts,int idLength)
    {
        string folder=Make(name);
        using var trace=Enable(folder);
        string target=new string('x',idLength);
        for(int i=0;i<attempts;i++)
            trace.Append(new ParkHandEvidence.Row {scene="plant",target_after=target},i*.01f);
        Require(!trace.IsOpen,name+" left the recorder open");
        var file=new FileInfo(Directory.GetFiles(folder,"hand-samples-*.jsonl").Single());
        int count=File.ReadLines(file.FullName).Count();
        Require(file.Length<=ParkHandEvidence.MaxBytes&&count>0&&
            (idLength==0?count==ParkHandEvidence.MaxSamples:count<ParkHandEvidence.MaxSamples),
            "Incorrect bound for "+name);
    }

    static void VerifyDeadline()
    {
        string folder=Make("deadline");
        using var trace=Enable(folder);
        trace.Append(default,ParkHandEvidence.MaxSeconds-.1f);
        trace.Append(default,ParkHandEvidence.MaxSeconds);
        Require(!trace.IsOpen&&File.ReadLines(Directory.GetFiles(folder,"hand-samples-*.jsonl").Single()).Count()==1,
            "Deadline did not close and flush");
    }

    static void VerifySavedSceneBindings()
    {
        foreach(var scene in new[]{"ParkTree","ParkBugs","ParkStatue","ParkRoom","ParkPlant","ParkPet","BridgeStereoFusion"}) {
            EditorSceneManager.OpenScene($"Assets/Scenes/{scene}.unity");
            var camera=UnityEngine.Object.FindObjectsByType<PicoStereoCapture>(FindObjectsSortMode.None).Single();
            Require(camera.enabled,"Stereo capture is disabled in "+scene);
            Require((camera.parkReceiver!=null)==(scene!="BridgeStereoFusion"),"Wrong Park recorder scope in "+scene);
        }
    }

    static ParkHandEvidence Enable(string folder)
    {
        File.WriteAllText(Path.Combine(folder,ParkHandEvidence.EnableFile),"");
        return ParkHandEvidence.TryOpen(folder,folder,0)??throw new InvalidOperationException("Could not open fixture");
    }

    static string Make(string name) => Directory.CreateDirectory(Path.Combine(Output,name)).FullName;
    static void Require(bool condition,string message) {if(!condition)throw new InvalidOperationException(message);}
    static void Set(PicoStereoCapture capture,string field,object value) =>
        typeof(PicoStereoCapture).GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(capture,value);
}
