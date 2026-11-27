// Manchas de pintura pegadas al fantasma (hasta 4). Capa extra sobre el modelo.
// Cada mancha: _PaintPoints[i] = (posición en el mundo, radio) y _PaintColors[i] = color (alpha = intensidad).
// Los valores llegan por MaterialPropertyBlock desde GhostController, que actualiza la posición cada frame
// para que la mancha se mueva con el fantasma.
Shader "Custom/GhostPaint"
{
    Properties
    {
        _NoiseScale ("Escala del ruido", Float) = 30
        _Edge ("Suavidad del borde", Range(0.01, 0.5)) = 0.15
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+20" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back
            Offset -1, -1

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

            CBUFFER_START(UnityPerMaterial)
                float _NoiseScale;
                float _Edge;
            CBUFFER_END

            // Por renderer (MaterialPropertyBlock): fuera del CBUFFER del material
            float4 _PaintPoints[4];
            float4 _PaintColors[4];

            float Hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float Noise(float3 x)
            {
                float3 i = floor(x);
                float3 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(lerp(Hash(i), Hash(i + float3(1, 0, 0)), f.x),
                                 lerp(Hash(i + float3(0, 1, 0)), Hash(i + float3(1, 1, 0)), f.x), f.y),
                            lerp(lerp(Hash(i + float3(0, 0, 1)), Hash(i + float3(1, 0, 1)), f.x),
                                 lerp(Hash(i + float3(0, 1, 1)), Hash(i + float3(1, 1, 1)), f.x), f.y), f.z);
            }

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
                half4 result = 0;
                [unroll]
                for (int k = 0; k < 4; k++)
                {
                    float4 p = _PaintPoints[k];
                    float4 c = _PaintColors[k];
                    if (p.w <= 0.0 || c.a <= 0.0) continue;
                    float3 rel = i.positionWS - p.xyz;
                    // Ruido relativo al punto de impacto: la forma de la mancha viaja con ella
                    float n = Noise(rel * _NoiseScale + k * 7.3) * 0.6 + Noise(rel * _NoiseScale * 2.7) * 0.4;
                    float d = length(rel) / p.w + (n - 0.5) * 0.8;
                    float a = (1.0 - smoothstep(1.0 - _Edge, 1.0, d)) * c.a;
                    if (a > result.a) result = half4(c.rgb * (0.85 + 0.3 * n), a);
                }
                return result;
            }
            ENDHLSL
        }
    }
}
