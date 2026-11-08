using UnityEngine;

namespace Perception
{
    public static class F2GPoseGeometry
    {
        // Existing PICO pinhole convention: native optical -> SDK head reflects Z.
        // Reflect the object basis too, so the result is a proper Unity rotation.
        public static bool TryWorldRotation(float[] r, Quaternion captureHead, Quaternion cameraToHead, out Quaternion world)
        {
            world = Quaternion.identity;
            if (r == null || r.Length != 9) return false;
            foreach (float x in r) if (!float.IsFinite(x)) return false;
            Vector3 xAxis = new Vector3(r[0], r[3], r[6]);
            Vector3 yAxis = new Vector3(r[1], r[4], r[7]);
            Vector3 zAxis = new Vector3(r[2], r[5], r[8]);
            if (Mathf.Abs(xAxis.sqrMagnitude - 1) > .02f || Mathf.Abs(yAxis.sqrMagnitude - 1) > .02f ||
                Mathf.Abs(zAxis.sqrMagnitude - 1) > .02f || Mathf.Abs(Vector3.Dot(xAxis,yAxis)) > .02f ||
                Mathf.Abs(Vector3.Dot(xAxis,zAxis)) > .02f || Mathf.Abs(Vector3.Dot(yAxis,zAxis)) > .02f ||
                Vector3.Dot(Vector3.Cross(xAxis,yAxis),zAxis) < .98f) return false;
            // F R F: forward = -F*zAxis; up = F*yAxis.
            Quaternion opticalUnity = Quaternion.LookRotation(new Vector3(-zAxis.x,-zAxis.y,zAxis.z),
                new Vector3(yAxis.x,yAxis.y,-yAxis.z));
            world = (captureHead * cameraToHead * opticalUnity).normalized;
            return true;
        }

        public static void RightFromLeft(Pose left, Pose right, out float[] rotation, out float[] translation)
        {
            Quaternion q = Quaternion.Inverse(right.rotation) * left.rotation;
            Matrix4x4 r = Matrix4x4.Rotate(q);
            rotation = new[] { r.m00,r.m01,-r.m02, r.m10,r.m11,-r.m12, -r.m20,-r.m21,r.m22 };
            Vector3 t = Quaternion.Inverse(right.rotation) * (left.position - right.position);
            translation = new[] { t.x, t.y, -t.z };
        }
    }

    // Absolute world orientations, with a fixed summon reference. No Euler interpolation.
    public sealed class F2GRotationFilter
    {
        public Quaternion Accepted { get; private set; } = Quaternion.identity;
        public int StableSamples { get; private set; }
        public float LastAcceptedTime { get; private set; } = -100;
        Quaternion candidate;
        int candidates;
        float lastObserved = -100;
        long lastId;
        bool hasSample;

        public void Reset()
        {
            hasSample = false; lastId = 0; StableSamples = candidates = 0;
            lastObserved = LastAcceptedTime = -100;
        }

        public bool Observe(long frameId, Quaternion rotation, float capturedAt, float now, float maxAge)
        {
            if (frameId <= lastId || capturedAt > now || now-capturedAt > maxAge) return false;
            lastId = frameId;
            // A single-flight GPU request can make the actual interval exceed the
            // nominal 0.5 s at 2 Hz. Do not restart confirmation on every such frame.
            bool gap = capturedAt-lastObserved > Mathf.Max(.6f, maxAge * 1.5f);
            lastObserved = capturedAt;
            if (!hasSample)
            {
                Accepted = rotation; hasSample = true; StableSamples = 1;
                LastAcceptedTime = capturedAt; return true;
            }
            float angle = Quaternion.Angle(Accepted,rotation);
            if (gap) { candidates = 0; StableSamples = 0; }
            if (gap || angle > 35 || candidates > 0)
            {
                if (candidates == 0 || Quaternion.Angle(candidate,rotation) > 12) candidates = 1;
                else candidates++;
                candidate = rotation;
                if (candidates < 3) return false;
                candidates = 0;
            }
            StableSamples = angle < 8 && !gap ? StableSamples+1 : 1;
            Accepted = rotation; LastAcceptedTime = capturedAt;
            return true;
        }
    }
}
