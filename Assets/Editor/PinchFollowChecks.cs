using System;
using UnityEngine;
using Perception;

public static class PinchFollowChecks
{
    static void Require(bool ok,string message) {if(!ok)throw new Exception(message);}
    public static void Run()
    {
        var grip=new PinchGripFollow();
        Pose palm=new Pose(new Vector3(-.05f,1,0),Quaternion.identity);
        Vector3 pinch=new Vector3(0,1,0),offset=new Vector3(.015f,.035f,.02f),target=pinch+offset;
        for(int i=0;i<25;i++)grip.Observe(true,palm,true,true,pinch,target,i*.01f);
        Require(grip.IsHolding&&Vector3.Distance(grip.Position,target)<.0001f,"Pinch offset calibration snapped target to fingers");
        var turn=Quaternion.Euler(60,0,0);
        var rotatedPalm=new Pose(palm.position,turn);
        Vector3 rotatedPinch=palm.position+turn*(pinch-palm.position);
        for(int i=25;i<50;i++)grip.Observe(true,rotatedPalm,true,true,rotatedPinch,target,i*.01f);
        Require(Vector3.Distance(grip.Position,rotatedPinch+turn*offset)<.001f,"Wrist rotation failed positional offset compensation");
        int corrections=grip.Corrections;
        Require(grip.Preview(true,new Pose(rotatedPalm.position+Vector3.right*.005f,turn),true,true,rotatedPinch+Vector3.right*.005f,.495f,out var preview),"BeforeRender prediction missing");
        Require(grip.Corrections==corrections&&grip.IsHolding,"Rendering advanced interaction state");
        grip.Observe(true,new Pose(rotatedPalm.position+Vector3.right*.01f,turn),false,false,default,target,.50f);
        Require(grip.IsHolding&&grip.UsingPalmFallback,"Missing fingertips failed brief palm continuation");
        grip.Tick(.96f);
        Require(!grip.IsHolding,"Missing fingertips retained indefinite ownership");
        grip.Reset();
        for(int i=0;i<25;i++)grip.Observe(true,palm,true,true,pinch,target,i*.01f);
        Vector3 before=grip.Position;
        grip.Observe(true,palm,true,false,pinch,target,.25f);
        grip.Observe(true,new Pose(palm.position+Vector3.right*.02f,Quaternion.identity),false,false,default,target,.30f);
        Require(Vector3.Distance(grip.Position,before)<.0001f,"Opening and losing fingertips dragged released object");
        grip.Observe(true,palm,true,false,pinch,target,.44f);
        Require(!grip.IsHolding,"Separating fingers did not release");
        grip.Reset();
        for(int i=0;i<=65;i++)
        {
            float now=i*.01f;Vector3 move=Vector3.right*Mathf.Max(0,now-.25f)*.05f;
            grip.Observe(true,new Pose(palm.position+move,Quaternion.identity),true,true,pinch+move,target,now);
            if(i==36||i==46||i==56)
            {
                float capture=now-.04f;
                Vector3 seen=target+Vector3.right*(capture-.25f)*.05f+Vector3.up*.01f;
                bool corrected=grip.Correct(seen,capture,now,true);
                Require(corrected==(i==56),"Camera correction failed capture-time alignment or multi-frame confirmation");
            }
        }
        Require(grip.Corrections==1,"Reliable visual correction count invalid");
        Require(!grip.Correct(target+Vector3.one,.60f,.66f,true),"Vision outlier pulled held target");
        Require(!grip.Correct(target,.60f,.66f,false),"Unreliable detection corrected target");
        Require(!grip.Correct(target,.10f,.66f,true),"Stale camera result corrected target");
        grip.Reset();
        for(int i=0;i<25;i++)grip.Observe(true,palm,true,true,pinch,target+Vector3.right*.2f,i*.01f);
        Require(!grip.IsHolding,"Distant pinch acquired lighter");
        Debug.Log("PINCH_FOLLOW_CHECKS_PASS");
    }
}
