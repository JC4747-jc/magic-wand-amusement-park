using System;
using System.IO;
using System.Linq;
using MagicMR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Exercises the controller actions used by circle/swipe in the saved Pet scene.
// Synthetic actions are deliberately not reported as physical hand recognition.
[InitializeOnLoad]
public static class HciPetDreamChecks
{
    const string Key = "MagicMR.HciPetDreamChecks";
    static string Output => Path.GetFullPath(".codex-tmp/hci-pet-dream");
    static ParkController controller;
    static ParkActor actor;
    static Transform bubble;
    static int stage, species, errors;
    static double next, started;
    static Vector3 anchorPosition;
    static Quaternion anchorRotation;
    static HciPetDreamChecks() { EditorApplication.update += Tick; Application.logMessageReceived += Log; }
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Idle editor required");
        Directory.CreateDirectory(Output);
        File.WriteAllText(Output + "/result.txt", "RUNNING; not acceptance\n");
        SessionState.SetString(Key + ".Run", Guid.NewGuid().ToString("N"));
        SessionState.SetBool(Key, true); SessionState.SetBool(Key + ".Done", false);
        EditorSceneManager.OpenScene("Assets/Scenes/ParkPet.unity");
        EditorApplication.EnterPlaymode();
    }
    static void Require(bool ok, string why) { if (!ok) throw new Exception(why); }
    static void Log(string message, string stack, LogType type)
    {
        if (SessionState.GetBool(Key, false) && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)) errors++;
    }
    static void Later(int value, double seconds = 1.4) { stage = value; next = EditorApplication.timeSinceStartup + seconds; }
    static void Act(ParkAction action) => Require(controller.Dispatch(action, actor.transform.position, true), action + " rejected: " + controller.InteractionStatus);
    static void Capture(string name)
    {
        var camera = Camera.main; var previous = camera.targetTexture; var active = RenderTexture.active;
        var rt = new RenderTexture(800, 600, 24); var pixels = new Texture2D(800, 600, TextureFormat.RGB24, false);
        try { camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt; pixels.ReadPixels(new Rect(0, 0, 800, 600), 0, 0); pixels.Apply(); File.WriteAllBytes(Output + "/" + actor.species + "-" + name + ".png", pixels.EncodeToPNG()); }
        finally { camera.targetTexture = previous; RenderTexture.active = active; UnityEngine.Object.DestroyImmediate(pixels); UnityEngine.Object.DestroyImmediate(rt); }
    }
    static bool Visible => bubble && bubble.gameObject.activeInHierarchy && bubble.localScale.sqrMagnitude > .001f;
    static void Tick()
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (!EditorApplication.isPlaying)
        {
            if (SessionState.GetBool(Key + ".Done", false) && !EditorApplication.isPlayingOrWillChangePlaymode) { SessionState.SetBool(Key, false); EditorApplication.Exit(0); }
            return;
        }
        try
        {
            if (stage == 0)
            {
                started = EditorApplication.timeSinceStartup;
                controller = UnityEngine.Object.FindFirstObjectByType<ParkController>(); Require(controller && controller.scenario == ParkScenario.Pet, "Saved Pet scene missing");
                Camera.main.GetComponentsInChildren<Button>(false).Single(b => b.name == "Virtual test").onClick.Invoke(); Later(1); return;
            }
            Require(EditorApplication.timeSinceStartup - started < 80, "Pet flow timeout");
            Require(errors == 0, "Runtime error log");
            if (EditorApplication.timeSinceStartup < next) return;
            if (stage == 1)
            {
                actor = controller.selected; Require(actor && actor.species == (species == 0 ? "Cat" : "Dog"), "Wrong pet");
                bubble = actor.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Dream bubble");
                anchorPosition = actor.transform.position; anchorRotation = actor.transform.rotation;
                Require(!Visible, "Initial bubble visible");
                Require(!controller.Dispatch(ParkAction.Awaken, actor.transform.position, false), "Invalid hand accepted");
                Require(!controller.Dispatch(ParkAction.Awaken, actor.transform.position + Vector3.right * 20, true), "Out-of-range hand accepted");
                Act(ParkAction.Awaken); Later(2);
            }
            else if (stage == 2) { Require(Visible && !actor.Busy, "HCI B/circle did not open dream bubble"); Capture("B-open"); Act(ParkAction.Awaken); Later(3); }
            else if (stage == 3) { Require(Visible, "Repeated B incorrectly toggled dream closed"); Act(ParkAction.Wind); Later(4); }
            else if (stage == 4) { Require(!Visible && !actor.Busy, "HCI C/swipe did not pop dream"); Capture("C-pop"); Require(!controller.Dispatch(ParkAction.Wind, actor.transform.position, true), "Empty dream pop accepted"); Act(ParkAction.Undo); Later(5, .5); }
            else if (stage == 5) { Require(Visible, "Undo did not restore pre-pop dream"); Act(ParkAction.Reward); Later(6); }
            else if (stage == 6) { Require(!Visible && actor.Affection == 1, "HCI D affection/pop failed"); Act(ParkAction.Undo); Later(7, .5); }
            else if (stage == 7) { Require(Visible && actor.Affection == 0, "Reward undo failed"); Act(ParkAction.Stop); Later(8, .6); }
            else if (stage == 8) { Require(!Visible, "Stop retained dream"); Act(ParkAction.Awaken); Later(9); }
            else if (stage == 9) { Require(Visible, "Dream could not reopen after Stop"); Act(ParkAction.Reset); Later(10, .5); }
            else if (stage == 10)
            {
                Require(!Visible && actor.Affection == 0, "Reset retained dream state"); Capture("reset");
                actor.SetTracked(false); Require(!controller.Dispatch(ParkAction.Awaken, actor.transform.position, true), "Lost target accepted B"); actor.SetTracked(true);
                Require(actor.transform.position == anchorPosition && actor.transform.rotation == anchorRotation, "Magic moved tracking anchor");
                if (species++ == 0) { Require(controller.SelectVirtualPet("Dog"), "Dog selection failed"); Later(1); }
                else
                {
                    File.WriteAllText(Output + "/result.txt", "PASS " + SessionState.GetString(Key + ".Run", "") + ": saved ParkPet, Cat+Dog; B opens/idempotent, C pops/rejects empty, undo, D affection/undo, Stop/reopen, Reset, invalid/far/lost gates, anchor unchanged; runtime errors=0; six actual camera PNGs. Synthetic controller actions; no physical gestures, YOLO or PICO acceptance.\n");
                    SessionState.SetBool(Key + ".Done", true); EditorApplication.ExitPlaymode();
                }
            }
        }
        catch (Exception e) { SessionState.SetBool(Key, false); File.WriteAllText(Output + "/result.txt", "FAIL: " + e); Debug.LogException(e); EditorApplication.Exit(1); }
    }
}
