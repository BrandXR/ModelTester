using System;
using UnityEngine.Rendering;

namespace Piglet
{
    /// <summary>
    /// Utility methods for querying the active render pipeline
    /// (e.g. BuiltIn, URP, HDRP), since Unity currently does
    /// not provide an API for this. These methods work
    /// in both the Editor and in standalone builds.
    /// </summary>
    public static class RenderPipelineUtil
    {
        /// <summary>
        /// Return the active render pipeline (BuiltIn, URP, or HDRP).
        /// Based on: https://forum.unity.com/threads/how-to-tell-if-srp-is-active-in-script-or-shaders.776960/#post-5172332
        /// </summary>
        static public RenderPipelineType GetRenderPipeline()
        {
            var pipeline = GraphicsSettings.renderPipelineAsset;

#if UNITY_2019_3_OR_NEWER
            if (pipeline != null) {

                // Note: It would be simpler/cleaner to just
                // use `pipeline is HDRenderPipelineAsset` and
                // `pipeline is UniversalRenderPipelineAsset` in
                // our if tests here. However, we can't do this
                // because these types are only defined when URP/HDRP
                // is the active render pipeline.

                var type = pipeline.GetType().ToString();

                if (type.Contains("HDRenderPipelineAsset"))
                    return RenderPipelineType.Unsupported;

                if (type.Contains("UniversalRenderPipelineAsset"))
                    return RenderPipelineType.URP;

                return RenderPipelineType.Unsupported;
            }
#else
            if (pipeline != null) {
                // SRP not supported before 2019.3
                return RenderPipelineType.Unsupported;
            }
#endif

            return RenderPipelineType.BuiltIn;
        }

        /// <summary>
        /// Return the active render pipeline (BuiltIn, URP, or HDRP).
        /// </summary>
        /// <param name="throwException">
        /// If true, throw an exception if the active render pipeline
        /// is unsupported in Piglet. If false, return
        /// RenderPipelineType.Unsupported instead.
        /// </param>
        static public RenderPipelineType GetRenderPipeline(bool throwException)
        {
            var pipeline = GetRenderPipeline();

            if (throwException && pipeline == RenderPipelineType.Unsupported)
            {
                throw new NotSupportedException(
                    "The currently active render pipeline is not " +
                    "supported by Piglet. This version of Piglet supports " +
                    "the built-in render pipeline in Unity 2018.4+ and " +
                    "URP in Unity 2019.3+.");
            }

            return pipeline;
        }
    }
}