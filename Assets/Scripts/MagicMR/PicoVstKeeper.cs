using UnityEngine;

namespace MagicMR
{
    /// <summary>
    /// Keeps VST alive: re-asserts camera alpha=0 and passthrough APIs
    /// for the first few seconds and whenever the app regains focus.
    /// </summary>
    public class PicoVstKeeper : MonoBehaviour
    {
        [SerializeField]
        float m_KeepAliveSeconds = 8f;

        float m_Until;

        void OnEnable()
        {
            m_Until = Time.unscaledTime + m_KeepAliveSeconds;
            PicoVideoSeeThrough.DisableOpaqueEnvironment();
            PicoVideoSeeThrough.ConfigureAllXrCameras();
            PicoVideoSeeThrough.ConfigurePxrManagerForVst();
            PicoVideoSeeThrough.Enable(true);
        }

        void Update()
        {
            if (Time.unscaledTime > m_Until)
                return;

            // Re-assert every ~0.5s while keep-alive window is open.
            if (Time.frameCount % 30 != 0)
                return;

            PicoVideoSeeThrough.DisableOpaqueEnvironment();
            PicoVideoSeeThrough.ConfigureAllXrCameras();
            PicoVideoSeeThrough.ConfigurePxrManagerForVst();
            PicoVideoSeeThrough.Enable(true);
        }

        void OnApplicationPause(bool pause)
        {
            if (!pause)
            {
                m_Until = Time.unscaledTime + m_KeepAliveSeconds;
                PicoVideoSeeThrough.OnApplicationResumed();
            }
        }

        void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus)
            {
                m_Until = Time.unscaledTime + m_KeepAliveSeconds;
                PicoVideoSeeThrough.OnApplicationResumed();
            }
        }
    }
}
