using UnityEngine;

namespace PokeProfessions.World
{
    /// <summary>Third-person follow camera with mouse-wheel zoom.</summary>
    public class FollowCamera : MonoBehaviour
    {
        public Transform target;
        public Vector3 offset = new Vector3(0f, 9.5f, -8.5f);
        public float lerp = 6f;
        public float minDist = 5.5f;
        public float maxDist = 15f;

        float dist = 0.5f;

        void LateUpdate()
        {
            if (target == null) return;

            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll != 0f) dist = Mathf.Clamp(dist - scroll * 0.7f, 0.4f, 1.8f);

            Vector3 scaled = offset * Mathf.Lerp(minDist / 10f, maxDist / 10f, dist);
            Vector3 want = target.position + scaled;
            want.y = Mathf.Max(want.y, WorldBuilder.GroundHeight(want.x, want.z) + 3f);

            transform.position = Vector3.Lerp(transform.position, want, lerp * Time.deltaTime);
            var look = target.position + Vector3.up * 1.2f;
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(look - transform.position), lerp * Time.deltaTime);
        }
    }
}
