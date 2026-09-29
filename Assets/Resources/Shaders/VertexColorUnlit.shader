Shader "PokeProfessions/VertexColorUnlit"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
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
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 normal : TEXCOORD0;
                float3 wpos : TEXCOORD1;
                float2 uv : TEXCOORD2;
                fixed4 color : COLOR;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

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
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 N = normalize(i.normal);
                float ndl = dot(N, normalize(WorldStyleSunDir));
                float lit = smoothstep(-0.15, 0.35, ndl);

                float3 albedo = tex2D(_MainTex, i.uv).rgb * i.color.rgb;

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
