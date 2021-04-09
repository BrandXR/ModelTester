using Newtonsoft.Json.Linq;

namespace GLTF.Schema
{
    /// <summary>
    /// C# class mirroring the JSON content of the KHR_texture_basisu
    /// glTF extension (for loading KTX2/BasisU images).
    ///
    /// For details/examples of the KHR_texture_basisu extension, see:
    /// https://github.com/KhronosGroup/glTF/blob/master/extensions/2.0/Khronos/KHR_texture_basisu/README.md
     /// </summary>
    public class KHR_texture_basisuExtension : Extension
    {
        /// <summary>
        /// glTF image index for the KTX2 file/buffer
        /// that backs this texture.
        /// </summary>
        public int Source;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="source">
        /// glTF image index for the KTX2 file/buffer.
        /// </param>
        public KHR_texture_basisuExtension(int source)
        {
            Source = source;
        }

        public JProperty Serialize()
        {
            throw new System.NotImplementedException();
        }
    }
}