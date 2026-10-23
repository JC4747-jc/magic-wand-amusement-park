using System.Collections.Generic;
using Perception;

namespace MagicMR {
    // Recognition only: this policy never creates an anchor or claims depth readiness.
    public sealed class DiscoverySelection {
        public float minimumConfidence=.65f,stableSeconds=.8f,cooldownSeconds=2f,exitWaitSeconds=2f;
        public int minimumFrames=3;
        public bool automatic=true;
        public string LockedId {get;private set;}
        public string LockedScene {get;private set;}
        public readonly List<DetectionMessage> Candidates=new List<DetectionMessage>();
        string pendingId;float pendingSince,lastSeen,nextSwitch;int confirmations;
        long lastFrame=-1;
        readonly Dictionary<string,List<DetectionMessage>> sources=new Dictionary<string,List<DetectionMessage>>();
        readonly Dictionary<string,float> sourceTimes=new Dictionary<string,float>();
        readonly Dictionary<string,bool> sourceAutomatic=new Dictionary<string,bool>();
        public float candidateLifetime=3f;
        public void Tick(float now,bool busy) {
            Candidates.Clear();
            foreach(var pair in sources)if(now-sourceTimes[pair.Key]<=candidateLifetime)Candidates.AddRange(pair.Value);
            if(!Candidates.Exists(d=>d.instance_id==pendingId)) {pendingId=null;confirmations=0;}
            if(LockedId!=null && !busy && now-lastSeen>=exitWaitSeconds)Return(now);
        }
        public string Update(DetectionFrameMessage frame,float now,bool busy) {
            if(frame==null || frame.scene!="discovery" || frame.status!="OK" || frame.frame_id<=lastFrame)return null;
            lastFrame=frame.frame_id;
            string source=frame.weights??"default";var current=new List<DetectionMessage>();
            foreach(var d in frame.detections??System.Array.Empty<DetectionMessage>())
                if(ValidCandidate(d,frame.width,frame.height) && d.confidence>=minimumConfidence)current.Add(d);
            sources[source]=current;sourceTimes[source]=now;
            sourceAutomatic[source]=AllowsAutomaticRouting(frame.model_validation);
            Candidates.Clear();foreach(var pair in sources)if(now-sourceTimes[pair.Key]<=candidateLifetime)Candidates.AddRange(pair.Value);
            if(LockedId!=null) {
                foreach(var d in current)if(d.instance_id==LockedId && d.target_scene==LockedScene) {lastSeen=now;return null;}
                if(!busy && now-lastSeen>=exitWaitSeconds)Return(now);
                return null;
            }
            if(busy || !automatic || now<nextSwitch || Candidates.Count!=1) {pendingId=null;confirmations=0;return null;}
            var candidate=Candidates[0];
            foreach(var pair in sources)if(pair.Value.Exists(d=>d.instance_id==candidate.instance_id) && !sourceAutomatic[pair.Key]) {pendingId=null;confirmations=0;return null;}
            if(pendingId!=candidate.instance_id) {pendingId=candidate.instance_id;pendingSince=now;confirmations=1;return null;}
            if(!current.Exists(d=>d.instance_id==candidate.instance_id))return null;
            confirmations++;
            if(confirmations<minimumFrames || now-pendingSince<stableSeconds)return null;
            return Choose(candidate.instance_id,now,false);
        }
        public string Choose(string id,float now,bool busy) {
            Tick(now,busy);
            if(busy || now<nextSwitch)return null;
            foreach(var d in Candidates)if(d.instance_id==id) {
                LockedId=id;LockedScene=d.target_scene;lastSeen=now;nextSwitch=now+cooldownSeconds;
                pendingId=null;confirmations=0;return LockedScene;
            }
            return null;
        }
        public static bool AllowsAutomaticRouting(string validation) {
            // Legacy lighter and explicit simulation streams have no marker.
            if(string.IsNullOrEmpty(validation))return true;
            string marker=validation.ToUpperInvariant();
            return !marker.Contains("REJECTED") && !marker.Contains("PENDING") &&
                   !marker.Contains("PILOT") && !marker.Contains("RESEARCH") &&
                   !marker.Contains("EXPERIMENTAL");
        }
        static string CanonicalClass(string name) {
            string value=(name??"").Trim().ToLowerInvariant().Replace("_","").Replace(" ","");
            return value=="sculpture"?"statue":value;
        }
        static string SceneForClass(string name) {
            switch(CanonicalClass(name)) {
                case "lighter":return "lighter";
                case "tree":return "tree";
                case "cockroach":case "spider":return "bugs";
                case "statue":case "sculpture":case "bronzebeast":return "statue";
                case "pottedplant":return "plant";
                case "cat":case "dog":return "pet";
                default:return null;
            }
        }
        public static bool ValidCandidate(DetectionMessage d,int width,int height) {
            if(d==null || string.IsNullOrEmpty(d.instance_id) || width<=0 || height<=0)return false;
            if(d.target_scene!="lighter" && d.target_scene!="tree" && d.target_scene!="bugs" &&
               d.target_scene!="statue" && d.target_scene!="plant" && d.target_scene!="pet")return false;
            string label=string.IsNullOrWhiteSpace(d.class_name)?d.@class:d.class_name;
            if(SceneForClass(label)!=d.target_scene)return false;
            if(!string.IsNullOrWhiteSpace(d.class_name) && !string.IsNullOrWhiteSpace(d.@class) && CanonicalClass(d.@class)!=CanonicalClass(d.class_name))return false;
            return float.IsFinite(d.confidence) && d.confidence>=0 && d.confidence<=1 &&
                   float.IsFinite(d.x1) && float.IsFinite(d.y1) && float.IsFinite(d.x2) && float.IsFinite(d.y2) &&
                   d.x1>=0 && d.y1>=0 && d.x1<d.x2 && d.y1<d.y2 && d.x2<=width && d.y2<=height;
        }
        public void Return(float now) {LockedId=null;LockedScene=null;pendingId=null;confirmations=0;Candidates.Clear();sources.Clear();sourceTimes.Clear();sourceAutomatic.Clear();nextSwitch=now+cooldownSeconds;}
        // A new input stream may restart its frame counter. Ordinary Reset keeps ordering.
        public void ResetSession(float now) {Return(now);lastFrame=-1;}
    }
}
