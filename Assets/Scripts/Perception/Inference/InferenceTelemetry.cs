namespace Perception
{
    /// <summary>
    /// Minimal telemetry. Fields that cannot be measured yet stay at sentinel defaults
    /// (e.g. -1 for unknown latencies). Do not fabricate PC inference time on device.
    /// </summary>
    public sealed class InferenceTelemetry
    {
        public bool IsConnected;
        public InferenceConnectionState ConnectionState;

        public float CameraFps;
        public float SendFps;
        public float InferenceFps;
        public float NetworkReceiveFps;

        /// <summary>Unity submit → detection received (ms). -1 if unknown.</summary>
        public float RoundTripLatencyMs = -1f;

        /// <summary>Measured on PC and returned in future JSON extension. -1 until server reports it.</summary>
        public float LastInferenceLatencyMs = -1f;

        public int DroppedFrames;
        public int PendingFrames;

        public long LastSubmittedFrameId;
        public long LastDetectionFrameId;

        public string LastError;

        public InferenceTelemetry Clone()
        {
            return (InferenceTelemetry)MemberwiseClone();
        }
    }
}
