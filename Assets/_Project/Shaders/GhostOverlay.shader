// Capa extra sobre el modelo del fantasma: brillo del color de su tipo con efecto fresnel.
// _AlwaysOnTop = 1 la dibuja a través de los muebles (silueta del sonar, ZTest Always).
// Se usa con dos materiales: M_GhostTint (normal) y M_GhostSilhouette (siempre encima).
Shader "Custom/GhostOverlay"
{
    Properties
    {
        _Color ("Color", Color) = (0.8, 0.95, 1, 1)
        _Intensity ("Intensidad", Range(0, 2)) = 0.6
        _Base ("Relleno", Range(0, 1)) = 0.15
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 4
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+10" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Blend SrcAlpha One, One OneMinusSrcAlpha
            ZWrite Off
            ZTest [_ZTest]
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewWS : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Intensity;
                float _Base;
                float _ZTest;
            CBUFFER_END

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 posWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(posWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.viewWS = GetWorldSpaceViewDir(posWS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float f = 1.0 - saturate(dot(normalize(i.normalWS), normalize(i.viewWS)));
                float a = saturate((_Base + f * f * 1.5) * _Intensity);
                return half4(_Color.rgb, a);
            }
            ENDHLSL
        }
    }
}
