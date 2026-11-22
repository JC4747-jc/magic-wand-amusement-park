using System;
using System.Text;
using System.Threading.Tasks;
using MagicMR;
using UnityEngine;

namespace Perception
{
    /// <summary>Optional head estimator for the synchronized pair that produced an accepted Pet detection.</summary>
    internal sealed class PetStereoHeadClient : IDisposable
    {
        const int PairWidth = 640, PairHeight = 480;
        const float MaxGap = .24f;
        const int Confirmations = 3;

        readonly string host;
        readonly int port;
        readonly PoseTransport transport = new();
        readonly PoseRetryPolicy retry = new();
        readonly string session = Guid.NewGuid().ToString("N");
        Pending operation;
        ParkActor target;
        Transform anchor;
        int generation, confirmations;
        long lastFrame;
        float lastSample = float.NegativeInfinity, anchorCapture;
        public string Status { get; private set; } = "Idle";

        [Serializable]
        internal sealed class Request
        {
            public string schema = "pet-head-v1", session, instance_id;
            public long frame_id, timestamp;
            public int width = PairWidth, height = PairHeight;
            public float x1, y1, x2, y2, disparity, focal, baseline;
            public int left_x, left_w, right_x, right_w, top, crop_h;
        }

        [Serializable]
        internal sealed class Reply
        {
            public string schema, session, instance_id;
            public long frame_id, timestamp;
            public bool verified;
            public float x, y, disparity;
        }

        sealed class Pending
        {
            public Task<string> reply;
            public Request request;
            public int generation;
            public Pose head, camera;
            public Vector4 optics;
            public float captureTime;
        }

        public PetStereoHeadClient(string host, int port) { this.host = host; this.port = port; }

        public void Offer(ParkActor actor, long frame, long timestamp, byte[] rgba, byte[] left, byte[] right, Vector4 box,
            float disparity, Vector4 optics, float baseline, Pose head, Pose camera, float captureTime, float limit)
        {
            var now = Time.realtimeSinceStartup;
            if (!actor || rgba?.Length != PairWidth*PairHeight*4 || left?.Length != PairWidth*PairHeight ||
                right?.Length != PairWidth*PairHeight || !float.IsFinite(disparity) || disparity < 2 || disparity >= 120)
            {
                Reset();
                return;
            }
            if (target != actor) { Reset(); target = actor; }
            var remaining = Mathf.FloorToInt((limit - (now - captureTime))*1000);
            if (operation != null || remaining <= 0 || !retry.CanAttempt(now))
            {
                Status=$"Skipped {frame}: {(operation!=null ? "busy" : remaining<=0 ? "aged" : "backoff")}";
                return;
            }
            var request = new Request {
                session=session, frame_id=frame, timestamp=timestamp, instance_id=actor.instanceId,
                x1=box.x, y1=box.y, x2=box.z, y2=box.w, disparity=disparity, focal=optics.x, baseline=baseline
            };
            float radius=Mathf.Max(box.z-box.x,box.w-box.y)*.625f+8f;
            float x=(box.x+box.z)*.5f, y=(box.y+box.w)*.5f;
            request.left_x=Mathf.Clamp(Mathf.FloorToInt(x-radius),0,PairWidth);
            request.left_w=Mathf.Clamp(Mathf.CeilToInt(x+radius),0,PairWidth)-request.left_x;
            request.right_x=Mathf.Clamp(Mathf.FloorToInt(x-disparity-radius),0,PairWidth);
            request.right_w=Mathf.Clamp(Mathf.CeilToInt(x-disparity+radius),0,PairWidth)-request.right_x;
            request.top=Mathf.Clamp(Mathf.FloorToInt(y-radius),0,PairHeight);
            request.crop_h=Mathf.Clamp(Mathf.CeilToInt(y+radius),0,PairHeight)-request.top;
            if (request.left_w < 12 || request.right_w < 12 || request.crop_h < 12) { Reset(); return; }
            byte[] header = Encoding.UTF8.GetBytes(JsonUtility.ToJson(request));
            operation = new Pending {
                request=request, head=head, camera=camera, optics=optics, captureTime=captureTime, generation=generation,
                reply=Task.Run(() => transport.ExchangeAsync(host,port,header,
                    Crop(rgba,request.left_x,request.top,request.left_w,request.crop_h,4),
                    Crop(right,request.right_x,request.top,request.right_w,request.crop_h,1),remaining,compress:false))
            };
            Status=$"Sent {frame}; budget {remaining} ms";
        }

        static byte[] Crop(byte[] image, int x, int y, int width, int height, int channels)
        {
            int rowBytes=width*channels;
            var crop=new byte[rowBytes*height];
            for (int row=0; row<height; row++)
                Buffer.BlockCopy(image,((y+row)*PairWidth+x)*channels,crop,row*rowBytes,rowBytes);
            return crop;
        }

        internal static bool Matches(Request request, Reply reply) => reply != null && reply.schema == request.schema &&
            reply.session == request.session && reply.instance_id == request.instance_id &&
            reply.frame_id == request.frame_id && reply.timestamp == request.timestamp;

        public void Poll(ParkController controller, bool captureAvailable, float limit)
        {
            float now = Time.realtimeSinceStartup;
            if (!captureAvailable || (target && controller.PhysicalPetForHead(target.instanceId) != target) ||
                (anchor && (!target || !target.tracked || target.headAnchor != anchor))) Reset();
            else if (anchor && now - anchorCapture > limit) ClearAnchor();
            if (operation == null || !operation.reply.IsCompleted) return;
            var completed = operation;
            operation = null;
            if (completed.reply.IsFaulted || completed.reply.IsCanceled)
            {
                _ = completed.reply.Exception;
                if (completed.generation == generation)
                {
                    // A frame deadline can surface as a socket fault on some transports.
                    // Reserve reconnect backoff for faults that arrived before that deadline.
                    if (!completed.reply.IsCanceled && now-completed.captureTime < limit-.02f) retry.Fail(now);
                    Reset();
                    Status=$"Failed {completed.request.frame_id}: {completed.reply.Exception?.GetBaseException().Message ?? "deadline"}";
                }
                return;
            }
            if (completed.generation != generation || !target) return;
            Reply reply;
            try { reply = JsonUtility.FromJson<Reply>(completed.reply.Result); }
            catch (ArgumentException) { Reset(); return; }
            var request = completed.request;
            float age = now - completed.captureTime;
            if (!Matches(request,reply) || !RecognitionReplyPolicy.AcceptSpatial(age,limit,true,captureAvailable) ||
                request.frame_id <= lastFrame || !reply.verified || !InRange(reply,request))
            {
                Reset();
                Status=$"Rejected {request.frame_id}; age {age*1000:F1} ms, verified {reply?.verified}, bound {Matches(request,reply)}";
                return;
            }
            retry.Reset();
            confirmations = completed.captureTime > lastSample && completed.captureTime-lastSample <= MaxGap ?
                Math.Min(confirmations+1,Confirmations) : 1;
            lastFrame = request.frame_id;
            lastSample = completed.captureTime;
            Status=$"Accepted {lastFrame}; age {age*1000:F1} ms, confirmations {confirmations}";
            if (confirmations < Confirmations || !target.tracked) return;
            var k = completed.optics;
            float depth = request.focal*request.baseline/reply.disparity;
            var optical = new Vector3((reply.x-k.z)*depth/k.x,(reply.y-k.w)*depth/k.y,depth);
            Vector3 world = completed.head.position + completed.head.rotation*StereoFusionGeometry.OpticalToHead(optical,completed.camera);
            if (!float.IsFinite(world.x) || !float.IsFinite(world.y) || !float.IsFinite(world.z)) { Reset(); return; }
            if (!anchor) anchor = new GameObject("Pet stereo head").transform;
            anchor.position = world;
            anchorCapture = completed.captureTime;
            target.headAnchor = anchor;
            target.reliableHeadPose = true;
        }

        static bool InRange(Reply reply, Request request)
        {
            if (!(reply.x >= 0 && reply.x < PairWidth && reply.y >= 0 && reply.y < PairHeight &&
                  reply.disparity>=2 && reply.disparity<120)) return false;
            float scale = request.focal*request.baseline;
            float depth = scale/reply.disparity;
            return depth>=.2f && depth<=3.7f && Mathf.Abs(depth-scale/request.disparity)<=.70f;
        }

        void ClearAnchor()
        {
            if (!anchor) return;
            if (target && target.headAnchor == anchor) { target.headAnchor = null; target.reliableHeadPose = false; }
            UnityEngine.Object.Destroy(anchor.gameObject);
            anchor = null;
        }

        public void Reset()
        {
            ClearAnchor();
            generation++;
            target = null;
            confirmations = 0;
            lastFrame = 0;
            lastSample = float.NegativeInfinity;
            Status = "Idle";
        }

        public void Dispose() { Reset();transport.Dispose(); }
    }
}
