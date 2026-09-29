Shader "PokeProfessions/ToonLit"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        _UseVertexColor ("Use Vertex Color", Float) = 0
        _RimPower ("Rim Power", Float) = 3
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 normal : TEXCOORD0;
                float3 wpos : TEXCOORD1;
                fixed4 color : COLOR;
            };

            fixed4 _Color;
            float _UseVertexColor;
            float _RimPower;

            // set every frame from WorldStyle (won't be stripped: set via Shader.SetGlobalVector)
            float3 WorldStyleSunDir;
            float3 WorldStyleSunColor;
            float3 WorldStyleSkyTint;
            float3 WorldStyleGroundTint;
            float WorldStyleShadow;
            float WorldStyleRim;
            float WorldStyleStepCount;
            float WorldStyleSpecular;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.wpos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 N = normalize(i.normal);
                float3 L = normalize(WorldStyleSunDir);
                float ndl = dot(N, L);

                // soft step between lit and shade
                float lit = smoothstep(-0.08, 0.25, ndl);

                // optional second step for a cel hint
                float steps = max(WorldStyleStepCount, 1);
                if (steps > 1.5)
                {
                    float q = floor(lit * steps) / (steps - 1.0);
                    lit = lerp(lit, q, 0.55);
                }

                float3 albedo = _Color.rgb;
                if (_UseVertexColor > 0.5) albedo *= i.color.rgb;

                float3 sky = WorldStyleSkyTint;
                float3 ground = WorldStyleGroundTint;
                float upness = N.y * 0.5 + 0.5;

                float3 lighting = lerp(ground, sky, upness) * WorldStyleShadow;
                lighting += WorldStyleSunColor * lit;

                // specular pop on smooth surfaces
                float3 V = normalize(_WorldSpaceCameraPos - i.wpos);
                float3 H = normalize(L + V);
                float spec = pow(max(dot(N, H), 0.0), 64.0) * WorldStyleSpecular * lit;

                // rim light on silhouette edges
                float rim = pow(1.0 - saturate(dot(N, V)), _RimPower) * WorldStyleRim;
                float rimMask = smoothstep(0.0, 0.3, ndl + 0.4);
                float3 rimCol = WorldStyleSunColor * rim * rimMask;

                float3 col = albedo * lighting + spec + rimCol;
                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }
    Fallback "Legacy Shaders/Diffuse"
}
