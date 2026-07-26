Shader "Custom/ToonShader"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}

        _LightTint("Light Tint", Color) = (1, 1, 1, 1)
        _MidTint("Mid Tint", Color) = (0.28, 0.9, 0.35, 1)
        _ShadowTint("Shadow Tint", Color) = (0.0, 0.45, 0.48, 1)
        _SpecularColor("Specular Color", Color) = (1, 1, 1, 1)

        _LightBand("Light Band", Range(0.0, 1.0)) = 0.60
        _MidBand("Mid Band", Range(0.0, 1.0)) = 0.25
        _SpecularIntensity("Specular Intensity", Range(0.0, 2.0)) = 0.35
        _SpecularBand("Specular Band", Range(0.0, 1.0)) = 0.70
        _BandBlend("Band Blend", Range(0.0, 0.5)) = 0.05

        _HalftoneCellSize("Halftone Dot Size (px)", Range(2.0, 40.0)) = 10.0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

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
                float4 positionHCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float4 _BaseMap_ST;
                half4 _LightTint;
                half4 _MidTint;
                half4 _ShadowTint;
                half4 _SpecularColor;
                float _LightBand;
                float _MidBand;
                float _BandBlend;
                float _SpecularIntensity;
                float _SpecularBand;
                float _HalftoneCellSize;
            CBUFFER_END

            // Screen-space halftone dot: value (0..1) controls how much of the
            // grid cell the dot covers, so a smooth blend factor reads as a
            // growing/shrinking dot instead of a soft gradient.
            float Halftone(float2 screenPos, float value, float cellSize)
            {
                // Outside the blend band, value is pinned to exactly 0 or 1 by the
                // caller - return flat coverage directly instead of letting the dot
                // grid's own edge math leak a faint always-on dot into solid areas.
                if (value <= 0.0) return 0.0;
                if (value >= 1.0) return 1.0;

                float2 grid = screenPos / max(cellSize, 0.001);
                float2 cell = frac(grid) - 0.5;
                float dist = length(cell);
                float radius = value * 0.70710678;
                float aa = max(fwidth(dist), 0.001) * 1.5;
                return smoothstep(radius + aa, radius - aa, dist);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.normalWS = normalize(TransformObjectToWorldNormal(IN.normalOS));
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                Light mainLight = GetMainLight();
                float diffuse = saturate(dot(IN.normalWS, mainLight.direction));

                float shadowEdge = 1.0 - _LightBand - _MidBand;
                float midEdge = shadowEdge + _MidBand;

                float3 toonShade = _ShadowTint.rgb;
                float shadowToMid = saturate((diffuse - (shadowEdge - _BandBlend)) / (2.0 * _BandBlend));
                float midToLight = saturate((diffuse - (midEdge - _BandBlend)) / (2.0 * _BandBlend));

                float shadowToMidDot = Halftone(IN.positionHCS.xy, shadowToMid, _HalftoneCellSize);
                float midToLightDot = Halftone(IN.positionHCS.xy, midToLight, _HalftoneCellSize);

                toonShade = lerp(_ShadowTint.rgb, _MidTint.rgb, shadowToMidDot);
                toonShade = lerp(toonShade, _LightTint.rgb, midToLightDot);

                float3 viewDirWS = normalize(_WorldSpaceCameraPos - IN.positionWS);
                float3 halfDir = normalize(mainLight.direction + viewDirWS);
                float specTerm = pow(saturate(dot(IN.normalWS, halfDir)), 32.0);
                float specMask = step(_SpecularBand, specTerm);
                float3 specular = specMask * _SpecularColor.rgb * _SpecularIntensity * mainLight.color;

                half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;
                half3 finalColor = texColor.rgb * toonShade + specular;
                return half4(finalColor, texColor.a);
            }
            ENDHLSL
        }
    }
}
