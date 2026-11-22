using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace MagicMR
{
    // Original finite point sculpture. Shapes are authored parametrically; no
    // external floral photograph, Gaussian model or webcam recognizer is used.
    public sealed class PointFlowerMorph : MonoBehaviour
    {
        public const int PointBudget=2048;
        const float BrushRadius=.075f;
        const float BrushPush=.035f;
        const float HandSampleLifetime=.16f;
        public int Shape {get;private set;}
        public int Palette {get;private set;}
        public bool Alive {get;private set;}
        public bool Active {get;private set;}
        public float Transition {get;private set;}=1;
        public int LivePoints=>cloud?cloud.particleCount:0;
        public int OwnedMaterialCount=>material?1:0;
        public string Status {get;private set;}="Particle flower: A colors, B breathing, C flower/sphere/cube/ring, D scatter and regroup";
        public Vector3[] CurrentPoints=>current;
        Transform root;ParticleSystem cloud;Material material;
        ParticleSystem.Particle[] particles;Vector3[] current,starts;Vector3[][] targets;Color[] flowerColors;
        struct State {public int shape,palette;public bool alive;public float burst;}
        float phase,burst;readonly State[] history=new State[12];int historyCount;
        Vector3 fingertip, brushWorld;
        float handRemaining, brushWeight;
        static float Sample(uint value){value^=value>>16;value*=0x7feb352d;value^=value>>15;value*=0x846ca68b;value^=value>>16;return (value&0x00ffffff)/16777216f;}
        public void Initialize(Transform visual)
        {
            if(root)return;var shader=Resources.Load<Shader>("MagicMR/SoftParticle");if(!shader)throw new InvalidOperationException("Point flower shader missing");
            root=new GameObject("Owned finite point flower").transform;root.SetParent(visual,false);root.localPosition=new Vector3(0,.30f,0);
            cloud=root.gameObject.AddComponent<ParticleSystem>();cloud.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=cloud.main;main.playOnAwake=false;main.loop=false;main.maxParticles=PointBudget;main.simulationSpace=ParticleSystemSimulationSpace.Local;main.scalingMode=ParticleSystemScalingMode.Hierarchy;main.startSpeed=0;
            var emission=cloud.emission;emission.enabled=false;var shape=cloud.shape;shape.enabled=false;
            material=new Material(shader){name="Owned point flower material"};var renderer=cloud.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            particles=new ParticleSystem.Particle[PointBudget];current=new Vector3[PointBudget];starts=new Vector3[PointBudget];targets=new Vector3[4][];flowerColors=new Color[PointBudget];
            for(int i=0;i<4;i++)targets[i]=new Vector3[PointBudget];
            for(int i=0;i<PointBudget;i++){
                float u=(i+.5f)/PointBudget;float angle=i*2.39996323f;
                if(u<.76f){
                    int petal=i%15,layer=petal/5;
                    float v=Mathf.Sqrt(Sample((uint)i+731)),across=Sample((uint)i+17041)*2-1;
                    float a=(petal%5)*Mathf.PI*2/5+layer*.48f+across*.38f*Mathf.Sin(v*Mathf.PI);
                    float radius=.009f+(.105f-layer*.021f)*v;
                    float height=.023f+layer*.018f+.038f*v*v+.01f*(1-across*across);
                    targets[0][i]=new Vector3(Mathf.Cos(a)*radius,height,Mathf.Sin(a)*radius);
                    flowerColors[i]=Color.Lerp(new Color(.9f,.20f,.48f),new Color(1,.72f,.82f),v);
                }else if(u<.88f){
                    float v=(u-.76f)/.12f;targets[0][i]=new Vector3(Mathf.Cos(angle)*.006f,-.14f+v*.165f,Mathf.Sin(angle)*.006f);flowerColors[i]=new Color(.19f,.48f,.10f);
                }else{
                    float v=(u-.88f)/.12f;float side=(i%2==0?1:-1);targets[0][i]=new Vector3(side*.09f*v,-.075f+.035f*Mathf.Sin(v*Mathf.PI),Mathf.Sin(angle)*.032f*Mathf.Sin(v*Mathf.PI));flowerColors[i]=new Color(.28f,.57f,.12f);
                }
                float y=1-2*u,radial=Mathf.Sqrt(Mathf.Max(0,1-y*y));targets[1][i]=new Vector3(Mathf.Cos(angle)*radial,y,Mathf.Sin(angle)*radial)*.105f;
                // Six independently filled faces, not a cube-shaped solid noise volume.
                int face=i%6;float a2=Mathf.Repeat(i*.61803399f,1)*2-1,b2=Mathf.Repeat(i*.41421356f,1)*2-1;
                targets[2][i]=(face<2?new Vector3(face==0?-1:1,a2,b2):face<4?new Vector3(a2,face==2?-1:1,b2):new Vector3(a2,b2,face==4?-1:1))*.095f;
                float ring=i*Mathf.PI*2/PointBudget;targets[3][i]=new Vector3(Mathf.Cos(ring)*(.085f+.023f*Mathf.Cos(angle)),.023f*Mathf.Sin(angle),Mathf.Sin(ring)*(.085f+.023f*Mathf.Cos(angle)));
            }
            SetActive(false);
        }
        void Remember(){if(historyCount==history.Length){Array.Copy(history,1,history,0,history.Length-1);historyCount--;}history[historyCount++]=new State{shape=Shape,palette=Palette,alive=Alive,burst=burst};}
        public void SetActive(bool value){Active=value;if(root)root.gameObject.SetActive(value);ResetCloud();}
        public void ResetCloud(){Shape=Palette=0;Alive=false;Transition=1;phase=burst=0;historyCount=0;handRemaining=brushWeight=0;if(root){root.localRotation=Quaternion.identity;root.localScale=Vector3.one;}if(current!=null){Array.Copy(targets[0],current,PointBudget);Draw();}}
        public void ObserveHand(Vector3 indexTip)
        {
            if(!Active||!root||!TabletopHandSample.Finite(indexTip)||
                Vector3.Distance(indexTip,root.position)>GestureReachWindow.EnterMeters)return;
            fingertip=indexTip;
            if(brushWeight==0)brushWorld=indexTip;
            handRemaining=HandSampleLifetime;
        }
        public void MorphTo(int value)
        {if(value<0||value>=4)return;Array.Copy(current,starts,PointBudget);Shape=value;Transition=0;}
        public bool TryAction(ParkAction action)
        {
            if(!Active)return false;
            if(action==ParkAction.Style){Remember();Palette=(Palette+1)%3;Status="Point flower palette changed";return true;}
            if(action==ParkAction.Awaken){Remember();Alive=!Alive;Status=Alive?"Point flower is breathing and turning":"Point flower rests";return true;}
            if(action==ParkAction.Wind){Remember();MorphTo((Shape+1)%4);Status="Morphing: "+new[]{"flower","sphere","cube","ring"}[Shape];return true;}
            if(action==ParkAction.Reward){Remember();burst=1;Status="Point flower scatters and regroups";return true;}
            if(action==ParkAction.Undo&&historyCount>0){var previous=history[--historyCount];MorphTo(previous.shape);Palette=previous.palette;Alive=previous.alive;burst=previous.burst;Status="Previous point effect restored";return true;}
            return false;
        }
        void Draw()
        {
            if(!cloud||current==null)return;if(!Active){cloud.Clear();return;}
            var brush=brushWeight>0?root.InverseTransformPoint(brushWorld):Vector3.zero;
            float scatter=burst>0?Mathf.Sin(burst*Mathf.PI)*.10f:0;
            for(int i=0;i<PointBudget;i++){
                var p=current[i];if(scatter>0)p+=p.normalized*scatter;
                if(brushWeight>0)
                {
                    var delta=p-brush;
                    float squared=delta.sqrMagnitude;
                    if(squared<BrushRadius*BrushRadius)
                    {
                        float distance=Mathf.Sqrt(squared);
                        float falloff=1-distance/BrushRadius;
                        var direction=distance>.001f?delta/distance:Vector3.up;
                        p+=direction*(BrushPush*brushWeight*falloff*falloff);
                    }
                }
                particles[i].position=p;particles[i].startSize=.0055f;var color=flowerColors[i];
                if(Palette==1)color=new Color(color.b*.65f,color.r*.8f,color.g*.5f+.45f);else if(Palette==2)color=new Color(1,color.r*.7f+.2f,color.g*.25f);
                particles[i].startColor=color;particles[i].startLifetime=10;particles[i].remainingLifetime=10;
            }
            cloud.SetParticles(particles,PointBudget);
        }
        public void Step(float dt)
        {
            if(!Active||!root||!root.gameObject.activeInHierarchy||!float.IsFinite(dt)||dt<0)return;dt=Mathf.Min(dt,.1f);phase+=dt;
            Transition=Mathf.MoveTowards(Transition,1,dt/.85f);float t=Mathf.SmoothStep(0,1,Transition);
            for(int i=0;i<PointBudget;i++)current[i]=Vector3.Lerp(starts[i],targets[Shape][i],t);
            burst=Mathf.MoveTowards(burst,0,dt/.9f);
            handRemaining=Mathf.Max(0,handRemaining-dt);
            brushWeight=Mathf.MoveTowards(brushWeight,handRemaining>0?1:0,dt*6);
            if(handRemaining>0)brushWorld=Vector3.Lerp(brushWorld,fingertip,1-Mathf.Exp(-24*dt));
            root.localRotation=Alive?Quaternion.Euler(0,phase*20,0):Quaternion.identity;
            root.localScale=Vector3.one*(Alive?1+.035f*Mathf.Sin(phase*2.4f):1);Draw();
        }
        void Update(){Step(Time.unscaledDeltaTime);}
        void OnDisable(){handRemaining=brushWeight=0;if(cloud)cloud.Clear();}
        void OnDestroy(){if(root)Destroy(root.gameObject);if(material)Destroy(material);}
    }
}
