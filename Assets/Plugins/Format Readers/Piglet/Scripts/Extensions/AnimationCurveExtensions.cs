#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Collections;
using System.Diagnostics;
using GLTF.Schema;
using UnityEngine;

namespace Piglet
{
	/// <summary>
	/// Provides methods for computing/setting tangents of animation
	/// curves. These methods are useful when creating/importing
	/// animation curves at runtime, since Unity currently does not
	/// provide any helper methods for performing spline
	/// calculations.
	/// </summary>
    public static class AnimationCurveExtensions
	{
		/// <summary>
		/// Compute and set tangents of an animation curve to make
		/// it a linear curve.
		/// </summary>
		public static IEnumerable SetTangentsForLinearCurve(
			this AnimationCurve curve)
		{
			for (var i = 0; i < curve.length; ++i)
			{
				// Note: `curve[i]` is read-only, so we must copy
				// the keyframe struct, modify it, then set the
				// new value using curve.MoveKey(i, key).
				var key = curve[i];

				var value = (double)key.value;
				var time = (double)key.time;

				if (i > 0)
				{
					var keyPrev = curve[i - 1];
					var valuePrev = (double) keyPrev.value;
					var timePrev = (double) keyPrev.time;

					key.inTangent = (float) ((value - valuePrev) / (time - timePrev));
				}

				if (i < curve.length - 1)
				{
					var keyNext = curve[i + 1];
					var valueNext = (double) keyNext.value;
					var timeNext = (double) keyNext.time;

					key.outTangent = (float) ((valueNext - value) / (timeNext - time));
				}

				curve.MoveKey(i, key);

				if (YieldTimer.Instance.Expired)
				{
					yield return null;
					YieldTimer.Instance.Restart();
				}
			}
		}

		/// <summary>
		/// Compute and set tangents of an animation curve to make
		/// it a stepwise curve.
		/// </summary>
		public static IEnumerable SetTangentsForStepwiseCurve(
			this AnimationCurve curve)
		{
			for (var i = 0; i < curve.length; ++i)
			{
				// Note: `curve[i]` is read-only, so we must copy
				// the keyframe struct, modify it, then set the
				// new value using curve.MoveKey(i, key).
				var key = curve[i];

				key.inTangent = float.PositiveInfinity;
				key.outTangent = float.PositiveInfinity;

				curve.MoveKey(i, key);

				if (YieldTimer.Instance.Expired)
				{
					yield return null;
					YieldTimer.Instance.Restart();
				}
			}
		}

		/// <summary>
		/// Set left/right tangent values for keyframes based on the
		/// glTF interpolation type (constant, linear, or spline).
		/// </summary>
		public static IEnumerable SetTangents(
			this AnimationCurve curve,
			InterpolationType interpolationType)
		{
			// If there are zero keyframes, the animation is empty.
			// If there is only one keyframe, the animation is a
			// single fixed pose and changing the tangent slopes will
			// have no visible effect.
			if (curve.length < 2)
				yield break;

			switch (interpolationType)
			{
				case InterpolationType.STEP:
					foreach (var unused in curve.SetTangentsForStepwiseCurve())
						yield return null;
					break;
				case InterpolationType.LINEAR:
					foreach (var usused in curve.SetTangentsForLinearCurve())
						yield return null;
					break;
				case InterpolationType.CUBICSPLINE:
					// Note: In the case that interpolation type is
					// CUBICSPLINE, the tangent values have already
					// been set, because they are provided
					// by the glTF file. Instead of providing one
					// value per keyframe, three values are provided:
					// in tangent, value, and out tangent. See the
					// "sampler.interpolation" section of the
					// glTF spec further details:
					// https://github.com/KhronosGroup/glTF/tree/master/specification/2.0#animation-samplerinterpolation					break;
					break;
			}

#if UNITY_EDITOR
			// If we are importing the animation as part of
			// an Editor glTF import, we need to set some
			// additional properties on the animation curve
			// so that the tangents show up correctly
			// in Unity's Curve Editor.
			if (!Application.isPlaying)
			{
				foreach (var unused in SetCurveEditorTangentModes(curve, interpolationType))
					yield return null;
			}
#endif
		}

#if UNITY_EDITOR
		/// <summary>
		/// Set the tangent modes for each keyframe in
		/// Unity's Animation Curve Editor. The current
		/// tangent mode for a keyframe can be viewed/edited
		/// by right-clicking the keyframe in the Curve
		/// Editor. These tangent modes are only used for
		/// auto-calculating spline tangents in the Editor
		/// and have no effect on animation curves created at runtime.
		/// (For runtime animation curves we must calculate and
		/// set the tangent values ourselves.)
		/// </summary>
		private static IEnumerable SetCurveEditorTangentModes(
			this AnimationCurve curve, InterpolationType interpolationType)
		{
			var tangentMode = AnimationUtility.TangentMode.Auto;
			switch (interpolationType)
			{
				case InterpolationType.LINEAR:
					tangentMode = AnimationUtility.TangentMode.Linear;
					break;
				case InterpolationType.STEP:
					tangentMode = AnimationUtility.TangentMode.Constant;
					break;
				case InterpolationType.CUBICSPLINE:
					tangentMode = AnimationUtility.TangentMode.Free;
					break;
			}

			for (var i = 0; i < curve.length; ++i)
			{
				// If the broken flag is true, the in tangent
				// and out tangent values for the keyframe are not
				// required to be equal, and the user is allowed
				// to edit the two tangent slopes independently
				// in Unity's Curve Editor window. In mathematical terms,
				// the curve is permitted to be discontinuous.
				//
				// In the case that the tangent mode for the keyframe is
				// linear or constant (a.k.a. stepwise), Unity
				// fixes the broken flag to true and the value
				// we set here makes no difference. In the
				// case that the tangent mode is free (a.k.a. cubic),
				// the glTF file provides the values for the
				// in tangents and out tangents [1], and they are not
				// required to be equal. Thus, in the case of cubic tangents,
				// we need the set the broken flag to true by
				// default, so that Unity will respect the tangent
				// values specified in the glTF file.
				//
				// [1]: https://github.com/KhronosGroup/glTF/tree/master/specification/2.0#animation-samplerinterpolation

				AnimationUtility.SetKeyBroken(curve, i, true);

				AnimationUtility.SetKeyLeftTangentMode(curve, i, tangentMode);
				AnimationUtility.SetKeyRightTangentMode(curve, i, tangentMode);

				if (YieldTimer.Instance.Expired)
				{
					yield return null;
					YieldTimer.Instance.Restart();
				}
			}
		}
#endif
	}
}
