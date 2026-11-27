// Onda del sonar sobre la mesh de la escena: un anillo que se aleja de _Center a distancia _Radius.
// Aditivo y transparente; solo se dibuja durante la onda (unos 1,5 s).
Shader "Custom/SonarWave"
{
    Properties
    {
        _Color ("Color", Color) = (0.3, 0.9, 1, 1)
        _Center ("Centro", Vector) = (0, 0, 0, 0)
        _Radius ("Radio", Float) = 0
        _Width ("Grosor del anillo (m)", Float) = 0.25
        _Fade ("Desvanecido", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Blend SrcAlpha One, One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Offset -1, -1
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float4 _Center;
                float _Radius;
                float _Width;
                float _Fade;
            CBUFFER_END

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float d = distance(i.positionWS, _Center.xyz);
                float x = (d - _Radius) / max(_Width, 1e-3);
                // Frente brillante y estela que se apaga por detrás
                float ring = exp(-x * x * 4.0) + saturate(-x) * exp(x * 0.8) * 0.25;
                float a = saturate(ring) * _Fade;
                return half4(_Color.rgb, a * _Color.a);
            }
            ENDHLSL
        }
    }
}
