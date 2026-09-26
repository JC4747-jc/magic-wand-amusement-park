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
                var rb = EnsurePhysics(go);
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
                56,
                0.05f,
                2.2f,
                Tex("StarFlame") ?? Tex("FlameSheet") ?? Tex("GlowCircle"),
                0.62f);
            Object.Destroy(rain.gameObject, 5.5f);

            var origin = parent != null ? parent.TransformPoint(new Vector3(0f, 0.55f, 0f)) : Vector3.up * 0.4f;
            var candy = new[]
            {
                new Color(1f, 0.25f, 0.45f),
                new Color(0.25f, 0.85f, 1f),
                new Color(1f, 0.85f, 0.2f),
                new Color(0.55f, 1f, 0.35f),
                new Color(0.85f, 0.4f, 1f)
            };
            for (var i = 0; i < 14; i++)
            {
                var go = GameObject.CreatePrimitive(i % 2 == 0 ? PrimitiveType.Sphere : PrimitiveType.Cube);
                Object.Destroy(go.GetComponent<Collider>());
                go.name = "CandyDrop";
                go.transform.position = origin + new Vector3(Random.Range(-0.18f, 0.18f), Random.Range(0f, 0.08f), Random.Range(-0.12f, 0.12f));
                go.transform.localScale = Vector3.one * Random.Range(0.028f, 0.05f);
                go.GetComponent<Renderer>().sharedMaterial = HciMeetingVfx.LitGlow(candy[i % candy.Length], candy[i % candy.Length] * 1.4f);
                var rb = EnsurePhysics(go);
                rb.AddForce(Vector3.down * 0.4f + Random.insideUnitSphere * 0.25f, ForceMode.VelocityChange);
                Object.Destroy(go, 4.2f);
            }

            return rain;
        }

        public static void ImpactBurst(Vector3 worldPos)
        {
            PlayWorld("ExplosiveSmokeSmall", worldPos, 0.16f, 2.2f);
            var hit = HciMeetingVfx.Make(
                null,
                "Impact",
                new Color(1.7f, 0.85f, 0.2f, 1f),
                28,
                0.45f,
                0.045f,
                0.4f,
                false,
                Tex("GlowCircle") ?? Tex("Sparks"),
                true,
                0.15f,
                0.03f,
                false,
                0,
                worldPos);
            Object.Destroy(hit.gameObject, 1.5f);
        }

        public static ParticleSystem Fireflies(Transform parent, Vector3 localPos)
        {
            var ps = HciMeetingVfx.Make(
                parent,
                "Fireflies",
                new Color(1.7f, 1.35f, 0.35f, 1f),
                8,
                0.035f,
                0.016f,
                2.2f,
                true,
                Tex("GlowCircle"),
                true,
                -0.04f,
                0.2f,
                false,
                0);
            ps.transform.localPosition = localPos;
            return ps;
        }

        public static ParticleSystem Sakura(Transform parent)
        {
            var rain = HciMeetingVfx.Rain(
                parent,
                "Sakura",
                new Color(1.5f, 0.55f, 0.85f, 1f),
                32,
                0.038f,
                2.4f,
                Tex("StarFlame") ?? Tex("GlowCircle"),
                0.48f);
            var main = rain.main;
            main.gravityModifier = 0.22f;
            return rain;
        }

        public static ParticleSystem Snow(Transform parent)
        {
            var rain = HciMeetingVfx.Rain(
                parent,
                "Snow",
                new Color(0.92f, 0.96f, 1.2f, 1f),
                40,
                0.028f,
                3.2f,
                Tex("GlowCircle"),
                0.52f);
            var main = rain.main;
            main.gravityModifier = 0.12f;
            return rain;
        }

        public static ParticleSystem InkSplash(Vector3 worldPos)
        {
            PlayWorld("SmokeWhite", worldPos, 0.2f, 2.4f);
            var ink = HciMeetingVfx.Make(
                null,
                "Ink",
                new Color(0.05f, 0.05f, 0.08f, 0.95f),
                36,
                0.32f,
                0.07f,
                0.7f,
                false,
                Tex("SmokeNoise") ?? Tex("SmokeLoop"),
                false,
                0.05f,
                0.04f,
                false,
                0,
                worldPos);
            Object.Destroy(ink.gameObject, 2.2f);
            return ink;
        }

        public static GameObject AuraRing(Transform parent, Vector3 localPos, Color color)
        {
            var ring = HciMeetingVfx.Primitive(
                PrimitiveType.Cylinder,
                parent,
                "Aura",
                localPos,
                Quaternion.identity,
                new Vector3(0.32f, 0.006f, 0.32f),
                HciMeetingVfx.Unlit(new Color(color.r, color.g, color.b, 0.55f), true));
            var spark = HciMeetingVfx.Make(
                parent,
                "AuraSpark",
                color,
                22,
                0.05f,
                0.028f,
                0.9f,
                true,
                Tex("GlowCircle") ?? Tex("Sparks"),
                true,
                0f,
                0.16f,
                false,
                0);
            spark.transform.localPosition = localPos + Vector3.up * 0.02f;
            return ring;
        }

        public static GameObject CreateRewardFlower()
        {
            var root = new GameObject("HciRewardLotus");
            var stemMat = HciMeetingVfx.LitGlow(new Color(0.12f, 0.38f, 0.2f), new Color(0.05f, 0.4f, 0.18f));
            HciMeetingVfx.Primitive(
                PrimitiveType.Cylinder,
                root.transform,
                "Stem",
                new Vector3(0f, 0.11f, 0f),
                Quaternion.identity,
                new Vector3(0.018f, 0.11f, 0.018f),
                stemMat);

            var head = new GameObject("Head").transform;
            head.SetParent(root.transform, false);
            head.localPosition = new Vector3(0f, 0.24f, 0f);

            var inner = HciMeetingVfx.LitGlow(new Color(1f, 0.28f, 0.62f), new Color(2.2f, 0.25f, 1.1f));
            var mid = HciMeetingVfx.LitGlow(new Color(1f, 0.62f, 0.82f), new Color(1.6f, 0.4f, 0.9f));
            var outer = HciMeetingVfx.Unlit(new Color(1f, 0.92f, 1f, 0.72f), true);
            var gold = HciMeetingVfx.LitGlow(new Color(1f, 0.82f, 0.25f), new Color(2.4f, 1.4f, 0.2f));

            AddPetalRing(head, 8, 0.038f, -22f, new Vector3(0.055f, 0.016f, 0.032f), inner);
            AddPetalRing(head, 10, 0.062f, -34f, new Vector3(0.07f, 0.014f, 0.038f), mid);
            AddPetalRing(head, 12, 0.09f, -46f, new Vector3(0.08f, 0.012f, 0.042f), outer);

            HciMeetingVfx.Primitive(
                PrimitiveType.Sphere,
                head,
                "Center",
                Vector3.zero,
                Quaternion.identity,
                Vector3.one * 0.045f,
                gold);

            var cardTex = Tex("StarFlame") ?? Tex("GlowCircle");
            if (cardTex != null)
            {
                var cardMat = HciMeetingVfx.ParticleMat(new Color(1.5f, 0.7f, 1.3f, 1f), cardTex, true);
                for (var i = 0; i < 6; i++)
                {
                    var a = i / 6f * Mathf.PI * 2f;
                    var dir = new Vector3(Mathf.Sin(a), 0.15f, Mathf.Cos(a));
                    var rot = Quaternion.LookRotation(dir) * Quaternion.Euler(0f, 180f, 0f);
                    HciMeetingVfx.Primitive(
                        PrimitiveType.Quad,
                        head,
                        "HaloPetal" + i,
                        dir.normalized * 0.05f,
                        rot,
                        new Vector3(0.11f, 0.14f, 1f),
                        cardMat);
                }
            }

            return root;
        }

        static void AddPetalRing(Transform head, int count, float radius, float tilt, Vector3 scale, Material mat)
        {
            for (var i = 0; i < count; i++)
            {
                var yaw = i * (360f / count);
                var rot = Quaternion.Euler(tilt, yaw, 0f);
                var pos = rot * Vector3.forward * radius;
                HciMeetingVfx.Primitive(PrimitiveType.Sphere, head, "Petal" + count + "_" + i, pos, rot, scale, mat);
            }
        }

        public static void BloomReward(Transform flower, Vector3 worldPos)
        {
            PlayWorld("ExplosionSmall", worldPos, 0.24f, 2.8f);
            PlayWorld("MagicFirePurple", worldPos, 0.2f, 3.2f);

            var petals = HciMeetingVfx.Make(
                null,
                "BloomPetals",
                new Color(1.7f, 0.55f, 1.1f, 1f),
                72,
                0.55f,
                0.07f,
                1.35f,
                false,
                Tex("StarFlame") ?? Tex("GlowCircle"),
                true,
                0.18f,
                0.05f,
                false,
                0,
                worldPos);
            Object.Destroy(petals.gameObject, 2.8f);

            var gold = HciMeetingVfx.Make(
                null,
                "BloomGold",
                new Color(1.8f, 1.2f, 0.25f, 1f),
                56,
                0.42f,
                0.055f,
                1.1f,
                false,
                Tex("GlowCircle") ?? Tex("GlowPalet"),
                true,
                -0.12f,
                0.04f,
                true,
                0,
                worldPos);
            Object.Destroy(gold.gameObject, 2.4f);

            var ring = HciMeetingVfx.Make(
                null,
                "BloomRing",
                new Color(1.5f, 0.95f, 1.6f, 1f),
                28,
                0.08f,
                0.16f,
                0.85f,
                false,
                Tex("GlowCircle"),
                true,
                0f,
                0.02f,
                false,
                0,
                worldPos);
            var ringMain = ring.main;
            ringMain.startSpeed = 0.02f;
            ringMain.gravityModifier = 0f;
            Object.Destroy(ring.gameObject, 1.6f);

            if (flower != null)
            {
                var halo = HciMeetingVfx.Make(
                    flower,
                    "BloomHalo",
                    new Color(1.6f, 0.9f, 1.4f, 1f),
                    26,
                    0.06f,
                    0.045f,
                    1.2f,
                    true,
                    Tex("GlowCircle") ?? Tex("Sparks"),
                    true,
                    -0.02f,
                    0.06f,
                    false,
                    0);
                halo.transform.localPosition = new Vector3(0f, 0.22f, 0f);
            }

            var prefab = Resources.Load<GameObject>(Root + "GoldCoinMesh")
                         ?? Resources.Load<GameObject>(Root + "GoldCoin");
            var coinMat = CoinMaterial();
            for (var i = 0; i < 8; i++)
            {
                GameObject go;
                if (prefab != null)
                {
                    go = Object.Instantiate(prefab, worldPos + Vector3.up * 0.08f, Random.rotation);
                    RemapMeshesToUrp(go, coinMat);
                }
                else
                {
                    go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    Object.Destroy(go.GetComponent<Collider>());
                    go.GetComponent<Renderer>().sharedMaterial = coinMat;
                    go.transform.SetPositionAndRotation(worldPos, Random.rotation);
                }

                go.name = "BloomCoin";
                go.transform.localScale = Vector3.one * 0.028f;
                var rb = EnsurePhysics(go);
                rb.AddForce(Vector3.up * 1.1f + Random.insideUnitSphere * 0.45f, ForceMode.VelocityChange);
                rb.AddTorque(Random.insideUnitSphere * 8f, ForceMode.VelocityChange);
                Object.Destroy(go, 2.8f);
            }
        }

        static Rigidbody EnsurePhysics(GameObject go)
        {
            if (go.GetComponentInChildren<Collider>() == null)
                go.AddComponent<SphereCollider>();

            var rb = go.GetComponent<Rigidbody>();
            if (rb == null)
                rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.04f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            return rb;
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
