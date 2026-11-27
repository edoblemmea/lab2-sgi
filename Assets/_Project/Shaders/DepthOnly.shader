// Oclusión: escribe solo profundidad, sin color.
// Puesto en la mesh de la escena, los muebles reales tapan a los objetos virtuales
// y se sigue viendo el passthrough (no pinta nada sobre la habitación real).
Shader "Custom/DepthOnly"
{
    SubShader
    {
        Tags { "Queue"="Geometry-1" "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZWrite On
            ColorMask 0
            Cull Back
        }
    }
}
