#if UNITY_EDITOR
using UnityEngine;

namespace MagicMR
{
    // Play Mode QA only: explicit Camera.Render can advance editor clocks without running actor updates.
    [DefaultExecutionOrder(20000)]
    public sealed class ParkAnimationFrameProbe : MonoBehaviour
    {
        public float SimulatedSeconds {get;private set;}
        public int Frames {get;private set;}
        void LateUpdate(){SimulatedSeconds+=Time.unscaledDeltaTime;Frames++;}
    }
}
#endif
