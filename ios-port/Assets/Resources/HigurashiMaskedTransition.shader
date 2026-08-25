Shader "Higurashi/MaskedTransition"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _MaskTex ("Mask", 2D) = "black" {}
        _Progress ("Progress", Range(0, 1)) = 0
        _Fuzziness ("Fuzziness", Range(0.001, 1)) = 0.45
        _NegativeStrength ("Negative strength", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _MaskTex;
            float4 _MainTex_ST;
            float _Progress;
            float _Fuzziness;
            float _NegativeStrength;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                fixed4 color = tex2D(_MainTex, input.uv) * input.color;
                color.rgb = lerp(color.rgb, 1.0 - color.rgb, saturate(_NegativeStrength));
                float maskValue = tex2D(_MaskTex, input.uv).r;
                float reveal = saturate((_Progress - maskValue) / _Fuzziness + 1.0);
                color.a *= reveal;
                return color;
            }
            ENDCG
        }
    }
}
