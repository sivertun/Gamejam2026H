Shader "Custom/URP_DissolveEmissive"
{
    Properties
    {
        [MainColor] _BaseColor      ("Base Color", Color) = (1,1,1,1)
        [MainTexture] _BaseMap      ("Base Map (Albedo)", 2D) = "white" {}

        _BumpMap        ("Normal Map", 2D) = "bump" {}
        _BumpScale      ("Normal Scale", Float) = 1.0

        _MetallicGlossMap ("Metallic (R) Smoothness (A)", 2D) = "white" {}
        _Metallic       ("Metallic", Range(0,1)) = 0.0
        _Smoothness     ("Smoothness", Range(0,1)) = 0.5

        _OcclusionMap   ("Occlusion Map", 2D) = "white" {}
        _OcclusionStrength ("Occlusion Strength", Range(0,1)) = 1.0

        _EmissionMap    ("Emission Map", 2D) = "white" {}
        [HDR] _EmissionColor ("Emission Color", Color) = (0,0,0,1)

        [Space(10)]
        _DissolveAmount ("Dissolve Amount", Range(0,1)) = 0
        _NoiseTex       ("Dissolve Noise", 2D) = "white" {}

        _EdgeWidth      ("Edge Width", Range(0.001, 0.5)) = 0.05
        [HDR] _EdgeColor ("Edge Emissive Color", Color) = (0, 3, 6, 1)
        _EdgeIntensity  ("Edge Emissive Intensity", Range(0, 20)) = 3
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // URP keywords so the object reacts correctly to lighting/shadows,
            // same as the default Lit shader
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
                float3 normalWS    : TEXCOORD2;
                float4 tangentWS   : TEXCOORD3; // w = sign, for bitangent reconstruction
                float fogCoord     : TEXCOORD4;
            };

            TEXTURE2D(_BaseMap);          SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap);          SAMPLER(sampler_BumpMap);
            TEXTURE2D(_MetallicGlossMap); SAMPLER(sampler_MetallicGlossMap);
            TEXTURE2D(_OcclusionMap);     SAMPLER(sampler_OcclusionMap);
            TEXTURE2D(_EmissionMap);      SAMPLER(sampler_EmissionMap);
            TEXTURE2D(_NoiseTex);         SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _BumpScale;
                float _Metallic;
                float _Smoothness;
                float _OcclusionStrength;
                float4 _EmissionColor;

                float _DissolveAmount;
                float _EdgeWidth;
                float4 _EdgeColor;
                float _EdgeIntensity;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);

                OUT.positionHCS = positionInputs.positionCS;
                OUT.positionWS  = positionInputs.positionWS;
                OUT.normalWS    = normalInputs.normalWS;
                real sign = IN.tangentOS.w * GetOddNegativeScale();
                OUT.tangentWS   = float4(normalInputs.tangentWS.xyz, sign);
                OUT.uv          = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.fogCoord    = ComputeFogFactor(positionInputs.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // ---- Dissolve ----
                float noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, IN.uv).r;

                // Core dissolve cutout: pixels where noise < _DissolveAmount are gone
                clip(noise - _DissolveAmount);

                // Clamp edge width to current dissolve progress so it can't bleed
                // into undissolved geometry when _DissolveAmount is near 0
                float effectiveEdgeWidth = max(min(_EdgeWidth, _DissolveAmount), 0.0001);
                float edgeFactor = 1.0 - saturate((noise - _DissolveAmount) / effectiveEdgeWidth);

                // ---- Standard Lit-style surface sampling ----
                half4 baseTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;

                half4 metallicGloss = SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, IN.uv);
                half metallic = metallicGloss.r * _Metallic;
                half smoothness = metallicGloss.a * _Smoothness;

                half occlusionSample = SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, IN.uv).g;
                half occlusion = lerp(1.0, occlusionSample, _OcclusionStrength);

                half3 emissionMap = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, IN.uv).rgb;
                half3 materialEmission = emissionMap * _EmissionColor.rgb;

                // Normal mapping (tangent space -> world space)
                half4 normalSample = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, IN.uv);
                half3 normalTS = UnpackNormalScale(normalSample, _BumpScale);

                float3 normalWS = normalize(IN.normalWS);
                float3 tangentWS = normalize(IN.tangentWS.xyz);
                float3 bitangentWS = cross(normalWS, tangentWS) * IN.tangentWS.w;
                half3x3 tangentToWorld = half3x3(tangentWS, bitangentWS, normalWS);
                float3 finalNormalWS = normalize(mul(normalTS, tangentToWorld));

                // ---- Lighting ----
                InputData lightingInput = (InputData)0;
                lightingInput.positionWS = IN.positionWS;
                lightingInput.normalWS = finalNormalWS;
                lightingInput.viewDirectionWS = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                lightingInput.shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                lightingInput.fogCoord = IN.fogCoord;

                // Ambient/GI (spherical harmonics) - without this, unlit areas go pure black
                lightingInput.bakedGI = SampleSH(finalNormalWS);

                SurfaceData surfaceInput = (SurfaceData)0;
                surfaceInput.albedo = baseTex.rgb;
                surfaceInput.alpha = baseTex.a;
                surfaceInput.metallic = metallic;
                surfaceInput.smoothness = smoothness;
                surfaceInput.occlusion = occlusion;
                surfaceInput.normalTS = normalTS;

                // Material's own emission map/color, plus the dissolve edge glow on top
                surfaceInput.emission = materialEmission + (_EdgeColor.rgb * _EdgeIntensity * edgeFactor);

                half4 color = UniversalFragmentPBR(lightingInput, surfaceInput);
                color.rgb = MixFog(color.rgb, IN.fogCoord);
                color.a = 1.0;
                return color;
            }
            ENDHLSL
        }

        // Needed so the object still casts shadows correctly while dissolving
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _BumpScale;
                float _Metallic;
                float _Smoothness;
                float _OcclusionStrength;
                float4 _EmissionColor;

                float _DissolveAmount;
                float _EdgeWidth;
                float4 _EdgeColor;
                float _EdgeIntensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            float3 _LightDirection;

            Varyings ShadowVert(Attributes IN)
            {
                Varyings OUT;
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                OUT.positionHCS = positionCS;
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 ShadowFrag(Varyings IN) : SV_Target
            {
                float noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, IN.uv).r;
                clip(noise - _DissolveAmount);
                return 0;
            }
            ENDHLSL
        }

        // Needed for depth prepass / SSAO / depth-based effects, mirroring default Lit
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _BumpScale;
                float _Metallic;
                float _Smoothness;
                float _OcclusionStrength;
                float4 _EmissionColor;

                float _DissolveAmount;
                float _EdgeWidth;
                float4 _EdgeColor;
                float _EdgeIntensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings DepthVert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 DepthFrag(Varyings IN) : SV_Target
            {
                float noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, IN.uv).r;
                clip(noise - _DissolveAmount);
                return 0;
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
