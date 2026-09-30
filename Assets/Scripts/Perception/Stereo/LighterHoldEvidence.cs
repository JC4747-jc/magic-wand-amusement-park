using UnityEngine;

namespace Perception
{
    // Hand shape proposes a grip. Independent, capture-time object motion grants ownership.
    public sealed class LighterHoldEvidence
    {
        public bool Confirmed { get; private set; }
        public string Status { get; private set; } = "NO_GRIP";
        public Vector3 ReleasedPosition { get; private set; }
        Vector3 origin, stationaryObject, departureHand;
        float began, lastCapture=-100, firstEvidence;
        int evidence;
        public void Reset() { Confirmed=false;Status="NO_GRIP";evidence=0;lastCapture=-100; }
        public void Begin(Vector3 target,float now)
        { Reset();origin=target;began=now;Status="VERIFY_PICKUP"; }
        public void ConfirmContact() { Confirmed=true;evidence=0;Status="CONTACT_HELD"; }
        // True only when independent vision proves that the hand has left the object.
        public bool Observe(Vector3 visual,Vector3 predicted,float captured,float now,bool reliable)
        {
            if(!reliable||!float.IsFinite(visual.sqrMagnitude)||!float.IsFinite(predicted.sqrMagnitude)||
                captured<began||captured<=lastCapture||now-captured<0||now-captured>.35f)
            { evidence=0;return false; }
            if(captured-lastCapture>.5f)evidence=0;
            lastCapture=captured;
            if(!Confirmed)
            {
                Vector3 objectMove=visual-origin,handMove=predicted-origin;
                bool together=objectMove.magnitude>=.018f&&handMove.magnitude>=.018f&&
                    Vector3.Distance(objectMove,handMove)<.025f&&Vector3.Dot(objectMove.normalized,handMove.normalized)>.7f;
                if(!together){evidence=0;Status="VERIFY_PICKUP";return false;}
                if(evidence++==0)firstEvidence=captured;
                if(evidence>=2&&captured-firstEvidence>=.10f)
                {Confirmed=true;evidence=0;Status="OBJECT_HELD";}
                return false;
            }
            float gap=Vector3.Distance(visual,predicted);
            if(gap<.03f){evidence=0;Status="OBJECT_HELD";return false;}
            if(evidence==0||Vector3.Distance(visual,stationaryObject)>.015f)
            {stationaryObject=visual;departureHand=predicted;firstEvidence=captured;evidence=1;}
            else {stationaryObject=Vector3.Lerp(stationaryObject,visual,.3f);evidence++;}
            Status="VERIFY_RELEASE";
            if(evidence>=3&&captured-firstEvidence>=.18f&&
                (Vector3.Distance(predicted,departureHand)>.018f||gap>.06f))
            {
                ReleasedPosition=stationaryObject;Confirmed=false;Status="OBJECT_LEFT_BEHIND";evidence=0;return true;
            }
            return false;
        }
    }
}
