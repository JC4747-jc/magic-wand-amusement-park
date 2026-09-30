using System.Collections.Generic;
using UnityEngine;

namespace Perception
{
    // All times use realtimeSinceStartup, including camera capture timestamps.
    public sealed class PinchGripFollow
    {
        struct Sample { public float time; public Pose pose; }
        readonly List<Sample> history=new List<Sample>(120);
        public bool IsHolding { get; private set; }
        public bool UsingPalmFallback { get; private set; }
        public Vector3 Position { get; private set; }
        public int Corrections { get; private set; }
        public float HandGapMs { get; private set; }
        public float FilterLagMm => motion.LagMm;
        public float VisionResidualMm { get; private set; } = -1;
        public float VisionAgeMs { get; private set; } = -1;
        public string VisionStatus { get; private set; } = "NO_SAMPLE";
        public string ReleaseReason { get; private set; } = "NONE";
        public int Attachments { get; private set; }
        public int Fallbacks { get; private set; }
        readonly TrackingPositionFilter motion=new TrackingPositionFilter();
        Vector3 recoveryBias,lastPalm;
        float settleUntil;
        Vector3 offset,initialOffset,pinchInPalm,candidateOffset,rawPosition,visionCandidate;
        Pose current;
        float lastValid=-100,lastTips=-100,candidateAt=-1,openAt=-1,lastVision=-100;
        int candidates,visionCount;
        static bool Finite(Vector3 p)=>float.IsFinite(p.sqrMagnitude);
        static bool Valid(Pose p)=>Finite(p.position)&&float.IsFinite(p.rotation.x+p.rotation.y+p.rotation.z+p.rotation.w)&&Quaternion.Dot(p.rotation,p.rotation)>.5f&&Quaternion.Dot(p.rotation,p.rotation)<1.5f;
        public void Reset()
        {IsHolding=UsingPalmFallback=false;history.Clear();lastValid=lastTips=lastVision=-100;candidateAt=openAt=-1;candidates=visionCount=Corrections=Attachments=Fallbacks=0;
            VisionResidualMm=VisionAgeMs=-1;VisionStatus="NO_SAMPLE";ReleaseReason="NONE";settleUntil=0;recoveryBias=Vector3.zero;}
        public void Tick(float now)
        {if(now-lastValid>.35f || (IsHolding&&now-lastTips>.45f)) {
            if(IsHolding)ReleaseReason=now-lastValid>.35f?"HAND_LOST":"TIPS_TIMEOUT";
            IsHolding=UsingPalmFallback=false;candidateAt=-1;history.Clear();visionCount=0;
        }}
        public void ReleaseFromVision()
        { IsHolding=UsingPalmFallback=false;candidateAt=openAt=-1;history.Clear();visionCount=0;ReleaseReason="OBJECT_LEFT_BEHIND"; }
        public void Observe(bool palmValid,Pose palm,bool tipsValid,bool closed,Vector3 pinch,Vector3 target,float now)
        {
            Tick(now);
            if(!palmValid||!Valid(palm)||!Finite(target)){candidateAt=-1;return;}
            float dt=now-lastValid;
            if(IsHolding&&(Vector3.Distance(palm.position,lastPalm)>.12f+3f*Mathf.Clamp(dt,0,.1f)))
            {candidateAt=-1;return;}
            HandGapMs=dt<1?dt*1000:0;
            lastPalm=palm.position;
            bool wasFallback=UsingPalmFallback;
            if(tipsValid && (!Finite(pinch)||Vector3.Distance(pinch,palm.position)>.16f))tipsValid=false;
            if(tipsValid&&closed&&IsHolding&&dt<.1f && Vector3.Distance(pinch,palm.position+palm.rotation*pinchInPalm)>.045f)tipsValid=false;
            if(!tipsValid)
            {
                candidateAt=-1;
                if(openAt>=0)return;
                if(!IsHolding||now-lastTips>.45f)return;
                if(!UsingPalmFallback)Fallbacks++;
                pinch=palm.position+palm.rotation*pinchInPalm;
            }
            else
            {
                lastTips=now;
                if(!closed)
                {
                    candidateAt=-1;
                    if(openAt<0)openAt=now;
                    if(now-openAt>=.18f){IsHolding=UsingPalmFallback=false;history.Clear();visionCount=0;ReleaseReason="FINGERS_OPEN";}
                    lastValid=now;return;
                }
                openAt=-1;
                if(!IsHolding)pinchInPalm=Quaternion.Inverse(palm.rotation)*(pinch-palm.position);
                else pinch=palm.position+palm.rotation*pinchInPalm;
            }
            current=new Pose(pinch,palm.rotation);UsingPalmFallback=!tipsValid;lastValid=now;
            if(!IsHolding)
            {
                if(!tipsValid||Vector3.Distance(pinch,target)>.085f){candidateAt=-1;return;}
                Vector3 local=Quaternion.Inverse(palm.rotation)*(target-pinch);
                if(candidateAt<0||dt>.15f||Vector3.Distance(local,candidateOffset)>.02f)
                {candidateAt=now;candidateOffset=local;candidates=1;}
                else {candidateOffset=Vector3.Lerp(candidateOffset,local,1f/++candidates);}
                if(now-candidateAt<.15f||candidates<3)return;
                offset=initialOffset=candidateOffset;IsHolding=true;history.Clear();visionCount=Corrections=0;
                rawPosition=pinch+palm.rotation*offset;
                Position=target;motion.Reset(target);recoveryBias=Vector3.zero;settleUntil=now+.08f;
                Attachments++;ReleaseReason="NONE";
            }
            else
            {
                Vector3 next=pinch+palm.rotation*offset;
                if(wasFallback&&tipsValid){recoveryBias=rawPosition-next;settleUntil=now+.18f;visionCount=0;history.Clear();}
                recoveryBias*=Mathf.Exp(-Mathf.Clamp(dt,0,.05f)/.06f);
                next+=recoveryBias;
                Position=motion.Update(next,dt);
                rawPosition=next;
            }
            if(tipsValid){history.Add(new Sample{time=now,pose=current});if(history.Count>120)history.RemoveAt(0);}
        }
        public bool Preview(bool palmValid,Pose palm,bool tipsValid,bool closed,Vector3 pinch,float now,out Vector3 position)
        {
            position=Position;
            if(!IsHolding||!palmValid||!Valid(palm)||openAt>=0||now-lastValid>.1f||now<settleUntil)return false;
            if(!tipsValid){if(now-lastTips>.45f)return false;pinch=palm.position+palm.rotation*pinchInPalm;}
            else if(!closed||!Finite(pinch)||Vector3.Distance(pinch,palm.position+palm.rotation*pinchInPalm)>.045f)return false;
            pinch=palm.position+palm.rotation*pinchInPalm;
            Vector3 delta=pinch+palm.rotation*offset-rawPosition;
            if(delta.magnitude>.035f)return false;
            position=Position+delta*Mathf.Lerp(.15f,1f,Mathf.Clamp01(motion.Speed/.15f));return true;
        }
        bool TryPoseAt(float capturedAt,out Pose pose)
        {
            pose=default;
            for(int i=history.Count-1;i>0;i--)
            {
                var a=history[i-1];var b=history[i];
                if(capturedAt<a.time||capturedAt>b.time||b.time-a.time>.1f)continue;
                float t=(capturedAt-a.time)/Mathf.Max(.0001f,b.time-a.time);
                pose=new Pose(Vector3.Lerp(a.pose.position,b.pose.position,t),Quaternion.Slerp(a.pose.rotation,b.pose.rotation,t));return true;
            }
            return false;
        }
        public bool TryPredictAt(float capturedAt,out Vector3 predicted)
        {
            predicted=default;
            if(!IsHolding||!TryPoseAt(capturedAt,out var pose))return false;
            predicted=pose.position+pose.rotation*offset;return true;
        }
        public bool Correct(Vector3 visual,float capturedAt,float now,bool reliable,bool objectMatched=false)
        {
            VisionAgeMs=(now-capturedAt)*1000;VisionResidualMm=-1;
            if(!IsHolding||UsingPalmFallback||openAt>=0||now<settleUntil)
            {VisionStatus="HAND_TRANSITION";visionCount=0;return false;}
            if(!reliable||!Finite(visual)){VisionStatus="QUALITY_GATE";visionCount=0;return false;}
            if(now-capturedAt>.35f||now-capturedAt<0){VisionStatus="STALE_FRAME";visionCount=0;return false;}
            if(capturedAt<=lastVision){VisionStatus="DUPLICATE_FRAME";return false;}
            if(!TryPoseAt(capturedAt,out var pose)){VisionStatus="NO_TIME_MATCH";visionCount=0;return false;}
            Vector3 measured=Quaternion.Inverse(pose.rotation)*(visual-pose.position);
            VisionResidualMm=Vector3.Distance(measured,offset)*1000;
            if(Vector3.Distance(measured,offset)>(objectMatched?.08f:.04f)||Vector3.Distance(measured,initialOffset)>(objectMatched?.07f:.035f)){VisionStatus="OUTLIER";visionCount=0;return false;}
            if(now-lastVision>.5f||visionCount==0||Vector3.Distance(measured,visionCandidate)>.008f)
            {visionCandidate=measured;visionCount=1;}
            else {visionCandidate=Vector3.Lerp(visionCandidate,measured,.35f);visionCount++;}
            lastVision=capturedAt;
            if(visionCount<3){VisionStatus="CONFIRMING";return false;}
            offset+=Vector3.ClampMagnitude((visionCandidate-offset)*(objectMatched?.35f:.2f),objectMatched?.006f:.002f);
            Corrections++;VisionStatus="CORRECTED";return true;
        }
    }
}
