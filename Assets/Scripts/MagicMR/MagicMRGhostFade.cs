using UnityEngine;
namespace MagicMR
{
    public sealed class MagicMRGhostFade : MonoBehaviour
    {
        Renderer[] renderers;
        MaterialPropertyBlock properties;
        float began, duration;
        public void Initialize(float lifetime)
        {
            properties = new MaterialPropertyBlock();
            began = Time.time; duration = Mathf.Max(.01f, lifetime);
            renderers = GetComponentsInChildren<Renderer>();
            Update();
        }
        void Update()
        {
            float t = (Time.time - began) / duration;
            if (t >= 1) { Destroy(gameObject); return; }
            properties.SetFloat("_Softness", 0);
            properties.SetColor("_BaseColor", new Color(.3f, .72f, 1, .22f * (1-t)*(1-t)));
            if (renderers != null) foreach (var renderer in renderers) renderer.SetPropertyBlock(properties);
        }
    }
}
