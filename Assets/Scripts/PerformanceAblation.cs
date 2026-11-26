#if PERFORMANCE_ABLATION
using System;
using System.Collections;
using System.Globalization;
using System.IO;
using UnityEngine;

// Included only in the dedicated experimental build.
public sealed class PerformanceAblation : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install() => new GameObject("PerformanceAblation").AddComponent<PerformanceAblation>();
    Renderer[] models;
    string mode = "waiting";
    int repeat;
    StreamWriter output;
    bool sampling;
    double previous;
    IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(20);
        var lighter = GameObject.Find("Lighter");
        if (!lighter) { Debug.LogError("ABLATION missing Lighter"); yield break; }
        models = lighter.GetComponentsInChildren<Renderer>(true);
        // Wait for the ordinary summon workflow to make the real model visible.
        while (!Array.Exists(models, r => r && r.enabled && r.gameObject.activeInHierarchy && !r.forceRenderingOff))
            yield return new WaitForSecondsRealtime(1);
        output = new StreamWriter(Path.Combine(Application.persistentDataPath, "ablation.csv"));
        output.WriteLine("mode,repeat,time_s,frame_ms,enabled_model_renderers,visible_model_renderers");
        for (int i = 1; i <= 3; i++)
        {
            repeat = i;
            yield return Run("baseline");
            yield return Run("no_model");
        }
        mode = "transition";
        // Destroy capture to invoke its explicit asynchronous shutdown; disabling
        // the MonoBehaviour would leave its async acquisition loop running.
        foreach (var capture in FindObjectsByType<PicoStereoCapture>(FindObjectsSortMode.None)) Destroy(capture);
        foreach (var script in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
        {
            if (!script || script == this) continue;
            string ns = script.GetType().Namespace ?? "";
            if (ns == "Perception" || ns.StartsWith("Perception.") || ns == "MagicMR")
            {
                // Keep VST maintenance and XR infrastructure consistent.
                if (script is MagicMR.PicoVstKeeper) continue;
                script.StopAllCoroutines();
                script.enabled = false;
            }
        }
        foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None)) canvas.enabled = false;
        foreach (var renderer in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            if (Array.IndexOf(models, renderer) < 0) renderer.forceRenderingOff = true;
        for (int i = 1; i <= 3; i++) { repeat = i; yield return Run("model_only"); }
        output.Dispose(); output = null;
        Debug.Log("ABLATION COMPLETE " + Application.persistentDataPath);
    }
    IEnumerator Run(string name)
    {
        mode = name;
        foreach (var r in models) if (r) r.forceRenderingOff = name == "no_model";
        Debug.Log("ABLATION WARMUP " + name + " " + repeat);
        yield return new WaitForSecondsRealtime(10);
        previous = Time.realtimeSinceStartupAsDouble;
        sampling = true;
        Debug.Log("ABLATION SAMPLE " + name + " " + repeat);
        yield return new WaitForSecondsRealtime(60);
        sampling = false;
        output.Flush();
    }
    void LateUpdate()
    {
        if (mode == "no_model" && models != null)
            foreach (var r in models) if (r) r.forceRenderingOff = true;
        if (!sampling) return;
        double now = Time.realtimeSinceStartupAsDouble;
        int enabled = 0, visible = 0;
        foreach (var r in models) if (r && r.enabled && r.gameObject.activeInHierarchy && !r.forceRenderingOff)
        { enabled++; if (r.isVisible) visible++; }
        output.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0},{1},{2:F6},{3:F6},{4},{5}", mode, repeat, now, (now-previous)*1000, enabled, visible));
        previous = now;
    }
    void OnDestroy() { output?.Dispose(); }
}
#endif


