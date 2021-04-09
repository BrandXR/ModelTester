using System;
using System.Collections.Generic;
using UnityEngine;

namespace Piglet
{
    /// <summary>
    /// <para>
    /// This component stores an ordered list of the imported
    /// animation clips along with their original names
    /// from the glTF file. The `.name` field of the
    /// animation clips are used as keys for playing back
    /// the animations at runtime with Unity's Animation/Animator
    /// components. The value of `clip.name` is identical to
    /// the original name from the glTF file, with the exception
    /// that characters which not allowed in Unity asset
    /// filenames or AnimationController state names are masked
    /// with '_'.
    /// </para>
    ///
    /// <para>
    /// In addition to recovering the original names for the
    /// animation clips, this component is also useful for
    /// recovering the original order of the clips from
    /// the glTF file. Neither the Animation component
    /// (for playing Legacy clips) nor the Animator component
    /// (for playing Mecanim clips) has any notion of clip order.
    /// </para>
    ///
    /// <para>
    /// The idea for this component was inspired by:
    /// https://answers.unity.com/questions/8245/how-to-select-an-animation-clip-by-index-number.html
    /// </para>
    /// </summary>
    public class AnimationList : MonoBehaviour
    {
        /// <summary>
        /// The list of imported animation clips.
        /// The order of the clips corresponds to the original
        /// order of the animations in the glTF file, with the
        /// exception that a special "Static Pose" clip is
        /// inserted at index 0.
        /// </summary>
        public List<AnimationClip> Clips;

        /// <summary>
        /// <para>
        /// The names of the imported animation clips. These
        /// strings exactly match the animation names provided
        /// in the glTF file, and are intended for use as labels
        /// in user interfaces. In contrast, the ".name" field
        /// of an AnimationClip is used for the asset filename
        /// and as the key to access the clip in the
        /// Animation/Animator components, and as result may
        /// be altered from the original name given in the
        /// glTF file.
        /// </para>
        ///
        /// <para>
        /// The order of the animation names in this list
        /// corresponds to the original order of the animations
        /// in the glTF file, with the exception that a special
        /// "Static Pose" clip is inserted at index 0.
        /// </para>
        /// </summary>
        public List<string> Names;
    }
}
