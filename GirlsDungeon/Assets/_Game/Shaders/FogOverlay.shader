// 시야 안개 덧그리기. 지형 메시를 한 번 더 그리면서, 월드 XZ 위치로 안개 텍스처(칸별 어둡기)를 읽어 검정을 그만큼 덮는다.
// 텍스처 · 범위 · 세기는 FogView가 전역 값으로 넣는다. 세기가 0(기본 · 에디터)이면 아무것도 덮지 않는다.
Shader "GirlsDungeon/FogOverlay"
{
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "FogOverlay"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back
            Offset -1, -1

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_GD_FogTex);
            SAMPLER(sampler_GD_FogTex);
            float4 _GD_FogRect;      // x, z = 텍스처 왼쪽 아래 월드 위치, z, w = 크기
            float _GD_FogStrength;   // 0 = 끔, 1 = 켬

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = (input.positionWS.xz - _GD_FogRect.xy) / max(_GD_FogRect.zw, 0.0001);
                half darkness = SAMPLE_TEXTURE2D(_GD_FogTex, sampler_GD_FogTex, uv).a;
                return half4(0, 0, 0, darkness * _GD_FogStrength);
            }
            ENDHLSL
        }
    }
}
