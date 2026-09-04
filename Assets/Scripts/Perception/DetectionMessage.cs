using System;

namespace Perception
{
    /// <summary>
    /// Wire-format DTO for one detection (pixel space = PICO Camera / JPEG sent to YOLO).
    /// </summary>
    [Serializable]
    public class DetectionMessage
    {
        public string @class;
        public float confidence;
        public float centerX;
        public float centerY;
        public float x1;
        public float y1;
        public float x2;
        public float y2;
    }

    /// <summary>
    /// One YOLO frame. Phase 1 adds frame_id / image size for PICO Camera correlation.
    /// {"frame_id":1,"timestamp":...,"width":W,"height":H,"detections":[...]}
    /// </summary>
    [Serializable]
    public class DetectionFrameMessage
    {
        public long frame_id;
        public long timestamp;
        public int width;
        public int height;
        public DetectionMessage[] detections;
    }
}
