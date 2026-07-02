using System.Collections;
using UnityEngine;

namespace MagicMR
{
    /// <summary>
    /// Minimal, isolated video-see-through test: just a camera + this script.
    /// Mirrors PICO's official minimal sample exactly (camera alpha=0 +
    /// PXR_Boundary.EnableSeeThroughManual) with none of MagicMR's extra
    /// URP/XR Origin/gesture setup, to isolate whether VST itself works on
    /// this device/SDK combo at all.
    /// </summary>
    public class VstTestRunner : MonoBehaviour
    {
        [SerializeField]
        Camera m_Camera;

        void Awake()
        {
            if (m_Camera == null)
                m_Camera = GetComponent<Camera>();

            if (m_Camera == null)
            {
                Debug.LogError("[VstTest] No camera found.");
                return;
            }

            m_Camera.clearFlags = CameraClearFlags.SolidColor;
            m_Camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            m_Camera.allowHDR = false;

            StartCoroutine(EnableLoop());
        }

        IEnumerator EnableLoop()
        {
            yield return new WaitForSeconds(0.2f);
            for (var i = 0; i < 20; i++)
            {
                PicoVideoSeeThrough.Enable(true);
                yield return new WaitForSeconds(0.5f);
            }
        }

        void OnApplicationPause(bool pause)
        {
            if (!pause)
                PicoVideoSeeThrough.Enable(true);
        }
    }
}
