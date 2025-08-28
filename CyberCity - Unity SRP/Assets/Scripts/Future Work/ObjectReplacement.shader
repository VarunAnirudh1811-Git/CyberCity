Shader "Hidden/ObjectReplacement"
{
    Properties
    {
        _MainTex ("Main Tex", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _ContrastObjectID ("ContrastObjectID", Range(0,1)) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "DisableBatching"="True" }
        // No lighting, no shadows, no fog — simple unlit pass.
        Cull Off
        ZWrite On
        ZTest LEqual

        Pass
        {
            Name "REPLACE_UNLIT"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            float _ContrastObjectID;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex); // required transform
                o.uv  = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            // Manual sRGB -> Linear per-channel conversion
            // Using a standard gamma approximation of 2.2
            float3 SRGBToLinear(float3 srgb)
            {
                // clamp to [0,1] defensive
                srgb = saturate(srgb);
                return float3(
                    pow(srgb.r, 2.2),
                    pow(srgb.g, 2.2),
                    pow(srgb.b, 2.2)
                );
            }

            float4 frag (v2f i) : SV_Target
            {
                float4 sampled = tex2D(_MainTex, i.uv) * _Color; // sample & multiply by _Color (RGBA)
                float3 linearRGB = SRGBToLinear(sampled.rgb);
                // Output: RGB = linear color, A = ContrastObjectID
                return float4(linearRGB, _ContrastObjectID);
            }
            ENDCG
        } // Pass
    } // SubShader

    // No Fallback, as requested.
}
