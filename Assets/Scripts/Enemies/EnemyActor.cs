using UnityEngine;

namespace Zombio
{
    /// <summary>One collision-aware actor; behaviour is selected by EnemyKind, visuals are replaceable children.</summary>
    public sealed class EnemyActor : MonoBehaviour
    {
        public EnemyKind Kind { get; private set; }
        public float Health { get; private set; }
        public float MaxHealth { get; private set; }
        public bool IsDead { get; private set; }

        private enum Attack { None, Melee, Spit, Explode, Slam, Volley }
        private GameSession _session;
        private WaveDirector _director;
        private CharacterController _controller;
        private Transform _model, _leftArm, _rightArm, _telegraph;
        private Renderer _bodyRenderer;
        private MaterialPropertyBlock _tint;
        private Color _color;
        private float _speed, _damage, _reach, _cooldown, _height;
        private float _nextAttack, _nextPath, _nextSpecial, _gravity, _hitFlash;
        private Vector3 _waypoint, _push;
        private Attack _attack;
        private float _resolveAt;
        private bool _configured;

        public void Configure(GameSession session, WaveDirector director, EnemyKind kind, float healthScale, float damageScale)
        {
            _session = session;
            _director = director;
            Kind = kind;
            _height = 1.85f;
            float radius = 0.37f;
            _reach = 1.7f;
            _cooldown = 1.35f;
            switch (kind)
            {
                case EnemyKind.Runner:
                    MaxHealth = 45f; _speed = 5.2f; _damage = 9f;
                    _color = new Color(0.93f, 0.51f, 0.22f); _height = 1.65f; radius = 0.31f; _cooldown = 0.95f;
                    break;
                case EnemyKind.Brute:
                    MaxHealth = 260f; _speed = 1.65f; _damage = 27f;
                    _color = new Color(0.56f, 0.44f, 0.72f); _height = 2.75f; radius = 0.62f; _reach = 2.45f; _cooldown = 2.5f;
                    break;
                case EnemyKind.Spitter:
                    MaxHealth = 85f; _speed = 2.6f; _damage = 13f;
                    _color = new Color(0.57f, 0.88f, 0.21f); _cooldown = 2.8f;
                    break;
                case EnemyKind.Exploder:
                    MaxHealth = 75f; _speed = 3.5f; _damage = 36f;
                    _color = new Color(1f, 0.29f, 0.23f); _height = 1.9f; radius = 0.43f;
                    break;
                case EnemyKind.Boss:
                    MaxHealth = 1250f; _speed = 2.1f; _damage = 25f;
                    _color = new Color(0.92f, 0.25f, 0.44f); _height = 3.8f; radius = 0.72f; _reach = 3f; _cooldown = 1.9f;
                    break;
                default:
                    MaxHealth = 70f; _speed = 2.3f; _damage = 12f;
                    _color = new Color(0.29f, 0.64f, 0.56f);
                    break;
            }
            MaxHealth *= Mathf.Max(1f, healthScale);
            Health = MaxHealth;
            _damage *= Mathf.Max(1f, damageScale);
            _controller = gameObject.AddComponent<CharacterController>();
            _controller.height = _height;
            _controller.radius = radius;
            _controller.center = Vector3.up * (_height * 0.5f);
            _controller.stepOffset = 0.3f;
            _controller.slopeLimit = 45f;
            _controller.skinWidth = 0.04f;
            BuildVisuals(radius);
            _waypoint = transform.position;
            _nextPath = Time.time + Random.Range(0f, 0.3f);
            _nextAttack = Time.time + 0.6f;
            _nextSpecial = Time.time + 3f;
            _configured = true;
        }

        private void BuildVisuals(float radius)
        {
            _model = new GameObject("Replaceable zombie model").transform;
            _model.SetParent(transform, false);
            Transform body = EnemyVisuals.Primitive(PrimitiveType.Capsule, _model, "Torso", new Vector3(0f, _height * 0.49f, 0f), new Vector3(radius * 1.75f, _height * 0.35f, radius * 1.4f), _color);
            _bodyRenderer = body.GetComponent<Renderer>();
            _tint = new MaterialPropertyBlock();
            EnemyVisuals.Primitive(PrimitiveType.Cube, _model, "Head", new Vector3(0f, _height * 0.91f, 0.06f), new Vector3(radius * 1.45f, _height * 0.22f, radius * 1.4f), _color * 0.85f);
            Color eye = Kind == EnemyKind.Boss ? new Color(1f, 0.77f, 0.32f) : new Color(0.9f, 1f, 0.62f);
            EnemyVisuals.Primitive(PrimitiveType.Cube, _model, "Luminous visor", new Vector3(0f, _height * 0.94f, radius * 0.78f), new Vector3(radius * 1.23f, _height * 0.035f, 0.055f), eye, true);
            _leftArm = EnemyVisuals.Primitive(PrimitiveType.Cube, _model, "Left arm", new Vector3(-radius * 1.2f, _height * 0.57f, 0.12f), new Vector3(radius * 0.56f, _height * 0.38f, radius * 0.62f), _color * 0.72f);
            _rightArm = EnemyVisuals.Primitive(PrimitiveType.Cube, _model, "Right arm", new Vector3(radius * 1.2f, _height * 0.57f, 0.12f), _leftArm.localScale, _color * 0.72f);
            if (Kind == EnemyKind.Brute || Kind == EnemyKind.Boss)
            {
                Color armor = new Color(0.13f, 0.18f, 0.24f);
                EnemyVisuals.Primitive(PrimitiveType.Cube, _model, "Chest armor", new Vector3(0f, _height * 0.61f, radius * 0.7f), new Vector3(radius * 1.65f, _height * 0.22f, 0.18f), armor);
                EnemyVisuals.Primitive(PrimitiveType.Cube, _model, "Left pauldron", new Vector3(-radius * 1.1f, _height * 0.79f, 0f), new Vector3(radius, 0.3f, radius), armor);
                EnemyVisuals.Primitive(PrimitiveType.Cube, _model, "Right pauldron", new Vector3(radius * 1.1f, _height * 0.79f, 0f), new Vector3(radius, 0.3f, radius), armor);
            }
            if (Kind == EnemyKind.Spitter || Kind == EnemyKind.Exploder)
                EnemyVisuals.Primitive(PrimitiveType.Sphere, _model, "Volatile sac", new Vector3(0f, _height * 0.6f, -0.32f), Vector3.one * 0.75f, _color, true);
            _telegraph = EnemyVisuals.Primitive(PrimitiveType.Cylinder, transform, "Attack danger zone", new Vector3(0f, 0.04f, 0f), new Vector3(2f, 0.012f, 2f), new Color(0.93f, 0.23f, 0.12f), true);
            _telegraph.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!_configured || IsDead || !_session.IsPlaying || _session.Player.IsDead) return;
            float dt = Time.deltaTime;
            Vector3 target = _session.Player.transform.position;
            Vector3 toPlayer = target - transform.position;
            toPlayer.y = 0f;
            float distance = toPlayer.magnitude;
            if (distance > 0.02f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(toPlayer), dt * (Kind == EnemyKind.Brute ? 4f : 8f));

            _hitFlash = Mathf.Max(0f, _hitFlash - dt * 5f);
            _tint.SetColor("_Color", Color.Lerp(_color, Color.white, _hitFlash));
            _bodyRenderer.SetPropertyBlock(_tint);

            if (_attack != Attack.None)
            {
                float pulse = 1f + Mathf.Sin(Time.time * 30f) * 0.025f;
                _model.localScale = Vector3.one * pulse;
                _leftArm.localRotation = Quaternion.Euler(-95f, 0f, -15f);
                _rightArm.localRotation = Quaternion.Euler(-95f, 0f, 15f);
                if (Time.time >= _resolveAt) ResolveAttack();
                ApplyMovement(Vector3.zero, dt);
                return;
            }

            _model.localScale = Vector3.one;
            Vector3 motion = Vector3.zero;
            if (distance < 140f)
            {
                Vector3 waypointDelta = _waypoint - transform.position;
                waypointDelta.y = 0f;
                if (Time.time >= _nextPath || waypointDelta.sqrMagnitude < 0.16f)
                {
                    Vector3 desired = target;
                    if (Kind == EnemyKind.Spitter && distance < 10f)
                    {
                        Vector3 retreat = transform.position - toPlayer.normalized * 6f;
                        if (_session.World.IsWalkable(retreat)) desired = retreat;
                        else
                        {
                            Vector3 side = transform.position + Vector3.Cross(Vector3.up, toPlayer.normalized) * 5f;
                            if (_session.World.IsWalkable(side)) desired = side;
                        }
                    }
                    _waypoint = _session.World.GetWaypoint(transform.position, desired);
                    _nextPath = Time.time + 0.4f + Random.Range(0f, 0.15f);
                }
                Vector3 path = _waypoint - transform.position;
                path.y = 0f;
                bool shouldMove = Kind == EnemyKind.Spitter || distance > _reach * 0.85f;
                if (Kind == EnemyKind.Spitter && distance >= 10f && distance <= 16f && HasLineOfSight()) shouldMove = false;
                if (shouldMove && path.sqrMagnitude > 0.08f)
                    motion = (path.normalized + _director.Separation(this) * 0.75f).normalized * Mathf.Min(_speed, path.magnitude / Mathf.Max(dt, 0.001f));
                TryBeginAttack(distance);
            }
            float gait = Mathf.Sin(Time.time * _speed * 3f + GetInstanceID()) * 22f;
            _leftArm.localRotation = Quaternion.Euler(-25f + gait, 0f, -8f);
            _rightArm.localRotation = Quaternion.Euler(-25f - gait, 0f, 8f);
            _model.localPosition = Vector3.up * Mathf.Abs(Mathf.Sin(Time.time * _speed * 2.5f)) * (motion.sqrMagnitude > 0.1f ? 0.045f : 0f);
            ApplyMovement(motion, dt);
        }

        private void ApplyMovement(Vector3 horizontal, float dt)
        {
            if (IsDead || !_controller.enabled) return;
            _gravity = _controller.isGrounded && _gravity < 0f ? -2f : Mathf.Max(-35f, _gravity - 24f * dt);
            _controller.Move((horizontal + _push + Vector3.up * _gravity) * dt);
            _push = Vector3.Lerp(_push, Vector3.zero, dt * 8f);
        }

        private void TryBeginAttack(float distance)
        {
            if (Time.time < _nextAttack) return;
            if (Kind == EnemyKind.Exploder && distance < 3.2f)
                BeginAttack(Attack.Explode, 1.05f, 4.5f);
            else if (Kind == EnemyKind.Boss && Time.time >= _nextSpecial && distance < 25f)
            {
                BeginAttack(distance < 8.5f ? Attack.Slam : Attack.Volley, distance < 8.5f ? 1.45f : 0.95f, distance < 8.5f ? 8.5f : 1.8f);
                _nextSpecial = Time.time + 7.5f;
            }
            else if (Kind == EnemyKind.Spitter && distance <= 23f && HasLineOfSight())
                BeginAttack(Attack.Spit, 0.7f, 0.7f);
            else if (Kind != EnemyKind.Spitter && Kind != EnemyKind.Exploder && distance <= _reach && HasLineOfSight())
                BeginAttack(Attack.Melee, Kind == EnemyKind.Brute ? 0.95f : 0.42f, _reach);
        }

        private void BeginAttack(Attack attack, float delay, float radius)
        {
            _attack = attack;
            _resolveAt = Time.time + delay;
            _nextAttack = _resolveAt + _cooldown;
            _telegraph.localScale = new Vector3(radius * 2f, 0.012f, radius * 2f);
            _telegraph.gameObject.SetActive(true);
        }

        private void ResolveAttack()
        {
            Attack attack = _attack;
            _attack = Attack.None;
            _telegraph.gameObject.SetActive(false);
            float distance = Vector3.Distance(transform.position, _session.Player.transform.position);
            switch (attack)
            {
                case Attack.Melee:
                    if (distance < _reach + 0.3f && HasLineOfSight()) _session.Player.TakeDamage(_damage);
                    break;
                case Attack.Spit:
                    ShootAcid(0f, 13f);
                    break;
                case Attack.Volley:
                    ShootAcid(-10f, 16f); ShootAcid(0f, 16f); ShootAcid(10f, 16f);
                    break;
                case Attack.Explode:
                    Blast(4.5f, _damage);
                    Die(false);
                    break;
                case Attack.Slam:
                    Blast(8.5f, _damage * 1.25f);
                    break;
            }
        }

        private void ShootAcid(float angle, float speed)
        {
            Vector3 start = transform.position + Vector3.up * (_height * 0.74f) + transform.forward * 0.9f;
            Vector3 direction = (_session.Player.transform.position + Vector3.up * 0.9f - start).normalized;
            direction = Quaternion.AngleAxis(angle, Vector3.up) * direction;
            GameObject projectile = new GameObject("Acid projectile");
            projectile.transform.SetParent(_director.RuntimeRoot, false);
            projectile.transform.position = start;
            projectile.AddComponent<AcidProjectile>().Configure(_session, this, direction, speed, _damage, _color);
        }

        private void Blast(float radius, float damage)
        {
            float distance = Vector3.Distance(transform.position, _session.Player.transform.position);
            if (distance < radius && HasLineOfSight())
                _session.Player.TakeDamage(damage * Mathf.Lerp(1f, 0.55f, distance / radius));
            EnemyVisuals.Burst(transform.position + Vector3.up * 0.5f, _color, radius, _director.RuntimeRoot);
            _session.PlaySound(SoundCue.Explosion, transform.position);
            if (distance < 25f) _session.ViewCamera.Shake(0.5f * (1f - distance / 25f));
        }

        private bool HasLineOfSight()
        {
            Vector3 origin = transform.position + Vector3.up * (_height * 0.65f);
            Vector3 delta = _session.Player.transform.position + Vector3.up * 0.9f - origin;
            RaycastHit[] hits = Physics.RaycastAll(origin, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore);
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.transform.IsChildOf(transform)) continue;
                if (hit.collider.GetComponentInParent<PlayerController>() != null) continue;
                if (hit.collider.GetComponentInParent<EnemyActor>() != null) continue;
                return false;
            }
            return true;
        }

        public void TakeDamage(float damage, Vector3 hitPoint)
        {
            if (!_configured || IsDead || damage <= 0f || !_session.IsPlaying) return;
            Health = Mathf.Max(0f, Health - damage);
            _hitFlash = 1f;
            Vector3 delta = transform.position - hitPoint;
            delta.y = 0f;
            _push = delta.normalized * (Kind == EnemyKind.Boss || Kind == EnemyKind.Brute ? 0.3f : 1.8f);
            if (Health <= 0f) Die(true);
        }

        private void Die(bool awardKill)
        {
            if (IsDead) return;
            IsDead = true;
            Health = 0f;
            _controller.enabled = false;
            _telegraph.gameObject.SetActive(false);
            _director.OnEnemyDied(this);
            if (awardKill) _session.EnemyKilled(this, transform.position);
            EnemyVisuals.Burst(transform.position + Vector3.up * 0.7f, _color, Kind == EnemyKind.Boss ? 3f : 0.8f, _director.RuntimeRoot);
            Destroy(gameObject);
        }
    }
}
