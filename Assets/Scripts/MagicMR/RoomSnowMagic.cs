using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace MagicMR
{
    // Original bounded snow inside the existing virtual room. No room scanning,
    // webcam input, downloaded snow textures or replacement gesture detector.
    public sealed class RoomSnowMagic : MonoBehaviour
    {
        public const int FlakeBudget=160;
        const float WindDuration = .9f;
        public bool Active {get;private set;}
        public bool Snowing {get;private set;}
        public bool Gathering {get;private set;}
        public bool Frozen {get;private set;}
        public int LiveFlakes=>cloud?cloud.particleCount:0;
        public int OwnedMaterialCount=>material?1:0;
        public Vector3[] Positions=>positions;
        public Vector3 FocusLocal=>hand;
        public string Status {get;private set;}="Snow room: A snow on/off, B gather/release, C wind, D freeze/unfreeze";
        struct State {public bool on,gather,frozen;public float windRemaining;}
        readonly State[] history=new State[12];readonly Vector3[][] savedPositions=new Vector3[12][];int historyCount;
        Transform root;ParticleSystem cloud;Material material;ParticleSystem.Particle[] particles;
        Vector3[] positions, snowfallPositions;Vector3 hand;float handAt=-1,windRemaining;bool hasHand;
        public void Initialize(Transform visual)
        {
            if(root)return;var shader=Resources.Load<Shader>("MagicMR/Snowflake");if(!shader)throw new InvalidOperationException("Snowflake shader missing");
            root=new GameObject("Owned bounded room snow").transform;root.SetParent(visual,false);
            cloud=root.gameObject.AddComponent<ParticleSystem>();cloud.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=cloud.main;main.playOnAwake=false;main.loop=false;main.maxParticles=FlakeBudget;main.simulationSpace=ParticleSystemSimulationSpace.Local;main.scalingMode=ParticleSystemScalingMode.Hierarchy;main.startSpeed=0;
            var emission=cloud.emission;emission.enabled=false;var shape=cloud.shape;shape.enabled=false;
            material=new Material(shader){name="Owned room snow material"};var renderer=cloud.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            positions=new Vector3[FlakeBudget];particles=new ParticleSystem.Particle[FlakeBudget];for(int i=0;i<12;i++)savedPositions[i]=new Vector3[FlakeBudget];
            snowfallPositions = new Vector3[FlakeBudget];
            for (int i = 0; i < FlakeBudget; i++) snowfallPositions[i] = SnowfallPosition(i);
            SetActive(false);
        }

        static Vector3 SnowfallPosition(int index)
        {
            // Spread the budget across the room, with independent offsets inside each cell.
            uint seed = (uint)index * 3;
            return new Vector3(
                -.30f + (index % 3 + SnowOffset(seed + 1)) * .20f,
                .04f + (index / 3 % 3 + SnowOffset(seed + 2)) * .19f,
                -.25f + (index / 9 % 3 + SnowOffset(seed + 3)) / 6f);
        }

        static float SnowOffset(uint seed)
        {
            unchecked
            {
                seed = (seed ^ (seed >> 16)) * 0x7feb352d;
                seed = (seed ^ (seed >> 15)) * 0x846ca68b;
                return (seed >> 8) * (1f / (1 << 24));
            }
        }

        public void SetActive(bool value){Active=value;Clear();if(root)root.gameObject.SetActive(value);}
        public void Clear(){Snowing=Gathering=Frozen=false;hasHand=false;handAt=-1;windRemaining=0;historyCount=0;if(positions!=null)Array.Copy(snowfallPositions,positions,FlakeBudget);if(cloud)cloud.Clear();}
        public void ObserveHand(Vector3 point,float time)
        {if(!Active||!TabletopHandSample.Finite(point)||!float.IsFinite(time)){hasHand=false;Gathering=false;return;}hand=root.InverseTransformPoint(point);hand=new Vector3(Mathf.Clamp(hand.x,-.3f,.3f),Mathf.Clamp(hand.y,.015f,.61f),Mathf.Clamp(hand.z,-.25f,.25f));handAt=time;hasHand=true;}
        void Remember(){if(historyCount==12){for(int i=1;i<12;i++){history[i-1]=history[i];Array.Copy(savedPositions[i],savedPositions[i-1],FlakeBudget);}historyCount=11;}history[historyCount]=new State{on=Snowing,gather=Gathering,frozen=Frozen,windRemaining=windRemaining};Array.Copy(positions,savedPositions[historyCount],FlakeBudget);historyCount++;}
        public bool TryAction(ParkAction action)
        {
            if(!Active)return false;
            if(action==ParkAction.Undo){if(historyCount==0)return false;var state=history[--historyCount];Snowing=state.on;Gathering=state.gather;Frozen=state.frozen;windRemaining=state.windRemaining;Array.Copy(savedPositions[historyCount],positions,FlakeBudget);Draw();Status="Previous snow state restored";return true;}
            if(action==ParkAction.Style){Remember();Snowing=!Snowing;if(!Snowing){Gathering=Frozen=false;windRemaining=0;}Status=Snowing?"Snowfall begins inside the virtual room":"Snowfall stopped";Draw();return true;}
            if(!Snowing){Status="Start snowfall with A first";return false;}
            if(action==ParkAction.Awaken){Remember();Gathering=!Gathering;Frozen=false;windRemaining=0;Status=Gathering?"Snow gathers at your fingertips":"Snow released to fall";return true;}
            if(action==ParkAction.Wind){Remember();Frozen=Gathering=false;windRemaining=WindDuration;Status="A gust scatters snow across the room";return true;}
            if(action==ParkAction.Reward){Remember();Frozen=!Frozen;Status=Frozen?"Snow suspended in midair":"Snowfall resumes";return true;}
            return false;
        }
        void Draw()
        {
            if(!cloud)return;if(!Active||!Snowing){cloud.Clear();return;}
            for(int i=0;i<FlakeBudget;i++){
                particles[i].position=positions[i];particles[i].startSize=(.012f+Mathf.Repeat(i*.618f,1)*.010f)*(Gathering?.55f:1);particles[i].rotation=Mathf.Repeat(i*137.5f,360);
                particles[i].startColor=Frozen?new Color(.64f,.86f,1,.95f):new Color(.95f,.98f,1,.84f);particles[i].startLifetime=10;particles[i].remainingLifetime=10;
            }
            cloud.SetParticles(particles,FlakeBudget);
        }
        public void Step(float dt)
        {
            if(!Active||!Snowing||!float.IsFinite(dt)||dt<0)return;dt=Mathf.Min(dt,.1f);
            if(Gathering&&(!hasHand||Time.unscaledTime-handAt>TabletopGestureRules.MaximumSampleGapSeconds))Gathering=false;
            float windAfterStep = Mathf.Max(0, windRemaining - dt);
            float windFraction = windRemaining > 0 ? 1 - windAfterStep / windRemaining : 0;
            var windOffset = new Vector3(.15f, .035f, -.04f)
                * Mathf.Sin(Mathf.PI * windAfterStep / WindDuration);
            if(!Frozen)for(int i=0;i<FlakeBudget;i++){
                var p=positions[i];
                if(Gathering){float a=i*2.399963f;var s=root.lossyScale;float radius=Mathf.Min(.035f,.025f/Mathf.Max(.1f,Mathf.Max(Mathf.Abs(s.x),Mathf.Max(Mathf.Abs(s.y),Mathf.Abs(s.z)))));var aim=hand+new Vector3(Mathf.Cos(a),Mathf.Sin(a*.7f),Mathf.Sin(a))*radius;p=Vector3.MoveTowards(p,aim,dt*.45f);}
                else if (windRemaining > 0)
                {
                    // Drive each flake toward its own room-wide position. The gust
                    // bends the path sideways without moving the destination.
                    var destination = snowfallPositions[i] + windOffset;
                    destination.x = Mathf.Clamp(destination.x, -.30f, .30f);
                    destination.y = Mathf.Clamp(destination.y, .04f, .61f);
                    destination.z = Mathf.Clamp(destination.z, -.25f, .25f);
                    p = Vector3.Lerp(p, destination, windFraction);
                }
                else{
                    p+=new Vector3(Mathf.Sin(i*2.399f+Time.unscaledTime)*.012f,-.045f,0)*dt;
                    if(p.y<.04f)p.y=.61f;if(p.y>.62f)p.y=.06f;
                    if(p.x>.31f)p.x=-.30f;if(p.x<-.31f)p.x=.30f;if(p.z>.26f)p.z=-.25f;if(p.z<-.26f)p.z=.25f;
                }
                positions[i]=p;
            }
            if (!Frozen) windRemaining = windAfterStep;
            Draw();
        }
        void Update(){Step(Time.unscaledDeltaTime);}
        void OnDisable(){Clear();}
        void OnDestroy(){if(root)Destroy(root.gameObject);if(material)Destroy(material);}
    }
}
