using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using GLTF;
using UnityEngine;
using UnityEngine.Networking;

namespace Piglet
{
	/// <summary>
	/// Utility methods for downloading and reading URIs.
	/// </summary>
	public class UriUtil
	{
		static readonly Regex DATA_URI_REGEX = new Regex("^data:[a-z-]+/[a-z-]+;base64,");

		/// <summary>
		/// Return true if a URI refers to a local file, or false otherwise.
		/// </summary>
		static public bool IsLocalUri(Uri uri)
		{
			// Note: uri.IsFile/uri.IsLoopback/uri.Scheme will throw
			// an exception if 'uri' is not an absolute URI
			return !uri.IsAbsoluteUri
				|| uri.IsFile
				|| uri.IsLoopback
				|| uri.Scheme == "jar"
				|| uri.Scheme == "content";
		}

		/// <summary>
		/// Return a URI through which the given data (byte[])
		/// can be read.  This method was written to facilitate
		/// the use of UnityWebRequestTexture with in-memory
		/// PNG/JPG data.
		/// </summary>
		static public IEnumerable<string> CreateUri(byte[] data)
		{
			// Note: While it is possible to read/write files in
			// Application.temporaryCachePath in a WebGL build
			// using standard C# I/O methods (e.g. File.WriteAllBytes),
			// there is no straightforward way to read such files
			// via a URI. In particular, UnityWebRequest/UnityWebRequestTexture
			// will fail with a browser permissions error when given *any*
			// local file path, including paths under
			// Application.temporaryCachePath or Application.persisentDataPath.
			// See this thread for a discussion of the issue:
			// https://forum.unity.com/threads/indexeddb-files-and-www.461171/
			//
			// To work around this issue, I create a temporary URL for the
			// file on the Javascript side, using `createObjectUrl`.
			// I got this idea from the following post on the Unity Forum:
			// https://forum.unity.com/threads/how-do-i-let-the-user-load-an-image-from-their-harddrive-into-a-webgl-app.380985/#post-2474594

#if UNITY_WEBGL && !UNITY_EDITOR
			yield return JsLib.CreateObjectUrl(data, data.Length);
#else
			string path = Path.Combine(Application.temporaryCachePath,
				StringUtil.GetRandomString(8));

			foreach (var unused in FileUtil.WriteAllBytes(path, data))
				yield return null;

			yield return path;
#endif
		}

		/// <summary>
		/// Try to parse the given URI as a base64-encoded data URI.
		/// If successful, return true and pass the data back to the
		/// user via the output parameter `data`.
		/// </summary>
		/// <returns>
		/// True if the given URI is a valid base64-data URI and false
		/// otherwise.
		/// </returns>
		static public bool TryParseDataUri(string uri, out byte[] data)
		{
			Match match = DATA_URI_REGEX.Match(uri);
			if (match.Success)
			{
				var base64Data = uri.Substring(match.Length);
				data = Convert.FromBase64String(base64Data);
				return true;
			}
			data = null;
			return false;
		}

		/// <summary>
		/// Coroutine to download/read all bytes from a URI into an array.
		/// The URI may refer to a local file or an URL on the web.
		/// </summary>
		static public IEnumerable<byte[]> ReadAllBytesEnum(
			string uriStr, Action<ulong, ulong> onProgress = null)
		{
			Uri uri = new Uri(uriStr);
			foreach (var result in ReadAllBytesEnum(uri, onProgress))
				yield return result;
		}

		/// <summary>
		/// Coroutine to download/read all bytes from a URI into an array.
		/// The URI may refer to a local file or an URL on the web.
		/// </summary>
		static public IEnumerable<byte[]> ReadAllBytesEnum(
			Uri uri, Action<ulong, ulong> onProgress=null)
		{
			// Handle reading data from Android content URIs.
			// For background, see: https://developer.android.com/reference/android/support/v4/content/FileProvider
			if (uri.Scheme == "content")
			{
				byte[] data = null;
				foreach (var result in ContentUriUtil.ReadAllBytesEnum(uri, onProgress))
				{
					data = result;
					yield return null;
				}

				yield return data;
				yield break;
			}

			ulong size = 0;

			// Attempt to determine the file size from the filesystem
			// or by HTTP HEAD request. If this fails for any reason,
			// just report a size of 0 and carry on.
			//
			// The following loop structure works around the
			// limitation that C# does not allow `yield` statements
			// in try/catch blocks. For further discussion, see:
			// https://stackoverflow.com/questions/5067188/yield-return-with-try-catch-how-can-i-solve-it

			using (var sizeRequest = GetSizeInBytesEnum(uri).GetEnumerator())
			{
				while (true)
				{
					try
					{
						if (!sizeRequest.MoveNext())
							break;

						size = sizeRequest.Current;
					}
					catch (Exception e)
					{
						Debug.LogWarningFormat(e.ToString());
						size = 0;
					}

					yield return null;
				}
			}

			var request = new UnityWebRequest(uri);
			request.downloadHandler = new DownloadHandlerBuffer();
			request.SendWebRequest();

			while (!request.isDone)
			{
				onProgress?.Invoke(request.downloadedBytes, size);
				yield return null;
			}

			if (request.HasError())
			{
				throw new Exception(string.Format(
					"failed to read from {0}: {1}",
					uri, request.error));
			}

			onProgress?.Invoke(request.downloadedBytes, size);

			yield return request.downloadHandler.data;
		}

		/// <summary>
		/// Coroutine to get size of a file in bytes from a URI.
		/// The URI may refer to a local file or an URL on the web.
		/// </summary>
		static public IEnumerable<ulong> GetSizeInBytesEnum(Uri uri)
		{
			// Return zero size for URIs that aren't file paths
			// and don't use the http/https URI scheme.
			//
			// Other types of URI include "jar" and
			// "content" URIs, both of which are commonly used
			// to reference local files on Android.

			if (!uri.IsFile && uri.Scheme != "http" && uri.Scheme != "https")
			{
				yield return 0;
				yield break;
			}

			if (uri.IsFile)
			{
				// Note: We need to use uri.LocalPath here instead
				// of uri.AbsolutePath in order to correctly
				// handle file names with spaces, because uri.AbsolutePath
				// returns an URL-encoded string.
				yield return (ulong)
					new FileInfo(uri.LocalPath).Length;
				yield break;
			}

			var request = UnityWebRequest.Head(uri);
			request.SendWebRequest();

			while (!request.isDone)
				yield return 0;

			if (request.HasError())
			{
				throw new Exception(string.Format(
					"HEAD request failed for {0}: {1}",
					uri, request.error));
			}

			string size = request.GetResponseHeader("Content-Length");

			// returns 0 if size == null
			yield return Convert.ToUInt64(size);
		}

	}
}
