using System;
using System.Reflection;
using MagicMR;
using UnityEngine;

public static class ParkGestureSafetyChecks
{
    public static void Run()
    {
        if(!ParkHandTracking.SampleShapeAssetsAvailable)throw new Exception("Pinned sample hand shape assets failed to load");
        foreach(var name in new[]{"Open Hand Shape","Fist Hand Shape"}) {
            var shape=Resources.Load<UnityEngine.XR.Hands.Gestures.XRHandShape>("MagicMR/HandShapes/"+name);
            if(!shape || shape.fingerShapeConditions.Count!=5)throw new Exception("Incomplete sample shape conditions: "+name);
        }
        if(ParkHandTracking.MatchesSampleShape(default,true) || ParkHandTracking.MatchesSampleShape(default,false))throw new Exception("Untracked hand matched sample pose");
        Debug.Log("PARK_SAMPLE_SHAPE_ASSETS_PASS: compatible assets and missing-hand rejection; tracked PICO conditions NOT validated");
        var root=new GameObject("Gesture safety fixture");
        var controller=root.AddComponent<ParkController>();controller.scenario=ParkScenario.Bugs;
        var foreign=new GameObject("Unregistered virtual enemy").AddComponent<ParkActor>();
        try {
            var hold=new ParkStableHold();int actions=0;
            var pinch=new ParkPinchLifecycle();
            ParkPinchPhase Pinch(bool pressed,float time,bool allowed=true)=>pinch.Step(pressed,allowed,time,.08f,.8f,.25f);
            if(Pinch(true,0)!=ParkPinchPhase.Started || Pinch(true,.1f)!=ParkPinchPhase.Holding || Pinch(false,.2f)!=ParkPinchPhase.Confirmed || Pinch(false,.21f)!=ParkPinchPhase.None)
                throw new Exception("Pinch lifecycle did not confirm once on release");
            Pinch(true,.3f);if(Pinch(false,.31f)!=ParkPinchPhase.Ended)throw new Exception("Short pinch confirmed");
            Pinch(true,.4f);Pinch(true,.6f);Pinch(true,.8f);Pinch(true,1);Pinch(true,1.2f);
            if(Pinch(false,1.4f)!=ParkPinchPhase.Ended)throw new Exception("Overlong pinch confirmed");
            Pinch(true,1.5f);Pinch(true,1.6f,false);
            if(Pinch(false,1.7f)==ParkPinchPhase.Confirmed)throw new Exception("UI-owned pinch release confirmed");
            Pinch(true,1.8f);if(Pinch(false,2.5f)!=ParkPinchPhase.Cancelled)throw new Exception("Interrupted pinch confirmed");
            Pinch(false,3,false);
            if(Pinch(true,3.1f)!=ParkPinchPhase.Started || Pinch(false,3.2f)!=ParkPinchPhase.Confirmed)
                throw new Exception("Valid UI-owned release swallowed first new scene pinch");
            Debug.Log("PARK_PINCH_LIFECYCLE_PASS: release once, short/long hold, UI ownership and sampling gap; synthetic inputs");
            if(hold.Step(true,Vector3.zero,0,.3f,.035f)!=ParkHoldPhase.Started)throw new Exception("Hold did not begin");
            for(int i=1;i<=120;i++) {
                if(hold.Step(true,Vector3.right*.01f,i/60f,.3f,.035f)==ParkHoldPhase.Ready) {actions++;hold.Consume();}
            }
            if(actions!=1)throw new Exception("Continuous hold repeated discrete action");
            if(hold.Step(false,Vector3.zero,2.1f,.3f,.035f)!=ParkHoldPhase.Ended)throw new Exception("Release did not end hold");
            hold.Step(true,Vector3.zero,3,.3f,.035f);
            if(hold.Step(true,Vector3.right*.08f,3.2f,.3f,.035f)!=ParkHoldPhase.Holding || hold.Began!=3.2f)
                throw new Exception("Palm drift did not restart stable interval");
            if(hold.Step(true,Vector3.right*.08f,3.4f,.3f,.035f)==ParkHoldPhase.Ready)throw new Exception("Drift fired before new stable interval");
            if(hold.Step(true,Vector3.right*.08f,3.6f,.3f,.035f)!=ParkHoldPhase.Ready)throw new Exception("Stable palm failed after drift");
            hold.Consume();hold.Reset();
            if(hold.Active || hold.Consumed)throw new Exception("Interrupted hold retained state");
            hold.Step(true,Vector3.zero,4,.3f,.035f);
            if(hold.Step(true,new Vector3(float.NaN,0,0),4.1f,.3f,.035f)!=ParkHoldPhase.Ended)throw new Exception("Invalid joint pose retained hold");
            hold.Step(true,Vector3.zero,5,.3f,.035f);
            if(hold.Step(true,Vector3.zero,6,.3f,.035f)!=ParkHoldPhase.Ended || hold.Active)
                throw new Exception("Sampling gap counted as continuous hold");
            hold.Step(true,Vector3.zero,7,.3f,.035f);
            if(hold.Step(true,Vector3.zero,6.9f,.3f,.035f)!=ParkHoldPhase.Ended)
                throw new Exception("Backwards sample clock retained hold");
            if(hold.Step(true,Vector3.zero,8,float.NaN,.035f)!=ParkHoldPhase.None)
                throw new Exception("Invalid hold configuration began interaction");
            Debug.Log("PARK_HOLD_LIFECYCLE_PASS: synthetic 60Hz hold, release, drift, reset and invalid pose; not PICO trajectory");
            controller.selected=foreign;
            if(controller.ConfirmVirtualAttack(null,Vector3.zero,true))throw new Exception("Empty ray attacked selected enemy");
            if(controller.ConfirmVirtualAttack(foreign,Vector3.zero,true))throw new Exception("Foreign actor accepted attack");
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var headGate=typeof(ParkController).GetMethod("AcceptMotionHeadPose",flags);
            bool Head(Vector3 p,Quaternion q,float t)=>(bool)headGate.Invoke(controller,new object[]{p,q,t});
            if(Head(Vector3.zero,Quaternion.identity,0))throw new Exception("Unsampled head accepted motion gestures");
            Head(Vector3.zero,Quaternion.identity,.1f);
            if(!Head(Vector3.zero,Quaternion.identity,.21f))throw new Exception("Stable head did not rearm motion gestures");
            if(Head(Vector3.right*.1f,Quaternion.identity,.31f))throw new Exception("Fast headset translation accepted motion gestures");
            Head(Vector3.right*.1f,Quaternion.identity,.41f);
            if(!Head(Vector3.right*.1f,Quaternion.identity,.52f))throw new Exception("Head settling did not recover");
            if(Head(Vector3.right*.1f,Quaternion.Euler(0,15,0),.62f))throw new Exception("Fast head rotation accepted motion gestures");
            if(Head(new Vector3(float.NaN,0,0),Quaternion.identity,.72f))throw new Exception("Invalid head pose accepted motion gestures");
            if(Head(Vector3.zero,new Quaternion(0,0,0,0),.82f) || !controller.MotionGestureStatus.Contains("invalid"))throw new Exception("Invalid head rotation accepted or lacked feedback");
            Debug.Log("PARK_HEAD_MOTION_GATE_PASS: translation, rotation, settling and invalid pose; synthetic inputs, PICO thresholds pending");
            var fists=typeof(ParkController).GetMethod("StepFists",flags);
            bool Fists(bool r,bool l,float time,ParkAction? expected=null) {
                object[] args={r,l,time,ParkAction.Undo};bool fired=(bool)fists.Invoke(controller,args);
                if(expected.HasValue && (!fired || (ParkAction)args[3]!=expected.Value))throw new Exception("Wrong fist command");
                return fired;
            }
            Fists(true,true,0);
            if(Fists(true,false,.8f))throw new Exception("Interrupted pair fell back to solo undo");
            Fists(false,false,.9f);Fists(true,false,1);
            Fists(true,false,1.8f,ParkAction.Undo);
            if(Fists(true,false,2))throw new Exception("Held solo fist repeated undo");
            Fists(false,false,2.1f);Fists(true,true,2.2f);Fists(true,true,2.8f,ParkAction.Reset);
            if(Fists(true,true,3.5f) || Fists(true,false,3.6f))throw new Exception("Held reset repeated or became undo");
            Fists(false,false,3.7f);
            Debug.Log("PARK_FIST_LIFECYCLE_PASS: interrupted pair, solo undo once and pair reset once; synthetic inputs");
            var sample=typeof(ParkController).GetMethod("AcceptHandSample",flags);
            if(!(bool)sample.Invoke(controller,new object[]{1f}) || !(bool)sample.Invoke(controller,new object[]{1.1f}))throw new Exception("Continuous hand samples rejected");
            typeof(ParkController).GetField("fistHeld",flags).SetValue(controller,true);
            if((bool)sample.Invoke(controller,new object[]{2f}) || (bool)typeof(ParkController).GetField("fistHeld",flags).GetValue(controller) || !(bool)typeof(ParkController).GetField("requireGestureRelease",flags).GetValue(controller))throw new Exception("Hand gap retained fist timer or skipped release protection");
            if((bool)sample.Invoke(controller,new object[]{1.9f}) || (bool)sample.Invoke(controller,new object[]{float.NaN}))throw new Exception("Invalid hand sample clock accepted");
            Debug.Log("PARK_SAMPLE_CONTINUITY_PASS: gap, backwards clock and invalid sample cancel shared interaction; synthetic inputs");
            var actors=(System.Collections.Generic.Dictionary<string,ParkActor>)typeof(ParkController).GetField("actors",flags).GetValue(controller);
            var far=new GameObject("Far virtual enemy").AddComponent<ParkActor>();
            try {
                foreign.tracked=true;far.tracked=true;
                foreign.transform.position=new Vector3(.1f,0,1);far.transform.position=Vector3.forward*2;
                actors["far"]=far;actors["near"]=foreign;
                var point=typeof(ParkController).GetMethod("Point",flags);
                if(point.Invoke(controller,new object[]{Vector3.zero,Vector3.forward,false})!=foreign)
                    throw new Exception("Pointing chose farther collinear enemy over nearer candidate");
                controller.selected=far;
                typeof(ParkController).GetField("pullArmed",flags).SetValue(controller,true);
                typeof(ParkController).GetField("openPalmSince",flags).SetValue(controller,0f);
                if(point.Invoke(controller,new object[]{Vector3.zero,Vector3.forward,true})!=foreign || controller.selected!=foreign || (bool)typeof(ParkController).GetField("pullArmed",flags).GetValue(controller) || (float)typeof(ParkController).GetField("openPalmSince",flags).GetValue(controller)>=0)
                    throw new Exception("Target selection retained previous target hold or pull state");
                foreign.SetTracked(false);
                if(point.Invoke(controller,new object[]{Vector3.zero,Vector3.forward,false})!=far)
                    throw new Exception("Lost target remained pointing candidate");
                if(point.Invoke(controller,new object[]{Vector3.zero,new Vector3(float.NaN,0,1),false})!=null)
                    throw new Exception("Nonfinite pointing direction accepted target");
            } finally {actors.Clear();UnityEngine.Object.DestroyImmediate(far.gameObject);}
            var ui=root.AddComponent<ParkSharedInterface>();ui.controller=controller;
            var finiteUi=typeof(ParkSharedInterface).GetMethod("FiniteUiPoint",BindingFlags.Static|BindingFlags.NonPublic);
            foreach(var invalid in new[]{new Vector3(float.NaN,0,0),new Vector3(0,float.PositiveInfinity,0),new Vector3(0,0,float.NegativeInfinity)})
                if((bool)finiteUi.Invoke(null,new object[]{invalid}))throw new Exception("Invalid UI joint coordinate accepted");
            if(!(bool)finiteUi.Invoke(null,new object[]{Vector3.zero}))throw new Exception("Finite UI joint coordinate rejected");
            var processUi=typeof(ParkSharedInterface).GetMethod("ProcessUiHand",flags);
            // The same unavailable-hand path is used by either side; this is not a tracked-hand fixture.
            object[] arguments={default(UnityEngine.XR.Hands.XRHand),true,true};
            if(processUi.Invoke(ui,arguments)!=null || (bool)arguments[1] || (bool)arguments[2])
                throw new Exception("Unavailable UI hand retained click ownership or arming");
            var reset=typeof(ParkController).GetMethod("ResetHandInteraction",flags);
            typeof(ParkController).GetField("pinchHeld",flags).SetValue(controller,true);
            if(!controller.AnimationBusy)throw new Exception("Held scene gesture did not block automatic routing");
            controller.SetRightHandUiCapture(true);
            if(controller.AnimationBusy)throw new Exception("UI-owned pinch blocked candidate selection");
            controller.SetRightHandUiCapture(false);
            foreach(var name in new[]{"pinchHeld","fistHeld","leftFistHeld","pullArmed","clapArmed","openPalmFired"})
                typeof(ParkController).GetField(name,flags).SetValue(controller,true);
            typeof(ParkController).GetField("pinchTarget",flags).SetValue(controller,foreign);
            reset.Invoke(controller,null);
            foreach(var name in new[]{"pinchHeld","fistHeld","leftFistHeld","pullArmed","clapArmed","openPalmFired"})
                if((bool)typeof(ParkController).GetField(name,flags).GetValue(controller))throw new Exception("Reset retained "+name);
            if(typeof(ParkController).GetField("pinchTarget",flags).GetValue(controller)!=null)throw new Exception("Reset retained attack target");
            if(!(bool)typeof(ParkController).GetField("requireGestureRelease",flags).GetValue(controller))throw new Exception("Interrupted gesture could re-arm without release");
            Debug.Log("PARK_GESTURE_SAFETY_PASS: empty/foreign attack rejected and interrupted gesture state cleared; editor fixture, not device validation");
        } finally {UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(foreign.gameObject);}
    }
}
