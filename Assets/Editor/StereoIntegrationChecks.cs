using System;
using System.IO;
using UnityEngine;

public static class StereoIntegrationChecks
{
    static void CheckHandheldFollowing()
    {
        var grip = new Perception.LeftGripFollow();
        Vector3 target = new Vector3(0, 1, 0), palm = target + Vector3.left * .04f;
        for (int i=0;i<6;i++) grip.Observe(true,true,palm,.2f,target,i*.1f);
        if (grip.IsHolding) throw new Exception("Distant hand acquired target");
        grip.Observe(true,true,palm,.04f,target,1);
        grip.Observe(true,true,palm,.04f,target,1.1f);
        if (grip.IsHolding) throw new Exception("Grip acquired before confirmation");
        grip.Observe(true,true,palm,.04f,target,1.2f);
        if (!grip.IsHolding || Vector3.Distance(grip.Position,target)>.0001f)
            throw new Exception("Confirmed grip failed or snapped to palm");
        Vector3 motion = new Vector3(.04f,.06f,-.03f);
        grip.Observe(true,true,palm+motion,.3f,target,1.3f);
        if (Vector3.Distance(grip.Position,target+motion)>.0001f)
            throw new Exception("Held target failed 3D translation");
        grip.Observe(true,true,palm+Vector3.one*3,.04f,target,1.31f);
        if (Vector3.Distance(grip.Position,target+motion)>.0001f)
            throw new Exception("Tracking spike moved held target");
        grip.Observe(true,false,palm+motion,.04f,target,1.4f);
        grip.Observe(true,false,palm+motion+Vector3.right*.02f,.04f,target,1.6f);
        if (grip.IsHolding || Vector3.Distance(grip.Position,target+motion)>.0001f)
            throw new Exception("Open hand failed release or dragged target");
        grip.Reset();
        for (int i=0;i<3;i++) grip.Observe(true,true,palm,.04f,target,2+i*.1f);
        grip.Observe(false,true,palm,.04f,target,2.3f);
        if (!grip.IsHolding) throw new Exception("Brief tracking loss broke attachment");
        grip.Tick(2.6f);
        if (grip.IsHolding) throw new Exception("Tracking loss retained hand ownership");
        var follow = new Perception.TabletopVisualFollow();
        follow.Reset(target);
        if (!follow.Observe(target+Vector3.down*.03f,0) ||
            Vector3.Distance(follow.Position,target+Vector3.down*.03f)>.0001f)
            throw new Exception("Visual follow blocked downward movement");
        Debug.Log("HANDHELD_FOLLOW_CHECKS_PASS");
    }
    public static void Run()
    {
        TabletopGestureChecks.Run();
        PinchFollowChecks.Run();
        FollowOptimizationChecks.Run();
        VisualAverageChecks.Run();
        ObjectTrackingChecks.Run();
        CheckHandheldFollowing();
        var eligibilityObject = new GameObject("Summon eligibility regression");
        eligibilityObject.SetActive(false);
        try
        {
            var locator = eligibilityObject.AddComponent<Perception.StereoYoloLocator>();
            var fields = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var type = typeof(Perception.StereoYoloLocator);
            type.GetField("tracking",fields).SetValue(locator,true);
            if (locator.CanSummon) throw new Exception("Uncalibrated target allowed summon");
            var acquired = (Perception.TabletopTargetLock)type.GetField("tabletopLock",fields).GetValue(locator);
            for (int n=0;n<5;n++) acquired.Observe(Vector3.one,n*.2f);
            type.GetField("holdFollowing",fields).SetValue(locator,true);
            type.GetField("validAt",fields).SetValue(locator,-100f);
            locator.Invalidate("NO_LIGHTER");
            if (!locator.CanSummon) throw new Exception("Hand hold / expired vision deadlocked a calibrated summon");
            type.GetField("summoned",fields).SetValue(locator,true);
            if (locator.CanSummon || !locator.InteractionReady) throw new Exception("Already summoned lifecycle invalid");
            type.GetField("summoned",fields).SetValue(locator,false);
            type.GetField("tracking",fields).SetValue(locator,false);
            if (locator.CanSummon) throw new Exception("Tracking loss left summon enabled");
            type.GetField("tracking",fields).SetValue(locator,true);
            type.GetField("paused",fields).SetValue(locator,true);
            if (locator.CanSummon) throw new Exception("Paused target allowed summon");
            type.GetField("paused",fields).SetValue(locator,false);
            acquired.Reset();
            if (locator.CanSummon) throw new Exception("Reset retained summon permission");
        }
        finally { UnityEngine.Object.DestroyImmediate(eligibilityObject); }
        Debug.Log("TABLETOP_SUMMON_ELIGIBILITY_CHECKS_PASS");
        var follow = new Perception.TabletopVisualFollow();
        follow.Reset(Vector3.zero);
        follow.Observe(Vector3.right * .003f, 0);
        if (follow.Position != Vector3.zero) throw new Exception("Stationary jitter moved the anchor");
        if (!follow.Observe(Vector3.right * .02f, .1f) || follow.Position.x != .02f)
            throw new Exception("Small physical movement did not follow");
        if (follow.Observe(Vector3.right * .2f, .2f) || follow.Observe(Vector3.right * .2f, .3f) ||
            !follow.Observe(Vector3.right * .2f, .4f)) throw new Exception("Large move confirmation failed");
        follow.Reset(Vector3.zero);
        follow.Observe(Vector3.right, 1);
        follow.Observe(Vector3.right, 1.1f);
        follow.Reset(Vector3.zero); // Entering hand hold discards pending visual evidence.
        if (follow.Observe(Vector3.right, 1.2f) || follow.Position != Vector3.zero ||
            follow.Observe(new Vector3(float.NaN, 0, 0), 1.3f))
            throw new Exception("Hand hold or invalid point changed the position");
        if (follow.Observe(Vector3.right, 3)) throw new Exception("Stale confirmations accepted a jump");
        Debug.Log("TABLETOP_VISUAL_FOLLOW_CHECKS_PASS");
        Vector3 viewOrigin = new Vector3(-.1f, 1.2f, -.4f);
        Vector3 objectCenter = new Vector3(.2f, .8f, .3f);
        Vector3 expectedBottom = objectCenter - Vector3.up * .04f;
        if (!StereoFusionGeometry.TryTabletopPosition(objectCenter + new Vector3(.08f,.012f,0),
            expectedBottom + Vector3.up*.012f, expectedBottom, objectCenter.y, out var tablePosition) ||
            Mathf.Abs(tablePosition.y-objectCenter.y) > .0001f || Mathf.Abs(tablePosition.x-objectCenter.x-.08f) > .0001f)
            throw new Exception("Tabletop follow lifted the base or blocked horizontal movement");
        if (StereoFusionGeometry.TryTabletopPosition(objectCenter+Vector3.up*.08f, expectedBottom+Vector3.up*.08f,
            expectedBottom, objectCenter.y, out _)) throw new Exception("Raised/occluded bbox base was accepted");
        foreach (var viewerOffset in new[] {new Vector3(1,1,0),new Vector3(-1,2,-1),new Vector3(0,-1,1)})
        {
            var yaw = StereoFusionGeometry.UprightFacing(objectCenter,objectCenter+viewerOffset,Quaternion.identity);
            var horizontal = viewerOffset; horizontal.y=0;
            if (Vector3.Dot(yaw*Vector3.forward,horizontal.normalized)<.999f || Vector3.Dot(yaw*Vector3.up,Vector3.up)<.999f)
                throw new Exception("Lighter broad face does not face viewer upright");
        }
        Debug.Log("TABLETOP_BASE_AND_FACING_CHECKS_PASS");
        if (!StereoFusionGeometry.TryUprightExtent(objectCenter, viewOrigin,
            (objectCenter + Vector3.up * .04f - viewOrigin).normalized,
            (expectedBottom - viewOrigin).normalized, out var recoveredBottom, out float recoveredHeight) ||
            Vector3.Distance(expectedBottom, recoveredBottom) > .0001f || Mathf.Abs(recoveredHeight - .08f) > .0001f)
            throw new Exception("Pitched-view bbox base/height projection failed");
        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var cap = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            cap.transform.SetParent(body.transform, false);
            cap.transform.localPosition = Vector3.up * .7f;
            cap.transform.localScale = Vector3.one * .2f;
            body.transform.localScale = new Vector3(.025f, .08f, .012f);
            body.transform.position = objectCenter;
            var editor = body.AddComponent<MagicMR.RealityEditor>();
            // Force stale renderer bounds to reproduce registration while hidden before summon.
            body.GetComponent<Renderer>().forceRenderingOff = true;
            body.GetComponent<Renderer>().bounds = new Bounds(Vector3.up * 10, Vector3.one * 3);
            editor.RegisterVisualBounds(expectedBottom, .075f);
            body.GetComponent<Renderer>().ResetBounds();
            var bodyBounds = body.GetComponent<Renderer>().bounds;
            bodyBounds.Encapsulate(cap.GetComponent<Renderer>().bounds);
            if (Mathf.Abs(bodyBounds.min.y - expectedBottom.y) > .0001f || Mathf.Abs(bodyBounds.size.y - .075f) > .0001f)
                throw new Exception("Full virtual model did not register to bbox base and height");
            Vector3 registeredRoot = body.transform.position;
            var privateFields = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(MagicMR.RealityEditor).GetField("m_IsDeconstructed",privateFields).SetValue(editor,true);
            typeof(MagicMR.RealityEditor).GetField("m_FlowerSupportPoint",privateFields).SetValue(editor,expectedBottom);
            editor.SyncWorldAnchor(registeredRoot+Vector3.up*.05f);
            var pendingSupport = (Vector3)typeof(MagicMR.RealityEditor).GetField("m_FlowerSupportPoint",privateFields).GetValue(editor);
            if (Vector3.Distance(pendingSupport,expectedBottom+Vector3.up*.05f)>.0001f)
                throw new Exception("Pending flower spawn lost moving support");
            editor.SyncWorldAnchor(registeredRoot);
            typeof(MagicMR.RealityEditor).GetField("m_IsDeconstructed",privateFields).SetValue(editor,false);
            body.transform.position += Vector3.right;
            editor.ResetTarget();
            if (Vector3.Distance(body.transform.position, registeredRoot) > .0001f)
                throw new Exception("Reset lost the model registration offset");
            body.transform.rotation = StereoFusionGeometry.UprightFacing(objectCenter,objectCenter+Vector3.right,Quaternion.identity);
            editor.RegisterVisualBounds(expectedBottom, .075f);
            bodyBounds = body.GetComponent<Renderer>().bounds;
            bodyBounds.Encapsulate(cap.GetComponent<Renderer>().bounds);
            if (Mathf.Abs(bodyBounds.min.y-expectedBottom.y)>.0001f || Mathf.Abs(bodyBounds.size.y-.075f)>.0001f)
                throw new Exception("Facing viewer changed registered base or height");
            var anchor = new GameObject("Follow test anchor");
            try
            {
                body.transform.SetParent(anchor.transform, true);
                Vector3 scale = body.transform.localScale;
                Vector3 baseLocal = body.transform.localPosition;
                body.transform.localPosition += Vector3.right * .2f; // Rule animation offset.
                Vector3 animatedLocal = body.transform.localPosition;
                anchor.transform.position += new Vector3(.04f, 0, .02f);
                editor.SyncWorldAnchor(anchor.transform.TransformPoint(baseLocal));
                if (body.transform.localPosition != animatedLocal || body.transform.localScale != scale)
                    throw new Exception("Following overwrote child animation or registered size");
                editor.ResetTarget();
                if (Vector3.Distance(body.transform.position, anchor.transform.TransformPoint(baseLocal)) > .0001f)
                    throw new Exception("Reset returned to an old world anchor after following");
            }
            finally { body.transform.SetParent(null, true); UnityEngine.Object.DestroyImmediate(anchor); }
            Debug.Log("LIGHTER_BBOX_REGISTRATION_CHECK_PASS");
        }
        finally { UnityEngine.Object.DestroyImmediate(body); }
        var flowerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MagicMR/Prefabs/Flower.prefab");
        var flower = UnityEngine.Object.Instantiate(flowerPrefab);
        try
        {
            Vector3 support = new Vector3(.3f, .7f, -.2f);
            flower.transform.localScale = Vector3.one * .8f;
            flower.transform.rotation = Quaternion.Euler(0, 37, 0);
            MagicMR.RealityEditor.FitFlowerHeight(flower.transform, .11f);
            Vector3 lighterSize=new Vector3(.025f,.08f,.012f);
            MagicMR.RealityEditor.CoverLighterWithStem(flower.transform,lighterSize);
            MagicMR.RealityEditor.AlignFlowerBase(flower.transform, support);
            for (int rotation = 0; rotation < 16; rotation++)
            {
                var stemBottom = flower.transform.Find("Base").position;
                if (Vector3.Distance(stemBottom, support) > .0001f)
                    throw new Exception("Flower stem does not remain on the support point");
                var meshBounds = new Bounds(); bool firstRenderer = true;
                foreach (var renderer in flower.GetComponentsInChildren<MeshRenderer>())
                { if(firstRenderer) { meshBounds=renderer.bounds; firstRenderer=false; } else meshBounds.Encapsulate(renderer.bounds); }
                if (meshBounds.size.y > .20f || meshBounds.size.y < lighterSize.y)
                    throw new Exception("Covering flower height outside limits");
                var stem=flower.transform.Find("Stem");
                var stemMesh = stem.GetComponent<MeshFilter>().sharedMesh;
                float radius=stemMesh.bounds.extents.x*Mathf.Cos(Mathf.PI/32);
                for(int corner=0;corner<8;corner++)
                {
                    var point=support+new Vector3((corner&1)==0?-lighterSize.x*.5f:lighterSize.x*.5f,
                        (corner&2)==0?0:lighterSize.y,(corner&4)==0?-lighterSize.z*.5f:lighterSize.z*.5f);
                    var local=stem.InverseTransformPoint(point);
                    if(new Vector2(local.x,local.z).magnitude>radius||local.y<-.0001f||local.y>stemMesh.bounds.max.y)
                        throw new Exception("Rotating flower stem exposes part of the lighter");
                }
                var petal=flower.transform.Find("Blossom/LayeredPetals");
                var petalBounds=new Bounds(petal.position,Vector3.zero);
                foreach(var vertex in petal.GetComponent<MeshFilter>().sharedMesh.vertices)
                    petalBounds.Encapsulate(petal.TransformPoint(vertex));
                float diameter=stemMesh.bounds.size.x*Mathf.Abs(stem.lossyScale.x);
                float ratio=Mathf.Max(petalBounds.size.x,petalBounds.size.z)/diameter;
                if(ratio<3.8f||ratio>5.3f)throw new Exception($"Flower head / covering stem ratio invalid: {ratio:F2}");
                flower.transform.RotateAround(support, Vector3.up, 22.5f);
            }
            Debug.Log("FLOWER_BASE_ALIGNMENT_CHECK_PASS");
        }
        finally { UnityEngine.Object.DestroyImmediate(flower); }
        var tabletop = new Perception.TabletopTargetLock();
        for (int i = 0; i < 4; i++)
            if (tabletop.Observe(Vector3.one, i * .2f)) throw new Exception("Tabletop locked too early");
        if (!tabletop.Observe(Vector3.one, .8f)) throw new Exception("Stable tabletop failed to lock");
        if (!tabletop.Observe(Vector3.right * 10, 10) || tabletop.Position != Vector3.one)
            throw new Exception("Locked target moved when vision changed");
        tabletop.Reset();
        if (tabletop.IsLocked || tabletop.Observe(Vector3.zero, 11)) throw new Exception("Reset retained tabletop lock");
        for (int i = 0; i < 10; i++) tabletop.Observe(Vector3.right * i, 12 + i * .2f);
        if (tabletop.IsLocked) throw new Exception("Moving target must not lock");
        tabletop.Reset();
        for (int i = 0; i < 10; i++) tabletop.Observe(Vector3.zero, 20 + i);
        if (tabletop.IsLocked) throw new Exception("Sparse stale samples must not lock");
        Debug.Log("TABLETOP_LOCK_CHECKS_PASS");
        if (!Perception.StereoYoloLocator.IsMatchingReply(17,17,640,480,.1f,1.5f) ||
            Perception.StereoYoloLocator.IsMatchingReply(16,17,640,480,.1f,1.5f) ||
            Perception.StereoYoloLocator.IsMatchingReply(17,17,0,0,.1f,1.5f) ||
            Perception.StereoYoloLocator.IsMatchingReply(17,17,640,480,1.6f,1.5f) ||
            Perception.StereoYoloLocator.IsMatchingReply(17,17,640,480,-.1f,1.5f))
            throw new Exception("Stereo response age/identity/dimensions gate failed");
        var guard = new Perception.StereoPositionGuard();
        if (!guard.Accept(Vector3.zero,0) || guard.Accept(Vector3.right, .1f) ||
            !guard.Accept(Vector3.zero,.2f) || guard.Accept(Vector3.right,.3f) ||
            guard.Accept(Vector3.right,.4f) || !guard.Accept(Vector3.right,.5f) ||
            guard.Accept(new Vector3(float.NaN,0,0),.6f))
            throw new Exception("A depth outlier must be rejected; a persistent new position must be accepted");
        var random = new System.Random(42);
        var left = new byte[640*480]; var right = new byte[left.Length];
        random.NextBytes(left);
        for (int y=0; y<480; y++) for (int x=0; x<616; x++) right[y*640+x]=left[y*640+x+24];
        var result = StereoPatchMatcher.EstimateDepth(left,right,640,480,320,240,407,.06392f,3,7);
        if (!result.valid || Math.Abs(result.disparityPixels-24)>.5f) throw new Exception("ROI stereo shift failed");
        Array.Clear(left,0,left.Length); Array.Clear(right,0,right.Length);
        if (StereoPatchMatcher.EstimateDepth(left,right,640,480,320,240,407,.06392f,3,7).valid)
            throw new Exception("Flat target must be rejected");
        Vector3 head = StereoFusionGeometry.OpticalToHead(new Vector3(0,0,1),new Pose(Vector3.zero,Quaternion.Euler(180,0,0)));
        if (Vector3.Distance(head,Vector3.forward)>.001f) throw new Exception("Optical reflection failed");
        var tex = new Texture2D(32,32,TextureFormat.RGB24,false);
        for (int y=0;y<32;y++) for (int x=0;x<32;x++) tex.SetPixel(x,y,y<16?Color.red:Color.blue);
        tex.Apply(); Directory.CreateDirectory(".codex-tmp");
        File.WriteAllBytes(".codex-tmp/stereo-jpeg-orientation.jpg",tex.EncodeToJPG(100));
        UnityEngine.Object.DestroyImmediate(tex);
        Debug.Log("STEREO_INTEGRATION_CHECKS_PASS");
    }
}
