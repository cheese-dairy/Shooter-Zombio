using UnityEngine;

namespace Zombio
{
    /// <summary>Feet-origin character controller. No animation assets or input package required.</summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : MonoBehaviour
    {
        [Header("Survival")]
        public float MaxHealth = 100f;
        public float MaxStamina = 100f;
        [Header("Movement")]
        public float WalkSpeed = 2.4f;
        public float RunSpeed = 4.9f;
        public float SprintSpeed = 8f;
        public float JumpHeight = 1.4f;
        public float Gravity = 25f;
        public float Acceleration = 14f;
        public float TurnSpeed = 15f;
        public float DodgeCost = 25f;
        public float SlideCost = 16f;

        public float Health { get; private set; }
        public float Stamina { get; private set; }
        public bool IsDead => Health <= 0f;
        public bool IsAiming { get; private set; }
        public bool IsSprinting { get; private set; }
        public bool IsCrouching { get; private set; }
        public bool IsSliding => slideRemaining > 0f;
        public bool IsDodging => dodgeRemaining > 0f;
        public Vector3 Velocity { get; private set; }
        public float SpeedBoostRemaining => Mathf.Max(0f, speedBoostUntil - Time.time);

        GameSession session;
        ThirdPersonCamera view;
        CharacterController controller;
        Transform visuals, leftLeg, rightLeg, leftArm;
        Vector3 planarVelocity, actionDirection;
        float verticalVelocity, slideRemaining, dodgeRemaining, actionCooldown;
        float speedBoostUntil, lastGrounded = -10f, jumpPressed = -10f, staminaDelay;
        float hurtCooldown, stride;
        const float StandingHeight = 1.9f;
        const float CrouchHeight = 1.15f;

        public void Configure(GameSession game, ThirdPersonCamera cameraRig)
        {
            session = game;
            view = cameraRig;
            controller = GetComponent<CharacterController>();
            controller.height = StandingHeight;
            controller.radius = .34f;
            controller.center = Vector3.up * StandingHeight * .5f;
            controller.stepOffset = .3f;
            controller.slopeLimit = 48f;
            controller.skinWidth = .035f;
            visuals = transform.Find("Visuals");
            if (visuals == null) BuildVisuals();
            ResetState();
        }

        public void ResetState()
        {
            Health = MaxHealth;
            Stamina = MaxStamina;
            planarVelocity = Velocity = Vector3.zero;
            verticalVelocity = 0f;
            slideRemaining = dodgeRemaining = actionCooldown = 0f;
            speedBoostUntil = staminaDelay = hurtCooldown = 0f;
            IsCrouching = IsSprinting = IsAiming = false;
            lastGrounded = jumpPressed = -10f;
            stride = 0f;
            if (visuals != null) { visuals.localPosition = Vector3.zero; visuals.localRotation = Quaternion.identity; }
            if (controller != null)
            {
                controller.height = StandingHeight;
                controller.center = Vector3.up * StandingHeight * .5f;
            }
        }

        void Update()
        {
            if (session == null || !session.IsPlaying || IsDead) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            actionCooldown = Mathf.Max(0f, actionCooldown - dt);
            hurtCooldown = Mathf.Max(0f, hurtCooldown - dt);
            IsAiming = Input.GetMouseButton(1);
            Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            input = Vector2.ClampMagnitude(input, 1f);
            Quaternion heading = Quaternion.Euler(0f, view != null ? view.Yaw : transform.eulerAngles.y, 0f);
            Vector3 desiredDirection = heading * new Vector3(input.x, 0f, input.y);
            bool grounded = controller.isGrounded;
            if (grounded) lastGrounded = Time.time;
            if (Input.GetKeyDown(KeyCode.Space)) jumpPressed = Time.time;

            bool crouchRequested = Input.GetKey(KeyCode.LeftControl);
            IsCrouching = crouchRequested || IsSliding || !CanStand();
            IsSprinting = input.sqrMagnitude > .05f && Input.GetKey(KeyCode.LeftShift) &&
                !IsAiming && !IsCrouching && !IsSliding && !IsDodging && Stamina > 1f;

            if (Input.GetKeyDown(KeyCode.C) && grounded && IsSprinting && Stamina >= SlideCost && actionCooldown <= 0f)
            {
                slideRemaining = .75f;
                actionDirection = desiredDirection.normalized;
                Stamina -= SlideCost;
                staminaDelay = Time.time + .7f;
                IsCrouching = true;
                IsSprinting = false;
            }
            if (Input.GetKeyDown(KeyCode.Q) && grounded && !IsSliding && !IsDodging && Stamina >= DodgeCost && actionCooldown <= 0f)
            {
                dodgeRemaining = .42f;
                actionDirection = desiredDirection.sqrMagnitude > .01f ? desiredDirection.normalized : transform.forward;
                Stamina -= DodgeCost;
                staminaDelay = Time.time + .8f;
                IsSprinting = false;
            }

            float targetHeight = IsCrouching || IsDodging ? CrouchHeight : StandingHeight;
            controller.height = Mathf.MoveTowards(controller.height, targetHeight, dt * 7f);
            controller.center = Vector3.up * controller.height * .5f;

            if (IsSliding)
            {
                planarVelocity = actionDirection * Mathf.Lerp(4f, 11f, slideRemaining / .75f);
                slideRemaining -= dt;
                if (slideRemaining <= 0f) actionCooldown = .3f;
            }
            else if (IsDodging)
            {
                planarVelocity = actionDirection * 10.5f;
                dodgeRemaining -= dt;
                if (dodgeRemaining <= 0f) actionCooldown = .35f;
            }
            else
            {
                float speed = IsSprinting ? SprintSpeed : (IsCrouching || IsAiming ? WalkSpeed : RunSpeed);
                if (Input.GetKey(KeyCode.LeftAlt)) speed = Mathf.Min(speed, WalkSpeed);
                if (SpeedBoostRemaining > 0f) speed *= 1.3f;
                float responsiveness = input.sqrMagnitude > .01f ? Acceleration : Acceleration * 1.35f;
                planarVelocity = Vector3.Lerp(planarVelocity, desiredDirection * speed, 1f - Mathf.Exp(-responsiveness * dt));
            }

            if (IsSprinting)
            {
                Stamina = Mathf.Max(0f, Stamina - 22f * dt);
                staminaDelay = Time.time + .6f;
            }
            else if (Time.time > staminaDelay && !IsDodging && !IsSliding)
                Stamina = Mathf.Min(MaxStamina, Stamina + 20f * dt);

            if (grounded && verticalVelocity < 0f) verticalVelocity = -2f;
            if (Time.time - jumpPressed < .12f && Time.time - lastGrounded < .12f && !IsCrouching && !IsDodging)
            {
                verticalVelocity = Mathf.Sqrt(2f * Gravity * JumpHeight);
                jumpPressed = lastGrounded = -10f;
            }
            verticalVelocity = Mathf.Max(-45f, verticalVelocity - Gravity * dt);
            Vector3 motion = planarVelocity + Vector3.up * verticalVelocity;
            CollisionFlags flags = controller.Move(motion * dt);
            if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0f) verticalVelocity = 0f;
            Velocity = controller.velocity;

            Vector3 facing = IsAiming || Input.GetMouseButton(0) ? heading * Vector3.forward : planarVelocity;
            facing.y = 0f;
            if (facing.sqrMagnitude > .06f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(facing), 1f - Mathf.Exp(-TurnSpeed * dt));
            AnimateModel(dt);
            if (transform.position.y < -12f) TakeDamage(MaxHealth * 2f);
        }

        bool CanStand()
        {
            if (controller == null || controller.height >= StandingHeight - .01f) return true;
            Vector3 point = transform.position + Vector3.up * (StandingHeight - controller.radius);
            Collider[] overlaps = Physics.OverlapSphere(point, controller.radius * .94f, ~0, QueryTriggerInteraction.Ignore);
            foreach (Collider overlap in overlaps)
                if (!overlap.transform.IsChildOf(transform)) return false;
            return true;
        }

        public void TakeDamage(float amount)
        {
            if (session == null || !session.IsPlaying || IsDead || amount <= 0f) return;
            // Dodge invulnerability covers only the opening portion; attacks still matter on recovery.
            if (dodgeRemaining > .18f) return;
            Health = Mathf.Max(0f, Health - amount);
            session.DamageFeedback(amount);
            if (hurtCooldown <= 0f)
            {
                session.PlaySound(SoundCue.Hurt, transform.position);
                hurtCooldown = .18f;
            }
            if (view != null) view.Shake(.12f + amount * .002f);
            if (IsDead) session.PlayerDied();
        }

        public void Heal(float amount) { Health = Mathf.Min(MaxHealth, Health + Mathf.Max(0f, amount)); }
        public void AddSpeedBoost(float seconds) { speedBoostUntil = Mathf.Max(Time.time, speedBoostUntil) + Mathf.Max(0f, seconds); }

        void BuildVisuals()
        {
            visuals = new GameObject("Visuals").transform;
            visuals.SetParent(transform, false);
            Material coat = PlayerGeometry.Material(new Color(.08f, .28f, .3f));
            Material dark = PlayerGeometry.Material(new Color(.06f, .09f, .12f));
            Material trim = PlayerGeometry.Material(new Color(1f, .56f, .24f));
            Material visor = PlayerGeometry.Material(new Color(.46f, .96f, .87f), .8f);
            PlayerGeometry.Part("Torso", visuals, PrimitiveType.Capsule, new Vector3(0f, 1.08f, 0f), new Vector3(.56f, .43f, .36f), coat);
            PlayerGeometry.Part("Vest", visuals, PrimitiveType.Cube, new Vector3(0f, 1.12f, .18f), new Vector3(.45f, .46f, .14f), dark);
            PlayerGeometry.Part("Helmet", visuals, PrimitiveType.Sphere, new Vector3(0f, 1.72f, 0f), new Vector3(.47f, .45f, .43f), dark);
            PlayerGeometry.Part("Visor", visuals, PrimitiveType.Cube, new Vector3(0f, 1.75f, .2f), new Vector3(.35f, .13f, .07f), visor);
            PlayerGeometry.Part("Backpack", visuals, PrimitiveType.Cube, new Vector3(0f, 1.14f, -.25f), new Vector3(.43f, .5f, .24f), dark);
            PlayerGeometry.Part("Beacon", visuals, PrimitiveType.Cube, new Vector3(-.19f, 1.38f, -.27f), new Vector3(.06f, .18f, .04f), trim);
            leftLeg = Leg("LeftLeg", -.17f, dark);
            rightLeg = Leg("RightLeg", .17f, dark);
            leftArm = PlayerGeometry.Part("LeftArm", visuals, PrimitiveType.Capsule, new Vector3(-.37f, 1.12f, .03f), new Vector3(.19f, .31f, .2f), coat);
            PlayerGeometry.Part("RightShoulder", visuals, PrimitiveType.Sphere, new Vector3(.34f, 1.36f, 0f), new Vector3(.26f, .26f, .27f), trim);
        }

        Transform Leg(string name, float x, Material material)
        {
            Transform pivot = new GameObject(name).transform;
            pivot.SetParent(visuals, false);
            pivot.localPosition = new Vector3(x, .8f, 0f);
            PlayerGeometry.Part("Boot", pivot, PrimitiveType.Capsule, new Vector3(0f, -.35f, .01f), new Vector3(.22f, .35f, .24f), material);
            return pivot;
        }

        void AnimateModel(float dt)
        {
            if (visuals == null) return;
            float speed = new Vector2(Velocity.x, Velocity.z).magnitude;
            stride += speed * dt * 2.8f;
            float swing = Mathf.Sin(stride) * Mathf.Min(32f, speed * 5f);
            if (leftLeg != null) leftLeg.localRotation = Quaternion.Euler(swing, 0f, 0f);
            if (rightLeg != null) rightLeg.localRotation = Quaternion.Euler(-swing, 0f, 0f);
            if (leftArm != null) leftArm.localRotation = Quaternion.Euler(IsAiming ? -55f : -swing * .65f, 0f, IsAiming ? -18f : 0f);
            float dip = IsCrouching || IsDodging ? -.45f : 0f;
            visuals.localPosition = Vector3.Lerp(visuals.localPosition, Vector3.up * dip, 1f - Mathf.Exp(-14f * dt));
            visuals.localRotation = Quaternion.Slerp(visuals.localRotation, Quaternion.Euler(IsSliding ? -25f : (IsSprinting ? 9f : 0f), 0f, IsDodging ? -20f : 0f), 1f - Mathf.Exp(-12f * dt));
        }
    }

    internal static class PlayerGeometry
    {
        public static Material Material(Color color, float glow = 0f)
        {
            Material result = new Material(Shader.Find("Standard"));
            result.color = color;
            result.SetFloat("_Glossiness", .28f);
            if (glow > 0f) { result.EnableKeyword("_EMISSION"); result.SetColor("_EmissionColor", color * glow); }
            return result;
        }

        public static Transform Part(string name, Transform parent, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            Collider collider = part.GetComponent<Collider>();
            if (collider != null) { collider.enabled = false; Object.Destroy(collider); }
            part.GetComponent<Renderer>().sharedMaterial = material;
            return part.transform;
        }
    }
}
