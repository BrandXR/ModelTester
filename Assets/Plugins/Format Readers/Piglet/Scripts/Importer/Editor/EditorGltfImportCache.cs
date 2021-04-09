#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Piglet
{
    /// <summary>
    /// An import cache that serializes imported Unity assets to
    /// disk, in addition to holding them in memory.  This import
    /// cache is intended for glTF imports that take place
    /// in the Unity Editor.
    /// </summary>
    public class EditorGltfImportCache : GltfImportCache
    {
        /// <summary>
        /// The state machine that controls transitions/blending
        /// between animation clips. This asset is only used when
        /// Piglet is configured to import animations as Mecanim
        /// clips (as opposed to Legacy clips).
        /// </summary>
        protected AnimatorController _animatorController;

        /// <summary>
        /// The state machine that controls transitions/blending
        /// between animation clips. This asset is only used when
        /// Piglet is configured to import animations as Mecanim
        /// clips (as opposed to Legacy clips).
        /// </summary>
        public AnimatorController AnimatorController
        {
            get
            {
                if (_animatorController == null)
                {
                    Directory.CreateDirectory(UnityPathUtil.GetAbsolutePath(_importAnimationsDir));
                    var path = Path.Combine(_importAnimationsDir, "controller.controller");
                    _animatorController = AnimatorController.CreateAnimatorControllerAtPath(path);
                }

                return _animatorController;
            }
        }

        /// <summary>
        /// The base project directory for saving the imported
        /// Unity assets (e.g. "Assets/Imported/MyModel").
        /// </summary>
        protected string _importBaseDir;

        /// <summary>
        /// The project directory for saving imported textures
        /// (e.g. "Assets/Imported/MyModel/Textures").
        /// </summary>
        protected string _importTexturesDir;

        /// <summary>
        /// The project directory for saving imported materials
        /// (e.g. "Assets/Imported/MyModel/Materials").
        /// </summary>
        protected string _importMaterialsDir;

        /// <summary>
        /// The project directory for saving imported meshes
        /// (e.g. "Assets/Imported/MyModel/Meshes").
        /// </summary>
        protected string _importMeshesDir;

        /// <summary>
        /// The project directory for saving imported animations
        /// (e.g. "Assets/Imported/MyModel/Animations").
        /// </summary>
        protected string _importAnimationsDir;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="importBaseDir">
        /// The base project directory for saving the imported
        /// Unity assets (e.g. "Assets/Imported/MyModel").
        /// </param>
        public EditorGltfImportCache(string importBaseDir)
        {
            Textures = new SerializedAssetList<Texture2D>(SerializeTexture);
            Materials = new SerializedAssetList<Material>(SerializeMaterial);
            Meshes = new SerializedAssetList<List<KeyValuePair<Mesh,Material>>>
                (SerializeMesh);
            Animations = new SerializedAssetList<AnimationClip>(SerializeAnimationClip);

            // create directory structure for imported assets

            _importBaseDir = importBaseDir;
            Directory.CreateDirectory(
                UnityPathUtil.GetAbsolutePath(_importBaseDir));

            _importTexturesDir = Path.Combine(_importBaseDir, "Textures");
            _importMaterialsDir = Path.Combine(_importBaseDir, "Materials");
            _importMeshesDir = Path.Combine(_importBaseDir, "Meshes");
            _importAnimationsDir = Path.Combine(_importBaseDir, "Animations");
        }

        /// <summary>
        /// Save the given texture to disk as a Unity asset and
        /// return a new Texture2D. The returned Texture2D
        /// is the same as the original, except that it
        /// knows about the asset file that backs it and will
        /// automatically synchronize in-memory changes to disk.
        /// (For further info, see the Unity documentation for
        /// AssetDatabase.)
        /// </summary>
        /// <param name="texture">
        /// The texture to be serialized to disk.
        /// </param>
        /// <returns>
        /// A new Texture2D that is backed by an asset file.
        /// </returns>
        protected Texture2D SerializeTexture(int index, Texture2D texture)
        {
            Directory.CreateDirectory(UnityPathUtil.GetAbsolutePath(_importTexturesDir));

			// If the texture was initially loaded upside-down,
			// flip the texture vertically using a RenderTexture.
			//
			// `UnityWebRequestTexture` loads PNG/JPG images into textures
			// upside-down, whereas KtxUnity loads KTX2/BasisU images
            // into textures right-side-up.

            if (TextureIsUpsideDown[index])
                texture = TextureUtil.FlipTexture(texture);

            // Convert the texture to a "readable" texture so
            // that we can call EncodeToPNG() on it.
            //
            // In Unity, a "readable" texture is a texture whose
            // uncompressed color data is available in RAM, in
            // addition to existing on the GPU. For further info, see:
            // https://docs.unity3d.com/ScriptReference/TextureImporter-isReadable.html

            texture = TextureUtil.GetReadableTexture(texture);

            string basename = String.Format("{0}.png", texture.name);
            string pngPath = Path.Combine(_importTexturesDir, basename);
            byte[] pngData = texture.EncodeToPNG();
            File.WriteAllBytes(UnityPathUtil.GetAbsolutePath(pngPath), pngData);

            AssetDatabase.Refresh();
            texture = (Texture2D) AssetDatabase.LoadAssetAtPath(
                pngPath, typeof(Texture2D));

            return texture;
        }

        /// <summary>
        /// Save the given material to disk as a Unity asset
        /// and return a new Material. The returned Material
        /// is the same as the original, except that it knows
        /// about the .mat file that backs it and will automatically
        /// synchronize in-memory changes to disk. (For further
        /// info, see the Unity documentation for AssetDatabase.)
        /// </summary>
        /// <param name="material">
        /// The material to be serialized to disk
        /// </param>
        /// <returns>
        /// A new Material that is backed by a .mat file
        /// </returns>
        protected Material SerializeMaterial(int index, Material material)
        {
            Directory.CreateDirectory(UnityPathUtil.GetAbsolutePath(_importMaterialsDir));

            string basename = String.Format("{0}.mat", material.name);
            string path = Path.Combine(_importMaterialsDir, basename);

            AssetDatabase.CreateAsset(material, path);
            AssetDatabase.Refresh();
            material = (Material) AssetDatabase.LoadAssetAtPath(
                path, typeof(Material));

            return material;
        }

        /// <summary>
        /// Save the input mesh to disk as a set of Unity .asset
        /// files and return a new mesh. The input mesh is a list
        /// mesh primitives, where each primitive is a KeyValuePair
        /// of a Mesh and a Material.  The returned mesh (i.e. list of primitives)
        /// is the same as the input list, except that the Mesh
        /// for each primitive has been replaced by one that is backed
        /// by a Unity .asset file.  These Mesh objects know about
        /// their backing .asset file and will automatically sync
        /// in-memory changes to the Mesh to disk. (For further
        /// info, see the Unity documentation for AssetDatabase.)
        /// </summary>
        /// <param name="mesh">
        /// The mesh (list of mesh primitives) to be serialized to disk.
        /// </param>
        /// <returns>
        /// A new mesh (list of mesh primitives) that is backed by a
        /// set of .asset files (one per mesh primitive).
        /// </returns>
        protected List<KeyValuePair<Mesh, Material>> SerializeMesh(
            int index, List<KeyValuePair<Mesh, Material>> mesh)
        {
            Directory.CreateDirectory(UnityPathUtil.GetAbsolutePath(_importMeshesDir));

            for (int i = 0; i < mesh.Count; ++i)
            {
                Mesh primitiveMesh = mesh[i].Key;
                Material primitiveMaterial = mesh[i].Value;

                string basename = String.Format("{0}.asset", primitiveMesh.name);
                string path = Path.Combine(_importMeshesDir, basename);

                // Serialize the mesh to disk as a Unity asset.
                //
                // Note: The primitiveMaterial does not need
                // to be serialized here, since that has already
                // been done during the earlier material-importing
                // step.

                AssetDatabase.CreateAsset(primitiveMesh, path);
                AssetDatabase.Refresh();
                primitiveMesh = (Mesh) AssetDatabase.LoadAssetAtPath(
                    path, typeof(Mesh));

                mesh[i] = new KeyValuePair<Mesh, Material>(
                    primitiveMesh, primitiveMaterial);
            }

            return mesh;
        }

        /// <summary>
        /// Save the given AnimationClip to disk as a Unity asset
        /// and return a new AnimationClip. The returned AnimationClip
        /// is the same as the original, except that it knows
        /// about the .anim file that backs it and will automatically
        /// synchronize in-memory changes to disk. (For further
        /// info, see the Unity documentation for AssetDatabase.)
        /// </summary>
        /// <param name="clip">
        /// The AnimationClip to be serialized to disk
        /// </param>
        /// <returns>
        /// A new AnimationClip that is backed by a .anim file
        /// </returns>
        private AnimationClip SerializeAnimationClip(int index, AnimationClip clip)
        {
            Directory.CreateDirectory(UnityPathUtil.GetAbsolutePath(_importAnimationsDir));

            string basename = string.Format("{0}.anim", clip.name);
            string path = Path.Combine(_importAnimationsDir, basename);

            AssetDatabase.CreateAsset(clip, path);
            AssetDatabase.Refresh();
            clip = (AnimationClip) AssetDatabase.LoadAssetAtPath(
                path, typeof(AnimationClip));

            return clip;
        }

        /// <summary>
        /// Remove a game object from the scene and from memory.
        /// Note: This method uses Object.DestroyImmediate instead of
        /// Object.Destroy because it is run from inside the Editor.
        /// Object.Destroy relies on the Unity game loop and thus only
        /// works in Play Mode.
        /// </summary>
        override protected void Destroy(GameObject gameObject)
        {
            Object.DestroyImmediate(gameObject);
        }
    }
}
#endif
