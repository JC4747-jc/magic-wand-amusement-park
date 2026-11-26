using System.Collections.Generic;
using UnityEngine;

namespace Perception
{
    // Diagnostic only: never writes to the target or changes tracking eligibility.
    public sealed class LighterDetectionOverlay : MonoBehaviour
    {
        readonly List<LineRenderer> boxes = new List<LineRenderer>();
        LineRenderer marker;
        Material material;
        float expires;
        int count;

        LineRenderer MakeLine(string label, Color color)
        {
            if (material == null)
            {
                var shader = Resources.Load<Shader>("MagicMR/HandSpell");
                if (shader == null)
                {
                    Debug.LogError("[DetectionOverlay] Missing Resources/MagicMR/HandSpell shader; overlay disabled.");
                    enabled = false;
                    return null;
                }
                material = new Material(shader);
            }
            var go = new GameObject(label);
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.startWidth = line.endWidth = .0015f;
            line.startColor = line.endColor = color;
            // The Resources shader is included in Android builds and reads vertex colours.
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        public void BeginFrame()
        {
            foreach (var box in boxes) box.enabled = false;
            count = 0;
            expires = Time.realtimeSinceStartup + .35f;
        }

        public static Vector3 Project(Vector2 pixel, float depth, Vector4 k, Pose camera, Pose head)
        {
            var optical = new Vector3((pixel.x-k.z)*depth/k.x, (pixel.y-k.w)*depth/k.y, depth);
            return head.position + head.rotation * StereoFusionGeometry.OpticalToHead(optical, camera);
        }

        public void Box(Rect rect, float depth, bool measured, Vector4 k, Pose camera, Pose head)
        {
            if (!enabled) return;
            if (count == boxes.Count)
            {
                var created = MakeLine("YOLO detection", Color.green);
                if (created == null) return;
                boxes.Add(created);
            }
            var line = boxes[count++];
            line.startColor = line.endColor = measured ? Color.green : Color.yellow;
            line.enabled = true;
            line.positionCount = 4; line.loop = true;
            line.SetPositions(new[] {
                Project(new Vector2(rect.xMin,rect.yMin),depth,k,camera,head),
                Project(new Vector2(rect.xMax,rect.yMin),depth,k,camera,head),
                Project(new Vector2(rect.xMax,rect.yMax),depth,k,camera,head),
                Project(new Vector2(rect.xMin,rect.yMax),depth,k,camera,head) });
        }

        public void Placement(Vector3 point, bool visible)
        {
            if (!enabled) return;
            if (marker == null && visible) marker = MakeLine("Applied lighter position", Color.cyan);
            if (marker == null) return;
            marker.enabled = visible;
            marker.positionCount = 5;
            marker.SetPositions(new[] { point-Vector3.right*.012f, point+Vector3.right*.012f,
                point, point-Vector3.up*.012f, point+Vector3.up*.012f });
        }

        void Update()
        {
            if (Time.realtimeSinceStartup > expires)
                foreach (var box in boxes) box.enabled = false;
        }
        void OnDisable()
        {
            foreach (var box in boxes) box.enabled = false;
            if (marker != null) marker.enabled = false;
        }
        void OnDestroy() { if (material != null) Destroy(material); }
    }
}
