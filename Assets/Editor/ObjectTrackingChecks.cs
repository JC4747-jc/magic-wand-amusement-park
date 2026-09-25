using System;
using UnityEngine;
using Perception;
using MagicMR;
public static class ObjectTrackingChecks
{
    static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
    public static void Run()
    {
        CheckFollowingWithoutVision();
        CheckObjectOwnership();
        CheckContactAndMovingVision();
        byte[] frame=new byte[640*480];Array.Fill(frame,(byte)110);
        Rect original=new Rect(280,180,40,120);
        void Draw(byte[] image,int dx,int dy)
        {
            for(int y=0;y<120;y++)for(int x=0;x<40;x++)
                image[(180+y+dy)*640+280+x+dx]=(byte)(40+((x/4*57+y/6*31+x*y)%170));
        }
        Draw(frame,0,0);
        var tracker=new LighterAppearanceTrack();tracker.Capture(frame,original);
        Require(tracker.Ready&&tracker.Score(frame,original)>.99f,"Appearance calibration failed");
        byte[] moved=new byte[640*480];Array.Fill(moved,(byte)110);Draw(moved,15,-10);
        Require(tracker.Find(moved,original,out var found)&&Vector2.Distance(found.center,original.center+new Vector2(15,-10))<2,"Object tracking did not recover translation without YOLO");
        Array.Fill(moved,(byte)130);
        Require(!tracker.Find(moved,original,out _),"Textureless finger/background accepted as lighter");
        Draw(moved,15,-10);
        for(int y=180+70-10;y<180+120-10;y++)for(int x=280+15;x<320+15;x++)moved[y*640+x]=130;
        Require(tracker.Find(moved,original,out found)&&Vector2.Distance(found.center,original.center+new Vector2(15,-10))<2,"Partial occlusion lost the visible lighter texture");
        var gestures=new TabletopGestureRules();
        for(int i=0;i<10;i++)Require(gestures.Step(new TabletopGestureRules.Sample{time=i*.02f,pinch=.1f,open=true,palm=Vector3.forward*i*.025f,swipeAxis=Vector3.right})!=EditDimension.Rule,
            "Reaching toward the lighter triggered a lateral dodge");
        gestures.Reset();bool fired=false;
        for(int i=0;i<10;i++)fired|=gestures.Step(new TabletopGestureRules.Sample{time=i*.02f,pinch=.1f,open=true,palm=Vector3.right*i*.025f,swipeAxis=Vector3.right})==EditDimension.Rule;
        Require(fired,"Intentional lateral swipe no longer triggers");
        Debug.Log("OBJECT_TRACKING_CHECKS_PASS");
    }
    static void CheckFollowingWithoutVision()
    {
        // Reproduce the device regression through the locator AND the rendered anchor,
        // rather than testing only the grip estimator that kept moving behind the freeze.
        var host=new GameObject("Vision loss following regression");host.SetActive(false);
        var anchorObject=new GameObject("Regression anchor");
        try
        {
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var locator=host.AddComponent<StereoYoloLocator>();
            locator.followMode=LighterFollowMode.HybridGrip;
            var anchor=host.AddComponent<AnchorManager>();
            var rightHand=host.AddComponent<TabletopGestureRecognizer>();
            var type=typeof(StereoYoloLocator);
            type.GetMethod("UpdateFollowingHold",flags).Invoke(locator,null);
            Vector3 target=Vector3.one;anchorObject.transform.position=target;
            type.GetField("tracking",flags).SetValue(locator,true);
            type.GetField("summoned",flags).SetValue(locator,true);
            type.GetField("registeredWidth",flags).SetValue(locator,.03f);
            type.GetField("registeredHeight",flags).SetValue(locator,.09f);
            type.GetField("anchorManager",flags).SetValue(locator,anchor);
            type.GetField("gestureRecognizer",flags).SetValue(locator,rightHand);
            typeof(TabletopGestureRecognizer).GetField("visionHoldUntil",flags).SetValue(rightHand,Time.unscaledTime+10);
            typeof(AnchorManager).GetField("m_Anchor",flags).SetValue(anchor,anchorObject.transform);
            typeof(AnchorManager).GetField("m_HasTarget",flags).SetValue(anchor,true);
            typeof(AnchorManager).GetField("m_TargetPosition",flags).SetValue(anchor,target);
            var acquired=(TabletopTargetLock)type.GetField("tabletopLock",flags).GetValue(locator);
            for(int i=0;i<5;i++)acquired.Observe(target,i*.2f);
            var grip=(PinchGripFollow)type.GetField("grip",flags).GetValue(locator);
            // Batch executeMethod can run within 0.36 s of the editor clock starting.
            // Keep synthetic history positive; negative times collide with unset-time sentinels.
            float now=Mathf.Max(1f,Time.realtimeSinceStartup);
            Vector3 palm=target+Vector3.left*.04f,pinch=target;
            for(int i=0;i<=36;i++)grip.Observe(true,new Pose(palm,Quaternion.identity),true,true,pinch,target,now-.36f+i*.01f);
            Require(grip.IsHolding,$"Regression setup did not acquire left grip: now={now:R} gap={grip.HandGapMs:R} reason={grip.ReleaseReason}");
            var ownership=(LighterHoldEvidence)type.GetField("ownership",flags).GetValue(locator);
            ownership.Begin(target,now-.36f);
            locator.Invalidate("NO_LIGHTER"); // No template, no fresh vision, right hand casting.
            for(int i=1;i<=8;i++)
            {
                Vector3 move=new Vector3(i*.005f,i*.003f,-i*.002f);
                locator.UpdatePinchHand(true,new Pose(palm+move,Quaternion.identity),true,true,pinch+move,now+i*.01f,false);
                typeof(AnchorManager).GetMethod("Update",flags).Invoke(anchor,null);
            }
            Require(!locator.IsHeld&&Vector3.Distance(anchor.PlacementPosition,target)<.0001f,
                "An empty pinch moved the rendered lighter without object evidence");
            // Independent object measurements confirm a real pickup before testing occlusion.
            Vector3 pickup=target+new Vector3(.04f,.024f,-.016f);
            ownership.Observe(pickup,pickup,now+.01f,now+.02f,true);
            ownership.Observe(pickup,pickup,now+.12f,now+.13f,true);
            Require(ownership.Confirmed,"Regression pickup evidence missing");
            for(int i=9;i<=16;i++)
            {
                Vector3 move=new Vector3(i*.005f,i*.003f,-i*.002f);
                locator.UpdatePinchHand(true,new Pose(palm+move,Quaternion.identity),true,true,pinch+move,now+i*.01f,false);
                typeof(AnchorManager).GetMethod("Update",flags).Invoke(anchor,null);
            }
            Require(locator.IsHeld&&Vector3.Distance(anchor.PlacementPosition,target)>.03f,
                "Visual loss / right-hand casting froze the displayed held lighter");
            Require(Vector3.Distance(anchor.PlacementPosition,grip.Position)<.0001f,"Rendered anchor stopped following the grip estimator");
            Vector3 before=anchor.PlacementPosition,latest=new Vector3(.083f,.048f,-.032f);
            locator.UpdatePinchHand(true,new Pose(palm+latest,Quaternion.identity),true,true,pinch+latest,now+.165f,true);
            Require(anchor.PlacementPosition.x>before.x+.0001f,"BeforeRender following requires fresh vision");
            Require((LighterAppearanceTrack)type.GetField("appearance",flags).GetValue(locator) is {Ready:false},"Regression unexpectedly had a template");
            // Real contact acquisition must also drive the final anchor when YOLO is occluded.
            grip.ReleaseFromVision();ownership.Reset();
            anchorObject.transform.position=target;anchor.EndHandPlacement();
            for(int i=0;i<=45;i++)
            {
                float sampleTime=now+1+i*.01f;
                Vector3 move=Vector3.up*Mathf.Max(0,i-25)*.003f;
                locator.UpdateGripContact(true,target+move+Vector3.left*.018f,target+move+Vector3.right*.018f,sampleTime);
                locator.UpdatePinchHand(true,new Pose(palm+move,Quaternion.identity),true,true,target+move,sampleTime,false);
                typeof(AnchorManager).GetMethod("Update",flags).Invoke(anchor,null);
            }
            Require(locator.IsHeld&&anchor.PlacementPosition.y>target.y+.045f,
                "A contact-confirmed pickup with missing YOLO froze the displayed model");
            Debug.Log("NO_VISION_RENDERED_FOLLOW_CHECK_PASS");
        }
        finally{UnityEngine.Object.DestroyImmediate(host);UnityEngine.Object.DestroyImmediate(anchorObject);}
    }
    static void CheckObjectOwnership()
    {
        var evidence=new LighterHoldEvidence();Vector3 target=Vector3.up;
        evidence.Begin(target,0);
        for(int i=1;i<=8;i++)evidence.Observe(target,target+Vector3.right*i*.02f,i*.15f,i*.15f+.03f,true);
        Require(!evidence.Confirmed,"Empty fingers near a stationary lighter acquired ownership");
        evidence.Begin(target,2);
        for(int i=1;i<=4;i++)evidence.Observe(target+Vector3.right*i*.025f,target,2+i*.15f,2+i*.15f+.03f,true);
        Require(!evidence.Confirmed,"Object moving independently of the hand acquired ownership");
        evidence.Begin(target,3);
        evidence.Observe(target+Vector3.up*.025f,target+Vector3.up*.026f,3.2f,3.23f,true);
        evidence.Observe(target+Vector3.up*.04f,target+Vector3.up*.041f,3.36f,3.39f,true);
        Require(evidence.Confirmed,"A visible real pickup did not acquire ownership");
        for(int i=0;i<10;i++)evidence.Observe(target,target,4+i*.2f,4+i*.2f+.03f,false);
        Require(evidence.Confirmed,"Visual occlusion disabled an already confirmed pickup");
        evidence.Observe(target+Vector3.one,target,6,6.03f,true);
        Require(evidence.Confirmed,"A single erroneous depth released the real object");
        evidence.Observe(target,target,6.2f,6.23f,true);
        Require(!evidence.Observe(target,target+Vector3.right*.05f,6.4f,6.43f,true),"Released before multi-frame confirmation");
        Require(!evidence.Observe(target,target+Vector3.right*.075f,6.56f,6.59f,true),"Released before third stationary object frame");
        Require(evidence.Observe(target,target+Vector3.right*.10f,6.72f,6.75f,true)&&!evidence.Confirmed&&
            Vector3.Distance(evidence.ReleasedPosition,target)<.001f,"Set-down object followed departing closed fingers");
        evidence.Begin(target,7);
        for(int i=1;i<=5;i++)evidence.Observe(target,target+Vector3.right*i*.025f,7+i*.15f,7+i*.15f+.03f,true);
        Require(!evidence.Confirmed,"Empty fingers immediately reacquired the placed object");
        Debug.Log("OBJECT_PICKUP_AND_SETDOWN_CHECK_PASS");
    }
    static void CheckContactAndMovingVision()
    {
        Vector3 center=Vector3.up;
        Require(!LighterGripContact.Fits(center+Vector3.left*.003f,center+Vector3.right*.003f,center,.03f,.09f),"Empty closed pinch counted as object contact");
        Require(!LighterGripContact.Fits(center+Vector3.right*.02f,center+Vector3.right*.045f,center,.03f,.09f),"Same-side fingers acquired lighter");
        Require(!LighterGripContact.Fits(center+new Vector3(-.018f,.08f,0),center+new Vector3(.018f,.08f,0),center,.03f,.09f),"Fingers above lighter acquired it");
        Require(LighterGripContact.Fits(center+new Vector3(-.018f,-.02f,.006f),center+new Vector3(.018f,-.02f,.006f),center,.03f,.09f),"Two-sided contact rejected modest tracking offset");
        var contact=new LighterGripContact();
        contact.Observe(true,center+Vector3.left*.018f,center+Vector3.right*.018f,center,.03f,.09f,0);
        Require(!contact.Recent(0),"One contact frame acquired the object");
        for(int i=1;i<=12;i++)contact.Observe(true,center+Vector3.left*.018f,center+Vector3.right*.018f,center,.03f,.09f,i*.01f);
        Require(contact.Recent(.12f)&&!contact.Recent(.4f),"Contact dwell / short transfer window invalid");
        foreach(bool reacquire in new[]{false,true})
        {
            var visual=new TabletopVisualFollow();visual.Reset(Vector3.zero,reacquire);
            Require(!visual.Observe(Vector3.right*.20f,0),"Single distant point bypassed visual confirmation");
            Require(!visual.Observe(Vector3.right*.245f,.15f),"Visual confirmation accepted only two points");
            Require(visual.Observe(Vector3.right*.29f,.30f)&&visual.Position.x>.28f,"Continuously moving object deadlocked CONFIRMING_MOVE");
        }
        var jitter=new TabletopVisualFollow();jitter.Reset(Vector3.zero);
        Require(!jitter.Observe(Vector3.right*.3f,0)&&!jitter.Observe(Vector3.left*.3f,.15f)&&!jitter.Observe(Vector3.up*.3f,.30f),"Incoherent visual spikes passed motion confirmation");
        Debug.Log("CONTACT_AND_CONTINUOUS_VISUAL_FOLLOW_CHECK_PASS");
    }
}
