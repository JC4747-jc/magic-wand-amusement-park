using System;
using System.Linq;
using System.Reflection;
using MagicMR;
using UnityEditor;
using UnityEngine;

public static class NaturalInsectChecks
{
    static void Require(bool value,string message){if(!value)throw new Exception(message);}
    public static void Run()
    {
        foreach(var name in new[]{"Cockroach","Spider"})
        {
            var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/MagicMR/NaturalModels/"+name+".prefab"));
            try
            {
                go.transform.SetPositionAndRotation(new Vector3(.8f,.2f,.4f),Quaternion.Euler(0,37,0));
                var rig=go.GetComponent<NaturalInsectRig>();rig.Initialize();Require(rig.LegCount==(name=="Spider"?8:6),"Wrong insect anatomy");
                var anchor=go.transform.localToWorldMatrix;rig.ResetPose();var reference=new InsectSkinReference(rig);var rest=reference.Sample().ToArray();
                var restFeet=Enumerable.Range(0,rig.LegCount).Select(i=>rig.FootLocal(i)).ToArray();
                float restMinimum=rest.Min(p=>p.y);
                Require(Mathf.Abs(restMinimum)<.0003f,"Insect rest geometry floats above/below its presentation floor: "+name+" "+restMinimum);
                float maxError=0,minHeight=float.PositiveInfinity,maxMinimum=float.NegativeInfinity;int lifted=0;
                for(int frame=0;frame<48;frame++)
                {
                    rig.Step(frame/48f,true);
                    for(int i=0;i<rig.LegCount;i++)
                    {
                        var point=rig.FootLocal(i);var target=rig.FootTarget(i);maxError=Mathf.Max(maxError,Vector3.Distance(point,target));
                        if(target.y>restFeet[i].y+.0005f)lifted++;
                        Require(float.IsFinite(point.x)&&float.IsFinite(point.y)&&float.IsFinite(point.z),"Nonfinite IK foot");
                    }
                    float frameMinimum=float.PositiveInfinity;
                    foreach(var point in reference.Sample())
                    {
                        minHeight=Mathf.Min(minHeight,point.y);frameMinimum=Mathf.Min(frameMinimum,point.y);
                    }
                    maxMinimum=Mathf.Max(maxMinimum,frameMinimum);
                    Require(go.transform.localToWorldMatrix==anchor,"Insect gait moved presentation anchor");
                }
                Require(maxError<.0005f,"Insect IK missed its requested ankle target: "+name+" "+maxError);
                Require(minHeight>-.0005f,"Insect gait put geometry below its local ground: "+name+" "+minHeight);
                Require(maxMinimum<.001f,"Insect gait lost all ground contact: "+name+" "+maxMinimum);
                Require(lifted>0,"Insect gait never lifted a foot");
                rig.Step(float.NaN,true);Require(reference.Sample().Zip(rest,(a,b)=>(a-b).sqrMagnitude).Max()<1e-8f,"Invalid insect time retained a stale pose");
                for(int frame=0;frame<64;frame++)rig.Step(frame*.013f,true);
                long start=GC.GetAllocatedBytesForCurrentThread();for(int frame=0;frame<256;frame++)rig.Step(frame*.013f,true);long allocated=GC.GetAllocatedBytesForCurrentThread()-start;
                Require(allocated<=1024,"Insect gait allocated managed memory per frame: "+allocated);
                Debug.Log("NATURAL_INSECT_IK_PASS "+name+": "+rig.LegCount+" legs, max ankle error="+maxError+", min local height="+minHeight+", 256 steps managed bytes="+allocated+"; translated/yawed anchor invariant");
            }
            finally{UnityEngine.Object.DestroyImmediate(go);}
            CheckPlacement(name);
        }
    }
    static void CheckPlacement(string name)
    {
        var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MagicMR/Park/ParkBugs.prefab"));
        try
        {
            var actor=go.GetComponent<ParkActor>();actor.species=name;go.SetActive(true);actor.Build();actor.ConfirmOrientation(37);
            var anchor=go.transform.localToWorldMatrix;var bottom=new Vector3(.4f,.2f,.8f);const float height=.08f;
            Require(actor.FitRepresentation(bottom,bottom+Vector3.up*height,true),"Insect nominal placement rejected");
            var rig=actor.visual.GetComponentInChildren<NaturalInsectRig>();var points=new InsectSkinReference(rig).Sample();
            float minimum=float.PositiveInfinity,maximum=float.NegativeInfinity;
            foreach(var point in points){float y=rig.transform.TransformPoint(point).y;minimum=Mathf.Min(minimum,y);maximum=Mathf.Max(maximum,y);}
            Require(Mathf.Abs(minimum-bottom.y)<.0005f&&Mathf.Abs(maximum-bottom.y-height)<.0005f,"Manual insect fit used padded culling bounds instead of visible rest geometry");
            Require(go.transform.localToWorldMatrix==anchor&&Vector3.Distance(actor.ConfirmedBottomWorld,bottom)<.0005f,"Insect fit moved tracking anchor or missed bottom");
            Debug.Log("NATURAL_INSECT_PLACEMENT_PASS "+name+": independent skinned world geometry matches confirmed bottom/height within 0.5 mm; yaw and anchor preserved");
        }
        finally{UnityEngine.Object.DestroyImmediate(go);}
    }
}
