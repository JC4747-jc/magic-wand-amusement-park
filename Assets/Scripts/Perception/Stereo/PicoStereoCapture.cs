using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Android;
using UnityEngine.UI;
using UnityEngine.XR.Management;
using Unity.XR.PXR;
using Debug = UnityEngine.Debug;

public sealed class PicoStereoCapture : MonoBehaviour
{
    const int Width = 640;
    const int Height = 480;
    const int PollDelayMs = 55;

    struct FrameCopy
    {
        public long captureTime;
        public byte[] rgba;
    }

    public Perception.StereoYoloLocator locator;

    static readonly XrCameraIdPICO Left = XrCameraIdPICO.XR_CAMERA_ID_RGB_LEFT_PICO;
    static readonly XrCameraIdPICO Right = XrCameraIdPICO.XR_CAMERA_ID_RGB_RIGHT_PICO;
    readonly CultureInfo invariant = CultureInfo.InvariantCulture;

    StreamWriter journal;
    XrCameraIntrinsics leftK;
    XrCameraExtrinsics leftE;
    float baselineMeters;
    string phase = "STARTING";
    string detail = "Waiting for PICO XR.";
    bool leftDevice;
    bool rightDevice;
    bool leftSession;
    bool rightSession;
    bool leftCapturing;
    bool rightCapturing;
    bool stopping;
    bool running, destroyed;
    long pairCount;
    long rejectedPairCount;
    long lastLeftTime;
    long lastRightTime;
    double lastTimestampDeltaMs;

    async void Start()
    {
        if (running || destroyed) return;
        running = true;
        stopping = false;
        try
        {
            if (Application.isEditor)
            {
                phase = "DEVICE_REQUIRED";
                detail = "Run this APK on PICO 4 Ultra.";
                return;
            }

            string id = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", invariant);
            journal?.Dispose();
            journal = new StreamWriter(Path.Combine(Application.persistentDataPath,
                "stereo-depth-" + id + ".log")) { AutoFlush = true };
            Trace("APP_START version=" + ("1.0 stereo YOLO") + " os=" + SystemInfo.operatingSystem +
                  " device=" + SystemInfo.deviceModel);

            await WaitForXr();
            await RequireCameraPermission();
            ValidateCameraCapabilities();
            await CreateStereoCameras();
            ReadCalibration();
            if (stopping) return;
            PXR_Manager.EnableVideoSeeThrough = true;

            Require(PXR_CameraImage.BeginCameraCapture(Left), "Begin left capture");
            leftCapturing = true;
            Require(PXR_CameraImage.BeginCameraCapture(Right), "Begin right capture");
            rightCapturing = true;
            phase = "SEARCHING_DEPTH";
            detail = "Aim the yellow crosshair at a textured object 0.4-3 m away.";
            Trace("CAPTURE_BEGIN center=" + Math.Round(leftK.principalPoint.X) + "," +
                  Math.Round(leftK.principalPoint.Y));

            while (!stopping)
            {
                await Task.Delay(PollDelayMs);
                if (stopping) break;
                PxrResult leftResult = TryAcquire(Left, lastLeftTime, out FrameCopy leftFrame);
                PxrResult rightResult = TryAcquire(Right, lastRightTime, out FrameCopy rightFrame);
                if (leftResult == PxrResult.SUCCESS) lastLeftTime = leftFrame.captureTime;
                if (rightResult == PxrResult.SUCCESS) lastRightTime = rightFrame.captureTime;
                if (leftResult != PxrResult.SUCCESS || rightResult != PxrResult.SUCCESS) continue;
                
                lastLeftTime = leftFrame.captureTime;
                lastRightTime = rightFrame.captureTime;
                lastTimestampDeltaMs = Math.Abs((double)leftFrame.captureTime - rightFrame.captureTime) / 1_000_000.0;
                if (lastTimestampDeltaMs > 5.0)
                {
                    rejectedPairCount++;
                    phase = "PAIR_NOT_SYNCHRONIZED";
                    detail = "Waiting for a synchronized left/right pair.";
                    locator?.Invalidate("PAIR_OUT_OF_SYNC");
                    continue;
                }

                pairCount++;
                locator?.AcceptPair(leftFrame.rgba, ToGray(leftFrame.rgba), ToGray(rightFrame.rgba), leftK, leftE, baselineMeters, leftFrame.captureTime);
            }
        }
        catch (Exception e)
        {
            phase = "STOPPED";
            detail = e.GetType().Name + ": " + e.Message;
            Trace("ERROR " + detail);
            locator?.Invalidate("STOPPED: " + detail);
            Debug.LogException(e);
        }
        finally
        {
            StopAndDestroyCameras();
            running = false;
        }
    }

    async void OnApplicationPause(bool paused)
    {
        if (paused)
        {
            // Permission dialogs before capture must not cancel camera setup.
            if (leftCapturing || rightCapturing) stopping = true;
            locator?.Invalidate("PAUSED");
        }
        else if (stopping)
        {
            while (running && !destroyed) await Task.Delay(100);
            if (!destroyed) { lastLeftTime = lastRightTime = 0; Start(); }
        }
    }

    async Task WaitForXr()
    {
        float deadline = Time.realtimeSinceStartup + 30f;
        while (XRGeneralSettings.Instance?.Manager?.activeLoader == null ||
               !XRGeneralSettings.Instance.Manager.isInitializationComplete)
        {
            if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("PICO XR loader initialization");
            if (stopping) throw new OperationCanceledException();
            await Task.Delay(100);
        }
        Trace("XR_READY loader=" + XRGeneralSettings.Instance.Manager.activeLoader.name);
    }

    async Task RequireCameraPermission()
    {
        if (Permission.HasUserAuthorizedPermission(Permission.Camera))
        {
            Trace("PERMISSION_OK " + Permission.Camera);
            return;
        }
        phase = "CAMERA_PERMISSION_REQUIRED";
        detail = "Allow Camera in the system dialog.";
        var completion = new TaskCompletionSource<bool>();
        var callbacks = new PermissionCallbacks();
        callbacks.PermissionGranted += _ => completion.TrySetResult(true);
        callbacks.PermissionDenied += _ => completion.TrySetResult(false);
        Permission.RequestUserPermission(Permission.Camera, callbacks);
        float deadline = Time.realtimeSinceStartup + 120f;
        while (!completion.Task.IsCompleted && !Permission.HasUserAuthorizedPermission(Permission.Camera))
        {
            if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("Camera permission");
            if (stopping) throw new OperationCanceledException();
            await Task.Delay(100);
        }
        if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
            throw new UnauthorizedAccessException("Camera permission was denied");
        Trace("PERMISSION_GRANTED " + Permission.Camera);
    }

    void ValidateCameraCapabilities()
    {
        phase = "CHECKING_STEREO_CAMERAS";
        Require(PXR_CameraImage.GetAvailableCameras(out XrCameraIdPICO[] cameras), "GetAvailableCameras");
        cameras ??= Array.Empty<XrCameraIdPICO>();
        if (!cameras.Contains(Left) || !cameras.Contains(Right))
            throw new NotSupportedException("RGB_LEFT and RGB_RIGHT are required");
        ValidateCapabilities(Left);
        ValidateCapabilities(Right);
        Trace("CAMERAS_OK values=" + string.Join(",", cameras.Select(CameraName)) +
              " config=640x480@30/RGBA8888/RAW/PINHOLE");
    }

    void ValidateCapabilities(XrCameraIdPICO id)
    {
        Require(PXR_CameraImage.GetCameraImageResolutionCapability(id, out PxrExtent2Di[] resolutions), CameraName(id) + " resolutions");
        Require(PXR_CameraImage.GetCameraImageFpsCapability(id, out XrCameraImageFpsPICO[] fps), CameraName(id) + " fps");
        Require(PXR_CameraImage.GetCameraImageFormatCapability(id, out XrCameraImageFormatPICO[] formats), CameraName(id) + " formats");
        Require(PXR_CameraImage.GetCameraDataTransferTypeCapability(id, out XrCameraDataTransferTypePICO[] transfers), CameraName(id) + " transfers");
        Require(PXR_CameraImage.GetCameraCameraModelCapability(id, out XrCameraModelPICO[] models), CameraName(id) + " models");
        if (!resolutions.Any(r => r.width == Width && r.height == Height) ||
            !fps.Contains(XrCameraImageFpsPICO.XR_CAMERA_IMAGE_FPS_30_PICO) ||
            !formats.Contains(XrCameraImageFormatPICO.XR_CAMERA_IMAGE_FORMAT_RGBA_8888_PICO) ||
            !transfers.Contains(XrCameraDataTransferTypePICO.XR_CAMERA_DATA_TRANSFER_TYPE_RAW_BUFFER_PICO) ||
            !models.Contains(XrCameraModelPICO.XR_CAMERA_MODEL_PINHOLE_PICO))
            throw new NotSupportedException(CameraName(id) + " lacks the required stereo configuration");
    }

    async Task CreateStereoCameras()
    {
        phase = "CREATING_STEREO_SESSIONS";
        Require(await PXR_CameraImage.CreateCameraDeviceAsync(Left), "Create left device");
        leftDevice = true;
        if (stopping) throw new OperationCanceledException();
        Require(await PXR_CameraImage.CreateCameraDeviceAsync(Right), "Create right device");
        rightDevice = true;
        if (stopping) throw new OperationCanceledException();
        Require(await PXR_CameraImage.CreateCameraCaptureSessionAsync(Left, Width, Height,
                XrCameraImageFpsPICO.XR_CAMERA_IMAGE_FPS_30_PICO,
                XrCameraImageFormatPICO.XR_CAMERA_IMAGE_FORMAT_RGBA_8888_PICO,
                XrCameraDataTransferTypePICO.XR_CAMERA_DATA_TRANSFER_TYPE_RAW_BUFFER_PICO,
                XrCameraModelPICO.XR_CAMERA_MODEL_PINHOLE_PICO), "Create left session");
        leftSession = true;
        if (stopping) throw new OperationCanceledException();
        Require(await PXR_CameraImage.CreateCameraCaptureSessionAsync(Right, Width, Height,
                XrCameraImageFpsPICO.XR_CAMERA_IMAGE_FPS_30_PICO,
                XrCameraImageFormatPICO.XR_CAMERA_IMAGE_FORMAT_RGBA_8888_PICO,
                XrCameraDataTransferTypePICO.XR_CAMERA_DATA_TRANSFER_TYPE_RAW_BUFFER_PICO,
                XrCameraModelPICO.XR_CAMERA_MODEL_PINHOLE_PICO), "Create right session");
        rightSession = true;
        if (stopping) throw new OperationCanceledException();
    }

    void ReadCalibration()
    {
        phase = "READING_CALIBRATION";
        Require(PXR_CameraImage.GetCameraIntrinsics(Left, out leftK), "Get left intrinsics");
        Require(PXR_CameraImage.GetCameraExtrinsics(Left, out leftE), "Get left extrinsics");
        Require(PXR_CameraImage.GetCameraIntrinsics(Right, out XrCameraIntrinsics rightK), "Get right intrinsics");
        Require(PXR_CameraImage.GetCameraExtrinsics(Right, out XrCameraExtrinsics rightE), "Get right extrinsics");
        baselineMeters = Distance(leftE.pose.Position, rightE.pose.Position);
        if (leftK.focalLength.X <= 0 || leftK.focalLength.Y <= 0 || baselineMeters < 0.01f)
            throw new InvalidDataException("Invalid camera calibration");
        Trace("CALIBRATION fx=" + leftK.focalLength.X.ToString("F4", invariant) +
              " fy=" + leftK.focalLength.Y.ToString("F4", invariant) +
              " cx=" + leftK.principalPoint.X.ToString("F4", invariant) +
              " cy=" + leftK.principalPoint.Y.ToString("F4", invariant) +
              " baseline_m=" + baselineMeters.ToString("F6", invariant) +
              " left_q=" + QuaternionText(leftE.pose.Orientation) +
              " right_q=" + QuaternionText(rightE.pose.Orientation));
    }

    PxrResult TryAcquire(XrCameraIdPICO id, long lastTime, out FrameCopy frame)
    {
        frame = default;
        PxrResult result = PXR_CameraImage.AcquireCameraImage(id, lastTime, out ulong imageId, out long captureTime);
        if (result != PxrResult.SUCCESS) return result;
        try
        {
            Require(PXR_CameraImage.GetCameraImageData(id, imageId, out XrCameraImageDataRawBuffer raw), CameraName(id) + " image data");
            if (raw.width != Width || raw.height != Height || raw.buffer == IntPtr.Zero || raw.bufferSize == 0)
                throw new InvalidDataException(CameraName(id) + " returned an invalid raw frame");
            byte[] bytes = new byte[(int)raw.bufferSize];
            Marshal.Copy(raw.buffer, bytes, 0, bytes.Length);
            frame.captureTime = captureTime;
            frame.rgba = RepackRows(bytes, (int)raw.stride);
            return result;
        }
        finally
        {
            PXR_CameraImage.ReleaseCameraImage(id, imageId);
        }
    }

    static byte[] RepackRows(byte[] source, int stride)
    {
        int rowBytes = Width * 4;
        int expected = rowBytes * Height;
        if (stride == rowBytes && source.Length == expected) return source;
        if (stride < rowBytes || source.Length < stride * Height)
            throw new InvalidDataException("Unexpected RGBA stride " + stride);
        var packed = new byte[expected];
        for (int row = 0; row < Height; row++)
            Buffer.BlockCopy(source, row * stride, packed, row * rowBytes, rowBytes);
        return packed;
    }

    static byte[] ToGray(byte[] rgba)
    {
        var gray = new byte[Width * Height];
        for (int pixel = 0, source = 0; pixel < gray.Length; pixel++, source += 4)
            gray[pixel] = (byte)((77 * rgba[source] + 150 * rgba[source + 1] + 29 * rgba[source + 2]) >> 8);
        return gray;
    }

    void Trace(string message)
    {
        string line = DateTime.UtcNow.ToString("O", invariant) + " " + message;
        Debug.Log("[StereoCapture] " + line);
        journal?.WriteLine(line);
    }

    static void Require(PxrResult result, string operation)
    {
        if (result != PxrResult.SUCCESS)
            throw new InvalidOperationException(operation + " returned " + result + " (" + (int)result + ")");
    }

    void StopAndDestroyCameras()
    {
        if (leftCapturing) { PXR_CameraImage.EndCameraCapture(Left); leftCapturing = false; }
        if (rightCapturing) { PXR_CameraImage.EndCameraCapture(Right); rightCapturing = false; }
        if (leftSession) { PXR_CameraImage.DestroyCameraCaptureSession(Left); leftSession = false; }
        if (rightSession) { PXR_CameraImage.DestroyCameraCaptureSession(Right); rightSession = false; }
        if (leftDevice) { PXR_CameraImage.DestroyCameraDevice(Left); leftDevice = false; }
        if (rightDevice) { PXR_CameraImage.DestroyCameraDevice(Right); rightDevice = false; }
    }

    static float Distance(XrVector3f a, XrVector3f b)
    {
        float x = a.X - b.X;
        float y = a.Y - b.Y;
        float z = a.Z - b.Z;
        return (float)Math.Sqrt(x * x + y * y + z * z);
    }

    string QuaternionText(XrQuaternionf value) => "(" + value.X.ToString("F4", invariant) + "," +
        value.Y.ToString("F4", invariant) + "," + value.Z.ToString("F4", invariant) + "," +
        value.W.ToString("F4", invariant) + ")";

    static string CameraName(XrCameraIdPICO value) => value == Left ? "RGB_LEFT" : value == Right ? "RGB_RIGHT" : ((int)value).ToString();

    void OnDestroy()
    {
        destroyed = true;
        stopping = true;
        StopAndDestroyCameras();
        Trace("APP_DESTROY pairs=" + pairCount);
        journal?.Dispose();
        journal = null;
    }
}



