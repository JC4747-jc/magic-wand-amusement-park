// Adapted from PICO Developer SimpleStaticHandGesture.cs, MIT License, Copyright (c) 2023 PICO Developer.
// Source commit a30ba657553fb485b072664b0af874e9cefbd048; full license in ThirdPartyNotices/PICO-InteractionSample/License.md.
namespace MagicMR {
    // Receives an already evaluated shared hand condition; it does not acquire hands or create another recognizer.
    public sealed class ParkStaticGestureLatch {
        bool wasDetected,performedTriggered;
        public bool Active=>wasDetected;
        public bool Performed=>performedTriggered;
        public float HoldStartTime {get;private set;}=-1;
        public ParkHoldPhase Step(bool detected,float now,float minimumHold) {
            if(!detected) {bool ended=wasDetected;Reset();return ended?ParkHoldPhase.Ended:ParkHoldPhase.None;}
            if(!wasDetected) {wasDetected=true;HoldStartTime=now;return ParkHoldPhase.Started;}
            if(!performedTriggered && now-HoldStartTime>=minimumHold) {
                performedTriggered=true;return ParkHoldPhase.Ready;
            }
            return ParkHoldPhase.Holding;
        }
        public void Consume() {if(wasDetected)performedTriggered=true;}
        public void Reset() {wasDetected=performedTriggered=false;HoldStartTime=-1;}
    }
}
