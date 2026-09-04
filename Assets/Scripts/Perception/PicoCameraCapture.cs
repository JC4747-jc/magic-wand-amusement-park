using System;
using System.Collections;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif
using Unity.XR.PXR;

namespace Perception
{
    /// <summary>
    /// PXR_CameraImage → Texture2D. Used by Preview UI and PicoYoloFrameSender (YOLO input).
    /// Does not run YOLO itself; does not change DetectionManager event semantics.
    /// </summary>
    public class PicoCameraCapture : MonoBehaviour
    {
        public enum CaptureStatus
        {
            Idle,
            RequestingPermission,
            Initializing,
            Running,
            Failed,
            Stopped
        }

        [SerializeField]
        float m_TargetFps = 5f;

        [SerializeField]
        float m_InitDelaySeconds = 1.5f;

        [SerializeField]
        bool m_AutoStart = true;

        Texture2D m_Texture;
        byte[] m_PackedBuffer;
        XrCameraIdPICO m_CameraId;
        long m_LastCaptureTime;
        float m_NextAcquireTime;
        float m_FpsWindowStart;
        int m_FramesInWindow;
        float m_MeasuredFps;
        bool m_DeviceCreated;
        bool m_SessionCreated;
        bool m_CaptureStarted;
        bool m_HardFailed;
        int m_ConsecutiveAcquireErrors;
        bool m_LoggedAcquireSuccess;
        string m_StatusDetail = "Idle";

        public CaptureStatus Status { get; private set; } = CaptureStatus.Idle;
        public Texture2D PreviewTexture => m_Texture;
        public XrCameraIdPICO ActiveCameraId => m_CameraId;
        public int Width { get; private set; }
        public int Height { get; private set; }
        public string PixelFormatLabel { get; private set; } = "RGBA_8888";
        public float MeasuredFps => m_MeasuredFps;
        public string StatusDetail => m_StatusDetail;
        public bool HasFrame { get; private set; }

        /// <summary>PICO captureTime from last successful AcquireCameraImage (device clock).</summary>
        public long LastCaptureTimestamp => m_LastCaptureTime;

        public event Action<Texture2D> FrameUpdated;
        public event Action StatusChanged;

        void Start()
        {
            if (m_AutoStart)
                StartCoroutine(BootstrapRoutine());
        }

        void OnDestroy()
        {
            ShutdownCamera("OnDestroy");
            if (m_Texture != null)
            {
                Destroy(m_Texture);
                m_Texture = null;
            }
        }

        void OnApplicationPause(bool pause)
        {
            if (pause)
            {
                if (m_CaptureStarted)
                {
                    var end = PXR_CameraImage.EndCameraCapture(m_CameraId);
                    Log($"EndCameraCapture on pause → {end}");
                    m_CaptureStarted = false;
                }
            }
            else if (Status == CaptureStatus.Running && m_SessionCreated && !m_CaptureStarted)
            {
                var begin = PXR_CameraImage.BeginCameraCapture(m_CameraId);
                Log($"BeginCameraCapture on resume → {begin}");
                m_CaptureStarted = begin == PxrResult.SUCCESS;
            }
        }

        void Update()
        {
            if (Status != CaptureStatus.Running || m_HardFailed || !m_CaptureStarted)
                return;

            if (Time.unscaledTime < m_NextAcquireTime)
                return;

            float interval = m_TargetFps <= 0.1f ? 0.2f : 1f / m_TargetFps;
            m_NextAcquireTime = Time.unscaledTime + interval;
            TryAcquireOneFrame();
        }

        IEnumerator BootstrapRoutine()
        {
#if !UNITY_ANDROID || UNITY_EDITOR
            SetStatus(CaptureStatus.Failed, "PXR_CameraImage preview is Android/PICO device only (Editor skipped).");
            FailException("Platform", XrCameraIdPICO.XR_CAMERA_ID_RGB_LEFT_PICO,
                "Not running on Android device (Editor Play skipped).");
            yield break;
#else
            Log("Initializing");
            SetStatus(CaptureStatus.RequestingPermission, "Requesting CAMERA permission...");
            yield return RequestCameraPermissionRoutine();

            if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
            {
                FailException("Permission", XrCameraIdPICO.XR_CAMERA_ID_RGB_LEFT_PICO,
                    "android.permission.CAMERA not granted");
                yield break;
            }

            Log("CAMERA permission granted (Permission.Camera)");
            SetStatus(CaptureStatus.Initializing, $"Waiting {m_InitDelaySeconds:0.0}s for XR session...");
            if (m_InitDelaySeconds > 0f)
                yield return new WaitForSecondsRealtime(m_InitDelaySeconds);

            yield return InitializeCameraRoutine();
#endif
        }

#if UNITY_ANDROID
        IEnumerator RequestCameraPermissionRoutine()
        {
            if (Permission.HasUserAuthorizedPermission(Permission.Camera))
                yield break;

            Log("Requesting android.permission.CAMERA ...");
            var callbacks = new PermissionCallbacks();
            bool done = false;
            callbacks.PermissionGranted += _ => done = true;
            callbacks.PermissionDenied += _ => done = true;
            callbacks.PermissionDeniedAndDontAskAgain += _ => done = true;
            Permission.RequestUserPermission(Permission.Camera, callbacks);

            float timeout = Time.unscaledTime + 30f;
            while (!done && Time.unscaledTime < timeout)
                yield return null;
        }
#endif

        IEnumerator InitializeCameraRoutine()
        {
            SetStatus(CaptureStatus.Initializing, "GetAvailableCameras...");
            Log("Initializing");

            var availableResult = PXR_CameraImage.GetAvailableCameras(out XrCameraIdPICO[] cameras);
            if (availableResult != PxrResult.SUCCESS)
            {
                Fail("GetAvailableCameras", availableResult, XrCameraIdPICO.XR_CAMERA_ID_RGB_LEFT_PICO);
                yield break;
            }

            var sb = new StringBuilder();
            sb.Append("[PicoCamera] Available cameras:");
            if (cameras == null || cameras.Length == 0)
                sb.Append(" (none reported)");
            else
            {
                foreach (var id in cameras)
                    sb.Append(' ').Append(id).Append('(').Append((int)id).Append(')');
            }

            Debug.Log(sb.ToString());

            XrCameraIdPICO[] preferOrder =
            {
                XrCameraIdPICO.XR_CAMERA_ID_RGB_LEFT_PICO,
                XrCameraIdPICO.XR_CAMERA_ID_RGB_RIGHT_PICO
            };

            // Prefer listed cameras in LEFT→RIGHT order; if list empty, try both anyway.
            bool anyListed = cameras != null && cameras.Length > 0;
            foreach (var candidate in preferOrder)
            {
                if (anyListed)
                {
                    bool listed = false;
                    foreach (var id in cameras)
                    {
                        if (id == candidate)
                        {
                            listed = true;
                            break;
                        }
                    }

                    if (!listed)
                    {
                        Log($"Skipping {candidate} (not in available list).");
                        continue;
                    }
                }

                Log($"Trying camera: {candidate}");
                bool ok = false;
                yield return StartCameraRoutine(candidate, r => ok = r);
                if (ok)
                    yield break;

                ShutdownCamera($"failed candidate {candidate}");
            }

            if (!anyListed)
            {
                // already tried both in loop above when list empty
            }
            else
            {
                // Listed cameras failed — still try the other eye as last resort.
                foreach (var candidate in preferOrder)
                {
                    Log($"Last-resort trying camera: {candidate}");
                    bool ok = false;
                    yield return StartCameraRoutine(candidate, r => ok = r);
                    if (ok)
                        yield break;
                    ShutdownCamera($"failed last-resort {candidate}");
                }
            }

            FailException("Initialize", XrCameraIdPICO.XR_CAMERA_ID_RGB_LEFT_PICO,
                "PXR_CameraImage could not open LEFT or RIGHT camera. See prior [ERROR] logs.");
        }

        IEnumerator StartCameraRoutine(XrCameraIdPICO cameraId, Action<bool> done)
        {
            m_CameraId = cameraId;
            Log($"Selected camera: {cameraId} ({(int)cameraId})");

            Task<PxrResult> deviceTask = PXR_CameraImage.CreateCameraDeviceAsync(cameraId, CancellationToken.None);
            while (!deviceTask.IsCompleted)
                yield return null;

            if (deviceTask.IsFaulted)
            {
                FailException("CreateCameraDeviceAsync", cameraId,
                    deviceTask.Exception?.GetBaseException().Message ?? "faulted");
                done(false);
                yield break;
            }

            PxrResult createDevice = deviceTask.Result;
            if (createDevice != PxrResult.SUCCESS)
            {
                Fail("CreateCameraDeviceAsync", createDevice, cameraId);
                done(false);
                yield break;
            }

            m_DeviceCreated = true;
            Log("Camera device created");

            if (!SelectSessionParams(cameraId, out int width, out int height, out var fps, out var format, out var transfer, out var model))
            {
                done(false);
                yield break;
            }

            Task<PxrResult> sessionTask = PXR_CameraImage.CreateCameraCaptureSessionAsync(
                cameraId, width, height, fps, format, transfer, model, CancellationToken.None);
            while (!sessionTask.IsCompleted)
                yield return null;

            if (sessionTask.IsFaulted)
            {
                FailException("CreateCameraCaptureSessionAsync", cameraId,
                    sessionTask.Exception?.GetBaseException().Message ?? "faulted");
                done(false);
                yield break;
            }

            PxrResult createSession = sessionTask.Result;
            if (createSession != PxrResult.SUCCESS)
            {
                Fail("CreateCameraCaptureSessionAsync", createSession, cameraId,
                    $"width={width} height={height} fps={fps} format={format} transfer={transfer} model={model}");
                done(false);
                yield break;
            }

            m_SessionCreated = true;
            Width = width;
            Height = height;
            PixelFormatLabel = format.ToString();
            Log(
                $"Capture session created | CameraId={cameraId} | Width={width} | Height={height} | " +
                $"PixelFormat={format} | FpsEnum={fps} | Transfer={transfer} | Model={model}");

            var begin = PXR_CameraImage.BeginCameraCapture(cameraId);
            if (begin != PxrResult.SUCCESS)
            {
                Fail("BeginCameraCapture", begin, cameraId);
                done(false);
                yield break;
            }

            m_CaptureStarted = true;
            m_LastCaptureTime = 0;
            m_FpsWindowStart = Time.unscaledTime;
            m_FramesInWindow = 0;
            m_LoggedAcquireSuccess = false;
            EnsureTexture(width, height);
            SetStatus(CaptureStatus.Running, $"Running {cameraId} {width}x{height}");
            Log($"Begin capture SUCCESS | CameraId={cameraId}");
            done(true);
        }

        bool SelectSessionParams(
            XrCameraIdPICO cameraId,
            out int width,
            out int height,
            out XrCameraImageFpsPICO fps,
            out XrCameraImageFormatPICO format,
            out XrCameraDataTransferTypePICO transfer,
            out XrCameraModelPICO model)
        {
            width = 1280;
            height = 720;
            fps = XrCameraImageFpsPICO.XR_CAMERA_IMAGE_FPS_30_PICO;
            format = XrCameraImageFormatPICO.XR_CAMERA_IMAGE_FORMAT_RGBA_8888_PICO;
            transfer = XrCameraDataTransferTypePICO.XR_CAMERA_DATA_TRANSFER_TYPE_RAW_BUFFER_PICO;
            model = XrCameraModelPICO.XR_CAMERA_MODEL_PINHOLE_PICO;

            var resResult = PXR_CameraImage.GetCameraImageResolutionCapability(cameraId, out PxrExtent2Di[] resolutions);
            if (resResult == PxrResult.SUCCESS && resolutions != null && resolutions.Length > 0)
            {
                width = resolutions[0].width;
                height = resolutions[0].height;
                for (int i = 0; i < resolutions.Length; i++)
                {
                    int w = resolutions[i].width;
                    int h = resolutions[i].height;
                    if (w * h <= 1280 * 720)
                    {
                        width = w;
                        height = h;
                        break;
                    }
                }

                Log($"Resolution capability: chose {width}x{height} from {resolutions.Length} options.");
            }
            else
            {
                Log($"GetCameraImageResolutionCapability → {resResult}; using default {width}x{height}.");
            }

            var fpsResult = PXR_CameraImage.GetCameraImageFpsCapability(cameraId, out XrCameraImageFpsPICO[] fpsList);
            if (fpsResult == PxrResult.SUCCESS && fpsList != null && fpsList.Length > 0)
                fps = fpsList[0];

            var formatResult = PXR_CameraImage.GetCameraImageFormatCapability(cameraId, out XrCameraImageFormatPICO[] formats);
            if (formatResult == PxrResult.SUCCESS && formats != null && formats.Length > 0)
            {
                format = formats[0];
                for (int i = 0; i < formats.Length; i++)
                {
                    if (formats[i] == XrCameraImageFormatPICO.XR_CAMERA_IMAGE_FORMAT_RGBA_8888_PICO)
                    {
                        format = formats[i];
                        break;
                    }
                }
            }

            var transferResult = PXR_CameraImage.GetCameraDataTransferTypeCapability(cameraId, out XrCameraDataTransferTypePICO[] transfers);
            if (transferResult == PxrResult.SUCCESS && transfers != null && transfers.Length > 0)
                transfer = transfers[0];

            var modelResult = PXR_CameraImage.GetCameraCameraModelCapability(cameraId, out XrCameraModelPICO[] models);
            if (modelResult == PxrResult.SUCCESS && models != null && models.Length > 0)
                model = models[0];

            return true;
        }

        void TryAcquireOneFrame()
        {
            var acquire = PXR_CameraImage.AcquireCameraImage(m_CameraId, m_LastCaptureTime, out ulong imageId, out long captureTime);
            if (acquire != PxrResult.SUCCESS)
            {
                if (IsHardAcquireError(acquire))
                {
                    m_ConsecutiveAcquireErrors++;
                    if (m_ConsecutiveAcquireErrors == 1 || m_ConsecutiveAcquireErrors % 25 == 0)
                    {
                        LogPxrError("AcquireCameraImage", acquire, m_CameraId,
                            $"consecutive={m_ConsecutiveAcquireErrors}");
                    }

                    if (m_ConsecutiveAcquireErrors >= 50)
                    {
                        m_HardFailed = true;
                        Fail("AcquireCameraImage", acquire, m_CameraId,
                            $"consecutive={m_ConsecutiveAcquireErrors} (giving up)");
                    }
                }

                return;
            }

            m_ConsecutiveAcquireErrors = 0;

            var dataResult = PXR_CameraImage.GetCameraImageData(m_CameraId, imageId, out XrCameraImageDataRawBuffer raw);
            if (dataResult != PxrResult.SUCCESS)
            {
                LogPxrError("GetCameraImageData", dataResult, m_CameraId, $"imageId={imageId}");
                PXR_CameraImage.ReleaseCameraImage(m_CameraId, imageId);
                return;
            }

            try
            {
                if (raw.buffer == IntPtr.Zero || raw.bufferSize == 0 || raw.width == 0 || raw.height == 0)
                {
                    LogPxrError("GetCameraImageData", PxrResult.ERROR_SIZE_INSUFFICIENT, m_CameraId,
                        $"Empty buffer width={raw.width} height={raw.height} bufferSize={raw.bufferSize} " +
                        $"ptr={(raw.buffer == IntPtr.Zero ? "null" : "set")}");
                    return;
                }

                int w = (int)raw.width;
                int h = (int)raw.height;
                int bpp = raw.bytesPerPixel > 0 ? (int)raw.bytesPerPixel : 4;
                int packedStride = w * bpp;
                int packedSize = packedStride * h;

                EnsureTexture(w, h);
                // Texture2D.LoadRawTextureData(byte[]) requires exact length.
                EnsureExactBuffer(ref m_PackedBuffer, packedSize);

                if (raw.stride > 0 && raw.stride != (uint)packedStride)
                {
                    for (int y = 0; y < h; y++)
                    {
                        IntPtr row = IntPtr.Add(raw.buffer, y * (int)raw.stride);
                        Marshal.Copy(row, m_PackedBuffer, y * packedStride, packedStride);
                    }
                }
                else
                {
                    Marshal.Copy(raw.buffer, m_PackedBuffer, 0, packedSize);
                }

                m_Texture.LoadRawTextureData(m_PackedBuffer);
                m_Texture.Apply(false, false);
                Width = w;
                Height = h;
                PixelFormatLabel = $"RGBA_8888 bpp={bpp}";
                m_LastCaptureTime = captureTime;
                HasFrame = true;
                UpdateFpsCounter();
                LogOnceAcquireSuccess(w, h, bpp, (int)raw.stride);
                FrameUpdated?.Invoke(m_Texture);
            }
            finally
            {
                var release = PXR_CameraImage.ReleaseCameraImage(m_CameraId, imageId);
                if (release != PxrResult.SUCCESS)
                    LogPxrError("ReleaseCameraImage", release, m_CameraId, $"imageId={imageId}");
            }
        }

        static bool IsHardAcquireError(PxrResult result)
        {
            return result != PxrResult.TIMEOUT_EXPIRED
                   && result != PxrResult.EVENT_UNAVAILABLE
                   && result != PxrResult.FRAME_DISCARDED;
        }

        void LogOnceAcquireSuccess(int w, int h, int bpp, int stride)
        {
            if (m_LoggedAcquireSuccess)
                return;
            m_LoggedAcquireSuccess = true;
            Log("AcquireCameraImage SUCCESS");
            Log("GetCameraImageData SUCCESS");
            Log(
                $"Frame received | CameraId={m_CameraId} | Width={w} | Height={h} | " +
                $"PixelFormat=RGBA_8888 | bpp={bpp} | stride={stride} | TargetFps={m_TargetFps}");
            Log(
                "Orientation audit: raw buffer copied top→bottom rows into Texture2D without " +
                "rotation/mirror correction. Origin for YOLO pixel coords = OpenCV imdecode " +
                "(top-left). Phase 1 does not apply Intrinsics/Extrinsics.");
        }

        void EnsureTexture(int width, int height)
        {
            if (m_Texture != null && m_Texture.width == width && m_Texture.height == height)
                return;

            if (m_Texture != null)
                Destroy(m_Texture);

            m_Texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            m_Texture.wrapMode = TextureWrapMode.Clamp;
            m_Texture.filterMode = FilterMode.Bilinear;
            Width = width;
            Height = height;
        }

        static void EnsureExactBuffer(ref byte[] buffer, int size)
        {
            if (buffer == null || buffer.Length != size)
                buffer = new byte[size];
        }

        void UpdateFpsCounter()
        {
            m_FramesInWindow++;
            float elapsed = Time.unscaledTime - m_FpsWindowStart;
            if (elapsed >= 1f)
            {
                m_MeasuredFps = m_FramesInWindow / elapsed;
                m_FramesInWindow = 0;
                m_FpsWindowStart = Time.unscaledTime;
                SetStatus(CaptureStatus.Running,
                    $"{m_CameraId} {Width}x{Height} FPS={m_MeasuredFps:0.0}");
                Log(
                    $"FPS={m_MeasuredFps:0.0} | CameraId={m_CameraId} | Width={Width} | Height={Height} | " +
                    $"PixelFormat={PixelFormatLabel}");
            }
        }

        void ShutdownCamera(string reason)
        {
            Log($"ShutdownCamera ({reason}) device={m_DeviceCreated} session={m_SessionCreated} capturing={m_CaptureStarted}");

            if (m_CaptureStarted)
            {
                var end = PXR_CameraImage.EndCameraCapture(m_CameraId);
                Log($"EndCameraCapture → {end}");
                m_CaptureStarted = false;
            }

            if (m_SessionCreated)
            {
                var destroySession = PXR_CameraImage.DestroyCameraCaptureSession(m_CameraId);
                Log($"DestroyCameraCaptureSession → {destroySession}");
                m_SessionCreated = false;
            }

            if (m_DeviceCreated)
            {
                var destroyDevice = PXR_CameraImage.DestroyCameraDevice(m_CameraId);
                Log($"DestroyCameraDevice → {destroyDevice}");
                m_DeviceCreated = false;
            }
        }

        void Fail(string stage, PxrResult result, XrCameraIdPICO cameraId, string extra = null)
        {
            LogPxrError(stage, result, cameraId, extra);
            SetStatus(CaptureStatus.Failed, $"{stage} → {result}");
        }

        void FailException(string stage, XrCameraIdPICO cameraId, string message)
        {
            Debug.LogError(
                $"[PicoCamera][ERROR]\nStage={stage}\nResult=EXCEPTION\nCameraId={cameraId} ({(int)cameraId})\nExtra={message}");
            SetStatus(CaptureStatus.Failed, $"{stage} EXCEPTION");
        }

        static void LogPxrError(string stage, PxrResult result, XrCameraIdPICO cameraId, string extra = null)
        {
            string detail =
                $"[PicoCamera][ERROR]\nStage={stage}\nResult={result} ({(int)result})\nCameraId={cameraId} ({(int)cameraId})";
            if (!string.IsNullOrEmpty(extra))
                detail += $"\nExtra={extra}";
            Debug.LogError(detail);
        }

        void SetStatus(CaptureStatus status, string detail)
        {
            Status = status;
            m_StatusDetail = detail ?? string.Empty;
            StatusChanged?.Invoke();
        }

        static void Log(string message) => Debug.Log($"[PicoCamera] {message}");
    }
}
