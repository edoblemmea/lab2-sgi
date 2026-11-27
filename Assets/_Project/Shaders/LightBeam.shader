// Haz de luz visible de la linterna: cono aditivo en el aire que se desvanece con la distancia
// (uv.y = 0 en la linterna, 1 al final) y en los bordes (efecto fresnel).
// No ilumina las superficies reales: Flashlight acorta el cono donde choca con la mesh o un mueble,
// y el haz ya es casi transparente al llegar.
Shader "Custom/LightBeam"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.95, 0.8, 1)
        _Intensity ("Intensidad", Range(0, 2)) = 0.35
        _EdgeSoftness ("Suavidad de los bordes", Range(0.1, 4)) = 1.5
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+5" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Blend SrcAlpha One, Zero One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Intensity;
                float _EdgeSoftness;
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
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                // Más intenso en el centro del cono (donde se mira de frente a la superficie) que en los bordes
                float facing = abs(dot(normalize(i.normalWS), normalize(i.viewWS)));
                float edge = pow(saturate(facing), _EdgeSoftness);
                // Se apaga a lo largo del haz: casi nada al final, para no "pintar" la superficie donde acaba
                float along = pow(saturate(1.0 - i.uv.y), 1.6);
                float a = edge * along * _Intensity * _Color.a;
                return half4(_Color.rgb, a);
            }
            ENDHLSL
        }
    }
}
