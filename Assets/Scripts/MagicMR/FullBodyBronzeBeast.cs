using System;
using System.Collections.Generic;
using UnityEngine;

namespace MagicMR
{
    // Original fictional sculpture, not a museum reconstruction. The old CC0 bust is untouched.
    public sealed partial class FullBodyBronzeBeast : MonoBehaviour
    {
        sealed class Leg
        {
            public Transform upper, lower, foot;
            public Vector3 upperVector, lowerVector, contact, pole;
        }
        readonly List<Transform> bones = new List<Transform>();
        readonly List<Leg> legs = new List<Leg>();
        Vector3[] restPositions;
        Quaternion[] restRotations;
        Transform pelvis, chest, neck, head, jaw, tail;
        Mesh ownedMesh;
        Material[] ownedMaterials;
        SkinnedMeshRenderer skin;
        public float Duration => 5.4f;
        public float Progress { get; private set; }
        public Transform Head => head;
        public Transform Chest => chest;
        public SkinnedMeshRenderer Skin => skin;
        public int FootCount => legs.Count;
        public Vector3 FootPosition(int index) => legs[index].foot.position;

        public static FullBodyBronzeBeast Create(Transform parent)
        {
            var root = new GameObject("Original fictional bronze guardian - full body");
            root.transform.SetParent(parent, false);
            var model = root.AddComponent<FullBodyBronzeBeast>();
            try { model.Initialize(); return model; }
            catch { if (Application.isPlaying) Destroy(root); else DestroyImmediate(root); throw; }
        }

        Transform Bone(string name, Transform parent, Vector3 restPoint)
        {
            var bone = new GameObject(name).transform;
            bone.SetParent(parent, false);
            bone.position = transform.TransformPoint(restPoint);
            bone.rotation = transform.rotation;
            bones.Add(bone);
            return bone;
        }

        public void Initialize()
        {
            if (skin) return;
            pelvis = Bone("BronzePelvis", transform, new Vector3(0, .31f, .16f));
            chest = Bone("BronzeChest", pelvis, new Vector3(0, .31f, -.17f));
            neck = Bone("BronzeNeck", chest, new Vector3(0, .40f, -.29f));
            head = Bone("BronzeHead", neck, new Vector3(0, .51f, -.37f));
            jaw = Bone("BronzeJaw", head, new Vector3(0, .46f, -.43f));
            tail = Bone("BronzeTail", pelvis, new Vector3(0, .365f, .37f));
            for (int i = 0; i < 4; i++)
            {
                float side = i % 2 == 0 ? -1 : 1;
                bool front = i < 2;
                var hip = new Vector3(side * .145f, .30f, front ? -.18f : .23f);
                var knee = new Vector3(side * .16f, .17f, front ? -.11f : .16f);
                var ankle = new Vector3(side * .17f, .048f, front ? -.28f : .29f);
                string id = (front ? "Front" : "Hind") + (side < 0 ? "Left" : "Right");
                var upper = Bone(id + "Upper", front ? chest : pelvis, hip);
                var lower = Bone(id + "Lower", upper, knee);
                var foot = Bone(id + "Foot", lower, ankle);
                legs.Add(new Leg { upper = upper, lower = lower, foot = foot,
                    upperVector = knee - hip, lowerVector = ankle - knee, contact = ankle,
                    pole = (knee - hip) - Vector3.Project(knee - hip, ankle - hip) });
            }
            restPositions = new Vector3[bones.Count]; restRotations = new Quaternion[bones.Count];
            for (int i = 0; i < bones.Count; i++)
            { restPositions[i] = bones[i].localPosition; restRotations[i] = bones[i].localRotation; }
            BuildGeometry();
            transform.localScale = Vector3.one * (.48f / ownedMesh.bounds.size.y);
            ResetPose();
        }

        void RestoreBones()
        {
            for (int i = 0; i < bones.Count; i++) if (bones[i])
            { bones[i].localPosition = restPositions[i]; bones[i].localRotation = restRotations[i]; }
        }

        bool Plant(Leg leg)
        {
            Vector3 hip = transform.InverseTransformPoint(leg.upper.position);
            Vector3 direction = leg.contact - hip;
            float distance = direction.magnitude;
            if (!BronzeStretchMath.Triangle(distance, leg.upperVector.magnitude, leg.lowerVector.magnitude,
                    out float along, out float height)) return false;
            direction /= distance;
            Vector3 pole = leg.pole - direction * Vector3.Dot(leg.pole, direction);
            if (pole.sqrMagnitude < 1e-8f) return false;
            Vector3 knee = hip + direction * along + pole.normalized * height;
            // Solve in each parent's local space. World rotation assignment under
            // an anisotropically fitted ancestor introduces shear-related foot drift.
            Vector3 kneeInParent = leg.upper.parent.InverseTransformPoint(transform.TransformPoint(knee));
            leg.upper.localRotation = AlignLeg(leg.lower.localPosition, kneeInParent - leg.upper.localPosition);
            Vector3 contactInParent = leg.lower.parent.InverseTransformPoint(transform.TransformPoint(leg.contact));
            leg.lower.localRotation = AlignLeg(leg.foot.localPosition, contactInParent - leg.lower.localPosition);
            leg.foot.localRotation = Quaternion.LookRotation(
                leg.foot.parent.InverseTransformVector(transform.TransformVector(Vector3.forward)),
                leg.foot.parent.InverseTransformVector(transform.TransformVector(Vector3.up)));
            return true;
        }

        static Quaternion AlignLeg(Vector3 from, Vector3 to)
        {
            from.Normalize();to.Normalize();
            float dot = Mathf.Clamp(Vector3.Dot(from,to),-1,1);
            if(dot < -.9999f)return Quaternion.FromToRotation(from,to);
            // Keep small nonzero corrections near the start/end of the stretch.
            // FromToRotation's near-parallel shortcut can leave sub-mm contact error.
            Vector3 cross = Vector3.Cross(from,to);
            return new Quaternion(cross.x,cross.y,cross.z,1+dot).normalized;
        }

        public void Sample(float progress)
        {
            Initialize();
            Progress = float.IsFinite(progress) ? Mathf.Clamp01(progress) : 0;
            RestoreBones();
            float amount = BronzeStretchMath.Amount(Progress);
            if (amount == 0) return;
            pelvis.localPosition += new Vector3(0, .014f, .012f) * amount;
            chest.localPosition += new Vector3(0, -.085f, .055f) * amount;
            chest.localRotation *= Quaternion.Euler(8 * amount, 0, 0);
            neck.localRotation *= Quaternion.Euler(17 * amount, 0, 0);
            head.localRotation *= Quaternion.Euler(9 * amount, 0, 0);
            jaw.localRotation *= Quaternion.Euler(-7 * amount, 0, 0);
            tail.localRotation *= Quaternion.Euler(0, 8 * amount, -12 * amount);
            foreach (var leg in legs) if (!Plant(leg))
            { RestoreBones(); Progress = 0; throw new InvalidOperationException("Bronze stretch exceeded planted-leg reach"); }
        }

        public void ResetPose() { if (restPositions != null) RestoreBones(); Progress = 0; }
        void OnDisable() { ResetPose(); }
        void OnDestroy()
        {
            Release(ownedMesh);
            if (ownedMaterials != null) foreach (var material in ownedMaterials) Release(material);
        }
        static void Release(UnityEngine.Object resource)
        {
            if (!resource) return;
            if (Application.isPlaying) Destroy(resource); else DestroyImmediate(resource);
        }
    }
}
