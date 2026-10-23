using UnityEngine;

namespace MagicMR {
    public enum ParkPinchPhase { None, Started, Holding, Confirmed, Ended, Cancelled }
    public sealed class ParkPinchLifecycle {
        bool held,mustRelease;
        float began,last=-1;
        public void Cancel() {held=false;mustRelease=true;last=-1;}
        public ParkPinchPhase Step(bool pinched,bool allowed,float time,float minimum,float maximum,float maximumGap) {
            if(!float.IsFinite(time) || !float.IsFinite(minimum) || !float.IsFinite(maximum) ||
                minimum<=0 || maximum<minimum || !float.IsFinite(maximumGap) || maximumGap<=0 ||
                (last>=0 && (time<last || time-last>maximumGap))) {Cancel();return ParkPinchPhase.Cancelled;}
            if(!allowed) {
                Cancel();
                // A valid release while UI owns the hand satisfies rearming,
                // but can never confirm the cancelled scene action.
                if(!pinched)mustRelease=false;
                return ParkPinchPhase.Cancelled;
            }
            last=time;
            if(mustRelease) {if(!pinched)mustRelease=false;return ParkPinchPhase.None;}
            if(pinched) {
                if(!held) {held=true;began=time;return ParkPinchPhase.Started;}
                return ParkPinchPhase.Holding;
            }
            if(!held)return ParkPinchPhase.None;
            held=false;float duration=time-began;
            return duration>=minimum && duration<=maximum?ParkPinchPhase.Confirmed:ParkPinchPhase.Ended;
        }
    }
}
