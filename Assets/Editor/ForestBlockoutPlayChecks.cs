using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class ForestBlockoutPlayChecks
{
    const string Key = "MagicMR.ForestBlockoutReview";
    static string Output => SessionState.GetString(Key + ".Output", ForestBlockoutBuilder.Output);
    static ForestPreviewWalker walker;
    static int target, steps, errors, wallSteps;
    static double started;
    static Vector3 spawn, wallStart;
    static GameObject wall;
    static float maxGroundGap;
    static ForestBlockoutPlayChecks() { EditorApplication.update += Tick; Application.logMessageReceived += Log; }
    static void Log(string message, string trace, LogType type)
    {
        if (SessionState.GetBool(Key, false) && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)) errors++;
    }
    public static void Run()
    {
        Begin(ForestBlockoutBuilder.ScenePath, ForestBlockoutBuilder.Output);
    }
    public static void RunStudy()
    {
        Begin(ForestStudyBuilder.ScenePath, ".codex-tmp/forest-study");
    }
    static void Begin(string scene, string output)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Requires idle editor");
        SessionState.SetString(Key + ".Output", output);
        Directory.CreateDirectory(Output);
        File.WriteAllText(Output + "/play-result.txt", "RUNNING\n");
        SessionState.SetString(Key + ".Previous", UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);
        SessionState.SetInt(Key + ".Exit", -1); SessionState.SetBool(Key, true);
        EditorSceneManager.OpenScene(scene); EditorApplication.EnterPlaymode();
    }
    static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Tick()
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (!EditorApplication.isPlaying)
        {
            int code = SessionState.GetInt(Key + ".Exit", -1); if (code < 0) return;
            SessionState.SetBool(Key, false);
            string previous = SessionState.GetString(Key + ".Previous", "");
            if (!string.IsNullOrEmpty(previous)) EditorSceneManager.OpenScene(previous);
            EditorApplication.Exit(code); return;
        }
        try
        {
            if (!walker)
            {
                walker = UnityEngine.Object.FindFirstObjectByType<ForestPreviewWalker>(); Require(walker, "Walker missing in saved scene");
                walker.scriptedReview = true; spawn = walker.SpawnPosition; started = EditorApplication.timeSinceStartup;
                target = 1; Application.targetFrameRate = 60; return;
            }
            Require(EditorApplication.timeSinceStartup - started < 180, "Walkthrough timeout");
            Require(errors == 0, "Runtime errors: " + errors);
            // Exercise the same CharacterController.Move path used by keyboard input, in real Play Mode.
            // Fixed bounded movement steps make this independent of editor rendering throughput.
            for (int substep = 0; substep < 4; substep++)
            {
                if (target < walker.route.Length)
                {
                    var delta = walker.route[target] - walker.transform.position; delta.y = 0;
                    if (delta.magnitude < .10f) { target++; continue; }
                    walker.transform.rotation = Quaternion.LookRotation(delta);
                    walker.MoveWorld(delta.normalized, 1f / 60); steps++;
                    Require(steps < 1600, "Walker stalled before completing corridor");
                    float gap = walker.transform.position.y - ForestBlockoutBuilder.Height(walker.transform.position.x, walker.transform.position.z);
                    maxGroundGap = Mathf.Max(maxGroundGap, Mathf.Abs(gap)); Require(Mathf.Abs(gap) < .2f, "Walkthrough lost ground: " + gap);
                    foreach (var station in walker.stations)
                        if (!station.Active && Mathf.Abs(walker.transform.position.z - station.transform.position.z) < .2f)
                            Require(walker.Interact(), "Nearby station did not respond");
                }
                else if (wallSteps == 0)
                {
                    foreach (var station in walker.stations) Require(station.Active, "Station not reached: " + station.purpose);
                    Capture("play-finish.png");
                    wallStart = walker.transform.position; wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    wall.name = "Temporary collision review wall"; wall.transform.position = wallStart + new Vector3(1.1f, 1, 0);
                    wall.transform.localScale = new Vector3(.2f, 3, 3); Physics.SyncTransforms(); wallSteps++;
                }
                else if (wallSteps <= 100)
                {
                    walker.MoveWorld(Vector3.right, 1f / 60); wallSteps++;
                    Require(walker.transform.position.x < wallStart.x + .85f, "Character passed through collider");
                }
                else
                {
                    Require(walker.transform.position.x > wallStart.x + .4f, "Collision check never approached wall");
                    UnityEngine.Object.Destroy(wall); walker.ResetWalk();
                    Require(Vector3.Distance(walker.transform.position, spawn) < .001f, "Reset lost initial pose");
                    foreach (var station in walker.stations) Require(!station.Active, "Reset retained station state");
                    Capture("play-reset.png");
                    string result = "PASS: saved forest scene; CharacterController route traversal " + steps + " steps; 3 station callbacks; blocking collider; reset pose/state; runtime errors=" + errors + "; max sampled ground gap=" + maxGroundGap + "m. Scripted movement through the same runtime method, not physical keyboard/headset acceptance.\n";
                    File.WriteAllText(Output + "/play-result.txt", result);
                    Debug.Log("FOREST_BLOCKOUT_PLAY_PASS " + result); Finish(0); return;
                }
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception); File.WriteAllText(Output + "/play-result.txt", "FAIL: " + exception); Finish(1);
        }
    }
    static void Finish(int code) { SessionState.SetInt(Key + ".Exit", code); EditorApplication.ExitPlaymode(); }
    static void Capture(string name)
    {
        Directory.CreateDirectory(ForestBlockoutBuilder.Output);
        ForestBlockoutBuilder.Capture(Camera.main, name);
        if (Output != ForestBlockoutBuilder.Output) File.Copy(ForestBlockoutBuilder.Output + "/" + name, Output + "/" + name, true);
    }
}
