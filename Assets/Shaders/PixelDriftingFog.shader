Shader "Custom/Pixel Drifting Fog"
{
    Properties
    {
        [MainTexture] _MainTex ("Fog Texture (Wrap: Repeat)", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _ScrollSpeed ("Scroll Speed (UV/sec)", Vector) = (0.01, 0, 0, 0)
        _WaveAmplitude ("Wave Amplitude", Range(0, 0.1)) = 0.01
        _WaveFrequency ("Wave Frequency", Range(0, 10)) = 2
        _WaveSpeed ("Wave Speed", Range(0, 5)) = 0.5
        [Toggle] _PixelSnap ("Snap To Pixels", Float) = 1
        _SecondLayerAlpha ("Second Layer Alpha", Range(0, 1)) = 0
        _SecondLayerScale ("Second Layer Scale", Range(0.25, 4)) = 1.3
        _SecondLayerSpeed ("Second Layer Speed", Range(-2, 2)) = 0.6
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
                half4 _Color;
                float4 _ScrollSpeed;
                float _WaveAmplitude;
                float _WaveFrequency;
                float _WaveSpeed;
                float _PixelSnap;
                float _SecondLayerAlpha;
                float _SecondLayerScale;
                float _SecondLayerSpeed;
            CBUFFER_END

            float2 Snap(float2 uv)
            {
                if (_PixelSnap < 0.5) return uv;
                float2 size = _MainTex_TexelSize.zw;
                return (floor(uv * size) + 0.5) / size;
            }

            half4 SampleFog(float2 uv, float2 offset)
            {
                float2 size = _MainTex_TexelSize.zw;
                float column = _PixelSnap > 0.5 ? floor(uv.x * size.x) / size.x : uv.x;
                float wave = sin(column * _WaveFrequency * 6.28318 + _Time.y * _WaveSpeed) * _WaveAmplitude;
                if (_PixelSnap > 0.5)
                    wave = round(wave * size.y) / size.y;

                uv.y += wave;
                uv += offset;
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, Snap(uv));
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 offset = frac(_ScrollSpeed.xy * _Time.y);
                half4 fog = SampleFog(input.uv, offset);

                if (_SecondLayerAlpha > 0.001)
                {
                    float2 secondOffset = frac(_ScrollSpeed.xy * _SecondLayerSpeed * _Time.y + 0.37);
                    half4 second = SampleFog(input.uv * _SecondLayerScale, secondOffset);
                    second.a *= _SecondLayerAlpha;
                    fog.rgb = lerp(fog.rgb, second.rgb, second.a * (1.0 - fog.a));
                    fog.a = saturate(fog.a + second.a * (1.0 - fog.a));
                }

                return fog * _Color * input.color;
            }
            ENDHLSL
        }
    }
}
