// This shader is written to implement a glTF material
// using the "metallic roughness" model (physically based
// rendering).
//
// See: https://github.com/KhronosGroup/glTF/tree/master/specification/2.0#reference-pbrmetallicroughness

Shader "Piglet/MetallicRoughnessOpaque"
{
    Properties
    {
        // The following properties correspond to basic glTF material properties
        // described at:
        //
        // https://github.com/KhronosGroup/glTF/tree/master/specification/2.0#reference-material

        _normalTexture ("Normal Map", 2D) = "bump" {}

        _occlusionTexture ("Occlusion Map", 2D) = "white" {}

        _emissiveFactor ("Emissive Factor", Color) = (1, 1, 1, 1)
        _emissiveTexture ("Emission Map", 2D) = "black" {}

        // The following properties correspond to metallic-roughness model properties
        // described at:
        //
        // https://github.com/KhronosGroup/glTF/tree/master/specification/2.0#reference-pbrmetallicroughness

        _baseColorFactor ("Base Color Factor", Color) = (1, 1, 1, 1)
        _baseColorTexture ("Base Color Texture", 2D) = "white" {}

        _roughnessFactor ("Roughness Factor", Range(0,1)) = 1.0
        _metallicFactor ("Metallic Factor", Range(0,1)) = 1.0
        _metallicRoughnessTexture ("Metallic Roughness Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Cull Off
        LOD 200

        CGPROGRAM
        #pragma target 3.0
        #pragma surface surf Standard noshadow nolightmap nofog nometa nolppv
        #include "MetallicRoughness.cginc"
        ENDCG
    }
    FallBack "Diffuse"
}
