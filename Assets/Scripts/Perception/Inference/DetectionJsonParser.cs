using System;
using System.Collections.Generic;
using UnityEngine;

namespace Perception
{
    /// <summary>
    /// Parses newline JSON detection payloads into DetectionFrame.
    /// Field/coordinate semantics match DetectionManager (absolute JPEG pixels).
    /// Extracted for RemoteInferenceClient without changing DetectionManager wiring.
    /// </summary>
    public static class DetectionJsonParser
    {
        public static bool TryParse(string json, float minConfidence, out DetectionFrame frame)
        {
            frame = default;
            if (string.IsNullOrWhiteSpace(json))
                return false;

            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            long frameId = 0;
            int width = 0;
            int height = 0;
            var events = new List<ObjectDetectionEvent>(8);

            if (json.IndexOf("\"detections\"", StringComparison.Ordinal) >= 0)
            {
                DetectionFrameMessage frameDto = JsonUtility.FromJson<DetectionFrameMessage>(json);
                if (frameDto == null)
                    return false;

                frameId = frameDto.frame_id;
                if (frameDto.timestamp != 0)
                    timestamp = frameDto.timestamp;
                width = frameDto.width;
                height = frameDto.height;

                if (frameDto.detections != null)
                {
                    foreach (DetectionMessage dto in frameDto.detections)
                        TryAdd(dto, timestamp, minConfidence, events);
                }
            }
            else
            {
                DetectionMessage dto = JsonUtility.FromJson<DetectionMessage>(json);
                TryAdd(dto, timestamp, minConfidence, events);
            }

            frame = new DetectionFrame(events, timestamp, frameId, width, height);
            return true;
        }

        static void TryAdd(
            DetectionMessage dto,
            long timestamp,
            float minConfidence,
            List<ObjectDetectionEvent> events)
        {
            if (dto == null || string.IsNullOrEmpty(dto.@class))
                return;
            if (dto.confidence < minConfidence)
                return;

            float x1 = dto.x1;
            float y1 = dto.y1;
            float x2 = dto.x2;
            float y2 = dto.y2;

            if (!(x2 > x1 && y2 > y1))
            {
                const float half = 8f;
                x1 = dto.centerX - half;
                y1 = dto.centerY - half;
                x2 = dto.centerX + half;
                y2 = dto.centerY + half;
            }

            events.Add(new ObjectDetectionEvent(
                className: dto.@class,
                confidence: dto.confidence,
                centerX: dto.centerX,
                centerY: dto.centerY,
                timestamp: timestamp,
                x1: x1,
                y1: y1,
                x2: x2,
                y2: y2));
        }
    }
}
