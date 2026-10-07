using UnityEngine;
using PokeProfessions.Core;
using PokeProfessions.UI;
using PokeProfessions.Audio;

namespace PokeProfessions.World
{
    /// <summary>
    /// Third-person player. Camera-relative WASD + optional arrows (hold Shift
    /// to run), E to interact with the nearest Interactable, J for Journal, C for
    /// Creatures. Ground follows WorldBuilder.GroundHeight so the procedural
    /// terrain works without baked nav data.
    ///
    /// The two gears exist so PlayerLocomotion has a walk and a run to switch
    /// between, and both are matched to the clip they play rather than picked to
    /// taste: the imported KayKit walk cycle covers its stride in 1.067s and the
    /// run cycle in 0.8s, which is 0.78u/s and 2.85u/s of ground. Travelling any
    /// faster than the clip's stride is what makes the planted foot skate, so
    /// these are the sliding-free floor — EditorPlaytest measures both from the
    /// clips and fails if a replacement animation drifts away from them.
    ///
    /// They are slow in absolute terms because the character is only ~1.45u tall
    /// and the cycles are a settled walking pace and jog, not a sprint.
    /// </summary>
    [RequireComponent(typeof(CapsuleCollider))]
    public class PlayerController : MonoBehaviour
    {
        /// <summary>Speed while walking (no run modifier held); the walk clip's stride.</summary>
        public float walkSpeed = 0.78f;
        /// <summary>Speed while running (Shift held); the run clip's stride.</summary>
        public float runSpeed = 2.85f;
        public float turnSpeed = 12f;
        public float interactRange = 3.4f;

        public static PlayerController I { get; private set; }

        Transform camT;
        CapsuleCollider body;
        Interactable nearest;

        // ---- melee combat ----
        float nextAttackAt;      // earliest time the next swing may start
        float swingUntil;        // weapon prop is mid-swing until this time
        PlayerLocomotion locomotion;  // drives the imported body's clips (may be absent)
        GameObject weaponProp;
        GameObject armorProp;
        Quaternion weaponRestRot = Quaternion.identity; // resting pose of the prop
        MeleeTarget aimTarget;

        // ---- the trainer's own health ----
        // Wild creatures fight back now, so the trainer has something to lose. See
        // TakeHit/KnockOut; the pool and its armour mitigation live in PlayerCombat.
        float lastHurtAt;
        float regenCarry;        // holds sub-1 HP so slow regen still accumulates

        void Awake()
        {
            I = this;
            body = GetComponent<CapsuleCollider>();
        }

        void OnDestroy()
        {
            if (I == this) I = null;
        }

        void Start()
        {
            camT = Camera.main != null ? Camera.main.transform : null;
            RefreshGearProps();
        }

        /// <summary>
        /// The player body's animation driver. Only present when the imported
        /// KayKit model is in use (the procedural capsule body has no clips), so
        /// every caller treats it as optional.
        /// </summary>
        PlayerLocomotion Locomotion
        {
            get
            {
                if (locomotion == null) locomotion = GetComponent<PlayerLocomotion>();
                return locomotion;
            }
        }

        /// <summary>Rebuild both worn props (weapon and armour) after equipping.</summary>
        public void RefreshGearProps()
        {
            RefreshWeaponProp();
            RefreshArmorProp();
        }

        /// <summary>
        /// A breastplate over the chest, tinted by the armour's tier, so what you
        /// are wearing is readable in the world and not only in a menu. Wearing
        /// nothing draws nothing — bare is a look, not a missing prop.
        /// </summary>
        public void RefreshArmorProp()
        {
            if (armorProp != null) Destroy(armorProp);
            armorProp = null;

            var s = Game.I != null ? Game.I.State : null;
            var def = s != null ? ItemDB.Get(s.playerArmor) : null;
            if (def == null) return;

            armorProp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            armorProp.name = "armour_" + def.id;
            armorProp.transform.SetParent(transform, false);
            armorProp.transform.localPosition = new Vector3(0f, 1.02f, 0.1f);
            armorProp.transform.localScale = new Vector3(0.4f, 0.46f, 0.18f);
            armorProp.GetComponent<MeshRenderer>().material = WorldBuilder.ToonMat(ItemRarity.ColorHex(def.rarity));
            var ac = armorProp.GetComponent<Collider>();
            if (ac != null) Destroy(ac);
        }

        /// <summary>Rebuild the visible weapon prop (call after equipping/unequipping).</summary>
        public void RefreshWeaponProp()
        {
            if (weaponProp != null) Destroy(weaponProp);
            weaponProp = null;

            var s = Game.I != null ? Game.I.State : null;
            string w = s != null ? s.playerWeapon : "";
            if (string.IsNullOrEmpty(w)) return;

            string hex;
            Vector3 pos, scale, euler;
            switch (PlayerCombat.KindOf(w))
            {
                case PlayerWeaponKind.Blade:
                    hex = "#E08D57"; pos = new Vector3(0.42f, 0.95f, 0.18f);
                    scale = new Vector3(0.07f, 0.85f, 0.16f); euler = new Vector3(20f, 0f, -10f); break;
                case PlayerWeaponKind.Spear:
                    hex = "#A8B0B8"; pos = new Vector3(0.44f, 1.0f, 0.1f);
                    scale = new Vector3(0.05f, 0.18f, 0.05f); euler = new Vector3(70f, 0f, -8f); break;
                case PlayerWeaponKind.Axe:
                    hex = "#B08968"; pos = new Vector3(0.42f, 0.9f, 0.16f);
                    scale = new Vector3(0.09f, 0.6f, 0.34f); euler = new Vector3(25f, 0f, -14f); break;
                default: // club
                    hex = "#8A5E33"; pos = new Vector3(0.42f, 0.9f, 0.14f);
                    scale = new Vector3(0.1f, 0.7f, 0.1f); euler = new Vector3(22f, 0f, -10f); break;
            }

            weaponProp = GameObject.CreatePrimitive(PlayerWeaponKind.Spear == PlayerCombat.KindOf(w)
                ? PrimitiveType.Cylinder : PrimitiveType.Cube);
            weaponProp.name = "weapon_" + w;
            weaponProp.transform.SetParent(transform, false);
            weaponProp.transform.localPosition = pos;
            weaponProp.transform.localScale = scale;
            weaponRestRot = Quaternion.Euler(euler);
            weaponProp.transform.localRotation = weaponRestRot;
            // the prop wears its tier's colour, so a Legendary weapon reads as
            // one at a glance in the world and not just in the panel
            var def = ItemDB.Get(w);
            if (def != null && def.rarity != Rarity.Normal) hex = ItemRarity.ColorHex(def.rarity);

            weaponProp.GetComponent<MeshRenderer>().material = WorldBuilder.ToonMat(hex);
            var wc = weaponProp.GetComponent<Collider>();
            if (wc != null) Destroy(wc);
        }

        void Update()
        {
            if (Game.I == null || UiRouter.I == null) return;

            TickHealth();

            if (UiRouter.I.UiOpen) return; // menus eat input

            // ---- ground snap (uses walkable surface: terrain or bridge deck) ----
            float ground = WorldBuilder.SurfaceHeight(transform.position.x, transform.position.z);
            Vector3 p = transform.position;
            // snap fast downward (step off), smooth upward (climb bank)
            float rate = ground < p.y ? 20f : 10f;
            p.y = Mathf.Lerp(p.y, ground, Time.deltaTime * rate);
            transform.position = p;

            // ---- movement ----
            Vector3 input = new Vector3(Input.GetAxisRaw("Horizontal"), 0, Input.GetAxisRaw("Vertical"));
            if (input.sqrMagnitude > 1f) input.Normalize();

            Vector3 move = Vector3.zero;
            if (camT != null)
            {
                Vector3 fwd = camT.forward; fwd.y = 0; fwd.Normalize();
                Vector3 right = camT.right; right.y = 0; right.Normalize();
                move = fwd * input.z + right * input.x;
            }
            else
            {
                move = input;
            }

            bool running = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            float speed = running ? runSpeed : walkSpeed;

            if (move.sqrMagnitude > 0.001f)
            {
                transform.position += move * speed * Time.deltaTime;
                var look = Quaternion.LookRotation(move);
                transform.rotation = Quaternion.Slerp(transform.rotation, look, turnSpeed * Time.deltaTime);

                // wild encounter roll while walking
                if (Game.I != null) Game.I.RollEncounter();
            }

            // clamp to island
            Vector3 pos = transform.position;
            pos.x = Mathf.Clamp(pos.x, -95f, 95f);
            pos.z = Mathf.Clamp(pos.z, -95f, 95f);
            transform.position = pos;

            // ---- interact ----
            UpdateNearest();
            if (Input.GetKeyDown(KeyCode.E) && nearest != null)
            {
                nearest.Interact(gameObject);
            }

            // ---- melee attack ----
            UpdateAim();
            bool swingPressed = Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0);
            if (swingPressed && Time.time >= nextAttackAt)
                TryAttack();

            // ease the weapon prop back to its per-weapon resting pose after a swing
            if (weaponProp != null)
                weaponProp.transform.localRotation = Time.time < swingUntil
                    ? Quaternion.Euler(-70f, 0f, 0f)
                    : weaponRestRot;
        }

        // ---------------- the trainer's own health ----------------

        /// <summary>
        /// Slow out-of-combat mending, and the ceiling clamp. Armour sets the
        /// ceiling (base + "hp+N"), so taking a piece off can leave the trainer
        /// above their own maximum — this is what pulls them back down. Runs even
        /// with a menu open: health should not freeze because the bag is.
        /// </summary>
        void TickHealth()
        {
            var s = Game.I.State;
            if (s == null) return;

            int maxHP = PlayerCombat.MaxHP(s);
            if (s.playerHP > maxHP) { s.playerHP = maxHP; return; }
            if (s.playerHP >= maxHP) { regenCarry = 0f; return; }
            if (Time.time - lastHurtAt < PlayerCombat.RegenDelay) { regenCarry = 0f; return; }

            regenCarry += PlayerCombat.RegenPerSecond * Time.deltaTime;
            if (regenCarry < 1f) return;
            int whole = (int)regenCarry;
            regenCarry -= whole;
            s.playerHP = Mathf.Min(maxHP, s.playerHP + whole);
        }

        /// <summary>
        /// Take a hit from a wild creature. Armour takes a flat bite out of it
        /// (PlayerCombat.DamageTaken), which is the whole reason the armour ladder
        /// exists: without it, the same hit lands for full. Dropping to zero knocks
        /// the trainer out — see KnockOut.
        /// </summary>
        public void TakeHit(int raw, MeleeTarget from = null)
        {
            var s = Game.I != null ? Game.I.State : null;
            if (s == null || raw <= 0) return;

            int dmg = PlayerCombat.DamageTaken(s, raw);
            int soaked = Mathf.Min(PlayerCombat.Mitigation(s), raw - 1);
            s.playerHP = Mathf.Max(0, s.playerHP - dmg);
            lastHurtAt = Time.time;
            regenCarry = 0f;

            Toast.Push("You take " + dmg + " from the wild creature" +
                       (soaked > 0 ? "  (armour soaks " + soaked + ")" : "") +
                       "  —  " + PlayerCombat.HealthLabel(s) + " HP");
            if (Sfx.I != null) Sfx.I.Play("hit");

            // a flinch, so a bite that landed on you is visible without reading
            // the toast. It outranks a swing in progress: being hit interrupts.
            var loco = Locomotion;
            if (loco != null) loco.PlayOnce("hit", PlayerCombat.FlinchSeconds);

            if (s.playerHP <= 0) KnockOut(s, from);
        }

        /// <summary>
        /// Zero health: the trainer comes to on Emberpost's plaza, patched up. The
        /// walk back is the cost — no coins, no gear — and whatever put them down
        /// shakes the fight off, so trading knockouts for damage is not a way to
        /// chip something down for free.
        /// </summary>
        void KnockOut(GameState s, MeleeTarget from)
        {
            s.playerHP = PlayerCombat.MaxHP(s);
            if (from != null && from.wild != null) from.wild.curHP = from.wild.MaxHP;

            // the default new-game spawn: back on the plaza, where it all started
            const float HomeX = 0f, HomeZ = -16f;
            transform.position = new Vector3(HomeX,
                WorldBuilder.GroundHeight(HomeX, HomeZ) + 0.6f, HomeZ);

            Toast.Push("You go down... and wake up in Emberpost, bruised but whole.");
            if (Sfx.I != null) Sfx.I.Play("ui");
        }

        void UpdateNearest()
        {
            nearest = null;
            if (Time.time < (UiRouter.I != null ? UiRouter.I.InteractSuppressUntil : 0f)) return;
            float best = interactRange * interactRange;
            var all = FindObjectsOfType<Interactable>();
            foreach (var it in all)
            {
                float d = (it.transform.position - transform.position).sqrMagnitude;
                if (d < best) { best = d; nearest = it; }
            }
        }

        public Interactable NearestInteractable => nearest;

        // ---------------- melee combat ----------------

        void UpdateAim()
        {
            aimTarget = null;
            float best = PlayerCombat.ReachOf(Game.I.State);
            best *= best;
            foreach (var t in FindObjectsOfType<MeleeTarget>())
            {
                if (t.IsKo) continue;
                float d = (t.transform.position - transform.position).sqrMagnitude;
                if (d < best) { best = d; aimTarget = t; }
            }
        }

        void TryAttack()
        {
            var s = Game.I.State;
            nextAttackAt = Time.time + PlayerCombat.CooldownOf(s);
            swingUntil = Time.time + PlayerCombat.SwingSecondsOf(s);

            // The body swings with the prop: the clip is scaled to the weapon's
            // cooldown (see PlayerLocomotion.PlayOnce), so the arm lands with the
            // fastest weapon and takes its time with the slowest. Played even at
            // empty air — a swing that whiffs should still look like one.
            var loco = Locomotion;
            if (loco != null) loco.PlayOnce("attack", PlayerCombat.CooldownOf(s));

            if (aimTarget == null)
            {
                // a swing at empty air still costs the cooldown — feels weighty
                return;
            }

            var target = aimTarget;
            Vector3 dir = (target.transform.position - transform.position); dir.y = 0; dir.Normalize();
            var look = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, 0.75f);

            int dmg = PlayerCombat.DamageRoll(s);
            bool ko = target.Hit(dmg, dir);
            PlayerCombat.AddXp(s, PlayerCombat.XpPerHit);

            if (ko)
            {
                target.KO(s);
                PlayerCombat.AddXp(s, PlayerCombat.XpPerKO);
                aimTarget = null;
            }
        }

        /// <summary>Debug/cheat hook: does the trainer currently aim at a wild target?</summary>
        public MeleeTarget AimedTarget => aimTarget;
    }
}
