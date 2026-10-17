Shader "Custom/Pixel Water"
{
    Properties
    {
        [MainTexture] _MainTex ("Sprite (Mask)", 2D) = "white" {}

        [Header(Body)]
        _BodyTopColor ("Body Top Colour", Color) = (0.36, 0.72, 0.68, 1)
        _BodyBottomColor ("Body Bottom Colour", Color) = (0.16, 0.42, 0.45, 1)
        _GradientDepth ("Gradient Depth (px)", Float) = 48
        _BandCount ("Colour Bands", Range(2, 8)) = 4
        _DitherStrength ("Band Dither", Range(0, 1)) = 0.4
        _TopAlpha ("Top Transparency (alpha)", Range(0, 1)) = 0.55
        _TopFadeDepth ("Top Fade Depth (px)", Float) = 16
        _AlphaSteps ("Alpha Steps", Range(2, 8)) = 4

        [Header(Surface)]
        _SurfaceColor ("Surface Colour", Color) = (0.85, 0.97, 0.95, 1)
        _SurfaceThickness ("Surface Thickness (px)", Range(0, 6)) = 2
        _SurfaceOffset ("Waterline Below Top (px)", Float) = 8

        [Header(Waves)]
        _Amplitude ("Amplitude (px)", Float) = 3
        _Frequency ("Frequency (waves per 100px)", Float) = 2
        _SecondaryWave ("Secondary Wave", Range(0, 1)) = 0.5
        _WavePhase ("Wave Phase", Float) = 0
        _ScrollOffset ("Scroll Offset (px)", Float) = 0

        [Header(Back Layer)]
        _BackColor ("Back Colour", Color) = (0.6, 0.85, 0.82, 0.8)
        _BackRise ("Back Rise (px)", Float) = 3
        _BackAmplitudeScale ("Back Amplitude Scale", Range(0, 2)) = 1.2
        _BackFrequencyScale ("Back Frequency Scale", Range(0.25, 2)) = 0.7
        _BackPhaseOffset ("Back Phase Offset", Float) = 1.7
        _BackParallax ("Back Parallax", Range(0, 1)) = 0.6

        [Header(Ripples)]
        _RippleColor ("Ripple Colour", Color) = (0.7, 0.92, 0.9, 1)
        _RippleAlpha ("Ripple Strength", Range(0, 1)) = 0.85
        _RippleSpacing ("Ripple Row Spacing (px)", Range(2, 16)) = 5
        _RippleLength ("Ripple Length", Range(0, 1)) = 0.25
        _RippleSpeed ("Ripple Drift Speed (px/sec)", Float) = 6
        _RippleDepth ("Ripple Depth (px)", Float) = 40

        [Header(Glitter)]
        _GlitterColor ("Glitter Colour", Color) = (1, 1, 1, 1)
        _GlitterCell ("Glitter Cell Size (px)", Range(3, 32)) = 9
        _GlitterDensity ("Glitter Density", Range(0, 1)) = 0.65
        _GlitterDepth ("Glitter Depth (px)", Float) = 14
        _GlitterSpeed ("Glitter Twinkle Speed", Float) = 3
        _GlitterSharpness ("Glitter Sharpness", Range(1, 16)) = 6

        [Header(Pixels)]
        _PixelsPerUnit ("Pixels Per Unit", Float) = 16
        _TopY ("Top (world Y)", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _BodyTopColor;
                half4 _BodyBottomColor;
                float _GradientDepth;
                float _BandCount;
                float _DitherStrength;
                float _TopAlpha;
                float _TopFadeDepth;
                float _AlphaSteps;
                half4 _SurfaceColor;
                float _SurfaceThickness;
                float _SurfaceOffset;
                float _Amplitude;
                float _Frequency;
                float _SecondaryWave;
                float _WavePhase;
                float _ScrollOffset;
                half4 _BackColor;
                float _BackRise;
                float _BackAmplitudeScale;
                float _BackFrequencyScale;
                float _BackPhaseOffset;
                float _BackParallax;
                half4 _RippleColor;
                float _RippleAlpha;
                float _RippleSpacing;
                float _RippleLength;
                float _RippleSpeed;
                float _RippleDepth;
                half4 _GlitterColor;
                float _GlitterCell;
                float _GlitterDensity;
                float _GlitterDepth;
                float _GlitterSpeed;
                float _GlitterSharpness;
                float _PixelsPerUnit;
                float _TopY;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half4 color : COLOR;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color;
                return output;
            }

            float Bayer4(float2 p)
            {
                int2 c = int2(fmod(abs(p), 4.0));
                int i = c.x + c.y * 4;
                const float m[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
                return (m[i] + 0.5) / 16.0;
            }

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float Wave(float x, float amplitude, float frequency, float phase)
            {
                float w = 6.2831853 * frequency / 100.0;
                float primary = sin(x * w + phase);
                float secondary = sin(x * w * 2.17 - phase * 1.37 + 1.3);
                float tertiary = sin(x * w * 0.53 + phase * 0.61 + 4.1);
                float total = primary + secondary * 0.5 * _SecondaryWave + tertiary * 0.35 * _SecondaryWave;
                float norm = 1.0 + 0.85 * _SecondaryWave;
                return amplitude * total / norm;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 mask = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;
                if (mask.a <= 0.001) discard;

                float2 px = floor(input.positionWS.xy * _PixelsPerUnit);
                float topPx = floor(_TopY * _PixelsPerUnit);
                float depth = topPx - px.y;
                float scroll = floor(_ScrollOffset);
                float x = px.x + scroll;
                float dither = Bayer4(px);

                float frontSurface = _SurfaceOffset - round(Wave(x, _Amplitude, _Frequency, _WavePhase));

                float backX = px.x + floor(_ScrollOffset * _BackParallax);
                float backSurface = _SurfaceOffset - _BackRise - round(Wave(backX, _Amplitude * _BackAmplitudeScale, _Frequency * _BackFrequencyScale, _WavePhase * 0.8 + _BackPhaseOffset));

                half4 colour;

                if (depth >= frontSurface)
                {
                    float below = depth - frontSurface;

                    if (below < _SurfaceThickness)
                    {
                        colour = _SurfaceColor;
                    }
                    else
                    {
                        float t = saturate((depth - _SurfaceOffset) / max(1.0, _GradientDepth));
                        float bands = max(2.0, _BandCount);
                        float band = floor(t * (bands - 1.0) + (dither - 0.5) * _DitherStrength + 0.5);
                        band = clamp(band, 0.0, bands - 1.0);
                        colour = lerp(_BodyTopColor, _BodyBottomColor, band / (bands - 1.0));

                        float fadeT = saturate((below - _SurfaceThickness) / max(1.0, _TopFadeDepth));
                        float steps = max(2.0, _AlphaSteps);
                        float alphaStep = floor(fadeT * (steps - 1.0) + (dither - 0.5) * _DitherStrength + 0.5);
                        alphaStep = clamp(alphaStep, 0.0, steps - 1.0);
                        colour.a *= lerp(_TopAlpha, 1.0, alphaStep / (steps - 1.0));

                        float row = floor(below / max(1.0, _RippleSpacing));
                        bool onRow = fmod(below, max(1.0, _RippleSpacing)) < 1.0 && below > _SurfaceThickness + 1.0;
                        if (onRow && below < _RippleDepth)
                        {
                            float rowSeed = Hash(float2(row, 7.1));
                            float drift = _Time.y * _RippleSpeed * (0.6 + rowSeed * 0.8);
                            float rx = x + drift + rowSeed * 97.0;
                            float dash = sin(rx * 0.21 + row * 1.7) * 0.5 + sin(rx * 0.077 - _WavePhase + row) * 0.5;
                            float fade = 1.0 - below / max(1.0, _RippleDepth);
                            float strength = step(1.0 - _RippleLength * 2.0, dash) * _RippleAlpha * fade;
                            if (strength > dither)
                            {
                                colour = lerp(colour, _RippleColor, 0.85);
                                colour.a = _RippleColor.a;
                            }
                        }

                        if (below < _GlitterDepth && _GlitterDensity > 0.0)
                        {
                            float cellSize = max(2.0, _GlitterCell);
                            float2 local = float2(x, below);
                            float2 cell = floor(local / cellSize);
                            float seed = Hash(cell);
                            if (seed < _GlitterDensity)
                            {
                                float2 spot = floor(float2(Hash(cell + 3.7), Hash(cell + 9.2)) * cellSize);
                                float2 inCell = local - cell * cellSize;
                                if (all(inCell == spot))
                                {
                                    float twinkle = sin(_Time.y * _GlitterSpeed + seed * 62.83) * 0.5 + 0.5;
                                    twinkle = pow(twinkle, _GlitterSharpness);
                                    float fade = 1.0 - below / max(1.0, _GlitterDepth);
                                    if (twinkle * fade > dither * 0.5)
                                        colour = _GlitterColor;
                                }
                            }
                        }
                    }
                }
                else if (depth >= backSurface)
                {
                    colour = _BackColor;
                    if (colour.a < 1.0 && colour.a <= dither) discard;
                    colour.a = 1.0;
                }
                else
                {
                    discard;
                }

                colour.rgb *= input.color.rgb;
                colour.a *= mask.a;
                return colour;
            }
            ENDHLSL
        }
    }
}
