using System;
using System.IO;
using MagicMR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class AffectiveNarrativeChecks
{
    static void Require(bool value, string reason) { if (!value) throw new Exception(reason); }

    [MenuItem("MagicMR/Checks/Affective Narrative")]
    public static void Run()
    {
        // Isolated edit-mode regression. Existing scene objects are not mutated.
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        GameObject host = null, target = null;
        try
        {
            TabletopGestureChecks.Run();
            host = new GameObject("Affective checks");
            target = new GameObject("Explicit physical anchor");
            target.transform.localScale = Vector3.one * .08f;
            var overlay = host.AddComponent<Affective2DOverlayAnimator>();
            foreach (AffectiveMotif motif in Enum.GetValues(typeof(AffectiveMotif)))
            {
                if (motif == AffectiveMotif.None) continue;
                overlay.Play(target.transform, motif, Color.white, "check");
                Require(overlay.IsPlaying, "Prototype did not start: " + motif);
                var runtime = GameObject.Find("Affective 2D Overlay (Runtime) — check");
                Require(runtime != null && runtime.transform.parent == null, "Overlay inherited target scale");
                Require(runtime.GetComponentsInChildren<MeshRenderer>().Length > 0, "Empty motif");
                overlay.Stop();
                Require(!overlay.IsPlaying && runtime == null, "Stop left a runtime overlay");
            }
            var director = host.AddComponent<RealityScenarioDirector>();
            director.SelectScenario(2);
            Require(!director.TryActivateNarrative(EditDimension.Appearance, Vector3.zero, false),
                "Unbound non-lighter scenario silently accepted another subject");
            var settings = new SerializedObject(director);
            settings.FindProperty("m_Scenarios").GetArrayElementAtIndex(1).FindPropertyRelative("target").objectReferenceValue = target;
            settings.ApplyModifiedPropertiesWithoutUndo();
            director.SelectScenario(2);
            foreach (var d in new[] { EditDimension.Appearance, EditDimension.Agency, EditDimension.Rule, EditDimension.Deconstruction })
            {
                Require(director.AcceptsGesture(d), "Missing narrative stage " + d);
                Require(director.TryActivateNarrative(d, Vector3.zero, false), "Stage did not play " + d);
                Require(!director.TryActivateNarrative(d, Vector3.zero, false), "Replay interrupted active stimulus");
                director.ResetNarrative();
                Require(!overlay.IsPlaying, "Reset retained overlay");
            }
            Require(!director.AcceptsGesture(EditDimension.Scale), "Legacy scale leaked into narrative input");
            director.SelectScenario(8);
            Require(!overlay.IsPlaying, "Scene transition retained reward");

            var player = host.GetComponent<AffectiveFramePlayer>();
            var texture = new Texture2D(8, 8);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 8, 8), Vector2.one * .5f, 100);
            try
            {
                player.Play(target.transform, new[] { sprite }, null, 1f, .2f, Vector3.zero);
                Require(player.IsPlaying, "Authored frame playback failed");
                var renderer = GameObject.Find("Affective authored animation").GetComponent<SpriteRenderer>();
                Require(Mathf.Abs(renderer.sprite.bounds.size.x * renderer.transform.lossyScale.x - .2f) < .001f,
                    "Authored frame does not preserve physical width");
                player.Stop();
                player.Play(target.transform, new Sprite[] { null }, null, 1f, .2f, Vector3.zero);
                Require(!player.IsPlaying, "Invalid frame produced a playing state");
            }
            finally { UnityEngine.Object.DestroyImmediate(sprite); UnityEngine.Object.DestroyImmediate(texture); }
            Debug.Log("AFFECTIVE_NARRATIVE_CHECKS_PASS");
        }
        finally
        {
            if (host != null) UnityEngine.Object.DestroyImmediate(host);
            if (target != null) UnityEngine.Object.DestroyImmediate(target);
            EditorSceneManager.CloseScene(scene, true);
        }
    }
}
