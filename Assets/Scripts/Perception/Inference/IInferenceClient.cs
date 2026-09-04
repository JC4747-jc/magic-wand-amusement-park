using System;

namespace Perception
{
    public enum InferenceConnectionState
    {
        Disconnected = 0,
        Connecting = 1,
        Connected = 2,
        Faulted = 3
    }

    /// <summary>
    /// Hides TCP / PICF / JSON / IP / Port from DetectionManager and upper layers.
    /// Detection payload type = existing Perception.DetectionFrame (absolute JPEG pixel coords).
    /// Do NOT introduce a duplicate DetectionResult type unless DetectionFrame is insufficient.
    /// </summary>
    public interface IInferenceClient
    {
        bool IsConnected { get; }
        InferenceConnectionState ConnectionState { get; }
        InferenceTelemetry Telemetry { get; }

        /// <summary>Raised on the Unity main thread only.</summary>
        event Action<DetectionFrame> DetectionReceived;

        /// <summary>Raised on the Unity main thread when telemetry snapshot changes meaningfully.</summary>
        event Action<InferenceTelemetry> TelemetryUpdated;

        void Connect();
        void Disconnect();

        /// <summary>
        /// Offers a camera frame for inference.
        /// Implementations should apply Latest-Frame-Wins: if busy, keep only the newest pending frame.
        /// Returns false if not connected or frame rejected.
        /// Must not block waiting for YOLO.
        /// </summary>
        bool TrySubmitFrame(CameraFrame frame);
    }
}
