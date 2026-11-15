using UnityEngine;
using UnityEngine.XR;

namespace Perception
{
    // Remains enabled when the optional client is disabled.
    public sealed class PoseControl : MonoBehaviour
    {
        public F2GPoseClient client;
        bool previous;
        void Start()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unity.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var intent = activity.Call<AndroidJavaObject>("getIntent"))
            {
                // Explicit lab-only transport exercise; production defaults never treat recording as pose inference.
                client.allowDiagnosticServer = intent.Call<bool>("getBooleanExtra", "pose_diagnostic", false);
                client.SetPoseEnabled(intent.Call<bool>("getBooleanExtra", "pose_enabled", true));
            }
#endif
        }
        void Update()
        {
            bool pressed = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand)
                .TryGetFeatureValue(CommonUsages.secondaryButton, out bool down) && down;
            if (pressed && !previous && client != null)
            {
                client.SetPoseEnabled(!client.enabled);
                Debug.Log("[PoseLink] user enabled=" + client.enabled);
            }
            previous = pressed;
        }
    }
}
