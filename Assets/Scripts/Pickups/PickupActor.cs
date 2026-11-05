using UnityEngine;

namespace Zombio
{
    /// <summary>Proximity collection, deliberately independent of trigger/rigidbody setup.</summary>
    public sealed class PickupActor : MonoBehaviour
    {
        public PickupKind Kind { get; private set; }
        GameSession session;
        int weapon;
        Transform model;
        Material material;
        Vector3 basePosition;
        bool collected;
        float phase;
        public void Configure(GameSession owner, PickupKind kind, int weaponIndex)
        {
            session = owner; Kind = kind; weapon = weaponIndex;
            phase = transform.position.x * .7f + transform.position.z;
            basePosition = transform.position;
            var visual = GameObject.CreatePrimitive(kind == PickupKind.Weapon ? PrimitiveType.Cube : PrimitiveType.Cylinder);
            visual.name = "Supply icon";
            visual.transform.SetParent(transform, false);
            visual.transform.localScale = kind == PickupKind.Weapon ? new Vector3(.8f, .25f, .3f) : new Vector3(.42f, .16f, .42f);
            var collider = visual.GetComponent<Collider>();
            collider.enabled = false; Destroy(collider);
            model = visual.transform;
            material = new Material(Shader.Find("Standard"));
            material.color = Tint(kind);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", Tint(kind) * .5f);
            visual.GetComponent<Renderer>().sharedMaterial = material;
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = "Ground marker";
            marker.transform.SetParent(transform, false);
            marker.transform.localPosition = Vector3.up * .025f;
            marker.transform.localScale = new Vector3(.85f, .02f, .85f);
            var markerCollider = marker.GetComponent<Collider>();
            markerCollider.enabled = false; Destroy(markerCollider);
            marker.GetComponent<Renderer>().sharedMaterial = material;
        }
        void Update()
        {
            if (session == null || !session.IsPlaying || collected) return;
            model.localPosition = Vector3.up * (.8f + Mathf.Sin(Time.time * 2.8f + phase) * .12f);
            model.Rotate(Vector3.up, 70 * Time.deltaTime, Space.World);
            if ((session.Player.transform.position - basePosition).sqrMagnitude < 2.25f) Collect();
        }
        void Collect()
        {
            // A full-health survivor leaves medkits for later.
            if (Kind == PickupKind.Health && session.Player.Health >= session.Player.MaxHealth) return;
            collected = true;
            string message;
            switch (Kind)
            {
                case PickupKind.Health: session.Player.Heal(35); message = "MEDKIT  /  +35 health"; break;
                case PickupKind.Ammo: session.Weapons.AddAmmo(60); message = "SUPPLIES  /  +60 reserve rounds"; break;
                case PickupKind.Weapon:
                    session.Weapons.UnlockWeapon(weapon);
                    session.Weapons.AddAmmo(30);
                    message = session.Weapons.Definitions[weapon].Name + "  /  weapon supply";
                    break;
                case PickupKind.Damage: session.Weapons.AddDamageBoost(18); message = "OVERCHARGE  /  damage boost 18s"; break;
                case PickupKind.Speed: session.Player.AddSpeedBoost(18); message = "ADRENALINE  /  speed boost 18s"; break;
                case PickupKind.RapidFire: session.Weapons.AddRapidFireBoost(18); message = "HYPERDRIVE  /  rapid fire 18s"; break;
                default: session.AddScore(250); message = "SIGNAL CACHE  /  +250 score"; break;
            }
            session.Notify(message, Tint(Kind));
            session.PlaySound(SoundCue.Pickup, transform.position);
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
        public static Color Tint(PickupKind kind)
        {
            switch (kind)
            {
                case PickupKind.Health: return new Color(.35f, 1, .62f);
                case PickupKind.Ammo: return new Color(1, .72f, .24f);
                case PickupKind.Weapon: return new Color(.5f, .8f, 1);
                case PickupKind.Damage: return new Color(1, .3f, .35f);
                case PickupKind.Speed: return new Color(.2f, .95f, 1);
                case PickupKind.RapidFire: return new Color(.85f, .4f, 1);
                default: return new Color(1, .9f, .45f);
            }
        }
        void OnDestroy() { if (material != null) Destroy(material); }
    }
}
