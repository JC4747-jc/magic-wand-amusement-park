using System.Collections;
using MagicMR;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class ParkInteractionTests
{
    GameObject target;
    Material material;
    GameObject controllerObject;
    ParkActor Create(ParkScenario scenario)
    {
        target=new GameObject("Interaction regression anchor");target.transform.position=new Vector3(1,2,3);
        material=new Material(Shader.Find("MagicMR/ParkSurface"));
        var actor=target.AddComponent<ParkActor>();actor.scenario=scenario;actor.sharedMaterial=material;actor.Build();return actor;
    }
    [UnityTearDown] public IEnumerator Cleanup() { Object.Destroy(controllerObject);Object.Destroy(target);Object.Destroy(material);yield return null; }

    [UnityTest] public IEnumerator ManualFitKeepsTrackingRootAndClearsOnLoss()
    {
        var actor=Create(ParkScenario.Plant);Vector3 root=actor.transform.position;
        controllerObject=new GameObject("Manual fit controller");var controller=controllerObject.AddComponent<ParkController>();controller.selected=actor;
        Assert.IsTrue(controller.RequestFit());Assert.IsFalse(controller.ConfirmFit(new Vector3(float.NaN,0,0),true));
        Vector3 bottom=root+new Vector3(.02f,-.1f,.03f);
        Assert.IsTrue(controller.ConfirmFit(bottom,true));Assert.IsFalse(actor.sizeConfirmed);
        Assert.IsTrue(controller.ConfirmFit(bottom+Vector3.up*.4f,true));Assert.IsTrue(actor.sizeConfirmed);
        Assert.Less(Vector3.Distance(bottom,actor.ConfirmedBottomWorld),.0001f);Assert.AreEqual(.4f,actor.ConfirmedHeight,.0001f);Assert.AreEqual(root,actor.transform.position);
        yield return null;Assert.Less(Vector3.Distance(bottom,actor.ConfirmedBottomWorld),.0001f);
        Vector3 follow=Vector3.right*.12f;actor.transform.position+=follow;Assert.Less(Vector3.Distance(bottom+follow,actor.ConfirmedBottomWorld),.0001f);
        Assert.IsTrue(actor.ConfirmOrientation(90));Assert.IsFalse(actor.sizeConfirmed);
        Assert.IsFalse(actor.FitRepresentation(bottom,bottom,false));Assert.IsFalse(actor.FitRepresentation(bottom,bottom,true));
        actor.SetTracked(false);Assert.IsFalse(actor.sizeConfirmed);Assert.IsFalse(actor.FitRepresentation(bottom,bottom+Vector3.up*.4f,true));
        actor.SetTracked(true);Assert.IsTrue(controller.RequestFit());Assert.IsTrue(controller.ConfirmFit(bottom,true));actor.transform.position+=Vector3.right*.2f;
        Assert.IsFalse(controller.ConfirmFit(bottom+Vector3.up*.4f,true));Assert.IsFalse(controller.FitConfirmationPending);
    }

    [UnityTest] public IEnumerator OrientationConfirmationRequiresTrackingAndClearsOnLossAndReset()
    {
        var actor=Create(ParkScenario.Plant);
        Assert.IsFalse(actor.orientationConfirmed);
        Assert.IsFalse(actor.ConfirmOrientation(float.NaN));
        Assert.IsTrue(actor.ConfirmOrientation(45));Assert.IsTrue(actor.orientationConfirmed);
        actor.SetTracked(false);Assert.IsFalse(actor.orientationConfirmed);Assert.IsFalse(actor.ConfirmOrientation(30));
        actor.SetTracked(true);Assert.IsTrue(actor.ConfirmOrientation(90));actor.ResetEffects();Assert.IsFalse(actor.orientationConfirmed);
        yield return null;
    }

    [UnityTest] public IEnumerator ManualSurfacesRequireConfirmationAndStayIndependentOfTargets()
    {
        var first=Create(ParkScenario.Room);
        controllerObject=new GameObject("Independent scene controller");var controller=controllerObject.AddComponent<ParkController>();controller.selected=first;
        Assert.IsTrue(controller.RequestManualPlane());Assert.IsTrue(controller.PlaneConfirmationPending);Assert.IsFalse(first.confirmedPlane);
        Assert.IsFalse(controller.ConfirmManualPlane(Vector3.one,false));Assert.IsFalse(controller.ConfirmManualPlane(new Vector3(float.NaN,0,0),true));
        Vector3 a=new Vector3(0,.1f,1);Assert.IsTrue(controller.ConfirmManualPlane(a,true));
        var secondObject=new GameObject("Second room target");secondObject.transform.SetParent(target.transform,false);
        var second=secondObject.AddComponent<ParkActor>();second.scenario=ParkScenario.Room;second.sharedMaterial=material;second.Build();controller.selected=second;
        Assert.IsTrue(controller.RequestManualPlane());Vector3 b=new Vector3(2,.2f,3);Assert.IsTrue(controller.ConfirmManualPlane(b,true));
        Assert.AreNotSame(first.environmentAnchor,second.environmentAnchor);Assert.AreEqual(a,first.environmentAnchor.position);
        target.transform.position+=Vector3.right;yield return null;
        Assert.AreEqual(a,first.environmentAnchor.position);Assert.AreEqual(b,second.environmentAnchor.position);
        controller.Dispatch(ParkAction.Reset,second.transform.position,true);yield return null;
        Assert.IsFalse(second.confirmedPlane);Assert.IsNull(second.environmentAnchor);Assert.AreEqual(a,first.environmentAnchor.position);
        controller.enabled=false;yield return null;Assert.IsTrue(first.environmentAnchor==null);Assert.IsFalse(first.confirmedPlane);
    }

    [UnityTest] public IEnumerator BugRewardFragmentsCoinsAndResetKeepTheTrackedAnchor()
    {
        var actor=Create(ParkScenario.Bugs);Vector3 anchor=target.transform.position;
        Assert.IsTrue(actor.TryAction(ParkAction.Awaken,anchor,true));yield return new WaitForSecondsRealtime(1.2f);
        Assert.IsTrue(actor.visual.Find("Eyes").gameObject.activeSelf);
        Assert.IsTrue(actor.TryAction(ParkAction.Reward,anchor,true));
        yield return new WaitForSecondsRealtime(.5f);
        Assert.IsFalse(actor.visual.Find("Pixel body").GetComponent<Renderer>().enabled);
        Assert.IsFalse(actor.visual.Find("Eyes").gameObject.activeSelf);
        Assert.AreEqual("ParkFeedback_Coin",actor.visual.Find("Pooled feedback 8").GetComponent<MeshFilter>().sharedMesh.name);
        Assert.IsTrue(actor.visual.Find("Pooled feedback 8").gameObject.activeSelf);
        Assert.AreEqual(anchor,target.transform.position);
        actor.ResetEffects();yield return null;
        Assert.IsTrue(actor.visual.Find("Pixel body").GetComponent<Renderer>().enabled);
        Assert.IsFalse(actor.Busy);
        Assert.IsFalse(actor.visual.Find("Pooled feedback 8").gameObject.activeSelf);
        Assert.AreEqual(anchor,target.transform.position);
    }

    [UnityTest] public IEnumerator PetFeedCancellationOnTargetLossDoesNotCommitAffection()
    {
        var actor=Create(ParkScenario.Pet);Vector3 anchor=target.transform.position;
        Assert.IsFalse(actor.TryAction(ParkAction.Feed,anchor,true));
        Assert.IsTrue(actor.TryAction(ParkAction.Pull,anchor,true));yield return new WaitForSecondsRealtime(1.2f);
        Assert.IsTrue(actor.TryAction(ParkAction.Feed,anchor,true));yield return new WaitForSecondsRealtime(.1f);
        actor.SetTracked(false);yield return new WaitForSecondsRealtime(.5f);
        Assert.AreEqual(0,actor.Affection);Assert.IsFalse(actor.Busy);Assert.IsFalse(actor.visual.gameObject.activeSelf);
        Assert.IsFalse(actor.TryAction(ParkAction.Pull,anchor,true));
        actor.SetTracked(true);Assert.IsTrue(actor.visual.gameObject.activeSelf);Assert.AreEqual(anchor,target.transform.position);
    }

    [UnityTest] public IEnumerator RoomConfirmedSurfaceRewardAndDisableClearAllTransientState()
    {
        var actor=Create(ParkScenario.Room);Vector3 anchor=target.transform.position;
        Assert.IsFalse(actor.TryAction(ParkAction.Reward,anchor,true));
        actor.environmentAnchor=target.transform;actor.confirmedPlane=true;
        Assert.IsTrue(actor.TryAction(ParkAction.Reward,anchor,true));yield return new WaitForSecondsRealtime(.5f);
        Assert.AreEqual("ParkFeedback_Candy",actor.visual.Find("Pooled feedback 0").GetComponent<MeshFilter>().sharedMesh.name);
        actor.enabled=false;yield return null;
        Assert.IsFalse(actor.Busy);Assert.IsFalse(actor.confirmedPlane);Assert.IsNull(actor.environmentAnchor);
        Assert.IsFalse(actor.visual.Find("Pooled feedback 0").gameObject.activeSelf);Assert.AreEqual(anchor,target.transform.position);
    }
}
