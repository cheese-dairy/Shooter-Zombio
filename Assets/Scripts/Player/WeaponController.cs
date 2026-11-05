using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Zombio
{
    [Serializable]
    public sealed class WeaponDefinition
    {
        public string Name;
        public int Magazine = 12;
        public int StartingReserve = 72;
        public float Damage = 32f;
        public float ShotInterval = .24f;
        public float ReloadSeconds = 1.35f;
        public int Pellets = 1;
        public float Spread = .7f;
        public float Recoil = 1f;
        public Color Accent = Color.cyan;
    }

    /// <summary>Camera targeting with a second muzzle ray prevents shooting through nearby cover.</summary>
    public sealed class WeaponController : MonoBehaviour
    {
        public WeaponDefinition[] Definitions =
        {
            new WeaponDefinition { Name = "M-12 SIDEARM", Magazine = 12, StartingReserve = 72, Damage = 32f, ShotInterval = .24f, ReloadSeconds = 1.35f, Pellets = 1, Spread = .7f, Recoil = 1.2f, Accent = new Color(.24f, .94f, .84f) },
            new WeaponDefinition { Name = "R-30 CARBINE", Magazine = 30, StartingReserve = 120, Damage = 22f, ShotInterval = .105f, ReloadSeconds = 1.85f, Pellets = 1, Spread = 1.15f, Recoil = .75f, Accent = new Color(1f, .64f, .25f) },
            new WeaponDefinition { Name = "BREACH-6", Magazine = 6, StartingReserve = 36, Damage = 15f, ShotInterval = .8f, ReloadSeconds = 2.2f, Pellets = 8, Spread = 5.5f, Recoil = 3f, Accent = new Color(.95f, .34f, .5f) }
        };
        public string WeaponName => Current.Name;
        public int Ammo => loaded != null ? loaded[WeaponIndex] : 0;
        public int ReserveAmmo => reserves != null ? reserves[WeaponIndex] : 0;
        public int MagazineSize => Current.Magazine;
        public bool IsReloading { get; private set; }
        public float ReloadProgress => IsReloading ? Mathf.Clamp01((Time.time - reloadStarted) / Current.ReloadSeconds) : 0f;
        public int WeaponIndex { get; private set; }
        public float DamageBoostRemaining => Mathf.Max(0f, damageBoostUntil - Time.time);
        public float RapidFireRemaining => Mathf.Max(0f, rapidBoostUntil - Time.time);
        public bool IsUnlocked(int index) => unlocked != null && index >= 0 && index < unlocked.Length && unlocked[index];

        WeaponDefinition Current => Definitions[Mathf.Clamp(WeaponIndex, 0, Definitions.Length - 1)];
        GameSession session;
        PlayerController player;
        ThirdPersonCamera view;
        int[] loaded, reserves;
        bool[] unlocked;
        Transform weaponPivot, model, muzzle, flash;
        Light flashLight;
        Material metalMaterial, gripMaterial, accentMaterial, flashMaterial;
        float nextShot, reloadStarted, damageBoostUntil, rapidBoostUntil, flashUntil;
        float visualRecoil;

        public void Configure(GameSession game, PlayerController actor, ThirdPersonCamera cameraRig)
        {
            session = game;
            player = actor;
            view = cameraRig;
            metalMaterial = PlayerGeometry.Material(new Color(.17f, .22f, .26f));
            gripMaterial = PlayerGeometry.Material(new Color(.045f, .06f, .075f));
            accentMaterial = PlayerGeometry.Material(Current.Accent, .45f);
            flashMaterial = PlayerGeometry.Material(new Color(1f, .72f, .25f), 3f);
            weaponPivot = new GameObject("WeaponRig").transform;
            weaponPivot.SetParent(transform, false);
            weaponPivot.localPosition = new Vector3(.38f, 1.32f, .3f);
            ResetState();
        }

        public void ResetState()
        {
            loaded = new int[Definitions.Length];
            reserves = new int[Definitions.Length];
            unlocked = new bool[Definitions.Length];
            for (int i = 0; i < Definitions.Length; i++)
            {
                loaded[i] = Definitions[i].Magazine;
                reserves[i] = Definitions[i].StartingReserve;
            }
            unlocked[0] = true;
            WeaponIndex = 0;
            IsReloading = false;
            nextShot = damageBoostUntil = rapidBoostUntil = flashUntil = visualRecoil = 0f;
            if (weaponPivot != null) BuildWeapon();
        }

        void Update()
        {
            if (flash != null) flash.gameObject.SetActive(Time.time < flashUntil);
            if (flashLight != null) flashLight.enabled = Time.time < flashUntil;
            if (session == null || !session.IsPlaying || player.IsDead) return;
            for (int i = 0; i < Mathf.Min(Definitions.Length, 3); i++)
                if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i))) SelectWeapon(i);
            if (IsReloading && Time.time - reloadStarted >= Current.ReloadSeconds) FinishReload();
            if (Input.GetKeyDown(KeyCode.R)) BeginReload();
            if (Input.GetMouseButton(0)) TryFire();
        }

        public bool TryFire()
        {
            if (session == null || !session.IsPlaying || player.IsDead || IsReloading || Time.time < nextShot || player.IsSliding || player.IsDodging) return false;
            if (Ammo > 0) { Fire(); return true; }
            nextShot = Time.time + .35f;
            if (ReserveAmmo > 0) BeginReload();
            else
            {
                session.Notify("OUT OF AMMO · FIND A SUPPLY CACHE", new Color(1f, .58f, .3f));
                session.PlaySound(SoundCue.Click, transform.position);
            }
            return false;
        }

        void LateUpdate()
        {
            if (weaponPivot == null || view == null || view.Camera == null) return;
            Vector3 aimPoint = view.Camera.transform.position + view.Camera.transform.forward * 70f;
            RaycastHit hit;
            if (Cast(view.Camera.transform.position, view.Camera.transform.forward, 100f, out hit)) aimPoint = hit.point;
            weaponPivot.localPosition = new Vector3(.38f, player.IsCrouching ? .95f : 1.32f, .3f);
            Vector3 direction = aimPoint - weaponPivot.position;
            if (direction.sqrMagnitude > .01f) weaponPivot.rotation = Quaternion.LookRotation(direction);
            visualRecoil = Mathf.MoveTowards(visualRecoil, 0f, Time.deltaTime * 2f);
            if (model != null)
            {
                model.localPosition = new Vector3(0f, 0f, -visualRecoil);
                float reloadTilt = IsReloading ? Mathf.Sin(ReloadProgress * Mathf.PI) * 42f : 0f;
                model.localRotation = Quaternion.Euler(-visualRecoil * 25f + reloadTilt, 0f, -reloadTilt * .5f);
            }
        }

        public void SelectWeapon(int index)
        {
            if (index < 0 || index >= Definitions.Length || index == WeaponIndex) return;
            if (!unlocked[index])
            {
                session.Notify("WEAPON LOCKED · SEARCH FOR A CACHE", new Color(.65f, .8f, .85f));
                return;
            }
            WeaponIndex = index;
            IsReloading = false;
            nextShot = Time.time + .2f;
            BuildWeapon();
            session.PlaySound(SoundCue.Click, transform.position);
        }

        public void UnlockWeapon(int index)
        {
            index = Mathf.Clamp(index, 0, Definitions.Length - 1);
            bool fresh = !unlocked[index];
            unlocked[index] = true;
            if (!fresh) reserves[index] = Mathf.Min(999, reserves[index] + Definitions[index].Magazine * 2);
            if (fresh) { SelectWeapon(index); session.Notify(Definitions[index].Name + " ACQUIRED", Definitions[index].Accent); }
        }

        public void AddAmmo(int amount)
        {
            for (int i = 0; i < reserves.Length; i++)
                if (unlocked[i]) reserves[i] = Mathf.Min(999, reserves[i] + Mathf.Max(0, i == WeaponIndex ? amount : amount / 2));
        }
        public void AddDamageBoost(float seconds) { damageBoostUntil = Mathf.Max(Time.time, damageBoostUntil) + Mathf.Max(0f, seconds); }
        public void AddRapidFireBoost(float seconds) { rapidBoostUntil = Mathf.Max(Time.time, rapidBoostUntil) + Mathf.Max(0f, seconds); }

        public void BeginReload()
        {
            if (session == null || !session.IsPlaying || player.IsDead) return;
            if (IsReloading || Ammo >= Current.Magazine || ReserveAmmo <= 0) return;
            IsReloading = true;
            reloadStarted = Time.time;
            session.PlaySound(SoundCue.Reload, transform.position);
        }

        void FinishReload()
        {
            int moved = Mathf.Min(Current.Magazine - loaded[WeaponIndex], reserves[WeaponIndex]);
            loaded[WeaponIndex] += moved;
            reserves[WeaponIndex] -= moved;
            IsReloading = false;
        }

        void Fire()
        {
            loaded[WeaponIndex]--;
            nextShot = Time.time + Current.ShotInterval * (RapidFireRemaining > 0f ? .62f : 1f);
            visualRecoil = WeaponIndex == 2 ? .16f : .08f;
            flashUntil = Time.time + .045f;
            if (flash != null) { flash.gameObject.SetActive(true); flash.localRotation = Random.rotation; }
            if (flashLight != null) flashLight.enabled = true;
            session.PlaySound(SoundCue.Shot, transform.position);
            view.Kick(Current.Recoil);
            bool anyHit = false, anyKill = false;
            Camera camera = view.Camera;
            Vector3 origin = muzzle.position;
            // A muzzle may penetrate cover while its owner remains outside. Start the
            // shot at the first cover surface along the chest-to-muzzle segment.
            Vector3 chest = transform.position + Vector3.up * (player.IsCrouching ? .95f : 1.32f);
            Vector3 barrelSegment = origin - chest;
            RaycastHit cover;
            bool barrelBlocked = Cast(chest, barrelSegment.normalized, barrelSegment.magnitude, out cover);
            for (int i = 0; i < Current.Pellets; i++)
            {
                Vector2 spread = Random.insideUnitCircle * Current.Spread * (player.IsAiming ? .45f : 1f);
                Vector3 direction = camera.transform.rotation * Quaternion.Euler(spread.y, spread.x, 0f) * Vector3.forward;
                Vector3 target = camera.transform.position + direction * 100f;
                RaycastHit hit;
                bool cameraHit = Cast(camera.transform.position, direction, 100f, out hit);
                if (cameraHit) target = hit.point;
                Vector3 muzzleDirection = target - origin;
                bool muzzleHit;
                if (barrelBlocked) { hit = cover; muzzleHit = true; }
                else muzzleHit = Cast(origin, muzzleDirection.normalized, muzzleDirection.magnitude + .1f, out hit);
                Vector3 end = muzzleHit ? hit.point : target;
                ShotEffect.Tracer(origin, end, Current.Accent);
                if (!muzzleHit) continue;
                EnemyActor enemy = hit.collider.GetComponentInParent<EnemyActor>();
                if (enemy != null && !enemy.IsDead)
                {
                    float damage = Current.Damage * (DamageBoostRemaining > 0f ? 1.65f : 1f);
                    enemy.TakeDamage(damage, hit.point);
                    anyHit = true;
                    anyKill |= enemy.IsDead;
                    ShotEffect.Impact(hit.point, new Color(.5f, .95f, .54f));
                }
                else ShotEffect.Impact(hit.point + hit.normal * .035f, new Color(1f, .7f, .3f));
            }
            if (anyHit)
            {
                session.HitConfirmed(anyKill);
                if (!anyKill) session.PlaySound(SoundCue.Hit, transform.position);
            }
        }

        bool Cast(Vector3 origin, Vector3 direction, float distance, out RaycastHit nearest)
        {
            nearest = default(RaycastHit);
            float closest = distance + 1f;
            bool found = false;
            RaycastHit[] hits = Physics.RaycastAll(origin, direction, distance, ~0, QueryTriggerInteraction.Ignore);
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.transform.IsChildOf(transform) || hit.distance >= closest) continue;
                nearest = hit;
                closest = hit.distance;
                found = true;
            }
            return found;
        }

        void BuildWeapon()
        {
            if (model != null) { model.gameObject.SetActive(false); Destroy(model.gameObject); }
            model = new GameObject(Current.Name).transform;
            model.SetParent(weaponPivot, false);
            accentMaterial.color = Current.Accent;
            accentMaterial.SetColor("_EmissionColor", Current.Accent * .45f);
            float length = WeaponIndex == 0 ? .38f : .68f;
            PlayerGeometry.Part("Receiver", model, PrimitiveType.Cube, new Vector3(0f, 0f, length * .4f), new Vector3(.17f, .18f, length), metalMaterial);
            PlayerGeometry.Part("Grip", model, PrimitiveType.Cube, new Vector3(0f, -.17f, .04f), new Vector3(.13f, .25f, .16f), gripMaterial).localRotation = Quaternion.Euler(-12f, 0f, 0f);
            Transform barrel = PlayerGeometry.Part("Barrel", model, PrimitiveType.Cylinder, new Vector3(0f, .015f, length), new Vector3(.09f, .17f, .09f), gripMaterial);
            barrel.localRotation = Quaternion.Euler(90f, 0f, 0f);
            PlayerGeometry.Part("Sight", model, PrimitiveType.Cube, new Vector3(0f, .14f, .2f), new Vector3(.08f, .09f, .15f), gripMaterial);
            PlayerGeometry.Part("IdentityStripe", model, PrimitiveType.Cube, new Vector3(.09f, .015f, length * .45f), new Vector3(.018f, .1f, length * .65f), accentMaterial);
            if (WeaponIndex > 0)
            {
                PlayerGeometry.Part("Stock", model, PrimitiveType.Cube, new Vector3(0f, -.035f, -.24f), new Vector3(.13f, .2f, .3f), gripMaterial);
                PlayerGeometry.Part("Magazine", model, PrimitiveType.Cube, new Vector3(0f, -.2f, .27f), new Vector3(.11f, .28f, .19f), metalMaterial);
            }
            if (WeaponIndex == 2)
                PlayerGeometry.Part("Pump", model, PrimitiveType.Cube, new Vector3(0f, -.1f, .55f), new Vector3(.21f, .14f, .25f), accentMaterial);
            muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(model, false);
            muzzle.localPosition = new Vector3(0f, .015f, length + .19f);
            flash = PlayerGeometry.Part("MuzzleFlash", muzzle, PrimitiveType.Sphere, Vector3.zero, new Vector3(.16f, .16f, .3f), flashMaterial);
            flash.gameObject.SetActive(false);
            flashLight = muzzle.gameObject.AddComponent<Light>();
            flashLight.type = LightType.Point;
            flashLight.color = new Color(1f, .6f, .22f);
            flashLight.range = 4f;
            flashLight.intensity = 2f;
            flashLight.enabled = false;
        }

        void OnDestroy()
        {
            if (metalMaterial != null) Destroy(metalMaterial);
            if (gripMaterial != null) Destroy(gripMaterial);
            if (accentMaterial != null) Destroy(accentMaterial);
            if (flashMaterial != null) Destroy(flashMaterial);
        }
    }
}
