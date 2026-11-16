using System.Collections.Generic;
using UnityEngine;

namespace MagicMR
{
    public sealed partial class ParkActor
    {
        readonly List<Transform> naturalPlantLeaves=new List<Transform>();
        readonly List<Quaternion> naturalPlantRestRotations=new List<Quaternion>();
        TextMesh plantSpeech;
        Transform plantMouth;
        Vector3 plantMouthScale;
        void BuildPlantDialogue(Bounds bounds)
        {
            // Small virtual face at pot height; reuse pooled lifetime and shared material.
            float unit=Mathf.Max(.01f,bounds.size.y*.055f);
            eyes.localPosition=new Vector3(bounds.center.x,bounds.min.y+bounds.size.y*.23f,bounds.center.z);
            float front=Mathf.Max(bounds.extents.x,bounds.extents.z)*.78f;
            foreach(float side in new[]{-1f,1f}) {
                Part(eyes,"Plant eye",PrimitiveType.Sphere,new Vector3(side*unit,unit*.4f,front),Vector3.one*unit,new Color(.86f,.84f,.75f),false);
                Part(eyes,"Plant pupil",PrimitiveType.Sphere,new Vector3(side*unit,unit*.4f,front+unit*.4f),Vector3.one*unit*.45f,new Color(.04f,.055f,.035f),false);
            }
            plantMouthScale=new Vector3(unit*.9f,unit*.15f,unit*.2f);
            plantMouth=Part(eyes,"Plant speaking mouth",PrimitiveType.Sphere,new Vector3(0,-unit*.7f,front),plantMouthScale,new Color(.055f,.065f,.04f),false);
            plantSpeech=new GameObject("Virtual plant dialogue").AddComponent<TextMesh>();
            plantSpeech.transform.SetParent(representationFit,false);
            plantSpeech.transform.localPosition=new Vector3(bounds.center.x,bounds.max.y+unit*5,bounds.center.z);
            plantSpeech.text="A little light helps me grow.\n(Virtual plant dialogue)";
            plantSpeech.fontSize=40;plantSpeech.characterSize=unit*.48f;plantSpeech.anchor=TextAnchor.MiddleCenter;
            plantSpeech.alignment=TextAlignment.Center;plantSpeech.color=new Color(.91f,.90f,.78f);
            plantSpeech.gameObject.SetActive(false);
            BuildPlantVoice();
        }
        void StepPlantDialogue(float now)
        {
            if(!plantSpeech)return;
            plantSpeech.gameObject.SetActive(state.awake);
            var viewer=Camera.main;
            if(viewer) {
                Vector3 forward=plantSpeech.transform.position-viewer.transform.position;
                if(forward.sqrMagnitude>.0001f)plantSpeech.transform.rotation=Quaternion.LookRotation(forward,viewer.transform.up);
                Vector3 facing=Vector3.ProjectOnPlane(viewer.transform.position-eyes.position,Vector3.up);
                if(facing.sqrMagnitude>.0001f)eyes.rotation=Quaternion.LookRotation(facing,Vector3.up);
            }
            StepPlantVoice();
            float syllable=PlantVoiceMouth;
            plantMouth.localScale=new Vector3(plantMouthScale.x,plantMouthScale.y*(1+syllable*3),plantMouthScale.z);
        }
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
