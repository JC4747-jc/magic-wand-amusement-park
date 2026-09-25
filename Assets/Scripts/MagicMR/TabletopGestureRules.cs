using System.Collections.Generic;
using UnityEngine;

namespace MagicMR
{
    public sealed class GestureReachWindow
    {
        public const float EnterMeters = .20f;
        public const float ExitMeters = .32f;
        float nearAt = -100;
        public bool Allowed { get; private set; }
        public bool Observe(bool valid, float distance, float now)
        {
            if (!valid || !float.IsFinite(distance)) { Reset(); return false; }
            if (distance <= EnterMeters) nearAt = now;
            Allowed = distance <= EnterMeters || (distance <= ExitMeters && now - nearAt <= .65f);
            return Allowed;
        }
        public void Reset() { Allowed = false; nearAt = -100; }
    }

    // One mutually exclusive recognizer owns a right-hand action. No callback-order races.
    public sealed class TabletopGestureRules
    {
        public const float RepeatCooldownSeconds = .35f;
        public struct Sample
        {
            public float time, pinch;
            public Vector3 index, palm, swipeAxis;
            public bool fist, open, pointing;
        }
        public string Status { get; private set; } = "Show right hand";
        enum Mode { Idle, Pinch, Fist }
        Mode mode;
        float began, lastClosed, cooldownUntil, lastTime = -100;
        bool fistArmed;
        readonly List<Vector3> circle = new List<Vector3>();
        float circleBegan;
        Vector3 swipeStart, swipeLast, swipeAxis;
        float swipeBegan, swipePath;
        bool swiping;

        public void Reset()
        {
            mode = Mode.Idle; circle.Clear(); swiping = false;
            fistArmed = false; lastTime = -100; cooldownUntil = 0;
            Status = "Show right hand";
        }

        public EditDimension Step(Sample s)
        {
            if (s.time - lastTime > .15f) { mode = Mode.Idle; circle.Clear(); swiping = false; fistArmed = false; }
            lastTime = s.time;
            if (s.time < cooldownUntil) { Status = "Done - pause before next action"; return EditDimension.None; }

            if (s.fist)
            {
                if (mode != Mode.Fist) { began = s.time; fistArmed = false; }
                mode = Mode.Fist; lastClosed = s.time;
                fistArmed |= s.time - began >= .12f;
                circle.Clear(); swiping = false;
                Status = fistArmed ? "Fist ready - open fingers" : "Hold fist briefly";
                return EditDimension.None;
            }
            if (mode == Mode.Fist)
            {
                if (s.time - lastClosed > .7f) { mode = Mode.Idle; Status = "Open fingers sooner"; return EditDimension.None; }
                if (s.open && fistArmed) return Fire(EditDimension.Deconstruction, s.time);
                Status = "Open all fingers";
                return EditDimension.None;
            }

            if (s.pinch <= .035f)
            {
                if (mode != Mode.Pinch) began = s.time;
                mode = Mode.Pinch; circle.Clear(); swiping = false;
                Status = "Pinch - release to burn";
                return EditDimension.None;
            }
            if (mode == Mode.Pinch)
            {
                if (s.pinch < .060f) return EditDimension.None;
                mode = Mode.Idle;
                if (s.time - began >= .04f && s.time - began <= .85f)
                    return Fire(EditDimension.Appearance, s.time);
                Status = "Pinch then release within 0.85s";
                return EditDimension.None;
            }

            if (s.pointing)
            {
                swiping = false;
                if (circle.Count == 0 || s.time - circleBegan > 3f)
                { circle.Clear(); circleBegan = s.time; circle.Add(s.index); }
                if (Vector3.Distance(circle[circle.Count - 1], s.index) >= .003f) circle.Add(s.index);
                if (circle.Count > 256) { circle.Clear(); return EditDimension.None; }
                Status = "Pointing - complete one circle";
                if (s.time - circleBegan >= .5f && IsClosedCircle(circle)) return Fire(EditDimension.Agency, s.time);
                return EditDimension.None;
            }
            circle.Clear();

            if (s.open)
            {
                if (!swiping || s.time - swipeBegan > .45f)
                { swiping = true; swipeStart = swipeLast = s.palm; swipeBegan = s.time; swipePath = 0;
                    swipeAxis=s.swipeAxis.sqrMagnitude>.1f?s.swipeAxis.normalized:Vector3.right; }
                swipePath += Vector3.Distance(swipeLast, s.palm); swipeLast = s.palm;
                Vector3 delta = s.palm - swipeStart;
                float duration = s.time - swipeBegan;
                Status = "Open palm - swipe horizontally";
                if (duration >= .1f && delta.magnitude >= .10f && delta.magnitude / duration >= .55f &&
                    delta.magnitude / Mathf.Max(swipePath, .001f) >= .85f &&
                    Mathf.Abs(Vector3.Dot(delta,swipeAxis)) >= .9f * delta.magnitude)
                    return Fire(EditDimension.Rule, s.time);
            }
            else { swiping = false; Status = "Pinch / point-circle / palm-swipe / fist-open"; }
            return EditDimension.None;
        }

        EditDimension Fire(EditDimension d, float time)
        {
            mode = Mode.Idle; circle.Clear(); swiping = false; fistArmed = false;
            cooldownUntil = time + RepeatCooldownSeconds; Status = "Recognized: " + d;
            return d;
        }

        public static bool IsClosedCircle(IReadOnlyList<Vector3> points)
        {
            if (points.Count < 16) return false;
            Vector3 center = Vector3.zero, normal = Vector3.zero;
            foreach (var p in points) center += p;
            center /= points.Count;
            for (int i = 1; i < points.Count; i++) normal += Vector3.Cross(points[i - 1] - center, points[i] - center);
            if (normal.magnitude < .001f) return false;
            normal.Normalize();
            float radius = 0, planeError = 0, path = 0;
            foreach (var p in points) { radius += Vector3.Distance(p, center); planeError += Mathf.Abs(Vector3.Dot(p - center, normal)); }
            radius /= points.Count;
            if (radius < .022f || radius > .10f || planeError / points.Count > radius * .25f ||
                Vector3.Distance(points[0], points[points.Count - 1]) > radius * .65f) return false;
            float variance = 0, signedAngle = 0, totalAngle = 0;
            for (int i = 0; i < points.Count; i++)
            {
                variance += Mathf.Pow(Vector3.Distance(points[i], center) - radius, 2);
                if (i == 0) continue;
                float a = Vector3.SignedAngle(points[i - 1] - center, points[i] - center, normal);
                signedAngle += a; totalAngle += Mathf.Abs(a);
                path += Vector3.Distance(points[i - 1], points[i]);
            }
            return Mathf.Sqrt(variance / points.Count) / radius < .30f &&
                Mathf.Abs(signedAngle) >= 290f && Mathf.Abs(signedAngle) / Mathf.Max(totalAngle, 1) > .85f &&
                path >= radius * 5f && path <= radius * 8.5f;
        }
    }
}
