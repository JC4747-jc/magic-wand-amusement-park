using UnityEngine;

namespace MagicMR
{
    public sealed partial class ParkActor
    {
        NaturalJellyfish naturalJellyfish;
        internal void FollowRoomPhotoPalm(Vector3 palm)
        {
            if(roomMagic && roomMagic.PhotoTransfer)roomMagic.PhotoTransfer.FollowPalm(palm);
        }
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
            float visibility=naturalJellyfish.Visibility;
            jellyfish.localPosition=new Vector3(.07f*Mathf.Sin(time*.48f)*visibility,
                .6f+Mathf.Sin(time*.8f)*.018f,.035f*Mathf.Sin(time*.96f)*visibility);
            jellyfish.localRotation=Quaternion.Euler(0,0,-6*Mathf.Cos(time*.48f)*visibility);
        }
    }
}
