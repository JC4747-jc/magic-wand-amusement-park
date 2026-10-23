using System;
using System.Collections.Generic;
using MagicMR;
using UnityEngine;
using UnityEngine.XR;
using Unity.XR.PXR;

namespace Perception
{
    /// <summary>Generic translation only; no upright size, bottom, surface-normal or skeleton inference.</summary>
    public sealed class ParkStereoReceiver : MonoBehaviour, IParkTrackingStatus
    {
        public PicoYoloFrameSender sender;
        public TcpClient transport;
        public ParkController controller;
        public bool discoveryMode;
        public readonly DiscoverySelection discovery=new DiscoverySelection();
        public event Action<string,string> DiscoverySceneRequested;
        public event Action<string> RecognitionAccepted;
        public float maxAge=.3f;
        public float maxRecognitionAge=1.5f;
        public float minDepth=.2f, maxDepth=3f;
        public string Status { get; private set; }="NO_TARGET";
        public float AcceptedResultAge {get;private set;}
        struct Sample { public long ns; public Pose pose; }
        readonly List<Sample> poses=new List<Sample>(180);
        Texture2D texture;
        AndroidJavaClass clock;
        byte[] left,right;
        Pose capturePose,cameraPose;
        Vector4 intrinsics;
        float baseline,capturedAt,lastReply;
        long expected,lastId;
        bool waiting;
        bool capturePoseValid,headTracked;
        void Awake() { texture=new Texture2D(640,480,TextureFormat.RGBA32,false);
#if UNITY_ANDROID && !UNITY_EDITOR
            clock=new AndroidJavaClass("java.lang.System");
#endif
        }
        long Now() {
#if UNITY_ANDROID && !UNITY_EDITOR
            return clock.CallStatic<long>("nanoTime");
#else
            return (long)(Time.realtimeSinceStartupAsDouble*1e9);
#endif
        }
        void OnEnable() { if(transport)transport.LineReceived+=Receive; }
        void OnDisable() { if(transport)transport.LineReceived-=Receive; waiting=false; poses.Clear(); controller?.ExpirePhysicalTargets(); }
        void OnDestroy() { if(texture)Destroy(texture); clock?.Dispose(); }
        void Update()
        {
            var device=InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
            bool tracked=device.TryGetFeatureValue(CommonUsages.isTracked,out bool valid) && valid;
            headTracked=tracked;
            if(tracked && device.TryGetFeatureValue(CommonUsages.devicePosition,out Vector3 p) && device.TryGetFeatureValue(CommonUsages.deviceRotation,out Quaternion q)) {
                var root=Camera.main?Camera.main.transform.parent:null;
                poses.Add(new Sample {ns=Now(),pose=new Pose(root?root.TransformPoint(p):p,root?root.rotation*q:q)}); if(poses.Count>180)poses.RemoveAt(0);
            } else { headTracked=false;poses.Clear(); Status="HEAD_TRACKING_LOST"; controller?.ExpirePhysicalTargets(); }
            if(!transport || !transport.IsConnected) { waiting=false; Status="PC_DISCONNECTED"; controller?.ExpirePhysicalTargets(); }
            else if(waiting && Time.realtimeSinceStartup-capturedAt>maxRecognitionAge) { waiting=false; Status="DETECTION_STALE"; controller?.ExpirePhysicalTargets(); }
            else if(waiting && Time.realtimeSinceStartup-capturedAt>maxAge) {Status="LOCATION_RESULT_STALE: recognition pending";controller?.ExpirePhysicalTargets();}
            if(lastReply>0 && Time.realtimeSinceStartup-lastReply>.65f)controller?.ExpirePhysicalTargets();
        }
        public void AcceptPair(byte[] rgba,byte[] l,byte[] r,XrCameraIntrinsics k,XrCameraExtrinsics e,float separation,long timestamp)
        {
            if(!isActiveAndEnabled || waiting || !controller || controller.simulation)return;
            float age=(float)((Now()-timestamp)/1e9);
            if(age<0 || age>maxAge) { Status=age<0?"CAPTURE_CLOCK_MISMATCH":"CAPTURE_TOO_OLD";controller.ExpirePhysicalTargets();return; }
            bool found=false;
            for(int i=poses.Count-1;i>0;i--) { var a=poses[i-1]; var b=poses[i]; if(timestamp<a.ns || timestamp>b.ns || b.ns-a.ns>100000000)continue;
                float t=(float)((double)(timestamp-a.ns)/Math.Max(1,b.ns-a.ns)); capturePose=new Pose(Vector3.Lerp(a.pose.position,b.pose.position,t),Quaternion.Slerp(a.pose.rotation,b.pose.rotation,t)); found=true; break; }
            capturePoseValid=found;
            if(!found)Status="CAPTURE_POSE_UNAVAILABLE: recognition only";
            if(!sender || !transport || !transport.IsConnected) { Status="PC_DISCONNECTED"; return; }
            sender.SceneProfile=discoveryMode?(byte)7:(byte)((int)controller.scenario+1);
            texture.LoadRawTextureData(rgba); texture.Apply(false,false); if(!sender.TrySendTexture(texture))return;
            if(sender.LastSentWidth!=640 || sender.LastSentHeight!=480) { Status="IMAGE_GEOMETRY_MISMATCH"; return; }
            left=l; right=r; intrinsics=new Vector4(k.focalLength.X,k.focalLength.Y,k.principalPoint.X,k.principalPoint.Y); baseline=separation;
            cameraPose=new Pose(new Vector3(e.pose.Position.X,e.pose.Position.Y,e.pose.Position.Z),new Quaternion(e.pose.Orientation.X,e.pose.Orientation.Y,e.pose.Orientation.Z,e.pose.Orientation.W).normalized);
            expected=sender.LastSentFrameId; capturedAt=Time.realtimeSinceStartup-age; waiting=true;
            if(capturePoseValid)PicoStereoCapture.Active?.RecordAcceptedPose(expected,timestamp,capturePose,cameraPose);
        }
        public void Receive(string json)
        {
            DetectionFrameMessage f;
            try { f=JsonUtility.FromJson<DetectionFrameMessage>(json); } catch(Exception ex) { Debug.LogWarning("[ParkDetection] invalid JSON: "+ex.Message); return; }
            float resultAge=Time.realtimeSinceStartup-capturedAt;
            if(f==null || !waiting || !RecognitionReplyPolicy.AcceptRecognition(f.frame_id,expected,lastId,f.width,f.height,resultAge,maxRecognitionAge))return;
            waiting=false; lastId=f.frame_id; lastReply=Time.realtimeSinceStartup;
            AcceptedResultAge=resultAge;
            RecognitionAccepted?.Invoke(json);
            if(discoveryMode && f.scene=="discovery") {
                bool busy=controller.selected && controller.selected.Busy;
                string requested=discovery.Update(f,Time.unscaledTime,busy);
                if(requested!=null) {
                    Debug.Log($"[Discovery] class_scene={requested} instance={discovery.LockedId} reason=stable_single_target weights={f.weights}");
                    DiscoverySceneRequested?.Invoke(requested,discovery.LockedId);
                    if(requested!="lighter" && !controller.SwitchContent(requested,discovery.LockedId)) {
                        discovery.Return(Time.unscaledTime);Status="SCENE_CONTENT_NOT_REGISTERED";return;
                    }
                }
                Status=discovery.LockedId==null?"RECOGNITION_ENTRY: choose a candidate; location not ready":"CLASS_RECOGNIZED: "+discovery.LockedScene+"; location not ready";
                if(discovery.LockedId==null || discovery.LockedScene!=controller.Profile)return;
            }
            if(!string.IsNullOrEmpty(f.status) && f.status!="OK") { Status=f.status; controller.ExpirePhysicalTargets(); return; }
            if(f.scene!=controller.Profile && !(discoveryMode && f.scene=="discovery")) { Status="SCENE_MISMATCH"; controller.ExpirePhysicalTargets(); return; }
            if(f.detections==null || f.detections.Length==0) { Status="NO_TARGET"; controller.ExpirePhysicalTargets(); return; }
            if(!RecognitionReplyPolicy.AcceptSpatial(resultAge,maxAge,capturePoseValid,headTracked)) {
                Status=!capturePoseValid?"CAPTURE_POSE_UNAVAILABLE: recognition only":!headTracked?"HEAD_TRACKING_LOST: recognition only":"LOCATION_RESULT_STALE: recognition only";
                controller.ExpirePhysicalTargets();return;
            }
            bool located=false;
            Status="NO_RELIABLE_TARGET";
            foreach(var d in f.detections) {
                if(discoveryMode && (d.instance_id!=discovery.LockedId || d.target_scene!=controller.Profile))continue;
                string cls=string.IsNullOrEmpty(d.class_name)?d.@class:d.class_name;
                if(!Finite(d.x1)||!Finite(d.x2)||!Finite(d.y1)||!Finite(d.y2)||!Finite(d.confidence)) { Status="INVALID_DETECTION_COORDINATES";continue; }
                if(!controller.Allows(cls)) { Status="CLASS_NOT_ALLOWED_FOR_SCENE";continue; }
                if(d.confidence<.5f) { Status="LOW_DETECTION_CONFIDENCE";continue; }
                if(string.IsNullOrEmpty(d.instance_id)) { Status="INSTANCE_ID_UNAVAILABLE";continue; }
                if(d.x1<4 || d.y1<4 || d.x2>636 || d.y2>476 || d.x2<=d.x1 || d.y2<=d.y1) { Status="TARGET_CLIPPED_OR_INVALID_BOX";continue; }
                int sx=Mathf.Min(24,Mathf.FloorToInt((d.x2-d.x1)*.3f)-5),sy=Mathf.Min(16,Mathf.FloorToInt((d.y2-d.y1)*.3f)-4);
                if(sx<2 || sy<2) { Status="DEPTH_UNRELIABLE: small target"; continue; }
                // Match the raw camera row convention used by the lighter pipeline.
                float y1=sender.FlipVerticallyBeforeEncode?d.y1:479-d.y2;
                float y2=sender.FlipVerticallyBeforeEncode?d.y2:479-d.y1;
                int u=Mathf.RoundToInt((d.x1+d.x2)*.5f),v=Mathf.RoundToInt((y1+y2)*.5f);
                var estimate=StereoPatchMatcher.EstimateDepth(left,right,640,480,u,v,intrinsics.x,baseline,sx,sy);
                if(!estimate.valid || estimate.confidence<.04f || estimate.acceptedSamples<5 || estimate.depthMeters<minDepth || estimate.depthMeters>maxDepth) { Status="DEPTH_UNRELIABLE: "+estimate.reason; continue; }
                Vector3 optical=new Vector3((u-intrinsics.z)*estimate.depthMeters/intrinsics.x,(v-intrinsics.w)*estimate.depthMeters/intrinsics.y,estimate.depthMeters);
                Vector3 world=capturePose.position+capturePose.rotation*StereoFusionGeometry.OpticalToHead(optical,cameraPose);
                controller.Observe(d.instance_id,cls,world); located=true;
            }
            if(located) { Status="TRANSLATION_ONLY: "+f.model_validation+"; confirm orientation / virtual scale"; Debug.Log($"[ParkDetection] frame={f.frame_id} capture_to_reply_ms={(Time.realtimeSinceStartup-capturedAt)*1000:F1} (PICO clock correlated)"); }
            else controller.ExpirePhysicalTargets();
        }
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
    }
}
