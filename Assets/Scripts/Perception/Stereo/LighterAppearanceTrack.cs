using UnityEngine;
namespace Perception
{
    // Fixed first-acquisition appearance, never trained on subsequent hand/occluded frames.
    public sealed class LighterAppearanceTrack
    {
        const int W=10,H=30,N=W*H;
        readonly float[] reference=new float[N];
        readonly float[] scores=new float[3];
        static readonly float[] scales={.85f,1f,1.15f};
        public bool Ready { get; private set; }
        public float LastScore { get; private set; }
        public void Reset(){Ready=false;LastScore=0;}
        static float Pixel(byte[] gray,Rect box,int x,int y)
        {
            int u=Mathf.RoundToInt(box.xMin+box.width*(.12f+.76f*(x+.5f)/W));
            int v=Mathf.RoundToInt(box.yMin+box.height*(y+.5f)/H);
            return gray[Mathf.Clamp(v,0,479)*640+Mathf.Clamp(u,0,639)];
        }
        public void Capture(byte[] gray,Rect box)
        {
            Reset();if(gray==null||gray.Length!=640*480||box.width<12||box.height<24)return;
            float sum=0,sq=0;
            for(int y=0;y<H;y++)for(int x=0;x<W;x++){float v=Pixel(gray,box,x,y);reference[y*W+x]=v;sum+=v;sq+=v*v;}
            Ready=sq/N-(sum/N)*(sum/N)>36;
        }
        public float Score(byte[] gray,Rect box)
        {
            if(!Ready||gray==null||gray.Length!=640*480||box.xMin<1||box.yMin<1||box.xMax>638||box.yMax>478)return -1;
            for(int band=0;band<3;band++)
            {
                float a=0,b=0,aa=0,bb=0,ab=0;
                for(int y=band*10;y<(band+1)*10;y++)for(int x=0;x<W;x++)
                {float r=reference[y*W+x],v=Pixel(gray,box,x,y);a+=r;b+=v;aa+=r*r;bb+=v*v;ab+=r*v;}
                float va=aa-a*a/100,vb=bb-b*b/100;
                scores[band]=va>400&&vb>400?(ab-a*b/100)/Mathf.Sqrt(va*vb):-1;
            }
            // Two distinct textured bands must agree. One occluded band may be discarded.
            float low=Mathf.Min(scores[0],Mathf.Min(scores[1],scores[2]));
            return (scores[0]+scores[1]+scores[2]-low)*.5f;
        }
        public bool Find(byte[] gray,Rect predicted,out Rect found)
        {
            found=predicted;LastScore=-1;
            if(!Ready||predicted.width<12||predicted.height<24)return false;
            float best=-1;
            foreach(float scale in scales)
            for(int dy=-40;dy<=40;dy+=5)for(int dx=-40;dx<=40;dx+=5)
            {
                Vector2 size=predicted.size*scale;
                Rect box=new Rect(predicted.center+new Vector2(dx,dy)-size*.5f,size);
                float score=Score(gray,box);
                if(score>best){best=score;found=box;}
            }
            Rect coarse=found;
            for(int dy=-4;dy<=4;dy++)for(int dx=-4;dx<=4;dx++)
            {Rect box=new Rect(coarse.position+new Vector2(dx,dy),coarse.size);float score=Score(gray,box);if(score>best){best=score;found=box;}}
            LastScore=best;return best>=.72f;
        }
    }
}
