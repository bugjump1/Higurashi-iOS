Shader "Higurashi/LayerFilter"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _FilterRR ("Filter RR", Float) = 1
        _FilterRG ("Filter RG", Float) = 0
        _FilterRB ("Filter RB", Float) = 0
        _FilterGR ("Filter GR", Float) = 0
        _FilterGG ("Filter GG", Float) = 1
        _FilterGB ("Filter GB", Float) = 0
        _FilterBR ("Filter BR", Float) = 0
        _FilterBG ("Filter BG", Float) = 0
        _FilterBB ("Filter BB", Float) = 1
        _FilterAlpha ("Filter alpha", Range(0, 1)) = 1
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
            float _FilterRR, _FilterRG, _FilterRB;
            float _FilterGR, _FilterGG, _FilterGB;
            float _FilterBR, _FilterBG, _FilterBB;
            float _FilterAlpha;

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
                float3 rgb = color.rgb;
                color.r = dot(rgb, float3(_FilterRR, _FilterRG, _FilterRB));
                color.g = dot(rgb, float3(_FilterGR, _FilterGG, _FilterGB));
                color.b = dot(rgb, float3(_FilterBR, _FilterBG, _FilterBB));
                color.a *= saturate(_FilterAlpha);
                return color;
            }
            ENDCG
        }
    }
}
