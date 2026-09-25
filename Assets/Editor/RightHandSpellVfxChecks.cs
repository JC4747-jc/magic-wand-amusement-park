using System;
using MagicMR;
using UnityEditor;
using UnityEngine;

public static class RightHandSpellVfxChecks
{
    [MenuItem("MagicMR/Checks/Right Hand Spell VFX")]
    public static void Run()
    {
        var shader = Resources.Load<Shader>("MagicMR/HandSpell");
        if (shader == null || ShaderUtil.ShaderHasError(shader))
            throw new Exception("Hand spell shader missing or failed compilation");
        var owner = new GameObject("Spell VFX check");
        try
        {
            var fx = RightHandSpellVfx.GetOrCreate(owner);
            // MonoBehaviour Awake does not run automatically in edit mode.
            if (!Application.isPlaying) Lifecycle(fx, "Awake");
            fx.Cast(EditDimension.Appearance, Vector3.up, Vector3.zero, false);
            Require(Active(owner) == 0, "Cast without a valid hand emitted effects");
            foreach (EditDimension d in new[] { EditDimension.Appearance, EditDimension.Agency,
                EditDimension.Rule, EditDimension.Deconstruction, EditDimension.Scale })
            {
                fx.Cast(d, Vector3.up * .2f, Vector3.zero, true);
                Require(Active(owner) > 0, "Missing cast: " + d);
                fx.Clear();
                Require(Active(owner) == 0, "Reset leaked a visible stroke");
            }
            for (int n = 0; n < 100; n++) fx.Cast(EditDimension.Deconstruction, Vector3.up, Vector3.zero, true);
            Require(owner.GetComponentsInChildren<LineRenderer>().Length <= 81, "Pool exceeded capacity");
            foreach (var line in owner.GetComponentsInChildren<LineRenderer>())
                Require(line.useWorldSpace && line.sharedMaterial.shader == shader, "Wrong coordinates or material");
            fx.Clear();
            fx.Summon(Vector3.zero, Vector3.up * .2f);
            Require(Active(owner) == 2, "Summon must emit transfer and ring");
            fx.enabled = false;
            if (!Application.isPlaying) Lifecycle(fx, "OnDisable");
            Require(Active(owner) == 0, "Disabled VFX left visible geometry");
        }
        finally
        {
            if (!Application.isPlaying) Lifecycle(owner.GetComponent<RightHandSpellVfx>(), "OnDestroy");
            UnityEngine.Object.DestroyImmediate(owner);
        }
        TabletopGestureChecks.Run();
        Debug.Log("[RightHandSpellVfxChecks] PASS: shader, cast gating, all spells, pool limit, reset, summon, gesture regression");
    }
    static int Active(GameObject owner)
    {
        int count = 0;
        foreach (var line in owner.GetComponentsInChildren<LineRenderer>()) if (line.enabled) count++;
        return count;
    }
    static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Lifecycle(RightHandSpellVfx fx, string method)
    {
        typeof(RightHandSpellVfx).GetMethod(method,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(fx, null);
    }
}
