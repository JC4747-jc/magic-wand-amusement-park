using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class CaptureDemoTimelineChecks
{
    const string Key="MagicMR.CaptureTimeline";
    static CaptureTo2D capture;
    static int stage;
    static double started,phaseStart;
    static string Output=>Path.GetFullPath(SessionState.GetString(Key+".Output",".codex-tmp/capture-animation-timeline"));
    static CaptureDemoTimelineChecks() {EditorApplication.update+=Tick;}
    public static void Run()=>Begin("DesktopCaptureDemo",".codex-tmp/capture-animation-timeline");
    public static void RunSample()=>Begin("SampleScene",".codex-tmp/sample-capture-animation-timeline");
    static void Begin(string scene,string output)
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("An idle editor is required");
        SessionState.SetString(Key+".Case",scene);SessionState.SetString(Key+".Output",output);
        SessionState.SetString(Key+".Scene",UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);
        SessionState.SetInt(Key+".Exit",-1);SessionState.SetBool(Key,true);
        Directory.CreateDirectory(Output);EditorSceneManager.OpenScene("Assets/Scenes/"+scene+".unity");EditorApplication.EnterPlaymode();
    }
    static void Require(bool ok,string reason) {if(!ok)throw new Exception(reason);}
    static bool CardVisible=>capture.card2DUI.gameObject.activeInHierarchy;
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false))return;
        if(!EditorApplication.isPlaying)
        {
            int code=SessionState.GetInt(Key+".Exit",-1);if(code<0)return;SessionState.SetBool(Key,false);
            string previous=SessionState.GetString(Key+".Scene","");if(!string.IsNullOrEmpty(previous))EditorSceneManager.OpenScene(previous);
            EditorApplication.Exit(code);return;
        }
        try
        {
            if(stage==0) {started=phaseStart=EditorApplication.timeSinceStartup;stage=1;return;}
            Require(EditorApplication.timeSinceStartup-started<20,"Capture timeline timed out");
            double elapsed=EditorApplication.timeSinceStartup-phaseStart;
            if(stage==1 && elapsed>.25)
            {
                capture=UnityEngine.Object.FindFirstObjectByType<CaptureTo2D>();
                Require(capture&&capture.target3DObject&&capture.card2DUI&&capture.backpackIcon&&capture.captureCamera,"Actual desktop scene bindings incomplete");
                Time.timeScale=0;
                Require(capture.TryCapture()&&!capture.TryCapture(),"Single capture guard failed");
                Require(capture.IsCaptured&&capture.IsAnimating&&!capture.target3DObject.activeSelf&&CardVisible,"Capture did not show card and hide source");
                Snapshot();stage=2;phaseStart=EditorApplication.timeSinceStartup;
            }
            else if(stage==2 && elapsed>.2)
            {
                if(SessionState.GetString(Key+".Case","DesktopCaptureDemo")=="DesktopCaptureDemo"){
                    var control=GameObject.Find("CaptureResetButton");Require(control,"Actual desktop reset control missing");
                    var reset=control.GetComponent<UnityEngine.UI.Button>();Require(reset,"Actual desktop reset button missing");reset.onClick.Invoke();
                    File.WriteAllText(Path.Combine(Output,"reset-source.txt"),"Actual DesktopCaptureDemo reset button callback invoked.\n");
                }else{
                    capture.ResetCapture();
                    File.WriteAllText(Path.Combine(Output,"reset-source.txt"),"SampleScene has no DesktopCaptureDemo reset button; actual CaptureTo2D.ResetCapture API invoked. No button, keyboard or AR input claim.\n");
                }
                Require(capture.target3DObject.activeSelf&&!capture.IsCaptured&&!capture.IsAnimating&&!CardVisible,"Mid-flight reset failed");
                Require(capture.backpackIcon.gameObject.activeInHierarchy,"Reset hid the shared backpack UI");
                Require(capture.TryCapture(),"Reset could not start another capture");stage=3;phaseStart=EditorApplication.timeSinceStartup;
            }
            else if(stage==3 && elapsed>1.8)
            {
                Require(capture.IsCaptured&&!capture.IsAnimating&&!CardVisible&&!capture.target3DObject.activeSelf,"Animation did not complete while timeScale=0");
                capture.ResetCapture();Require(capture.TryCapture(),"Completed capture could not reset");stage=4;phaseStart=EditorApplication.timeSinceStartup;
            }
            else if(stage==4 && elapsed>.2)
            {
                capture.enabled=false;Require(!capture.IsCaptured&&!capture.IsAnimating&&capture.target3DObject.activeSelf&&!CardVisible,"Disable left a hidden source or running animation");
                capture.enabled=true;Require(capture.TryCapture(),"Re-enable could not capture");
                var original=capture.target3DObject;var replacement=new GameObject("Retarget fixture");capture.SetTarget(replacement);
                Require(original.activeSelf&&!capture.IsCaptured&&!capture.IsAnimating,"Retarget failed to restore previous object");capture.SetTarget(original);UnityEngine.Object.Destroy(replacement);
                Time.timeScale=1;
                File.WriteAllText(Path.Combine(Output,"result.txt"),"PASS: actual "+SessionState.GetString(Key+".Case","DesktopCaptureDemo")+" bindings, nonblank rendered snapshot, duplicate request, mid-flight reset, unscaled completion, disable/re-enable, retarget cleanup. No backend request or device claim.\n");
                Debug.Log("CAPTURE_PLAYMODE_TIMELINE_PASS: "+SessionState.GetString(Key+".Case","")+", snapshot, reset/cancel/reuse, unscaled completion, retarget");Finish(0);
            }
        }
        catch(Exception error) {Debug.LogException(error);File.WriteAllText(Path.Combine(Output,"result.txt"),"FAIL: "+error);Time.timeScale=1;Finish(1);}
    }
    static void Finish(int code) {SessionState.SetInt(Key+".Exit",code);EditorApplication.ExitPlaymode();}
    static void Snapshot()
    {
        var texture=capture.captureCamera.targetTexture;Require(texture&&texture.IsCreated(),"Capture render texture unavailable");
        var bounds=capture.target3DObject.GetComponent<Renderer>().bounds;
        for(int i=0;i<8;i++)
        {
            var corner=bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
            var view=capture.captureCamera.WorldToViewportPoint(corner);
            Require(view.x>.01f&&view.x<.99f&&view.y>.01f&&view.y<.99f&&view.z>capture.captureCamera.nearClipPlane,"Capture cropped the target bounds");
        }
        var previous=RenderTexture.active;var pixels=new Texture2D(texture.width,texture.height,TextureFormat.RGB24,false);
        try
        {
            RenderTexture.active=texture;pixels.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0);pixels.Apply();
            var colors=pixels.GetPixels32();bool varied=false;for(int i=1;i<colors.Length;i++)if(Math.Abs(colors[i].r-colors[0].r)>12||Math.Abs(colors[i].g-colors[0].g)>12) {varied=true;break;}
            Require(varied,"Captured card contains only a flat background");File.WriteAllBytes(Path.Combine(Output,"captured-card.png"),pixels.EncodeToPNG());
        }
        finally {RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(pixels);}
    }
}
