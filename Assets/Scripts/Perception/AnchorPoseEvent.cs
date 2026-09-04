using System;
using UnityEngine;

namespace Perception
{
    /// <summary>
    /// Raised when AnchorManager updates a class-bound Unity anchor pose.
    /// </summary>
    public readonly struct AnchorPoseEvent
    {
        public readonly string className;
        public readonly float confidence;
        public readonly Vector3 worldPosition;
        public readonly Transform anchor;
        public readonly long timestamp;

        public AnchorPoseEvent(
            string className,
            float confidence,
            Vector3 worldPosition,
            Transform anchor,
            long timestamp)
        {
            this.className = className;
            this.confidence = confidence;
            this.worldPosition = worldPosition;
            this.anchor = anchor;
            this.timestamp = timestamp;
        }
    }
}
