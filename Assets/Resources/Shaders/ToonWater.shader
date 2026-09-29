Shader "PokeProfessions/ToonWater"
{
    Properties
    {
        _Color ("Shallow Color", Color) = (0.29, 0.56, 0.76, 0.85)
        _DeepColor ("Deep Color", Color) = (0.13, 0.3, 0.48, 0.9)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 wpos : TEXCOORD0;
            };

            fixed4 _Color;
            fixed4 _DeepColor;

            float3 WorldStyleSunDir;
            float3 WorldStyleSunColor;

            v2f vert(appdata v)
            {
                v2f o;
                float3 p = v.vertex.xyz;
                // gentle vertex waves
                float w = sin(p.x * 0.8 + _Time.y * 1.6) * cos(p.z * 0.7 + _Time.y * 1.1);
                p.y += w * 0.06;
                o.pos = UnityObjectToClipPos(float4(p, 1.0));
                o.wpos = mul(unity_ObjectToWorld, float4(p, 1.0)).xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // banded depth fake: distance from center of the lake plane
                float d = length(i.wpos.xz - float2(-8.0, -78.0));
                float band = smoothstep(6.0, 26.0, d);
                fixed4 c = lerp(_Color, _DeepColor, band);

                // moving sparkle highlights
                float sp = sin(i.wpos.x * 2.1 + _Time.y * 2.0) * sin(i.wpos.z * 1.7 - _Time.y * 1.4);
                c.rgb += WorldStyleSunColor * smoothstep(0.86, 0.99, sp) * 0.5;

                // soft shoreline foam ring
                float foam = smoothstep(30.0, 27.5, d) * smoothstep(25.5, 27.0, d);
                c.rgb += foam * 0.5;

                return c;
            }
            ENDCG
        }
    }
    Fallback Off
}
