Shader "MRStrategyRTS/Towers/StoneMetal"
{
    Properties
    {
        _MainTex ("Stone / Metal / Crystal / Iron Atlas", 2D) = "white" {}
        _SurfaceTex ("Surface Normal RGB + Roughness A", 2D) = "bump" {}
        _NormalStrength ("Surface Relief", Range(0, 2)) = 0.65
        [PerRendererData] _PurchaseUnavailable ("Purchase Unavailable", Float) = 0
        [PerRendererData] _PurchaseAvailable ("Purchase Available", Float) = 0
        [PerRendererData] _PurchaseAccent ("Purchase Accent", Color) = (0,0.85,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            sampler2D _SurfaceTex;
            half _NormalStrength;
            UNITY_INSTANCING_BUFFER_START(Purchase)
                UNITY_DEFINE_INSTANCED_PROP(float, _PurchaseUnavailable)
                UNITY_DEFINE_INSTANCED_PROP(float, _PurchaseAvailable)
                UNITY_DEFINE_INSTANCED_PROP(float4, _PurchaseAccent)
            UNITY_INSTANCING_BUFFER_END(Purchase)
            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                fixed4 color : COLOR;
                float4 tangent : TANGENT;
                float2 uv : TEXCOORD0;
                float2 surface : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct v2f
            {
                float4 position : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 tile : TEXCOORD1;
                half3 normal : TEXCOORD2;
                half3 tangent : TEXCOORD3;
                half3 bitangent : TEXCOORD4;
                float3 worldPosition : TEXCOORD5;
                half unavailable : TEXCOORD6;
                half available : TEXCOORD7;
                half3 accent : TEXCOORD8;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            v2f vert(appdata input)
            {
                v2f output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_OUTPUT(v2f, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.position = UnityObjectToClipPos(input.vertex);
                output.color = input.color;
                output.uv = input.uv;
                output.tile = float2(fmod(input.surface.x, 2), floor(input.surface.x / 2));
                output.normal = UnityObjectToWorldNormal(input.normal);
                output.tangent = UnityObjectToWorldDir(input.tangent.xyz);
                output.bitangent = cross(output.normal, output.tangent)
                    * input.tangent.w * unity_WorldTransformParams.w;
                output.worldPosition = mul(unity_ObjectToWorld, input.vertex).xyz;
                output.unavailable = UNITY_ACCESS_INSTANCED_PROP(Purchase, _PurchaseUnavailable);
                output.available = UNITY_ACCESS_INSTANCED_PROP(Purchase, _PurchaseAvailable);
                output.accent = UNITY_ACCESS_INSTANCED_PROP(Purchase, _PurchaseAccent).rgb;
                return output;
            }
            fixed4 frag(v2f input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                // Inset each atlas tile so mip filtering cannot sample the next material.
                float2 uv = input.tile * 0.5 + 0.008 + frac(input.uv) * 0.484;
                float2 dx = ddx(input.uv) * 0.484;
                float2 dy = ddy(input.uv) * 0.484;
                half3 albedo = tex2Dgrad(_MainTex, uv, dx, dy).rgb * input.color.rgb;
                half4 surface = tex2Dgrad(_SurfaceTex, uv, dx, dy);
                half3 detail = surface.rgb * 2 - 1;
                detail.xy *= _NormalStrength;
                half3 normal = normalize(input.tangent * detail.x
                    + input.bitangent * detail.y + input.normal * detail.z);
                half3 lightDir = normalize(half3(-0.4, 0.8, 0.6));
                half diffuse = 0.36h + 0.64h * saturate(dot(normal, lightDir));
                half3 viewDir = normalize(_WorldSpaceCameraPos.xyz - input.worldPosition);
                half3 halfway = normalize(lightDir + viewDir);
                half specular = pow(saturate(dot(normal, halfway)),
                    lerp(72.0h, 10.0h, surface.a)) * (1 - surface.a) * 0.65h;
                half rim = pow(1 - saturate(dot(normal, viewDir)), 3) * 0.12h;
                half3 lit = albedo * diffuse + specular * lerp(half3(1,1,1), input.color.rgb, 0.6h);
                half3 result = lit + albedo * input.color.a * 0.75h + albedo * rim;
                // Only shop stock receives the boost. Placed towers retain their authored look.
                half luminance = dot(result, half3(0.2126h, 0.7152h, 0.0722h));
                half3 vivid = max(0, lerp(luminance.xxx, result, 1.65h));
                half chroma = max(input.color.r, max(input.color.g, input.color.b))
                    - min(input.color.r, min(input.color.g, input.color.b));
                half coloredPart = saturate(chroma * 2 + input.color.a);
                half3 accentLit = input.accent * (0.55h + 0.45h * diffuse);
                vivid = lerp(vivid, accentLit, coloredPart * 0.48h) * 1.22h;
                // Preserve hue instead of clipping bright crystal highlights to white.
                vivid /= max(1.0h, max(vivid.r, max(vivid.g, vivid.b)));
                vivid += input.accent * rim * 1.5h;
                vivid /= max(1.0h, max(vivid.r, max(vivid.g, vivid.b)));
                result = lerp(result, vivid, saturate(input.available));
                // A subdued neutral edge preserves the silhouette on dark backgrounds.
                // Disabled stock has no colored emission or specular highlight.
                half gray = 0.085h + saturate(dot(albedo * diffuse,
                    half3(0.2126h, 0.7152h, 0.0722h))) * 0.20h + rim * 0.65h;
                return fixed4(lerp(result, gray.xxx, saturate(input.unavailable)), 1);
            }
            ENDCG
        }
    }
}
