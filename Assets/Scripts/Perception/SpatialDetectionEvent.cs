using System;
using UnityEngine;

namespace Perception
{
    /// <summary>
    /// Domain event: a detection projected into Unity world space.
    /// No GameObjects — pose data only.
    /// </summary>
    public readonly struct SpatialDetectionEvent
    {
        public readonly string className;
        public readonly float confidence;
        public readonly Vector3 worldPosition;
        public readonly Vector3 worldDirection;
        public readonly long timestamp;

        public SpatialDetectionEvent(
            string className,
            float confidence,
            Vector3 worldPosition,
            Vector3 worldDirection,
            long timestamp)
        {
            this.className = className;
            this.confidence = confidence;
            this.worldPosition = worldPosition;
            this.worldDirection = worldDirection;
            this.timestamp = timestamp;
        }
    }
}
