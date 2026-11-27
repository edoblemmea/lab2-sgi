// Charco que se adapta a la mesh (SurfaceLiquid).
// Cada triángulo lleva en uv1 la "fuente desplegada": el punto de impacto desdoblado sobre su plano.
// En cada píxel se calcula la distancia sobre la superficie a esa fuente, con más coste al subir
// (×(1 + _UpCost·pendiente)) y menos al bajar (×_DownCost) para que el líquido escurra por los bordes.
// Se ve donde distancia / _Radius < _Spread, con el borde irregular gracias a un ruido en espacio de mundo.
// uv0.x = distancia / radio por vértice (aproximada; por si se quiere usar en Shader Graph).
Shader "Custom/SurfaceLiquid"
{
    Properties
    {
        _Color ("Color", Color) = (0.4, 1, 0.5, 0.85)
        _Spread ("Extensión (0-1)", Range(0, 1.2)) = 1
        _Radius ("Radio (m)", Float) = 0.35
        _UpCost ("Coste al subir", Float) = 3
        _DownCost ("Coste al bajar", Float) = 0.7
        _NoiseScale ("Escala del ruido", Float) = 14
        _NoiseAmount ("Irregularidad del borde", Range(0, 0.6)) = 0.3
        _Edge ("Suavidad del borde", Range(0.005, 0.3)) = 0.05
        _Seed ("Semilla", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            Offset -1, -1

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 source : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 sourceWS : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Spread;
                float _Radius;
                float _UpCost;
                float _DownCost;
                float _NoiseScale;
                float _NoiseAmount;
                float _Edge;
                float _Seed;
            CBUFFER_END

            float Hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            // Ruido de valor 3D
            float Noise(float3 x)
            {
                float3 i = floor(x);
                float3 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(lerp(Hash(i + float3(0, 0, 0)), Hash(i + float3(1, 0, 0)), f.x),
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
                o.sourceWS = TransformObjectToWorld(v.source); // el charco cuelga de un objeto sin transformar
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                // Distancia sobre la superficie con coste según la pendiente (subir cuesta más, bajar menos)
                float3 v = i.positionWS - i.sourceWS;
                float len = length(v);
                float slope = v.y / max(len, 1e-4);
                float factor = slope > 0.0 ? 1.0 + _UpCost * slope : lerp(1.0, _DownCost, saturate(-slope));
                float d = len * factor / max(_Radius, 1e-4);

                float3 p = i.positionWS * _NoiseScale + _Seed;
                float n = Noise(p) * 0.65 + Noise(p * 2.3) * 0.35;
                float edge = _Spread * (1.0 - _NoiseAmount * 0.5) + (n - 0.5) * _NoiseAmount * _Spread;

                float alpha = 1.0 - smoothstep(edge - _Edge, edge, d);
                // Borde más claro y algo más opaco, como el menisco de un líquido
                float rim = smoothstep(edge - _Edge * 4.0, edge - _Edge, d) * alpha;
                half3 col = _Color.rgb * (0.7 + 0.3 * n) + rim * 0.35;
                return half4(col, saturate(alpha * _Color.a + rim * 0.15));
            }
            ENDHLSL
        }
    }
}
