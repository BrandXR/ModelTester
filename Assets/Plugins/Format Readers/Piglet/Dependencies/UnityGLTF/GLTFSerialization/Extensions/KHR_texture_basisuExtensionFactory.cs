using GLTF.Extensions;
using Newtonsoft.Json.Linq;

namespace GLTF.Schema
{
	/// <summary>
	/// Parses JSON data for the KHR_texture_basisu glTF extension
	/// and loads it into an equivalent C# class (KHR_texture_basisuExtension).
	///
    /// For details/examples of the KHR_texture_basisu extension, see:
    /// https://github.com/KhronosGroup/glTF/blob/master/extensions/2.0/Khronos/KHR_texture_basisu/README.md
	/// </summary>
    public class KHR_texture_basisuExtensionFactory : ExtensionFactory
    {
		public const string EXTENSION_NAME = "KHR_texture_basisu";

		public KHR_texture_basisuExtensionFactory()
		{
			ExtensionName = EXTENSION_NAME;
		}

		/// <summary>
		/// Parse JSON for KHR_texture_basisu glTF extension and load it
		/// into an equivalent C# class (`KHR_texture_basisuExtension`).
		/// </summary>
		/// <param name="root">
		/// C# object hierarchy mirroring entire JSON content of glTF file
		/// (everything except extensions).
		/// </param>
		/// <param name="extensionToken">
		/// Root JSON token for KHR_texture_basisu extension.
		/// </param>
		/// <returns>
		/// C# object (`KHR_texture_basisuExtension`) mirroring content of
		/// JSON for KHR_texture_basisu extension.
		/// </returns>
        public override Extension Deserialize(GLTFRoot root, JProperty extensionToken)
        {
	        // The "source" JSON property provides the glTF image index for the
	        // associated KTX2 file/buffer. This property is required for
	        // the KHR_texture_basisu extension, so if it missing we just return
	        // null for the entire extension, as if the extension were not
	        // present in the first place.

	        var sourceToken = extensionToken?.Value["source"];
	        if (sourceToken == null)
		        return null;

	        var source = sourceToken.DeserializeAsInt();

	        return new KHR_texture_basisuExtension(source);
        }
    }
}