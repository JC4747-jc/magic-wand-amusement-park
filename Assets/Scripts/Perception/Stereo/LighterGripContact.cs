using UnityEngine;

namespace Perception
{
    // Contact estimate around the registered upright object, not a wide proximity gesture.
    public sealed class LighterGripContact
    {
        float began=-1,last=-100,confirmedAt=-100;
        int samples;
        public bool Touching { get; private set; }
        public bool Recent(float now)=>now-confirmedAt>=0&&now-confirmedAt<.20f;
        public void Reset(){began=-1;last=confirmedAt=-100;samples=0;Touching=false;}
        public void Observe(bool valid,Vector3 thumb,Vector3 index,Vector3 center,float width,float height,float now)
        {
            Touching=valid&&Fits(thumb,index,center,width,height);
            if(!Touching){began=-1;samples=0;return;}
            if(began<0||now-last>.10f){began=now;samples=0;}
            last=now;samples++;
            if(samples>=3&&now-began>=.10f)confirmedAt=now;
        }
        public static bool Fits(Vector3 thumb,Vector3 index,Vector3 center,float width,float height)
        {
            if(!float.IsFinite(thumb.sqrMagnitude+index.sqrMagnitude+center.sqrMagnitude)||width<=0||height<=0)return false;
            float separation=Vector3.Distance(thumb,index);
            if(separation<.014f||separation>Mathf.Clamp(width+.028f,.035f,.07f))return false;
            if(Mathf.Abs(thumb.y-center.y)>height*.5f+.008f||Mathf.Abs(index.y-center.y)>height*.5f+.008f)return false;
            Vector2 a=new Vector2(thumb.x-center.x,thumb.z-center.z),b=new Vector2(index.x-center.x,index.z-center.z);
            float radius=Mathf.Clamp(width*.55f,.009f,.025f);
            if(a.magnitude<radius*.25f||b.magnitude<radius*.25f||a.magnitude>radius+.020f||b.magnitude>radius+.020f)return false;
            if(Vector2.Dot(a.normalized,b.normalized)>-.15f)return false;
            Vector2 line=b-a;float t=-Vector2.Dot(a,line)/Mathf.Max(.000001f,line.sqrMagnitude);
            return t>.15f&&t<.85f&&(a+line*t).magnitude<Mathf.Max(.010f,radius*.6f);
        }
    }
}
