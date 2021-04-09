using System;

namespace Piglet
{
    /// <summary>
    /// This exception gets thrown by a glTF importer when it
    /// cannot resolve a relative URI to an absolute URI (e.g.
    /// an absolute file path or an HTTP URL.)
    ///
    /// Typically, this happens when the user attempts to import
    /// a .gltf file on the WebGL platform and the .gltf file
    /// references other files (e.g. .png files for textures).
    /// For security reasons, the browser does not tell us the
    /// absolute path of the input .gltf file, and so we have no
    /// way to locate files referenced by the .gltf on the local
    /// filesystem.  Furthermore, even if we could determine
    /// the paths of the dependent files, the browser does not
    /// allow reading files from arbitrary paths on the local filesystem.
    ///
    /// These security restrictions are the reason that the Piglet
    /// WebGL demo can only load self-contained .glb and .zip files.
    /// A similar problem occurs with "content URIs" on the Android platform,
    /// which are typically in place of an absolute file path used opening
    /// files from an Android file browser.
    /// </summary>
    public class UriResolutionException : Exception
    {
        public UriResolutionException(string message) : base(message) {}
    }
}
