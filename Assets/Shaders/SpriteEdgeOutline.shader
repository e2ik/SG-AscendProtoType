Shader "Custom/Sprite Edge Outline"
{
    Properties
    {
        [MainTexture] _MainTex ("Sprite Texture", 2D) = "white" {}
        [HDR] _OutlineColor ("Outline Color", Color) = (1, 1, 1, 1)
        [Enum(Inside, 0, Outside, 1)] _OutlineSide ("Outline Side", Float) = 1
        _OutlineWidth ("Outline Width (pixels)", Range(1, 4)) = 1
        _OutlineAmount ("Outline Amount", Range(0, 1)) = 1
        [Toggle] _OutlineDiagonals ("Include Diagonals", Float) = 1
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
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _OutlineColor;
                half _OutlineSide;
                half _OutlineWidth;
                half _OutlineAmount;
                half _OutlineDiagonals;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color;
                return output;
            }

            half SampleAlpha(float2 uv)
            {
                if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0) return 0.0;
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a;
            }

            void RingAlpha(float2 uv, float2 step, out half maxAlpha, out half minAlpha)
            {
                half a0 = SampleAlpha(uv + float2(step.x, 0.0));
                half a1 = SampleAlpha(uv - float2(step.x, 0.0));
                half a2 = SampleAlpha(uv + float2(0.0, step.y));
                half a3 = SampleAlpha(uv - float2(0.0, step.y));

                maxAlpha = max(max(a0, a1), max(a2, a3));
                minAlpha = min(min(a0, a1), min(a2, a3));

                if (_OutlineDiagonals > 0.5)
                {
                    half d0 = SampleAlpha(uv + step);
                    half d1 = SampleAlpha(uv - step);
                    half d2 = SampleAlpha(uv + float2(step.x, -step.y));
                    half d3 = SampleAlpha(uv + float2(-step.x, step.y));

                    maxAlpha = max(maxAlpha, max(max(d0, d1), max(d2, d3)));
                    minAlpha = min(minAlpha, min(min(d0, d1), min(d2, d3)));
                }
            }

            bool IsEdge(float2 uv, bool outside)
            {
                int width = clamp((int)round(_OutlineWidth), 1, 4);

                [unroll]
                for (int i = 1; i <= 4; i++)
                {
                    if (i > width) break;

                    half maxAlpha;
                    half minAlpha;
                    RingAlpha(uv, _MainTex_TexelSize.xy * i, maxAlpha, minAlpha);

                    if (outside && maxAlpha > 0.01) return true;
                    if (!outside && minAlpha <= 0.01) return true;
                }

                return false;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;
                bool opaque = tex.a > 0.01;
                bool outside = _OutlineSide > 0.5;
                half amount = saturate(_OutlineAmount) * _OutlineColor.a;

                if (amount <= 0.001)
                    return tex;

                if (outside)
                {
                    if (opaque || !IsEdge(input.uv, true))
                        return tex;

                    return half4(_OutlineColor.rgb, amount * input.color.a);
                }

                if (!opaque || !IsEdge(input.uv, false))
                    return tex;

                tex.rgb = lerp(tex.rgb, _OutlineColor.rgb, amount);
                return tex;
            }
            ENDHLSL
        }
    }
}
