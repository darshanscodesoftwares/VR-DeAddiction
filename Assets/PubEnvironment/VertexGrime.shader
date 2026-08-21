// VertexGrime.shader
//
// Standard lighting, but albedo is multiplied by the mesh's vertex colours.
// Unity's built-in Standard shader ignores vertex colours entirely, so grime
// baked in Blender would be invisible without this.
//
// The vertex colour acts as a multiplier: white leaves the base colour alone,
// darker values read as dirt, soot, water staining and ambient occlusion in
// crevices. That keeps the existing flat-colour palette as the "clean" base
// and layers wear on top, with no texture files and no extra memory.
//
// Cheap on mobile: one extra interpolator and one multiply.

Shader "PubEnvironment/VertexGrime"
{
    Properties
    {
        _Color      ("Base Colour", Color) = (1,1,1,1)
        _Glossiness ("Smoothness", Range(0,1)) = 0.3
        _Metallic   ("Metallic", Range(0,1)) = 0.0
        _GrimeStrength ("Grime Strength", Range(0,1)) = 1.0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 200

        CGPROGRAM
        // Standard lighting model, shadows on all light types.
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        struct Input
        {
            float4 vertColour : COLOR;
        };

        half   _Glossiness;
        half   _Metallic;
        fixed4 _Color;
        half   _GrimeStrength;

        UNITY_INSTANCING_BUFFER_START(Props)
        UNITY_INSTANCING_BUFFER_END(Props)

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            // Fade the vertex colour toward white as strength drops, so the
            // same mesh can be used clean or filthy without a second material.
            fixed3 grime = lerp(fixed3(1, 1, 1), IN.vertColour.rgb, _GrimeStrength);

            fixed4 c = _Color;
            o.Albedo = c.rgb * grime;

            // Dirt is rough: darker areas lose smoothness.
            half wear = saturate(dot(grime, fixed3(0.333, 0.333, 0.333)));
            o.Metallic = _Metallic * wear;
            o.Smoothness = _Glossiness * wear;
            o.Alpha = c.a;
        }
        ENDCG
    }

    FallBack "Standard"
}
