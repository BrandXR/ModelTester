using System;
using System.Collections;
using GLTF.Schema;
using UnityEngine;

namespace Piglet
{
	/// <summary>
	/// Provides convenience methods for adding curves to animation
	/// clips from Vector3 arrays, Vector4 arrays, and float arrays. These
	/// methods incorporate Piglet's code for calculating animation curve tangents
	/// at runtime, so that they can be used from both runtime and Editor
	/// scripts. The methods in this class are implemented as coroutines
	/// in order to minimize interruptions to the main Unity thread during
	/// runtime glTF imports.
	/// </summary>
	public static class AnimationClipExtensions
	{
		/// <summary>
		/// Coroutine that adds x/y/z animation curves from an
		/// array of Vector3 values.
		/// </summary>
		/// <param name="clip">
		/// The target animation clip that will store the new
		/// x/y/z animation curves.
		/// </param>
		/// <param name="nodePath">
		/// A slash-separated path of GameObject names that identifies
		/// the target GameObject that will be animated (e.g.
		/// "LeftLeg/LeftFoot/BigToe"). The path is specified relative
		/// to the root GameObject for the model, which contains
		/// the Animation/Animator component for playing back the animation
		/// clip at runtime.
		/// </param>
		/// <param name="type">
		/// The type of GameObject component that will be animated
		/// (e.g. `typeof(Transform)`, `typeof(SkinnedMeshRenderer)`).
		/// </param>
		/// <param name="property">
		/// The property of the target GameObject component that will
		/// be animated (e.g. `m_LocalScale`).
		/// </param>
		/// <param name="times">
		/// An array of floats specifying the time values for the
		/// keyframes.
		/// </param>
		/// <param name="values">
		/// An array of Vector3 values specifying the x/y/z keyframe
		/// values of the animated property (e.g. `m_LocalScale`).
		/// </param>
		/// <param name="valueMapper">
		/// A function that is applied to each Vector3
		/// prior to assigning its x/y/z values.
		/// This function is typically used for transforming the
		/// input vectors between glTF's right-handed coordinate system
		/// and Unity's left-handed coordinate system.
		/// </param>
		/// <param name="interpolationType">
		/// The type of interpolation to use between keyframes (e.g.
		/// cubic, linear, stepwise).
		/// </param>
		public static IEnumerable SetCurvesFromVector3Array(
			this AnimationClip clip,
			string nodePath,
			Type type,
			string property,
			float[] times,
			Vector3[] values,
			Func<Vector3, Vector3> valueMapper,
			InterpolationType interpolationType)
		{
			var curveX = new AnimationCurve();
			var curveY = new AnimationCurve();
			var curveZ = new AnimationCurve();

			// Note: In the case that interpolation type is
			// CUBICSPLINE, three values are provided for
			// each keyframe: in tangent, value, out tangent.
			// For the other interpolation types (STEP and
			// LINEAR), only the values are provided and
			// we must calculate the tangents ourselves.
			// See the "sampler.interpolation"
			// section of the glTF 2.0 spec further details:
			// https://github.com/KhronosGroup/glTF/tree/master/specification/2.0#animation-samplerinterpolation

			var numKeyframes = 0;
			switch (interpolationType)
			{
				case InterpolationType.STEP:
				case InterpolationType.LINEAR:

					numKeyframes = Mathf.Min(times.Length, values.Length);

					// set values for each keyframe

					for (var i = 0; i < numKeyframes; ++i)
					{
						var value = values[i];
						if (valueMapper != null)
							value = valueMapper(value);

						curveX.AddKey(new Keyframe(times[i] - times[0], value.x));
						curveY.AddKey(new Keyframe(times[i] - times[0], value.y));
						curveZ.AddKey(new Keyframe(times[i] - times[0], value.z));

						if (YieldTimer.Instance.Expired)
						{
							yield return null;
							YieldTimer.Instance.Restart();
						}
					}

					break;

				case InterpolationType.CUBICSPLINE:

					// Note: We are using `values.Length * 3` here because
					// when interpolation type is CUBICSPLINE, there are
					// three values provided per keyframe: in tangent,
					// value, out tangent. See the "sampler.interpolation"
					// section of the glTF spec further details:
					// https://github.com/KhronosGroup/glTF/tree/master/specification/2.0#animation-samplerinterpolation

					numKeyframes = Mathf.Min(times.Length, values.Length * 3);

					// set values for each keyframe

					for (var i = 0; i < numKeyframes; ++i)
					{
						var inTangent = values[i * 3 + 0];

						var value = values[i * 3 + 1];
						if (valueMapper != null)
							value = valueMapper(value);

						var outTangent = values[i * 3 + 2];

						curveX.AddKey(new Keyframe(times[i] - times[0], value.x,
							inTangent.x, outTangent.x));

						curveY.AddKey(new Keyframe(times[i] - times[0], value.y,
							inTangent.y, outTangent.y));

						curveZ.AddKey(new Keyframe(times[i] - times[0], value.z,
							inTangent.z, outTangent.z));

						if (YieldTimer.Instance.Expired)
						{
							yield return null;
							YieldTimer.Instance.Restart();
						}
					}

					break;
			}

			foreach (var unused in curveX.SetTangents(interpolationType))
				yield return null;

			foreach (var unused in curveY.SetTangents(interpolationType))
				yield return null;

			foreach (var unused in curveZ.SetTangents(interpolationType))
				yield return null;

			clip.SetCurve(nodePath, type, string.Format("{0}.x", property), curveX);

			if (YieldTimer.Instance.Expired)
			{
				yield return null;
				YieldTimer.Instance.Restart();
			}

			clip.SetCurve(nodePath, type, string.Format("{0}.y", property), curveY);

			if (YieldTimer.Instance.Expired)
			{
				yield return null;
				YieldTimer.Instance.Restart();
			}

			clip.SetCurve(nodePath, type, string.Format("{0}.z", property), curveZ);

			if (YieldTimer.Instance.Expired)
			{
				yield return null;
				YieldTimer.Instance.Restart();
			}
		}

		/// <summary>
		/// Coroutine that adds x/y/z/w animation curves from an
		/// array of Vector4 values.
		/// </summary>
		/// <param name="clip">
		/// The target animation clip that will store the new
		/// x/y/z/w animation curves.
		/// </param>
		/// <param name="nodePath">
		/// A slash-separated path of GameObject names that identifies
		/// the target GameObject that will be animated (e.g.
		/// "LeftLeg/LeftFoot/BigToe"). The path is specified relative
		/// to the root GameObject for the model, which contains
		/// the Animation/Animator component for playing back the animation
		/// clip at runtime.
		/// </param>
		/// <param name="type">
		/// The type of GameObject component that will be animated
		/// (e.g. `typeof(Transform)`, `typeof(SkinnedMeshRenderer)`).
		/// </param>
		/// <param name="property">
		/// The property of the target GameObject component that will
		/// be animated (e.g. `m_LocalScale`).
		/// </param>
		/// <param name="times">
		/// An array of floats specifying the time values for the
		/// keyframes.
		/// </param>
		/// <param name="values">
		/// An array of Vector4 values specifying the x/y/z/w keyframe
		/// values of the animated property (e.g. `m_LocalScale`).
		/// </param>
		/// <param name="valueMapper">
		/// A function that is applied to each Vector4
		/// prior to assigning its x/y/z/w values.
		/// This function is typically used for transforming the
		/// input vectors between glTF's right-handed coordinate system
		/// and Unity's left-handed coordinate system.
		/// </param>
		/// <param name="interpolationType">
		/// The type of interpolation to use between keyframes (e.g.
		/// cubic, linear, stepwise).
		/// </param>
		public static IEnumerable SetCurvesFromVector4Array(
			this AnimationClip clip,
			string nodePath,
			Type type,
			string property,
			float[] times,
			Vector4[] values,
			Func<Vector4, Vector4> valueMapper,
			InterpolationType interpolationType)
		{
			var curveX = new AnimationCurve();
			var curveY = new AnimationCurve();
			var curveZ = new AnimationCurve();
			var curveW = new AnimationCurve();

			// Note: In the case that interpolation type is
			// CUBICSPLINE, three values are provided for
			// each keyframe: in tangent, value, out tangent.
			// For the other interpolation types (STEP and
			// LINEAR), only the values are provided and
			// we must calculate the tangents ourselves.
			// See the "sampler.interpolation"
			// section of the glTF 2.0 spec further details:
			// https://github.com/KhronosGroup/glTF/tree/master/specification/2.0#animation-samplerinterpolation

			var numKeyframes = 0;
			switch (interpolationType)
			{
				case InterpolationType.STEP:
				case InterpolationType.LINEAR:

					numKeyframes = Mathf.Min(times.Length, values.Length);

					// set values for each keyframe

					for (var i = 0; i < numKeyframes; ++i)
					{
						var value = values[i];
						if (valueMapper != null)
							value = valueMapper(value);

						curveX.AddKey(new Keyframe(times[i] - times[0], value.x));
						curveY.AddKey(new Keyframe(times[i] - times[0], value.y));
						curveZ.AddKey(new Keyframe(times[i] - times[0], value.z));
						curveW.AddKey(new Keyframe(times[i] - times[0], value.w));

						if (YieldTimer.Instance.Expired)
						{
							yield return null;
							YieldTimer.Instance.Restart();
						}
					}

					break;

				case InterpolationType.CUBICSPLINE:

					// Note: We are using `values.Length * 3` here because
					// when interpolation type is CUBICSPLINE, there are
					// three values provided per keyframe: in tangent,
					// value, out tangent. See the "sampler.interpolation"
					// section of the glTF spec further details:
					// https://github.com/KhronosGroup/glTF/tree/master/specification/2.0#animation-samplerinterpolation

					numKeyframes = Mathf.Min(times.Length, values.Length * 3);

					// set values for each keyframe

					for (var i = 0; i < numKeyframes; ++i)
					{
						var inTangent = values[i * 3 + 0];

						var value = values[i * 3 + 1];
						if (valueMapper != null)
							value = valueMapper(value);

						var outTangent = values[i * 3 + 2];

						curveX.AddKey(new Keyframe(times[i] - times[0], value.x,
							inTangent.x, outTangent.x));

						curveY.AddKey(new Keyframe(times[i] - times[0], value.y,
							inTangent.y, outTangent.y));

						curveZ.AddKey(new Keyframe(times[i] - times[0], value.z,
							inTangent.z, outTangent.z));

						curveW.AddKey(new Keyframe(times[i] - times[0], value.w,
							inTangent.w, outTangent.w));

						if (YieldTimer.Instance.Expired)
						{
							yield return null;
							YieldTimer.Instance.Restart();
						}
					}

					break;
			}

			foreach (var unused in curveX.SetTangents(interpolationType))
				yield return null;

			foreach (var unused in curveY.SetTangents(interpolationType))
				yield return null;

			foreach (var unused in curveZ.SetTangents(interpolationType))
				yield return null;

			foreach (var unused in curveW.SetTangents(interpolationType))
				yield return null;

			clip.SetCurve(nodePath, type, string.Format("{0}.x", property), curveX);

			if (YieldTimer.Instance.Expired)
			{
				yield return null;
				YieldTimer.Instance.Restart();
			}

			clip.SetCurve(nodePath, type, string.Format("{0}.y", property), curveY);

			if (YieldTimer.Instance.Expired)
			{
				yield return null;
				YieldTimer.Instance.Restart();
			}

			clip.SetCurve(nodePath, type, string.Format("{0}.z", property), curveZ);

			if (YieldTimer.Instance.Expired)
			{
				yield return null;
				YieldTimer.Instance.Restart();
			}

			clip.SetCurve(nodePath, type, string.Format("{0}.w", property), curveW);

			if (YieldTimer.Instance.Expired)
			{
				yield return null;
				YieldTimer.Instance.Restart();
			}
		}

		/// <summary>
		/// Coroutine that adds an animation curve from an
		/// array of float values.
		/// </summary>
		/// <param name="clip">
		/// The target animation clip that will store the new
		/// animation curve.
		/// </param>
		/// <param name="nodePath">
		/// A slash-separated path of GameObject names that identifies
		/// the target GameObject that will be animated (e.g.
		/// "LeftLeg/LeftFoot/BigToe"). The path is specified relative
		/// to the root GameObject for the model, which contains
		/// the Animation/Animator component for playing back the animation
		/// clip at runtime.
		/// </param>
		/// <param name="type">
		/// The type of GameObject component that will be animated
		/// (e.g. `typeof(Transform)`, `typeof(SkinnedMeshRenderer)`).
		/// </param>
		/// <param name="property">
		/// The property of the target GameObject component that will
		/// be animated (e.g. `m_LocalScale`).
		/// </param>
		/// <param name="times">
		/// An array of floats specifying the time values for the
		/// keyframes.
		/// </param>
		/// <param name="values">
		/// An array of floats specifying the keyframe values
		/// for the animated property (e.g. `m_LocalScale.x`).
		/// </param>
		/// <param name="valueIndexMapper">
		/// A function that maps the current keyframe
		/// index to the corresponding index in the `values` array.
		/// This function is used to unpack weight values for different
		/// morph targets from a shared array of all morph target
		/// weights.
		/// </param>
		/// <param name="interpolationType">
		/// The type of interpolation to use between keyframes (e.g.
		/// cubic, linear, stepwise).
		/// </param>
		public static IEnumerable SetCurveFromFloatArray(
			this AnimationClip clip,
			string nodePath,
			Type type,
			string property,
			float[] times,
			float[] values,
			Func<int, int> valueIndexMapper,
			InterpolationType interpolationType)
		{
			var curve = new AnimationCurve();

			// Note: In the case that interpolation type is
			// CUBICSPLINE, three values are provided for
			// each keyframe: in tangent, value, out tangent.
			// For the other interpolation types (STEP and
			// LINEAR), only the values are provided and
			// we must calculate the tangents ourselves.
			// See the "sampler.interpolation"
			// section of the glTF 2.0 spec further details:
			// https://github.com/KhronosGroup/glTF/tree/master/specification/2.0#animation-samplerinterpolation

			var numKeyframes = 0;
			switch (interpolationType)
			{
				case InterpolationType.STEP:
				case InterpolationType.LINEAR:

					numKeyframes = Mathf.Min(times.Length, values.Length);
					for (var i = 0; i < numKeyframes; ++i)
					{
						var valueIndex = i;
						if (valueIndexMapper != null)
							valueIndex = valueIndexMapper(valueIndex);

						curve.AddKey(new Keyframe(times[i] - times[0], values[valueIndex]));

						if (YieldTimer.Instance.Expired)
						{
							yield return null;
							YieldTimer.Instance.Restart();
						}
					}

					break;

				case InterpolationType.CUBICSPLINE:

					// Note: We are using `values.Length * 3` here because
					// when interpolation type is CUBICSPLINE, there are
					// three values provided per keyframe: in tangent,
					// value, out tangent. See the "sampler.interpolation"
					// section of the glTF spec further details:
					// https://github.com/KhronosGroup/glTF/tree/master/specification/2.0#animation-samplerinterpolation

					numKeyframes = Mathf.Min(times.Length, values.Length * 3);

					for (var i = 0; i < numKeyframes; ++i)
					{
						var valueIndex = i;
						if (valueIndexMapper != null)
							valueIndex = valueIndexMapper(valueIndex);

						var inTangent = values[valueIndex * 3 + 0];
						var value = values[valueIndex * 3 + 1];
						var outTangent = values[valueIndex * 3 + 2];

						curve.AddKey(new Keyframe(times[i] - times[0],
							value, inTangent, outTangent));

						if (YieldTimer.Instance.Expired)
						{
							yield return null;
							YieldTimer.Instance.Restart();
						}
					}

					break;
			}

			foreach (var unused in curve.SetTangents(interpolationType))
				yield return null;

			clip.SetCurve(nodePath, type, property, curve);

			if (YieldTimer.Instance.Expired)
			{
				yield return null;
				YieldTimer.Instance.Restart();
			}
		}
	}
}