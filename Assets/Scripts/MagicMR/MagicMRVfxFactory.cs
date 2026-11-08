using UnityEngine;
using UnityEngine.Rendering;

namespace MagicMR
{
    /// <summary>Small, bounded particle layers, using a build-included stereo shader.</summary>
    public static class MagicMRVfxFactory
    {
        static Material softMaterial;
        static Material SoftMaterial
        {
            get
            {
                if (softMaterial == null) softMaterial = new Material(Resources.Load<Shader>("MagicMR/SoftParticle")) { hideFlags = HideFlags.HideAndDontSave };
                return softMaterial;
            }
        }
        static ParticleSystem Layer(Transform parent, string label, Color color, int count, float life, float size, float speed, bool loop = false)
        {
            var go = new GameObject(label);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false; main.loop = loop; main.duration = 1;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * .65f, life);
            main.startSize = new ParticleSystem.MinMaxCurve(size * .45f, size);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * .4f, speed);
            main.startColor = color; main.maxParticles = count;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            var emission = ps.emission; emission.rateOverTime = loop ? count / life * .55f : 0;
            if (!loop) emission.SetBursts(new[] { new ParticleSystem.Burst(0, (short)count) });
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = .012f;
            var fade = ps.colorOverLifetime; fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .12f), new GradientAlphaKey(.45f, .55f), new GradientAlphaKey(0, 1) });
            fade.color = gradient;
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = SoftMaterial; renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            return ps;
        }
        // Position particle emitters in metres regardless of the authored model scale.
        static void Position(ParticleSystem ps, Vector3 world)
        {
            ps.transform.position = world;
            var scale = ps.transform.parent.lossyScale;
            ps.transform.localScale = new Vector3(1 / Mathf.Max(.0001f, Mathf.Abs(scale.x)), 1 / Mathf.Max(.0001f, Mathf.Abs(scale.y)), 1 / Mathf.Max(.0001f, Mathf.Abs(scale.z)));
        }
        public static ParticleSystem CreateSmoke(Transform parent)
        {
            var ps = Layer(parent, "Appearance smoke + embers", new Color(.19f, .17f, .16f, .32f), 26, 1.7f, .025f, .012f);
            Position(ps, parent.position);
            var velocity = ps.velocityOverLifetime; velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.World; velocity.y = .028f;
            var size = ps.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, .3f, 1, 1.6f));
            var noise = ps.noise; noise.enabled = true; noise.strength = .008f; noise.frequency = 4; noise.scrollSpeed = .2f; noise.quality = ParticleSystemNoiseQuality.Low;
            var embers = Layer(ps.transform, "Cooling embers", new Color(1, .37f, .055f, 1), 22, .85f, .0028f, .055f);
            var gravity = embers.main; gravity.gravityModifier = .012f;
            return ps;
        }
        public static ParticleSystem CreateRingBurst(Transform parent)
        {
            var ps = Layer(parent, "Calibration dew", new Color(.5f, .88f, 1, .9f), 32, .65f, .004f, .075f);
            Position(ps, parent.position);
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = .018f;
            return ps;
        }
        public static ParticleSystem CreateGoldDust(Transform parent)
        {
            var blossom = parent.Find("Blossom");
            var ps = Layer(parent, "Flower pollen", new Color(1, .79f, .28f, .85f), 24, 2.2f, .0025f, .012f, true);
            Position(ps, blossom != null ? blossom.position : parent.position + Vector3.up * .08f);
            var shape = ps.shape; shape.radius = .045f;
            var velocity = ps.velocityOverLifetime; velocity.enabled = true; velocity.y = .008f;
            var petals = Layer(ps.transform, "Rose dew", new Color(1, .39f, .5f, .8f), 16, 1.2f, .005f, .045f);
            ps.Play(true);
            return ps;
        }
        public static GameObject CreateGhostTrailClone(Transform source, float lifetime)
        {
            // Copy only geometry; cloning the gameplay root invokes Awake on tracking and audio.
            var ghost = new GameObject("Evade silhouette");
            ghost.transform.SetPositionAndRotation(source.position, source.rotation);
            ghost.transform.localScale = source.lossyScale;
            foreach (var mesh in source.GetComponentsInChildren<MeshFilter>())
            {
                var renderer = mesh.GetComponent<MeshRenderer>();
                if (renderer == null || !renderer.enabled || mesh.sharedMesh == null) continue;
                var part = new GameObject(mesh.name);
                part.transform.SetPositionAndRotation(mesh.transform.position, mesh.transform.rotation);
                part.transform.localScale = mesh.transform.lossyScale;
                part.transform.SetParent(ghost.transform, true);
                part.AddComponent<MeshFilter>().sharedMesh = mesh.sharedMesh;
                var copy = part.AddComponent<MeshRenderer>(); copy.sharedMaterial = SoftMaterial;
                copy.shadowCastingMode = ShadowCastingMode.Off;
            }
            ghost.AddComponent<MagicMRGhostFade>().Initialize(lifetime);
            return ghost;
        }
    }
}
