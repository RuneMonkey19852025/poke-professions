using UnityEngine;
using PokeProfessions.Core;
using PokeProfessions.UI;
using PokeProfessions.Audio;

namespace PokeProfessions.World
{
    /// <summary>
    /// A wild creature roaming the overworld that the player can strike with
    /// a melee weapon. Optional, but no longer free: closing to AggroRange — or
    /// landing a hit — wakes it up, and while it is awake it lunges back on a
    /// timer, taking health off the trainer. That is what gives armour a job
    /// (PlayerCombat.Mitigation comes straight off each hit). It disengages if
    /// you leave it alone. Real fights still happen in the battle scene;
    /// striking grants weapon mastery XP, and a KO sheds the same loot a battle
    /// win would.
    /// </summary>
    public class MeleeTarget : MonoBehaviour
    {
        public Creature wild;          // roster entry this visual represents
        Vector3 home;
        Vector3 wanderTarget;
        float nextWander;
        float lungeUntil;
        bool ko;
        Transform visualRoot;
        CreatureVisual visual;   // pose driver (walk/attack/hit/death) when the pack has them

        const float WanderRadius = 6f;
        const float WanderSpeed = 1.6f;

        // Retaliation: how close the trainer has to be to wake it, how far it can
        // still reach, how often it swings, and how long it stays angry once you
        // leave. Raw damage is 2 + 1.2/level, before armour.
        public const float AggroRange = 3.0f;
        public const float AttackRange = 2.8f;
        public const float AttackInterval = 1.9f;
        public const float DisengageSeconds = 7f;

        bool aggro;
        float nextAttack;
        float lastSeen;

        public static MeleeTarget Spawn(Creature c, Vector3 xz)
        {
            if (c == null || c.Species == null) return null;

            var go = new GameObject("WildTarget_" + c.speciesId);
            var y = WorldBuilder.GroundHeight(xz.x, xz.z);
            go.transform.position = new Vector3(xz.x, y, xz.z);

            var mt = go.AddComponent<MeleeTarget>();
            mt.wild = c;
            mt.home = go.transform.position;
            mt.wanderTarget = mt.home;

            // the procedural creature visual doubles as the melee body
            var cv = CreatureVisual.Build(c.Species, c.shiny, go.transform);
            mt.visualRoot = cv.transform;
            mt.visual = cv;

            var trigger = go.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 1.2f;
            trigger.center = new Vector3(0, 0.9f, 0);

            return mt;
        }

        void Update()
        {
            if (ko) return;
            bool moving = Wander();
            if (Time.time < lungeUntil) return;   // mid-lunge: the lunge owns the pose

            // The pose follows what the creature is doing: a wild sliding across
            // the meadow inside its idle cycle is the visible tell that the
            // borrowed clips are not wired up. PlayState is idempotent, so asking
            // every frame does not restart the cycle.
            if (visual != null) visual.PlayState(moving ? "walk" : "idle");

            var pc = PlayerController.I;
            if (pc == null) return;
            float d = (pc.transform.position - transform.position).magnitude;

            // Waking up is what makes this a fight rather than a pounce at
            // nothing: close in or land a blow and it stays angry until you have
            // been out of reach long enough to lose interest.
            if (d <= AggroRange) { aggro = true; lastSeen = Time.time; }
            else if (aggro && Time.time - lastSeen > DisengageSeconds) aggro = false;

            if (!aggro || d > AttackRange || Time.time < nextAttack) return;

            nextAttack = Time.time + AttackInterval;
            StartCoroutine(LungeAt(pc.transform.position));
            pc.TakeHit(RetaliationDamage(), this);
        }

        /// <summary>The raw damage one of this creature's hits lands, before armour.</summary>
        public int RetaliationDamage()
        {
            return Mathf.RoundToInt(2f + wild.level * 1.2f);
        }

        /// <summary>True while this creature is actively fighting the trainer.</summary>
        public bool IsAggro => aggro;

        System.Collections.IEnumerator LungeAt(Vector3 target)
        {
            lungeUntil = Time.time + 0.6f;
            if (visual != null) visual.PlayState("attack");
            Vector3 dir = (target - transform.position); dir.y = 0; dir.Normalize();
            float end = Time.time + 0.28f;
            Vector3 start = transform.position;
            while (Time.time < end)
            {
                float t = Mathf.Sin((0.28f - (end - Time.time)) / 0.28f * Mathf.PI);
                transform.position = start + dir * (t * 0.9f);
                yield return null;
            }
            if (Sfx.I != null) Sfx.I.Play("ui");
        }

        /// <summary>
        /// Roam inside the home radius. Returns whether it actually travelled this
        /// frame, which is what picks the walk cycle over the idle one.
        /// </summary>
        bool Wander()
        {
            if (Time.time >= nextWander)
            {
                if ((transform.position - wanderTarget).magnitude < 0.3f)
                {
                    nextWander = Time.time + Random.Range(1.5f, 4f);
                    float ang = Random.Range(0f, Mathf.PI * 2f);
                    float rad = Mathf.Sqrt(Random.value) * WanderRadius;
                    Vector2 r = new Vector2(Mathf.Cos(ang) * rad, Mathf.Sin(ang) * rad);
                    Vector3 t = home + new Vector3(r.x, 0, r.y);
                    t.y = WorldBuilder.GroundHeight(t.x, t.z);
                    wanderTarget = t;
                    return false;
                }
                Vector3 dir = (wanderTarget - transform.position); dir.y = 0;
                if (dir.sqrMagnitude > 0.001f)
                {
                    dir.Normalize();
                    transform.position += dir * WanderSpeed * Time.deltaTime;
                    var look = Quaternion.LookRotation(dir);
                    transform.rotation = Quaternion.Slerp(transform.rotation, look, 6f * Time.deltaTime);
                    return true;
                }
            }
            return false;
        }

        /// <summary>Apply a trainer melee hit. Returns true when this blow KO'd the creature.</summary>
        public bool Hit(int dmg, Vector3 fromDir)
        {
            if (ko) return false;
            wild.curHP = Mathf.Max(0, wild.curHP - dmg);
            aggro = true;                 // hitting it is the clearest way to start a fight
            lastSeen = Time.time;
            Toast.Push("You hit the " + wild.DisplayName + " for " + dmg + ".");
            if (visual != null) visual.PlayState("hit");

            if (visualRoot != null)
            {
                var look = Quaternion.LookRotation(new Vector3(-fromDir.x, 0, -fromDir.z));
                visualRoot.rotation = look;
            }
            if (wild.curHP <= 0)
            {
                ko = true;
                return true;
            }
            if (Sfx.I != null) Sfx.I.Play("hit");
            return false;
        }

        /// <summary>KO payout: wild loot + coins, visual collapse, then despawn.</summary>
        public void KO(GameState save)
        {
            foreach (var d in WildLoot.Roll(wild))
            {
                save.AddItem(d.id, d.n);
                Toast.Push("+" + d.n + " " + ItemDB.Name(d.id));
            }
            int coins = 5 + wild.level;
            save.coins += coins;
            Toast.Push("The " + wild.DisplayName + " flees!  +" + coins + " coins.");
            if (Sfx.I != null) Sfx.I.Play("coin");

            if (visual != null) visual.PlayState("death");
            if (visualRoot != null)
                StartCoroutine(SinkAway(visualRoot.gameObject));
            var col = GetComponent<Collider>();
            if (col != null) col.enabled = false;
            Destroy(gameObject, 1.2f);
        }

        System.Collections.IEnumerator SinkAway(GameObject vis)
        {
            float end = Time.time + 1.1f;
            while (Time.time < end && vis != null)
            {
                vis.transform.localScale *= Mathf.Lerp(1f, 0.965f, Time.deltaTime * 4f);
                var lp = vis.transform.localPosition;
                lp.y -= Time.deltaTime * 0.35f;
                vis.transform.localPosition = lp;
                yield return null;
            }
            if (vis != null) Destroy(vis);
        }

        public bool IsKo => ko;
    }
}
