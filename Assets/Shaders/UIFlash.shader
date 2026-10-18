Shader "Custom/UI Flash"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)

        _FlashColor ("Flash Colour", Color) = (1, 1, 1, 1)
        _FlashAmount ("Flash Amount", Range(0, 1)) = 0
        [Toggle] _FlashChecker ("Flash Checkerboard", Float) = 0

        _OverlayColor ("Overlay Colour", Color) = (1, 1, 1, 1)
        _OverlayAmount ("Overlay Amount", Range(0, 1)) = 0
        [Toggle] _OverlayChecker ("Overlay Checkerboard", Float) = 1
        _CheckerSize ("Checker Size (texels)", Range(1, 8)) = 1

        _EdgeColor ("Edge Colour", Color) = (1, 1, 1, 1)
        _EdgeAmount ("Edge Amount", Range(0, 1)) = 0
        _EdgeWidth ("Edge Width (texels)", Range(1, 4)) = 1

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
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
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
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
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
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;

            fixed4 _FlashColor;
            float _FlashAmount;
            float _FlashChecker;
            fixed4 _OverlayColor;
            float _OverlayAmount;
            float _OverlayChecker;
            float _CheckerSize;
            fixed4 _EdgeColor;
            float _EdgeAmount;
            float _EdgeWidth;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;
                return OUT;
            }

            float SampleAlpha(float2 uv)
            {
                if (uv.x < 0.0 || uv.y < 0.0 || uv.x > 1.0 || uv.y > 1.0) return 0.0;
                return tex2D(_MainTex, uv).a;
            }

            float EdgeMask(float2 uv)
            {
                if (SampleAlpha(uv) < 0.5) return 0.0;

                float2 texel = _MainTex_TexelSize.xy;
                float width = clamp(round(_EdgeWidth), 1.0, 4.0);
                float minAlpha = 1.0;

                [unroll]
                for (int i = 1; i <= 4; i++)
                {
                    if (i > width) break;
                    float2 o = texel * i;
                    minAlpha = min(minAlpha, SampleAlpha(uv + float2(o.x, 0)));
                    minAlpha = min(minAlpha, SampleAlpha(uv - float2(o.x, 0)));
                    minAlpha = min(minAlpha, SampleAlpha(uv + float2(0, o.y)));
                    minAlpha = min(minAlpha, SampleAlpha(uv - float2(0, o.y)));
                }

                return minAlpha < 0.5 ? 1.0 : 0.0;
            }

            float Checker(float2 uv)
            {
                float2 cell = floor(uv * _MainTex_TexelSize.zw / max(1.0, round(_CheckerSize)));
                return fmod(abs(cell.x + cell.y), 2.0) < 1.0 ? 1.0 : 0.0;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                half4 color = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;
                float checker = Checker(IN.texcoord);

                if (_OverlayAmount > 0.0)
                {
                    float mask = _OverlayChecker > 0.5 ? checker : 1.0;
                    color.rgb = lerp(color.rgb, _OverlayColor.rgb, saturate(_OverlayAmount) * _OverlayColor.a * mask);
                }

                if (_EdgeAmount > 0.0)
                {
                    float edge = EdgeMask(IN.texcoord);
                    color.rgb = lerp(color.rgb, _EdgeColor.rgb, saturate(_EdgeAmount) * _EdgeColor.a * edge);
                }

                if (_FlashAmount > 0.0)
                {
                    float mask = _FlashChecker > 0.5 ? checker : 1.0;
                    color.rgb = lerp(color.rgb, _FlashColor.rgb, saturate(_FlashAmount) * _FlashColor.a * mask);
                }

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }
            ENDCG
        }
    }
}
