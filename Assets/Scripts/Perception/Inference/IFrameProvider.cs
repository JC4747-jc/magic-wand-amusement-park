using System;
using UnityEngine;

namespace Perception
{
    /// <summary>
    /// Phase-2 design sketch: one camera frame snapshot for inference.
    /// Texture ownership remains with PicoCameraCapture — consumers must not Destroy it.
    /// Encode/network copies happen inside IInferenceClient implementations.
    /// </summary>
    public readonly struct CameraFrame
    {
        public readonly uint FrameId;
        public readonly long TimestampMs;
        public readonly int Width;
        public readonly int Height;
        public readonly Texture2D Texture;

        public CameraFrame(uint frameId, long timestampMs, int width, int height, Texture2D texture)
        {
            FrameId = frameId;
            TimestampMs = timestampMs;
            Width = width;
            Height = height;
            Texture = texture;
        }

        public bool IsValid => Texture != null && Width > 0 && Height > 0;
    }

    /// <summary>
    /// Thin frame source. Prefer adapting PicoCameraCapture.FrameUpdated — do not rewrite Capture.
    /// </summary>
    public interface IFrameProvider
    {
        event Action<CameraFrame> FrameAvailable;

        bool HasFrame { get; }
        CameraFrame LatestFrame { get; }
    }
}
