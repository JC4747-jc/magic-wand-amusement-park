using System;
using System.Collections.Generic;

namespace Perception
{
    /// <summary>
    /// All detections belonging to one PICO Camera / YOLO frame.
    /// </summary>
    public readonly struct DetectionFrame
    {
        public readonly IReadOnlyList<ObjectDetectionEvent> detections;
        public readonly long timestamp;
        public readonly long frameId;
        public readonly int width;
        public readonly int height;
        public readonly string rawJson;

        public DetectionFrame(
            IReadOnlyList<ObjectDetectionEvent> detections,
            long timestamp,
            long frameId = 0,
            int width = 0,
            int height = 0,
            string rawJson = null)
        {
            this.detections = detections ?? Array.Empty<ObjectDetectionEvent>();
            this.timestamp = timestamp;
            this.frameId = frameId;
            this.width = width;
            this.height = height;
            this.rawJson = rawJson;
        }

        public int Count => detections.Count;
    }
}
