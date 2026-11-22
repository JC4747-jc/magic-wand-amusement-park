using UnityEngine;
#if XR_HANDS_1_1_OR_NEWER
using UnityEngine.XR.Hands;
#endif

namespace MagicMR
{
    // Both the lighter and Park consume this geometry, then TabletopGestureRules.
    // Joint extension is invariant to hand size; world conversion happens once.
    public struct TabletopHandSample
    {
        public TabletopGestureRules.Sample gesture;
        public Vector3 thumb, indexBase;
        public Vector4 extension;
        public Vector3 ClosestTo(Vector3 target) {
            var p=gesture.palm;
            if((gesture.index-target).sqrMagnitude<(p-target).sqrMagnitude)p=gesture.index;
            var pinch=(gesture.index+thumb)*.5f;
            if((pinch-target).sqrMagnitude<(p-target).sqrMagnitude)p=pinch;
            return p;
        }
        public bool Valid=>Finite(gesture.palm)&&Finite(gesture.index)&&Finite(thumb)&&Finite(indexBase)&&Finite(gesture.swipeAxis)&&
            float.IsFinite(gesture.time)&&float.IsFinite(gesture.pinch)&&gesture.pinch>=0&&
            ValidExtension(extension.x)&&ValidExtension(extension.y)&&ValidExtension(extension.z)&&ValidExtension(extension.w);
        static bool ValidExtension(float value)=>float.IsFinite(value)&&value>=0&&value<=1.001f;
        public static bool Finite(Vector3 p)=>float.IsFinite(p.x)&&float.IsFinite(p.y)&&float.IsFinite(p.z);
        public static TabletopHandSample Create(float time,Vector3 palm,Vector3 index,Vector3 thumb,Vector3 indexBase,Vector4 extension,Vector3 swipeAxis,float pinchDistance) {
            int extended=(extension.x>.82f?1:0)+(extension.y>.82f?1:0)+(extension.z>.82f?1:0)+(extension.w>.82f?1:0);
            int curled=(extension.y<.68f?1:0)+(extension.z<.68f?1:0)+(extension.w<.68f?1:0);
            return new TabletopHandSample {thumb=thumb,indexBase=indexBase,extension=extension,
                gesture=new TabletopGestureRules.Sample {time=time,pinch=pinchDistance,palm=palm,index=index,swipeAxis=swipeAxis,
                    fist=extension.x<.68f&&curled==3,open=extended>=3,pointing=extension.x>.82f&&curled>=2}};
        }
#if XR_HANDS_1_1_OR_NEWER
        public static bool TryRead(XRHand hand,Transform trackingRoot,Vector3 swipeAxis,float time,out TabletopHandSample sample) {
            sample=default;
            if(!hand.isTracked||!Read(hand,XRHandJointID.Palm,out var palm)||
                !Read(hand,XRHandJointID.IndexTip,out var index)||!Read(hand,XRHandJointID.ThumbTip,out var thumb)||
                !Read(hand,XRHandJointID.IndexProximal,out var indexBase)||
                !Extension(hand,XRHandJointID.IndexProximal,XRHandJointID.IndexIntermediate,XRHandJointID.IndexDistal,XRHandJointID.IndexTip,out var i)||
                !Extension(hand,XRHandJointID.MiddleProximal,XRHandJointID.MiddleIntermediate,XRHandJointID.MiddleDistal,XRHandJointID.MiddleTip,out var m)||
                !Extension(hand,XRHandJointID.RingProximal,XRHandJointID.RingIntermediate,XRHandJointID.RingDistal,XRHandJointID.RingTip,out var r)||
                !Extension(hand,XRHandJointID.LittleProximal,XRHandJointID.LittleIntermediate,XRHandJointID.LittleDistal,XRHandJointID.LittleTip,out var l))return false;
            Vector3 World(Vector3 p)=>trackingRoot?trackingRoot.TransformPoint(p):p;
            sample=Create(time,World(palm),World(index),World(thumb),World(indexBase),new Vector4(i,m,r,l),swipeAxis,Vector3.Distance(index,thumb));
            return sample.Valid;
        }
        static bool Read(XRHand hand,XRHandJointID id,out Vector3 p) {
            bool ok=hand.GetJoint(id).TryGetPose(out var pose);p=pose.position;return ok&&Finite(p);
        }
        static bool Extension(XRHand hand,XRHandJointID a,XRHandJointID b,XRHandJointID c,XRHandJointID d,out float value) {
            value=0;
            if(!Read(hand,a,out var p0)||!Read(hand,b,out var p1)||!Read(hand,c,out var p2)||!Read(hand,d,out var p3))return false;
            float length=Vector3.Distance(p0,p1)+Vector3.Distance(p1,p2)+Vector3.Distance(p2,p3);
            if(length<.01f)return false;
            value=Vector3.Distance(p0,p3)/length;return ValidExtension(value);
        }
#endif
    }
}
