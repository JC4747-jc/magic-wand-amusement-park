using System;
using System.Reflection;
using MagicMR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class SharedInterfaceChecks {
 public static void Run(){
  string restore=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
  var preference=typeof(ParkSharedInterface).GetField("automaticSwitching",BindingFlags.Static|BindingFlags.NonPublic);bool previous=(bool)preference.GetValue(null);
  EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
  var camera=new GameObject("Shared UI check camera").AddComponent<Camera>();camera.tag="MainCamera";
  var go=new GameObject("Shared UI check");var ui=go.AddComponent<ParkSharedInterface>();
  try{
   var invalidCandidates=new[]{
    new Perception.DetectionMessage{instance_id="bad",target_scene="bugs",class_name="Cockroach",confidence=1.1f,x1=1,y1=1,x2=20,y2=20},
    new Perception.DetectionMessage{instance_id="bad",target_scene="bugs",class_name="Cockroach",confidence=.9f,x1=float.NaN,y1=1,x2=20,y2=20},
    new Perception.DetectionMessage{instance_id="bad",target_scene="bugs",class_name="Cockroach",confidence=.9f,x1=20,y1=1,x2=10,y2=20},
    new Perception.DetectionMessage{instance_id="bad",target_scene="bugs",class_name="Cockroach",confidence=.9f,x1=1,y1=1,x2=700,y2=20},
    new Perception.DetectionMessage{instance_id="bad",target_scene="unknown",confidence=.9f,x1=1,y1=1,x2=20,y2=20},null};
   foreach(var d in invalidCandidates) {
    var policy=new DiscoverySelection();
    policy.Update(new Perception.DetectionFrameMessage{scene="discovery",status="OK",frame_id=1,width=640,height=480,detections=new[]{d}},0,false);
    if(policy.Candidates.Count!=0)throw new Exception("Malformed detection became selectable");
   }
   foreach(var item in new[]{new[]{"Cat","pet"},new[]{"Dog","pet"},new[]{"potted plant","plant"},new[]{"Spider","bugs"},new[]{"Cockroach","bugs"},new[]{"Tree","tree"},new[]{"Statue","statue"},new[]{"BronzeBeast","statue"},new[]{"Lighter","lighter"}}) {
    var d=new Perception.DetectionMessage{instance_id="mapping",class_name=item[0],target_scene=item[1],confidence=.9f,x1=1,y1=1,x2=20,y2=20};
    if(!DiscoverySelection.ValidCandidate(d,640,480))throw new Exception("Valid class route rejected: "+item[0]);
    d.target_scene=item[1]=="plant"?"pet":"plant";
    if(DiscoverySelection.ValidCandidate(d,640,480))throw new Exception("Mismatched class route accepted: "+item[0]);
   }
   var legacy=new Perception.DetectionMessage{instance_id="legacy",@class="Cockroach",target_scene="bugs",confidence=.9f,x1=1,y1=1,x2=20,y2=20};
   if(!DiscoverySelection.ValidCandidate(legacy,640,480))throw new Exception("Legacy class field rejected");
   legacy.class_name="Cat";if(DiscoverySelection.ValidCandidate(legacy,640,480))throw new Exception("Conflicting class fields accepted");
   foreach(var pair in new[]{new[]{"Cat","Dog","pet"},new[]{"Dog","Cat","pet"},new[]{"Spider","Cockroach","bugs"},new[]{"Cockroach","Spider","bugs"}}) {
    var d=new Perception.DetectionMessage{instance_id="conflict",class_name=pair[0],@class=pair[1],target_scene=pair[2],confidence=.9f,x1=1,y1=1,x2=20,y2=20};
    if(DiscoverySelection.ValidCandidate(d,640,480))throw new Exception("Same-scene conflicting species accepted");
   }
   foreach(var pair in new[]{new[]{"potted plant","PottedPlant","plant"},new[]{"Statue","sculpture","statue"},new[]{" cat ","Cat","pet"}}) {
    var d=new Perception.DetectionMessage{instance_id="alias",class_name=pair[0],@class=pair[1],target_scene=pair[2],confidence=.9f,x1=1,y1=1,x2=20,y2=20};
    if(!DiscoverySelection.ValidCandidate(d,640,480))throw new Exception("Equivalent class aliases rejected");
   }
   foreach(string marker in new[]{"PUBLIC_BASELINE_ONLY_PICO_PENDING","LOCAL_TEST_PENDING","REJECTED_LOW_RECALL_OFFLINE_RESEARCH_ONLY","public_pilot_only"}) {
    var policy=new DiscoverySelection();
    for(int i=0;i<4;i++) {
     var frame=new Perception.DetectionFrameMessage{status="OK",scene="discovery",frame_id=i+1,width=640,height=480,weights="candidate",model_validation=marker,detections=new[]{new Perception.DetectionMessage{instance_id="candidate-1",target_scene="bugs",class_name="Cockroach",confidence=.95f,x1=10,y1=10,x2=100,y2=100}}};
     if(policy.Update(frame,i*.4f,false)!=null)throw new Exception("Unvalidated model automatically routed: "+marker);
    }
    if(policy.Candidates.Count!=1 || policy.Choose("candidate-1",1.3f,false)!="bugs")throw new Exception("Research candidate unavailable for manual selection");
   }
   ui.SendMessage("Start");bool before=ui.Selection.automatic;ui.Command("Auto");if(ui.Selection.automatic==before)throw new Exception("Auto toggle failed");
   if(typeof(ParkSharedInterface).GetMethod("OnGUI",BindingFlags.Instance|BindingFlags.NonPublic)!=null)throw new Exception("Shared UI retained a second desktop debug panel");
   ui.Command("Test bloom");if(!ui.Feedback.Contains("require a selected VIRTUAL SIMULATION"))throw new Exception("Unselected live UI accepted simulated bloom");
   var privateFlags=BindingFlags.Instance|BindingFlags.NonPublic;
   var cache=typeof(ParkSharedInterface).GetMethod("CacheSentImage",privateFlags);
   var bind=typeof(ParkSharedInterface).GetMethod("BindMatchedImage",privateFlags);
   var preview=(UnityEngine.UI.RawImage)typeof(ParkSharedInterface).GetField("matchedPreview",privateFlags).GetValue(ui);
   var image=new Texture2D(2,2,TextureFormat.RGB24,false);
   byte[] jpeg;
   try {image.SetPixels(new[]{Color.red,Color.green,Color.blue,Color.white});image.Apply();jpeg=image.EncodeToJPG();}
   finally {UnityEngine.Object.DestroyImmediate(image);}
   ui.replayMode=false;cache.Invoke(ui,new object[]{77u,jpeg});
   var previewFrame=new Perception.DetectionFrameMessage{frame_id=77,width=2,height=2};
   bind.Invoke(ui,new object[]{previewFrame,false});if(!preview.enabled || !preview.texture)throw new Exception("Exact sent frame did not bind preview");
   previewFrame.frame_id=78;bind.Invoke(ui,new object[]{previewFrame,false});if(preview.enabled || preview.texture)throw new Exception("Unmatched frame reused stale image");
   previewFrame.frame_id=77;bind.Invoke(ui,new object[]{previewFrame,true});if(preview.enabled)throw new Exception("Simulation reused live image");
   previewFrame.replay_image_base64=Convert.ToBase64String(jpeg);bind.Invoke(ui,new object[]{previewFrame,true});
   if(!preview.enabled || !preview.texture)throw new Exception("Explicit replay image did not display");
   previewFrame.replay_image_base64="invalid-base64";bind.Invoke(ui,new object[]{previewFrame,true});
   if(preview.enabled || preview.texture)throw new Exception("Invalid replay image reused previous background");
   previewFrame.replay_image_base64=null;
   previewFrame.width=3;bind.Invoke(ui,new object[]{previewFrame,false});if(preview.enabled)throw new Exception("Mismatched dimensions displayed image");
   for(uint id=78;id<83;id++)cache.Invoke(ui,new object[]{id,jpeg});
   previewFrame.width=2;bind.Invoke(ui,new object[]{previewFrame,false});if(preview.enabled)throw new Exception("Evicted sent frame remained visible");
   Debug.Log("SHARED_MATCHED_PREVIEW_PASS: exact frame, missing frame, simulation, dimensions and bounded eviction; synthetic JPEG only");
   var imageRecording=Resources.Load<TextAsset>("MagicMR/recorded-recognition-images.jsonl");
   if(!imageRecording)throw new Exception("Recorded image replay resource missing");
   var imageRows=imageRecording.text.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries);
   if(imageRows.Length!=5)throw new Exception("Recorded image replay frame count changed");
   foreach(var row in imageRows) {
    var recordedImage=JsonUtility.FromJson<Perception.DetectionFrameMessage>(row);
    if(recordedImage.replay_image_kind!="SOURCE_JPEG_OFFLINE_NOT_EXACT_SENT_BYTES")throw new Exception("Replay image provenance missing");
    bind.Invoke(ui,new object[]{recordedImage,true});
    if(!preview.enabled || !preview.texture)throw new Exception("Recorded source image missing or dimensions differ");
   }
   Debug.Log("SHARED_RECORDED_IMAGE_PASS: five source JPEG frames decoded at recorded dimensions; no device or model acceptance");
   var second=new GameObject("Second scene interface").AddComponent<ParkSharedInterface>();second.SendMessage("Start");if(second.Selection.automatic!=ui.Selection.automatic)throw new Exception("Scene transition lost Auto preference");
   ui.Command("Plane");string instruction=ui.Feedback;ui.ApplyJson("{\"status\":\"OK\",\"scene\":\"lighter\",\"frame_id\":1,\"width\":640,\"height\":480,\"detections\":[]}",true);
   if((string)typeof(ParkSharedInterface).GetField("actionFeedback",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ui)!=instruction)throw new Exception("Recognition replaced command feedback");
   ui.ApplyJson("{\"status\":\"OK\",\"scene\":\"lighter\",\"frame_id\":2,\"width\":640,\"height\":480,\"detections\":[{\"class_name\":\"Lighter\",\"instance_id\":\"main-lighter-test\",\"confidence\":0.9,\"x1\":10,\"y1\":10,\"x2\":80,\"y2\":100}]}",false);
   if(ui.Selection.Candidates.Count!=1 || ui.Selection.Candidates[0].target_scene!="lighter")throw new Exception("Main lighter recognition missing from shared candidates");
   typeof(ParkSharedInterface).GetMethod("Route",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ui,new object[]{"lighter","main-lighter-test"});
   if(!ui.Feedback.Contains("main tracking"))throw new Exception("Selecting current lighter attempted scene reload");
   ui.ApplyJson("{\"status\":\"OK\",\"scene\":\"lighter\",\"frame_id\":3,\"width\":640,\"height\":480,\"detections\":[null]}",false);
   if(ui.Selection.Candidates.Count!=0)throw new Exception("Null UI detection retained a current candidate");
   ui.ApplyJson("{\"status\":\"OK\",\"scene\":\"discovery\",\"frame_id\":4,\"width\":640,\"height\":480,\"detections\":[{\"instance_id\":\"roach-live\",\"target_scene\":\"bugs\",\"class_name\":\"Cockroach\",\"confidence\":0.9,\"x1\":10,\"y1\":10,\"x2\":80,\"y2\":100}]}",false);
   if(ui.Selection.Candidates.Count!=1 || ui.Selection.Candidates[0].target_scene!="bugs")throw new Exception("Main interface overwrote discovery target scene");
   ui.controller=go.AddComponent<ParkController>();
   ui.Command("Recorded replay");
   if(!ui.replayMode || !ui.replay)throw new Exception("Recorded inference replay did not start");
   var recorded=ui.replay.text.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries);
   if(recorded.Length!=5)throw new Exception("Recorded TCP fixture missing frames");
   ui.ApplyJson(recorded[0],true);
   if(ui.CurrentFrame.scene!="discovery" || ui.Selection.Candidates.Count==0)throw new Exception("Actual YOLO recording missing shared candidates");
   ui.Command("Replay");
   if(ui.CurrentFrame!=null || preview.enabled || preview.texture)throw new Exception("Leaving replay retained offline image or detection frame");
   var imageCache=(System.Collections.Generic.Dictionary<long,byte[]>)typeof(ParkSharedInterface).GetField("sentImages",privateFlags).GetValue(ui);
   if(imageCache.Count!=0)throw new Exception("Replay boundary retained live frame cache");
   Debug.Log("SHARED_REPLAY_BOUNDARY_PASS: immediate image/frame/cache cleanup on live transition; editor fixture only");
   var rect=new GameObject("Distant hand-ray button",typeof(RectTransform)).GetComponent<RectTransform>();rect.position=new Vector3(0,0,1.2f);rect.localScale=Vector3.one*.001f;rect.sizeDelta=new Vector2(140,55);
   if(!ParkSharedInterface.TryButtonRayHit(rect,new Ray(Vector3.zero,Vector3.forward),out float distance) || Mathf.Abs(distance-1.2f)>.001f)throw new Exception("Distant UI ray did not hit");
   if(ParkSharedInterface.TryButtonRayHit(rect,new Ray(Vector3.right,Vector3.forward),out _))throw new Exception("Ray outside button accepted");
   if(ParkSharedInterface.TryButtonRayHit(rect,new Ray(Vector3.zero,Vector3.back),out _))throw new Exception("Backward ray accepted");rect.gameObject.SetActive(false);if(ParkSharedInterface.TryButtonRayHit(rect,new Ray(Vector3.zero,Vector3.forward),out _))throw new Exception("Inactive button accepted");
   Debug.Log("SHARED_INTERFACE_CHECK_PASS: cross-scene Auto preference and feedback retained during recognition; editor fixture only");
  }finally{preference.SetValue(null,previous);EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);if(!string.IsNullOrEmpty(restore))EditorSceneManager.OpenScene(restore);}
 }
}
