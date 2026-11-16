using System.Collections.Generic;
using UnityEngine;

namespace MagicMR
{
    public sealed partial class ParkActor
    {
        Transform naturalTreeGrowth;
        Renderer naturalTreeLeaves;
        SkinnedMeshRenderer treeBranchSkin;
        SkinnedMeshRenderer seasonTreeLeaves,willowTreeLeaves,willowTreeBranches;
        WillowCanopyData willowCanopy;
        bool willowMeshApplied;
        float treeGustUntil=float.PositiveInfinity;
        Transform treeChestLid;
        float treeChestOpen;
        Renderer treeScarRenderer;
        Transform BuildTreeChest(){
            var chest=new GameObject("Reward chest").transform;chest.SetParent(visual,false);chest.localPosition=new Vector3(.2f,.015f,0);
            var wood=new Color(.25f,.12f,.055f);var brass=new Color(.56f,.38f,.14f);
            Part(chest,"Chest floor",PrimitiveType.Cube,new Vector3(0,.004f,0),new Vector3(.12f,.008f,.1f),wood,false);
            foreach(float z in new[]{-.046f,.046f})Part(chest,"Front/back wall",PrimitiveType.Cube,new Vector3(0,.025f,z),new Vector3(.12f,.05f,.008f),wood,false);
            foreach(float x in new[]{-.056f,.056f})Part(chest,"Side wall",PrimitiveType.Cube,new Vector3(x,.025f,0),new Vector3(.008f,.05f,.084f),wood,false);
            for(int i=0;i<7;i++)Part(chest,"Treasure coin",PrimitiveType.Cylinder,new Vector3((i%3-1)*.027f,.012f+(i/3)*.003f,(i/3-1)*.024f),new Vector3(.022f,.002f,.022f),brass,false);
            treeChestLid=new GameObject("Hinged lid").transform;treeChestLid.SetParent(chest,false);treeChestLid.localPosition=new Vector3(0,.05f,.05f);
            Part(treeChestLid,"Lid wood",PrimitiveType.Cube,new Vector3(0,.008f,-.05f),new Vector3(.126f,.016f,.106f),wood,false);
            foreach(float x in new[]{-.04f,.04f}){
                foreach(float z in new[]{-.051f,.051f})Part(chest,"Brass strap",PrimitiveType.Cube,new Vector3(x,.025f,z),new Vector3(.009f,.05f,.003f),brass,false);
                Part(treeChestLid,"Lid strap",PrimitiveType.Cube,new Vector3(x,.009f,-.05f),new Vector3(.009f,.019f,.108f),brass,false);
            }
            Part(chest,"Clasp",PrimitiveType.Cube,new Vector3(0,.037f,-.052f),new Vector3(.018f,.025f,.006f),brass,false);
            return chest;
        }
        void StepWillowEffects(float dt,float now){
            if(treeScarRenderer){treeScarRenderer.GetPropertyBlock(block);block.SetFloat("_Breath",magicTree&&state.scar&&!state.sealedTree?.5f+.5f*Mathf.Sin(now*2.5f):0);treeScarRenderer.SetPropertyBlock(block);}
            treeChestOpen=Mathf.MoveTowards(treeChestOpen,magicTree&&state.rewarded?1:0,dt/.65f);
            if(treeChestLid)treeChestLid.localRotation=Quaternion.Euler(Mathf.SmoothStep(0,1,treeChestOpen)*105,0,0);
        }
        readonly List<Transform> treeEyelids=new List<Transform>();
        readonly List<Vector3> treeEyeScales=new List<Vector3>();
        void BuildSeasonTreeFace() {
            var anchor=naturalTreeGrowth.Find("FantasyScarAnchor");
            var mark=scar.GetComponentInChildren<MeshFilter>();
            float unit=Mathf.Clamp(mark.sharedMesh.bounds.size.x*.22f,.005f,.012f);
            eyes.SetParent(anchor,false);eyes.localPosition=Vector3.forward*unit*.6f;
            foreach(float side in new[]{-1f,1f}) {
                var eye=Part(eyes,"Tree eye",PrimitiveType.Sphere,new Vector3(side*unit,unit*.55f,0),new Vector3(unit*.85f,unit*.7f,unit*.45f),new Color(.78f,.72f,.52f),false);
                Part(eye,"Tree pupil",PrimitiveType.Sphere,new Vector3(0,0,.42f),new Vector3(.42f,.58f,.55f),new Color(.065f,.05f,.035f),false);
                treeEyelids.Add(eye);treeEyeScales.Add(eye.localScale);
            }
            var smile=new GameObject("Tree smile").AddComponent<LineRenderer>();smile.transform.SetParent(eyes,false);
            smile.useWorldSpace=false;smile.positionCount=9;smile.widthMultiplier=unit*.16f;smile.sharedMaterial=sharedMaterial;
            for(int i=0;i<9;i++){float x=(i/8f-.5f)*1.4f;smile.SetPosition(i,new Vector3(x*unit,(-.7f+x*x*.5f)*unit,unit*.25f));}
            var tint=new MaterialPropertyBlock();tint.SetColor("_BaseColor",new Color(.07f,.05f,.035f));smile.SetPropertyBlock(tint);
        }
        void StepSeasonTreeFace(float now) {
            float blink=state.awake&&!magicTree?1-Mathf.Pow(Mathf.Max(0,Mathf.Cos(now*1.6f)),24)*.9f:1;
            for(int i=0;i<treeEyelids.Count;i++)treeEyelids[i].localScale=Vector3.Scale(treeEyeScales[i],new Vector3(1,blink,1));
        }
        readonly List<Quaternion> treeBranchRest=new List<Quaternion>();
        public Vector3 TreeScarWorldPosition=>scar?scar.position:transform.position;
        void BuildNaturalTreeMark()
        {
            var anchor=naturalTreeGrowth.Find("FantasyScarAnchor");var prefab=Resources.Load<GameObject>("MagicMR/NaturalModels/TreeScar");
            if(!anchor||!prefab)throw new System.InvalidOperationException("Missing surface-fitted fantasy bark mark; run NaturalTreeMarkAssetBuilder.Run");
            scar=Instantiate(prefab,anchor,false).transform;scar.name="Virtual scar";
            treeScarRenderer=scar.GetComponentInChildren<Renderer>();
        }
        void BuildNaturalTree()
        {
            var prefab=Resources.Load<GameObject>("MagicMR/NaturalModels/SmallTree");
            if(!prefab)throw new System.InvalidOperationException("Missing CC0 small tree; run NaturalTreeAssetBuilder.Run");
            var model=Instantiate(prefab,visual,false);naturalTreeGrowth=model.transform.Find("TreeGrowth");
            var transforms=model.GetComponentsInChildren<Transform>();
            for(int i=0;i<5;i++)
            {
                Transform bone=null;foreach(var t in transforms)if(t.name=="Branch_"+i.ToString("D2")){bone=t;break;}
                if(!bone)throw new System.InvalidOperationException("Tree wind sector missing");
                branches.Add(bone);treeBranchRest.Add(bone.localRotation);
            }
            foreach(var renderer in model.GetComponentsInChildren<Renderer>())
            {
                body.Add(renderer);componentColors[renderer]=Color.white;
                if(renderer.sharedMaterial.shader.name=="MagicMR/NaturalTreeLeaves")naturalTreeLeaves=renderer;
            }
            if(!naturalTreeGrowth||!naturalTreeLeaves)throw new System.InvalidOperationException("Tree growth/season representation missing");
            var leafSkin=naturalTreeLeaves as SkinnedMeshRenderer;
            foreach(var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>())if(skin!=leafSkin)treeBranchSkin=skin;
            if(!leafSkin||!treeBranchSkin)throw new System.InvalidOperationException("Tree canopy skin missing");
            seasonTreeLeaves=leafSkin;
            willowCanopy=Resources.Load<WillowCanopyData>("MagicMR/NaturalModels/WillowCanopy");
            if(!willowCanopy||!willowCanopy.leaves||!willowCanopy.branches||!willowCanopy.leafMaterial)
                throw new System.InvalidOperationException("Authored willow canopy missing; run WillowCanopyAssetBuilder.Run");
            SkinnedMeshRenderer Canopy(string name,Mesh mesh,Material material,Transform[] bones){
                var skin=new GameObject(name).AddComponent<SkinnedMeshRenderer>();skin.transform.SetParent(naturalTreeGrowth,false);
                skin.sharedMesh=mesh;skin.sharedMaterial=material;skin.bones=bones;skin.rootBone=naturalTreeGrowth;skin.quality=SkinQuality.Bone2;
                float radius=Mathf.Max(mesh.bounds.min.magnitude,mesh.bounds.max.magnitude)*1.3f;skin.localBounds=new Bounds(Vector3.zero,Vector3.one*radius*2);
                skin.enabled=false;body.Add(skin);componentColors[skin]=Color.white;return skin;
            }
            willowTreeLeaves=Canopy("Willow leaves",willowCanopy.leaves,willowCanopy.leafMaterial,leafSkin.bones);
            willowTreeBranches=Canopy("Willow branches",willowCanopy.branches,treeBranchSkin.sharedMaterial,treeBranchSkin.bones);
        }
        void ApplyTreeCanopyVariant()
        {
            if(!willowCanopy||!naturalTreeLeaves||willowMeshApplied==magicTree)return;
            willowMeshApplied=magicTree;
            seasonTreeLeaves.enabled=treeBranchSkin.enabled=!magicTree;
            willowTreeLeaves.enabled=willowTreeBranches.enabled=magicTree;
            naturalTreeLeaves=magicTree?willowTreeLeaves:seasonTreeLeaves;
            ApplyComponentColors(styleMix*2);
        }
        void ApplyNaturalTreeGrowth()
        {
            if(naturalTreeGrowth)naturalTreeGrowth.localScale=Vector3.one*(Mathf.Lerp(.78f,1.26f,growthTime)/.996f);
        }
        void ApplyNaturalTreeSeason()
        {
            if(!naturalTreeLeaves)return;
            float season=Mathf.Repeat(seasonTime,1)*4;
            // Retain original albedo in summer. Autumn changes hue through luminance, preserving photographed detail.
            float fall=Mathf.SmoothStep(0,1,Mathf.InverseLerp(1f,2f,season));
            float spring=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,.8f,season));
            float winter=Mathf.SmoothStep(0,1,Mathf.InverseLerp(2f,3f,season));
            float recovery=Mathf.SmoothStep(0,1,Mathf.InverseLerp(3.5f,4,season));
            float coverage=Mathf.Lerp(Mathf.Lerp(1,.025f,winter),1,recovery);
            naturalTreeLeaves.GetPropertyBlock(block);
            block.SetColor("_SeasonTint",Color.Lerp(new Color(.33f,.49f,.14f),new Color(.95f,.48f,.08f),fall));
            block.SetFloat("_SeasonMix",Mathf.Max(spring*.45f,fall*.92f)*(1-recovery));
            block.SetFloat("_LeafCoverage",coverage);
            block.SetFloat("_ToonMix",magicTree?0:Mathf.Clamp01(styleMix*2));naturalTreeLeaves.SetPropertyBlock(block);
        }
        void ResetNaturalTreePose()
        {
            ApplyTreeCanopyVariant();
            treeChestOpen=0;StepWillowEffects(0,0);
            for(int i=0;i<branches.Count&&i<treeBranchRest.Count;i++)branches[i].localRotation=treeBranchRest[i];
            ApplyNaturalTreeGrowth();ApplyNaturalTreeSeason();
        }
    }
}
