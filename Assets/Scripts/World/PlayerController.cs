using UnityEngine;
using PokeProfessions.Core;
using PokeProfessions.UI;

namespace PokeProfessions.World
{
    /// <summary>
    /// Third-person player. Camera-relative WASD + optional arrows, E to
    /// interact with the nearest Interactable, J for Journal, C for Creatures.
    /// Ground follows WorldBuilder.GroundHeight so the procedural terrain works
    /// without baked nav data.
    /// </summary>
    [RequireComponent(typeof(CapsuleCollider))]
    public class PlayerController : MonoBehaviour
    {
        public float moveSpeed = 7.5f;
        public float turnSpeed = 12f;
        public float interactRange = 3.4f;

        Transform camT;
        CapsuleCollider body;
        Interactable nearest;

        void Awake()
        {
            body = GetComponent<CapsuleCollider>();
        }

        void Start()
        {
            camT = Camera.main != null ? Camera.main.transform : null;
        }

        void Update()
        {
            if (Game.I == null || UiRouter.I == null) return;
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

            if (move.sqrMagnitude > 0.001f)
            {
                transform.position += move * moveSpeed * Time.deltaTime;
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
    }
}
