using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Piglet
{
    public static class AnimationUtil
    {
        /// <summary>
        /// Create an animation clip for a static pose (a.k.a. bind pose
        /// or T-pose) which captures the current transform values
        /// (position/rotation/scale) for the given game object
        /// and all of its descendants. The purpose of a static pose clip is to
        /// restore the original pose of a model after it has been
        /// changed by playing an animation (e.g. a running animation
        /// for a game character). This is needed because the
        /// Unity animation system directly alters the position/rotation/scale
        /// values of the target game objects, without keeping any record of
        /// the original values.
        /// </summary>
        public static IEnumerable<AnimationClip> CreateStaticPoseClip(
            GameObject o, bool legacy = false)
        {
            var clip = new AnimationClip { name = "Static Pose", legacy = legacy };

            YieldTimer.Instance.Restart();

            foreach (var unused in CreateStaticPoseClipImpl(o, "", clip))
                yield return null;

            yield return clip;
        }

        /// <summary>
        /// Recursive depth-first method for creating a static pose animation clip
        /// (a.k.a. bind pose or T-pose). See the documentation for
        /// <see cref="CreateStaticPoseClip" /> for an explanation of when/why you
        /// would want to use a static pose animation clip.
        /// </summary>
        /// <param name="o">
        /// the current GameObject in the target hierarchy
        /// of GameObjects
        /// </param>
        /// <param name="parentPath">
        /// The slash-separated path of GameObject names that
        /// identifies the parent GameObject (e.g. "Torso/LeftLeg/LeftFoot").
        /// The Unity animation API uses such paths to identify
        /// GameObjects within a hierarchy.
        /// </param>
        /// <param name="clip">
        /// the output AnimationClip
        /// </param>
        private static IEnumerable CreateStaticPoseClipImpl(
            GameObject o, string parentPath, AnimationClip clip)
        {
            string path = string.Format("{0}/{1}", parentPath, o.name);

            var position = o.transform.localPosition;

            // position

            var positionCurveX = new AnimationCurve();
            positionCurveX.AddKey(0, position.x);
            positionCurveX.AddKey(1, position.x);
            clip.SetCurve(path, typeof(Transform), "m_LocalPosition.x", positionCurveX);

            var positionCurveY = new AnimationCurve();
            positionCurveY.AddKey(0, position.y);
            positionCurveY.AddKey(1, position.y);
            clip.SetCurve(path, typeof(Transform), "m_LocalPosition.y", positionCurveY);

            var positionCurveZ = new AnimationCurve();
            positionCurveZ.AddKey(0, position.z);
            positionCurveZ.AddKey(1, position.z);
            clip.SetCurve(path, typeof(Transform), "m_LocalPosition.z", positionCurveZ);

            // rotation (quaternion)

            var rotation = o.transform.localRotation;

            var rotationCurveX = new AnimationCurve();
            rotationCurveX.AddKey(0, rotation.x);
            rotationCurveX.AddKey(1, rotation.x);
            clip.SetCurve(path, typeof(Transform), "m_LocalRotation.x", rotationCurveX);

            var rotationCurveY = new AnimationCurve();
            rotationCurveY.AddKey(0, rotation.y);
            rotationCurveY.AddKey(1, rotation.y);
            clip.SetCurve(path, typeof(Transform), "m_LocalRotation.y", rotationCurveY);

            var rotationCurveZ = new AnimationCurve();
            rotationCurveZ.AddKey(0, rotation.z);
            rotationCurveZ.AddKey(1, rotation.z);
            clip.SetCurve(path, typeof(Transform), "m_LocalRotation.z", rotationCurveZ);

            var rotationCurveW = new AnimationCurve();
            rotationCurveW.AddKey(0, rotation.w);
            rotationCurveW.AddKey(1, rotation.w);
            clip.SetCurve(path, typeof(Transform), "m_LocalRotation.w", rotationCurveW);

            // scale

            var scale = o.transform.localScale;

            var scaleCurveX = new AnimationCurve();
            scaleCurveX.AddKey(0, scale.x);
            scaleCurveX.AddKey(1, scale.x);
            clip.SetCurve(path, typeof(Transform), "m_LocalScale.x", scaleCurveX);

            var scaleCurveY = new AnimationCurve();
            scaleCurveY.AddKey(0, scale.y);
            scaleCurveY.AddKey(1, scale.y);
            clip.SetCurve(path, typeof(Transform), "m_LocalScale.y", scaleCurveY);

            var scaleCurveZ = new AnimationCurve();
            scaleCurveZ.AddKey(0, scale.z);
            scaleCurveZ.AddKey(1, scale.z);
            clip.SetCurve(path, typeof(Transform), "m_LocalScale.z", scaleCurveZ);

            // morph target weights (a.k.a. blend shape weights)

            var smr = o.GetComponent<SkinnedMeshRenderer>();

            if (smr != null)
            {
                var mesh = smr.sharedMesh;

                for (var i = 0; i < mesh.blendShapeCount; ++i)
                {
                    var weight = smr.GetBlendShapeWeight(i);

                    var curve = new AnimationCurve();
                    curve.AddKey(0, weight);
                    curve.AddKey(1, weight);

                    var name = string.Format("blendShape.{0}", mesh.GetBlendShapeName(i));
                    clip.SetCurve(path, typeof(SkinnedMeshRenderer), name, curve);
                }
            }

            if (YieldTimer.Instance.Expired)
            {
                yield return null;
                YieldTimer.Instance.Restart();
            }

            // recurse

            foreach (Transform child in o.transform)
            {
                foreach (var unused in
                    CreateStaticPoseClipImpl(child.gameObject, path, clip))
                {
                    yield return null;
                }

            }
        }
    }
}