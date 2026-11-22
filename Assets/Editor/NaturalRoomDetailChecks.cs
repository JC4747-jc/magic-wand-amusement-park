using System;
using System.Linq;
using MagicMR;
using UnityEditor;
using UnityEngine;

public static class NaturalRoomDetailChecks
{
    static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
    static bool HitTriangle(Ray ray,Vector3 a,Vector3 b,Vector3 c)
    {
        var u=b-a;var v=c-a;var p=Vector3.Cross(ray.direction,v);float determinant=Vector3.Dot(u,p);if(Mathf.Abs(determinant)<1e-9f)return false;
        var s=ray.origin-a;float x=Vector3.Dot(s,p)/determinant;if(x<0||x>1)return false;var q=Vector3.Cross(s,u);float y=Vector3.Dot(ray.direction,q)/determinant;
        return y>=0&&x+y<=1&&Vector3.Dot(v,q)/determinant>=0;
    }
    public static void Run()
    {
        var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MagicMR/Park/ParkRoom.prefab"));
        try
        {
            var actor=go.GetComponent<ParkActor>();go.SetActive(true);actor.Build();
            string[] names={"Timber joinery","Plaster walls","Woven rug","Table pedestal","Rust book cloth","Olive book cloth","Book pages","ReadingChair","CeramicVase"};
            var parts=names.Select(n=>actor.visual.Find(n)).ToArray();Require(parts.All(t=>t),"Reading-corner fixture missing");
            var renderers=parts.SelectMany(t=>t.GetComponentsInChildren<MeshRenderer>()).ToArray();Require(renderers.Length<=12,"Room static draw-object budget exceeded");
            long triangles=0;foreach(var part in parts)foreach(var filter in part.GetComponentsInChildren<MeshFilter>())
            {
                var mesh=filter.sharedMesh;Require(mesh&&mesh.vertexCount<30000,"Room mesh vertex budget");triangles+=mesh.triangles.Length/3;
                Require(mesh.vertices.All(p=>float.IsFinite(p.x)&&float.IsFinite(p.y)&&float.IsFinite(p.z)),"Nonfinite room geometry");
                Require(mesh.normals.All(n=>float.IsFinite(n.x)&&float.IsFinite(n.y)&&float.IsFinite(n.z)&&n.sqrMagnitude>.5f),"Invalid room normal");
                Require(mesh.tangents.Length==mesh.vertexCount&&mesh.tangents.All(t=>float.IsFinite(t.x)&&float.IsFinite(t.y)&&float.IsFinite(t.z)),"Room tangent frame missing");
                var vertices=mesh.vertices;var indices=mesh.triangles;
                for(int i=0;i<indices.Length;i+=3)Require(Vector3.Cross(vertices[indices[i+1]]-vertices[indices[i]],vertices[indices[i+2]]-vertices[indices[i]]).sqrMagnitude>1e-20f,"Degenerate room triangle "+mesh.name+" triangle="+i/3);
            }
            Require(triangles<25000,"Room triangle budget exceeded: "+triangles);
            foreach(var name in new[]{"ReadingChair","CeramicVase"})
            {
                var prop=actor.visual.Find(name);var rs=prop.GetComponentsInChildren<Renderer>();var bounds=rs[0].bounds;foreach(var renderer in rs)bounds.Encapsulate(renderer.bounds);
                Require(Mathf.Abs(bounds.min.y-(name=="ReadingChair"?.014f:.234f))<.0001f,"Room prop base not supported: "+name);
                Require(Mathf.Abs(bounds.size.y-(name=="ReadingChair"?.37f:.095f))<.0001f,"Prop nominal scale changed");
                foreach(var material in rs.SelectMany(r=>r.sharedMaterials))Require(material.shader.name=="Universal Render Pipeline/Lit"&&material.GetTexture("_BaseMap")&&material.GetTexture("_BumpMap")&&material.GetTexture("_MetallicGlossMap")==material.GetTexture("_OcclusionMap")&&material.IsKeywordEnabled("_METALLICSPECGLOSSMAP"),"Original prop PBR maps/channel packing missing");
            }
            var wall=actor.visual.Find("Plaster walls").GetComponent<MeshFilter>().sharedMesh;
            // Every plaster vertex must lie outside the interior of the opening.
            Require(!wall.vertices.Any(p=>p.x>-.351f&&p.x<-.049f&&p.y>.279f&&p.y<.521f),"Window opening filled by wall geometry");
            var wallPoints=wall.vertices;var wallIndices=wall.triangles;
            foreach(var origin in new[]{new Vector3(-.28f,.45f,-.2f),new Vector3(-.12f,.34f,-.2f)})
                for(int i=0;i<wallIndices.Length;i+=3)Require(!HitTriangle(new Ray(origin,Vector3.forward),wallPoints[wallIndices[i]],wallPoints[wallIndices[i+1]],wallPoints[wallIndices[i+2]]),"Wall triangle spans the window aperture");
            Require(NaturalRoomMeshes.Get("Timber joinery")==actor.visual.Find("Timber joinery").GetComponent<MeshFilter>().sharedMesh,"Room geometry cache not shared");
            Debug.Log("NATURAL_ROOM_DETAIL_PASS: "+triangles+" static triangles, "+renderers.Length+" renderers; source CC0 chair/vase, packed PBR channels, supported bases, metric UV/tangents, wall opening and shared geometry; nominal cutaway, no room reconstruction claim");
        }
        finally{UnityEngine.Object.DestroyImmediate(go);}
    }
    public static void BuildAndReview(){RoomFurnitureAssetBuilder.Run();Run();ParkNaturalModelChecks.Render();}
    public static void RunLifecycle(){BuildAndReview();NaturalRoomPlayChecks.RunAll();}
}
