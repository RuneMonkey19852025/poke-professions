using UnityEngine;

namespace PokeProfessions.World
{
    /// <summary>
    /// Deferred "feet on the ground" pass for an instantiated pack model.
    ///
    /// Why it is deferred: a SkinnedMeshRenderer that Unity has not re-skinned
    /// yet reports its <i>bind pose</i> bounds, and on these Legacy-imported FBX
    /// the bind pose is ~100x the animated size (the rig carries the FBX unit
    /// conversion on its skeleton nodes). ModelLoader turns
    /// <c>updateWhenOffscreen</c> on before this component runs, so once a frame
    /// has been skinned the renderer bounds describe the pose the player sees —
    /// that is the measurement used here.
    ///
    /// Sizing is deliberately NOT done here: ModelLoader sizes the model
    /// deterministically from the measured raw FBX height, so it is correct on
    /// the very first frame and never depends on a measurement settling. All
    /// that is left is the pivot: the packs vary from "feet at the origin" to
    /// "centered" to "hanging below", while the game's convention (procedural
    /// capsule bodies, blob shadows) is a visual bottom at y = 0 of the parent.
    /// </summary>
    [DisallowMultipleComponent]
    public class ModelFooting : MonoBehaviour
    {
        /// <summary>Frames to wait before the first measurement.</summary>
        const int FirstSample = 2;
        /// <summary>Stop trying (leaving the model as authored) after this many frames.</summary>
        const int GiveUpAfter = 30;
        /// <summary>Sole clearance above the ground plane, in parent units.</summary>
        const float SoleEpsilon = 0.02f;
        /// <summary>How far off the expected height a measurement may be before it is distrusted.</summary>
        const float MaxHeightRatio = 2.5f;

        float expectedHeight;   // in the parent's local space
        int frames;

        /// <summary>
        /// Ground-align <paramref name="model"/> over the next few frames, once
        /// its skinned renderers have settled onto the animated pose.
        /// </summary>
        /// <param name="expectedHeight">
        /// The height ModelLoader sized the model to, in parent units — used to
        /// reject a bind-pose (100x) measurement.
        /// </param>
        public static void Attach(GameObject model, float expectedHeight)
        {
            if (model == null) return;
            var footing = model.AddComponent<ModelFooting>();
            footing.expectedHeight = expectedHeight;
        }

        void LateUpdate()
        {
            if (++frames < FirstSample) return;
            if (frames > GiveUpAfter) { Destroy(this); return; }

            Bounds world;
            if (!TryRenderedBounds(transform, out world)) return;

            var parent = transform.parent;
            float parentY = parent != null ? parent.position.y : 0f;
            float parentScaleY = parent != null ? parent.lossyScale.y : 1f;
            if (Mathf.Abs(parentScaleY) < 0.00001f) parentScaleY = 1f;

            // A skin that has not settled yet reports the bind pose, which is
            // 100x on these rigs — that would lift the model tens of units. Skip
            // (and retry next frame) instead of trusting it.
            float heightLocal = world.size.y / parentScaleY;
            if (expectedHeight > 0.001f && heightLocal > expectedHeight * MaxHeightRatio) return;

            float minYLocal = (world.min.y - parentY) / parentScaleY;
            transform.localPosition += Vector3.up * (-minYLocal + SoleEpsilon);
            Destroy(this);
        }

        /// <summary>
        /// Union of every body part of a model, in world space. These renderers
        /// are the ones the player sees; the boss aura ring is decoration and
        /// deliberately excluded.
        /// </summary>
        static bool TryRenderedBounds(Transform model, out Bounds bounds)
        {
            bounds = new Bounds();
            bool any = false;
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || r.name == "aura_ring") continue;
                if (!any) { bounds = r.bounds; any = true; }
                else bounds.Encapsulate(r.bounds);
            }
            return any;
        }
    }
}
