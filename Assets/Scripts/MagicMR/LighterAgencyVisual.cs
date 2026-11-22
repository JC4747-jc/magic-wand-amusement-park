using UnityEngine;
using UnityEngine.Rendering;

namespace MagicMR
{
    // Decorative geometry only; never participates in real-object registration or physics.
    public sealed class LighterAgencyVisual : MonoBehaviour
    {
        Transform left,right;GameObject wings;Mesh membrane;Material material;
        ParticleSystem cough;AudioSource audioSource;Vector3 mouth,backMouth;
        float started,nextCough;bool active;
        public int CoughCount {get;private set;}
        public bool Visible=>active&&wings&&wings.activeSelf;
        public float WingAngle {get;private set;}
        public int SmokeCount=>cough?cough.particleCount:0;
        public Mesh SharedWingMesh=>membrane;
        public float WingReach=>left?left.TransformVector(Vector3.right*.62f).magnitude:0;
        public void Initialize(Bounds body,AudioSource source)
        {
            if(wings)return;
            audioSource=source;float height=Mathf.Max(.001f,body.size.y);
            material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Owned charcoal wing membrane"};
            material.SetColor("_BaseColor",new Color(.20f,.085f,.065f));material.SetFloat("_Smoothness",.25f);
            // A creased, scalloped membrane. Both faces share one tiny mesh and one material.
            var vertices=new[]{new Vector3(0,0,0),new Vector3(.15f,.28f,-.025f),new Vector3(.62f,.43f,0),new Vector3(.51f,.08f,-.05f),new Vector3(.44f,-.15f,0),new Vector3(.26f,-.04f,-.045f),new Vector3(.11f,-.23f,0),new Vector3(.04f,-.08f,-.02f)};
            var faces=new Vector3[16];System.Array.Copy(vertices,faces,8);System.Array.Copy(vertices,0,faces,8,8);
            var triangles=new int[36];for(int n=0;n<6;n++){int i=n*6;triangles[i]=0;triangles[i+1]=n+1;triangles[i+2]=n+2;triangles[i+3]=8;triangles[i+4]=n+10;triangles[i+5]=n+9;}
            membrane=new Mesh{name="Owned sixteen-vertex creased wing"};membrane.vertices=faces;membrane.triangles=triangles;membrane.RecalculateNormals();membrane.RecalculateBounds();
            wings=new GameObject("Agency wings (presentation only)");wings.layer=gameObject.layer;wings.transform.SetParent(transform,false);
            wings.transform.localPosition=new Vector3(body.center.x,body.min.y+height*.58f,body.center.z-body.extents.z*.4f);
            left=Wing("Left membrane",-1,height,body.extents.x);right=Wing("Right membrane",1,height,body.extents.x);
            mouth=new Vector3(body.center.x,body.min.y+height*.67f,body.max.z+height*.02f);
            backMouth=new Vector3(mouth.x,mouth.y,body.min.z-height*.02f);
            cough=MagicMRVfxFactory.CreateAgencyCough(transform);Stop();
        }
        Transform Wing(string label,float side,float height,float halfWidth)
        {
            var go=new GameObject(label);go.layer=gameObject.layer;go.transform.SetParent(wings.transform,false);
            go.transform.localPosition=Vector3.right*halfWidth*side*.85f;var scale=transform.lossyScale;
            float x=Mathf.Max(.000001f,Mathf.Abs(scale.x)),y=Mathf.Max(.000001f,Mathf.Abs(scale.y)),z=Mathf.Max(.000001f,Mathf.Abs(scale.z));
            go.transform.localScale=new Vector3(side*height*y/x,height,height*y/z);
            go.AddComponent<MeshFilter>().sharedMesh=membrane;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;return go.transform;
        }
        public void Begin(){Stop();active=true;started=Time.unscaledTime;nextCough=started+.35f;CoughCount=0;wings.SetActive(true);Step();}
        public void Step()
        {
            if(!active)return;
            float elapsed=Time.unscaledTime-started;
            WingAngle=18+Mathf.Sin(elapsed*7)*24;
            left.localRotation=Quaternion.Euler(0,-WingAngle,-8);right.localRotation=Quaternion.Euler(0,WingAngle,8);
            if(Time.unscaledTime>=nextCough){
                nextCough=Time.unscaledTime+3.2f;CoughCount++;
                bool front=!Camera.main||transform.InverseTransformPoint(Camera.main.transform.position).z>=0;
                cough.transform.position=transform.TransformPoint(front?mouth:backMouth);cough.Play();cough.Emit(7);
                if(audioSource)audioSource.PlayOneShot(MagicMRAudioFactory.Cough,.32f);
            }
        }
        public void Stop(){active=false;WingAngle=0;if(wings)wings.SetActive(false);if(cough)cough.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);}
        void OnDisable(){Stop();}
        void OnDestroy(){Release(membrane);Release(material);}
        static void Release(Object value){if(!value)return;if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
    }
}
