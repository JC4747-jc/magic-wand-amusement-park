using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MagicMR
{
    public sealed partial class FullBodyBronzeBeast
    {
        // All surfaces are authored here. One skin / four material passes; no new downloads or textures.
        sealed class Sculpture
        {
            public readonly List<Vector3> vertices = new List<Vector3>();
            public readonly List<BoneWeight> weights = new List<BoneWeight>();
            public readonly List<int>[] triangles = { new List<int>(), new List<int>(), new List<int>(), new List<int>() };
            int Add(Vector3 point, int bone, bool torso)
            {
                vertices.Add(point);
                float front = Mathf.Clamp01((.25f - point.z) / .5f);
                weights.Add(torso ? new BoneWeight { boneIndex0 = 0, boneIndex1 = 1, weight0 = 1 - front, weight1 = front } :
                    new BoneWeight { boneIndex0 = bone, weight0 = 1 });
                return vertices.Count - 1;
            }
            void Tri(int material, int a, int b, int c) { triangles[material].Add(a); triangles[material].Add(b); triangles[material].Add(c); }
            public void Oval(Vector3 center, Vector3 radius, int bone, int material = 0,
                int sides = 16, int rings = 10, Quaternion rotation = default, bool torso = false)
            {
                if (rotation.x == 0 && rotation.y == 0 && rotation.z == 0 && rotation.w == 0) rotation = Quaternion.identity;
                int bottom = Add(center + rotation * new Vector3(0, -radius.y, 0), bone, torso);
                int first = vertices.Count;
                for (int ring = 1; ring < rings; ring++)
                {
                    float angle = -Mathf.PI / 2 + Mathf.PI * ring / rings;
                    float latitude = Mathf.Cos(angle);
                    for (int side = 0; side < sides; side++)
                    {
                        float longitude = 2 * Mathf.PI * side / sides;
                        Add(center + rotation * new Vector3(radius.x * latitude * Mathf.Cos(longitude),
                            radius.y * Mathf.Sin(angle), radius.z * latitude * Mathf.Sin(longitude)), bone, torso);
                    }
                }
                for (int j = 0; j < sides; j++) Tri(material, bottom, first + j, first + (j + 1) % sides);
                for (int ring = 0; ring < rings - 2; ring++) for (int j = 0; j < sides; j++)
                {
                    int a = first + ring * sides + j, b = first + ring * sides + (j + 1) % sides;
                    int c = a + sides, d = b + sides;
                    Tri(material, a, c, b); Tri(material, b, c, d);
                }
                int top = Add(center + rotation * new Vector3(0, radius.y, 0), bone, torso);
                int last = first + (rings - 2) * sides;
                for (int j = 0; j < sides; j++) Tri(material, last + j, top, last + (j + 1) % sides);
            }
            public void Limb(Vector3 from, Vector3 to, float radius, int bone, int material = 0)
            {
                var direction = to - from;
                Oval((from + to) * .5f, new Vector3(radius, direction.magnitude * .61f, radius * .9f), bone,
                    material, 12, 8, Quaternion.FromToRotation(Vector3.up, direction));
            }
            public void Tube(Vector3[] path, float radius, int bone, int material, bool taper = false, bool torso = false)
            {
                const int sides = 8;
                int first = vertices.Count;
                for (int i = 0; i < path.Length; i++)
                {
                    Vector3 tangent = path[Mathf.Min(i + 1, path.Length - 1)] - path[Mathf.Max(0, i - 1)];
                    var orientation = Quaternion.FromToRotation(Vector3.up, tangent.normalized);
                    float r = radius * (taper ? Mathf.Lerp(1, .06f, (float)i / (path.Length - 1)) : 1);
                    for (int j = 0; j < sides; j++)
                    {
                        float angle = 2 * Mathf.PI * j / sides;
                        Add(path[i] + orientation * new Vector3(Mathf.Cos(angle) * r, 0, Mathf.Sin(angle) * r), bone, torso);
                    }
                }
                for (int i = 0; i < path.Length - 1; i++) for (int j = 0; j < sides; j++)
                {
                    int a = first + i * sides + j, b = first + i * sides + (j + 1) % sides;
                    Tri(material, a, a + sides, b); Tri(material, b, a + sides, b + sides);
                }
                int begin = Add(path[0], bone, torso), end = Add(path[path.Length - 1], bone, torso);
                int last = first + (path.Length - 1) * sides;
                for (int j = 0; j < sides; j++)
                { Tri(material, begin, first + j, first + (j + 1) % sides); Tri(material, end, last + (j + 1) % sides, last + j); }
            }
        }

        void BuildGeometry()
        {
            var shape = new Sculpture();
            shape.Oval(new Vector3(0, .325f, .045f), new Vector3(.185f, .135f, .335f), 0, 0, 24, 16, torso: true);
            shape.Oval(new Vector3(0, .43f, -.275f), new Vector3(.108f, .132f, .115f), 2, rotation: Quaternion.Euler(-22, 0, 0));
            shape.Oval(new Vector3(0, .51f, -.385f), new Vector3(.108f, .085f, .11f), 3, 0, 20, 12);
            shape.Oval(new Vector3(0, .477f, -.485f), new Vector3(.10f, .052f, .078f), 3, 0, 20, 10);
            shape.Oval(new Vector3(0, .449f, -.475f), new Vector3(.086f, .029f, .07f), 4);
            shape.Oval(new Vector3(0, .486f, -.55f), new Vector3(.064f, .024f, .014f), 3, 3, 16, 8);
            for (int side = -1; side <= 1; side += 2)
            {
                shape.Oval(new Vector3(side * .14f, .535f, -.34f), new Vector3(.075f, .023f, .042f), 3, 0, 12, 8, Quaternion.Euler(0, 0, side * 16));
                shape.Oval(new Vector3(side * .089f, .538f, -.445f), new Vector3(.024f, .019f, .017f), 3, 2, 12, 8);
                shape.Oval(new Vector3(side * .093f, .54f, -.456f), new Vector3(.014f, .012f, .008f), 3, 3, 10, 8);
                var horn = new Vector3[13];
                for (int i = 0; i < horn.Length; i++)
                {
                    float t = (float)i / (horn.Length - 1), angle = t * Mathf.PI * 1.2f;
                    horn[i] = new Vector3(side * (.085f + .12f * Mathf.Sin(angle * .75f)), .56f + .105f * Mathf.Sin(angle), -.32f + .115f * t);
                }
                shape.Tube(horn, .028f, 3, 2, true);
                var cheek = new Vector3[25];
                for (int i = 0; i < cheek.Length; i++)
                {
                    float t = (float)i / (cheek.Length - 1), angle = t * Mathf.PI * 3.2f, r = .033f * (1 - .8f * t);
                    cheek[i] = new Vector3(side * (.06f + Mathf.Cos(angle) * r), .493f + Mathf.Sin(angle) * r, -.48f);
                }
                shape.Tube(cheek, .0045f, 3, 1);
                for (int line = 0; line < 2; line++)
                {
                    var inlay = new Vector3[25];
                    for (int i = 0; i < inlay.Length; i++)
                    {
                        float t = (float)i / (inlay.Length - 1);
                        inlay[i] = new Vector3(side * .181f, .32f + .037f * Mathf.Sin(t * Mathf.PI * 4 + line * Mathf.PI), -.17f + .39f * t);
                    }
                    shape.Tube(inlay, .0045f, 0, line == 0 ? 1 : 2, torso: true);
                }
            }
            for (int i = 0; i < legs.Count; i++)
            {
                var leg = legs[i]; int index = 6 + i * 3;
                Vector3 hip = transform.InverseTransformPoint(leg.upper.position), knee = hip + leg.upperVector;
                shape.Oval(hip, new Vector3(.065f, .092f, .085f), index, 0, 14, 10);
                shape.Limb(hip, knee, .045f, index);
                shape.Limb(knee, leg.contact, .034f, index + 1);
                shape.Oval(leg.contact + new Vector3(0, -.016f, -.018f), new Vector3(.058f, .032f, .075f), index + 2, 0, 14, 8);
                for (int toe = -1; toe <= 1; toe++)
                    shape.Oval(leg.contact + new Vector3(toe * .03f, -.025f, -.065f), new Vector3(.019f, .023f, .032f), index + 2, 2, 8, 6);
            }
            var tailPath = new Vector3[17];
            for (int i = 0; i < tailPath.Length; i++)
            {
                float t = (float)i / (tailPath.Length - 1);
                tailPath[i] = new Vector3(.085f * Mathf.Sin(t * Mathf.PI * 1.25f), .365f + .09f * t, .37f + .17f * t);
            }
            shape.Tube(tailPath, .018f, 5, 0, true);
            shape.Oval(tailPath[tailPath.Length - 1], new Vector3(.025f, .032f, .023f), 5, 1, 10, 8);
            ownedMesh = new Mesh { name = "Original bronze guardian full-body skin", indexFormat = IndexFormat.UInt16 };
            ownedMesh.SetVertices(shape.vertices); ownedMesh.boneWeights = shape.weights.ToArray();
            var bindposes = new Matrix4x4[bones.Count];
            for (int i = 0; i < bones.Count; i++) bindposes[i] = bones[i].worldToLocalMatrix * transform.localToWorldMatrix;
            ownedMesh.bindposes = bindposes; ownedMesh.subMeshCount = 4;
            for (int i = 0; i < 4; i++) ownedMesh.SetTriangles(shape.triangles[i], i);
            ownedMesh.RecalculateNormals(); ownedMesh.RecalculateBounds();
            if (ownedMesh.vertexCount > 12000 || ownedMesh.triangles.Length / 3 > 22000) throw new InvalidOperationException("Bronze mesh budget exceeded");
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (!shader) throw new InvalidOperationException("Bronze PBR shader unavailable");
            Color[] colors = { new Color(.27f, .16f, .055f), new Color(.08f, .28f, .22f), new Color(.52f, .32f, .10f), new Color(.016f, .023f, .021f) };
            ownedMaterials = new Material[colors.Length];
            for (int i = 0; i < colors.Length; i++)
            {
                var material = ownedMaterials[i] = new Material(shader) { name = "Original bronze finish " + i };
                material.SetColor(material.HasProperty("_BaseColor") ? "_BaseColor" : "_Color", colors[i]);
                material.SetFloat("_Metallic", i == 1 ? .45f : .85f); material.SetFloat("_Smoothness", i == 3 ? .65f : .38f);
            }
            skin = gameObject.AddComponent<SkinnedMeshRenderer>(); skin.sharedMesh = ownedMesh;
            skin.bones = bones.ToArray(); skin.rootBone = pelvis; skin.sharedMaterials = ownedMaterials;
            skin.quality = SkinQuality.Bone2; skin.updateWhenOffscreen = true;
            var bounds = ownedMesh.bounds; bounds.Expand(.35f); skin.localBounds = bounds;
        }
    }
}
