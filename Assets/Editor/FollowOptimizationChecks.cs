using System;
using System.IO;
using UnityEngine;
using Perception;
public static class FollowOptimizationChecks
{
    static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
    public static void Run()
    {
        var filter=new TrackingPositionFilter();filter.Reset(Vector3.zero);
        Vector3 oldPosition=Vector3.zero,previousRaw=Vector3.zero;
        double newJitter=0,oldJitter=0,newLag=0,oldLag=0;int stationaryCount=0,movingCount=0;
        for(int i=0;i<440;i++)
        {
            float x=i<120?0:i<320?(i-120)*.0025f:.4975f;
            Vector3 truth=Vector3.right*x;
            Vector3 raw=truth+Vector3.right*(i%2==0?.001f:-.001f);
            Vector3 next=filter.Update(raw,.01f);
            float oldRate=20f+Mathf.Min(100f,Vector3.Distance(raw,previousRaw)/.01f*120f);
            oldPosition=Vector3.Lerp(oldPosition,raw,1-Mathf.Exp(-oldRate*.01f));previousRaw=raw;
            if(i>=40&&i<120){newJitter+=(next-truth).sqrMagnitude;oldJitter+=(oldPosition-truth).sqrMagnitude;stationaryCount++;}
            if(i>=160&&i<310){newLag+=Vector3.Distance(next,truth);oldLag+=Vector3.Distance(oldPosition,truth);movingCount++;}
        }
        double newRms=Math.Sqrt(newJitter/stationaryCount)*1000,oldRms=Math.Sqrt(oldJitter/stationaryCount)*1000;
        newLag=newLag/movingCount*1000;oldLag=oldLag/movingCount*1000;
        Require(newRms<oldRms&&newRms<.35,"Adaptive filter did not reduce stationary jitter");
        Require(newLag<oldLag&&newLag<6,"Adaptive filter increased moving lag");
        Require(Vector3.Distance(filter.Position,Vector3.right*.4975f)<.001,"Filter failed to settle after stopping");
        string report=$"Synthetic replay only: 100 Hz, alternating +/-1 mm noise, 0.25 m/s translation.\n"+
            $"Static RMS: old={oldRms:F3} mm new={newRms:F3} mm\nMoving mean gap: old={oldLag:F3} mm new={newLag:F3} mm\n";
        Directory.CreateDirectory(".codex-tmp");File.WriteAllText(".codex-tmp/follow-filter-benchmark.txt",report);
        Debug.Log("FOLLOW_FILTER_BENCHMARK " + report);

        var grip=new PinchGripFollow();
        Pose palm=new Pose(new Vector3(-.05f,1,0),Quaternion.identity);
        Vector3 pinch=Vector3.up,target=pinch+Vector3.up*.04f;
        bool wasHeld=false;
        for(int i=0;i<25;i++)
        {
            Vector3 changingTarget=target+Vector3.right*i*.0002f;
            grip.Observe(true,palm,true,true,pinch,changingTarget,i*.01f);
            if(!wasHeld&&grip.IsHolding)Require(Vector3.Distance(grip.Position,changingTarget)<.00001f,"Attachment caused a visible snap");
            wasHeld=grip.IsHolding;
        }
        for(int i=25;i<=55;i++)grip.Observe(true,palm,false,false,default,target,i*.01f);
        Require(grip.IsHolding&&grip.UsingPalmFallback,"Brief fingertip occlusion dropped attachment");
        Vector3 before=grip.Position;
        grip.Observe(true,palm,true,true,pinch+Vector3.right*.02f,target,.56f);
        Require(grip.IsHolding&&Vector3.Distance(before,grip.Position)<.004f,"Fingertip recovery caused a large jump");
        Require(!grip.Preview(true,palm,true,true,pinch+Vector3.right*.02f,.565f,out _),"BeforeRender bypassed recovery smoothing");
        for(int i=57;i<86;i++)grip.Observe(true,palm,true,true,pinch+Vector3.right*.02f,target,i*.01f);
        Require(Vector3.Distance(grip.Position,before)<.002,"Finger articulation dragged the held object");
        Require(grip.Fallbacks==1&&grip.Attachments==1,"Transition counters missing");
        grip.Observe(true,palm,true,false,pinch,target,.86f);
        Vector3 released=grip.Position;
        grip.Observe(true,palm,true,false,pinch,target,1.06f);
        Require(!grip.IsHolding&&grip.ReleaseReason=="FINGERS_OPEN"&&grip.Position==released,"Release moved target or lost reason");

        var visual=new TabletopVisualFollow();visual.Reset(Vector3.zero,true);
        Require(!visual.Observe(Vector3.right*.02f,0)&&!visual.Observe(Vector3.right*.021f,.1f),"Vision resumed on unconfirmed release frames");
        Require(visual.Observe(Vector3.right*.0205f,.2f),"Consistent release measurements did not resume vision");
        visual.Reset(Vector3.zero,true);
        visual.Observe(Vector3.right*.02f,0);visual.Observe(Vector3.right*.05f,.1f);
        Require(!visual.Observe(Vector3.right*.02f,.2f)&&visual.Position==Vector3.zero,"Inconsistent post-release vision moved anchor");
        Debug.Log("FOLLOW_OPTIMIZATION_CHECKS_PASS");
    }
}
