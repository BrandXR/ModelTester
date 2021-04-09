// This shader is written to implement a glTF material
// using the "specular glossiness" model (physically based
// rendering).
//
// The specular-glossiness is an extension to the
// glTF described at:
//
// https://github.com/KhronosGroup/glTF/blob/master/extensions/2.0/Khronos/KHR_materials_pbrSpecularGlossiness/README.md

Shader "Piglet/SpecularGlossinessOpaque"
{
    Properties
    {
        // The following properties correspond to basic glTF material properties
        // described at:
        //
        // https://github.com/KhronosGroup/glTF/tree/master/specification/2.0#reference-material

        _normalTexture ("Normal Map", 2D) = "bump" {}

        _occlusionTexture ("Occlusion Map", 2D) = "white" {}

        _emissiveTexture ("Emission Map", 2D) = "black" {}
        _emissiveFactor ("Emissive Factor", Color) = (1, 1, 1, 1)

        // The following properties correspond to specular-glossiness
        // model properties described at:
        //
        // https://github.com/KhronosGroup/glTF/blob/master/extensions/2.0/Khronos/KHR_materials_pbrSpecularGlossiness/README.md

        _diffuseFactor ("Diffuse Factor", Color) = (1, 1, 1, 1)
        _diffuseTexture ("Diffuse Texture", 2D) = "white" {}

        _specularFactor ("Specular Factor", Color) = (1, 1, 1, 1)
        _glossinessFactor ("Glossiness Factor", Range(0,1)) = 1.0
        _specularGlossinessTexture ("Specular Glossiness Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Cull Off
        LOD 200

        CGPROGRAM
        #pragma target 3.0
        #pragma surface surf StandardSpecular noshadow nolightmap nofog nometa nolppv
        #include "SpecularGlossiness.cginc"
        ENDCG
    }
    FallBack "Diffuse"
}
