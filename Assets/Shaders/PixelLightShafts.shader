Shader "Custom/Pixel Light Shafts"
{
    Properties
    {
        [MainTexture] _MainTex ("Mask (optional)", 2D) = "white" {}
        _Color ("Colour", Color) = (1, 0.95, 0.8, 1)
        _Intensity ("Intensity", Range(0, 2)) = 0.6
        _Origin ("Origin (UV)", Vector) = (0.5, 1.15, 0, 0)
        _Resolution ("Pixel Resolution", Vector) = (320, 180, 0, 0)
        _RayCount ("Ray Count", Range(1, 60)) = 18
        _Sharpness ("Ray Sharpness", Range(0.5, 8)) = 2.5
        _Spread ("Fan Spread (radians)", Range(0.1, 3.14)) = 0.9
        _Length ("Length", Range(0.1, 3)) = 1.2
        _StartFade ("Start Fade", Range(0, 1)) = 0.15
        _Speed ("Sway Speed", Range(0, 2)) = 0.15
        _PulseSpeed ("Pulse Speed", Range(0, 5)) = 0.6
        _PulseAmount ("Pulse Amount", Range(0, 1)) = 0.2
        _Bands ("Bands (0 = smooth)", Range(0, 16)) = 5
        _Dither ("Dither", Range(0, 1)) = 1
        [Toggle] _Repeat ("Repeat Sources", Float) = 0
        _SourceSpacing ("Source Spacing (screen heights)", Range(0.2, 4)) = 1.2
        _SourceVariation ("Source Variation", Range(0, 1)) = 0.5
        _PulseVariation ("Pulse Variation", Range(0, 1)) = 0.8
        _ScrollOffset ("Scroll Offset", Float) = 0
        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
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

        Blend SrcAlpha One
        Cull Off
        ZWrite Off
        ColorMask [_ColorMask]

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
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
                float2 localPosition : TEXCOORD1;
            };

            float4 _ClipRect;

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half _Intensity;
                float4 _Origin;
                float4 _Resolution;
                float _RayCount;
                float _Sharpness;
                float _Spread;
                float _Length;
                float _StartFade;
                float _Speed;
                float _PulseSpeed;
                float _PulseAmount;
                float _Bands;
                float _Dither;
                float _Repeat;
                float _SourceSpacing;
                float _SourceVariation;
                float _PulseVariation;
                float _ScrollOffset;
            CBUFFER_END

            float HashSource(float n)
            {
                return frac(sin(n * 127.1 + 311.7) * 43758.5453);
            }

            float ShaftValue(float2 delta, float seed)
            {
                float dist = length(delta);
                float angle = atan2(delta.x, -delta.y);

                float variation = (HashSource(seed) - 0.5) * _SourceVariation;
                float t = _Time.y * _Speed * (1.0 + variation) + seed * 7.31;
                float a = angle * _RayCount * (1.0 + variation * 0.3);
                float r1 = 0.5 + 0.5 * sin(a + t);
                float r2 = 0.5 + 0.5 * sin(a * 1.73 - t * 0.71 + 1.3 + seed);
                float r3 = 0.5 + 0.5 * sin(a * 0.47 + t * 0.37 + 4.1 + seed * 2.0);
                float rays = pow(saturate(r1 * r2 * 0.6 + r3 * 0.4), _Sharpness);

                float fan = 1.0 - smoothstep(_Spread * 0.5, _Spread, abs(angle));
                float fade = saturate(1.0 - dist / _Length);
                fade *= fade;
                float start = smoothstep(0.0, max(_StartFade, 0.0001), dist);
                float pulseRate = _PulseSpeed * lerp(1.0, 0.5 + HashSource(seed + 41.0) * 1.2, _PulseVariation);
                float pulsePhase = HashSource(seed + 57.0) * 6.2831853 * _PulseVariation;
                float wobble = sin(_Time.y * pulseRate * 2.31 + pulsePhase * 1.7) * 0.4 * _PulseVariation;
                float pulseDepth = _PulseAmount * lerp(1.0, 0.4 + HashSource(seed + 73.0) * 1.2, _PulseVariation);
                float pulse = 1.0 + (sin(_Time.y * pulseRate + pulsePhase) + wobble) / (1.0 + 0.4 * _PulseVariation) * pulseDepth;
                float strength = 1.0 + (HashSource(seed + 19.0) - 0.5) * _SourceVariation;

                return rays * fan * fade * start * pulse * strength;
            }

            static const float BayerMatrix[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

            float Bayer4(float2 cell)
            {
                int x = (int)fmod(abs(cell.x), 4.0);
                int y = (int)fmod(abs(cell.y), 4.0);
                return (BayerMatrix[x + y * 4] + 0.5) / 16.0;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color;
                output.localPosition = input.positionOS.xy;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 resolution = max(_Resolution.xy, float2(1, 1));
                float2 cell = floor(input.uv * resolution);
                float2 uv = (cell + 0.5) / resolution;

                float aspect = resolution.x / resolution.y;
                float shaft;

                if (_Repeat > 0.5)
                {
                    float spacing = max(_SourceSpacing, 0.05);
                    float x = uv.x * aspect;
                    float nearest = floor((x + _ScrollOffset) / spacing + 0.5);
                    shaft = 0.0;
                    for (int k = -1; k <= 1; k++)
                    {
                        float index = nearest + k;
                        float originX = index * spacing - _ScrollOffset;
                        float2 d = float2(x - originX, uv.y - _Origin.y);
                        shaft = max(shaft, ShaftValue(d, index));
                    }
                }
                else
                {
                    float2 delta = (uv - _Origin.xy) * float2(aspect, 1);
                    shaft = ShaftValue(delta, 0.0);
                }

                float value = saturate(shaft * _Intensity);

                if (_Bands > 0.5)
                {
                    float threshold = lerp(0.5, Bayer4(cell), _Dither);
                    value = saturate(floor(value * _Bands + threshold) / _Bands);
                }

                half mask = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a;
                half4 colour = _Color * input.color;
                colour.a *= value * mask;

                #ifdef UNITY_UI_CLIP_RECT
                float2 inside = step(_ClipRect.xy, input.localPosition) * step(input.localPosition, _ClipRect.zw);
                colour.a *= inside.x * inside.y;
                #endif

                return colour;
            }
            ENDHLSL
        }
    }
}
