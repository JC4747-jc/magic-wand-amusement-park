using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace MagicMR
{
    public sealed partial class ParkActor
    {
        Transform narrativeRoot, narrativeSheet;
        TextMesh narrativeTitle, narrativeText, narrativeCredit;
        string[] narrativePages = Array.Empty<string>();
        float narrativeStarted = -1, narrativeScale;
        int narrativePage = -1;
        public bool StatueNarrativeVisible => narrativeStarted >= 0 && narrativeRoot && narrativeRoot.gameObject.activeInHierarchy;
        public int StatueNarrativePage => narrativePage;
        public int StatueNarrativePageCount => narrativePages.Length;

        void BuildStatueNarrative()
        {
            // Separate from the animated skin; fitting and the tracking anchor remain untouched.
            narrativeRoot = new GameObject("Statue C - anchored narrative scroll").transform;
            narrativeRoot.SetParent(representationFit, false);
            float height = Mathf.Max(.05f, neutralBounds.size.y);
            narrativeScale = height * .8f;
            narrativeRoot.localPosition = new Vector3(neutralBounds.max.x + height * .48f,
                neutralBounds.center.y, neutralBounds.center.z);
            narrativeSheet = new GameObject("Unfolding parchment").transform;
            narrativeSheet.SetParent(narrativeRoot, false);
            Part(narrativeSheet, "Parchment", PrimitiveType.Cube, Vector3.zero,
                new Vector3(1.05f,.72f,.008f), new Color(.77f,.69f,.49f), false);
            for(int side=-1;side<=1;side+=2)
            {
                Part(narrativeSheet,"Scroll roller",PrimitiveType.Cylinder,
                    new Vector3(side*.53f,0,-.006f),new Vector3(.025f,.38f,.025f),new Color(.22f,.13f,.065f),false);
            }
            narrativeTitle = NarrativeLabel("Chapter", .25f, .044f);
            narrativeText = NarrativeLabel("Narrative", .035f, .055f);
            narrativeCredit = NarrativeLabel("Source", -.25f, .031f);
            narrativeRoot.gameObject.SetActive(false);
        }

        TextMesh NarrativeLabel(string name, float y, float size)
        {
            var text = new GameObject(name).AddComponent<TextMesh>();
            text.transform.SetParent(narrativeSheet,false);
            text.transform.localPosition=new Vector3(0,y,-.014f);
            text.fontSize=48;text.characterSize=size;text.anchor=TextAnchor.MiddleCenter;
            text.alignment=TextAlignment.Center;text.color=new Color(.14f,.10f,.055f);
            return text;
        }

        static void FitNarrativeLabel(TextMesh text, float y, float height)
        {
            // Fit the generated glyph geometry, including font-specific bearings and line spacing.
            text.transform.localScale=Vector3.one;
            var bounds=text.GetComponent<Renderer>().localBounds;
            float scale=Mathf.Min(1f,Mathf.Min(.9f/Mathf.Max(.0001f,bounds.size.x),
                height/Mathf.Max(.0001f,bounds.size.y)));
            text.transform.localScale=Vector3.one*scale;
            text.transform.localPosition=new Vector3(-bounds.center.x*scale,y-bounds.center.y*scale,-.014f);
        }

        static string NarrativeWrap(string value,int width)
        {
            var result=new StringBuilder();int column=0;
            foreach(var word in value.Split((char[])null,StringSplitOptions.RemoveEmptyEntries))
            {
                if(column>0){if(column+1+word.Length>width){result.Append('\n');column=0;}else{result.Append(' ');column++;}}
                // Bound even a single unbroken token; no unbounded mesh/text expansion.
                foreach(char c in word){if(column>=width){result.Append('\n');column=0;}result.Append(c);column++;}
            }
            return result.ToString();
        }

        void StartStatueNarrative()
        {
            StopStatueNarrative();
            if(!narrativeRoot)return;
            string content=string.IsNullOrWhiteSpace(story)?"This statue's story has not been supplied.":story;
            content=content.Substring(0,Mathf.Min(1800,content.Length));
            var pages=new List<string>();
            foreach(var paragraph in content.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries))
                for(int offset=0;offset<paragraph.Length && pages.Count<8;offset+=180)
                    pages.Add(NarrativeWrap(paragraph.Substring(offset,Mathf.Min(180,paragraph.Length-offset)),36));
            narrativePages=pages.ToArray();
            string source=string.IsNullOrWhiteSpace(storySource)?"Source unspecified; history unverified":storySource;
            narrativeCredit.text=NarrativeWrap(source.Substring(0,Mathf.Min(140,source.Length)),46);
            FitNarrativeLabel(narrativeCredit,-.25f,.10f);
            narrativePage=-1;narrativeStarted=Time.unscaledTime;
            narrativeRoot.gameObject.SetActive(true);
            // Repeated C restarts one owned source, never stacks narration clips.
            if(storyAudio && storyAudio.length<=60){audioSource.Stop();audioSource.PlayOneShot(storyAudio);}
            StepStatueNarrative();
        }

        void StopStatueNarrative()
        {
            if(narrativeStarted>=0 && audioSource)audioSource.Stop();
            narrativeStarted=-1;narrativePage=-1;
            if(narrativeRoot)narrativeRoot.gameObject.SetActive(false);
        }

        void StepStatueNarrative()
        {
            if(!StatueNarrativeVisible)return;
            float age=Time.unscaledTime-narrativeStarted;
            int page=Mathf.Min(narrativePages.Length-1,Mathf.Max(0,Mathf.FloorToInt((age-.6f)/4)));
            if(page!=narrativePage && page>=0)
            {
                narrativePage=page;
                narrativeTitle.text="STATUE STORY  "+(page+1)+" / "+narrativePages.Length;
                narrativeText.text=narrativePages[page];
                FitNarrativeLabel(narrativeTitle,.25f,.10f);
                FitNarrativeLabel(narrativeText,.035f,.30f);
            }
            narrativeRoot.localScale=Vector3.one*narrativeScale;
            narrativeSheet.localScale=new Vector3(Mathf.Lerp(.04f,1,Mathf.SmoothStep(0,1,age/.6f)),1,1);
            bool readable=age>=.5f;
            narrativeTitle.gameObject.SetActive(readable);narrativeText.gameObject.SetActive(readable);narrativeCredit.gameObject.SetActive(readable);
        }

        void LateUpdate()
        {
            if(!StatueNarrativeVisible)return;
            // The actor animates its ancestors in Update; face the viewer after those transforms settle.
            var viewer=Camera.main;
            if(!viewer)return;
            var center=representationFit.TransformPoint(neutralBounds.center);
            var projected=viewer.WorldToViewportPoint(center);
            if(projected.z<=viewer.nearClipPlane)return;
            float rightEdge=float.NegativeInfinity;
            for(int corner=0;corner<8;corner++)
            {
                var local=neutralBounds.center+Vector3.Scale(neutralBounds.extents,
                    new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1));
                var point=viewer.WorldToViewportPoint(representationFit.TransformPoint(local));
                if(point.z<=viewer.nearClipPlane)return;
                rightEdge=Mathf.Max(rightEdge,point.x);
            }
            float unitsToViewport=viewer.WorldToViewportPoint(center+viewer.transform.right).x-projected.x;
            if(!float.IsFinite(unitsToViewport)||unitsToViewport<=.000001f)return;
            float gap=representationFit.TransformVector(Vector3.up*Mathf.Max(.05f,neutralBounds.size.y)*.08f).magnitude;
            float halfWidth=narrativeRoot.TransformVector(Vector3.right*.55f).magnitude;
            projected.x=rightEdge+(halfWidth+gap)*unitsToViewport;
            narrativeRoot.position=viewer.ViewportToWorldPoint(projected);
            // Account for the billboard's perspective depth as well as the model's
            // sideways extent. A local-X offset alone put the wide bronze body behind it.
            for(int iteration=0;iteration<3;iteration++)
            {
                var forward=narrativeRoot.position-viewer.transform.position;
                if(forward.sqrMagnitude>.0001f)narrativeRoot.rotation=Quaternion.LookRotation(forward,viewer.transform.up);
                float leftEdge=float.PositiveInfinity;
                for(int corner=0;corner<8;corner++)
                {
                    var local=new Vector3((corner&1)==0?-.55f:.55f,(corner&2)==0?-.40f:.40f,(corner&4)==0?-.025f:.025f);
                    leftEdge=Mathf.Min(leftEdge,viewer.WorldToViewportPoint(narrativeRoot.TransformPoint(local)).x);
                }
                float correction=rightEdge+gap*unitsToViewport-leftEdge;
                if(correction<=.00001f)break;
                narrativeRoot.position+=viewer.transform.right*(correction/unitsToViewport);
            }
            var facing=narrativeRoot.position-viewer.transform.position;
            if(facing.sqrMagnitude>.0001f)narrativeRoot.rotation=Quaternion.LookRotation(facing,viewer.transform.up);
        }
    }
}
