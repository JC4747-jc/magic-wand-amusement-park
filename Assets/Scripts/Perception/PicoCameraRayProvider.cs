using System;
using UnityEngine;
using Unity.XR.PXR;
using Unity.XR.CoreUtils;

namespace Perception
{
    /// <summary>
    /// Phase 2: Detection / YOLO pixel → PICO RGB camera ray in Unity world space.
    /// Uses only audited PXR_CameraImage Intrinsics/Extrinsics APIs (SDK 3.4.0).
    /// Does not perform depth / mesh / plane hits (see AnchorManager.UsePhysicsDepth for Phase 3).
    /// </summary>
    public class PicoCameraRayProvider : MonoBehaviour
    {
        [SerializeField]
        PicoCameraCapture m_Capture;

        [SerializeField]
        PicoYoloFrameSender m_Sender;

        [SerializeField]
        [Tooltip("Log first N successful rays, then stop (0 = never).")]
        int m_LogFirstSuccesses = 5;

        [SerializeField]
        [Tooltip("Image v increases downward; flip Y into Unity camera (+Y up).")]
        bool m_ImageYDownToUnityYUp = true;

        [SerializeField]
        [Tooltip("Optional override for XR Origin; auto-found if null.")]
        Transform m_TrackingRoot;

        int m_LoggedSuccesses;
        bool m_LoggedEditorFallback;
        bool m_LoggedIntrinsicsFail;

        public Ray LastWorldRay { get; private set; }
        public bool HasLastWorldRay { get; private set; }
        public Ray LastXrCameraRay { get; private set; }
        public bool HasLastXrCameraRay { get; private set; }
        public string LastStatus { get; private set; } = "Idle";

        void Awake()
        {
            if (m_Capture == null)
                m_Capture = FindFirstObjectByType<PicoCameraCapture>();
            if (m_Sender == null)
                m_Sender = FindFirstObjectByType<PicoYoloFrameSender>();
            if (m_TrackingRoot == null)
                ResolveTrackingRoot();
        }

        void ResolveTrackingRoot()
        {
            var origin = FindFirstObjectByType<XROrigin>();
            if (origin != null)
            {
                m_TrackingRoot = origin.Origin != null ? origin.Origin.transform : origin.transform;
                return;
            }

            var go = GameObject.Find("XR Origin (VR)") ?? GameObject.Find("XR Origin");
            if (go != null)
                m_TrackingRoot = go.transform;
        }

        /// <summary>
        /// Builds a Unity world-space ray from a detection pixel in the YOLO/JPEG frame.
        /// </summary>
        /// <param name="detectionPixelX">centerX / bbox in YOLO JPEG pixel space</param>
        /// <param name="detectionPixelY">centerY / bbox in YOLO JPEG pixel space (top-left origin)</param>
        /// <param name="detectionWidth">JPEG / DetectionFrame width</param>
        /// <param name="detectionHeight">JPEG / DetectionFrame height</param>
        public bool TryGetWorldRay(
            float detectionPixelX,
            float detectionPixelY,
            int detectionWidth,
            int detectionHeight,
            out Ray worldRay)
        {
            worldRay = default;
            HasLastWorldRay = false;
            HasLastXrCameraRay = false;

#if !UNITY_ANDROID || UNITY_EDITOR
            if (!m_LoggedEditorFallback)
            {
                m_LoggedEditorFallback = true;
                Debug.Log(
                    "[PicoCameraRay] PICO runtime unavailable in Editor; " +
                    "using FixedDistance fallback (caller).");
            }

            LastStatus = "EditorFallback";
            return false;
#else
            if (m_Capture == null || m_Capture.Status != PicoCameraCapture.CaptureStatus.Running)
            {
                LastStatus = "CaptureNotRunning";
                return false;
            }

            int nativeW = m_Capture.Width;
            int nativeH = m_Capture.Height;
            if (nativeW <= 0 || nativeH <= 0)
            {
                LastStatus = "InvalidNativeSize";
                return false;
            }

            if (detectionWidth <= 0 || detectionHeight <= 0)
            {
                detectionWidth = m_Sender != null && m_Sender.LastSentWidth > 0
                    ? m_Sender.LastSentWidth
                    : nativeW;
                detectionHeight = m_Sender != null && m_Sender.LastSentHeight > 0
                    ? m_Sender.LastSentHeight
                    : nativeH;
            }

            // YOLO JPEG may be uniformly downscaled (max long edge) and optionally V-flipped.
            MapDetectionPixelToNative(
                detectionPixelX,
                detectionPixelY,
                detectionWidth,
                detectionHeight,
                nativeW,
                nativeH,
                m_Sender != null && m_Sender.FlipVerticallyBeforeEncode,
                out float u,
                out float v);

            XrCameraIdPICO cameraId = m_Capture.ActiveCameraId;
            PxrResult iRet = PXR_CameraImage.GetCameraIntrinsics(cameraId, out XrCameraIntrinsics intrinsics);
            if (iRet != PxrResult.SUCCESS)
            {
                if (!m_LoggedIntrinsicsFail)
                {
                    m_LoggedIntrinsicsFail = true;
                    Debug.LogError(
                        $"[PicoCameraRay][ERROR]\nStage=GetCameraIntrinsics\nResult={iRet} ({(int)iRet})\nCameraId={cameraId}");
                }

                LastStatus = $"IntrinsicsFail:{iRet}";
                return false;
            }

            PxrResult eRet = PXR_CameraImage.GetCameraExtrinsics(cameraId, out XrCameraExtrinsics extrinsics);
            if (eRet != PxrResult.SUCCESS)
            {
                Debug.LogError(
                    $"[PicoCameraRay][ERROR]\nStage=GetCameraExtrinsics\nResult={eRet} ({(int)eRet})\nCameraId={cameraId}");
                LastStatus = $"ExtrinsicsFail:{eRet}";
                return false;
            }

            float fx = intrinsics.focalLength.X;
            float fy = intrinsics.focalLength.Y;
            float cx = intrinsics.principalPoint.X;
            float cy = intrinsics.principalPoint.Y;
            if (fx <= 1e-6f || fy <= 1e-6f)
            {
                LastStatus = "InvalidIntrinsics";
                Debug.LogError($"[PicoCameraRay] Invalid fx/fy fx={fx} fy={fy}");
                return false;
            }

            // Pinhole unprojection in image pixels (u right, v down from top-left).
            // After GetCameraExtrinsics, pose is already Unity-converted (SDK flips Z/W).
            // Camera local: +X right, +Y up, +Z forward (Unity) — image Y-down inverted when mapping.
            float xCam = (u - cx) / fx;
            float yCam = (v - cy) / fy;
            if (m_ImageYDownToUnityYUp)
                yCam = -yCam;
            float zCam = 1f;
            Vector3 dirCam = new Vector3(xCam, yCam, zCam).normalized;

            // Extrinsics: "pose of the camera in the tracking space" (camera → tracking).
            // Do NOT call ToVector3/ToQuat again — GetCameraExtrinsics already flipped Z/W.
            Vector3 camPosTracking = new Vector3(
                extrinsics.pose.Position.X,
                extrinsics.pose.Position.Y,
                extrinsics.pose.Position.Z);
            Quaternion camRotTracking = new Quaternion(
                extrinsics.pose.Orientation.X,
                extrinsics.pose.Orientation.Y,
                extrinsics.pose.Orientation.Z,
                extrinsics.pose.Orientation.W);

            Vector3 originTracking = camPosTracking;
            Vector3 dirTracking = (camRotTracking * dirCam).normalized;

            Vector3 originWorld = originTracking;
            Vector3 dirWorld = dirTracking;
            if (m_TrackingRoot != null)
            {
                originWorld = m_TrackingRoot.TransformPoint(originTracking);
                dirWorld = m_TrackingRoot.TransformDirection(dirTracking).normalized;
            }

            worldRay = new Ray(originWorld, dirWorld);
            LastWorldRay = worldRay;
            HasLastWorldRay = true;

            // Validation candidate: the RGB camera is rigidly attached to the HMD,
            // so the tracked XR camera supplies a reliable live world pose. Keep the
            // audited PICO-extrinsics ray as the primary result; AnchorManager may use
            // this candidate only when the primary ray misses all environment geometry.
            var xrOrigin = FindFirstObjectByType<XROrigin>();
            Camera xrCamera = xrOrigin != null ? xrOrigin.Camera : Camera.main;
            if (xrCamera != null)
            {
                Transform xrCameraTransform = xrCamera.transform;
                LastXrCameraRay = new Ray(
                    xrCameraTransform.position,
                    xrCameraTransform.TransformDirection(dirCam).normalized);
                HasLastXrCameraRay = true;
            }

            LastStatus = "Ok";

            if (m_LoggedSuccesses < m_LogFirstSuccesses)
            {
                m_LoggedSuccesses++;
                Debug.Log(
                    $"[PicoCameraRay]\n" +
                    $"CameraId={cameraId}\n" +
                    $"ImageSize={nativeW}x{nativeH} DetectionSize={detectionWidth}x{detectionHeight}\n" +
                    $"PixelDet=({detectionPixelX:F1},{detectionPixelY:F1}) → Native=({u:F1},{v:F1})\n" +
                    $"Intrinsics=(fx={fx:F2},fy={fy:F2},cx={cx:F2},cy={cy:F2})\n" +
                    $"CameraPosition={camPosTracking}\n" +
                    $"CameraDirection={dirTracking}\n" +
                    $"WorldRayOrigin={originWorld}\n" +
                    $"WorldRayDirection={dirWorld}\n" +
                    $"XrCameraRayOrigin={(HasLastXrCameraRay ? LastXrCameraRay.origin.ToString() : "unavailable")}\n" +
                    $"XrCameraRayDirection={(HasLastXrCameraRay ? LastXrCameraRay.direction.ToString() : "unavailable")}");
            }

            return true;
#endif
        }

        /// <summary>
        /// Maps YOLO/JPEG pixel → native PICO RGB pixel.
        /// Sender uses uniform long-edge scale (and optional vertical flip) only — no crop.
        /// </summary>
        public static void MapDetectionPixelToNative(
            float detX,
            float detY,
            int detW,
            int detH,
            int nativeW,
            int nativeH,
            bool flipVerticalInJpeg,
            out float nativeX,
            out float nativeY)
        {
            float x = detX;
            float y = detY;
            if (flipVerticalInJpeg && detH > 0)
                y = (detH - 1) - y;

            if (detW <= 0 || detH <= 0 || nativeW <= 0 || nativeH <= 0)
            {
                nativeX = x;
                nativeY = y;
                return;
            }

            float scaleX = nativeW / (float)detW;
            float scaleY = nativeH / (float)detH;
            nativeX = x * scaleX;
            nativeY = y * scaleY;
        }
    }
}
