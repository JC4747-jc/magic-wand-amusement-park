using System.Collections.Generic;

namespace Perception
{
    public enum TargetLockState
    {
        NO_TARGET = 0,
        TARGET_ACQUIRE = 1,
        TARGET_LOCKED = 2,
        TARGET_LOST = 3
    }

    public enum TargetSelectionMode
    {
        HighestConfidence = 0,
        LargestArea = 1,
        ClosestToCenter = 2
    }

    public interface ITargetSelectionStrategy
    {
        bool TrySelect(IReadOnlyList<ObjectDetectionEvent> candidates, float frameWidth, float frameHeight, out ObjectDetectionEvent selected);
    }

    public interface ITargetMatchingStrategy
    {
        /// <summary>Returns best match index or -1. Also outputs match score.</summary>
        int FindBestMatch(
            ObjectDetectionEvent previous,
            IReadOnlyList<ObjectDetectionEvent> candidates,
            float frameWidth,
            float frameHeight,
            float alpha,
            float beta,
            float minScore,
            out float bestScore);
    }

    public sealed class HighestConfidenceSelection : ITargetSelectionStrategy
    {
        public bool TrySelect(IReadOnlyList<ObjectDetectionEvent> candidates, float frameWidth, float frameHeight, out ObjectDetectionEvent selected)
        {
            selected = default;
            if (candidates == null || candidates.Count == 0)
                return false;

            int best = 0;
            for (int i = 1; i < candidates.Count; i++)
            {
                if (candidates[i].confidence > candidates[best].confidence)
                    best = i;
            }

            selected = candidates[best];
            return true;
        }
    }

    public sealed class LargestAreaSelection : ITargetSelectionStrategy
    {
        public bool TrySelect(IReadOnlyList<ObjectDetectionEvent> candidates, float frameWidth, float frameHeight, out ObjectDetectionEvent selected)
        {
            selected = default;
            if (candidates == null || candidates.Count == 0)
                return false;

            int best = 0;
            for (int i = 1; i < candidates.Count; i++)
            {
                if (candidates[i].Area > candidates[best].Area)
                    best = i;
            }

            selected = candidates[best];
            return true;
        }
    }

    public sealed class ClosestToCenterSelection : ITargetSelectionStrategy
    {
        public bool TrySelect(IReadOnlyList<ObjectDetectionEvent> candidates, float frameWidth, float frameHeight, out ObjectDetectionEvent selected)
        {
            selected = default;
            if (candidates == null || candidates.Count == 0)
                return false;

            float cx = frameWidth * 0.5f;
            float cy = frameHeight * 0.5f;
            int best = 0;
            float bestDist = Dist2(candidates[0], cx, cy);
            for (int i = 1; i < candidates.Count; i++)
            {
                float d = Dist2(candidates[i], cx, cy);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = i;
                }
            }

            selected = candidates[best];
            return true;
        }

        static float Dist2(ObjectDetectionEvent e, float cx, float cy)
        {
            float dx = e.centerX - cx;
            float dy = e.centerY - cy;
            return dx * dx + dy * dy;
        }
    }

    /// <summary>matchScore = alpha * IoU + beta * centerSimilarity</summary>
    public sealed class IoUCenterMatching : ITargetMatchingStrategy
    {
        public int FindBestMatch(
            ObjectDetectionEvent previous,
            IReadOnlyList<ObjectDetectionEvent> candidates,
            float frameWidth,
            float frameHeight,
            float alpha,
            float beta,
            float minScore,
            out float bestScore)
        {
            bestScore = 0f;
            int bestIndex = -1;
            if (candidates == null || candidates.Count == 0)
                return -1;

            float diagonal = UnityEngine.Mathf.Sqrt(frameWidth * frameWidth + frameHeight * frameHeight);
            if (diagonal < 1f)
                diagonal = 1f;

            for (int i = 0; i < candidates.Count; i++)
            {
                ObjectDetectionEvent c = candidates[i];
                float iou = IoU(previous, c);
                float dx = c.centerX - previous.centerX;
                float dy = c.centerY - previous.centerY;
                float dist = UnityEngine.Mathf.Sqrt(dx * dx + dy * dy);
                float centerSim = 1f - UnityEngine.Mathf.Clamp01(dist / diagonal);
                float score = alpha * iou + beta * centerSim;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                }
            }

            if (bestIndex < 0 || bestScore < minScore)
                return -1;
            return bestIndex;
        }

        static float IoU(ObjectDetectionEvent a, ObjectDetectionEvent b)
        {
            float x1 = UnityEngine.Mathf.Max(a.x1, b.x1);
            float y1 = UnityEngine.Mathf.Max(a.y1, b.y1);
            float x2 = UnityEngine.Mathf.Min(a.x2, b.x2);
            float y2 = UnityEngine.Mathf.Min(a.y2, b.y2);
            float iw = UnityEngine.Mathf.Max(0f, x2 - x1);
            float ih = UnityEngine.Mathf.Max(0f, y2 - y1);
            float inter = iw * ih;
            float union = a.Area + b.Area - inter;
            if (union <= 1e-6f)
                return 0f;
            return inter / union;
        }
    }

    public readonly struct LockedTargetEvent
    {
        public readonly bool hasTarget;
        public readonly TargetLockState state;
        public readonly ObjectDetectionEvent detection;
        public readonly float matchScore;
        public readonly float lostSeconds;
        public readonly long timestamp;

        public LockedTargetEvent(
            bool hasTarget,
            TargetLockState state,
            ObjectDetectionEvent detection,
            float matchScore,
            float lostSeconds,
            long timestamp)
        {
            this.hasTarget = hasTarget;
            this.state = state;
            this.detection = detection;
            this.matchScore = matchScore;
            this.lostSeconds = lostSeconds;
            this.timestamp = timestamp;
        }
    }
}
