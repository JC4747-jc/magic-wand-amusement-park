using System.IO;
using System.Linq;
using MagicMR;
using UnityEngine;

public static partial class PetLiveRecognitionChecks
{
    static Transform suppliedHeadAnchor;
    static int contactStage;
    static float contactNext;

    static void StepHeadContact()
    {
        if (!metricActor || frame < 3) return;
        float now = Time.unscaledTime;
        if (now < contactNext || metricActor.Busy || waiting) return;
        switch (contactStage)
        {
            case 0:
                Require(controller.ProcessHandSample(MetricHand(Point,.02f,-Vector3.forward*.09f),true) == EditDimension.None &&
                    controller.selected == metricActor, "Shared point/pinch did not select the live optical Cat.");
                controller.ProcessHandSample(default,false);
                dream = metricActor.GetComponentsInChildren<Transform>(true).Single(x => x.name == "Dream bubble");
                Require(controller.Dispatch(ParkAction.Awaken,metricActor.InteractionCenter,true), "Could not open the dream.");
                break;
            case 1:
                Require(dream.gameObject.activeInHierarchy && !metricActor.reliableHeadPose && !metricActor.headAnchor,
                    "Body stereo unexpectedly set a head anchor.");
                Require(!controller.Dispatch(ParkAction.Feed,metricActor.InteractionCenter,true) &&
                    !controller.Dispatch(ParkAction.Poke,metricActor.InteractionCenter,true), "Body-only tracking allowed a head action.");
                // This externally reviewed fixture anchor is never produced by the runtime detector.
                if (frame % 3 != 1) return;
                var fixture=JsonUtility.FromJson<HeadFixture>(File.ReadAllText(Path.Combine(Input,"head.json")));
                suppliedHeadAnchor=new GameObject("Reviewed metric head - test input").transform;
                suppliedHeadAnchor.position=new Vector3(fixture.world[0],fixture.world[1],fixture.world[2]);
                metricActor.headAnchor=suppliedHeadAnchor;
                metricActor.reliableHeadPose=true;
                Vector3 body=metricActor.InteractionCenter;
                Vector3 head=suppliedHeadAnchor.position;
                Require(Vector3.Distance(head,body)>.12f && Vector3.Distance(head,body)<.40f,
                    "Fixture head and tracked body do not form distinct nearby targets.");
                Require(!controller.Dispatch(ParkAction.Feed,body,true) &&
                    !controller.Dispatch(ParkAction.Poke,body,true) &&
                    !controller.Dispatch(ParkAction.Feed,head+Vector3.right*.15f,true),
                    "Physical contact escaped the head or bubble range.");
                Require(controller.Dispatch(ParkAction.Feed,head,true), "Contact at the supplied head did not feed.");
                trace.Add("Tracked body, missing head, and a point 15 cm from the supplied head were rejected. Feed at the supplied head was accepted.");
                break;
            case 2:
                Require(metricActor.Affection==1 && dream.gameObject.activeInHierarchy, "A valid head contact did not add one affection.");
                Require(!controller.Dispatch(ParkAction.Poke,dream.position+Vector3.right*.15f,true) &&
                    controller.Dispatch(ParkAction.Poke,dream.position,true), "Direct poke did not distinguish the bubble and nearby space.");
                trace.Add("The bubble centre accepted a direct poke and a point 15 cm away was rejected.");
                break;
            case 3:
                Require(!dream.gameObject.activeInHierarchy, "Direct contact did not close the bubble.");
                withholdMetricFrames=true;
                break;
            case 4:
                Require(!metricActor.tracked && !metricActor.reliableHeadPose && !metricActor.headAnchor,
                    "Tracking loss left a usable head anchor.");
                Require(!controller.Dispatch(ParkAction.Feed,suppliedHeadAnchor.position,true), "Stale head contact was accepted.");
                withholdMetricFrames=false;
                break;
            case 5:
                Require(metricActor.tracked && controller.selected == metricActor && !metricActor.reliableHeadPose &&
                    !controller.Dispatch(ParkAction.Feed,suppliedHeadAnchor.position,true),
                    "Recovered body either lost identity or reused the expired head anchor.");
                Require(errors==0, "Unity logged an error during the head contact probe.");
                trace.Add("The same body recovered. Physical contact stayed blocked after the reviewed head expired.");
                Finish(0,"PASS: saved BridgeStereoFusion enforced Pet contact at a separately supplied metric head and cleared it on loss.");
                return;
        }
        contactStage++;
        contactNext=now+1.05f;
    }

    [System.Serializable]
    sealed class HeadFixture
    {
        public float[] world;
    }
}
