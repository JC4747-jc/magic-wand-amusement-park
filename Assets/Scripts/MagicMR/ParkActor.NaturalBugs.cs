using UnityEngine;

namespace MagicMR
{
    public sealed partial class ParkActor
    {
        NaturalInsectRig naturalBugRig;
        Vector3 naturalBugRestScale;
        void BuildNaturalBug()
        {
            string name=species=="Spider"?"Spider":"Cockroach";
            var prefab=Resources.Load<GameObject>("MagicMR/NaturalModels/"+name);
            if(!prefab)throw new System.InvalidOperationException("Missing original insect; run NaturalInsectAssetBuilder.Install");
            var model=Instantiate(prefab,visual,false);naturalBugRig=model.GetComponent<NaturalInsectRig>();
            if(!naturalBugRig)throw new System.InvalidOperationException("Insect articulation driver missing");
            naturalBugRig.Initialize();naturalBugRestScale=model.transform.localScale;
            var renderer=naturalBugRig.Skin;body.Add(renderer);componentColors[renderer]=Color.white;
        }
        void StepNaturalBug(float time,bool active)
        {
            if(naturalBugRig){naturalBugRig.SetPixelStyle(species,state.style==2);naturalBugRig.Step(time,active);}
        }
    }
}
