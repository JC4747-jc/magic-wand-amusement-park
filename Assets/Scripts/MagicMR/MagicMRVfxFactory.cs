using UnityEngine;

namespace MagicMR
{
    /// <summary>
    /// Runtime particle helpers (no prefab dependency) for study polish VFX.
    /// </summary>
    public static class MagicMRVfxFactory
    {
        public static ParticleSystem CreateSmoke(Transform parent)
        {
            var go = new GameObject("Appearance_smoke");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, -0.35f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            ConfigureCommon(ps);

            var main = ps.main;
            main.startLifetime = 1.4f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.01f, 0.04f);
            main.startColor = new Color(0.15f, 0.12f, 0.1f, 0.55f);
            main.startSpeed = 0.08f;
            main.maxParticles = 40;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.loop = false;
            main.playOnAwake = false;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 18) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 18f;
            shape.radius = 0.01f;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.black, 0f), new GradientColorKey(new Color(0.2f, 0.2f, 0.2f), 1f) },
                new[] { new GradientAlphaKey(0.55f, 0f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = grad;

            ApplyUnlitMaterial(ps, new Color(0.1f, 0.1f, 0.1f, 0.5f));
            return ps;
        }

        public static ParticleSystem CreateRingBurst(Transform parent)
        {
            var go = new GameObject("calibration_ring");
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ConfigureCommon(ps);

            var main = ps.main;
            main.startLifetime = 0.6f;
            main.startSize = 0.015f;
            main.startColor = new Color(0.45f, 0.85f, 1f, 0.9f);
            main.startSpeed = 0.45f;
            main.maxParticles = 48;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 36) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.02f;

            ApplyUnlitMaterial(ps, new Color(0.5f, 0.9f, 1f, 0.85f));
            return ps;
        }

        public static ParticleSystem CreateGoldDust(Transform parent)
        {
            var go = new GameObject("flower_gold_dust");
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ConfigureCommon(ps);

            var main = ps.main;
            main.startLifetime = 1.8f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.004f, 0.01f);
            main.startColor = new Color(1f, 0.85f, 0.35f, 0.8f);
            main.startSpeed = 0.03f;
            main.maxParticles = 30;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 8f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.05f;

            ApplyUnlitMaterial(ps, new Color(1f, 0.85f, 0.3f, 0.75f));
            return ps;
        }

        public static GameObject CreateGhostTrailClone(Transform source, float lifetime)
        {
            var ghost = Object.Instantiate(source.gameObject);
            ghost.name = "evade_ghost_trail";
            ghost.SetActive(false);

            foreach (var rb in ghost.GetComponentsInChildren<Rigidbody>(true))
                Object.Destroy(rb);
            foreach (var behaviour in ghost.GetComponentsInChildren<MonoBehaviour>(true))
                Object.Destroy(behaviour);
            foreach (var col in ghost.GetComponentsInChildren<Collider>(true))
                Object.Destroy(col);

            foreach (var renderer in ghost.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null)
                    continue;
                var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
                var color = new Color(0.35f, 0.75f, 1f, 0.35f);
                if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", color);
                if (mat.HasProperty("_Color"))
                    mat.SetColor("_Color", color);
                SetTransparent(mat);
                renderer.sharedMaterial = mat;
                renderer.enabled = true;
            }

            ghost.SetActive(true);
            Object.Destroy(ghost, lifetime);
            return ghost;
        }

        static void SetTransparent(Material mat)
        {
            if (mat.HasProperty("_Surface"))
                mat.SetFloat("_Surface", 1f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = 3000;
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }

        static void ConfigureCommon(ParticleSystem ps)
        {
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
        }

        static void ApplyUnlitMaterial(ParticleSystem ps, Color color)
        {
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                         ?? Shader.Find("Particles/Standard Unlit")
                         ?? Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Unlit/Color");
            var mat = new Material(shader);
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", color);
            renderer.sharedMaterial = mat;
        }
    }
}
