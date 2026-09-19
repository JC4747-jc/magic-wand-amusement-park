using UnityEngine;
using UnityEngine.Rendering;

namespace MagicMR
{
    public static class HciMeetingVfx
    {
        public static Material Unlit(Color color, bool transparent = false)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", color);
            if (!transparent)
                return mat;

            if (mat.HasProperty("_Surface"))
                mat.SetFloat("_Surface", 1f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = 3000;
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            return mat;
        }

        public static Material ParticleMat(Color color, Texture texture, bool additive)
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                         ?? Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Sprites/Default");
            var mat = new Material(shader);
            if (mat.HasProperty("_Surface"))
                mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_Blend"))
                mat.SetFloat("_Blend", additive ? 2f : 0f);
            mat.SetInt("_SrcBlend", additive ? (int)BlendMode.One : (int)BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", additive ? (int)BlendMode.One : (int)BlendMode.OneMinusSrcAlpha);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = 3000;
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            if (additive)
                mat.EnableKeyword("_COLOROVERLAY_ON");
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", color);
            if (texture != null)
            {
                if (mat.HasProperty("_BaseMap"))
                    mat.SetTexture("_BaseMap", texture);
                if (mat.HasProperty("_MainTex"))
                    mat.SetTexture("_MainTex", texture);
            }

            return mat;
        }

        public static GameObject Primitive(PrimitiveType type, Transform parent, string name, Vector3 localPos, Vector3 localScale, Color color, bool transparent = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = localScale;
            Object.Destroy(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = Unlit(color, transparent);
            return go;
        }

        public static ParticleSystem Burst(
            Transform parent,
            string name,
            Color color,
            int count,
            float speed,
            float size,
            float life,
            bool loop,
            Texture texture = null)
        {
            return Make(
                parent,
                name,
                color,
                count,
                speed,
                size,
                life,
                loop,
                texture,
                additive: true,
                gravity: loop ? 0f : 0.2f,
                radius: 0.04f,
                cone: false,
                tiles: 0);
        }

        public static ParticleSystem Make(
            Transform parent,
            string name,
            Color color,
            int count,
            float speed,
            float size,
            float life,
            bool loop,
            Texture texture,
            bool additive,
            float gravity,
            float radius,
            bool cone,
            int tiles,
            Vector3? worldPosition = null)
        {
            var go = new GameObject(name);
            if (worldPosition.HasValue)
            {
                go.transform.position = worldPosition.Value;
            }
            else if (parent != null)
            {
                go.transform.SetParent(parent, false);
            }

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.7f, life);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.7f, size * 1.35f);
            main.startColor = color;
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
            main.maxParticles = Mathf.Max(count * 8, 64);
            main.loop = loop;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.gravityModifier = gravity;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

            var emission = ps.emission;
            emission.enabled = true;
            if (loop)
            {
                emission.rateOverTime = count;
            }
            else
            {
                emission.rateOverTime = 0f;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count, (short)(count * 2)) });
            }

            var shape = ps.shape;
            if (cone)
            {
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 18f;
                shape.radius = radius;
                shape.rotation = new Vector3(-90f, 0f, 0f);
            }
            else
            {
                shape.shapeType = ParticleSystemShapeType.Hemisphere;
                shape.radius = radius;
            }

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.35f, 1f, 1.1f));

            var colorOver = ps.colorOverLifetime;
            colorOver.enabled = true;
            colorOver.color = new ParticleSystem.MinMaxGradient(new Gradient
            {
                colorKeys = new[]
                {
                    new GradientColorKey(color, 0f),
                    new GradientColorKey(Color.Lerp(color, Color.white, 0.35f), 0.35f),
                    new GradientColorKey(color, 1f)
                },
                alphaKeys = new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(color.a, 0.15f),
                    new GradientAlphaKey(0f, 1f)
                }
            });

            var vol = ps.velocityOverLifetime;
            vol.enabled = cone;
            if (cone)
                vol.y = new ParticleSystem.MinMaxCurve(speed * 0.25f, speed * 0.6f);

            if (tiles > 1)
            {
                var sheet = ps.textureSheetAnimation;
                sheet.enabled = true;
                sheet.numTilesX = tiles;
                sheet.numTilesY = tiles;
                sheet.animation = ParticleSystemAnimationType.WholeSheet;
                sheet.cycleCount = loop ? 8 : 1;
            }

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.maxParticleSize = 2.5f;
            renderer.sharedMaterial = ParticleMat(color, texture, additive);
            ps.Play();
            return ps;
        }

        public static ParticleSystem Rain(
            Transform parent,
            string name,
            Color color,
            int rate,
            float size,
            float life,
            Texture texture,
            float width)
        {
            var ps = Make(parent, name, color, rate, 0.08f, size, life, true, texture, true, 0.85f, width * 0.5f, false, 0);
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(width, 0.02f, width);
            var main = ps.main;
            main.gravityModifier = 0.9f;
            main.startSpeed = 0.05f;
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.35f, 0.7f, 1f),
                new Color(1f, 0.9f, 0.25f, 1f));
            ps.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            return ps;
        }

        public static TextMesh Label(Transform parent, string name, string text, Vector3 localPos, Color color, float characterSize = 0.012f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var mesh = go.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.fontSize = 42;
            mesh.characterSize = characterSize;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = color;
            return mesh;
        }
    }
}
