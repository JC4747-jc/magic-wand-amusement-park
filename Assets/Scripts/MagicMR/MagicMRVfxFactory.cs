using UnityEngine;

namespace MagicMR
{
    /// <summary>
    /// Runtime particle helpers (no prefab dependency) for study polish VFX.
    /// </summary>
    public static class MagicMRVfxFactory
    {
        static Shader s_ParticleShader;
        static Shader s_UnlitShader;
        static Material s_SmokeMaterial;
        static Material s_DustMaterial;
        static Material s_ShatterMaterial;
        static Material s_RingMaterial;

        public static ParticleSystem CreateSmoke(Transform parent)
        {
            var go = new GameObject("Appearance_smoke");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            ConfigureCommon(ps);

            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.7f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.038f);
            main.startColor = new Color(0.18f, 0.14f, 0.11f, 0.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.04f, 0.11f);
            main.maxParticles = 48;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.loop = false;
            main.playOnAwake = false;
            main.gravityModifier = -0.035f;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(System.Array.Empty<ParticleSystem.Burst>());

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 22f;
            shape.radius = 0.012f;
            shape.rotation = new Vector3(-90f, 0f, 0f);

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
                1f,
                AnimationCurve.EaseInOut(0f, 0.55f, 1f, 1.65f));

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.22f, 0.16f, 0.12f), 0f),
                    new GradientColorKey(new Color(0.12f, 0.12f, 0.12f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.5f, 0.12f),
                    new GradientAlphaKey(0.28f, 0.55f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = grad;

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.y = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
            velocity.x = new ParticleSystem.MinMaxCurve(-0.02f, 0.02f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.02f, 0.02f);

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.maxParticleSize = 0.09f;
            ApplyCachedMaterial(ps, ref s_SmokeMaterial, new Color(0.12f, 0.11f, 0.1f, 0.45f));
            return ps;
        }

        public static void EmitSmokeBurst(ParticleSystem ps, short count = 20)
        {
            if (ps == null)
                return;

            if (!ps.isPlaying)
                ps.Play(true);
            ps.Emit(count);
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

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(new Color(0.45f, 0.85f, 1f), 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = grad;

            ApplyCachedMaterial(ps, ref s_RingMaterial, new Color(0.5f, 0.9f, 1f, 0.85f));
            return ps;
        }

        public static ParticleSystem CreateGoldDust(Transform parent)
        {
            var go = new GameObject("flower_gold_dust");
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ConfigureCommon(ps);

            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 2.0f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.032f);
            main.startColor = new Color(1.6f, 1.1f, 0.35f, 1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.14f);
            main.maxParticles = 80;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.04f;

            var emission = ps.emission;
            emission.rateOverTime = 22f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 36) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.09f;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
                1f,
                AnimationCurve.EaseInOut(0f, 0.4f, 1f, 0.15f));

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.92f, 0.55f), 0f),
                    new GradientColorKey(new Color(1f, 0.7f, 0.2f), 1f)
                },
                new[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = grad;

            ApplyCachedMaterial(ps, ref s_DustMaterial, new Color(1f, 0.85f, 0.3f, 0.75f));
            ps.Play(true);
            return ps;
        }

        public static ParticleSystem CreateShatterBurst(Transform parent)
        {
            var go = new GameObject("deconstruction_shatter");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            var ps = go.AddComponent<ParticleSystem>();
            ConfigureCommon(ps);

            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.22f, 0.45f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.006f, 0.018f);
            main.startColor = new Color(0.95f, 0.82f, 0.55f, 0.95f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.35f, 0.9f);
            main.maxParticles = 40;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0.35f;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 28) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.02f;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.7f, 0.2f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = grad;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.maxParticleSize = 0.05f;
            ApplyCachedMaterial(ps, ref s_ShatterMaterial, new Color(1f, 0.85f, 0.45f, 0.9f));
            ps.Play(true);
            Object.Destroy(go, 1.2f);
            return ps;
        }

        /// <summary>
        /// Lightweight mesh afterimage. Avoids cloning Rigidbody / RealityEditor / particles.
        /// </summary>
        public static GameObject CreateGhostTrailClone(Transform source, float lifetime)
        {
            var ghost = new GameObject("evade_ghost_trail");
            ghost.transform.SetPositionAndRotation(source.position, source.rotation);
            ghost.transform.localScale = source.lossyScale;

            var shader = GetUnlitShader();
            if (shader == null)
                return null;

            var ghostMat = new Material(shader);
            var color = new Color(0.35f, 0.75f, 1f, 0.32f);
            if (ghostMat.HasProperty("_BaseColor"))
                ghostMat.SetColor("_BaseColor", color);
            if (ghostMat.HasProperty("_Color"))
                ghostMat.SetColor("_Color", color);
            SetTransparent(ghostMat);

            var renderers = source.GetComponentsInChildren<Renderer>(false);
            var added = 0;
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null || !renderer.enabled)
                    continue;
                if (renderer is ParticleSystemRenderer)
                    continue;

                Mesh mesh = null;
                var mf = renderer.GetComponent<MeshFilter>();
                if (mf != null)
                    mesh = mf.sharedMesh;
                else if (renderer is SkinnedMeshRenderer skin && skin.sharedMesh != null)
                    mesh = skin.sharedMesh;
                if (mesh == null)
                    continue;

                var child = new GameObject(renderer.name);
                child.transform.SetParent(ghost.transform, false);
                child.transform.position = renderer.transform.position;
                child.transform.rotation = renderer.transform.rotation;
                child.transform.localScale = InverseLossyScale(ghost.transform, renderer.transform.lossyScale);

                child.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = child.AddComponent<MeshRenderer>();
                mr.sharedMaterial = ghostMat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                added++;
            }

            if (added == 0)
            {
                Object.Destroy(ghostMat);
                Object.Destroy(ghost);
                return null;
            }

            var fader = ghost.AddComponent<MagicMRGhostFader>();
            fader.Begin(lifetime, ghostMat);
            return ghost;
        }

        static Vector3 InverseLossyScale(Transform parent, Vector3 childLossy)
        {
            var ps = parent.lossyScale;
            return new Vector3(
                Mathf.Abs(ps.x) > 1e-5f ? childLossy.x / ps.x : childLossy.x,
                Mathf.Abs(ps.y) > 1e-5f ? childLossy.y / ps.y : childLossy.y,
                Mathf.Abs(ps.z) > 1e-5f ? childLossy.z / ps.z : childLossy.z);
        }

        static void SetTransparent(Material mat)
        {
            if (mat.HasProperty("_Surface"))
                mat.SetFloat("_Surface", 1f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = 3000;
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
        }

        static void ConfigureCommon(ParticleSystem ps)
        {
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        static void ApplyCachedMaterial(ParticleSystem ps, ref Material cached, Color color)
        {
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            if (cached == null)
            {
                var shader = GetParticleShader();
                if (shader == null)
                    return;
                cached = new Material(shader);
                if (cached.HasProperty("_BaseColor"))
                    cached.SetColor("_BaseColor", color);
                if (cached.HasProperty("_Color"))
                    cached.SetColor("_Color", color);
                SetTransparent(cached);
            }

            renderer.sharedMaterial = cached;
        }

        static Shader GetParticleShader()
        {
            if (s_ParticleShader != null)
                return s_ParticleShader;

            s_ParticleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                               ?? Shader.Find("Particles/Standard Unlit")
                               ?? GetUnlitShader();
            return s_ParticleShader;
        }

        static Shader GetUnlitShader()
        {
            if (s_UnlitShader != null)
                return s_UnlitShader;

            s_UnlitShader = Shader.Find("Universal Render Pipeline/Unlit")
                            ?? Shader.Find("Unlit/Color")
                            ?? Shader.Find("Sprites/Default");
            return s_UnlitShader;
        }
    }
}
