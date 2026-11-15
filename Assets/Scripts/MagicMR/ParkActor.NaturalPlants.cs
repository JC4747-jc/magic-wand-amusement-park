using System.Collections.Generic;
using UnityEngine;

namespace MagicMR
{
    public sealed partial class ParkActor
    {
        readonly List<Transform> naturalPlantLeaves=new List<Transform>();
        readonly List<Quaternion> naturalPlantRestRotations=new List<Quaternion>();
        void BuildNaturalPlant()
        {
            var prefab=Resources.Load<GameObject>("MagicMR/NaturalModels/Haworthia");
            if(!prefab)throw new System.InvalidOperationException("Missing CC0 Haworthia; run NaturalPlantAssetBuilder.Run");
            var model=Instantiate(prefab,visual,false);
            branchRoot=model.transform.Find("Growth");
            if(!branchRoot)throw new System.InvalidOperationException("Natural plant growth pivot missing");
            var skin=branchRoot.GetComponentInChildren<SkinnedMeshRenderer>();
            if(!skin)throw new System.InvalidOperationException("Natural plant skinned foliage missing");
            foreach(var node in skin.bones)
                if(node.name.StartsWith("Leaf_")) {naturalPlantLeaves.Add(node);naturalPlantRestRotations.Add(node.localRotation);}
            foreach(var renderer in model.GetComponentsInChildren<Renderer>())
            {
                body.Add(renderer);componentColors[renderer]=Color.white;
            }
            if(naturalPlantLeaves.Count!=40)throw new System.InvalidOperationException("Natural plant leaf hierarchy incomplete");
        }
        void StepNaturalPlant(float now)
        {
            for(int i=0;i<naturalPlantLeaves.Count;i++)
            {
                float unfolding=Mathf.Lerp(6,0,growthTime);
                float sway=Mathf.Sin(now*1.3f+i*.73f)*.3f;
                naturalPlantLeaves[i].localRotation=naturalPlantRestRotations[i]*Quaternion.Euler(unfolding+sway,0,0);
            }
        }
        void BuildNaturalBlossom()
        {
            plantFlower=new GameObject("Virtual Haworthia flower stalk").transform;
            plantFlower.SetParent(branchRoot,false);plantFlower.localPosition=Vector3.up*.06f;
            // Decorative bloom is explicitly virtual; its slender white flowers follow the plant's proportions.
            Part(plantFlower,"Slender flower stem",PrimitiveType.Cylinder,Vector3.up*.10f,new Vector3(.003f,.10f,.003f),new Color(.27f,.34f,.13f),false);
            for(int i=0;i<6;i++)
            {
                float angle=i*137.5f*Mathf.Deg2Rad;
                var flower=Part(plantFlower,"Tubular ivory blossom",PrimitiveType.Cube,
                    new Vector3(Mathf.Sin(angle)*.005f,.105f+i*.016f,Mathf.Cos(angle)*.005f),Vector3.one*.026f,new Color(.92f,.91f,.79f),false);
                flower.GetComponent<MeshFilter>().sharedMesh=ParkModelMeshes.Flower;
                flower.localRotation=Quaternion.Euler(60,i*137.5f,0);plantPetals.Add(flower);
            }
        }
    }
}
