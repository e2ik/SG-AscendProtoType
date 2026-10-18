Shader "Custom/Pixel Fog Reveal"
{
    Properties
    {
        [PerRendererData] [MainTexture] _MainTex ("Shape (Sprite)", 2D) = "white" {}
        _Color ("Fog Colour", Color) = (0.75, 0.8, 0.9, 0.85)
        _ShadeColor ("Shade Colour", Color) = (0.45, 0.5, 0.65, 1)
        _PixelsPerUnit ("Pixels Per Unit", Float) = 16

        [Header(Noise)]
        _NoiseScale ("Noise Scale", Float) = 0.35
        _NoiseAmount ("Noise Amount", Range(0, 1)) = 0.8
        _DensityMin ("Density Min", Range(0, 1)) = 0.3
        _DensityMax ("Density Max", Range(0, 1)) = 0.75
        _ScrollSpeed ("Scroll (units/sec XY)", Vector) = (0.15, 0.03, 0, 0)
        _SecondLayerSpeed ("Second Layer Speed", Range(-2, 2)) = -0.6
        _SecondLayerScale ("Second Layer Scale", Range(0.25, 4)) = 2.1
        _SecondLayerAmount ("Second Layer Amount", Range(0, 1)) = 0.4
        _EdgeFade ("Sprite Edge Fade (UV)", Range(0, 0.5)) = 0.15

        [Header(Wisps)]
        _Stretch ("Stretch (XY frequency)", Vector) = (0.45, 1.6, 0, 0)
        _WarpAmount ("Warp Amount", Range(0, 3)) = 1.2
        _WarpScale ("Warp Scale", Range(0.1, 4)) = 0.6
        _WarpSpeed ("Warp Speed", Range(0, 2)) = 0.15
        _Wispiness ("Wispiness", Range(0, 1)) = 0.7
        _WispSharpness ("Wisp Sharpness", Range(0.5, 6)) = 2.5

        [Header(Dither)]
        _AlphaSteps ("Alpha Steps", Range(1, 8)) = 3
        _DitherAmount ("Dither Amount", Range(0, 1)) = 1

        [Header(Reveal)]
        _Reveal ("Reveal", Range(0, 1)) = 0
        [KeywordEnum(Radial, Dissolve, Sweep)] _RevealMode ("Reveal Mode", Float) = 0
        _RevealCenter ("Reveal Centre (world XY)", Vector) = (0, 0, 0, 0)
        _RevealRadius ("Reveal Radius (world)", Float) = 10
        _RevealDirection ("Sweep Direction (XY)", Vector) = (0, 1, 0, 0)
        _RevealNoiseScale ("Edge Noise Scale", Float) = 0.6
        _RevealNoise ("Edge Noise Amount", Range(0, 1)) = 0.35
        _RevealSoftness ("Edge Softness", Range(0.001, 0.5)) = 0.08
        _EdgeColor ("Edge Glow Colour", Color) = (1, 0.95, 0.8, 0.6)
        _EdgeWidth ("Edge Glow Width", Range(0, 0.3)) = 0.06

        [Header(Reveal Zones)]
        _ZoneGlowWidth ("Zone Glow Width (world)", Float) = 0.35
        _ZoneNoiseScale ("Zone Edge Noise Scale", Float) = 0.7
        _ZoneNoiseSpeed ("Zone Edge Drift Speed", Float) = 0.08
        _ZoneErode ("Zone Edge Follows Wisps", Range(0, 1)) = 0.6

        [Header(Player Hole)]
        _HolePosition ("Hole Position (world XY)", Vector) = (0, 0, 0, 0)
        _HoleRadius ("Hole Radius", Float) = 0
        _HoleSoftness ("Hole Softness", Float) = 1
        _HoleStrength ("Hole Strength", Range(0, 1)) = 0.6
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
            #pragma shader_feature_local _REVEALMODE_RADIAL _REVEALMODE_DISSOLVE _REVEALMODE_SWEEP
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 worldPos : TEXCOORD1;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            float _FogZoneCount;
            float4 _FogZones[16];
            float4 _FogZoneData[16];
            float4 _FogZoneGlow[16];

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _ShadeColor;
                float _PixelsPerUnit;
                float _NoiseScale;
                float _NoiseAmount;
                float _DensityMin;
                float _DensityMax;
                float4 _ScrollSpeed;
                float _SecondLayerSpeed;
                float _SecondLayerScale;
                float _SecondLayerAmount;
                float4 _Stretch;
                float _WarpAmount;
                float _WarpScale;
                float _WarpSpeed;
                float _Wispiness;
                float _WispSharpness;
                float _EdgeFade;
                float _AlphaSteps;
                float _DitherAmount;
                float _Reveal;
                float4 _RevealCenter;
                float _RevealRadius;
                float4 _RevealDirection;
                float _RevealNoiseScale;
                float _RevealNoise;
                float _RevealSoftness;
                half4 _EdgeColor;
                float _EdgeWidth;
                float4 _HolePosition;
                float _HoleRadius;
                float _HoleSoftness;
                float _HoleStrength;
                float _ZoneGlowWidth;
                float _ZoneNoiseScale;
                float _ZoneNoiseSpeed;
                float _ZoneErode;
            CBUFFER_END

            static const float BAYER[16] =
            {
                0, 8, 2, 10,
                12, 4, 14, 6,
                3, 11, 1, 9,
                15, 7, 13, 5
            };

            float Bayer(float2 p)
            {
                int2 i = int2(fmod(abs(p), 4.0));
                return (BAYER[i.y * 4 + i.x] + 0.5) / 16.0;
            }

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float Noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash(i);
                float b = Hash(i + float2(1, 0));
                float c = Hash(i + float2(0, 1));
                float d = Hash(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float Fbm(float2 p)
            {
                float value = 0.0;
                float amplitude = 0.5;
                for (int i = 0; i < 3; i++)
                {
                    value += Noise(p) * amplitude;
                    p = p * 2.03 + 17.1;
                    amplitude *= 0.5;
                }
                return value / 0.875;
            }

            float RoundedBox(float2 p, float2 halfSize, float radius)
            {
                radius = min(radius, min(halfSize.x, halfSize.y));
                float2 q = abs(p) - halfSize + radius;
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - radius;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                output.color = input.color;
                output.worldPos = TransformObjectToWorld(input.positionOS).xy;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float ppu = max(_PixelsPerUnit, 1.0);
                float2 cell = floor(input.worldPos * ppu);
                float2 world = (cell + 0.5) / ppu;
                float t = _Time.y;

                float2 stretch = max(_Stretch.xy, 0.01);
                float2 p = world * _NoiseScale * stretch;

                if (_WarpAmount > 0.0)
                {
                    float2 wp = p * _WarpScale;
                    float2 warp = float2(
                        Fbm(wp + float2(t * _WarpSpeed, 0.0)),
                        Fbm(wp + float2(5.2, 1.3) - float2(0.0, t * _WarpSpeed))) - 0.5;
                    p += warp * _WarpAmount;
                }

                float2 scroll = _ScrollSpeed.xy * t * _NoiseScale * stretch;
                float soft = Fbm(p + scroll);
                float ridge = 1.0 - abs(Fbm(p * 1.7 + scroll * 1.3 + 11.4) * 2.0 - 1.0);
                ridge = pow(saturate(ridge), _WispSharpness);
                float density = lerp(soft, ridge * (0.5 + soft), _Wispiness);

                if (_SecondLayerAmount > 0.0)
                {
                    float2 q = p * _SecondLayerScale + scroll * _SecondLayerSpeed + 31.7;
                    float secondSoft = Fbm(q);
                    float secondRidge = pow(saturate(1.0 - abs(Fbm(q * 1.7 + 7.7) * 2.0 - 1.0)), _WispSharpness);
                    float second = lerp(secondSoft, secondRidge * (0.5 + secondSoft), _Wispiness);
                    density = lerp(density, max(density, second), _SecondLayerAmount);
                }

                float shaped = saturate((density - _DensityMin) / max(_DensityMax - _DensityMin, 0.0001));

                half4 shape = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half4 colour = lerp(_ShadeColor, _Color, shaped);
                colour.rgb *= input.color.rgb;
                float alpha = shape.a * _Color.a * input.color.a * lerp(1.0, shaped, _NoiseAmount);

                if (_EdgeFade > 0.0)
                {
                    float2 edge = min(input.uv, 1.0 - input.uv) / _EdgeFade;
                    alpha *= saturate(min(edge.x, edge.y));
                }

                if (_HoleRadius > 0.0 && _HoleStrength > 0.0)
                {
                    float hole = saturate((distance(world, _HolePosition.xy) - _HoleRadius) / max(_HoleSoftness, 0.001));
                    alpha *= lerp(1.0, hole, _HoleStrength);
                }

                float field;
                float edgeNoise = Noise(world * _RevealNoiseScale) * 0.6 + Noise(world * _RevealNoiseScale * 2.7 + 9.3) * 0.4;
                #if defined(_REVEALMODE_DISSOLVE)
                    field = Fbm(world * _RevealNoiseScale);
                #elif defined(_REVEALMODE_SWEEP)
                    float2 direction = normalize(_RevealDirection.xy + float2(0.00001, 0.0));
                    float along = dot(world - _RevealCenter.xy, direction) / max(_RevealRadius, 0.001);
                    field = lerp(saturate(along), edgeNoise, _RevealNoise);
                #else
                    float radial = distance(world, _RevealCenter.xy) / max(_RevealRadius, 0.001);
                    field = lerp(saturate(radial), edgeNoise, _RevealNoise);
                #endif

                float reveal = _Reveal * (1.0 + _RevealSoftness);
                float keep = saturate((field - reveal + _RevealSoftness) / _RevealSoftness);
                if (_Reveal >= 0.999) keep = 0.0;
                alpha *= keep;

                if (_EdgeWidth > 0.0 && _Reveal > 0.001 && _Reveal < 0.999)
                {
                    float glow = saturate(1.0 - abs(field - reveal + _RevealSoftness * 0.5) / _EdgeWidth);
                    colour.rgb = lerp(colour.rgb, _EdgeColor.rgb, glow * _EdgeColor.a);
                }

                int zoneCount = (int)min(_FogZoneCount, 16.0);
                float zoneNoise = 0.5;
                if (zoneCount > 0)
                {
                    float2 zp = world * _ZoneNoiseScale + float2(t, -t * 0.7) * _ZoneNoiseSpeed;
                    zoneNoise = Fbm(zp + float2(Fbm(zp * 0.5 + 3.1), Fbm(zp * 0.5 + 8.7)) * 1.5);
                }

                for (int z = 0; z < zoneCount; z++)
                {
                    float4 zone = _FogZones[z];
                    float4 data = _FogZoneData[z];
                    if (data.x <= 0.0) continue;

                    float expand = _FogZoneGlow[z].y;
                    float minHalf = max(min(zone.z, zone.w) + expand, 0.05);
                    float softness = clamp(data.z, 0.01, minHalf * 0.6);
                    float noiseRange = min(data.w, minHalf * 0.3);
                    float sdf = RoundedBox(world - zone.xy, zone.zw, data.y);
                    float threshold = lerp(-minHalf + expand - softness - noiseRange, expand, data.x);
                    float d = sdf + zoneNoise * noiseRange - threshold;
                    float keepZone = saturate((d + softness) / softness);
                    keepZone = keepZone * keepZone * (3.0 - 2.0 * keepZone);
                    keepZone = saturate(keepZone * (1.0 + _ZoneErode) - (1.0 - shaped) * _ZoneErode * (1.0 - keepZone));
                    alpha *= keepZone;

                    float glowAmount = _FogZoneGlow[z].x;
                    if (glowAmount > 0.001 && _ZoneGlowWidth > 0.0)
                    {
                        float glow = saturate(1.0 - abs(d + softness * 0.5) / _ZoneGlowWidth);
                        colour.rgb = lerp(colour.rgb, _EdgeColor.rgb, glow * _EdgeColor.a * glowAmount);
                    }
                }

                if (_DitherAmount > 0.0)
                {
                    float steps = max(1.0, round(_AlphaSteps));
                    float dithered = saturate(floor(alpha * steps + Bayer(cell)) / steps);
                    alpha = lerp(alpha, dithered, _DitherAmount);
                }

                clip(alpha - 0.001);
                return half4(colour.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
