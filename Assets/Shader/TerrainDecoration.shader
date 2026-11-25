Shader "MRStrategyRTS/Terrain/Decoration"
{
    Properties
    {
        _WindAmplitude ("Leaf Sway", Range(0, .06)) = .028
        [HideInInspector] _BattleDim ("Battle Brightness", Range(0, 1)) = 1
        [HideInInspector] _PreviewTime ("Preview Time (-1 = Runtime)", Float) = -1
    }
    SubShader
    {
        // CPU batching bakes vertices to world space and would change the leaf sway scale.
        Tags { "RenderType"="Opaque" "DisableBatching"="True" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            half _WindAmplitude;
            half _BattleDim;
            float _PreviewTime;
            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 effects : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct v2f
            {
                float4 position : SV_POSITION;
                half3 normal : TEXCOORD0;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD1;
                half water : TEXCOORD2;
                float time : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            v2f vert(appdata input)
            {
                v2f output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_OUTPUT(v2f, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float t = _PreviewTime >= 0 ? _PreviewTime : _Time.y;
                float phase = unity_ObjectToWorld._m03 * 19 + unity_ObjectToWorld._m23 * 13;
                float wind = sin(t * 1.15 + phase);
                input.vertex.x += wind * _WindAmplitude * input.effects.x;
                input.vertex.z += cos(t * .91 + phase) * _WindAmplitude * .6 * input.effects.x;
                output.position = UnityObjectToClipPos(input.vertex);
                output.normal = UnityObjectToWorldNormal(input.normal);
                output.color = input.color;
                output.uv = input.uv;
                output.water = input.effects.y;
                output.time = t;
                return output;
            }
            fixed4 frag(v2f input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half diffuse = .58h + .42h * saturate(dot(normalize(input.normal),
                    normalize(half3(-.4h,.8h,.6h))));
                half grain = frac(sin(dot(floor(input.uv * 7), float2(12.9898,78.233))) * 43758.5453);
                half3 color = input.color.rgb * diffuse * (.96h + grain * .04h);
                half ripples = sin(input.uv.x * 8 + input.time * 1.35)
                    * cos(input.uv.y * 6 - input.time * .8);
                color += half3(.10h,.16h,.15h) * ripples * input.water * .26h;
                half pulse = .72h + .28h * sin(input.time * 1.6 + input.uv.x * 2);
                color += input.color.rgb * input.color.a * pulse * .45h;
                return fixed4(color * _BattleDim, 1);
            }
            ENDCG
        }
    }
}
