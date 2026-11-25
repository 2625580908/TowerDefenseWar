Shader "Custom/Wireframe_GS"
{
    Properties
    {
        _WireColor     ("线框颜色", Color) = (0, 1, 0.5, 1)
        _FillColor     ("填充颜色", Color) = (0.05, 0.05, 0.05, 1)
        _WireThickness ("线宽(像素)", Range(0.5, 10)) = 2.0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" }

        Pass
        {
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            fixed4 _WireColor;
            fixed4 _FillColor;
            float  _WireThickness;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 bary : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 bary : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.bary = v.bary;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 d  = fwidth(i.bary);
                float3 a3 = smoothstep(float3(0, 0, 0), d * _WireThickness, i.bary);
                float edge = min(min(a3.x, a3.y), a3.z);
                return lerp(_WireColor, _FillColor, edge);
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
