using UnityEngine;

namespace MagicMR
{
    // Perception implements this contract; the gesture assembly does not depend on it.
    public abstract class TabletopInteractionBase : MonoBehaviour
    {
        public abstract bool InteractionReady { get; }
        public abstract bool TargetLocated { get; }
        public abstract bool CanSummon { get; }
        public abstract bool TrySummon();
        public abstract Vector3 InteractionPosition { get; }
        public virtual Vector3 VisualPosition => InteractionPosition;
        public virtual bool IsHeld => false;
        public virtual bool HasGripCandidate => IsHeld;
        public virtual void UpdateGripContact(bool valid,Vector3 thumb,Vector3 index,float now) { }
        public virtual void UpdateHoldingHand(bool valid, bool closed, Vector3 palm, float nearestDistance, float now) { }
        public virtual void UpdatePinchHand(bool palmValid, Pose palm, bool tipsValid, bool closed, Vector3 pinch, float now, bool beforeRender) { }
        public abstract void ResetInteraction();
    }
}
