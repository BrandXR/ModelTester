using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Collections;
using UnityEngine.Networking;

#if KTX_UNITY_0_9_1_OR_NEWER
using KtxUnity;
#endif

namespace Piglet
{
	/// <summary>
	/// Utility methods for reading/loading textures.
	/// </summary>
	public static class TextureUtil
	{
		/// <summary>
		/// The initial bytes ("magic numbers") of a file/stream that are used
		/// to identify different image formats (e.g. PNG, JPG, KTX2).
		/// </summary>
		private struct Magic
		{
			/// <summary>
			/// KTX2 is a container format for supercompressed and GPU-ready textures.
			/// For further info, see: https://github.khronos.org/KTX-Specification/.
			/// I got the values for the KTX2 magic bytes by examining an example
			/// KTX2 files with the Linux `od` tool, e.g.
			/// `od -A n -N 12 -t u1 myimage.ktx2`. The magic byte values are also
			/// given in Section 3.1 of https://github.khronos.org/KTX-Specification/.
			/// </summary>
			public static readonly byte[] KTX2 = { 171, 75, 84, 88, 32, 50, 48, 187, 13, 10, 26, 10 };
		}

		/// <summary>
		/// Return true if the given byte array is a KTX2 image, or false otherwise.
		/// KTX2 is a container format for supercompressed or GPU-ready textures.
		/// For further info, see: https://github.khronos.org/KTX-Specification/.
		/// </summary>
		/// <param name="data"></param>
		/// <returns></returns>
		public static bool IsKtx2Data(byte[] data)
		{
			return Magic.KTX2.SequenceEqual(data.Take(Magic.KTX2.Length));
		}

#if KTX_UNITY_0_9_1_OR_NEWER
		/// <summary>
		/// Load a Texture2D from the given KTX2 image (byte array), using the
		/// KtxUnity package: https://github.com/atteneder/KtxUnity.
		/// </summary>
		public static IEnumerable<Texture2D> LoadKtx2Data(byte[] data)
		{
			var ktxTexture = new KtxTexture();

#if KTX_UNITY_1_0_0_OR_NEWER
			// In KtxUnity 1.0.0, KtxUnity switched from a coroutine-based API
			// to an async/await-based API.
			//
			// For a helpful overview of the differences between coroutine
			// (IEnumerator) methods and async/await methods, including
			// examples of how to translate between the two types of
			// methods, see the following blog post:
			//
			// http://www.stevevermeulen.com/index.php/2017/09/using-async-await-in-unity3d-2017/

			using (var na = new NativeArray<byte>(data, KtxNativeInstance.defaultAllocator))
			{
				var task = ktxTexture.LoadFromBytes(na);

				while (!task.IsCompleted)
					yield return null;

				if (task.IsFaulted)
					throw task.Exception;

				yield return task.Result.texture;
			}
#else
			// In version 0.9.1 and older, KtxUnity used a coroutine
			// (IEnumerator) based API, rather than an async/await-based
			// API.

			Texture2D result = null;

			ktxTexture.onTextureLoaded += (texture, _) => { result = texture; };

			using (var na = new NativeArray<byte>(data, KtxNativeInstance.defaultAllocator))
			{
				// We use a stack here because KtxUnity's `LoadBytesRoutine` returns
				// nested IEnumerators, and we need to iterate/execute
				// through them in depth-first order.
				//
				// `LoadBytesRoutine` works as-is when run with Unity's
				// `MonoBehaviour.StartCoroutine` because the Unity game loop
				// implements nested execution of IEnumerators as a special behaviour.
				// Piglet does not have the option of using `StartCoroutine`
				// because it needs to run `LoadBytesRoutine` outside of Play Mode
				// during Editor glTF imports.

				var task = new Stack<IEnumerator>();
				task.Push(ktxTexture.LoadBytesRoutine(na));
				while (task.Count > 0)
				{
					if (!task.Peek().MoveNext())
						task.Pop();
					else if (task.Peek().Current is IEnumerator)
						task.Push((IEnumerator)task.Peek().Current);
				}
			}

			yield return result;
#endif
		}
#endif

		/// <summary>
		/// Flip a texture upside down. This operation is needed
		/// because `Texture.LoadImage` imports .png/.jpg images
		/// into textures upside down (I don't know why).
		///
		/// Note that this method does not work reliably in
		/// WebGL builds. In particular, it was generating black textures
		/// in Chrome 79.0.3945.79 (64-bit).  It seems that
		/// the `Graphics.Blit` operation is not working as intended
		/// or is not completing before the `Texture2D.ReadPixels`
		/// operation is performed, and I was never able to figure
		/// out why.
		/// </summary>
		public static Texture2D FlipTexture(Texture2D texture)
		{
			Material flippedMaterial = new Material(
				Shader.Find("Piglet/FlipTexture"));

			var flippedTexture = new Texture2D(
				texture.width, texture.height,
				texture.format, texture.mipmapCount > 1);
			flippedTexture.name = texture.name;

			var renderTexture = RenderTexture.GetTemporary(
				texture.width, texture.height, 32,
				RenderTextureFormat.ARGB32);

			Graphics.Blit(texture, renderTexture, flippedMaterial);

			RenderTexture prevActive = RenderTexture.active;
			RenderTexture.active = renderTexture;

			flippedTexture.ReadPixels(
				new Rect(0, 0, texture.width, texture.height),
				0, 0, texture.mipmapCount > 1);
			flippedTexture.Apply();

			RenderTexture.active = prevActive;
			RenderTexture.ReleaseTemporary(renderTexture);

			return flippedTexture;
		}

		/// <summary>
		/// Return a "readable" version of a Texture2D. In Unity,
		/// a "readable" Texture2D is a texture whose uncompressed
		/// color data is available in RAM, in addition to existing on
		/// the GPU. A Texture2D must be readable before
		/// certain methods can be called (e.g. `GetPixels`, `SetPixels`,
		/// `encodeToPNG`). The code for this method was copied from
		/// the following web page, with minor modifications:
		/// https://support.unity.com/hc/en-us/articles/206486626-How-can-I-get-pixels-from-unreadable-textures-
		/// </summary>
		public static Texture2D GetReadableTexture(Texture2D texture)
		{
			if (texture.isReadable)
				return texture;

			// Create a temporary RenderTexture of the same size as the texture.
			//
			// Note: `RenderTextureReadWrite.Linear` means that RGB
			// color values will copied from source textures/materials without
			// modification, i.e. without color space conversions. For further
			// details, see:
			// https://docs.unity3d.com/ScriptReference/RenderTextureReadWrite.html
			var tmp = RenderTexture.GetTemporary(
				texture.width,
				texture.height,
				0,
				RenderTextureFormat.Default,
				RenderTextureReadWrite.Default);

			// Blit the pixels on texture to the RenderTexture
			Graphics.Blit(texture, tmp);

			// Backup the currently set RenderTexture
			var previous = RenderTexture.active;

			// Set the current RenderTexture to the temporary one we created
			RenderTexture.active = tmp;

			// Create a new readable Texture2D to copy the pixels to it
			var readableTexture = new Texture2D(texture.width, texture.height);
			readableTexture.name = texture.name;

			// Copy the pixels from the RenderTexture to the new Texture
			readableTexture.ReadPixels(new Rect(0, 0, tmp.width, tmp.height), 0, 0);
			readableTexture.Apply();

			// Reset the active RenderTexture
			RenderTexture.active = previous;

			// Release the temporary RenderTexture
			RenderTexture.ReleaseTemporary(tmp);

			// "readableTexture" now has the same pixels from "texture"
			// and it's readable.
			return readableTexture;
		}

		/// <summary>
		/// Coroutine to load a Texture2D from a URI.
		/// </summary>
		/// <returns>
		/// A two-item tuple consisting of: (1) a Texture2D,
		/// and (2) a bool that is true if the texture
		/// was loaded upside-down. The bool is needed because
		/// `UnityWebRequestTexture` loads PNG/JPG images into textures
		/// upside-down, whereas KtxUnity loads KTX2/BasisU images
		/// right-side-up.
		/// </returns>
		static public IEnumerable<(Texture2D, bool)> ReadTextureEnum(string uri)
		{
			foreach (var result in ReadTextureEnum(new Uri(uri)))
				yield return result;
		}

        /// <summary>
        /// Coroutine to load a Texture2D from a URI.
        /// </summary>
        /// <returns>
        /// A two-item tuple consisting of: (1) a Texture2D,
        /// and (2) a bool that is true if the texture
        /// was loaded upside-down. The bool is needed because
        /// `UnityWebRequestTexture` loads PNG/JPG images into textures
        /// upside-down, whereas KtxUnity loads KTX2/BasisU images
        /// right-side-up.
        /// </returns>
        static public IEnumerable<(Texture2D, bool)> ReadTextureEnum(Uri uri)
		{
			// Note!: Using `UnityWebRequestTexture`
			// is preferable to `Texture2D.LoadImage`
			// because it does not block the main Unity thread.

#if USE_MODIFIED_PIGLET
			var request = UnityWebRequest.Get(uri);
#else
			var request = UnityWebRequestTexture.GetTexture( uri, true );
#endif

			request.SendWebRequest();

			while (!request.isDone)
				yield return (null, false);

			if (request.HasError())
				throw new Exception(string.Format(
					"failed to load image URI {0}: {1}",
					uri, request.error));

			// Note: The `data != null` check below is needed because
			// (as of Unity 2021.1.1f1) UnityWebRequest no longer
			// initializes `request.downloadHandler.data` if the
			// texture was successfully loaded as a PNG/JPG.

			var data = request.downloadHandler.data;
			if (data != null && IsKtx2Data(data))
			{
#if KTX_UNITY_0_9_1_OR_NEWER
				foreach (var result in LoadKtx2Data(data))
					yield return (result, false);
#elif KTX_UNITY
				Debug.LogWarning("Failed to load texture in KTX2 format, "+
					 "because KtxUnity package is older than 0.9.1.");
				yield return (null, false);
#else
				Debug.LogWarning("Failed to load texture in KTX2 format "+
					"because KtxUnity package is not installed. Please install KtxUnity "+
					"by following the directions at https://github.com/atteneder/KtxUnity "+
					"(requires Unity 2019.3+).");
				yield return (null, false);
#endif
				yield break;
			}

#if USE_MODIFIED_PIGLET
				//Image is not a KTX2/Basis texture, load it as a .png/.jpg instead
                request = UnityWebRequestTexture.GetTexture( uri, true );
                request.SendWebRequest();
 
                while( !request.isDone )
                    yield return (null, false);
 
                if( request.HasError() )
                    throw new Exception( string.Format(
                        "failed to load image URI {0}: {1}",
                        uri, request.error ) );
#endif

			Texture2D texture = DownloadHandlerTexture.GetContent(request);

			yield return (texture, true);
		}
	}
}