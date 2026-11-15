using System;
using Perception;
using UnityEditor;
using UnityEngine;

public static class F2GPoseChecks
{
    static void Require(bool ok, string message) { if (!ok) throw new Exception(message); }
    static float[] Flat(Matrix4x4 m) => new[] {m.m00,m.m01,m.m02,m.m10,m.m11,m.m12,m.m20,m.m21,m.m22};
    static void Near(Quaternion a, Quaternion b, string message) => Require(Quaternion.Angle(a,b)<.1f,message);

    [MenuItem("MagicMR/Checks/F2G Pose")]
    public static void Run()
    {
        var f = Matrix4x4.Scale(new Vector3(1,1,-1));
        var world = Quaternion.Euler(20,40,15);
        var camera = Quaternion.Euler(0,3,0);
        foreach (var head in new[] {Quaternion.identity,Quaternion.Euler(25,80,-12)})
        {
            var cameraObject = f * Matrix4x4.Rotate(Quaternion.Inverse(head*camera)*world) * f;
            Require(F2GPoseGeometry.TryWorldRotation(Flat(cameraObject),head,camera,out var recovered),"Valid rotation rejected");
            Near(recovered,world,"Head motion changed stationary world rotation / wrong handedness");
        }
        Require(!F2GPoseGeometry.TryWorldRotation(Flat(f),Quaternion.identity,Quaternion.identity,out _),"Reflection accepted as rotation");
        var invalid = Flat(Matrix4x4.identity); invalid[0] = float.NaN;
        Require(!F2GPoseGeometry.TryWorldRotation(invalid,Quaternion.identity,Quaternion.identity,out _),"NaN rotation accepted");
        F2GPoseGeometry.RightFromLeft(new Pose(Vector3.zero,Quaternion.identity),
            new Pose(Vector3.right*.06f,Quaternion.identity),out var r,out var t);
        Require(Mathf.Abs(t[0]+.06f)<.00001f && t[1]==0 && t[2]==0,"Stereo baseline sign wrong");

        var filter = new F2GRotationFilter();
        for (int i=1;i<=3;i++) Require(filter.Observe(i,world,i*.1f,i*.1f+.02f,.6f),"Stable sample rejected");
        Require(filter.StableSamples==3,"Summon reference not ready");
        Require(!filter.Observe(3,Quaternion.identity,.3f,.35f,.6f),"Duplicate accepted");
        Require(!filter.Observe(4,Quaternion.identity,.4f,1.1f,.6f),"Stale sample accepted");
        Require(!filter.Observe(4,Quaternion.identity,2,1.1f,.6f),"Future sample accepted");
        var spike = Quaternion.Euler(150,40,15);
        Require(!filter.Observe(4,spike,.4f,.42f,.6f),"Single spike accepted");
        Near(filter.Accepted,world,"Rejected spike changed orientation");
        Require(!filter.Observe(5,spike,.5f,.52f,.6f),"Jump accepted before confirmation");
        Require(filter.Observe(6,spike,.6f,.62f,.6f),"Consistent jump not accepted");
        Require(!filter.Observe(7,world,2,2.02f,.6f),"Reacquisition skipped confirmation");
        Require(!filter.Observe(8,world,2.1f,2.12f,.6f),"Reacquisition confirmed too early");
        Require(filter.Observe(9,world,2.2f,2.22f,.6f),"Reacquisition failed");
        filter.Reset(); Require(filter.StableSamples==0,"Reset retained calibration samples");
        for (int i=1;i<=3;i++)
            Require(filter.Observe(i,world,i*.8f,i*.8f+.7f,.9f),"Measured single-flight cadence rejected");
        Require(filter.StableSamples==3,"Slow valid samples never establish reference");
        Require(!filter.Observe(4,world,4.5f,5.2f,.9f),"Long gap skipped confirmation");
        Require(!filter.Observe(5,world,5.3f,6f,.9f),"Long gap confirmed too early");
        Require(filter.Observe(6,world,6.1f,6.8f,.9f),"Slow reacquisition failed");
        Require(!filter.Observe(7,world,6.9f,7.9f,.9f),"Result older than new age bound accepted");

        // Rotating the anchor must preserve its translation and the authored scale;
        // the model root offset follows the pivot instead of a world-up bottom offset.
        var anchor = new GameObject("Pose check anchor");
        var model = new GameObject("Pose check model");
        try
        {
            model.transform.SetParent(anchor.transform,false);
            anchor.transform.position = new Vector3(.5f,1,2);
            model.transform.localPosition = Vector3.down*.04f;
            model.transform.localScale = Vector3.one*.1f;
            anchor.transform.rotation = Quaternion.Euler(0,0,90);
            Require(Vector3.Distance(anchor.transform.position,new Vector3(.5f,1,2))<.0001f,"Rotation changed stereo translation");
            Require(Vector3.Distance(model.transform.position,new Vector3(.54f,1,2))<.0001f,"Incorrect rotation pivot");
            Require(model.transform.localScale==Vector3.one*.1f,"Rotation changed scale");
        }
        finally { UnityEngine.Object.DestroyImmediate(model); UnityEngine.Object.DestroyImmediate(anchor); }
        Debug.Log("F2G_POSE_CHECKS_PASS");
    }
}
