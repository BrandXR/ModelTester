using System;

namespace Piglet
{
    /// <summary>
    /// Options controlling the behavior of the glTF
    /// importer, such as automatically scaling the
    /// model to a given size.
    /// </summary>
    [Serializable]
    public class GltfImportOptions
    {
        /// <summary>
        /// Automatically show the model after a successful glTF
        /// import by calling SetActive(true) on the root GameObject
        /// (i.e. the scene object). Piglet hides the model
        /// during a glTF import so that the user never
        /// sees the model in a partially reconstructed state.
        /// An application may wish to set this option to false and
        /// handle calling SetActive(true) itself, so that it can
        /// perform additional processing before revealing the model
        /// (e.g. adding colliders).
        /// </summary>
        public bool ShowModelAfterImport;

        /// <summary>
        /// If true, automatically scale the imported glTF
        /// model to the size given by `AutoScaleSize`.
        /// More precisely, the model is uniformly scaled
        /// up or down in size such that the longest
        /// dimension of its world-space axis-aligned
        /// bounding box is equal to `AutoScaleSize`.
        /// </summary>
        public bool AutoScale;

        /// <summary>
        /// If `AutoScale` is true, the imported glTF model
        /// is automatically scaled to this size.
        /// More precisely, the model is uniformly scaled
        /// up or down in size such that the longest
        /// dimension of its world-space axis-aligned
        /// bounding box is equal to `AutoScaleSize`.
        /// </summary>
        public float AutoScaleSize;

        /// <summary>
        /// If true, import animations from glTF file
        /// as Unity AnimationClips.
        /// </summary>
        public bool ImportAnimations;

        /// <summary>
        /// Controls the type of animation clip that
        /// Piglet will create when importing animation
        /// clips (Legacy or Mecanim).
        /// </summary>
        public AnimationClipType AnimationClipType;

        /// <summary>
        /// Default constructor, which sets the default
        /// values for the various glTF import options.
        /// </summary>
        public GltfImportOptions()
        {
            ShowModelAfterImport = true;
            AutoScale = false;
            AutoScaleSize = 1f;
            ImportAnimations = true;
            AnimationClipType = AnimationClipType.Mecanim;
        }
    }
}