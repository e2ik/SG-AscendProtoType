Shader "Custom/PixelWarp"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}

        [Header(Colour)]
        _ColorB ("Gradient Colour", Color) = (1,1,1,1)
        _GradientAmount ("Gradient Amount", Range(0,1)) = 0
        _GradientOffset ("Gradient Offset", Range(-1,1)) = 0
        _Alpha ("Alpha", Range(0,1)) = 1
        _PulseAmount ("Alpha Pulse Amount", Range(0,1)) = 0
        _PulseSpeed ("Alpha Pulse Speed", Float) = 1

        [Header(Warp)]
        _WarpAmplitude ("Wave Amplitude (pixels XY)", Vector) = (2,1,0,0)
        _WarpFrequency ("Wave Frequency (XY)", Vector) = (3,2,0,0)
        _WarpSpeed ("Wave Speed (XY)", Vector) = (1.5,1,0,0)
        _NoiseAmount ("Noise Amount (pixels)", Float) = 0
        _NoiseScale ("Noise Scale", Float) = 4
        _NoiseSpeed ("Noise Speed", Float) = 0.5
        _Scroll ("Scroll (UV per sec XY)", Vector) = (0,0,0,0)
        _Phase ("Phase Offset", Float) = 0
        [Toggle] _SnapWarp ("Snap Warp To Pixels", Float) = 1
        [Toggle] _Tile ("Tile Outside Edges", Float) = 0

        [Header(Dither)]
        _DitherAmount ("Dither Amount", Range(0,1)) = 1
        _AlphaSteps ("Alpha Steps", Range(1,8)) = 2
        _DitherScale ("Dither Pixel Size (screen)", Float) = 1
        [Toggle] _DitherInTexture ("Dither In Texture Pixels", Float) = 1

        [Header(Blending)]
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 10

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend [_SrcBlend] [_DstBlend]
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _ColorB;
            float _GradientAmount;
            float _GradientOffset;
            float _Alpha;
            float _PulseAmount;
            float _PulseSpeed;
            float4 _WarpAmplitude;
            float4 _WarpFrequency;
            float4 _WarpSpeed;
            float _NoiseAmount;
            float _NoiseScale;
            float _NoiseSpeed;
            float4 _Scroll;
            float _Phase;
            float _SnapWarp;
            float _Tile;
            float _DitherAmount;
            float _AlphaSteps;
            float _DitherScale;
            float _DitherInTexture;
            float4 _ClipRect;

            static const float BAYER[16] =
            {
                0, 8, 2, 10,
                12, 4, 14, 6,
                3, 11, 1, 9,
                15, 7, 13, 5
            };

            float Bayer(float2 p)
            {
                int2 i = int2(fmod(floor(abs(p)), 4.0));
                return (BAYER[i.y * 4 + i.x] + 0.5) / 16.0;
            }

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
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

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.screenPos = ComputeScreenPos(o.vertex);
                o.texcoord = v.texcoord;
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float t = _Time.y + _Phase;
                float2 texSize = _MainTex_TexelSize.zw;
                float2 baseUV = i.texcoord;
                float2 gridUV = _SnapWarp > 0.5 ? (floor(baseUV * texSize) + 0.5) / texSize : baseUV;

                float2 offset;
                offset.x = sin(gridUV.y * _WarpFrequency.x * 6.28318 + t * _WarpSpeed.x) * _WarpAmplitude.x;
                offset.y = sin(gridUV.x * _WarpFrequency.y * 6.28318 + t * _WarpSpeed.y) * _WarpAmplitude.y;

                if (_NoiseAmount != 0)
                {
                    float2 np = gridUV * _NoiseScale + t * _NoiseSpeed;
                    offset.x += (ValueNoise(np) - 0.5) * 2.0 * _NoiseAmount;
                    offset.y += (ValueNoise(np + 17.3) - 0.5) * 2.0 * _NoiseAmount;
                }

                if (_SnapWarp > 0.5)
                    offset = round(offset);

                float2 uv = baseUV + offset * _MainTex_TexelSize.xy + _Scroll.xy * _Time.y;

                float inside = 1.0;
                if (_Tile > 0.5)
                    uv = frac(uv);
                else
                    inside = step(0.0, uv.x) * step(uv.x, 1.0) * step(0.0, uv.y) * step(uv.y, 1.0);

                fixed4 tex = tex2D(_MainTex, uv);

                float g = saturate(baseUV.y + _GradientOffset);
                fixed4 tint = lerp(i.color, lerp(i.color, _ColorB, g), _GradientAmount);

                fixed4 col;
                col.rgb = tex.rgb * tint.rgb;
                col.a = tex.a * tint.a * _Alpha * inside;

                if (_PulseAmount > 0)
                    col.a *= 1.0 - _PulseAmount * (0.5 + 0.5 * sin(t * _PulseSpeed * 6.28318));

                if (_DitherAmount > 0)
                {
                    float2 screenPixel = (i.screenPos.xy / max(i.screenPos.w, 0.0001)) * _ScreenParams.xy / max(_DitherScale, 0.0001);
                    float2 texturePixel = baseUV * texSize;
                    float threshold = Bayer(_DitherInTexture > 0.5 ? texturePixel : screenPixel);
                    float steps = max(1.0, round(_AlphaSteps));
                    float dithered = saturate(floor(col.a * steps + threshold) / steps);
                    col.a = lerp(col.a, dithered, _DitherAmount);
                }

                #ifdef UNITY_UI_CLIP_RECT
                col.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(col.a - 0.001);
                #endif

                return col;
            }
            ENDCG
        }
    }
}
