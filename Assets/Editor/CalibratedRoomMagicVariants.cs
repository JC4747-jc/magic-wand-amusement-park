using System;
using System.Linq;
using MagicMR;
using Perception;
using UnityEngine;

public static partial class CalibratedRoomMagicChainChecks
{
    enum RoomVariant { None, Portal, Snow }
    static RoomVariant roomVariant;
    static RoomPortalPuzzle portal;
    static RoomSnowMagic snow;
    static ParkSharedInterface roomUi;
    static Vector3 fitPosition, fitScale;
    static Quaternion targetOrientation;
    static Vector3[] frozenPositions;
    static int fallingCells, gatheredCells;
    static bool testingResume;

    static Vector3 VariantGestureOffset(EditDimension spell)
    {
        if (roomVariant==RoomVariant.Portal)
            return (spell==EditDimension.Appearance ? portal.Seed.position : target.InteractionCenter)-anchor;
        return Vector3.up*.05f;
    }

    static void CheckRoomFit()
    {
        var fit=target.visual.parent;
        Require(controller.selected==target && !target.virtualTarget && target.tracked &&
            target.orientationConfirmed && target.sizeConfirmed && target.transform.rotation==targetOrientation &&
            fit.localPosition==fitPosition && fit.localScale==fitScale, "Menu or effect changed selection, orientation or fit.");
    }

    static int CountOccupiedSnowCells()
    {
        // Divide the local snowfall volume into thirds in all three dimensions.
        // A single stray flake does not count as coverage of a cell.
        var cellCounts = new int[27];
        foreach (var point in snow.Positions)
        {
            int x = Mathf.Clamp(Mathf.FloorToInt((point.x + .30f) / .20f), 0, 2);
            int y = Mathf.Clamp(Mathf.FloorToInt((point.y - .04f) / .19f), 0, 2);
            int z = Mathf.Clamp(Mathf.FloorToInt((point.z + .25f) * 6f), 0, 2);
            cellCounts[x + 3 * y + 9 * z]++;
        }
        return cellCounts.Count(count => count >= 2);
    }

    static void CheckRoomVariants(float now)
    {
        if (roomVariant==RoomVariant.None)
        {
            roomUi=UnityEngine.Object.FindFirstObjectByType<ParkSharedInterface>();
            Require(roomUi, "The saved shared menu did not load.");
            fitPosition=target.visual.parent.localPosition;
            fitScale=target.visual.parent.localScale;
            targetOrientation=target.transform.rotation;
            roomUi.Command("Portal room");
            portal=target.RoomPortals;
            Require(target.RoomPortalMode && portal && portal.Enabled && !target.RoomSnowMode, "Portal menu command was rejected.");
            Require(portal.OwnedMaterialCount==8 && Vector3.Distance(portal.Seed.position,anchor)<.2f,
                "The fitted portal seed is outside the shared reach or material budget.");
            var ceiling=target.environmentAnchor;
            var tablePoint=new Vector3(.34f,-.085f,-.03f);
            roomUi.Command("Plane");
            Require(controller.PlaneConfirmationPending,"The shared Plane command did not enter confirmation.");
            controller.ProcessHandSample(Hand(Vector4.one*.95f,.08f,tablePoint),true);
            Require(controller.ProcessHandSample(Hand(Vector4.one*.95f,.02f,tablePoint),true)==EditDimension.None &&
                    portal.UsesConfirmedSurface && !controller.PlaneConfirmationPending &&
                    Vector3.Distance(target.InteractionCenter,anchor+tablePoint+Vector3.up*.085f)<1e-5f &&
                    target.environmentAnchor==ceiling && target.confirmedPlane,
                    "Synthetic fingertip placement did not move the doors or isolate the default room plane.");
            roomVariant=RoomVariant.Portal;
            CheckRoomFit();
            StartSpell(now);
            return;
        }
        CheckRoomFit();
        if (roomVariant==RoomVariant.Portal)
        {
            if (spellIndex==1) Require(portal.Open, "B did not open the paired portals.");
            if (spellIndex==2) Require(portal.Held, "A did not grab the seed.");
            if (spellIndex>=3) Require(portal.Powered && !portal.Held && !portal.IsTravelling,
                "C did not send the seed to the receiver, or D lost the powered state.");
            if (spellIndex<Spells.Length) { StartSpell(now);return; }
            var portalBounds = new Bounds(portal.Entrance.position, Vector3.one*.16f);
            portalBounds.Encapsulate(new Bounds(portal.Exit.position, Vector3.one*.16f));
            portalBounds.Encapsulate(portal.Socket.position);
            CaptureVariantPerspective("portal-perspective.png", portal.Entrance, portalBounds);
            transitions.Add("Portal menu: confirmed a manual tabletop using a synthetic pinch. B linked the placed pair, A grabbed the seed, C powered the receiver and D kept it powered on the tracked instance.");
            roomUi.Command("Snow room");
            Require(!portal.UsesConfirmedSurface && target.confirmedPlane,
                "Switching content leaked the portal placement or reset the default room plane.");
            snow=target.RoomSnow;
            Require(target.RoomSnowMode && snow && snow.Active && !target.RoomPortalMode &&
                !portal.Enabled && !portal.Powered && !portal.Open && !portal.Held, "Switching to snow retained portal state.");
            roomVariant=RoomVariant.Snow;
            spellIndex=0;
            StartSpell(now);
            return;
        }
        if (spellIndex>=1) Require(snow.Snowing && snow.LiveFlakes==RoomSnowMagic.FlakeBudget && snow.OwnedMaterialCount==1,
            "A did not start bounded snowfall.");
        if (spellIndex==1) fallingCells=CountOccupiedSnowCells();
        if (spellIndex==2)
        {
            Require(snow.Gathering && !snow.Frozen, "B did not gather around the tracked fingertip.");
            gatheredCells=CountOccupiedSnowCells();
            Require(gatheredCells<=fallingCells/2, $"B did not concentrate snowfall ({fallingCells} to {gatheredCells} occupied cells).");
        }
        if (spellIndex==3 && !testingResume)
        {
            int dispersedCells=CountOccupiedSnowCells();
            transitions.Add($"Snow occupied cells out of 27: falling {fallingCells}, gathered {gatheredCells}, after C {dispersedCells}.");
            Require(!snow.Gathering && !snow.Frozen && dispersedCells>=21,
                $"C did not spread snow across the fitted room ({dispersedCells}/27 occupied cells).");
        }
        if (spellIndex<Spells.Length) { StartSpell(now);return; }
        if (!testingResume)
        {
            Require(snow.Frozen && !snow.Gathering, "D did not freeze the snow.");
            if (frozenPositions==null)
            {
                frozenPositions=(Vector3[])snow.Positions.Clone();
                readyAt=now+.32f;
                return;
            }
            Require(frozenPositions.SequenceEqual(snow.Positions), "Frozen positions changed between samples.");
            transitions.Add("Snow menu: A started 160 flakes, B gathered them, C dispersed them, and D held every frozen position.");
            testingResume=true;
            spellIndex=3;
            StartSpell(now);
            return;
        }
        Require(!snow.Frozen && !frozenPositions.SequenceEqual(snow.Positions), "Second D did not resume snowfall.");
        int resumedCells=CountOccupiedSnowCells();
        Require(resumedCells>=21, $"Resumed snowfall lost room coverage ({resumedCells}/27 occupied cells).");
        transitions.Add($"Resumed snow occupied {resumedCells}/27 cells.");
        Require(snow.Positions.All(p=>TabletopHandSample.Finite(p) && Mathf.Abs(p.x)<.32f &&
                p.y>=.035f && p.y<=.625f && Mathf.Abs(p.z)<.27f), "Resumed snow left the room volume.");
        Capture();
        CheckPassthroughRoom();
        var volume = target.visual.Find("Owned bounded room snow");
        Require(volume && volume.gameObject.activeInHierarchy, "Resumed snowfall did not show the snow volume.");
        var snowBounds = new Bounds(volume.position,Vector3.zero);
        foreach (var point in snow.Positions)
            snowBounds.Encapsulate(volume.TransformPoint(point));
        CaptureVariantPerspective("snow-room-perspective.png", volume, snowBounds);
        roomUi.Command("Magic window");
        Require(!target.RoomSnowMode && !target.RoomPortalMode && !snow.Active && !snow.Snowing &&
                !snow.Frozen && snow.LiveFlakes==0, "Restoring the default room left snow state or flakes.");
        CheckRoomFit();
        transitions.Add($"Snow resumed on second D. Restored the default menu on the same fit after {recognized} recorded PICF responses.");
        withholdingReplies=true;
    }

    static void CaptureVariantPerspective(string name, Transform view, Bounds bounds)
    {
        const int width = 960;
        const int height = 720;
        var go = new GameObject("Room variant perspective review camera");
        var camera = go.AddComponent<Camera>();
        camera.fieldOfView = 42f;
        camera.nearClipPlane = .01f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.clear;
        var direction = view.TransformDirection(new Vector3(.27f,.27f,-1f)).normalized;
        float distance = bounds.extents.magnitude / Mathf.Sin(camera.fieldOfView*Mathf.Deg2Rad*.5f) * 1.08f;
        camera.transform.position = bounds.center + direction*distance;
        camera.transform.LookAt(bounds.center);
        var surface = RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGB32);
        var pixels = new Texture2D(width,height,TextureFormat.RGBA32,false);
        var oldSurface = RenderTexture.active;
        var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude,FindObjectsSortMode.None)
            .Where(canvas => canvas.enabled && canvas.renderMode == RenderMode.WorldSpace).ToArray();
        var markers = target.GetComponentsInChildren<Renderer>(true)
            .Where(renderer => (renderer.name == "Selection corner" || renderer.name == "Gesture preview")
                && !renderer.forceRenderingOff).ToArray();
        try
        {
            foreach (var canvas in canvases)
                canvas.enabled = false;
            foreach (var marker in markers)
                marker.forceRenderingOff = true;
            camera.targetTexture = surface;
            camera.Render();
            RenderTexture.active = surface;
            pixels.ReadPixels(new Rect(0,0,width,height),0,0);
            pixels.Apply();
            var colors = pixels.GetPixels32();
            Require(colors.Any(pixel => pixel.a > 200) && colors[0].a < 5 && colors[colors.Length-1].a < 5,
                "The variant review did not retain visible geometry and transparent background.");
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(Output,name),pixels.EncodeToPNG());
        }
        finally
        {
            foreach (var canvas in canvases)
                canvas.enabled = true;
            foreach (var marker in markers)
                marker.forceRenderingOff = false;
            camera.targetTexture = null;
            RenderTexture.active = oldSurface;
            RenderTexture.ReleaseTemporary(surface);
            UnityEngine.Object.DestroyImmediate(pixels);
            UnityEngine.Object.DestroyImmediate(go);
        }
    }

    static void FinishRoomVariants()
    {
        Require(!target.RoomSnowMode && !target.RoomPortalMode && !snow.Active && !portal.Enabled &&
            !target.confirmedPlane, "Expired room retained a variant or manual surface.");
        transitions.Add("The tracked actor expired after replies stopped.");
        Finish(0,"PASS: saved ParkRoom tracked PICF replay entered portal and snow, executed shared gestures, and restored the original menu and fit.");
    }
}
