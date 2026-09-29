Shader "PokeProfessions/UnlitColor"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
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

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 normal : TEXCOORD0;
                float3 wpos : TEXCOORD1;
            };

            fixed4 _Color;

            float3 WorldStyleSunDir;
            float3 WorldStyleSunColor;
            float3 WorldStyleSkyTint;
            float3 WorldStyleGroundTint;
            float WorldStyleShadow;
            float WorldStyleRim;
            float WorldStyleSpecular;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.wpos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 N = normalize(i.normal);
                float3 L = normalize(WorldStyleSunDir);
                float ndl = dot(N, L);
                float lit = smoothstep(-0.08, 0.25, ndl);

                float upness = N.y * 0.5 + 0.5;
                float3 lighting = lerp(WorldStyleGroundTint, WorldStyleSkyTint, upness) * WorldStyleShadow;
                lighting += WorldStyleSunColor * lit;

                float3 V = normalize(_WorldSpaceCameraPos - i.wpos);
                float3 H = normalize(L + V);
                float spec = pow(max(dot(N, H), 0.0), 64.0) * WorldStyleSpecular * lit;
                float rim = pow(1.0 - saturate(dot(N, V)), 3.0) * WorldStyleRim;
                float rimMask = smoothstep(-0.2, 0.3, ndl);
                float3 rimCol = WorldStyleSunColor * rim * rimMask;

                return fixed4(_Color.rgb * lighting + spec + rimCol, 1.0);
            }
            ENDCG
        }
    }
    Fallback "Legacy Shaders/Diffuse"
}
