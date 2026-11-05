using UnityEngine;

namespace Zombio
{
    /// <summary>Swept collision avoids tunnelling; no Rigidbody or prefab setup is needed.</summary>
    public sealed class AcidProjectile : MonoBehaviour
    {
        private GameSession _session;
        private EnemyActor _owner;
        private Vector3 _velocity;
        private float _damage;
        private float _life = 5f;
        private Color _color;

        public void Configure(GameSession session, EnemyActor owner, Vector3 direction, float speed, float damage, Color color)
        {
            _session = session; _owner = owner; _velocity = direction * speed; _damage = damage; _color = color;
            EnemyVisuals.Primitive(PrimitiveType.Sphere, transform, "Acid core", Vector3.zero, Vector3.one * 0.3f, color, true);
            TrailRenderer trail = gameObject.AddComponent<TrailRenderer>();
            trail.time = 0.22f; trail.startWidth = 0.24f; trail.endWidth = 0.015f;
            trail.material = EnemyVisuals.Material(color, true);
            trail.startColor = color; trail.endColor = new Color(color.r, color.g, color.b, 0f);
        }

        private void Update()
        {
            if (_session == null || !_session.IsPlaying) return;
            _life -= Time.deltaTime;
            if (_life <= 0f) { Destroy(gameObject); return; }
            Vector3 step = _velocity * Time.deltaTime;
            RaycastHit[] hits = Physics.SphereCastAll(transform.position, 0.15f, step.normalized, step.magnitude, ~0, QueryTriggerInteraction.Ignore);
            float closest = float.PositiveInfinity;
            RaycastHit selected = new RaycastHit();
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.transform.IsChildOf(transform)) continue;
                if (_owner != null && hit.collider.transform.IsChildOf(_owner.transform)) continue;
                if (hit.distance < closest) { closest = hit.distance; selected = hit; }
            }
            if (closest < float.PositiveInfinity)
            {
                PlayerController player = selected.collider.GetComponentInParent<PlayerController>();
                if (player != null && !player.IsDead) player.TakeDamage(_damage);
                EnemyVisuals.Burst(selected.point, _color, 0.55f, transform.parent);
                Destroy(gameObject);
                return;
            }
            transform.position += step;
        }
    }
}
