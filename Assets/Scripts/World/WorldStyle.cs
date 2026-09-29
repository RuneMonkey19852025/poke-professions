using UnityEngine;

namespace PokeProfessions.World
{
    /// <summary>
    /// Style director for the modern creature-RPG look: pushes sun/sky/ambient
    /// colors into the toon shaders and provides the shared material factory.
    /// Created once by Game.
    /// </summary>
    public class WorldStyle : MonoBehaviour
    {
        public static WorldStyle I { get; private set; }

        public Vector3 sunDir = new Vector3(0.45f, 0.85f, 0.30f);
        public Color sunColor = new Color(1.00f, 0.96f, 0.86f);
        public Color skyTint = new Color(0.68f, 0.78f, 0.92f);   // ambient from above
        public Color groundTint = new Color(0.42f, 0.46f, 0.38f); // bounce from below
        public Color skyColor = new Color(0.62f, 0.83f, 0.93f);  // camera background
        public Color fogColor = new Color(0.74f, 0.86f, 0.94f);
        public float shadowStrength = 0.52f;
        public float rimStrength = 0.55f;
        public float specular = 0.5f;
        public float steps = 2f;

        void Awake()
        {
            I = this;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0055f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        }

        void Update()
        {
            Shader.SetGlobalVector("WorldStyleSunDir", sunDir.normalized);
            Shader.SetGlobalVector("WorldStyleSunColor", sunColor);
            Shader.SetGlobalVector("WorldStyleSkyTint", skyTint);
            Shader.SetGlobalVector("WorldStyleGroundTint", groundTint);
            Shader.SetGlobalFloat("WorldStyleShadow", shadowStrength);
            Shader.SetGlobalFloat("WorldStyleRim", rimStrength);
            Shader.SetGlobalFloat("WorldStyleSpecular", specular);
            Shader.SetGlobalFloat("WorldStyleStepCount", steps);

            RenderSettings.ambientLight = skyColor;
            RenderSettings.fogColor = fogColor;
        }

        /// <summary>
        /// Shared toon material factory. All world + creature + prop materials
        /// go through here so the whole game shares one lighting style.
        /// </summary>
        public static Material ToonMat(string hex)
        {
            var sh = Resources.Load<Shader>("Shaders/UnlitColor");
            if (sh == null) sh = Shader.Find("PokeProfessions/UnlitColor");
            if (sh == null) sh = Shader.Find("Legacy Shaders/Diffuse");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            if (sh == null)
            {
                Debug.LogError("[WorldStyle] No shader available for ToonMat!");
                return null;
            }
            var m = new Material(sh);
            if (m.HasProperty("_Color")) m.color = ColorFromHex(hex);
            return m;
        }

        public static Color ColorFromHex(string hex)
        {
            Color c;
            return ColorUtility.TryParseHtmlString(hex, out c) ? c : Color.magenta;
        }
    }
}
