// URP replacement for RCC Pro Lite's built-in-only RCCP_Shader_WheelBlur.
// RCCP_WheelBlurEditor.CreateNewMaterial() looks up Shader.Find("RCCP_WheelBlur_URP") when
// BCG_URP is defined, but the Lite package only ships the Built-in RP shader, so that lookup
// returned null and produced a magenta (shader-less) wheel blur material. The name below must
// stay exactly "RCCP_WheelBlur_URP", and the property names must match the Built-in shader so
// RCCP_WheelBlur keeps driving them at runtime.
Shader "RCCP_WheelBlur_URP"
{
    Properties
    {
        _WheelBlurTexture ("Wheel Blur Texture", 2D) = "white" {}
        _BlurIntensity ("Blur Intensity", Range(0, 1)) = 1
        _RimColor ("Rim Color", Color) = (1, 1, 1, 1)
        _Metallic ("Metallic", Range(0, 1)) = 0
        _Smoothness ("Smoothness", Range(0, 1)) = 0
        [Normal] _NormalMap ("Normal Map", 2D) = "bump" {}
        _NormalMapIntensity ("Normal Map Intensity", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            TEXTURE2D(_WheelBlurTexture);
            SAMPLER(sampler_WheelBlurTexture);

            // Declared for property parity with the Built-in shader; the blur quad is a thin
            // spinning billboard, so normal mapping and PBR response are not worth the cost.
            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _WheelBlurTexture_ST;
                float4 _NormalMap_ST;
                half4 _RimColor;
                half _BlurIntensity;
                half _Metallic;
                half _Smoothness;
                half _NormalMapIntensity;
            CBUFFER_END

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _WheelBlurTexture);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                half4 blur = SAMPLE_TEXTURE2D(_WheelBlurTexture, sampler_WheelBlurTexture, input.uv);
                half3 albedo = blur.rgb * _RimColor.rgb;

                float3 normalWS = normalize(input.normalWS);
                Light mainLight = GetMainLight();
                half3 lighting = SampleSH(normalWS) + mainLight.color * saturate(dot(normalWS, mainLight.direction));

                half alpha = blur.a * _BlurIntensity * _RimColor.a;
                return half4(albedo * lighting, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
