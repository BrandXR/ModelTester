using System;
using UnityEngine;

namespace Piglet
{
    /// <summary>
    /// User-configurable options for drag-and-drop import of
    /// glTF models in the Project Browser.  These options are
    /// set in the Piglet Options window, located under
    /// Window -> Piglet Options in the Unity menu.
    /// </summary>
    [CreateAssetMenu(fileName="PigletOptions", menuName="Piglet Options", order=51)]
    public class PigletOptions : ScriptableObject
    {
        /// <summary>
        /// Enable/disable automatic import of glTF models when
        /// dragging external .gltf/.glb/.zip files into the Project
        /// Browser.
        ///
        /// Note: If this option is set to false, none of the other options
        /// have any effect.
        /// </summary>
        [SerializeField] public bool EnableDragAndDropImport;

        /// <summary>
        /// Options that are common to both Editor and runtime glTF imports.
        /// </summary>
        [SerializeField] public GltfImportOptions ImportOptions;

        /// <summary>
        /// If true, print progress messages to the Unity
        /// Console during a glTF import.
        /// </summary>
        [SerializeField] public bool LogProgress;

        /// <summary>
        /// If true, show a confirmation prompt whenever a drag-and-drop
        /// import would overwrite existing files.
        /// </summary>
        [SerializeField] public bool PromptBeforeOverwritingFiles;

        /// <summary>
        /// If true, select the imported prefab in the Project Browser
        /// after a glTF import has completed.
        /// </summary>
        [SerializeField] public bool SelectPrefabAfterImport;

        /// <summary>
        /// Open the imported prefab in Prefab View (replaces current
        /// Scene View) after a glTF import has completed.
        /// </summary>
        [SerializeField] public bool OpenPrefabAfterImport;

        /// <summary>
        /// Add an instance of the imported prefab to the current
        /// scene, as a child of the currently selected GameObject
        /// (if any).
        [SerializeField] public bool AddPrefabToScene;

        /// <summary>
        /// Select the prefab in the scene view, after adding
        /// it to the current scene. This option has no effect
        /// unless AddPrefabToScene is true.
        /// </summary>
        [SerializeField] public bool SelectPrefabInScene;

        /// <summary>
        /// Reset all Piglet glTF import options to default values.
        /// </summary>
        public void Reset()
        {
            EnableDragAndDropImport = true;

            ImportOptions = new GltfImportOptions();

            LogProgress = false;
            PromptBeforeOverwritingFiles = true;
            SelectPrefabAfterImport = true;
            OpenPrefabAfterImport = true;
            AddPrefabToScene = false;
            SelectPrefabInScene = false;
        }
    }
}