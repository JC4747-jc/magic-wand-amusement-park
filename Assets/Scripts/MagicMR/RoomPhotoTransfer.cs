using System;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace MagicMR
{
    // Original effect using the existing room relief. The image is a rendered preview,
    // not a reconstructed photograph or a replacement for real target recognition.
    public sealed class RoomPhotoTransfer : MonoBehaviour
    {
        public const int Columns=32, Rows=48, ParticleBudget=Columns*Rows;
        public const float Duration=1.85f;
        public bool Running { get; private set; }
        public float Elapsed { get; private set; }
        public string Stage=>!Running?"idle":Elapsed<.2f?"photo":Elapsed<.85f?"gather":Elapsed<1.02f?"held":"release";
        public float Reveal=>Running?Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.18f,Duration,Elapsed)):1;
        public int LiveParticles=>particles?particles.particleCount:0;
        public Texture2D Photo=>photo;
        public Vector3 Focus=>focus;
        Transform window,card;
        Renderer landscape;
        ParticleSystem particles;
        ParticleSystem.Particle[] buffer;
        Vector3[] imagePoints,scenePoints;
        Color32[] colors;
        Texture2D photo, lakeArtwork;
        Material photoMaterial,particleMaterial;
        Vector3 focus,cardOrigin;
        Quaternion cardRotation;
        float sceneScale;

        public void Initialize(Transform roomWindow,Renderer roomLandscape,Texture2D lake=null)
        { window=roomWindow;landscape=roomLandscape;lakeArtwork=lake; }

        internal void FollowPalm(Vector3 palm)
        {
            if(lakeArtwork && Running && Elapsed<1.02f && TabletopHandSample.Finite(palm))
                focus=palm+Vector3.up*.025f;
        }

        void Prepare()
        {
            if(photo)return;
            var soft=Resources.Load<Shader>("MagicMR/SoftParticle");
            var unlit=Resources.Load<Shader>("MagicMR/PhotoCard");
            if(!soft||!unlit)throw new InvalidOperationException("Photo transfer shaders missing");
            if(lakeArtwork){
                photo=lakeArtwork;
                imagePoints=new Vector3[ParticleBudget];scenePoints=new Vector3[ParticleBudget];colors=new Color32[ParticleBudget];
                for(int y=0;y<Rows;y++)for(int x=0;x<Columns;x++){
                    int i=y*Columns+x;
                    float u=(x+.5f)/Columns,v=(y+.5f)/Rows;
                    imagePoints[i]=new Vector3((u-.5f)*.20f,(v-.5f)*.325f,0);
                    scenePoints[i]=RoomWindowLandscape.LakePoint(u,v);
                    colors[i]=photo.GetPixelBilinear(u,v);
                }
            }else LoadBakedPhoto();
            card=GameObject.CreatePrimitive(PrimitiveType.Quad).transform;card.name="Room photo card";card.SetParent(transform,false);
            var collider=card.GetComponent<Collider>();collider.enabled=false;Destroy(collider);
            photoMaterial=new Material(unlit){name="Owned room photo card"};photoMaterial.SetTexture("_BaseMap",photo);
            card.GetComponent<Renderer>().sharedMaterial=photoMaterial;card.gameObject.SetActive(false);
            var go=new GameObject("Room photo particles - bounded");go.transform.SetParent(transform,false);
            particles=go.AddComponent<ParticleSystem>();particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=particles.main;main.playOnAwake=false;main.loop=false;main.maxParticles=ParticleBudget;
            main.simulationSpace=ParticleSystemSimulationSpace.World;main.startSpeed=0;
            var emission=particles.emission;emission.enabled=false;var shape=particles.shape;shape.enabled=false;
            var renderer=go.GetComponent<ParticleSystemRenderer>();particleMaterial=new Material(soft){name="Owned room photo particles"};
            renderer.sharedMaterial=particleMaterial;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            buffer=new ParticleSystem.Particle[ParticleBudget];
        }

        void LoadBakedPhoto()
        {
            // Both resources come from the fixed original room relief. Loading
            // them avoids a blocking GPU readback and triangle scan in a hand frame.
            var preview=Resources.Load<TextAsset>("MagicMR/RoomPhotoPreview");
            var depths=Resources.Load<TextAsset>("MagicMR/RoomPhotoDepth");
            if(!preview||!depths)throw new InvalidOperationException("Baked room photo resources missing");
            var data=depths.bytes;
            if(data.Length!=2*sizeof(int)+ParticleBudget*sizeof(float))
                throw new InvalidOperationException("Room photo depth lattice has the wrong size");
            using var stream=new MemoryStream(data,false);
            using var reader=new BinaryReader(stream);
            if(reader.ReadInt32()!=Columns||reader.ReadInt32()!=Rows)
                throw new InvalidOperationException("Room photo depth lattice dimensions changed");
            var texture=new Texture2D(128,208,TextureFormat.RGB24,false){name="Owned room rendered photo preview"};
            if(!texture.LoadImage(preview.bytes)||texture.width!=128||texture.height!=208){
                Destroy(texture);
                throw new InvalidOperationException("Room photo preview could not be decoded");
            }
            photo=texture;
            imagePoints=new Vector3[ParticleBudget];
            scenePoints=new Vector3[ParticleBudget];
            colors=new Color32[ParticleBudget];
            for(int y=0;y<Rows;y++)for(int x=0;x<Columns;x++) {
                int i=y*Columns+x;float u=(x+.5f)/Columns,v=(y+.5f)/Rows;
                imagePoints[i]=new Vector3((u-.5f)*.20f,(v-.5f)*.325f,0);
                scenePoints[i]=new Vector3((u-.5f)*.32f,v*.516f,reader.ReadSingle());
                colors[i]=photo.GetPixelBilinear(u,v);
            }
        }
        public void Begin(Vector3 hand)
        {
            if(!TabletopHandSample.Finite(hand)||!window||!landscape)return;
            Prepare();Cancel();focus=hand;Elapsed=0;Running=true;
            sceneScale=lakeArtwork?1:Mathf.Clamp(window.parent.lossyScale.y,.1f,3);
            var mainCamera=Camera.main;
            var right=mainCamera?mainCamera.transform.right:window.parent.right;
            cardOrigin=hand+(right*.12f+Vector3.up*.12f)*sceneScale;
            var camera=Camera.main;cardRotation=camera?Quaternion.LookRotation(cardOrigin-camera.transform.position,camera.transform.up):window.rotation;
            card.SetPositionAndRotation(cardOrigin,cardRotation);card.localScale=Vector3.one;
            // Keep the preview proportional to the fitted room; the photograph
            // must not become a full-size panel through miniature furniture.
            card.localScale=new Vector3(.20f*sceneScale/Mathf.Max(.001f,Mathf.Abs(transform.lossyScale.x)),.325f*sceneScale/Mathf.Max(.001f,Mathf.Abs(transform.lossyScale.y)),1);
            card.gameObject.SetActive(true);Draw();
        }
        public void Step(float dt)
        {
            if(!Running||!float.IsFinite(dt)||dt<0)return;
            Elapsed=Mathf.Min(Duration,Elapsed+Mathf.Min(dt,.1f));Draw();
            if(Elapsed>=Duration){Running=false;card.gameObject.SetActive(false);particles.Clear();}
        }
        void Draw()
        {
            card.gameObject.SetActive(Elapsed<.2f);
            if(Elapsed<.2f){particles.Clear();return;}
            float gather=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.2f,.85f,Elapsed));
            float release=Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.02f,Duration,Elapsed));
            for(int i=0;i<ParticleBudget;i++) {
                float a=i*2.399963f;var offset=new Vector3(Mathf.Cos(a),Mathf.Sin(a),Mathf.Sin(a*.71f))*(.013f*sceneScale);
                var source=cardOrigin+cardRotation*(imagePoints[i]*sceneScale);
                var held=focus+offset;
                if(lakeArtwork){
                    float h=imagePoints[i].y/.325f;
                    float angle=a+6*h+Elapsed*7;
                    float radius=.004f+.012f*(1-4*h*h);
                    held=focus+cardRotation*new Vector3(radius*Mathf.Cos(angle)+.018f*h,.095f*h+.04f,radius*Mathf.Sin(angle));
                }
                var target=window.parent.TransformPoint(window.localPosition+scenePoints[i]);
                var p=Elapsed<1.02f?Vector3.Lerp(source,held,gather):Vector3.Lerp(held,target,release);
                if(lakeArtwork){
                    float arc=Mathf.Sin(Mathf.PI*(Elapsed<1.02f?gather:release));
                    p+=cardRotation*new Vector3(Mathf.Cos(a+Elapsed*8)*.035f,.07f+.02f*Mathf.Sin(a+Elapsed*8),0)*arc;
                }
                if(Elapsed>=1.02f)p+=window.parent.TransformDirection(new Vector3(Mathf.Sin(a),Mathf.Cos(a),-1))*(Mathf.Sin(release*Mathf.PI)*.025f*sceneScale);
                buffer[i].position=p;buffer[i].startSize=Mathf.Lerp(.0065f,.004f,release)*Mathf.Min(sceneScale,1.5f);
                if(lakeArtwork)buffer[i].startSize*=1-.70f*gather*(1-release);
                var color=colors[i];
                if(lakeArtwork && i%13==0)color=Color32.Lerp(color,new Color32(255,190,117,255),gather*(1-release));
                color.a=(byte)(255*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.65f,1,release))));
                buffer[i].startColor=color;buffer[i].startLifetime=10;buffer[i].remainingLifetime=10;
            }
            particles.SetParticles(buffer,ParticleBudget);
        }
        public void Cancel(){Running=false;Elapsed=0;if(card)card.gameObject.SetActive(false);if(particles)particles.Clear();}
        void OnDisable(){Cancel();}
        void OnDestroy(){if(card)Destroy(card.gameObject);if(particles)Destroy(particles.gameObject);if(photo && !lakeArtwork)Destroy(photo);if(photoMaterial)Destroy(photoMaterial);if(particleMaterial)Destroy(particleMaterial);}
    }
}
