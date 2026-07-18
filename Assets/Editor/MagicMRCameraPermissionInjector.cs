using System.IO;
using System.Text.RegularExpressions;
using UnityEditor.Android;
using UnityEngine;

namespace MagicMR.Editor
{
    /// <summary>
    /// PICO Video See-Through (VST) requires android.permission.CAMERA, but
    /// neither Unity nor the PICO SDK add it to the generated manifest by
    /// default (confirmed via `adb shell dumpsys package` showing it missing
    /// from the installed app). Rather than hand-maintain a full custom
    /// AndroidManifest.xml (which previously wiped out the launcher Activity),
    /// this appends just the one permission line to whatever manifest Unity
    /// and the PICO SDK already generated for each Gradle module.
    /// </summary>
    public class MagicMRCameraPermissionInjector : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 999;

        const string k_PermissionName = "android.permission.CAMERA";
        const string k_PermissionTag = "<uses-permission android:name=\"" + k_PermissionName + "\" />";

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            var manifestPath = Path.Combine(path, "src/main/AndroidManifest.xml");
            if (!File.Exists(manifestPath))
                return;

            var manifest = File.ReadAllText(manifestPath);
            if (manifest.Contains(k_PermissionName))
                return;

            var match = Regex.Match(manifest, @"<manifest[^>]*>");
            if (!match.Success)
                return;

            var insertionPoint = match.Index + match.Length;
            manifest = manifest.Insert(insertionPoint, "\n    " + k_PermissionTag);
            File.WriteAllText(manifestPath, manifest);

            Debug.Log($"[MagicMR] Injected {k_PermissionName} into {manifestPath} for PICO VST.");
        }
    }
}
