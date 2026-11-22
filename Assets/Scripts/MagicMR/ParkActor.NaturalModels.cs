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
            GameObject model;
            if(bronze) { bronzeStretch=FullBodyBronzeBeast.Create(visual);model=bronzeStretch.gameObject; }
            else {
                var prefab=Resources.Load<GameObject>("MagicMR/NaturalModels/StandingStatue");
                if(!prefab)throw new System.InvalidOperationException("Missing licensed natural statue prefab; run NaturalDecorAssetBuilder.Run");
                model=Instantiate(prefab,visual,false);model.name="Fictional standing sculpture - CC0 Quaternius base";
            }
            naturalStatueStretch=model.GetComponent<NaturalStatueStretch>();if(naturalStatueStretch)naturalStatueStretch.Initialize();
            naturalBronze=model.GetComponent<NaturalBronzeBeast>();if(naturalBronze)naturalBronze.Initialize();
            foreach(var renderer in model.GetComponentsInChildren<Renderer>())
            {
                body.Add(renderer);componentColors[renderer]=Color.white;
            }
            statueCartoon=model.AddComponent<NaturalCartoonStyle>();statueCartoon.Initialize();
        }

        NaturalCartoonStyle statueCartoon;
        NaturalStatueStretch naturalStatueStretch;
        FullBodyBronzeBeast bronzeStretch;
        public bool StatueCartoon=>scenario==ParkScenario.Statue&&state.style==1;

        void BuildNaturalRoom()
        {
            var parent=visual;
            RoomMesh(parent,"Timber joinery","Wood",Color.white);
            RoomMesh(parent,"Plaster walls","Plaster",Color.white);
            RoomMesh(parent,"Woven rug","Linen",new Color(.42f,.43f,.42f));
            RoomMesh(parent,"Table pedestal","RoomMetal",new Color(.055f,.065f,.06f));
            RoomMesh(parent,"Rust book cloth","BookCloth",new Color(.28f,.13f,.10f));
            RoomMesh(parent,"Olive book cloth","BookCloth",new Color(.16f,.21f,.17f));
            RoomMesh(parent,"Book pages","BookPaper",new Color(.78f,.75f,.66f));
            RoomProp(parent,"ReadingChair",new Vector3(-.16f,.014f,.025f));
            RoomProp(parent,"CeramicVase",new Vector3(.21f,.234f,.045f));
        }
        void RoomMesh(Transform parent,string name,string materialName,Color color)
        {
            var material=Resources.Load<Material>("MagicMR/NaturalMaterials/"+materialName);
            if(!material)throw new System.InvalidOperationException("Missing natural room material: "+materialName);
            var go=new GameObject(name);go.transform.SetParent(parent,false);
            go.AddComponent<MeshFilter>().sharedMesh=NaturalRoomMeshes.Get(name);
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
            if(virtualTarget){body.Add(renderer);componentColors[renderer]=color;}
        }
        void RoomProp(Transform parent,string name,Vector3 position)
        {
            var prefab=Resources.Load<GameObject>("MagicMR/NaturalModels/"+name);
            if(!prefab)throw new System.InvalidOperationException("Missing verified CC0 room prop: "+name);
            var model=Instantiate(prefab,parent,false);model.name=name;model.transform.localPosition=position;
            if(virtualTarget)foreach(var renderer in model.GetComponentsInChildren<Renderer>()){body.Add(renderer);componentColors[renderer]=Color.white;}
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
                // The original full-body bronze has four authored untextured finishes.
                // A shared white override would erase their colors; cartoon swaps retain them.
                if(bronzeStretch && renderer==bronzeStretch.Skin)continue;
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
            petOutline=model.AddComponent<NaturalPetOutline>();petOutline.Initialize(modelName=="BicolorCat");
        }

        NaturalPetOutline petOutline;
        public bool PetComic=>scenario==ParkScenario.Pet&&state.petComic;
        NaturalPetRig naturalPetRig;
        Transform mealTray;
        readonly Transform[] mealFood=new Transform[3];
        readonly Vector3[] mealFoodRest=new Vector3[3];
        public bool PetWalking=>naturalPetRig&&naturalPetRig.IsWalking;
        public bool PetFeeding=>naturalPetRig&&naturalPetRig.IsFeeding;
        public bool PetRestRequested=>naturalPetRig&&naturalPetRig.RestRequested;
        public bool TryPreviewPetRest(bool enabled)
        {
            if(scenario!=ParkScenario.Pet||!virtualTarget||!tracked||!isActiveAndEnabled||IsExiting||Busy||!naturalPetRig)
            {Feedback="Sleep preview requires a tracked virtual pet";return false;}
            bool accepted=naturalPetRig.SetResting(enabled);
            if(accepted)Feedback=enabled?"Virtual pet resting":"Virtual pet waking";
            return accepted;
        }
        System.Collections.IEnumerator FeedVirtualPet()
        {
            if(!naturalPetRig.BeginMeal())yield break;
            if(!mealTray)BuildMealTray();
            mealTray.localPosition=naturalPetRig.MealTrayPosition;
            mealTray.gameObject.SetActive(true);
            for(int i=0;i<mealFood.Length;i++)
            {
                mealFood[i].localPosition=mealFoodRest[i];
                mealFood[i].gameObject.SetActive(true);
            }
            Feedback="Pet eating";
            float elapsed=0;
            while(elapsed<NaturalPetRig.MealDuration)
            {
                naturalPetRig.SampleMeal(elapsed);
                for(int i=0;i<mealFood.Length;i++)
                {
                    bool uneaten=i>=naturalPetRig.ConsumedMorsels;
                    mealFood[i].gameObject.SetActive(uneaten);
                    if(!uneaten)continue;
                    float pickup=NaturalPetRig.MealPickupProgress(elapsed,i);
                    mealFood[i].localPosition=Vector3.Lerp(mealFoodRest[i],mealTray.InverseTransformPoint(naturalPetRig.MealMuzzlePosition),pickup);
                }
                yield return null;
                elapsed+=Time.unscaledDeltaTime;
            }
            StopPetMeal();
            Feedback="Pet finished eating";
        }
        void BuildMealTray()
        {
            mealTray=new GameObject("Virtual meal dish").transform;
            mealTray.SetParent(naturalPetRig.transform,false);
            float size=species=="Dog"?1.25f:1;
            Part(mealTray,"Dish base",PrimitiveType.Cylinder,Vector3.zero,new Vector3(.103f,.009f,.103f)*size,new Color(.055f,.44f,.39f),false);
            Part(mealTray,"Dish inset",PrimitiveType.Cylinder,Vector3.up*.008f*size,new Vector3(.079f,.002f,.079f)*size,new Color(.73f,.88f,.76f),false);
            for(int i=0;i<mealFood.Length;i++)
            {
                mealFoodRest[i]=new Vector3((i-1)*.018f,.015f,i==1?-.008f:0)*size;
                mealFood[i]=Part(mealTray,"Morsel "+(i+1),PrimitiveType.Sphere,mealFoodRest[i],
                    new Vector3(.014f,.012f,.014f)*size,new Color(.72f,.35f,.105f),false);
            }
        }
        void StopPetMeal()
        {
            if(naturalPetRig)naturalPetRig.ResetPose();
            if(mealTray)mealTray.gameObject.SetActive(false);
        }
        public bool TryPreviewPetWalk(bool enabled)
        {
            if(scenario!=ParkScenario.Pet||!virtualTarget||!tracked||!isActiveAndEnabled||IsExiting||Busy||!naturalPetRig){Feedback="Walking preview requires an idle tracked virtual pet";return false;}
            bool accepted=naturalPetRig.SetWalking(enabled);Feedback=enabled?"Virtual walk in place":"Virtual pet idle";return accepted;
        }
    }
}

