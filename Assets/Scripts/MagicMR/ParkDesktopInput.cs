using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MagicMR
{
    /// <summary>Desktop preview controls respect the configured input backend.</summary>
    public static class ParkDesktopInput
    {
        public static bool PointerCapturedByUi {get;set;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOwnership() {PointerCapturedByUi=false;}
        public static bool KeyDown(KeyCode code)
        {
#if ENABLE_INPUT_SYSTEM
            if(Keyboard.current==null)return false;
            Key key;
            switch(code) {
                case KeyCode.Alpha1:key=Key.Digit1;break;case KeyCode.Alpha2:key=Key.Digit2;break;
                case KeyCode.Alpha3:key=Key.Digit3;break;case KeyCode.Alpha4:key=Key.Digit4;break;
                case KeyCode.B:key=Key.B;break;case KeyCode.F:key=Key.F;break;case KeyCode.O:key=Key.O;break;
                case KeyCode.UpArrow:key=Key.UpArrow;break;case KeyCode.DownArrow:key=Key.DownArrow;break;
                case KeyCode.Z:key=Key.Z;break;case KeyCode.R:key=Key.R;break;case KeyCode.G:key=Key.G;break;
                case KeyCode.T:key=Key.T;break;case KeyCode.N:key=Key.N;break;
                default:return false;
            }
            return Keyboard.current[key].wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(code);
#else
            return false;
#endif
        }
        public static bool ClickDown {
            get {
#if ENABLE_INPUT_SYSTEM
                return Mouse.current!=null && Mouse.current.leftButton.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
                return Input.GetMouseButtonDown(0);
#else
                return false;
#endif
            }
        }
        public static Vector3 Pointer {
            get {
#if ENABLE_INPUT_SYSTEM
                return Mouse.current!=null?(Vector3)Mouse.current.position.ReadValue():Vector3.zero;
#elif ENABLE_LEGACY_INPUT_MANAGER
                return Input.mousePosition;
#else
                return Vector3.zero;
#endif
            }
        }
    }
}
