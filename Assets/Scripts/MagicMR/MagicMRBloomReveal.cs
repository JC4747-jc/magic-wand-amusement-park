using UnityEngine;
namespace MagicMR
{
    /// <summary>Unfurls the blossom without changing the stem's real-object coverage.</summary>
    public sealed class MagicMRBloomReveal : MonoBehaviour
    {
        Vector3 fullScale;
        float began;
        void Awake() { fullScale = transform.localScale; began = Time.time; Sample(0); }
        void Update() { Sample(Time.time - began); }
        public void Sample(float elapsed)
        {
            float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / .7f));
            transform.localScale = Vector3.Scale(fullScale, new Vector3(Mathf.Lerp(.22f, 1, t), Mathf.Lerp(.65f, 1, t), Mathf.Lerp(.22f, 1, t)));
            if (elapsed >= .7f) enabled = false;
        }
    }
}
