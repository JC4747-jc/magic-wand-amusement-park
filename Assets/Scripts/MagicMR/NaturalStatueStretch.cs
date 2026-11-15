using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace MagicMR
{
    // The actor owns timing and cancellation. This graph animates only the FBX skeleton.
    public sealed class NaturalStatueStretch : MonoBehaviour
    {
        [SerializeField] Animator target;
        [SerializeField] AnimationClip clip;
        PlayableGraph graph;
        AnimationClipPlayable playable;
        public float Duration=>clip?clip.length:0;
        public float Progress {get;private set;}
        public bool GraphValid=>graph.IsValid();
        public void Configure(Animator animator,AnimationClip animation){target=animator;clip=animation;}
        public void Initialize()
        {
            if(graph.IsValid())return;
            if(!target||!clip||clip.length<1)throw new InvalidOperationException("Standing statue animation references missing");
            target.enabled=true;target.applyRootMotion=false;target.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            graph=PlayableGraph.Create("Standing statue stretch");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            playable=AnimationClipPlayable.Create(graph,clip);playable.SetApplyFootIK(false);playable.SetApplyPlayableIK(false);
            AnimationPlayableOutput.Create(graph,"Statue skeleton",target).SetSourcePlayable(playable);
            graph.Play();Sample(0);
        }
        public void Sample(float progress)
        {
            if(!graph.IsValid())Initialize();
            Progress=float.IsFinite(progress)?Mathf.Clamp01(progress):0;
            playable.SetTime(Progress*clip.length);graph.Evaluate(0);
        }
        public void ResetPose(){if(graph.IsValid())Sample(0);}
        void OnDisable(){ResetPose();Release();}
        void OnDestroy(){Release();}
        void Release(){if(graph.IsValid())graph.Destroy();Progress=0;}
    }
}
