// Source Code Attribution/License:
//
// The GltfImporter class in this file is a (heavily) modified version of
// the `UnityGLTF.GLTFEditorImporter` class from Sketchfab's UnityGLTF project,
// published at https://github.com/sketchfab/UnityGLTF with an MIT license.
// The exact version of `UnityGLTF.GLTFEditorImporter` used as the basis
// for this file comes from git commit c54fd454859c9ef8e1244c8d08c3f90089768702
// of https://github.com/sketchfab/UnityGLTF ("Merge pull request #12 from
// sketchfab/feature/updates-repo-url_D3D-4855").
//
// Please refer to the Assets/Piglet/Dependencies/UnityGLTF directory of this
// project for the Sketchfab/UnityGLTF LICENSE file and all other source
// files originating from the Sketchfab/UnityGLTF project.

using GLTF;
using GLTF.Schema;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using ICSharpCode.SharpZipLib.Zip;
using UnityEngine;
using UnityEngine.Rendering;
using UnityGLTF.Extensions;
using Debug = UnityEngine.Debug;

namespace Piglet
{
	public class GltfImporter
	{
		/// <summary>
		/// URI (local file or remote URL) of the input .gltf/.glb/.zip file.
		/// </summary>
		protected Uri _uri;
		/// <summary>
		/// Raw byte content of the input .gltf/.glb/.zip file.
		/// </summary>
		protected byte[] _data;
		/// <summary>
		/// Options controlling glTF importer behaviour (e.g. should
		/// the imported model be automatically scaled to a certain size?).
		/// </summary>
		protected GltfImportOptions _importOptions;

		/// <summary>
		/// C# object hierarchy that mirrors JSON of input .gltf/.glb file.
		/// </summary>
		protected GLTFRoot _root;
		/// <summary>
		/// Caches data (e.g. buffers) and Unity assets (e.g. Texture2D)
		/// that are created during a glTF import.
		/// </summary>
		protected GltfImportCache _imported;

		/// <summary>
		/// Prototype for callback(s) that are invoked to report
		/// intermediate progress during a glTF import.
		/// </summary>
		/// <param name="step">
		/// The current step of the glTF import process.  Each step imports
		/// a different type of glTF entity (e.g. textures, materials).
		/// </param>
		/// <param name="completed">
		/// The number of glTF entities (e.g. textures, materials) that have been
		/// successfully imported for the current import step.
		/// </param>
		/// <param name="total">
		/// The total number of glTF entities (e.g. textures, materials) that will
		/// be imported for the current import step.
		/// </param>
		public delegate void ProgressCallback(GltfImportStep step, int completed, int total);

		/// <summary>
		/// Callback(s) that invoked to report intermediate progress
		/// during a glTF import.
		/// </summary>
		protected ProgressCallback _progressCallback;

		/// <summary>
		/// Constructor
		/// </summary>
		public GltfImporter(Uri uri, byte[] data,
			GltfImportOptions importOptions,
			GltfImportCache imported,
			ProgressCallback progressCallback)
		{
			_uri = uri;
			_data = data;
			_importOptions = importOptions;
			_imported = imported;
			_progressCallback = progressCallback;
		}

		/// <summary>
		/// Clear all game objects created by the glTF import from
		/// the Unity scene and from memory.
		/// </summary>
		protected virtual void Clear()
		{
			_imported?.Clear();
		}

		/// <summary>
		/// Read/download the byte content from the input glTF URI (`_uri`)
		/// into `_data`. The input URI may be a local or remote file
		/// (e.g. HTTP URL).
		/// </summary>
		protected IEnumerator ReadUri()
		{
			// Skip download step if input .gltf/.glb/.zip was passed
			// in as raw byte array (i.e. _data != null)

			if (_data != null)
				yield break;

			GltfImportStep importStep = UriUtil.IsLocalUri(_uri)
				? GltfImportStep.Read : GltfImportStep.Download;

			void onProgress(ulong bytesRead, ulong size)
			{
				_progressCallback?.Invoke(importStep, (int)bytesRead, (int)size);
			}

			foreach (var data in UriUtil.ReadAllBytesEnum(_uri, onProgress))
			{
				_data = data;
				yield return null;
			}
		}

		/// <summary>
		/// Return the byte content of the .gltf/.glb file that is
		/// currently being imported.
		/// </summary>
		protected IEnumerable<byte[]> GetGltfBytes()
		{
			if (!ZipUtil.IsZipData(_data))
			{
				yield return _data;
				yield break;
			}

			Regex regex = new Regex("\\.(gltf|glb)$");
			byte[] data = null;

			foreach (var result in ZipUtil.GetEntryBytes(_data, regex))
			{
				data = result;
				yield return null;
			}

			if (data == null)
				throw new Exception("No .gltf/.glb file found in zip archive.");

			yield return data;
		}

		/// <summary>
		/// Parse the JSON content of the input .gltf/.glb file and
		/// create an equivalent hierarchy of C# objects (`_root`).
		/// </summary>
		/// <returns></returns>
		protected IEnumerator ParseFile()
		{
			_progressCallback?.Invoke(GltfImportStep.Parse, 0, 1);

			byte[] gltf = null;
			foreach (var result in GetGltfBytes())
			{
				gltf = result;
				yield return null;
			}

			FixSpecularGlossinessDefaults();

			// Wrap Json.NET exceptions with our own
			// JsonParseException class, so that applications
			// that use Piglet do not need to compile
			// against the Json.NET DLL.

			try
			{
				_root = GLTFParser.ParseJson(gltf);
			}
			catch (Exception e)
			{
				throw new JsonParseException(
					"Error parsing JSON in glTF file", e);
			}

			_progressCallback?.Invoke(GltfImportStep.Parse, 1, 1);
		}

		/// <summary>
		/// Use C# reflection to fix incorrect default values for
		/// specular/glossiness values (GLTFSerialization bug).
		///
		/// For the correct default values and other
		/// details about the specular/glossiness glTF extension,
		/// see https://github.com/KhronosGroup/glTF/tree/master/extensions/2.0/Khronos/KHR_materials_pbrSpecularGlossiness
		/// </summary>
		protected void FixSpecularGlossinessDefaults()
		{
			var assembly = Assembly.GetAssembly(typeof(KHR_materials_pbrSpecularGlossinessExtension));
			var specularGlossiness = assembly.GetType("GLTF.Schema.KHR_materials_pbrSpecularGlossinessExtension");

			var specularFactor = specularGlossiness.GetField("SPEC_FACTOR_DEFAULT",
				BindingFlags.Static | BindingFlags.Public);
			specularFactor.SetValue(null, GLTF.Math.Vector3.One);

			var glossinessFactor = specularGlossiness.GetField("GLOSS_FACTOR_DEFAULT",
				BindingFlags.Static | BindingFlags.Public);
			glossinessFactor.SetValue(null, 1.0f);
		}

		/// <summary>
		/// Load the glTF buffers into memory. In glTF, "buffers" are the raw binary
		/// blobs that store the model data (e.g. PNG/JPG image data for
		/// textures, triangle indices, vertex positions). "Accessors" are views
		/// into the buffers that interpret the binary data as specific datatypes,
		/// e.g. Vector3's with floating point components.
		/// </summary>
		protected IEnumerator LoadBuffers()
		{
			if (_root.Buffers == null || _root.Buffers.Count == 0)
				yield break;

			_progressCallback?.Invoke(GltfImportStep.Buffer, 0, _root.Buffers.Count);
			for (int i = 0; i < _root.Buffers.Count; ++i)
			{
				GLTF.Schema.Buffer buffer = _root.Buffers[i];

				byte[] data = null;
				foreach (var result in LoadBuffer(buffer, i))
				{
					data = result;
					yield return null;
				}

				_imported.Buffers.Add(data);
				_progressCallback?.Invoke(GltfImportStep.Buffer, (i + 1), _root.Buffers.Count);

				yield return null;
			}
		}

		/// <summary>
		/// Resolve a relative URI (e.g. path to a PNG file)
		/// by appending it to the URI of the directory containing
		/// the .gltf/.glb file. If the given URI is already an
		/// absolute URI (e.g. an HTTP URL), return the URI unchanged.
		/// If input for the glTF import is a zip archive, append the
		/// URI to the directory path containing the .gltf/.glb file
		/// inside the zip archive.
		/// </summary>
		protected IEnumerable<string> ResolveUri(string uriStr)
		{
			// If the given URI is absolute, we don't need to resolve it.

			Uri uri = new Uri(uriStr, UriKind.RelativeOrAbsolute);
			if (uri.IsAbsoluteUri)
			{
				yield return uriStr;
				yield break;
			}

			// If we are importing from a .zip file, append the given URI
			// to directory path containing the .gltf/.glb file
			// inside the zip.

			if (ZipUtil.IsZipData(_data))
			{
				Regex regex = new Regex("\\.(gltf|glb)$");
				ZipEntry entry = null;
				foreach (var value in ZipUtil.GetEntry(_data, regex))
				{
					entry = value;
					yield return null;
				}

				if (entry == null)
					throw new Exception("error: no .gltf/.glb file found in .zip");

				// Note: The C# Uri class cannot combine two relative
				// URIs, so we must do the work ourselves.

				string resolvedUriStr = entry.Name;

				// If the base URI for the input .gltf/.glb file does not
				// contain a slash, it means that file is located at the root of
				// the .zip archive, and therefore input URI (`uriStr`)
				// does not need to be modified.

				int lastSlashIndex = resolvedUriStr.LastIndexOf('/');
				if (lastSlashIndex < 0)
				{
					yield return uriStr;
					yield break;
				}

				resolvedUriStr = resolvedUriStr.Remove(lastSlashIndex);
				resolvedUriStr += "/";
				resolvedUriStr += uriStr;

				yield return resolvedUriStr;
				yield break;
			}

			if (Application.platform == RuntimePlatform.WebGLPlayer
				&& (_uri == null || !_uri.IsAbsoluteUri))
			{
				throw new UriResolutionException(
					string.Format("Sorry, the Piglet WebGL demo can't load {0} " +
					"because it contains references to other files on " +
					"the local filesystem (e.g. PNG files for textures). " +
					"In general, web browsers are not allowed to read files " +
					"from arbitrary paths on the local filesystem (for " +
					"security reasons).\n" +
					"\n" +
					"Please try a .glb or .zip file instead, as these are " +
					"generally self-contained.",
					_uri != null ? string.Format("\"{0}\"", _uri) : "your glTF file"));
			}

			if (Application.platform == RuntimePlatform.Android
			    && _uri.Scheme == "content")
			{
				throw new UriResolutionException(
					String.Format("Sorry, Piglet can't load \"{0}\" on Android " +
					  "because it contains references to other files (e.g. PNG " +
					  "files for textures) that it isn't allowed to read, for " +
					  "security reasons.\n" +
					  "\n" +
					  "Please try a .glb file instead, as these are " +
					  "generally self-contained.",
						_uri.Segments[_uri.Segments.Length - 1]));
			}

			// Combine the given URI with
			// the URI for the input .gltf/.glb file.
			//
			// Given the other cases handled above, at
			// this point in the code we are certain that:
			//
			// 1. the input file is a .gltf/.glb (not a .zip)
			// 2. the URI for the input .gltf/.glb (`_uri`) is an absolute URI
			// 3. the URI passed to this method (`uriStr`) is a relative URI
			//
			// Note 1: The Uri constructor below
			// will strip the filename segment (if present)
			// from the first Uri before combining
			// it with the second Uri.
			//
			// Note 2: The constructor will throw
			// an exception unless the first Uri is
			// absolute and the second Uri is relative,
			// which is why I can't use the same approach
			// for .zip file paths above.

			var resolvedUri = new Uri(_uri, uriStr);
			yield return resolvedUri.ToString();
		}

		/// <summary>
		/// Extract a glTF buffer that is embedded in the input .glb file.
		/// </summary>
		protected IEnumerable<byte[]> ExtractBinaryChunk(int bufferIndex)
		{
			byte[] gltf = null;
			foreach (var result in GetGltfBytes())
			{
				gltf = result;
				yield return null;
			}

			GLTFParser.ExtractBinaryChunk(gltf, bufferIndex, out var chunk);
			yield return chunk;
		}

		/// <summary>
		/// Get the byte content of a glTF buffer.
		/// </summary>
		protected IEnumerable<byte[]> LoadBuffer(GLTF.Schema.Buffer buffer, int bufferIndex)
		{
			byte[] data = null;

			// case 1: no URI -> load buffer from .glb segment

			if (buffer.Uri == null)
			{
				foreach (var result in ExtractBinaryChunk(bufferIndex))
				{
					data = result;
					yield return null;
				}

				yield return data;
				yield break;
			}

			// case 2: data URI -> decode data from base64

			if (UriUtil.TryParseDataUri(buffer.Uri, out data))
			{
				yield return data;
				yield break;
			}

			// resolve buffer URI relative to URI
			// for input .gltf/.glb file

			string uri = null;
			foreach (var result in ResolveUri(buffer.Uri))
			{
				uri = result;
				yield return null;
			}

			// case 3: extract buffer file from .zip

			if (ZipUtil.IsZipData(_data))
			{
				foreach (var result in ZipUtil.GetEntryBytes(_data, uri))
				{
					data = result;
					yield return null;
				}

				yield return data;
				yield break;
			}

			// case 4: read/download buffer from URI

			foreach (var result in UriUtil.ReadAllBytesEnum(uri))
				yield return result;
		}

		/// <summary>
		/// Create a Unity Texture2D from in-memory image data (PNG/JPG/KTX2).
		/// </summary>
		/// <returns>
		/// A two-item tuple consisting of: (1) a Texture2D,
		/// and (2) a bool that is true if the texture
		/// was loaded upside-down. The bool is needed because
		/// `UnityWebRequestTexture` loads PNG/JPG images into textures
		/// upside-down, whereas KtxUnity loads KTX2/BasisU images
		/// right-side-up.
		/// </returns>
		protected IEnumerable<(Texture2D, bool)> LoadTexture(byte[] data)
		{
			// Case 1: Load KTX2/BasisU texture using KtxUnity.
			//
			// Unlike PNG/JPG images, KTX2/BasisU images are optimized
			// for use with GPUs.

			if (TextureUtil.IsKtx2Data(data))
			{
#if KTX_UNITY_0_9_1_OR_NEWER
				foreach (var result in TextureUtil.LoadKtx2Data(data))
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

			// Case 2: Load PNG/JPG during an Editor glTF import.
			//
			// `Texture2D.LoadImage` is fast but is not suitable for runtime
			// glTF imports because it is synchronous. (Decompressing
			// large images can stall the main Unity thread for hundreds of
			// milliseconds.)

			if (!Application.isPlaying)
			{
				var texture = new Texture2D(1, 1, TextureFormat.RGBA32, true, false);
				texture.LoadImage(data, true);
				yield return (texture, true);
				yield break;
			}

			// Case 3: Load PNG/JPG during a runtime glTF import.
			//
			// `UnityWebRequestTexture` is a better option than `Texture2D.LoadImage`
			// during runtime glTF imports because it performs the PNG/JPG decompression
			// on a background thread. Unfortunately, `UnityWebRequestTexture`
			// still uploads the uncompressed PNG/JPG data to the GPU
			// in a synchronous manner, and so stalling of the main Unity
			// thread is not completely eliminated. For further details/discussion,
			// see: https://forum.unity.com/threads/asynchronous-texture-upload-and-downloadhandlertexture.562303/
			//
			// One obstacle to using `UnityWebRequestTexture`
			// here is that it requires a URI (i.e. an URL or file path)
			// to read the data from. On Windows and Android, `UriUtil.CreateUri`
			// writes the PNG/JPG data to a temporary file under
			// `Application.temporaryCachePath` and returns the file path.
			// On WebGL, `UriUtil.CreateUri` creates a temporary localhost
			// URL on the Javascript side using `URL.createObjectURL`.

			string uri = null;
			foreach (var result in UriUtil.CreateUri(data))
			{
				uri = result;
				yield return (null, false);
			}

			foreach (var result in TextureUtil.ReadTextureEnum(uri))
				yield return result;
		}

		/// <summary>
		/// Create a Unity Texture2D from a glTF image.
		/// </summary>
		/// <returns>
		/// A two-item tuple consisting of: (1) a Texture2D,
		/// and (2) a bool that is true if the texture
		/// was loaded upside-down. The bool is needed because
		/// `UnityWebRequestTexture` loads PNG/JPG images into textures
		/// upside-down, whereas KtxUnity loads KTX2/BasisU images
		/// right-side-up.
		/// </returns>
		protected IEnumerable<(Texture2D, bool)> LoadImage(Image image)
		{
			(Texture2D, bool) result = (null, false);
			byte[] data = null;

			// case 1: no URI -> load image data from glTF buffer view

			if (image.Uri == null)
			{
				var bufferView = image.BufferView.Value;
				data = new byte[bufferView.ByteLength];

				var bufferContents = _imported.Buffers[bufferView.Buffer.Id];
				System.Buffer.BlockCopy(bufferContents,
					bufferView.ByteOffset, data, 0, data.Length);

				foreach (var item in LoadTexture(data))
				{
					result = item;
					yield return (null, false);
				}

				yield return result;
				yield break;
			}

			// case 2: data URI -> decode data from base64 string

			if (UriUtil.TryParseDataUri(image.Uri, out data))
			{
				foreach (var item in LoadTexture(data))
				{
					result = item;
					yield return (null, false);
				}

				yield return result;
				yield break;
			}

			// resolve image URI relative to URI
			// for input .gltf/.glb file

			string uri = null;
			foreach (var item in ResolveUri(image.Uri))
			{
				uri = item;
				yield return (null, false);
			}

			// case 3: extract image bytes from input .zip

			if (ZipUtil.IsZipData(_data))
			{
				foreach (var item in ZipUtil.GetEntryBytes(_data, uri))
				{
					data = item;
					yield return (null, false);
				}

				foreach (var item in LoadTexture(data))
				{
					result = item;
					yield return (null, false);
				}

				yield return result;
				yield break;
			}

			// case 4: load texture from an absolute URI
			// (file path or URL)

			foreach (var item in TextureUtil.ReadTextureEnum(uri))
				yield return item;
		}

		/// <summary>
		/// Create Unity Texture2D objects from glTF texture descriptions.
		/// </summary>
		protected IEnumerator LoadTextures()
		{
			if (_root.Textures == null || _root.Textures.Count == 0)
				yield break;

			_progressCallback?.Invoke(GltfImportStep.Texture, 0, _root.Textures.Count);

			// Names to be assigned to Texture2D.name fields, once
			// the corresponding texture finishes loading. We generate these
			// names up front so that any duplicate names are resolved
			// in a deterministic fashion, regardless of the order
			// that the parallel texture-loading tasks complete.

			var textureNames = new List<string>();

			// Tracks previously used texture names, in order to ensure that
			// each texture name is unique.

			var textureNamesSet = new HashSet<string>();

			// Interleave texture loading tasks, since loading
			// large textures can be slow.

			var tasks = new InterleavedTaskSet<(Texture2D, bool)>();
			for (var i = 0; i < _root.Textures.Count; ++i)
			{
				// Generate a name for each texture that:
				//
				// (1) is unique (i.e. unused)
				// (2) resembles the name from the glTF file (if any)
				// (3) is safe to use as a Unity asset filename

				var textureName = string.IsNullOrEmpty(_root.Textures[i].Name)
					? string.Format("texture_{0}", i)
					: UnityPathUtil.GetLegalAssetName(_root.Textures[i].Name);
				textureName = StringUtil.GetUniqueName(textureName, textureNamesSet);
				textureNames.Add(textureName);
				textureNamesSet.Add(textureName);

				// placeholder until Texture2D is loaded
				_imported.Textures.Add(null);

				// Flag indicating if texture was loaded upside-down.
				// This is needed because `UnityWebRequestTexture`
				// loads PNG/JPG images into textures upside-down, whereas
				// KtxUnity loads KTX2/BasisU images right-side-up.
				_imported.TextureIsUpsideDown.Add(false);

				tasks.Add(LoadTexture(i).GetEnumerator());
			}

			tasks.OnCompleted =
				(textureIndex, result) =>
				{
					var (texture, isFlipped) = result;

					if (texture != null)
						texture.name = textureNames[textureIndex];

					_imported.TextureIsUpsideDown[textureIndex] = isFlipped;
					_imported.Textures[textureIndex] = texture;

					_progressCallback?.Invoke(GltfImportStep.Texture, tasks.NumCompleted, _root.Textures.Count);
				};

			// Pump tasks until all are complete.

			while (tasks.MoveNext())
				yield return null;
		}

		/// <summary>
		/// Get the KHR_texture_basisu glTF extension data for
		/// the given texture, which is used for loading KTX2/BasisU
		/// images. Return null if the texture does not use the
		/// KHR_texture_basisu extension.
		/// </summary>
		protected KHR_texture_basisuExtension GetKtx2Extension(
			GLTF.Schema.Texture texture)
		{
			Extension extension;
			if (texture.Extensions != null && texture.Extensions.TryGetValue(
				"KHR_texture_basisu", out extension))
			{
				return (KHR_texture_basisuExtension)extension;
			}
			return null;
		}

		/// <summary>
		/// Get the glTF image index corresponding to the given
		/// glTF texture index. In a vanilla glTF file, the `source`
		/// property of the texture provides the image index for
		/// the underlying PNG/JPG data. However, when the
		/// `KHR_texture_basisu` extension for KTX2 files is used,
		/// some additional fallback logic is implemented, which
		/// depends on whether or not the KtxUnity package is
		/// installed. For further details on the `KHR_texture_basisu`
		/// extension and its fallback behaviour, see:
		/// https://github.com/KhronosGroup/glTF/blob/master/extensions/2.0/Khronos/KHR_texture_basisu/README.md
		/// </summary>
		protected int GetImageIndex(int textureIndex)
		{
			var texture = _root.Textures[textureIndex];

			// get KHR_texture_basisu glTF extension data for this texture (if any)
			var ktx2 = GetKtx2Extension(texture);
			if (ktx2 == null)
				return texture.Source.Id;

#if KTX_UNITY_0_9_1_OR_NEWER
			return ktx2.Source;
#else

#if KTX_UNITY
			Debug.LogWarningFormat("Texture {0}: Failed to load texture in KTX2 format "+
				"because KtxUnity package is older than 0.9.1.",
				textureIndex);
#else
			Debug.LogWarningFormat("Texture {0}: Failed to load texture in KTX2 format "+
				"because KtxUnity package is not installed. Please install KtxUnity "+
				"by following the directions at https://github.com/atteneder/KtxUnity "+
				"(requires Unity 2019.3+).", textureIndex);
#endif

			if (texture.Source != null)
			{
				Debug.LogWarningFormat("Texture {0}: Falling back to PNG/JPG "+
					"version of texture.", textureIndex);
				return texture.Source.Id;
			}

			return -1;

#endif
		}

		/// <summary>
		/// Create a Unity Texture2D from a glTF texture definition.
		/// </summary>
		/// <param name="textureIndex">
		/// The index of the texture in the glTF file.
		/// </param>
		/// <returns>
		/// A two-item tuple consisting of: (1) a Texture2D,
		/// and (2) a bool that is true if the texture
		/// was loaded upside-down. The bool is needed because
		/// `UnityWebRequestTexture` loads PNG/JPG images into textures
		/// upside-down, whereas KtxUnity loads KTX2/BasisU images
		/// right-side-up.
		/// </returns>
		protected IEnumerable<(Texture2D, bool)> LoadTexture(int textureIndex)
		{
			var imageId = GetImageIndex(textureIndex);

			(Texture2D texture, bool isFlipped) result = (null, false);

			if (imageId >= 0 && imageId < _root.Images.Count)
			{
				var image = _root.Images[imageId];

				foreach (var item in LoadImage(image))
				{
					result = item;
					yield return (null, false);
				}
			}

			// Default values
			var desiredFilterMode = FilterMode.Bilinear;
			var desiredWrapMode = UnityEngine.TextureWrapMode.Repeat;

			var def = _root.Textures[textureIndex];

			if (def.Sampler != null)
			{
				var sampler = def.Sampler.Value;
				switch (sampler.MinFilter)
				{
					case MinFilterMode.Nearest:
						desiredFilterMode = FilterMode.Point;
						break;
					case MinFilterMode.Linear:
					default:
						desiredFilterMode = FilterMode.Bilinear;
						break;
				}

				switch (sampler.WrapS)
				{
					case GLTF.Schema.WrapMode.ClampToEdge:
						desiredWrapMode = UnityEngine.TextureWrapMode.Clamp;
						break;
					case GLTF.Schema.WrapMode.Repeat:
					default:
						desiredWrapMode = UnityEngine.TextureWrapMode.Repeat;
						break;
				}
			}

			if (result.texture != null)
			{
				result.texture.filterMode = desiredFilterMode;
				result.texture.wrapMode = desiredWrapMode;
			}

			yield return result;
		}

		public GameObject GetSceneObject()
		{
			return _imported.Scene;
		}

		public IEnumerator<GameObject> GetSceneObjectEnum()
		{
			yield return GetSceneObject();
		}

		protected IEnumerator LoadMaterials()
		{
			if (_root.Materials == null || _root.Materials.Count == 0)
				yield break;

			_progressCallback?.Invoke(GltfImportStep.Material, 0, _root.Materials.Count);

			// Tracks values assigned to Material.name fields, to ensure
			// that each material gets a unique name.
			var materialNames = new HashSet<string>();

			for(int i = 0; i < _root.Materials.Count; ++i)
			{
				UnityEngine.Material material = LoadMaterial(_root.Materials[i], i);

				// Generate a name for each material that:
				//
				// (1) is unique (i.e. unused)
				// (2) resembles the name from the glTF file (if any)
				// (3) is safe to use as a Unity asset filename

				material.name = string.IsNullOrEmpty(_root.Materials[i].Name)
					? string.Format("material_{0}", i)
					: UnityPathUtil.GetLegalAssetName(_root.Materials[i].Name);
				material.name = StringUtil.GetUniqueName(material.name, materialNames);
				materialNames.Add(material.name);

				_imported.Materials.Add(material);
				_progressCallback?.Invoke(GltfImportStep.Material, (i + 1), _root.Materials.Count);
				yield return null;
			}
		}

		protected KHR_materials_pbrSpecularGlossinessExtension
			GetSpecularGlossinessExtension(GLTF.Schema.Material def)
		{
			Extension extension;
			if (def.Extensions != null && def.Extensions.TryGetValue(
				"KHR_materials_pbrSpecularGlossiness", out extension))
			{
				return (KHR_materials_pbrSpecularGlossinessExtension)extension;
			}
			return null;
		}

		/// <summary>
		/// Set U/V texture scale coefficients for a texture.
		/// </summary>
		/// <param name="material">the target material</param>
		/// <param name="shaderProperty">
		/// shader property for the target texture
		/// </param>
		/// <param name="scale">
		/// Vector2 with U/V texture scale coefficients
		/// </param>
		protected void SetTextureScale(UnityEngine.Material material,
			string shaderProperty, Vector2 scale)
		{
			var pipeline = RenderPipelineUtil.GetRenderPipeline(true);
			switch (pipeline)
			{
				case RenderPipelineType.BuiltIn:
					material.SetTextureScale(shaderProperty, scale);
					break;

				case RenderPipelineType.URP:
					// Note 1: As of Unity 2019.3, Unity has not implemented
					// Material.SetTextureScale and	Material.SetTextureOffset
					// for URP/HDRP materials, so I had to create my own
					// Shader Graph properties for these parameters.
					//
					// Note 2: All textures of the URP/HDRP shaders are
					// controlled by the same texture scale/offset, so
					// there is no need to use `shaderProperty` here.
					material.SetVector("_textureScale", scale);
					break;

				case RenderPipelineType.Unsupported:
					throw new Exception("current render pipeline unsupported, " +
						" GetRenderPipeline should have thrown exception");
			}
		}

		/// <summary>
		/// Assign a texture to a material.
		/// </summary>
		/// <param name="material">
		/// The target Unity material.
		/// </param>
		/// <param name="shaderProperty">
		/// The name of the shader property to which the texture will be
		/// assigned.
		/// </param>
		/// <param name="texture">
		/// The texture to assign to the material.
		/// </param>
		/// <param name="flipTexture">
		/// If true, flip the texture vertically (i.e. flip it upside-down).
		/// This is used to correct for the fact that UnityWebRequestTexture
		/// loads PNG/JPG images upside-down.
		/// </param>
		protected void SetMaterialTexture(UnityEngine.Material material,
			string shaderProperty, Texture2D texture, bool flipTexture)
		{
			material.SetTexture(shaderProperty, texture);

			// Flip the texture vertically, using the material's
			// texture scale parameter.
			//
			// This is necessary to correct the orientation of textures
			// loaded with `UnityWebRequestTexture`, because that class
			// loads PNG/JPG images upside-down. If the texture was instead
			// loaded with KtxUnity (i.e. a KTX2/BasisU texture), then there
			// there is no need to flip the texture since it is already
			// in the correct orientation.
			//
			// The `flipTexture` option is ignored during Editor glTF imports.
			// During Editor glTF imports, Piglet instead flips the textures
			// with a RenderTexture, immediately before writing them out to disk
			// (see `EditorGltfImportCache.SerializeTexture` method).

			if (flipTexture && Application.isPlaying)
				SetTextureScale(material, shaderProperty, new Vector2(1, -1));
		}

		/// <summary>
		/// Load the shader for the given material, given that the
		/// Universal Render Pipeline (URP) is the currently
		/// active render pipeline.
		/// </summary>
		protected Shader LoadURPShader(GLTF.Schema.Material material)
		{
			string shaderName = null;

			var sg = GetSpecularGlossinessExtension(material);
			if (sg != null) {
				switch(material.AlphaMode)
				{
				case AlphaMode.OPAQUE:
					shaderName = "Shader Graphs/URPSpecularGlossinessOpaque";
					break;
				case AlphaMode.MASK:
					shaderName = "Shader Graphs/URPSpecularGlossinessMask";
					break;
				case AlphaMode.BLEND:
					shaderName = "Shader Graphs/URPSpecularGlossinessBlend";
					break;
				}
			} else {
				switch(material.AlphaMode)
				{
				case AlphaMode.OPAQUE:
					shaderName = "Shader Graphs/URPMetallicRoughnessOpaque";
					break;
				case AlphaMode.MASK:
					shaderName = "Shader Graphs/URPMetallicRoughnessMask";
					break;
				case AlphaMode.BLEND:
					shaderName = "Shader Graphs/URPMetallicRoughnessBlend";
					break;
				}
			}

			Shader shader = Shader.Find(shaderName);
			if (shader == null)
				throw new Exception(String.Format(
					"Piglet failed to load URP shader \"{0}\". Please ensure that " +
					"you have installed the URP shaders from the appropriate .unitypackage " +
					"in Assets/Piglet/Extras, and that the shaders are being included " +
					"your build.",
					shaderName));

			return shader;
		}

		/// <summary>
		/// Load the shader for the given material, given that the
		/// built-in render pipeline (a.k.a. the standard render pipeline)
		/// is the currently active render pipeline.
		/// </summary>
		protected Shader LoadStandardShader(GLTF.Schema.Material material)
		{
			Shader shader = null;

			var sg = GetSpecularGlossinessExtension(material);
			if (sg != null) {
				switch(material.AlphaMode)
				{
				case AlphaMode.OPAQUE:
					shader = Shader.Find("Piglet/SpecularGlossinessOpaque");
					break;
				case AlphaMode.MASK:
					shader = Shader.Find("Piglet/SpecularGlossinessMask");
					break;
				case AlphaMode.BLEND:
					shader = Shader.Find("Piglet/SpecularGlossinessBlend");
					break;
				}
			} else {
				switch(material.AlphaMode)
				{
				case AlphaMode.OPAQUE:
					shader = Shader.Find("Piglet/MetallicRoughnessOpaque");
					break;
				case AlphaMode.MASK:
					shader = Shader.Find("Piglet/MetallicRoughnessMask");
					break;
				case AlphaMode.BLEND:
					shader = Shader.Find("Piglet/MetallicRoughnessBlend");
					break;
				}
			}

			return shader;
		}

		/// <summary>
		/// Load the shader for the given material.
		/// </summary>
		protected Shader LoadShader(GLTF.Schema.Material material)
		{
			Shader shader;

			var pipeline = RenderPipelineUtil.GetRenderPipeline(true);
			switch (pipeline)
			{
				case RenderPipelineType.BuiltIn:
					shader = LoadStandardShader(material);
					break;
				case RenderPipelineType.URP:
					shader = LoadURPShader(material);
					break;
				default:
					throw new Exception("current render pipeline unsupported, " +
						" GetRenderPipeline should have thrown exception");
			}

			return shader;
		}

		/// <summary>
		/// Create a Unity Material from a glTF material definition.
		/// </summary>
		virtual protected UnityEngine.Material LoadMaterial(
			GLTF.Schema.Material def, int index)
		{
			var shader = LoadShader(def);
			var material = new UnityEngine.Material(shader);

			// disable automatic deletion of unused material
			material.hideFlags = HideFlags.DontUnloadUnusedAsset;

			if (def.AlphaMode == AlphaMode.MASK)
				material.SetFloat("_alphaCutoff", (float)def.AlphaCutoff);

			if (def.NormalTexture != null)
			{
				SetMaterialTexture(material, "_normalTexture",
					_imported.Textures[def.NormalTexture.Index.Id],
					_imported.TextureIsUpsideDown[def.NormalTexture.Index.Id]);
			}

			if (def.OcclusionTexture != null)
			{
				SetMaterialTexture(material, "_occlusionTexture",
					_imported.Textures[def.OcclusionTexture.Index.Id],
					_imported.TextureIsUpsideDown[def.OcclusionTexture.Index.Id]);
			}

			material.SetColor("_emissiveFactor",
				def.EmissiveFactor.ToUnityColor());

			if (def.EmissiveTexture != null)
			{
				SetMaterialTexture(material, "_emissiveTexture",
					_imported.Textures[def.EmissiveTexture.Index.Id],
					_imported.TextureIsUpsideDown[def.EmissiveTexture.Index.Id]);
			}

			var mr = def.PbrMetallicRoughness;
			if (mr != null)
			{
				material.SetColor("_baseColorFactor",
					mr.BaseColorFactor.ToUnityColor());

				if (mr.BaseColorTexture != null)
				{
					SetMaterialTexture(material, "_baseColorTexture",
						_imported.Textures[mr.BaseColorTexture.Index.Id],
						_imported.TextureIsUpsideDown[mr.BaseColorTexture.Index.Id]);
				}

				material.SetFloat("_metallicFactor",
					(float)mr.MetallicFactor);
				material.SetFloat("_roughnessFactor",
					(float)mr.RoughnessFactor);

				if (mr.MetallicRoughnessTexture != null)
				{
					SetMaterialTexture(material, "_metallicRoughnessTexture",
						_imported.Textures[mr.MetallicRoughnessTexture.Index.Id],
						_imported.TextureIsUpsideDown[mr.MetallicRoughnessTexture.Index.Id]);
				}
			}

			var sg = GetSpecularGlossinessExtension(def);
			if (sg != null)
			{
				material.SetColor("_diffuseFactor",
					sg.DiffuseFactor.ToUnityColor());

				if (sg.DiffuseTexture != null)
				{
					SetMaterialTexture(material, "_diffuseTexture",
						_imported.Textures[sg.DiffuseTexture.Index.Id],
						_imported.TextureIsUpsideDown[sg.DiffuseTexture.Index.Id]);
				}

				Vector3 spec3 = sg.SpecularFactor.ToUnityVector3();
				material.SetColor("_specularFactor",
					new Color(spec3.x, spec3.y, spec3.z, 1f));

				material.SetFloat("_glossinessFactor",
					(float)sg.GlossinessFactor);

				if (sg.SpecularGlossinessTexture != null)
				{
					SetMaterialTexture(material, "_specularGlossinessTexture",
						_imported.Textures[sg.SpecularGlossinessTexture.Index.Id],
						_imported.TextureIsUpsideDown[sg.SpecularGlossinessTexture.Index.Id]);
				}

			}

			material.hideFlags = HideFlags.None;

			return material;
		}

		/// <summary>
		/// Create Unity meshes from glTF meshes.
		/// </summary>
		/// <returns></returns>
		protected IEnumerator LoadMeshes()
		{
			if (_root.Meshes == null || _root.Meshes.Count == 0)
				yield break;

			_progressCallback?.Invoke(GltfImportStep.Mesh, 0, _root.Meshes.Count);

			// Tracks previously used mesh names, to ensure that each
			// mesh name is unique.

			var meshNames = new HashSet<string>();

			for(int i = 0; i < _root.Meshes.Count; ++i)
			{
				// Generate a name for each mesh that:
				//
				// (1) is unique (i.e. unused)
				// (2) resembles the name from the glTF file (if any)
				// (3) is safe to use as a Unity asset filename

				var meshName = string.IsNullOrEmpty(_root.Meshes[i].Name)
					? string.Format("mesh_{0}", i)
					: UnityPathUtil.GetLegalAssetName(_root.Meshes[i].Name);
				meshName = StringUtil.GetUniqueName(meshName, meshNames);
				meshNames.Add(meshName);

				var mesh = LoadMesh(_root.Meshes[i], i, meshName);

				_imported.Meshes.Add(mesh);
				_progressCallback?.Invoke(GltfImportStep.Mesh, (i + 1), _root.Meshes.Count);

				yield return null;
			}
		}

		/// <summary>
		/// Create a Unity mesh from a glTF mesh.
		/// </summary>
		protected List<KeyValuePair<UnityEngine.Mesh, UnityEngine.Material>> LoadMesh(
			GLTF.Schema.Mesh meshDef, int meshId, string meshName)
		{
			// Note: In glTF, a mesh is a list of mesh primitives, each of which
			// has its own geometry and material.

			var mesh = new List<KeyValuePair<UnityEngine.Mesh, UnityEngine.Material>>();

			// true if one or more mesh primitives have morph targets
			bool hasMorphTargets = false;

			for (int i = 0; i < meshDef.Primitives.Count; ++i)
			{
				hasMorphTargets |= HasMorphTargets(meshId, i);

				var primitive = meshDef.Primitives[i];

				var meshPrimitive = LoadMeshPrimitive(primitive, meshId, i, meshName);

				var material = primitive.Material != null && primitive.Material.Id >= 0
					? _imported.Materials[primitive.Material.Id] : _imported.GetDefaultMaterial(true);

				mesh.Add(new KeyValuePair<UnityEngine.Mesh, UnityEngine.Material>(
					meshPrimitive, material));
			}

			// track which meshes have morph target data, so that we
			// can load them in a later step
			if (hasMorphTargets)
				_imported.MeshesWithMorphTargets.Add(meshId);

			return mesh;
		}

		protected UnityEngine.Mesh LoadMeshPrimitive(MeshPrimitive primitive,
			int meshIndex, int primitiveIndex, string meshName)
		{
			var meshAttributes = LoadMeshAttributes(primitive);

			// Determine whether to use 16-bit unsigned integers or 32-bit unsigned
			// integers for the triangle vertices array.
			//
			// By default, Unity uses 16-bit unsigned integers in order to maximize
			// performance and to support the maximum number of platforms (e.g.
			// old Android phones). However, this means that there is a limit
			// of 65,535 vertices per mesh.
			//
			// In the case where meshes have more that 65,535 vertices, Unity provides
			// the option to use 32-bit triangle indices instead by setting Mesh.indexFormat
			// to IndexFormat.UInt32. Using 32-bit indices allows for up to 4 billion
			// vertices per mesh.
			//
			// For further info, see:
			//
			// https://docs.unity3d.com/ScriptReference/Mesh-indexFormat.html

			var vertexCount = primitive.Attributes[SemanticProperties.POSITION].Value.Count;
			var numIndices = primitive.Indices?.Value.Count ?? vertexCount;
			var indexFormat = numIndices <= UInt16.MaxValue ? IndexFormat.UInt16 : IndexFormat.UInt32;

			var mesh = new UnityEngine.Mesh
			{
				indexFormat =  indexFormat,

				vertices = primitive.Attributes.ContainsKey(SemanticProperties.POSITION)
					? meshAttributes[SemanticProperties.POSITION].AccessorContent.AsVertices.ToUnityVector3()
					: null,

				normals = primitive.Attributes.ContainsKey(SemanticProperties.NORMAL)
					? meshAttributes[SemanticProperties.NORMAL].AccessorContent.AsNormals.ToUnityVector3()
					: null,

				uv = primitive.Attributes.ContainsKey(SemanticProperties.TexCoord(0))
					? meshAttributes[SemanticProperties.TexCoord(0)].AccessorContent.AsTexcoords.ToUnityVector2()
					: null,

				uv2 = primitive.Attributes.ContainsKey(SemanticProperties.TexCoord(1))
					? meshAttributes[SemanticProperties.TexCoord(1)].AccessorContent.AsTexcoords.ToUnityVector2()
					: null,

				uv3 = primitive.Attributes.ContainsKey(SemanticProperties.TexCoord(2))
					? meshAttributes[SemanticProperties.TexCoord(2)].AccessorContent.AsTexcoords.ToUnityVector2()
					: null,

				uv4 = primitive.Attributes.ContainsKey(SemanticProperties.TexCoord(3))
					? meshAttributes[SemanticProperties.TexCoord(3)].AccessorContent.AsTexcoords.ToUnityVector2()
					: null,

				colors = primitive.Attributes.ContainsKey(SemanticProperties.Color(0))
					? meshAttributes[SemanticProperties.Color(0)].AccessorContent.AsColors.ToUnityColor()
					: null,

				triangles = primitive.Indices != null
					? meshAttributes[SemanticProperties.INDICES].AccessorContent.AsTriangles
					: MeshPrimitive.GenerateTriangles(vertexCount),

				tangents = primitive.Attributes.ContainsKey(SemanticProperties.TANGENT)
					? meshAttributes[SemanticProperties.TANGENT].AccessorContent.AsTangents.ToUnityVector4(true)
					: null
			};

			mesh.RecalculateBounds();
			mesh.RecalculateTangents();

			mesh.name = meshName;

			// If the mesh has multiple mesh primitives append the primitive index.
			// This ensures that mesh primitive names are unique and also aids debugging.

			if (_root.Meshes[meshIndex].Primitives.Count > 1)
				mesh.name = string.Format("{0}_{1}", mesh.name, primitiveIndex);

			return mesh;
		}

		protected Dictionary<string, AttributeAccessor>
		LoadMeshAttributes(MeshPrimitive primitive)
		{
			var attributeAccessors =
				new Dictionary<string, AttributeAccessor>(primitive.Attributes.Count + 1);

			foreach (var attributePair in primitive.Attributes)
			{
				AttributeAccessor AttributeAccessor = new AttributeAccessor()
				{
					AccessorId = attributePair.Value,
					Buffer = _imported.Buffers[attributePair.Value.Value.BufferView.Value.Buffer.Id]
				};

				attributeAccessors[attributePair.Key] = AttributeAccessor;
			}

			if (primitive.Indices != null)
			{
				AttributeAccessor indexBuilder = new AttributeAccessor()
				{
					AccessorId = primitive.Indices,
					Buffer = _imported.Buffers[primitive.Indices.Value.BufferView.Value.Buffer.Id]
				};

				attributeAccessors[SemanticProperties.INDICES] = indexBuilder;
			}

			GLTFHelpers.BuildMeshAttributes(ref attributeAccessors);
			return attributeAccessors;
		}

		/// <summary>
		/// Return true if the given mesh primitive has morph target
		/// data (a.k.a. blend shapes).
		/// </summary>
		protected bool HasMorphTargets(int meshIndex, int primitiveIndex)
		{
			MeshPrimitive primitive
				= _root.Meshes[meshIndex].Primitives[primitiveIndex];

			return primitive.Targets != null
			       && primitive.Targets.Count > 0;
		}

		/// <summary>
		/// Assign glTF morph target data to a Unity mesh.
		///
		/// Note: In Unity, morph targets are usually referred to as "blend shapes".
		/// Interpolation between blend shapes is calculated/rendered by
		/// SkinnedMeshRenderer.
		/// </summary>
		protected void LoadMorphTargets(UnityEngine.Mesh mesh, int meshIndex, int primitiveIndex)
		{
			MeshPrimitive primitive
				= _root.Meshes[meshIndex].Primitives[primitiveIndex];

			if (!HasMorphTargets(meshIndex, primitiveIndex))
				return;

			for (int i = 0; i < primitive.Targets.Count; ++i)
			{
				var target = primitive.Targets[i];
				int numVertices = target["POSITION"].Value.Count;

				Vector3[] deltaVertices = new Vector3[numVertices];
				Vector3[] deltaNormals = new Vector3[numVertices];
				Vector3[] deltaTangents = new Vector3[numVertices];

				if(target.ContainsKey("POSITION"))
				{
					NumericArray num = new NumericArray();
					deltaVertices = target["POSITION"].Value
						.AsVector3Array(ref num, _imported.Buffers[0], false)
						.ToUnityVector3(true);
				}
				if (target.ContainsKey("NORMAL"))
				{
					NumericArray num = new NumericArray();
					deltaNormals = target["NORMAL"].Value
						.AsVector3Array(ref num, _imported.Buffers[0], true)
						.ToUnityVector3(true);
				}

				mesh.AddBlendShapeFrame(GLTFUtils.buildBlendShapeName(meshIndex, i),
					1.0f, deltaVertices, deltaNormals, deltaTangents);
			}
		}

		protected virtual void LoadSkinnedMeshAttributes(int meshIndex, int primitiveIndex, ref Vector4[] boneIndexes, ref Vector4[] weights)
		{
			GLTF.Schema.MeshPrimitive prim = _root.Meshes[meshIndex].Primitives[primitiveIndex];
			if (!prim.Attributes.ContainsKey(SemanticProperties.JOINT) || !prim.Attributes.ContainsKey(SemanticProperties.WEIGHT))
				return;

			parseAttribute(ref prim, SemanticProperties.JOINT, ref boneIndexes);
			parseAttribute(ref prim, SemanticProperties.WEIGHT, ref weights);
			foreach(Vector4 wei in weights)
			{
				wei.Normalize();
			}
		}

		private void parseAttribute(ref GLTF.Schema.MeshPrimitive prim, string property, ref Vector4[] values)
		{
			byte[] bufferData = _imported.Buffers[prim.Attributes[property].Value.BufferView.Value.Buffer.Id];
			NumericArray num = new NumericArray();
			GLTF.Math.Vector4[] gltfValues = prim.Attributes[property].Value.AsVector4Array(ref num, bufferData);
			values = new Vector4[gltfValues.Length];

			for (int i = 0; i < gltfValues.Length; ++i)
			{
				values[i] = gltfValues[i].ToUnityVector4();
			}
		}

		/// <summary>
		/// Initialize a MeshRenderer component by assigning the appropriate
		/// material(s). Generally this is as simple as
		/// `renderer.material = material`, but in the case of URP
		/// we may need to assign two materials in order to address
		/// the problem of Order Independent Transparency (OIT).
		/// For a background discussion of OIT, see:
		/// https://forum.unity.com/threads/render-mode-transparent-doesnt-work-see-video.357853/#post-2315934
		/// </summary>
		protected void SetMeshRendererMaterials(MeshRenderer meshRenderer,
			int meshIndex, int primitiveIndex)
		{
			var material = _imported.Meshes[meshIndex][primitiveIndex].Value;

			var pipeline = RenderPipelineUtil.GetRenderPipeline(true);
			switch (pipeline)
			{
				case RenderPipelineType.BuiltIn:
					meshRenderer.material = material;
					break;
				case RenderPipelineType.URP:
					var primitive = _root.Meshes[meshIndex].Primitives[primitiveIndex];
					var alphaMode = primitive.Material.Value.AlphaMode;
					if (alphaMode == AlphaMode.BLEND)
					{
						// Insert a material whose shader only writes
						// to the Z-buffer (a.k.a. depth buffer).
						//
						// This is a compromise/workaround for the
						// classic problem of Order Independent Transparency (OIT).
						// For background on this problem, see:
						// https://forum.unity.com/threads/render-mode-transparent-doesnt-work-see-video.357853/#post-2315934
						//
						// The shaders for `RenderPipeline.BuiltIn`
						// pipeline also address the OIT problem with
						// a Z-buffer pre-pass. The main difference
						// with URP is that each shader/material can only perform
						// one pass, and so we must assign two materials to the
						// mesh: one for the Z-buffer pre-pass and one for actually
						// rendering the object.

						var zwrite = _imported.GetZWriteMaterial(true);
						meshRenderer.materials =
							new UnityEngine.Material[] { zwrite, material };
					}
					else
					{
						meshRenderer.material = material;
					}
					break;
				default:
					throw new Exception("current render pipeline unsupported, " +
						" GetRenderPipeline should have thrown exception");
			}
		}

		/// <summary>
		/// Set up mesh nodes in the scene hierarchy by
		/// attaching MeshFilter/MeshRenderer components
		/// and linking them to the appropriate
		/// meshes/materials.
		///
		/// If a glTF mesh has more than one primitive,
		/// we must create a seperate GameObject for each additional
		/// primitive with its own MeshFilter/MeshRenderer,
		/// which are added as siblings of the GameObject for
		/// mesh primitive 0. See documentation of
		/// GltfImportCache.NodeToMeshPrimitives for further discussion.
		/// </summary>
		protected void SetupMeshNodes()
		{
			foreach (var kvp in _imported.Nodes)
			{
				int nodeIndex = kvp.Key;
				GameObject gameObject = kvp.Value;
				Node node = _root.Nodes[nodeIndex];

				if (node.Mesh == null)
					continue;

				int meshIndex = node.Mesh.Id;

				List<KeyValuePair<UnityEngine.Mesh, UnityEngine.Material>>
					primitives = _imported.Meshes[meshIndex];

				List<GameObject> primitiveNodes = new List<GameObject>();
				for (int i = 0; i < primitives.Count; ++i)
				{
					GameObject primitiveNode;
					if (i == 0)
					{
						primitiveNode = gameObject;
					}
					else
					{
						primitiveNode = createGameObject(
							node.Name ?? "GLTFNode_" + nodeIndex);
						primitiveNode.transform.localPosition
							= gameObject.transform.localPosition;
						primitiveNode.transform.localRotation
							= gameObject.transform.localRotation;
						primitiveNode.transform.localScale
							= gameObject.transform.localScale;
						primitiveNode.transform.SetParent(
							gameObject.transform.parent, false);
					}

					MeshFilter meshFilter
						= primitiveNode.AddComponent<MeshFilter>();
					meshFilter.sharedMesh = primitives[i].Key;

					MeshRenderer meshRenderer
						= primitiveNode.AddComponent<MeshRenderer>();
					SetMeshRendererMaterials(meshRenderer, meshIndex, i);

					primitiveNodes.Add(primitiveNode);
				}

				_imported.NodeToMeshPrimitives.Add(
					nodeIndex, primitiveNodes);
			}
		}

		virtual protected GameObject createGameObject(string name)
		{
			return new GameObject(GLTFUtils.cleanName(name));
		}

		/// <summary>
		/// Create a hierarchy of Unity GameObjects that mirrors
		/// the hierarchy of nodes in the glTF file.
		/// </summary>
		protected IEnumerator LoadScene()
		{
			Scene scene = _root.GetDefaultScene();
			if (scene == null)
				throw new Exception("No default scene in glTF file");

			// Set the name of the root GameObject for the
			// model (i.e. the scene object). Note that
			// we use `_uri.LocalPath` here instead of
			// `_uri.AbsolutePath` because the latter
			// will URL-encode special characters (e.g.
			// " " -> "%20").

			string importName = "model";
			if (_uri != null)
				importName = Path.GetFileNameWithoutExtension(_uri.LocalPath);

			_imported.Scene = createGameObject(importName);

			// Hide the model until it has finished importing, so that
			// the user never sees the model in a partially reconstructed
			// state.

			_imported.Scene.SetActive(false);

			foreach (var node in scene.Nodes)
			{
				var nodeObj = CreateNode(node.Value, node.Id);
				nodeObj.transform.SetParent(_imported.Scene.transform, false);
			}

			SetupMeshNodes();

			yield return null;
		}

		protected GameObject CreateNode(Node node, int index)
		{
			var nodeObj = createGameObject(node.Name != null && node.Name.Length > 0 ? node.Name : "GLTFNode_" + index);

			Vector3 position;
			Quaternion rotation;
			Vector3 scale;
			node.GetUnityTRSProperties(out position, out rotation, out scale);
			nodeObj.transform.localPosition = position;
			nodeObj.transform.localRotation = rotation;
			nodeObj.transform.localScale = scale;

			// record mesh -> node mappings, for later use in loading morph target data
			if (node.Mesh != null)
			{
				if (!_imported.MeshToNodes.TryGetValue(node.Mesh.Id, out var nodes))
				{
					nodes = new List<int>();
					_imported.MeshToNodes.Add(node.Mesh.Id, nodes);
				}

				nodes.Add(index);
			}

			// record skin -> node mappings, for later use in loading skin data
			if (node.Skin != null)
			{
				if (!_imported.SkinToNodes.TryGetValue(node.Skin.Id, out var nodes))
				{
					nodes = new List<int>();
					_imported.SkinToNodes.Add(node.Skin.Id, nodes);
				}

				nodes.Add(index);
			}

			_imported.Nodes.Add(index, nodeObj);
			_progressCallback?.Invoke(GltfImportStep.Node, _imported.Nodes.Count, _root.Nodes.Count);

			if (node.Children != null)
			{
				foreach (var child in node.Children)
				{
					var childObj = CreateNode(child.Value, child.Id);
					childObj.transform.SetParent(nodeObj.transform, false);
				}
			}

			return nodeObj;
		}

		/// <summary>
		/// Automatically scale the imported glTF model to the target size
		/// specified in the glTF import options (if any).
		/// </summary>
		protected IEnumerator ScaleModel()
		{
			if (_importOptions.AutoScale)
			{
				foreach (var unused in
					HierarchyUtil.Resize(_imported.Scene, _importOptions.AutoScaleSize))
				{
					yield return null;
				}
			}
		}

		private bool isValidSkin(int skinIndex)
		{
			if (skinIndex >= _root.Skins.Count)
				return false;

			Skin glTFSkin = _root.Skins[skinIndex];

			return glTFSkin.Joints.Count > 0 && glTFSkin.Joints.Count == glTFSkin.InverseBindMatrices.Value.Count;
		}

		/// <summary>
		/// Load morph target data (a.k.a. blend shapes) for the given
		/// mesh primitive.
		/// </summary>
		protected void LoadMorphTargets(int meshIndex)
		{
			// load morph target data for each mesh primitive

			int numPrimitives = _imported.Meshes[meshIndex].Count;
			for (int i = 0; i < numPrimitives; ++i)
			{
				var primitive = _imported.Meshes[meshIndex][i];
				var mesh = primitive.Key;

				LoadMorphTargets(mesh, meshIndex, i);
			}

			// Add/configure SkinnedMeshRenderer on game objects
			// corresponding to mesh primitives.

			// if mesh isn't referenced by any nodes in the scene hierarchy
			if (!_imported.MeshToNodes.TryGetValue(meshIndex, out var nodeIndices))
				return;

			// The default weights for each morph target. These are the weights
			// that determine the "static pose" for the model.
			//
			// Note:
			//
			// Oddly, the glTF spec places the `weights` array in the top-level mesh
			// JSON object, rather than alongside the `targets` arrays in the child
			// JSON objects for the mesh primitives. See the following section of the
			// glTF tutorial for an example:
			//
			// https://github.com/javagl/glTF-Tutorials/blob/master/gltfTutorial/gltfTutorial_017_SimpleMorphTarget.md
			//
			// The `weights` array defines the default weights for *all* morph targets
			// defined by the child mesh primitives, in the same order that they are
			// defined by the mesh primitives.

			var weights = _root.Meshes[meshIndex].Weights;

			// for each scene node that has the mesh attached
			foreach (int nodeIndex in nodeIndices)
			{
				var weightIndex = 0;

				// for each game object corresponding to a mesh primitive
				var gameObjects = _imported.NodeToMeshPrimitives[nodeIndex];
				for (int i = 0; i < gameObjects.Count; ++i)
				{
					if (!HasMorphTargets(meshIndex, i))
						return;

					var gameObject = gameObjects[i];
					var primitive = _imported.Meshes[meshIndex][i];
					var mesh = primitive.Key;
					var material = primitive.Value;

					// By default, GameObjects for mesh primitives
					// get a MeshRenderer/MeshFilter attached to them
					// in SetupMeshNodes().
					//
					// However, for primitives with morph targets,
					// we need to replace these two components with
					// a SkinnedMeshRenderer.

					gameObject.RemoveComponent<MeshRenderer>();
					gameObject.RemoveComponent<MeshFilter>();

					SkinnedMeshRenderer renderer
						= gameObject.GetOrAddComponent<SkinnedMeshRenderer>();

					renderer.sharedMesh = mesh;
					renderer.sharedMaterial = material;

					// set default morph target weights for "static pose"
					for (var j = 0; j < mesh.blendShapeCount; ++j)
						renderer.SetBlendShapeWeight(j, (float) weights[weightIndex++]);
				}
			}

		}

		/// <summary>
		/// Load morph targets (a.k.a. blend shapes).
		/// </summary>
		protected IEnumerator LoadMorphTargets()
		{
			if (_imported.MeshesWithMorphTargets.Count == 0)
				yield break;

			_progressCallback?.Invoke(GltfImportStep.MorphTarget, 0,
				_imported.MeshesWithMorphTargets.Count);

			for (int i = 0; i < _imported.MeshesWithMorphTargets.Count; ++i)
			{
				int meshIndex = _imported.MeshesWithMorphTargets[i];
				LoadMorphTargets(meshIndex);

				_progressCallback?.Invoke(GltfImportStep.MorphTarget, i + 1,
					_imported.MeshesWithMorphTargets.Count);
				yield return null;
			}
		}

		/// <summary>
		/// Load skinning data for a single skin and apply it to
		/// the relevant meshes.
		/// </summary>
		/// <param name="skinIndex"></param>
		protected void LoadSkin(int skinIndex)
		{
			if (!isValidSkin(skinIndex))
			{
				Debug.LogErrorFormat(
					"Piglet: skipped loading skin {0}: skin data is empty/invalid",
					skinIndex);
				return;
			}

			// load skinning data

			Skin skin = _root.Skins[skinIndex];

			Matrix4x4[] bindposes = GetBindPoses(skin);
			Transform[] bones = GetBones(skin);

			Transform rootBone = null;
			if(skin.Skeleton != null)
				rootBone = _imported.Nodes[skin.Skeleton.Id].transform;

			// apply skinning data to each node/mesh that uses the skin

			foreach (var nodeIndex in _imported.SkinToNodes[skinIndex])
			{
				Node node = _root.Nodes[nodeIndex];
				if (node.Mesh == null)
					continue;

				// attach/configure a SkinnedMeshRenderer for each
				// mesh primitive
				for (int i = 0; i < _imported.Meshes[node.Mesh.Id].Count; ++i)
					SetupSkinnedMeshPrimitive(nodeIndex, i, bindposes,bones, rootBone);
			}
		}

		/// <summary>
		/// Load skinning data for meshes.
		/// </summary>
		protected IEnumerator LoadSkins()
		{
			if (_root.Skins == null || _root.Skins.Count == 0)
				yield break;

			_progressCallback?.Invoke(GltfImportStep.Skin, 0, _root.Skins.Count);

			for (int i = 0; i < _root.Skins.Count; ++i)
			{
				LoadSkin(i);

				_progressCallback?.Invoke(GltfImportStep.Skin, i + 1, _root.Skins.Count);
				yield return null;
			}
		}

		/// <summary>
		/// Add/configure a SkinnedMeshRenderer for a mesh primitive.
		/// </summary>
		/// <param name="nodeIndex">The glTF node index of the parent mesh instance</param>
		/// <param name="primitiveIndex">The mesh primitive index</param>
		/// <param name="bindposes">Matrices that hold inverse transforms for the bones</param>
		/// <param name="bones">Transforms of the bones</param>
		/// <param name="rootBone">Root bone for the skin (typically null)</param>
		protected void SetupSkinnedMeshPrimitive(int nodeIndex, int primitiveIndex,
			Matrix4x4[] bindposes, Transform[] bones, Transform rootBone)
		{
			int meshIndex = _root.Nodes[nodeIndex].Mesh.Id;
			var primitive = _imported.Meshes[meshIndex][primitiveIndex];
			UnityEngine.Mesh mesh = primitive.Key;
			UnityEngine.Material material = primitive.Value;

			// All GameObjects that represent a mesh primitive
			// get a MeshRenderer/MeshFilter attached to them
			// by default in SetupMeshNodes().
			//
			// For skinned meshes, we need to replace these
			// two components with a SkinnedMeshRenderer.
			// Since a SkinnedMeshRenderer is also used for
			// interpolating/rendering morph targets
			// (a.k.a. blend shapes), we may have already
			// replaced the MeshRenderer/MeshFilter
			// with a SkinnedMeshRenderer during the
			// morph target importing step.

			GameObject primitiveNode
				= _imported.NodeToMeshPrimitives[nodeIndex][primitiveIndex];

			primitiveNode.RemoveComponent<MeshRenderer>();
			primitiveNode.RemoveComponent<MeshFilter>();

			SkinnedMeshRenderer renderer
				= primitiveNode.GetOrAddComponent<SkinnedMeshRenderer>();

			renderer.sharedMesh = mesh;
			renderer.sharedMaterial = material;
			renderer.bones = bones;
			renderer.rootBone = rootBone;

			mesh.boneWeights = GetBoneWeights(meshIndex, primitiveIndex);
			mesh.bindposes = bindposes;
		}

		/// <summary>
		/// Get bindpose matrices for a skinned mesh, in Unity's native format.
		/// The bindpose matrices are inverse transforms of the bones
		/// in their default pose. In glTF, these matrices are provided
		/// by the 'inverseBindMatrices' property of a skin.
		///
		/// See https://docs.unity3d.com/ScriptReference/Mesh-bindposes.html
		/// for a minimal example of how to set up a skinned mesh in
		/// Unity including bone weights, bindposes, etc.
		/// </summary>
		protected Matrix4x4[] GetBindPoses(Skin skin)
		{
			byte[] bufferData = _imported.Buffers[
				skin.InverseBindMatrices.Value.BufferView.Value.Buffer.Id];

			NumericArray content = new NumericArray();
			GLTF.Math.Matrix4x4[] inverseBindMatrices
				= skin.InverseBindMatrices.Value.AsMatrixArray(ref content, bufferData);

			List<Matrix4x4> bindposes = new List<Matrix4x4>();
			foreach (GLTF.Math.Matrix4x4 mat in inverseBindMatrices)
				bindposes.Add(mat.ToUnityMatrix().switchHandedness());

			return bindposes.ToArray();
		}

		/// <summary>
		/// Get bone weights for a skinned mesh, in Unity's native format.
		///
		/// See https://docs.unity3d.com/ScriptReference/Mesh-bindposes.html
		/// for a minimal example of how to set up a skinned mesh in
		/// Unity including bone weights, bindposes, etc.
		/// </summary>
		protected BoneWeight[] GetBoneWeights(int meshIndex, int primitiveIndex)
		{
			MeshPrimitive primitive
				= _root.Meshes[meshIndex].Primitives[primitiveIndex];

			UnityEngine.Mesh mesh
				= _imported.Meshes[meshIndex][primitiveIndex].Key;

			if (!primitive.Attributes.ContainsKey(SemanticProperties.JOINT)
			    || !primitive.Attributes.ContainsKey(SemanticProperties.WEIGHT))
				return null;

			Vector4[] bones = new Vector4[1];
			Vector4[] weights = new Vector4[1];

			LoadSkinnedMeshAttributes(meshIndex, primitiveIndex, ref bones, ref weights);
			if(bones.Length != mesh.vertices.Length || weights.Length != mesh.vertices.Length)
			{
				Debug.LogErrorFormat("Not enough skinning data "
					 + "(bones: {0}, weights: {1}, verts: {2})",
				      bones.Length, weights.Length, mesh.vertices.Length);
				return null;
			}

			BoneWeight[] boneWeights = new BoneWeight[mesh.vertices.Length];
			int maxBonesIndex = 0;
			for (int i = 0; i < boneWeights.Length; ++i)
			{
				// Unity seems expects the the sum of weights to be 1.
				float[] normalizedWeights = GLTFUtils.normalizeBoneWeights(weights[i]);

				boneWeights[i].boneIndex0 = (int)bones[i].x;
				boneWeights[i].weight0 = normalizedWeights[0];

				boneWeights[i].boneIndex1 = (int)bones[i].y;
				boneWeights[i].weight1 = normalizedWeights[1];

				boneWeights[i].boneIndex2 = (int)bones[i].z;
				boneWeights[i].weight2 = normalizedWeights[2];

				boneWeights[i].boneIndex3 = (int)bones[i].w;
				boneWeights[i].weight3 = normalizedWeights[3];

				maxBonesIndex = (int)Mathf.Max(maxBonesIndex,
					bones[i].x, bones[i].y, bones[i].z, bones[i].w);
			}

			return boneWeights;
		}

		/// <summary>
		/// Get the bone transforms for a skin, in Unity's native format.
		///
		/// See https://docs.unity3d.com/ScriptReference/Mesh-bindposes.html
		/// for a minimal example of how to set up a skinned mesh in
		/// Unity including bone weights, bindposes, etc.
		/// </summary>
		protected Transform[] GetBones(Skin skin)
		{
			Transform[] bones = new Transform[skin.Joints.Count];
			for (int i = 0; i < skin.Joints.Count; ++i)
				bones[i] = _imported.Nodes[skin.Joints[i].Id].transform;
			return bones;
		}

		/// <summary>
		/// Load glTF animations into Unity AnimationClips.
		/// </summary>
		protected IEnumerable LoadAnimations()
		{
			if (_root.Animations == null
			    || _root.Animations.Count == 0
			    || !_importOptions.ImportAnimations)
				yield break;

			_progressCallback?.Invoke(GltfImportStep.Animation, 0,
				_root.Animations.Count);

			// If this is a runtime import, force animation
			// clip type to Legacy, since Mecanim clips can only be
			// created in Editor scripts.

			if (Application.isPlaying)
				_importOptions.AnimationClipType = AnimationClipType.Legacy;

			var legacy = _importOptions.AnimationClipType == AnimationClipType.Legacy;

			// Tracks values assigned to AnimationClip.name fields, to ensure
			// that each clip gets a unique name.
			var clipNames = new HashSet<string>();

			for (int i = 0; i < _root.Animations.Count; ++i)
			{
				AnimationClip clip = null;

				// The following loop structure works around the
				// limitation that C# does not allow `yield` statements
				// in try/catch blocks. For further discussion, see:
				// https://stackoverflow.com/questions/5067188/yield-return-with-try-catch-how-can-i-solve-it

				var enumerator = LoadAnimation(_root.Animations[i], i);
				while (true)
				{
					// If we fail to import an animation for any reason, log an error
					// but continue importing the model anyway.
					try
					{
						if (!enumerator.MoveNext())
							break;

						clip = enumerator.Current;
					}
					catch (Exception e)
					{
						Debug.LogFormat("failed to import animation {0}\n{1}", i, e);

						// null signals that we failed to import the clip
						clip = null;

						break;
					}

					yield return null;
				}

				// Assign a name to clip.name that:
				//
				// (1) is unique (i.e. unused)
				// (2) resembles the name from the glTF file (if any)
				// (3) is safe to use as part of a Unity asset filename
				// (4) is safe to use as an AnimatorController state name

				if (clip != null)
				{
					clip.name = string.IsNullOrEmpty(_root.Animations[i].Name)
						? string.Format("animation_{0}", i)
						: UnityPathUtil.GetLegalAssetName(_root.Animations[i].Name);
					clip.name = StringUtil.GetUniqueName(clip.name, clipNames);
					clipNames.Add(clip.name);
				}

				_imported.Animations.Add(clip);

				// Note: We do not use clip.name to store the animation name
				// because Unity clobbers that field when the clip is serialized
				// to an .asset file (i.e. during Editor glTF imports).

				var name = !string.IsNullOrEmpty(_root.Animations[i].Name)
					? _root.Animations[i].Name : string.Format("animation_{0}", i);
				_imported.AnimationNames.Add(name);

				_progressCallback?.Invoke(GltfImportStep.Animation, i + 1,
					_root.Animations.Count);

				yield return null;
			}

			if (_imported.Animations.Count == 0)
				yield break;

			// If we successfully imported at least one animation clip,
			// add a special "Static Pose" clip which can be played to
			// reset the model to its default pose.

			AnimationClip staticPoseClip = null;

			foreach (var result in
				AnimationUtil.CreateStaticPoseClip(_imported.Scene, legacy))
			{
				staticPoseClip = result;
				yield return null;
			}

			_imported.StaticPoseAnimationIndex = _imported.Animations.Count;
			_imported.AnimationNames.Add("Static Pose");
			_imported.Animations.Add(staticPoseClip);

			// Add Animation-related components to the root scene object,
			// for playing back animation clips at runtime.

			AddAnimationComponentsToSceneObject();
		}

		/// <summary>
		/// Add Animation-related components to the root scene object,
		/// for playing back animation clips at runtime.
		/// </summary>
		virtual protected void AddAnimationComponentsToSceneObject()
		{
			AddAnimationComponentToSceneObject();
			AddAnimationListToSceneObject();
		}

		/// <summary>
		/// Attach an ordered list of animation clip names to the
		/// scene object. This allows us to recover the original
		/// order of the animation clips in the glTF file, should
		/// we need it.
		/// </summary>
		protected void AddAnimationListToSceneObject()
		{
			var clips = new List<AnimationClip>();
			var names = new List<string>();

            // Note: By convention, we always put the static pose clip at index 0.

            var staticPoseIndex = _imported.StaticPoseAnimationIndex;
            clips.Add(_imported.Animations[staticPoseIndex]);
            names.Add(_imported.AnimationNames[staticPoseIndex]);

			for (var i = 0; i < _imported.Animations.Count; i++)
			{
				if (i == _imported.StaticPoseAnimationIndex)
					continue;

				// if we failed to import this particular clip
				if (_imported.Animations[i] == null)
					continue;

				clips.Add(_imported.Animations[i]);
				names.Add(_imported.AnimationNames[i]);
			}

			var list = _imported.Scene.AddComponent<AnimationList>();
			list.Clips = clips;
			list.Names = names;
		}

		/// <summary>
		/// Set up an Animation component on the root GameObject
		/// (i.e. the scene node) for playing Legacy animation
		/// clip(s) at runtime.
		/// </summary>
		protected void AddAnimationComponentToSceneObject()
		{
            var anim = _imported.Scene.AddComponent<UnityEngine.Animation>();
            anim.playAutomatically = false;

            // Note: By convention, we always put the static pose clip at index 0.

            var staticPoseClip = _imported.Animations[_imported.StaticPoseAnimationIndex];
            anim.AddClip(staticPoseClip, staticPoseClip.name);

            for (var i = 0; i < _imported.Animations.Count; ++i)
            {
	            if (i == _imported.StaticPoseAnimationIndex)
		            continue;

                var clip = _imported.Animations[i];

                // if we failed to import this particular clip
                if (clip == null)
	                continue;

                anim.AddClip(clip, clip.name);

                // make the first valid clip the default clip (i.e. the clip that's played
                // by Animation.Play()).
                if (anim.clip == null)
                    anim.clip = clip;
            }
		}

		/// <summary>
		/// Create an AnimationClip with the given name.
		/// </summary>
		protected virtual AnimationClip CreateAnimationClip()
		{
			// Note: We create a Legacy animation clip here
			// regardless of _importOptions.AnimationClipType. That
			// option is only implemented for EditorGltfImporter since
			// it not possible to create Mecanim clips in runtime
			// scripts (only Editor scripts).

			return new AnimationClip
			{
				wrapMode = UnityEngine.WrapMode.Loop,
				legacy = _importOptions.AnimationClipType == AnimationClipType.Legacy
			};
		}

		/// <summary>
		/// Load a glTF animation into a Unity AnimationClip.
		/// </summary>
		protected IEnumerator<AnimationClip> LoadAnimation(GLTF.Schema.Animation animation, int index)
		{
			// Note: The auto-generated name assigned to clip.name field
			// below is used as the key for referencing the clip
			// after it's been added to an Animation/Animator component.
			// The full/proper name of the animation from the glTF file is saved
			// separately in _imported.AnimationNames. This is necessary
			// because Unity clobbers the clip.name field when it
			// serializes the clip to an .asset file (during Editor imports).

			var clip = CreateAnimationClip();

			YieldTimer.Instance.Restart();

			foreach (var channel in animation.Channels)
			{
				foreach (var unused in LoadAnimationChannel(channel, clip))
					yield return null;
			}

			clip.EnsureQuaternionContinuity();

			yield return clip;
		}

		/// <summary>
		/// Load data from a single glTF animation channel into a Unity AnimationClip.
		/// In glTF, an animation channel describes how a single property
		/// of a node varies over time (e.g. translation, rotation).
		/// </summary>
		private IEnumerable LoadAnimationChannel(
			AnimationChannel channel, AnimationClip clip)
		{
		    var stopwatch = new Stopwatch();
		    stopwatch.Start();

			var nodeIndex = channel.Target.Node.Id;
			if (!_imported.Nodes.ContainsKey(nodeIndex))
				throw new Exception(string.Format(
					"animation targets non-existent node {0}", nodeIndex));

			var node = _imported.Nodes[nodeIndex];
			var nodePath = _imported.Scene.GetPathToDescendant(node);
			Debug.Assert(nodePath != null);

			var sampler = channel.Sampler.Value;

			var timeAccessor = sampler.Input.Value;
			var timeBuffer = _imported.Buffers[timeAccessor.BufferView.Value.Buffer.Id];
			var times = GLTFHelpers.ParseKeyframeTimes(timeAccessor, timeBuffer);

			var valueAccessor = sampler.Output.Value;
			var valueBuffer = _imported.Buffers[valueAccessor.BufferView.Value.Buffer.Id];

			switch (channel.Target.Path)
			{
				case GLTFAnimationChannelPath.translation:

					var translations = GLTFHelpers
						.ParseVector3Keyframes(valueAccessor, valueBuffer)
						.ToUnityVector3();

					if (YieldTimer.Instance.Expired)
					{
						yield return null;
						YieldTimer.Instance.Restart();
					}

					// Note: We pass in a function to negate the z-coord in order
					// to transform from glTF coords (right-handed coords where
					// +Z axis is forward) to Unity coords (left-handed coords
					// where +Z axis is forward).

					foreach (var unused in
						clip.SetCurvesFromVector3Array(
							nodePath, typeof(Transform), "m_LocalPosition",
							times, translations, v => new Vector3(v.x, v.y, -v.z),
							sampler.Interpolation))
					{
						yield return null;
					}

					break;

				case GLTFAnimationChannelPath.scale:

					var scales = GLTFHelpers
						.ParseVector3Keyframes(valueAccessor, valueBuffer)
						.ToUnityVector3();

					if (YieldTimer.Instance.Expired)
					{
						yield return null;
						YieldTimer.Instance.Restart();
					}

					foreach (var unused in
						clip.SetCurvesFromVector3Array(
							nodePath, typeof(Transform), "m_LocalScale",
							times, scales, null, sampler.Interpolation))
					{
						yield return null;
					}

					break;

				case GLTFAnimationChannelPath.rotation:

					var rotations = GLTFHelpers
						.ParseRotationKeyframes(valueAccessor, valueBuffer)
						.ToUnityVector4();

					if (YieldTimer.Instance.Expired)
					{
						yield return null;
						YieldTimer.Instance.Restart();
					}

					// Note: We pass in a function to negate the z and w coords
					// in order to transform from glTF coords (right-handed coords
					// where +Z axis is forward) to Unity coords (left-handed coords
					// where +Z axis is forward).

					foreach (var unused in
						clip.SetCurvesFromVector4Array(
							nodePath, typeof(Transform), "m_LocalRotation",
							times, rotations,
							v => new Vector4(v.x, v.y, -v.z, -v.w),
							sampler.Interpolation))
					{
						yield return null;
					}

					break;

				case GLTFAnimationChannelPath.weights:

					var weights = GLTFHelpers.ParseKeyframeTimes(
						valueAccessor, valueBuffer);

					if (YieldTimer.Instance.Expired)
					{
						yield return null;
						YieldTimer.Instance.Restart();
					}

					var meshIndex = _root.Nodes[nodeIndex].Mesh.Id;
					var numTargets = _root.Meshes[meshIndex].Primitives[0].Targets.Count;

					for (var i = 0; i < numTargets; ++i)
					{
						var property = string.Format("blendShape.{0}",
							GLTFUtils.buildBlendShapeName(meshIndex, i));

						foreach (var unused in
							clip.SetCurveFromFloatArray(
								nodePath, typeof(SkinnedMeshRenderer), property,
								times, weights, index => index * numTargets + i,
								sampler.Interpolation))
						{
							yield return null;
						}
					}

					break;

				default:

					throw new Exception(string.Format(
						"unsupported animation channel target: {0}", channel.Target.Path));
			}
		}

		/// <summary>
		/// Make the imported model visible. The model is hidden while
		/// the glTF import is in progress, so that the end user never
		/// sees the model in a partially reconstructed state.
		/// </summary>
		protected IEnumerator ShowModel()
		{
			if (_importOptions.ShowModelAfterImport)
				_imported.Scene.SetActive(true);

			yield return null;
		}

	}
}
