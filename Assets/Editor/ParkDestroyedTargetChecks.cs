using System;
using System.Collections.Generic;
using System.Reflection;
using MagicMR;
using UnityEditor;
using UnityEngine;

// Regression for actor-first scene teardown observed while leaving native Editor Play Mode.
public static class ParkDestroyedTargetChecks
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
    static object Call(ParkController controller,string name,params object[] arguments)=>typeof(ParkController).GetMethod(name,Private).Invoke(controller,arguments);
    public static void Run()
    {
        var root=new GameObject("Destroyed-target controller fixture");var target=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MagicMR/Park/ParkTree.prefab"));var replacement=new GameObject("Replacement target");
        var controller=root.AddComponent<ParkController>();var actor=target.GetComponent<ParkActor>();actor.Build();var next=replacement.AddComponent<ParkActor>();
        var actors=(Dictionary<string,ParkActor>)typeof(ParkController).GetField("actors",Private).GetValue(controller);
        try
        {
            controller.selected=actor;UnityEngine.Object.DestroyImmediate(target);
            Require(!actor&&!ReferenceEquals(actor,null)&&ReferenceEquals(controller.selected,actor),"Fixture failed to retain a Unity destroyed-object reference");
            Call(controller,"ResetHandState",true);
            Call(controller,"OnDisable");Require(ReferenceEquals(controller.selected,null),"Teardown retained destroyed selection");
            controller.selected=actor;next.tracked=true;next.instanceId="replacement";replacement.transform.position=Vector3.forward;
            actors.Add("replacement",next);var selected=Call(controller,"Point",Vector3.zero,Vector3.forward,true);
            Require(ReferenceEquals(selected,next)&&ReferenceEquals(controller.selected,next),"Destroyed old selection prevented selecting a live replacement");
            Debug.Log("PARK_DESTROYED_TARGET_PASS: retained Unity fake-null selection, hand interruption, actor-first controller disable and live replacement selection; native Play exit originally reproduced separately");
        }
        finally
        {
            actors.Clear();controller.selected=null;
            if(target)UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(replacement);UnityEngine.Object.DestroyImmediate(root);
        }
    }
}
