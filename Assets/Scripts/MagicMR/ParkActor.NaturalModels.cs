using UnityEngine;

namespace MagicMR
{
    public sealed partial class ParkActor
    {

        Transform Segment(Transform parent,string name,Vector3 from,Vector3 to,float radius,Color color)
        {
            Vector3 direction=to-from;
            var segment=Part(parent,name,PrimitiveType.Cylinder,(from+to)*.5f,
                new Vector3(radius*2,direction.magnitude*.5f,radius*2),color);
            segment.localRotation=Quaternion.FromToRotation(Vector3.up,direction.normalized);
            return segment;
        }

        void BuildNaturalStatue()
        {
            bool bronze=species=="BronzeBeast";
            var prefab=Resources.Load<GameObject>("MagicMR/NaturalModels/"+(bronze?"BronzeBeast":"MarbleBust"));
            if(!prefab)throw new System.InvalidOperationException("Missing licensed natural statue prefab; run NaturalDecorAssetBuilder.Run");
            var model=Instantiate(prefab,visual,false);model.name=bronze?"CC0 bronze bovine bust by Tina - Poly Haven":"CC0 marble bust by Rico Cilliers - Poly Haven";
            naturalBronze=model.GetComponent<NaturalBronzeBeast>();if(naturalBronze)naturalBronze.Initialize();
            foreach(var renderer in model.GetComponentsInChildren<Renderer>())
            {
                body.Add(renderer);componentColors[renderer]=Color.white;
            }
        }

        void BuildNaturalRoom()
        {
            RoomMesh("Timber joinery","Wood",Color.white);
            RoomMesh("Plaster walls","Plaster",Color.white);
            RoomMesh("Woven rug","Linen",new Color(.42f,.43f,.42f));
            RoomMesh("Table pedestal","RoomMetal",new Color(.055f,.065f,.06f));
            RoomMesh("Rust book cloth","BookCloth",new Color(.28f,.13f,.10f));
            RoomMesh("Olive book cloth","BookCloth",new Color(.16f,.21f,.17f));
            RoomMesh("Book pages","BookPaper",new Color(.78f,.75f,.66f));
            RoomProp("ReadingChair",new Vector3(-.16f,.014f,.025f));
            RoomProp("CeramicVase",new Vector3(.21f,.234f,.045f));
        }
        void RoomMesh(string name,string materialName,Color color)
        {
            var material=Resources.Load<Material>("MagicMR/NaturalMaterials/"+materialName);
            if(!material)throw new System.InvalidOperationException("Missing natural room material: "+materialName);
            var go=new GameObject(name);go.transform.SetParent(visual,false);
            go.AddComponent<MeshFilter>().sharedMesh=NaturalRoomMeshes.Get(name);
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;body.Add(renderer);componentColors[renderer]=color;
        }
        void RoomProp(string name,Vector3 position)
        {
            var prefab=Resources.Load<GameObject>("MagicMR/NaturalModels/"+name);
            if(!prefab)throw new System.InvalidOperationException("Missing verified CC0 room prop: "+name);
            var model=Instantiate(prefab,visual,false);model.name=name;model.transform.localPosition=position;
            foreach(var renderer in model.GetComponentsInChildren<Renderer>()){body.Add(renderer);componentColors[renderer]=Color.white;}
        }

        Transform ModelMesh(Transform parent,string name,Mesh mesh,Vector3 position,Vector3 size,Color color)
        {
            var part=Part(parent,name,PrimitiveType.Cube,position,size,color);
            part.GetComponent<MeshFilter>().sharedMesh=mesh;
            return part;
        }

        void ApplyComponentColors(float style)
        {
            foreach(var renderer in body)
            {
                if(!renderer)continue;
                // Preserve bark, stone, fur and leaf colors through idle and Reset.
                // Artistic style is a shader transition, not a uniform green tint.
                renderer.GetPropertyBlock(block);
                block.SetColor("_BaseColor",componentColors[renderer]*(scenario==ParkScenario.Tree&&magicTree?Color.Lerp(Color.white,new Color(.42f,.48f,.52f),Mathf.Clamp01(style)):Color.white));
                block.SetFloat("_Style",scenario==ParkScenario.Tree?0:style);
                renderer.SetPropertyBlock(block);
            }
        }

        void BuildNaturalPet()
        {
            string modelName=species=="Dog"?"Wolfhound":"BicolorCat";
            var prefab=Resources.Load<GameObject>("MagicMR/NaturalModels/"+modelName);
            if(!prefab)throw new System.InvalidOperationException("Missing natural pet: "+modelName);
            var model=Instantiate(prefab,visual,false);naturalPetRig=model.GetComponent<NaturalPetRig>();
            if(!naturalPetRig)throw new System.InvalidOperationException("Natural pet rig missing: "+modelName);
            naturalPetRig.Initialize();
            foreach(var renderer in model.GetComponentsInChildren<Renderer>()){body.Add(renderer);componentColors[renderer]=Color.white;}
        }

        NaturalPetRig naturalPetRig;
        public bool PetWalking=>naturalPetRig&&naturalPetRig.IsWalking;
        public bool TryPreviewPetWalk(bool enabled)
        {
            if(scenario!=ParkScenario.Pet||!virtualTarget||!tracked||!isActiveAndEnabled||IsExiting||Busy||!naturalPetRig){Feedback="Walking preview requires an idle tracked virtual pet";return false;}
            bool accepted=naturalPetRig.SetWalking(enabled);Feedback=enabled?"Virtual walk in place":"Virtual pet idle";return accepted;
        }
    }
}

