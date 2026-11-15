using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class NaturalPetAnimatorBuilder
{
    public static void Configure(AnimatorController controller,AnimatorState idle,AnimatorState sniff,AnimationClip clip)
    {
        var machine=controller.layers[0].stateMachine;
        var walk=machine.states.Select(s=>s.state).FirstOrDefault(s=>s.name=="Walk in place")??machine.states.Select(s=>s.state).FirstOrDefault(s=>s.name=="Source walk in place");
        if(!walk)walk=machine.AddState("Walk in place");walk.name="Walk in place";walk.motion=clip;
        foreach(var stale in machine.states.Select(s=>s.state).Where(s=>s!=walk&&s.name=="Source walk in place").ToArray()){machine.RemoveState(stale);Object.DestroyImmediate(stale,true);}
        if(!controller.parameters.Any(p=>p.name=="Walking"))controller.AddParameter("Walking",AnimatorControllerParameterType.Bool);
        foreach(var state in new[]{idle,sniff,walk})foreach(var transition in state.transitions)state.RemoveTransition(transition);
        var start=idle.AddTransition(walk);start.hasExitTime=false;start.duration=.15f;start.AddCondition(AnimatorConditionMode.If,0,"Walking");
        var stop=walk.AddTransition(idle);stop.hasExitTime=false;stop.duration=.15f;stop.AddCondition(AnimatorConditionMode.IfNot,0,"Walking");
        foreach(var state in new[]{idle,walk}){var feed=state.AddTransition(sniff);feed.hasExitTime=false;feed.duration=.08f;feed.AddCondition(AnimatorConditionMode.If,0,"Sniff");}
        var recover=sniff.AddTransition(idle);recover.hasExitTime=true;recover.exitTime=.95f;recover.duration=.08f;
        machine.defaultState=idle;EditorUtility.SetDirty(controller);
    }
}
