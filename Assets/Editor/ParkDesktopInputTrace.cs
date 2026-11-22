using System;
using System.Collections.Generic;
using System.IO;
using MagicMR;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Opt-in, Editor-only diagnostic. Records only preview keys 2/T and window types,
// never text input; bounded to 128 records and writes once when Play Mode ends.
[InitializeOnLoad]
public static class ParkDesktopInputTrace
{
    const string Key="MagicMR.DesktopInputTrace";
    [Serializable] sealed class Entry {public string kind,focus,device,feedback;public double time;public bool digit2,t,keyboardEnabled;public int revision;}
    [Serializable] sealed class Report {public string scope="Native desktop input diagnostic only; not a headset/gesture acceptance";public bool truncated;public Entry[] entries;}
    static readonly List<Entry> entries=new List<Entry>(128);static string focus;static bool truncated;
    static ParkDesktopInputTrace(){InputSystem.onEvent+=OnInput;EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=PlayChanged;}
    [MenuItem("MagicMR/Checks/Arm desktop input trace")]
    public static void Arm(){entries.Clear();focus=null;truncated=false;SessionState.SetBool(Key,true);Add("armed",false,false);Debug.Log("PARK_DESKTOP_INPUT_TRACE_ARMED: up to 128 records, only 2/T preview keys and focus types, saved when exiting Play");}
    static void Add(string kind,bool two,bool t)
    {
        if(entries.Count==128){truncated=true;return;}
        var controller=Application.isPlaying?UnityEngine.Object.FindFirstObjectByType<ParkController>():null;var actor=controller?controller.selected:null;
        entries.Add(new Entry{kind=kind,time=EditorApplication.timeSinceStartup,focus=EditorWindow.focusedWindow?EditorWindow.focusedWindow.GetType().FullName:"none",device=Keyboard.current?.layout??"none",keyboardEnabled=Keyboard.current!=null&&Keyboard.current.enabled,digit2=two,t=t,revision=actor?actor.Revision:-1,feedback=actor?actor.Feedback:"no selected actor"});
    }
    static void OnInput(InputEventPtr data,InputDevice device)
    {
        if(!SessionState.GetBool(Key,false)||!Application.isPlaying||!(device is Keyboard keyboard)||!data.IsA<StateEvent>())return;
        Add("keyboard-state",keyboard.digit2Key.ReadValueFromEvent(data)>.5f,keyboard.tKey.ReadValueFromEvent(data)>.5f);
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false)||!Application.isPlaying)return;
        string current=EditorWindow.focusedWindow?EditorWindow.focusedWindow.GetType().FullName:"none";
        if(current!=focus){focus=current;Add("focus",false,false);}
    }
    static void PlayChanged(PlayModeStateChange value)
    {
        if(!SessionState.GetBool(Key,false)||value!=PlayModeStateChange.ExitingPlayMode)return;
        Add("exiting-play",false,false);SessionState.SetBool(Key,false);Directory.CreateDirectory(".codex-tmp/desktop-input");
        File.WriteAllText(".codex-tmp/desktop-input/trace.json",JsonUtility.ToJson(new Report{truncated=truncated,entries=entries.ToArray()},true));
        Debug.Log("PARK_DESKTOP_INPUT_TRACE_SAVED: "+entries.Count+" bounded records; inspect focus and actual raw key states, not a pass assertion");
    }
}
