// Unlit textured shader for third-party kit props (Quaternius nature kit).
// Matches the game's WorldStyle lighting (sun + sky/ground tint) but takes a
// uniform tint instead of vertex colors, and alpha-clips leaf cards.
Shader "PokeProfessions/KitProp"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Tint ("Tint", Color) = (1,1,1,1)
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
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
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 normal : TEXCOORD0;
                float3 wpos : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Tint;
            fixed _Cutoff;

            float3 WorldStyleSunDir;
            float3 WorldStyleSunColor;
            float3 WorldStyleSkyTint;
            float3 WorldStyleGroundTint;
            float WorldStyleShadow;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.wpos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);
                clip(tex.a - _Cutoff);

                float3 N = normalize(i.normal);
                float ndl = dot(N, normalize(WorldStyleSunDir));
                float lit = smoothstep(-0.15, 0.35, ndl);

                float3 albedo = tex.rgb * _Tint.rgb;

                float upness = N.y * 0.5 + 0.5;
                float3 lighting = lerp(WorldStyleGroundTint, WorldStyleSkyTint, upness) * WorldStyleShadow;
                lighting += WorldStyleSunColor * lit;

                return fixed4(albedo * lighting, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
