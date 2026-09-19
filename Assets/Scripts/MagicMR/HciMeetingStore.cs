using UnityEngine;

namespace MagicMR
{
    /// <summary>
    /// Asset Store War FX / Gold Coin / Boxes, remapped to URP so they actually show in MagicMR.
    /// One-shot blasts spawn in world space so they survive the lighter being hidden on D.
    /// </summary>
    public static class HciMeetingStore
    {
        const string Root = "HciStore/";

        public static Texture2D Tex(string name)
        {
            return Resources.Load<Texture2D>(Root + name);
        }

        public static GameObject Play(string resourceName, Transform parent, Vector3 localPos, float scale, float destroyAfter = -1f)
        {
            var prefab = Resources.Load<GameObject>(Root + resourceName);
            if (prefab == null)
                return null;

            var go = parent != null ? Object.Instantiate(prefab, parent) : Object.Instantiate(prefab);
            go.name = resourceName;
            if (parent != null)
            {
                go.transform.localPosition = localPos;
                go.transform.localRotation = Quaternion.identity;
            }
            else
            {
                go.transform.position = localPos;
                go.transform.rotation = Quaternion.identity;
            }

            go.transform.localScale = Vector3.one * Mathf.Max(0.01f, scale);

            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                main.playOnAwake = true;
                if (!main.loop && destroyAfter < 0f)
                    destroyAfter = Mathf.Max(destroyAfter, main.startLifetime.constantMax + 0.8f);
                ps.Play(true);
            }

            foreach (var light in go.GetComponentsInChildren<Light>(true))
                light.enabled = false;

            RemapMeshesToUrp(go);
            RemapParticlesToUrp(go, Tex("FlameLoop"), new Color(1.6f, 0.7f, 0.2f, 1f));

            if (destroyAfter > 0f)
                Object.Destroy(go, destroyAfter);

            return go;
        }

        public static GameObject PlayWorld(string resourceName, Vector3 worldPos, float scale, float destroyAfter)
        {
            return Play(resourceName, null, worldPos, scale, destroyAfter);
        }

        public static GameObject LoopFire(Transform parent, Vector3 localPos)
        {
            var fire = Play("FireSmall", parent, localPos, 0.22f);
            var overlay = HciMeetingVfx.Make(
                parent,
                "UrpFlame",
                new Color(1.8f, 0.55f, 0.12f, 1f),
                36,
                0.22f,
                0.07f,
                0.55f,
                true,
                Tex("FlameSheet") ?? Tex("FlameLoop"),
                true,
                -0.08f,
                0.02f,
                true,
                2);
            overlay.transform.localPosition = localPos;
            var glow = HciMeetingVfx.Make(
                parent,
                "UrpEmber",
                new Color(1.4f, 0.35f, 0.05f, 1f),
                18,
                0.08f,
                0.035f,
                0.9f,
                true,
                Tex("GlowCircle"),
                true,
                0.15f,
                0.03f,
                false,
                0);
            glow.transform.localPosition = localPos;
            return fire != null ? fire : overlay.gameObject;
        }

        public static void CoughSmoke(Vector3 worldPos)
        {
            PlayWorld("ExplosiveSmokeSmall", worldPos, 0.28f, 2.8f);
            var cough = HciMeetingVfx.Make(
                null,
                "UrpCough",
                new Color(0.55f, 0.52f, 0.48f, 0.85f),
                48,
                0.35f,
                0.09f,
                1.4f,
                false,
                Tex("SmokeLoop") ?? Tex("SmokeNoise"),
                false,
                0.05f,
                0.05f,
                false,
                0,
                worldPos);
            Object.Destroy(cough.gameObject, 2.4f);
        }

        public static void PurifyBlast(Vector3 worldPos)
        {
            PlayWorld("ExplosionSmall", worldPos, 0.32f, 3.2f);
            PlayWorld("MagicFirePurple", worldPos, 0.26f, 2.8f);
            var boom = HciMeetingVfx.Make(
                null,
                "UrpBoom",
                new Color(1.8f, 0.9f, 0.25f, 1f),
                56,
                0.7f,
                0.11f,
                0.7f,
                false,
                Tex("FlameBig") ?? Tex("StarFlame"),
                true,
                0.05f,
                0.06f,
                false,
                0,
                worldPos);
            var purify = HciMeetingVfx.Make(
                null,
                "UrpPurify",
                new Color(0.85f, 0.35f, 1.6f, 1f),
                40,
                0.45f,
                0.08f,
                0.9f,
                false,
                Tex("StarFlame") ?? Tex("GlowPalet"),
                true,
                -0.05f,
                0.05f,
                true,
                0,
                worldPos);
            Object.Destroy(boom.gameObject, 2.5f);
            Object.Destroy(purify.gameObject, 2.5f);
        }

        public static GameObject DarkFog(Transform parent)
        {
            var smoke = Play("SmokeWhite", parent, new Vector3(0f, 0.18f, 0f), 0.38f, 4.5f);
            var fog = HciMeetingVfx.Make(
                parent,
                "UrpFog",
                new Color(0.35f, 0.12f, 0.45f, 0.7f),
                28,
                0.08f,
                0.16f,
                2.2f,
                true,
                Tex("SmokeNoise") ?? Tex("SmokeLoop"),
                false,
                0.02f,
                0.12f,
                false,
                0);
            fog.transform.localPosition = new Vector3(0f, 0.12f, 0f);
            Object.Destroy(fog.gameObject, 5f);
            return smoke != null ? smoke : fog.gameObject;
        }

        public static GameObject ScarFire(Transform parent, Vector3 localPos)
        {
            var fire = Play("FireSmall", parent, localPos, 0.16f);
            var overlay = HciMeetingVfx.Make(
                parent,
                "ScarFlame",
                new Color(1.7f, 0.25f, 0.05f, 1f),
                28,
                0.16f,
                0.05f,
                0.45f,
                true,
                Tex("FlameSheet") ?? Tex("FlameLoop"),
                true,
                -0.05f,
                0.015f,
                true,
                2);
            overlay.transform.localPosition = localPos;
            return fire != null ? fire : overlay.gameObject;
        }

        public static GameObject ChestReveal(Transform parent, Vector3 localPos)
        {
            Play("ExplosiveSmokeSmall", parent, localPos + Vector3.up * 0.04f, 0.22f, 2.8f);
            HciMeetingVfx.Make(
                parent,
                "ChestPuff",
                new Color(1f, 0.85f, 0.4f, 0.9f),
                32,
                0.25f,
                0.06f,
                0.8f,
                false,
                Tex("SmokeLoop"),
                false,
                0.1f,
                0.04f,
                false,
                0).transform.localPosition = localPos;

            var chest = Play("SupplyBox", parent, localPos, 0.14f)
                        ?? Play("SupplyBoxMesh", parent, localPos, 0.14f);
            if (chest != null)
            {
                ApplyTexture(chest, Tex("BoxAlbedo"), new Color(0.92f, 0.72f, 0.28f));
                chest.transform.localScale = Vector3.one * 0.14f;
                return chest;
            }

            return HciMeetingVfx.Primitive(
                PrimitiveType.Cube,
                parent,
                "Chest",
                localPos,
                new Vector3(0.1f, 0.07f, 0.1f),
                new Color(1f, 0.82f, 0.2f));
        }

        public static void CoinExplosion(Vector3 worldPos)
        {
            var prefab = Resources.Load<GameObject>(Root + "GoldCoinMesh")
                         ?? Resources.Load<GameObject>(Root + "GoldCoin");
            var coinMat = CoinMaterial();
            var count = 18;
            for (var i = 0; i < count; i++)
            {
                GameObject go;
                if (prefab != null)
                {
                    go = Object.Instantiate(prefab, worldPos, Random.rotation);
                    RemapMeshesToUrp(go, coinMat);
                }
                else
                {
                    go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    Object.Destroy(go.GetComponent<Collider>());
                    go.GetComponent<Renderer>().sharedMaterial = coinMat;
                    go.transform.SetPositionAndRotation(worldPos, Random.rotation);
                }

                go.name = "HciGoldCoin";
                go.transform.localScale = Vector3.one * 0.04f;
                if (go.GetComponent<Collider>() == null)
                    go.AddComponent<SphereCollider>();

                var rb = go.GetComponent<Rigidbody>() ?? go.AddComponent<Rigidbody>();
                rb.mass = 0.05f;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                var dir = Vector3.up * 1.35f + Random.insideUnitSphere * 0.7f;
                rb.AddForce(dir, ForceMode.VelocityChange);
                rb.AddTorque(Random.insideUnitSphere * 10f, ForceMode.VelocityChange);
                Object.Destroy(go, 3.2f);
            }

            var spark = HciMeetingVfx.Make(
                null,
                "CoinSpark",
                new Color(1.6f, 1.2f, 0.3f, 1f),
                40,
                0.45f,
                0.04f,
                0.7f,
                false,
                Tex("GlowCircle") ?? Tex("Sparks"),
                true,
                0.2f,
                0.05f,
                false,
                0,
                worldPos);
            Object.Destroy(spark.gameObject, 2.2f);
        }

        public static ParticleSystem CandyRain(Transform parent)
        {
            Play("SmokeWhite", parent, new Vector3(0f, 0.2f, 0f), 0.18f, 2.5f);
            var rain = HciMeetingVfx.Rain(
                parent,
                "CandyRain",
                new Color(1.4f, 0.45f, 0.85f, 1f),
                42,
                0.045f,
                1.8f,
                Tex("StarFlame") ?? Tex("FlameSheet") ?? Tex("GlowCircle"),
                0.55f);
            Object.Destroy(rain.gameObject, 5.5f);
            return rain;
        }

        static void RemapParticlesToUrp(GameObject go, Texture fallbackTex, Color tint)
        {
            if (go == null)
                return;

            foreach (var renderer in go.GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                Texture tex = fallbackTex;
                var color = tint;
                var src = renderer.sharedMaterial;
                if (src != null)
                {
                    if (src.HasProperty("_MainTex") && src.GetTexture("_MainTex") != null)
                        tex = src.GetTexture("_MainTex");
                    else if (src.HasProperty("_BaseMap") && src.GetTexture("_BaseMap") != null)
                        tex = src.GetTexture("_BaseMap");
                    if (src.HasProperty("_TintColor"))
                        color = src.GetColor("_TintColor") * 2f;
                    else if (src.HasProperty("_Color"))
                        color = src.GetColor("_Color");
                }

                renderer.sharedMaterial = HciMeetingVfx.ParticleMat(color, tex, true);
                renderer.enabled = true;
                renderer.maxParticleSize = 3f;
            }
        }

        static void RemapMeshesToUrp(GameObject go, Material overrideMat = null)
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit")
                      ?? Shader.Find("Universal Render Pipeline/Unlit")
                      ?? Shader.Find("Standard");
            if (lit == null)
                return;

            foreach (var renderer in go.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is ParticleSystemRenderer)
                    continue;

                if (overrideMat != null)
                {
                    renderer.sharedMaterial = overrideMat;
                    continue;
                }

                var src = renderer.sharedMaterial;
                if (src != null && src.shader != null && src.shader.name.Contains("Universal Render Pipeline"))
                    continue;

                var mat = new Material(lit);
                Texture tex = null;
                var color = Color.white;
                if (src != null)
                {
                    if (src.HasProperty("_MainTex"))
                        tex = src.GetTexture("_MainTex");
                    if (tex == null && src.HasProperty("_BaseMap"))
                        tex = src.GetTexture("_BaseMap");
                    if (src.HasProperty("_Color"))
                        color = src.GetColor("_Color");
                    else if (src.HasProperty("_BaseColor"))
                        color = src.GetColor("_BaseColor");
                }

                if (tex != null)
                {
                    if (mat.HasProperty("_BaseMap"))
                        mat.SetTexture("_BaseMap", tex);
                    if (mat.HasProperty("_MainTex"))
                        mat.SetTexture("_MainTex", tex);
                }

                if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", color);
                if (mat.HasProperty("_Color"))
                    mat.SetColor("_Color", color);
                if (mat.HasProperty("_Smoothness"))
                    mat.SetFloat("_Smoothness", 0.72f);
                if (mat.HasProperty("_Metallic"))
                    mat.SetFloat("_Metallic", 0.85f);
                renderer.sharedMaterial = mat;
            }
        }

        static void ApplyTexture(GameObject go, Texture2D tex, Color fallback)
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Unlit");
            if (lit == null)
                return;

            var mat = new Material(lit);
            if (tex != null)
            {
                if (mat.HasProperty("_BaseMap"))
                    mat.SetTexture("_BaseMap", tex);
                if (mat.HasProperty("_MainTex"))
                    mat.SetTexture("_MainTex", tex);
            }

            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", fallback);
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", fallback);
            if (mat.HasProperty("_Smoothness"))
                mat.SetFloat("_Smoothness", 0.55f);
            if (mat.HasProperty("_Metallic"))
                mat.SetFloat("_Metallic", 0.35f);

            foreach (var renderer in go.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is ParticleSystemRenderer)
                    continue;
                renderer.sharedMaterial = mat;
            }
        }

        static Material CoinMaterial()
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Unlit");
            var mat = new Material(lit);
            var tex = Tex("CoinAlbedo");
            var gold = new Color(1f, 0.78f, 0.22f);
            if (tex != null)
            {
                if (mat.HasProperty("_BaseMap"))
                    mat.SetTexture("_BaseMap", tex);
                if (mat.HasProperty("_MainTex"))
                    mat.SetTexture("_MainTex", tex);
            }

            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", gold);
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", gold);
            if (mat.HasProperty("_Smoothness"))
                mat.SetFloat("_Smoothness", 0.85f);
            if (mat.HasProperty("_Metallic"))
                mat.SetFloat("_Metallic", 1f);
            return mat;
        }
    }
}
