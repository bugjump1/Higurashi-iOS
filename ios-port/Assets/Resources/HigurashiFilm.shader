Shader "Higurashi/Film"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _FilmType ("Film type", Float) = 0
        _FilmColor ("Film color", Color) = (1, 1, 1, 1)
        _FilmStrength ("Film strength", Range(0, 1)) = 0
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
            float4 _MainTex_ST;
            float _FilmType;
            float4 _FilmColor;
            float _FilmStrength;

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
                if (_FilmType < 1.5)
                {
                    color.rgb = lerp(color.rgb, _FilmColor.rgb, saturate(_FilmStrength));
                }
                else if (_FilmType < 2.5)
                {
                    float luminance = dot(color.rgb, float3(0.299, 0.587, 0.114));
                    color.rgb = lerp(color.rgb, luminance.xxx, saturate(_FilmStrength));
                }
                else if (_FilmType < 3.5)
                {
                    color.rgb = lerp(color.rgb, 1.0 - color.rgb, saturate(_FilmStrength));
                }
                return color;
            }
            ENDCG
        }
    }
}
