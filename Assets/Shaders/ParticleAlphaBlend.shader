Shader "Custom/ParticleAlphaBlend"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map (RGB=Color, A=Alpha)", 2D) = "white" {}
        [MainColor] _BaseColor("Tint", Color) = (1, 1, 1, 1)
        _FadeDistance("Soft Particle Fade Distance", Range(0.01, 10.0)) = 1.0
        _CameraOffset("Camera Offset", Range(-5.0, 5.0)) = 0.0
        [HDR] _EmissionColor("Emission Color", Color) = (0, 0, 0, 1)
        [Toggle(_USE_R_CHANNEL_MASK)] _UseRChannelMask("Use Base Map R Channel as Mask", Float) = 0

        [Toggle(_DISSOLVE)] _UseDissolve("Use Dissolve", Float) = 0
        _DissolveMap("Dissolve Map (R = Threshold)", 2D) = "white" {}
        _DissolveAmount("Dissolve Amount", Range(0.0, 1.0)) = 0.0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _USE_R_CHANNEL_MASK
            #pragma shader_feature_local _DISSOLVE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float4 screenPos   : TEXCOORD0;
                float2 uv          : TEXCOORD1;
                float4 color       : TEXCOORD2;
                float  eyeDepth    : TEXCOORD3;
                #if defined(_DISSOLVE)
                    float2 dissolveUV : TEXCOORD4;
                #endif
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_DissolveMap);
            SAMPLER(sampler_DissolveMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                float _FadeDistance;
                float _CameraOffset;
                half4 _EmissionColor;
                float4 _DissolveMap_ST;
                float _DissolveAmount;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);

                // View space: camera looks down -Z, so pushing Z up moves the vertex
                // toward the camera. Re-project after offsetting so the particle
                // actually renders nearer (not just a fade-math bias).
                float3 positionVS = posInputs.positionVS;
                positionVS.z += _CameraOffset;

                OUT.positionHCS = TransformWViewToHClip(positionVS);
                OUT.screenPos = ComputeScreenPos(OUT.positionHCS);
                OUT.eyeDepth = -positionVS.z;
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                #if defined(_USE_R_CHANNEL_MASK)
                    // Treat the map as a single-channel mask (e.g. a soft grayscale
                    // sprite) - R drives both color and alpha, tint comes from _BaseColor.
                    texColor = texColor.rrrr;
                #endif
                half4 col = texColor * _BaseColor * IN.color;

                // --- Soft particles: fade alpha out near intersections with scene geometry ---
                float2 screenUV = IN.screenPos.xy / IN.screenPos.w;
                float rawDepth = SampleSceneDepth(screenUV);
                float sceneEyeDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
                float fade = saturate((sceneEyeDepth - IN.eyeDepth) / max(_FadeDistance, 0.0001));
                col.a *= fade;

                // --- Emission: additive glow, masked by the texture, fades with the
                // --- particle's own alpha and the soft-particle fade so it doesn't
                // --- poke through once the particle itself has faded out ---
                col.rgb += texColor.rgb * _EmissionColor.rgb * IN.color.a * fade;

                return col;
            }
            ENDHLSL
        }
    }
}
