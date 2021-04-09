using System.Collections.Generic;
using UnityEngine;

namespace Piglet
{
    /// <summary>
    /// A glTF import cache that stores all imported Unity assets in
    /// memory. This import cache is intended for glTF imports that
    /// take place at runtime.
    /// </summary>
    public class RuntimeGltfImportCache : GltfImportCache
    {
        public RuntimeGltfImportCache()
        {
            Textures = new List<Texture2D>();
            Materials = new List<Material>();
            Meshes = new List<List<KeyValuePair<Mesh,Material>>>();
            Animations = new List<AnimationClip>();
        }
    }
}