using System;
using System.Reflection;
using MagicMR;
using Perception;
using UnityEngine;

public static class RequestedInteractionChecks
{
    static void Require(bool ok, string message) { if (!ok) throw new Exception(message); }
    public static void Run()
    {
        CheckBimanual(); CheckRegistration(); CheckReplacementAndScale();
        Debug.Log("REQUESTED_INTERACTION_CHECKS_PASS: bimanual ownership, full stationary registration, replacement bounds, 3x interior/reset");
    }

    static void CheckBimanual()
    {
        var g = new BimanualScaleGesture();
        Vector3 l = Vector3.left * .12f, r = Vector3.right * .12f;
        Require(!g.Step(true, false, l, r, Vector3.right, 0) && !g.OwnsGesture, "Far palms armed scaling");
        Require(!g.Step(true, true, l, r, Vector3.right, .1f) && g.OwnsGesture, "Both palms did not claim the gesture before swipe");
        Require(!g.Step(true, true, l, r + Vector3.right * .14f, Vector3.right, .2f), "One-hand swipe emitted Scale");
        Require(g.Step(true, false, l + Vector3.left * .07f, r + Vector3.right * .07f, Vector3.right, .3f), "Outward two-hand pull did not grow");
        for (int n = 4; n <= 13; n++)
            Require(!g.Step(true, true, l + Vector3.left * .07f, r + Vector3.right * .07f, Vector3.right, n * .1f), "Held spread repeated scaling");
        g.Step(true, true, l, r, Vector3.right, 1.4f);
        g.Step(true, true, l + Vector3.left * .025f, r + Vector3.right * .025f, Vector3.right, 1.5f);
        Require(g.Step(true, false, l + Vector3.left * .07f, r + Vector3.right * .07f, Vector3.right, 1.6f), "Returned palms could not rearm");
        g.Reset(); g.Step(true, true, l, r, Vector3.right, 2);
        Require(!g.Step(true, true, l + Vector3.up * .1f, r + Vector3.up * .1f, Vector3.right, 2.1f), "Vertical lift grew the lighter");
        Require(!g.Step(false, true, l, r, Vector3.right, 2.2f) && !g.OwnsGesture, "Lost hand retained ownership");
        g.Step(true, true, l, r, Vector3.right, 3);
        Require(!g.Step(true, false, l + Vector3.left * .1f, r + Vector3.right * .1f, Vector3.right, 3.3f), "Tracking gap completed a spread");
    }

    static void CheckRegistration()
    {
        var full = new Rect(300, 200, 30, 80);
        Require(StereoYoloLocator.CompleteRegistration(.9f, .1f, 8, full), "Complete reliable lighter rejected");
        Require(!StereoYoloLocator.CompleteRegistration(.9f, .1f, 8, new Rect(0, 200, 30, 80)), "Clipped lighter calibrated");
        Require(!StereoYoloLocator.CompleteRegistration(.3f, .1f, 8, full), "Weak detection calibrated");
        Require(!StereoYoloLocator.CompleteRegistration(.9f, .01f, 8, full), "Ambiguous depth calibrated");
        var target = new TabletopTargetLock();
        for (int n = 0; n < 8; n++)
            Require(!target.Observe(Vector3.right * n * .015f, n * .1f, .08f, .025f, -.04f), "Moving lighter calibrated");
        target.Reset();
        for (int n = 0; n < 8; n++)
            Require(!target.Observe(Vector3.zero, n * .1f, n % 2 == 0 ? .08f : .06f, .025f, -.04f), "Changing/occluded bbox calibrated");
        target.Reset();
        for (int n = 0; n < 7; n++) target.Observe(Vector3.zero, n * .11f, .08f, .025f, -.04f);
        Require(target.IsLocked, "Stationary complete target never became ready");
    }

    static void CheckReplacementAndScale()
    {
        var model = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var settings = ScriptableObject.CreateInstance<LighterModelSettings>();
        try
        {
            model.transform.localScale = new Vector3(.04f, .2f, .02f);
            root.transform.localScale = new Vector3(.025f, .08f, .012f);
            settings.modelPrefab = model; settings.rotationDegrees = new Vector3(0, 35, 0);
            LighterModelBuilder.Apply(root, settings);
            var editor = root.AddComponent<RealityEditor>();
            var awake = typeof(RealityEditor).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
            awake.Invoke(editor, null);
            Vector3 bottom = new Vector3(.3f, .8f, .5f);
            editor.RegisterVisualBounds(bottom, .08f);
            var renderer = root.transform.Find("VisualModel").GetComponentInChildren<MeshRenderer>();
            var bounds = renderer.bounds;
            Require(Mathf.Abs(bounds.size.y - .08f) < .0001f && Mathf.Abs(bounds.min.y - bottom.y) < .0001f,
                "Replacement lost registered height or bottom");
            Require(Mathf.Abs(bounds.center.x - bottom.x) < .0001f && Mathf.Abs(bounds.center.z - bottom.z) < .0001f,
                "Replacement lost horizontal alignment");
            var eyes = root.transform.Find("AgencyEyes");
            Require(eyes != null && eyes.position.y > bounds.min.y && eyes.position.y < bounds.max.y,
                "Replacement eyes were outside the registered body");
            for (int n = 0; n < 4; n++) editor.TriggerScale();
            Require(!editor.InteriorVisible, "Interior opened before reaching 3x");
            float scale = root.transform.localScale.y;
            Require(Mathf.Abs(scale - .08f * .4f * 3) < .0001f, "Scale did not cap at 3x");
            editor.TriggerScale();
            Require(editor.InteriorVisible, "Next pull at the 3x cap did not show the schematic");
            editor.ResetTarget();
            Require(!editor.InteriorVisible && Mathf.Abs(root.transform.localScale.y - scale / 3) < .0001f,
                "Reset retained interior or enlargement");
            Require(renderer.sharedMaterial == model.GetComponent<Renderer>().sharedMaterial, "Replacement lost its authored material on reset");
        }
        finally
        { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(model); UnityEngine.Object.DestroyImmediate(settings); }
    }
}
