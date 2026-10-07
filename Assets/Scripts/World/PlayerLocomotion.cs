using UnityEngine;

namespace PokeProfessions.World
{
    /// <summary>
    /// Plays the player's locomotion clips — idle, walk, run — on the imported
    /// model's legacy Animation component.
    ///
    /// It is driven by how fast the controller is <i>actually</i> moving rather
    /// than by which key is held, so the pose always matches the motion on screen
    /// and it degrades quietly: the state names are the ones ModelLoader registers
    /// from the KayKit clip library, and a state the pack does not provide is
    /// skipped rather than throwing, leaving the procedural body unanimated.
    ///
    /// The walk/run boundary is derived from the controller's two gears rather
    /// than fixed, so a speed tuned to the clips (see PlayerController) can never
    /// end up below the boundary, which would leave the run state unreachable.
    ///
    /// It also owns the <b>event poses</b> — the swing and the flinch — through
    /// PlayOnce, because a pose the game raises from PlayerController would
    /// otherwise be overwritten by the very next locomotion frame, and two writers
    /// on one Animation component is just a race. One machine, two inputs.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerLocomotion : MonoBehaviour
    {
        /// <summary>Below this planar speed the player is standing still.</summary>
        public const float IdleSpeed = 0.3f;

        /// <summary>Fallback boundary when there is no controller to ask.</summary>
        const float DefaultRunGate = 6f;

        /// <summary>How quickly the measured speed follows reality (per second).</summary>
        const float Smoothing = 8f;

        PlayerController pc;
        Animation anim;
        string state = "";
        Vector3 lastPos;
        float speed;

        // ---- event poses (swing, flinch) ----
        // A clip that owns the skeleton for a moment instead of being picked by
        // speed. While one is live Update stands down; the frame it ends, the
        // locomotion state is re-picked from the measured speed as if the pose had
        // never happened. Both fields are cleared together in the two places that
        // can end a pose (the timer, and a fresh PlayOnce), so a swing interrupted
        // by a flinch cannot leave the machine stuck.
        string oneShot;
        float oneShotUntil;
        float oneShotSpeed = 1f;

        /// <summary>
        /// Planar speed at or above which the player counts as running: halfway
        /// between the two gears, so walking always reads as walking and only the
        /// run gear reads as running.
        /// </summary>
        public float RunGate
        {
            get
            {
                if (pc == null) pc = GetComponent<PlayerController>();
                return pc != null ? (pc.walkSpeed + pc.runSpeed) * 0.5f : DefaultRunGate;
            }
        }

        /// <summary>
        /// The smoothed planar speed the current state was chosen from, in u/s.
        /// Exposed so a test can check the measurement rather than only the state:
        /// the gears are tuned to the clips, so this has to read back the gear the
        /// controller is actually driving at, or they are tuned to nothing.
        /// </summary>
        public float MeasuredSpeed => speed;

        /// <summary>Which locomotion state a given planar speed calls for.</summary>
        public static string StateForSpeed(float planarSpeed, float runGate)
        {
            if (planarSpeed < IdleSpeed) return "idle";
            return planarSpeed < runGate ? "walk" : "run";
        }

        /// <summary>
        /// Play a clip once, over the locomotion machine, for <paramref name="seconds"/>
        /// of wall clock: the swing and the flinch are events, not speeds.
        ///
        /// The clip is time-scaled to fill that window rather than played at its
        /// authored length (the KayKit chop is 1.07 s, a blade cooldown is 0.65 s),
        /// so the arm has finished moving exactly when the weapon is ready again —
        /// a pose still mid-swing while the cooldown has already expired is what
        /// reads as "the animation is not wired up". The rate is clamped so an
        /// unusually long clip cannot be turned into a blur.
        ///
        /// A state the rig does not provide is skipped, leaving the current pose
        /// alone — the same quiet degradation as the locomotion states.
        /// </summary>
        public void PlayOnce(string next, float seconds)
        {
            if (anim == null && (anim = GetComponentInChildren<Animation>()) == null) return;
            if (string.IsNullOrEmpty(next)) return;

            var st = anim[next];
            if (st == null || st.clip == null || st.clip.length <= 0.001f) return;

            st.wrapMode = WrapMode.Once;
            st.speed = seconds > 0.01f ? Mathf.Clamp(st.clip.length / seconds, 0.35f, 3f) : 1f;

            oneShot = next;
            oneShotSpeed = st.speed;
            oneShotUntil = Time.time + Mathf.Max(0.05f, seconds);
            anim.Play(next);
        }

        /// <summary>True while an event pose owns the skeleton.</summary>
        public bool InOneShot => oneShot != null && Time.time < oneShotUntil;

        /// <summary>The clip an event pose is playing ("attack"/"hit"), or null.</summary>
        public string OneShot => InOneShot ? oneShot : null;

        /// <summary>Playback rate the live event pose was scaled to, for tests.</summary>
        public float OneShotRate => InOneShot ? oneShotSpeed : 1f;

        void Start()
        {
            pc = GetComponent<PlayerController>();
            anim = GetComponentInChildren<Animation>();
            lastPos = transform.position;
        }

        void Update()
        {
            if (anim == null && (anim = GetComponentInChildren<Animation>()) == null) return;

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            var p = transform.position;
            var delta = p - lastPos;
            delta.y = 0f;   // climbing a bank is not locomotion
            lastPos = p;

            // smoothed, so one long frame cannot flip the state
            speed = Mathf.Lerp(speed, delta.magnitude / dt, Mathf.Clamp01(dt * Smoothing));

            // An event pose (swing/flinch) owns the skeleton until it runs out.
            // Returning here still measured the motion above, so the locomotion
            // state chosen on the frame it ends is the one the speed calls for.
            if (Time.time < oneShotUntil) return;

            if (oneShot != null)
            {
                oneShot = null;
                state = "";   // force a re-issue: the pose left the rig mid-clip
            }
            Play(StateForSpeed(speed, RunGate));
        }

        void Play(string next)
        {
            if (next == state) return;
            state = next;
            var st = anim[next];
            if (st == null) return;   // pack has no such clip: leave the pose alone
            st.wrapMode = WrapMode.Loop;
            st.speed = 1f;
            anim.Play(next);
        }
    }
}
