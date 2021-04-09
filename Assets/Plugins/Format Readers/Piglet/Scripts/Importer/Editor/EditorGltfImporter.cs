#if UNITY_EDITOR
using Material = UnityEngine.Material;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using GLTF;
using GLTF.Schema;
using UnityEngine;
using UnityGLTF.Extensions;
using UnityEditor;

namespace Piglet
{
	/// <summary>
	/// A glTF importer for use inside the Unity Editor.
	/// EditorGltfImporter differs from RuntimeGltfImporter
	/// in the following ways: (1) EditorGltfImporter serializes
	/// the imported assets (e.g. textures, materials, meshes)
	/// to disk as Unity assets during import, whereas
	/// RuntimeGltfImporter only creates assets in memory.
	/// (2) EditorGltfImporter creates a prefab as its
	/// final output, whereas RuntimeGltfImporter creates
	/// an ordinary hierarchy of GameObjects (and returns the
	/// root).
	/// </summary>
	public class EditorGltfImporter : GltfImporter
	{
		// Import paths and options
		/// <summary>
		/// Parent directory of directory where importer will
		/// create Unity prefab and associated files
		/// (e.g. meshes, materials). Must be located inside Unity
		/// project folder.
		/// </summary>
		private string _importPath;

		/// <summary>
		/// Constructor
		/// </summary>
		public EditorGltfImporter(string gltfPath, string importPath,
			GltfImportOptions importOptions,
			ProgressCallback progressCallback = null)
			: base(new Uri(gltfPath), null, importOptions,
				new EditorGltfImportCache(UnityPathUtil.GetProjectPath(importPath)),
				progressCallback)
		{
			_importPath = importPath;
		}

		/// <summary>
		/// Coroutine-style implementation of GLTF import.
		/// </summary>
		/// <param name="gltfPath">
		/// Absolute path to .gltf/.glb file.
		/// </param>
		/// <param name="importPath">
		/// Absolute path of folder where prefab and
		/// associated assets will be created. Must be located under
		/// the "Assets" folder for the current Unity project.
		/// </param>
        /// <param name="importOptions">
        /// Options controlling glTF importer behaviour (e.g. should
        /// the imported model be automatically scaled to a certain size?).
        /// </param>
		public static GltfImportTask GetImportTask(string gltfPath,
			string importPath, GltfImportOptions importOptions = null)
		{
			GltfImportTask importTask = new GltfImportTask();

			if (importOptions == null)
				importOptions = new GltfImportOptions();

			EditorGltfImporter importer = new EditorGltfImporter(
				gltfPath, importPath, importOptions,
				(step, completed, total) =>
					 importTask.OnProgress?.Invoke(step, completed, total));

			importTask.AddTask(importer.ReadUri());
			importTask.AddTask(importer.ParseFile());
			importTask.AddTask(importer.LoadBuffers());
			importTask.AddTask(importer.LoadTextures());
			importTask.AddTask(importer.LoadMaterials());
			importTask.AddTask(importer.LoadMeshes());
			importTask.AddTask(importer.LoadScene());
			importTask.AddTask(importer.LoadMorphTargets());
			importTask.AddTask(importer.LoadSkins());
            importTask.AddTask(importer.ScaleModel());
			importTask.AddTask(importer.LoadAnimations());

			// note: the final subtask must return the
			// root GameObject for the imported model.
			importTask.AddTask(importer.CreatePrefabEnum());

			// callbacks to clean up any imported game objects / files
			// when the user aborts the import or an exception
			// occurs
			importTask.OnAborted += importer.Clear;
			importTask.OnException += _ => importer.Clear();

			return importTask;
		}

		override protected UnityEngine.Material LoadMaterial(
			GLTF.Schema.Material def, int index)
		{
			// Note: In the editor, a texture must be imported with "Texture Type"
			// set to "Normal map" before it can be assigned as the normal map
			// of a material. (I don't know why!)
			//
			// The material import will still work without the fix below, but
			// Unity will show a warning dialog and prompt the user to change
			// the texture type to "Normal map".

			if (def.NormalTexture != null) {
				var texture = _imported.Textures[def.NormalTexture.Index.Id];
				if (texture != null)
				{
					var importer = AssetImporter.GetAtPath(
						AssetDatabase.GetAssetPath(texture)) as TextureImporter;
					importer.textureType = TextureImporterType.NormalMap;
					importer.SaveAndReimport();
				}
			}

			return base.LoadMaterial(def, index);
		}

		/// <summary>
		/// Create an empty AnimationClip. This method
		/// overrides the base class implementation to create
		/// either a Legacy or Mecanim animation clip based on
		/// the value of _importOptions.AnimationClipType.
		/// </summary>
		override protected AnimationClip CreateAnimationClip()
		{
			AnimationClip clip = null;

			switch (_importOptions.AnimationClipType)
			{
				case AnimationClipType.Legacy:

					clip = base.CreateAnimationClip();
					break;

				case AnimationClipType.Mecanim:

					clip = new AnimationClip { legacy = false };

					// Make the animation loop indefinitely.
					// Note: The clip.wrapMode field only applies to Legacy clips.

					var settings = AnimationUtility.GetAnimationClipSettings(clip);
					settings.loopTime = true;
					AnimationUtility.SetAnimationClipSettings(clip, settings);

					break;
			}

			return clip;
		}

		/// <summary>
		/// Add Animation-related components to the root scene object,
		/// for playing back animation clips at runtime.
		/// </summary>
		override protected void AddAnimationComponentsToSceneObject()
		{
			// If we are importing Legacy-type animation clips,
			// use the base class implementation.

			if (_importOptions.AnimationClipType == AnimationClipType.Legacy)
			{
				base.AddAnimationComponentsToSceneObject();
				return;
			}

			// Add Animation components for playing Mecanim animation
			// clips at runtime.

			AddAnimatorComponentToSceneObject();
			AddAnimationListToSceneObject();
		}

		/// <summary>
		/// Set up an `Animator` component on the root scene object,
		/// for playing back Mecanim animation clips at runtime.
		/// </summary>
		protected void AddAnimatorComponentToSceneObject()
		{
			// Attach an `Animator` component for playing Mecanim animation clips.

			var anim = _imported.Scene.AddComponent<Animator>();

			// Create an `AnimatorController`, which is a
			// state machine used by the `Animator` component to control
			// transitions/blending between animation clips.
			//
			// In our case, we create the simplest possible state machine,
			// with a separate state for each animation clip and no
			// transitions between them.

			var controller = ((EditorGltfImportCache) _imported).AnimatorController;
			var stateMachine = controller.layers[0].stateMachine;

			foreach (var clip in _imported.Animations)
			{
				// if we failed to import this clip
				if (clip == null)
					continue;

				var state = stateMachine.AddState(clip.name);
				state.motion = clip;

				// make the first valid animation clip the default state
				if (stateMachine.entryTransitions.Length == 0)
					stateMachine.AddEntryTransition(state);
			}

			// assign the AnimatorController to the Animator

			anim.runtimeAnimatorController = controller;
		}

		/// <summary>
		/// Create a prefab from the imported hierarchy of game objects.
		/// This is the final output of an Editor glTF import.
		/// </summary>
		protected IEnumerator<GameObject> CreatePrefabEnum()
		{
			string basename = "scene.prefab";
			if (!String.IsNullOrEmpty(_imported.Scene.name))
			{
				basename = String.Format("{0}.prefab",
					GLTFUtils.cleanName(_imported.Scene.name));
			}

			string dir = UnityPathUtil.GetProjectPath(_importPath);
			string path = Path.Combine(dir, basename);

			GameObject prefab =
				PrefabUtility.SaveAsPrefabAsset(_imported.Scene, path);

			// Make the prefab visible.
			//
			// Note: The model hierarchy is kept hidden during the glTF import
			// so that the user never sees the partially reconstructed
			// model.

			prefab.SetActive(true);

			// Note: base.Clear() removes imported game objects from
			// the scene and from memory, but does not remove imported
			// asset files from disk.

			base.Clear();

			yield return prefab;
		}

		/// <summary>
		/// Remove any imported game objects from scene and from memory,
		/// and remove any asset files that were generated.
		/// </summary>
		protected override void Clear()
		{
			// remove imported game objects from scene and from memory
			base.Clear();

			// remove Unity asset files that were created during import
			UnityPathUtil.RemoveProjectDir(_importPath);
		}
	}
}
#endif
