Shader "Custom/CRTPostProcess"
{
    Properties
    {
        _CurvatureX      ("Curvature X", Range(0, 10)) = 4
        _CurvatureY      ("Curvature Y", Range(0, 10)) = 4
        _ScanlineIntensity ("Scanline Intensity", Range(0,1)) = 0.5
        _ScanlineCount   ("Scanline Count", Float) = 240
        _Aberration      ("Chromatic Aberration", Range(0,0.02)) = 0.003
        _Vignette        ("Vignette Strength", Range(0,2)) = 1.0
        _PixelSize       ("Pixel Downscale (PS1 res)", Range(1,8)) = 4
        _ColorDepth      ("Color Depth (bits per channel)", Range(1,8)) = 5
        _Brightness      ("Brightness", Range(0,2)) = 1.1
    }

    SubShader
    {
        // Fullscreen blit pass, meant to be used with URP's
        // "Full Screen Pass Renderer Feature" (URP 14 / Unity 2022.2+).
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            Name "CRTPostProcess"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            // URP's Core.hlsl defines the TEXTURE2D_X macro family that Blit.hlsl relies on
            // but does not itself include - must come first or Blit.hlsl fails to compile.
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            // _BlitTexture is bound globally by URP's blitter (not a material Properties
            // texture), so no matching "sampler_BlitTexture" is ever auto-generated.
            // Use one of the engine-provided global samplers instead (declared in
            // GlobalSamplers.hlsl, included transitively via Blit.hlsl).
            #define sampler_BlitTexture sampler_LinearClamp

            float _CurvatureX;
            float _CurvatureY;
            float _ScanlineIntensity;
            float _ScanlineCount;
            float _Aberration;
            float _Vignette;
            float _PixelSize;
            float _ColorDepth;
            float _Brightness;

            #define PI 3.14159265359

            float2 Curve(float2 uv)
            {
                uv = uv * 2.0 - 1.0;
                float2 offset = abs(uv.yx) / float2(max(_CurvatureX, 0.001), max(_CurvatureY, 0.001));
                uv = uv + uv * offset * offset;
                uv = uv * 0.5 + 0.5;
                return uv;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // // Debug
                // return half4(1, 0, 0, 1);

                float2 uv = input.texcoord;

                // --- PS1-style low internal resolution ---
                float2 texSize = _BlitTexture_TexelSize.zw;
                float2 pixelStep = _PixelSize / texSize;
                uv = (floor(uv / pixelStep) + 0.5) * pixelStep;

                // --- CRT barrel curvature ---
                float2 curvedUV = Curve(uv);

                if (curvedUV.x < 0.0 || curvedUV.x > 1.0 || curvedUV.y < 0.0 || curvedUV.y > 1.0)
                    return half4(0, 0, 0, 1);

                // --- Chromatic aberration (RGB split toward edges) ---
                float2 dir = curvedUV - 0.5;
                half3 sourceColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, curvedUV).rgb;
                float edgeAmount = saturate(length(dir) * 2.0);
                half3 redShift = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, curvedUV - dir * _Aberration * edgeAmount).rgb;
                half3 blueShift = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, curvedUV + dir * _Aberration * edgeAmount).rgb;
                half3 col = sourceColor;
                col.r = lerp(sourceColor.r, redShift.r, edgeAmount);
                col.b = lerp(sourceColor.b, blueShift.b, edgeAmount);
                col = saturate(col);

                // --- Color depth reduction (PS1's limited color precision) ---
                float levels = exp2(_ColorDepth) - 1.0;
                col = floor(col * levels + 0.5) / levels;

                // --- Scanlines ---
                float scan = sin(curvedUV.y * _ScanlineCount * PI * 2.0) * 0.5 + 0.5;
                col *= lerp(1.0, scan, _ScanlineIntensity);

                // --- Vignette ---
                float vig = 1.0 - dot(dir, dir) * _Vignette;
                col *= saturate(vig);

                col *= _Brightness;

                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
}
