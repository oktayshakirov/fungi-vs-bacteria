Shader "Fungi/NaturalPath"
{
    Properties
    {
        [MainTexture] _BaseMap("Mineral grain", 2D) = "white" {}
        [MainColor] _BaseColor("Road color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-100" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #define _SURFACE_TYPE_TRANSPARENT 1
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half fog : TEXCOORD1; float3 positionWS : TEXCOORD2; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half grain = SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv * _BaseMap_ST.xy).r;
                half patch = SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,float2(input.uv.x*.07, input.uv.y*.35+.23)).r;
                // Fade directly into the real ground, with no separate dark outline.
                half edgeDistance = min(input.uv.y,1-input.uv.y);
                half irregularity = clamp((patch-.93)*.8,-.025,.025);
                half alpha = smoothstep(.015,.14,edgeDistance+irregularity);
                half centre = smoothstep(.12,.42,edgeDistance);
                half wear = saturate((patch-.88)*8) * centre;
                half3 color = _BaseColor.rgb * (grain*.94 + wear*.10);
                // Reuse the existing sun shadow map so shadows continue across
                // the feathered road instead of stopping at its edge.
                Light sun = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                color *= lerp(.35h,1.h,sun.shadowAttenuation);
                return half4(MixFog(color,input.fog),alpha*_BaseColor.a);
            }
            ENDHLSL
        }
    }
}
