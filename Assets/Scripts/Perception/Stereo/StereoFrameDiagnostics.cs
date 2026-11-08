using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Perception
{
    // Diagnostic build only. No network connection or inference; one bounded disk job at a time.
    // Captured arrays are immutable and belong to the exact YOLO request being evaluated.
    public sealed class StereoFrameDiagnostics
    {
        [Serializable]
        public sealed class Frame
        {
            public string session, utc, status;
            public long frameId, captureTimestampNs;
            public int width = 640, height = 480;
            public float captureAgeMs, roundTripMs, pairGapMs;
            public bool hasDetection, jpegFlipped;
            public float confidence, x1, y1, x2, y2;
            public Vector4 intrinsics;
            public float baseline;
            public Pose headPose, cameraPose;
            public StereoPatchMatcher.Estimate estimate;
            public StereoPatchMatcher.Sample[] samples;
        }

        readonly string directory;
        readonly string session = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        int busy, targetCount, emptyCount;
        float nextTargetAt, nextEmptyAt;
        string error;
        public StereoFrameDiagnostics(string persistentDataPath)
        {
            directory = Path.Combine(persistentDataPath, "stereo-diagnostics");
        }
        public void Record(Frame frame, byte[] left, byte[] right, float now)
        {
            string failure = Interlocked.Exchange(ref error, null);
            if (failure != null) Debug.LogWarning("[StereoEvidence] " + failure);
            if (left == null || right == null || left.Length != 640 * 480 || right.Length != 640 * 480) return;
            if (frame.hasDetection ? targetCount >= 60 || now < nextTargetAt : emptyCount >= 6 || now < nextEmptyAt) return;
            if (Interlocked.CompareExchange(ref busy, 1, 0) != 0) return;
            string slot;
            if (frame.hasDetection) { slot = "target-" + (targetCount++).ToString("D2"); nextTargetAt = now + 1f; }
            else { slot = "empty-" + (emptyCount++).ToString("D2"); nextEmptyAt = now + 5f; }
            frame.session = session;
            frame.utc = DateTime.UtcNow.ToString("O");
            // Unity serialization runs on the main thread, file IO on a worker.
            string json = JsonUtility.ToJson(frame, true);
            Task.Run(() =>
            {
                try
                {
                    Directory.CreateDirectory(directory);
                    string prefix = Path.Combine(directory, slot);
                    WriteGray(prefix + "-left.pgm", left);
                    WriteGray(prefix + "-right.pgm", right);
                    File.WriteAllText(prefix + ".json", json);
                }
                catch (Exception e) { Interlocked.Exchange(ref error, e.Message); }
                finally { Interlocked.Exchange(ref busy, 0); }
            });
        }
        static void WriteGray(string path, byte[] pixels)
        {
            using (var stream = File.Create(path))
            {
                byte[] header = Encoding.ASCII.GetBytes("P5\n640 480\n255\n");
                stream.Write(header, 0, header.Length);
                stream.Write(pixels, 0, pixels.Length);
            }
        }
    }
}
