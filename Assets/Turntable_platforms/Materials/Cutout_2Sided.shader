Shader "Custom/URP/Cutout_2Sided"
{
    Properties
    {
        _BaseColor ("Main Color", Color) = (1,1,1,1)
        _BaseMap ("Base (RGB) Trans (A)", 2D) = "white" {}
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5
        _Intensity ("Emission Intensity", Range(0,5)) = 0.5
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "Queue"="AlphaTest"
            "RenderType"="TransparentCutout"
        }

        Cull Off

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // URP lighting keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _BaseMap_ST;
                float _Cutoff;
                float _Intensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float2 uv         : TEXCOORD2;
                half   fogCoord   : TEXCOORD3;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                VertexPositionInputs posInputs = GetVertexPositionInputs(v.positionOS.xyz);
                VertexNormalInputs   nrmInputs = GetVertexNormalInputs(v.normalOS);

                o.positionCS = posInputs.positionCS;
                o.positionWS = posInputs.positionWS;
                o.normalWS   = nrmInputs.normalWS;
                o.uv         = TRANSFORM_TEX(v.uv, _BaseMap);
                o.fogCoord   = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;

                // Alpha cutout
                clip(tex.a - _Cutoff);

                // Lambert lighting
                float3 N = NormalizeNormalPerPixel(i.normalWS);
                float3 V = SafeNormalize(GetWorldSpaceViewDir(i.positionWS));

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half3 diffuse = tex.rgb * mainLight.color * saturate(dot(N, mainLight.direction));

                #if defined(_ADDITIONAL_LIGHTS)
                uint lightCount = GetAdditionalLightsCount();
                for (uint li = 0; li < lightCount; li++)
                {
                    Light l = GetAdditionalLight(li, i.positionWS);
                    diffuse += tex.rgb * l.color * saturate(dot(N, l.direction)) * l.distanceAttenuation * l.shadowAttenuation;
                }
                #endif

                // Emission like your old shader
                half3 emission = tex.rgb * _Intensity;

                half3 col = diffuse + emission;

                col = MixFog(col, i.fogCoord);
                return half4(col, 1.0h);
            }
            ENDHLSL
        }

        // Cutout shadows (important!)
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _BaseMap_ST;
                float _Cutoff;
                float _Intensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 normalWS   = TransformObjectToWorldNormal(v.normalOS);

                // URP helper for shadow caster position
                o.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, 0));
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;
                clip(tex.a - _Cutoff);
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
