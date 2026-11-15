using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Unity.XR.PXR;

namespace Perception
{
    // Optional sidecar. Only rotates the existing positional anchor; never writes translation/scale.
    [DefaultExecutionOrder(-20)]
    public sealed class F2GPoseClient : MonoBehaviour
    {
        public StereoYoloLocator locator;
        public string host = "127.0.0.1";
        public int port = 5006;
        [Range(1,5)] public float sendFps = 2;
        public int requestDeadlineMs = 1200;
        [Tooltip("Only for explicit transport tests; never generates a synthetic pose.")]
        public bool allowDiagnosticServer;
        [Range(.1f,2)] public float maxResultAge = .9f;
        public float rotationSmoothSpeed = 12;
        public string Status { get; private set; } = "WAITING_FOR_POSE";

        [Serializable] sealed class Header
        {
            public int version = 2, width = 640, height = 480, left_bytes = 640*480*4, right_bytes = 640*480;
            public string pixel_format = "native_top_down_gzip_rgba_gray", session;
            public long frame_id, timestamp_ns, right_timestamp_ns;
            public float[] left_k, right_k, right_from_left_r, right_from_left_t;
        }
        [Serializable] sealed class Health { public int version; public bool pose_ready, image_ready; public string mode; }
        [Serializable] sealed class Reply
        {
            public string session, reason;
            public long frame_id, timestamp_ns;
            public bool valid;
            public float[] rotation;
            public int valid_depth_points;
            public float valid_depth_ratio, processing_ms;
        }
        sealed class Completed
        {
            public string json, error, session;
            public bool health;
            public long id, timestamp;
            public float capturedAt;
            public Quaternion head, camera;
        }

        readonly ConcurrentQueue<Completed> completed = new ConcurrentQueue<Completed>();
        readonly F2GRotationFilter filter = new F2GRotationFilter();
        readonly PoseRetryPolicy retry = new PoseRetryPolicy();
        PoseTransport transport = new PoseTransport();
        bool serviceReady;
        float nextHealth;
        volatile string session = Guid.NewGuid().ToString("N");
        volatile bool stopping;
        int busy;
        long nextId;
        float nextSend;
        bool calibrated;
        Quaternion referenceWorld, initialAnchor;
        Transform anchor;
        AnchorManager anchors;
        MagicMR.RealityEditor reality;
        bool AdvertisedReady => locator != null && locator.transport != null && locator.transport.IsConnected &&
            locator.detections != null && Time.realtimeSinceStartup-locator.detections.PoseAdvertisementAt < .5f &&
            locator.detections.PoseAvailable && (allowDiagnosticServer || locator.detections.PoseModelReady);

        void Awake()
        {
            if (locator == null) locator = GetComponent<StereoYoloLocator>();
            anchors = FindFirstObjectByType<AnchorManager>();
            if (locator != null && locator.lighter != null) reality = locator.lighter.GetComponent<MagicMR.RealityEditor>();
        }
        void OnEnable() { stopping = false; ResetSession(); }

        public void SetPoseEnabled(bool value) { enabled = value; }

        public void ResetSession()
        {
            session = Guid.NewGuid().ToString("N");
            transport.Dispose(); transport = new PoseTransport();
            serviceReady = false; retry.Reset(); nextHealth = nextSend = 0; busy = 0;
            filter.Reset(); calibrated = false;
            if (anchor != null)
            {
                reality?.RotateWorldEffects(initialAnchor*Quaternion.Inverse(anchor.rotation));
                anchor.rotation = initialAnchor;
            }
            anchor = null;
            while (completed.TryDequeue(out _)) { }
            Status = "WAITING_FOR_POSE";
        }

        // Called immediately after normal upright summon registration, before rotation starts.
        public void CalibrateAtSummon()
        {
            if (!isActiveAndEnabled) return;
            if (anchors == null || anchors.LighterAnchor == null || filter.StableSamples < 3 ||
                Time.realtimeSinceStartup-filter.LastAcceptedTime > maxResultAge)
            {
                Status = "NO_ROTATION_REFERENCE - keep still, reset and summon again";
                return;
            }
            anchor = anchors.LighterAnchor;
            initialAnchor = anchor.rotation;
            referenceWorld = filter.Accepted;
            calibrated = true;
            Status = "ROTATION_CALIBRATED";
        }

        static Pose SdkPose(XrCameraExtrinsics e) => new Pose(
            new Vector3(e.pose.Position.X,e.pose.Position.Y,e.pose.Position.Z),
            new Quaternion(e.pose.Orientation.X,e.pose.Orientation.Y,e.pose.Orientation.Z,e.pose.Orientation.W).normalized);
        static float[] K(XrCameraIntrinsics k) => new[] {
            k.focalLength.X,0,k.principalPoint.X, 0,k.focalLength.Y,k.principalPoint.Y, 0,0,1f };

        public void OfferPair(byte[] rgba, byte[] rightGray, XrCameraIntrinsics leftK, XrCameraIntrinsics rightK,
            XrCameraExtrinsics leftE, XrCameraExtrinsics rightE, long timestamp, long rightTimestamp)
        {
            if (!isActiveAndEnabled || stopping || !serviceReady || !AdvertisedReady || locator == null || !locator.UsesVisualAverage ||
                Time.realtimeSinceStartup < nextSend || busy != 0) return;
            if (!locator.TryGetCapturePose(timestamp, out Pose head, out float capturedAt)) return;
            if (rgba == null || rgba.Length != 640*480*4 || rightGray == null || rightGray.Length != 640*480) return;
            Pose left = SdkPose(leftE), right = SdkPose(rightE);
            F2GPoseGeometry.RightFromLeft(left,right,out float[] r,out float[] t);
            var header = new Header { session = session, frame_id = ++nextId, timestamp_ns = timestamp,
                right_timestamp_ns = rightTimestamp, left_k = K(leftK), right_k = K(rightK),
                right_from_left_r = r, right_from_left_t = t };
            byte[] json = Encoding.UTF8.GetBytes(JsonUtility.ToJson(header));
            var result = new Completed { session = session, id = header.frame_id, timestamp = timestamp,
                capturedAt = capturedAt, head = head.rotation, camera = left.rotation };
            // No queue: if busy, the offered frame is dropped; next request takes a fresh camera pair.
            busy = 1;
            nextSend = Time.realtimeSinceStartup + 1/Mathf.Max(1,sendFps);
            Launch(json, rgba, rightGray, result);
        }

        void Launch(byte[] json, byte[] left, byte[] right, Completed result)
        {
            var owner = transport;
            string requestHost = host; int requestPort = port;
            int deadline = Mathf.Clamp(requestDeadlineMs, 100, 2000);
            Task.Run(async () =>
            {
                try { result.json = await owner.ExchangeAsync(requestHost, requestPort, json, left, right, deadline); }
                catch (Exception e) { result.error = e.GetBaseException().Message; }
                finally { completed.Enqueue(result); }
            });
        }

        void FailService(string reason)
        {
            serviceReady = false;
            retry.Fail(Time.realtimeSinceStartup);
            transport.Dispose(); transport = new PoseTransport();
            Status = $"POSE_OFFLINE / retry {retry.NextAttempt-Time.realtimeSinceStartup:F0}s: {reason}";
            Debug.Log($"[PoseLink] failure={retry.Failures} circuit={retry.CircuitOpen} reason={reason}");
        }

        void Update()
        {
            while (completed.TryDequeue(out var result))
            {
                if (result.session != session) continue;
                busy = 0;
                if (result.error != null) { FailService(result.error); continue; }
                try
                {
                    if (result.health)
                    {
                        var health = JsonUtility.FromJson<Health>(result.json);
                        if (health == null || health.version != 2 ||
                            !(health.pose_ready || (allowDiagnosticServer && health.image_ready)))
                        { FailService(health?.mode ?? "SERVER_NOT_READY"); continue; }
                        serviceReady = true;
                        Status = health.pose_ready ? "POSE_READY" : "DIAGNOSTIC_ONLY / no rotation";
                        Debug.Log("[PoseLink] ready mode=" + health.mode);
                        continue;
                    }
                    var reply = JsonUtility.FromJson<Reply>(result.json);
                    if (reply == null || reply.session != session || reply.frame_id != result.id ||
                        reply.timestamp_ns != result.timestamp) { FailService("POSE_ID_MISMATCH"); continue; }
                    retry.Reset(); // A health reply alone must not reset repeated image-transfer failures.
                    if (!reply.valid) { Status = reply.reason ?? "POSE_INVALID"; continue; }
                    if (reply.valid_depth_points < 100 || !float.IsFinite(reply.valid_depth_ratio) ||
                        reply.valid_depth_ratio < .3f || reply.valid_depth_ratio > 1 ||
                        !F2GPoseGeometry.TryWorldRotation(reply.rotation,result.head,result.camera,out var world))
                    { Status = "POSE_GEOMETRY_REJECTED"; continue; }
                    bool accepted = filter.Observe(reply.frame_id,world,result.capturedAt,Time.realtimeSinceStartup,maxResultAge);
                    Debug.Log($"[PoseResult] frame={reply.frame_id} age_ms={(Time.realtimeSinceStartup-result.capturedAt)*1000:F0} processing_ms={reply.processing_ms:F0} accepted={accepted} stable={filter.StableSamples}");
                    if (!accepted)
                    { Status = "POSE_STALE_OR_CONFIRMING"; continue; }
                    Status = calibrated ? "ROTATION_TRACKING" : locator.InteractionReady ?
                        "RESET_AND_SUMMON_FOR_ROTATION" : $"POSE_REFERENCE {Mathf.Min(3,filter.StableSamples)}/3 - keep still";
                }
                catch (Exception e) { FailService("POSE_REPLY_ERROR: " + e.Message); }
            }
            if (!AdvertisedReady && serviceReady)
            {
                serviceReady = false;
                transport.Dispose(); transport = new PoseTransport();
                Status = "POSE_OFFLINE / position continues";
            }
            if (!stopping && AdvertisedReady && !serviceReady && busy == 0 && retry.CanAttempt(Time.realtimeSinceStartup) && Time.realtimeSinceStartup >= nextHealth)
            {
                busy = 1; nextHealth = Time.realtimeSinceStartup + 1f;
                Launch(Encoding.UTF8.GetBytes("{\"version\":2,\"op\":\"health\"}"), null, null,
                    new Completed { session = session, health = true });
            }
            if (!AdvertisedReady && !calibrated) Status = "POSE_UNAVAILABLE / position continues";
            if (calibrated && locator != null && locator.InteractionReady && anchor != null &&
                Time.realtimeSinceStartup-filter.LastAcceptedTime <= maxResultAge)
            {
                Quaternion target = filter.Accepted * Quaternion.Inverse(referenceWorld) * initialAnchor;
                Quaternion before = anchor.rotation;
                anchor.rotation = Quaternion.Slerp(before,target,1-Mathf.Exp(-rotationSmoothSpeed*Time.deltaTime));
                // The flower is a world-space effect; transport its orientation without changing its animation.
                reality?.RotateWorldEffects(anchor.rotation*Quaternion.Inverse(before));
            }
            else if (calibrated && Time.realtimeSinceStartup-filter.LastAcceptedTime > maxResultAge)
                Status = "ROTATION_HOLD - waiting for fresh pose";
        }

        void OnDisable()
        {
            stopping = true;
            ResetSession(); transport.Dispose(); Status = "POSE_DISABLED";
        }
        void OnApplicationPause(bool paused)
        {
            stopping = paused;
            ResetSession();
            if (paused) transport.Dispose();
        }
        void OnDestroy() { stopping = true; transport.Dispose(); }
    }
}
