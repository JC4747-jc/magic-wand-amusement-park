using System;
using System.Collections.Generic;
using MagicMR;
using UnityEngine;

public static class TabletopGestureChecks
{
    static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    static List<Vector3> Circle(float angle = 360, bool horizontal = false)
    {
        var points = new List<Vector3>();
        for (int i=0; i<=40; i++)
        {
            float a = i / 40f * angle * Mathf.Deg2Rad;
            points.Add(horizontal ? new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*.04f : new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*.04f);
        }
        return points;
    }
    public static void Run()
    {
        var summon = new TabletopSummonGesture();
        Vector3 thumbBase = Vector3.zero, thumbJoint = Vector3.up * .025f, thumbTip = Vector3.up * .05f;
        Vector3 curledIndex = new Vector3(.03f, -.015f, 0);
        Require(TabletopSummonGesture.IsThumbsUp(.55f,.6f,.6f,.6f,thumbBase,thumbJoint,thumbTip,curledIndex), "Thumbs-up rejected");
        var tilt = Quaternion.Euler(0, 25, 35);
        Vector3 offset = new Vector3(1, 2, 3);
        Require(TabletopSummonGesture.IsThumbsUp(.6f,.6f,.6f,.6f,offset,offset + tilt * thumbJoint,
            offset + tilt * thumbTip,offset + tilt * curledIndex), "Naturally tilted thumbs-up rejected");
        Require(!TabletopSummonGesture.IsThumbsUp(.95f,.95f,.95f,.95f,thumbBase,thumbJoint,thumbTip,curledIndex), "Open palm summoned");
        Require(!TabletopSummonGesture.IsThumbsUp(.95f,.6f,.6f,.6f,thumbBase,thumbJoint,thumbTip,curledIndex), "Pointing hand summoned");
        Require(!TabletopSummonGesture.IsThumbsUp(.6f,.6f,.6f,.6f,thumbBase,-thumbJoint,-thumbTip,curledIndex), "Thumbs-down summoned");
        Require(!TabletopSummonGesture.IsThumbsUp(.6f,.6f,.6f,.6f,thumbBase,Vector3.right*.025f,Vector3.right*.05f,curledIndex), "Sideways thumb summoned");
        Require(!TabletopSummonGesture.IsThumbsUp(.6f,.6f,.6f,.6f,thumbBase,thumbJoint,new Vector3(.015f,.02f,0),curledIndex), "Folded thumb/fist summoned");
        Require(!TabletopSummonGesture.IsThumbsUp(.6f,.6f,.6f,.6f,thumbBase,thumbJoint,thumbTip,thumbTip), "Pinch summoned");
        Require(!TabletopSummonGesture.IsThumbsUp(float.NaN,.6f,.6f,.6f,thumbBase,thumbJoint,thumbTip,curledIndex) &&
            !TabletopSummonGesture.IsThumbsUp(.6f,.6f,.6f,.6f,thumbBase,thumbJoint,new Vector3(float.NaN,0,0),curledIndex), "Invalid joints summoned");
        for (int n = 0; n < 15; n++)
            Require(!summon.Step(false, true, Vector3.zero, n * .1f), "Unlocated target could be summoned");
        summon.Reset();
        for (int n = 0; n < 3; n++)
            Require(!summon.Step(true, true, Vector3.zero, n * .1f), "Short thumbs-up summoned early");
        Require(summon.Step(true, true, Vector3.zero, .31f), "Stable thumbs-up failed to summon");
        Require(!summon.Step(true, true, Vector3.zero, .9f), "Summon fired twice");
        Require(summon.BlockTransforms(true, .9f) && summon.AwaitingRelease, "Summon hand could trigger a transform");
        summon.CancelDwell(); // Tracking loss cannot bypass the required release.
        Require(summon.BlockTransforms(true, 2) && summon.AwaitingRelease, "Tracking gap bypassed release");
        for (int n = 0; n < 4; n++) summon.BlockTransforms(false, 2.1f + n * .1f);
        Require(!summon.AwaitingRelease && !summon.BlockTransforms(true, 2.5f), "Opening hand did not unlock transforms");
        summon.Reset();
        for (int n = 0; n < 20; n++)
            Require(!summon.Step(true, true, Vector3.right * n * .02f, n * .1f), "Moving thumbs-up summoned");
        summon.Reset();
        for (int n = 0; n < 20; n++)
            Require(!summon.Step(true, false, Vector3.zero, n * .1f), "Wrong hand pose accumulated dwell");
        summon.Reset();
        for (int n = 0; n < 20; n++)
            Require(!summon.Step(true, true, Vector3.zero, n * .2f), "Tracking gaps counted toward summon hold");
        summon.Reset();
        for (int n = 0; n < 2; n++) summon.Step(true, true, Vector3.zero, n * .1f);
        summon.Step(false, true, Vector3.zero, .4f);
        Require(!summon.Step(true, true, Vector3.zero, .6f) && summon.Progress == 0, "Stale target retained summon progress");
        summon.Reset();
        Require(!summon.AwaitingRelease && summon.Progress == 0, "Reset retained summon state");
        Debug.Log("TABLETOP_THUMBS_UP_CHECKS_PASS");
        var rapid = new TabletopGestureRules();
        rapid.Step(new TabletopGestureRules.Sample {time=0, pinch=.02f});
        rapid.Step(new TabletopGestureRules.Sample {time=.1f, pinch=.02f});
        Require(rapid.Step(new TabletopGestureRules.Sample {time=.2f, pinch=.08f, open=true}) == EditDimension.Appearance,
            "First rapid gesture failed");
        for (int n=3;n<=5;n++) Require(rapid.Step(new TabletopGestureRules.Sample {time=n*.1f,pinch=.08f,fist=true})==EditDimension.None,
            "Gesture repeated inside shortened cooldown");
        rapid.Step(new TabletopGestureRules.Sample {time=.56f,pinch=.02f,fist=true});
        rapid.Step(new TabletopGestureRules.Sample {time=.66f,pinch=.02f,fist=true});
        rapid.Step(new TabletopGestureRules.Sample {time=.76f,pinch=.02f,fist=true});
        Require(rapid.Step(new TabletopGestureRules.Sample {time=.86f,pinch=.08f,open=true})==EditDimension.Deconstruction,
            "Next gesture stayed blocked after 0.35 seconds");
        Debug.Log("TABLETOP_RAPID_TRANSITION_CHECKS_PASS");
        Require(TabletopGestureRules.IsClosedCircle(Circle()), "Vertical circle rejected");
        Require(TabletopGestureRules.IsClosedCircle(Circle(360,true)), "Horizontal circle rejected");
        Require(!TabletopGestureRules.IsClosedCircle(Circle(200)), "Open arc recognized as circle");
        var line = new List<Vector3>();
        for(int i=0;i<40;i++) line.Add(Vector3.right * (i<20?i:40-i)*.004f);
        Require(!TabletopGestureRules.IsClosedCircle(line), "Back-and-forth swipe recognized as circle");
        var noisy = new List<Vector3>();
        for(int i=0;i<40;i++) noisy.Add(new Vector3(i*.002f, i%2*.04f,0));
        Require(!TabletopGestureRules.IsClosedCircle(noisy), "Zigzag recognized as circle");

        var rules = new TabletopGestureRules();
        var s = new TabletopGestureRules.Sample { pinch=.02f, fist=true };
        for(int i=0;i<=10;i++) { s.time=i*.1f; Require(rules.Step(s)==EditDimension.None, "Fist fired before opening"); }
        s.time=1.1f; s.fist=false; s.open=true; s.pinch=.08f;
        Require(rules.Step(s)==EditDimension.Deconstruction, "Held fist must produce flower, not pinch");
        s.time=1.15f;
        Require(rules.Step(s)==EditDimension.None, "One opening emitted a second gesture");

        rules.Reset(); s=new TabletopGestureRules.Sample {pinch=.02f};
        for(int i=0;i<5;i++) { s.time=i*.1f; rules.Step(s); }
        s.time=.5f; s.pinch=.08f; s.open=true;
        Require(rules.Step(s)==EditDimension.Appearance, "Normal pinch release failed");
        rules.Reset(); s=new TabletopGestureRules.Sample {pinch=.02f}; rules.Step(s);
        s.time=.5f; s.pinch=.08f;
        Require(rules.Step(s)==EditDimension.None, "Tracking gap falsely completed pinch");

        rules.Reset(); s=new TabletopGestureRules.Sample {pinch=.08f, open=true};
        EditDimension result=EditDimension.None;
        for(int i=0;i<=6;i++) { s.time=i*.03f; s.palm=Vector3.right*i*.025f; var d=rules.Step(s); if(d!=EditDimension.None)result=d; }
        Require(result==EditDimension.Rule, "Open palm horizontal swipe failed");
        rules.Reset(); result=EditDimension.None;
        for(int i=0;i<=6;i++) { s.time=i*.03f; s.palm=Vector3.up*i*.025f; var d=rules.Step(s); if(d!=EditDimension.None)result=d; }
        Require(result==EditDimension.None, "Vertical opening movement recognized as horizontal swipe");

        rules.Reset(); var circle=Circle(); result=EditDimension.None;
        for(int i=0;i<circle.Count;i++)
        {
            s=new TabletopGestureRules.Sample { time=i*.04f, pinch=.1f, pointing=true, index=circle[i] };
            var d=rules.Step(s); if(d!=EditDimension.None)result=d;
        }
        Require(result==EditDimension.Agency, "Pointing circle failed through recognizer");

        var reach=new GestureReachWindow();
        Require(!reach.Observe(true,.27f,0), "Cannot start a cast far from target");
        Require(reach.Observe(true,.18f,.1f), "Near hand not armed");
        Require(reach.Observe(true,.27f,.5f), "Opening fingers lost recent proximity");
        Require(!reach.Observe(true,.27f,.8f), "Proximity memory did not expire");
        reach.Observe(true,.18f,1);
        Require(!reach.Observe(true,.4f,1.1f), "Far hand bypassed outer limit");
        reach.Observe(true,.18f,2);
        Require(!reach.Observe(false,.18f,2.1f) && !reach.Observe(true,.27f,2.2f), "Lost hand retained proximity permission");
        Debug.Log("TABLETOP_GESTURE_CHECKS_PASS");
    }
}

