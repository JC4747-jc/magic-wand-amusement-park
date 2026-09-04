using System;

namespace Perception
{
    /// <summary>
    /// Domain event published when a detection is accepted.
    /// Downstream systems subscribe to this — not to TCP or JSON.
    /// </summary>
    public readonly struct ObjectDetectionEvent
    {
        public readonly string className;
        public readonly float confidence;
        public readonly float centerX;
        public readonly float centerY;
        public readonly float x1;
        public readonly float y1;
        public readonly float x2;
        public readonly float y2;
        public readonly long timestamp;

        public float Width => Math.Max(0f, x2 - x1);
        public float Height => Math.Max(0f, y2 - y1);
        public float Area => Width * Height;
        public bool HasBBox => x2 > x1 && y2 > y1;

        public ObjectDetectionEvent(
            string className,
            float confidence,
            float centerX,
            float centerY,
            long timestamp,
            float x1 = 0f,
            float y1 = 0f,
            float x2 = 0f,
            float y2 = 0f)
        {
            this.className = className;
            this.confidence = confidence;
            this.centerX = centerX;
            this.centerY = centerY;
            this.timestamp = timestamp;
            this.x1 = x1;
            this.y1 = y1;
            this.x2 = x2;
            this.y2 = y2;
        }
    }
}
