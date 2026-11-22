using UnityEngine;

namespace MagicMR {
    public enum ParkHoldPhase { None, Started, Holding, Ready, Ended }
    // Shared hold lifecycle; callers consume Ready after mapping it to a scene action.
    public sealed class ParkStableHold {
        readonly ParkStaticGestureLatch latch=new ParkStaticGestureLatch();
        public bool Active=>latch.Active;
        public bool Consumed=>latch.Performed;
        public float Began=>latch.HoldStartTime;
        Vector3 origin;
        float lastTime=-1;
        public ParkHoldPhase Step(bool held,Vector3 position,float time,float duration,float drift,float maximumGap=.25f) {
            bool valid=float.IsFinite(time) && float.IsFinite(position.x) && float.IsFinite(position.y) && float.IsFinite(position.z) &&
                float.IsFinite(duration) && duration>0 && float.IsFinite(drift) && drift>0 && float.IsFinite(maximumGap) && maximumGap>0;
            if(!held || !valid || (Active && (time<lastTime || time-lastTime>maximumGap))) {
                bool ended=Active;Reset();return ended?ParkHoldPhase.Ended:ParkHoldPhase.None;
            }
            lastTime=time;
            if(!Active) {origin=position;return latch.Step(true,time,Mathf.Max(.1f,duration));}
            if(Consumed)return ParkHoldPhase.Holding;
            if(Vector3.Distance(position,origin)>Mathf.Max(.005f,drift)) {latch.Reset();latch.Step(true,time,duration);origin=position;}
            return latch.Step(true,time,Mathf.Max(.1f,duration));
        }
        public void Consume() {latch.Consume();}
        public void Reset() {latch.Reset();lastTime=-1;origin=default;}
    }
}
