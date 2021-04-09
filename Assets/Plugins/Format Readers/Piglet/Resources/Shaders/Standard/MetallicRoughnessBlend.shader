// This shader is written to implement a glTF material
// using the "metallic roughness" model (physically based
// rendering).
//
// See: https://github.com/KhronosGroup/glTF/tree/master/specification/2.0#reference-pbrmetallicroughness

Shader "Piglet/MetallicRoughnessBlend"
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
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Cull Off
        LOD 200

        // Add a preliminary shader pass that writes to the Z-buffer but
        // doesn't render any geometry.
        //
        // Without this pass, semi-transparent triangles will be drawn
        // in whatever order they appear in the mesh, rather than
        // in their proper depth-sorted order.  This happens because
        // transparent shaders don't write to the Z-buffer. (The
        // Z-buffer-based approach to depth-culling only works correctly
        // for opaque geometry.)
        //
        // For further background/discussion, see the following links:
        //
        // (1) https://forum.unity.com/threads/render-mode-transparent-doesnt-work-see-video.357853/#post-2315934
        // (2) https://answers.unity.com/questions/609021/how-to-fix-transparent-rendering-problem.html

        Pass { ColorMask 0 }

        CGPROGRAM
        #pragma target 3.0
        #pragma surface surf Standard alpha:fade noshadow nolightmap nofog nometa nolppv
        #include "MetallicRoughness.cginc"
        ENDCG
    }
    FallBack "Diffuse"
}
