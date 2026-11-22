using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
#if XR_HANDS_1_1_OR_NEWER
using UnityEngine.XR.Hands;
#endif
namespace MagicMR
{
    /// <summary>Additional scene entry, does not dispatch any lighter effects.</summary>
    public sealed class ParkLobby : MonoBehaviour
    {
        TextMesh label;
        float dwell;
#if XR_HANDS_1_1_OR_NEWER
        XRHandSubsystem hands;
#endif
        void Start() { if(!Camera.main)return; label=new GameObject("Six scene entry").AddComponent<TextMesh>(); label.transform.SetParent(Camera.main.transform,false); label.transform.localPosition=new Vector3(-.23f,-.18f,.45f); label.characterSize=.009f; label.fontSize=40; label.anchor=TextAnchor.MiddleCenter; label.text="PARK DEMOS\nHold pinch here"; }
        void Update() {
#if UNITY_EDITOR || UNITY_STANDALONE
            if(ParkDesktopInput.KeyDown(KeyCode.N))SceneManager.LoadScene("ParkTree");
#endif
#if XR_HANDS_1_1_OR_NEWER
            if(!label)return;
            hands=ParkHandTracking.Current;
            bool valid=false;
            if(hands!=null && hands.rightHand.isTracked && hands.rightHand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out var i) && hands.rightHand.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out var t)) {
                var root=Camera.main.transform.parent; var world=root?root.TransformPoint(i.position):i.position;
                valid=Vector3.Distance(world,label.transform.position)<.08f && Vector3.Distance(i.position,t.position)<.035f;
            }
            dwell=valid?dwell+Time.unscaledDeltaTime:0;
            if(dwell>.6f)SceneManager.LoadScene("ParkTree");
#endif
        }
        void OnDisable() { dwell=0; if(label)Destroy(label.gameObject); }
    }
}
