using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Hands.Gestures;

namespace MagicMR
{
    // One subsystem selection per frame, shared by the scene and its UI.
    public static class ParkHandTracking
    {
        static readonly List<XRHandSubsystem> systems = new List<XRHandSubsystem>();
        static XRHandSubsystem current;
        static int sampledFrame = -1;
        static XRHandShape openShape,fistShape;
        static bool shapesLoaded;
        static readonly XRHandJointsUpdatedEventArgs shapeArgs=new XRHandJointsUpdatedEventArgs();
        public static bool SampleShapeAssetsAvailable {
            get {
                if(!shapesLoaded) {
                    shapesLoaded=true;
                    openShape=Resources.Load<XRHandShape>("MagicMR/HandShapes/Open Hand Shape");
                    fistShape=Resources.Load<XRHandShape>("MagicMR/HandShapes/Fist Hand Shape");
                }
                return openShape && fistShape;
            }
        }
        public static bool MatchesSampleShape(XRHand hand,bool open) {
            if(!hand.isTracked || !SampleShapeAssetsAvailable)return false;
            shapeArgs.hand=hand;
            return (open?openShape:fistShape).CheckConditions(shapeArgs);
        }
        public static XRHandSubsystem Current
        {
            get
            {
                if (sampledFrame == Time.frameCount) return current;
                sampledFrame = Time.frameCount;
                if (current != null && current.running) return current;
                current = null;
                SubsystemManager.GetSubsystems(systems);
                foreach (var system in systems)
                    if (system.running) { current = system; break; }
                return current;
            }
        }
        public static string Status
        {
            get
            {
                var system = Current;
                if (system == null) return "Hands: tracking unavailable";
                return "Hands L: " + (system.leftHand.isTracked ? "tracked" : "lost") +
                    " | R: " + (system.rightHand.isTracked ? "tracked" : "lost");
            }
        }
        // Legacy lighter bootstrap explicitly starts tracking; ordinary UI readers do not.
        public static XRHandSubsystem EnsureRunning()
        {
            var system=Current;
            if(system!=null)return system;
            SubsystemManager.GetSubsystems(systems);
            foreach(var candidate in systems) {
                if(!candidate.running)candidate.Start();
                if(candidate.running) {current=candidate;sampledFrame=Time.frameCount;return current;}
            }
            return null;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { current = null; sampledFrame = -1; systems.Clear();shapesLoaded=false;openShape=fistShape=null;shapeArgs.hand=default; }
    }
}
