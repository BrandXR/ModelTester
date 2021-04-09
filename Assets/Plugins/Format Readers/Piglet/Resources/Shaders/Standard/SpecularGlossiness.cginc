#ifndef SPECULAR_GLOSSINESS_INCLUDE
#define SPECULAR_GLOSSINESS_INCLUDE

#pragma target 3.0

sampler2D _normalTexture;

sampler2D _occlusionTexture;

sampler2D _emissiveTexture;
float4 _emissiveFactor;

sampler2D _diffuseTexture;
float4 _diffuseFactor;

float4 _specularFactor;
half _glossinessFactor;
sampler2D _specularGlossinessTexture;

struct Input
{
    // Note: The `uv` prefix "magically" maps to
    // the corresponding shader property, e.g.
    // `uv_diffuseTexture` -> `_diffuseTexture`.

    float2 uv_normalTexture;
    float2 uv_occlusionTexture;
    float2 uv_emissiveTexture;
    float2 uv_diffuseTexture;
    float2 uv_specularGlossinessTexture;

    // Note: COLOR faults to (1,1,1,1) if unset

    float4 vertexColor : COLOR;

    // Note: VFACE is used to implement double-sided
    // rendering of triangles. The value of VFACE is
    // positive when a triangle is front-facing and negative when
    // a triangle is back-facing. See VFACE section of
    // https://docs.unity3d.com/Manual/SL-ShaderSemantics.html
    // and also a related discussion at
    // https://forum.unity.com/threads/standard-shader-modified-to-be-double-sided-is-very-shiny-on-the-underside.393068/

    fixed vface : VFACE;
};

void surf (Input IN, inout SurfaceOutputStandardSpecular o)
{
    fixed4 c = tex2D (_diffuseTexture, IN.uv_diffuseTexture) * IN.vertexColor * _diffuseFactor;
    o.Albedo = c.rgb;
    o.Alpha = c.a;
    o.Normal = UnpackNormal(tex2D(_normalTexture, IN.uv_normalTexture));
    if (IN.vface < 0)
        o.Normal.z *= -1.0;
    o.Occlusion = tex2D (_occlusionTexture, IN.uv_occlusionTexture).r;
    o.Emission = tex2D (_emissiveTexture, IN.uv_emissiveTexture) * _emissiveFactor;
    o.Specular = tex2D(_specularGlossinessTexture, IN.uv_specularGlossinessTexture) * _specularFactor;
    o.Smoothness = tex2D(_specularGlossinessTexture, IN.uv_specularGlossinessTexture).a * _glossinessFactor;
}

#endif