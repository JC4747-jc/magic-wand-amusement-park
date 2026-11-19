using UnityEngine;

namespace MagicMR
{
    public sealed partial class ParkActor
    {
        NaturalJellyfish naturalJellyfish;
        void BuildNaturalJellyfish()
        {
            var prefab=Resources.Load<GameObject>("MagicMR/NaturalModels/Jellyfish");
            if(!prefab)throw new System.InvalidOperationException("Missing jellyfish lamp; run NaturalJellyfishAssetBuilder.Run");
            jellyfish=new GameObject("Virtual jellyfish lamp").transform;jellyfish.SetParent(visual,false);jellyfish.localPosition=Vector3.up*.6f;
            var model=Instantiate(prefab,jellyfish,false);naturalJellyfish=model.GetComponent<NaturalJellyfish>();
            if(!naturalJellyfish)throw new System.InvalidOperationException("Jellyfish motion driver missing");naturalJellyfish.Initialize();
        }
        void StepRoomJellyfish(float dt,float time)
        {
            if(!naturalJellyfish)return;naturalJellyfish.Step(dt,time,state.bubble);
            jellyfish.localPosition=Vector3.up*(.6f+Mathf.Sin(time*.8f)*.018f);
        }
    }
}
