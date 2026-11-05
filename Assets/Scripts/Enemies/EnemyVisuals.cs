using System.Collections.Generic;
using UnityEngine;

namespace Zombio
{
    internal static class EnemyVisuals
    {
        private static readonly Dictionary<int, Material> Materials = new Dictionary<int, Material>();

        internal static Material Material(Color color, bool glow = false)
        {
            int key = color.GetHashCode() ^ (glow ? 1357 : 0);
            if (Materials.TryGetValue(key, out Material material) && material != null) return material;
            Shader shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            material = new Material(shader);
            material.color = color;
            material.SetFloat("_Glossiness", 0.25f);
            if (glow)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 1.3f);
            }
            Materials[key] = material;
            return material;
        }

        internal static Transform Primitive(PrimitiveType type, Transform parent, string name, Vector3 position, Vector3 scale, Color color, bool glow = false)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            Collider collider = go.GetComponent<Collider>();
            collider.enabled = false;
            Object.Destroy(collider);
            go.GetComponent<Renderer>().sharedMaterial = Material(color, glow);
            return go.transform;
        }

        internal static void Burst(Vector3 position, Color color, float radius, Transform parent)
        {
            GameObject go = new GameObject("Bio impact");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.AddComponent<EnemyBurst>().Configure(color, radius);
        }
    }
}
