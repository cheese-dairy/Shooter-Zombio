using UnityEngine;

namespace Zombio
{
    /// <summary>Small self-cleaning primitive effects; collider-free and paused with gameplay.</summary>
    public sealed class ShotEffect : MonoBehaviour
    {
        float lifetime, elapsed;
        Vector3 startScale;
        LineRenderer line;
        Material ownedMaterial;

        public static void Tracer(Vector3 from, Vector3 to, Color color)
        {
            GameObject go = new GameObject("BulletTrail");
            ShotEffect effect = go.AddComponent<ShotEffect>();
            effect.lifetime = .065f;
            effect.line = go.AddComponent<LineRenderer>();
            effect.ownedMaterial = new Material(Shader.Find("Sprites/Default"));
            effect.line.sharedMaterial = effect.ownedMaterial;
            effect.line.positionCount = 2;
            effect.line.SetPosition(0, from);
            effect.line.SetPosition(1, to);
            effect.line.startWidth = .025f;
            effect.line.endWidth = .009f;
            effect.line.startColor = Color.Lerp(color, Color.white, .65f);
            effect.line.endColor = new Color(color.r, color.g, color.b, .2f);
            effect.line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            effect.line.receiveShadows = false;
        }

        public static void Impact(Vector3 position, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "ImpactSpark";
            Collider collider = go.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            go.transform.position = position;
            go.transform.localScale = Vector3.one * .13f;
            ShotEffect effect = go.AddComponent<ShotEffect>();
            effect.lifetime = .15f;
            effect.startScale = go.transform.localScale;
            effect.ownedMaterial = PlayerGeometry.Material(color, 1.2f);
            go.GetComponent<Renderer>().sharedMaterial = effect.ownedMaterial;
        }

        void Update()
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / lifetime);
            if (line != null) line.widthMultiplier = 1f - t;
            else transform.localScale = startScale * (1f + 2f * t);
            if (elapsed >= lifetime) Destroy(gameObject);
        }

        void OnDestroy() { if (ownedMaterial != null) Destroy(ownedMaterial); }
    }
}
