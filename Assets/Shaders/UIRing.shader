Shader "Custom/UIRing"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _InnerRadius ("Inner Radius", Range(0,1)) = 0.6
        _OuterRadius ("Outer Radius", Range(0,1)) = 0.8
        _Resolution ("Pixel Resolution (0 = smooth)", Float) = 64
        _EdgeColor ("Edge Colour", Color) = (0,0,0,0.35)
        _EdgeWidth ("Edge Width (pixels)", Float) = 1

        _DitherEdge ("Dither Edge Width (pixels)", Float) = 2
        _DitherInner ("Dither Inner Edge", Range(0,1)) = 1
        _DitherOuter ("Dither Outer Edge", Range(0,1)) = 1
        [Toggle] _DitherAlpha ("Dither Alpha", Float) = 1
        _DitherScale ("Dither Scale (screen px, 0 = ring pixels)", Float) = 0
        _RadialFade ("Radial Fade (+ inner, - outer)", Range(-1,1)) = 0

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
        Blend SrcAlpha OneMinusSrcAlpha
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
            float _InnerRadius;
            float _OuterRadius;
            float _Resolution;
            fixed4 _EdgeColor;
            float _EdgeWidth;
            float _DitherEdge;
            float _DitherInner;
            float _DitherOuter;
            float _DitherAlpha;
            float _DitherScale;
            float _RadialFade;
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
                float2 uv = i.texcoord;
                bool pixelated = _Resolution > 0.5;
                if (pixelated)
                    uv = (floor(uv * _Resolution) + 0.5) / _Resolution;

                float d = length(uv - 0.5) * 2.0;
                float inner = min(_InnerRadius, _OuterRadius);
                float outer = max(_InnerRadius, _OuterRadius);

                if (d < inner || d > outer) discard;

                float pixelSize = pixelated ? 2.0 / _Resolution : 0.01;
                float edgeSize = _EdgeWidth * pixelSize;
                float edge = (_EdgeWidth > 0.0) ? saturate(step(d, inner + edgeSize) + step(outer - edgeSize, d)) : 0.0;

                fixed4 col = tex2D(_MainTex, i.texcoord) * i.color;
                col.rgb = lerp(col.rgb, _EdgeColor.rgb, edge * _EdgeColor.a);

                if (_DitherEdge > 0.0)
                {
                    float width = _DitherEdge * pixelSize;
                    float innerFade = lerp(1.0, saturate((d - inner) / width), _DitherInner);
                    float outerFade = lerp(1.0, saturate((outer - d) / width), _DitherOuter);
                    col.a *= innerFade * outerFade;
                }

                if (_RadialFade != 0.0)
                {
                    float t = saturate((d - inner) / max(outer - inner, 0.0001));
                    float f = abs(_RadialFade);
                    col.a *= _RadialFade > 0.0 ? lerp(1.0 - f, 1.0, t) : lerp(1.0, 1.0 - f, t);
                }

                if (_DitherAlpha > 0.5)
                {
                    float2 cell;
                    if (_DitherScale > 0.0)
                        cell = (i.screenPos.xy / max(i.screenPos.w, 0.0001)) * _ScreenParams.xy / _DitherScale;
                    else if (pixelated)
                        cell = floor(i.texcoord * _Resolution);
                    else
                        cell = (i.screenPos.xy / max(i.screenPos.w, 0.0001)) * _ScreenParams.xy;

                    col.a = col.a > Bayer(cell) ? 1.0 : 0.0;
                    if (col.a <= 0.0) discard;
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
