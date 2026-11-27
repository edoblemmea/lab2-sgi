// Visualización de depuración de la mesh de la escena.
// Color según la orientación de la superficie (no necesita luces) y una cuadrícula en espacio
// de mundo, para que se distingan las formas de los muebles, las esquinas y los bordes.
//   rojo  = superficies que miran hacia X (paredes)
//   verde = superficies horizontales (suelo, techo, tablero de las mesas)
//   azul  = superficies que miran hacia Z (paredes)
Shader "Custom/MeshDebug"
{
    Properties
    {
        _Alpha ("Opacidad", Range(0, 1)) = 0.35
        _GridSize ("Tamaño de la cuadrícula (m)", Float) = 0.1
        _LineWidth ("Grosor de línea", Range(0.001, 0.25)) = 0.05
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            // El alpha final se compone con el passthrough: se mezcla aparte para no elevarlo al cuadrado
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                float _Alpha;
                float _GridSize;
                float _LineWidth;
            CBUFFER_END

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float3 n = normalize(i.normalWS);
                half3 col = abs(n) * 0.8;

                // Cuadrícula: líneas en los dos ejes del plano de la superficie (se ignora el eje de la normal)
                float3 g = abs(frac(i.positionWS / _GridSize) - 0.5);
                float3 onLine = step(0.5 - _LineWidth, g);
                float3 inPlane = step(0.5, 1.0 - abs(n));
                float lineMask = saturate(max(max(onLine.x * inPlane.x, onLine.y * inPlane.y), onLine.z * inPlane.z));

                col = lerp(col, half3(1, 1, 1), lineMask);
                return half4(col, lerp(_Alpha, 0.85, lineMask));
            }
            ENDHLSL
        }
    }
}
