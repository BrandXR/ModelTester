using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityGLTF;

namespace Piglet
{
    /// <summary>
    /// Hooks up Piglet's EditorGltfImporter to be invoked when
    /// dragging-and-dropping .gltf/.glb/.zip files into the
    /// Project Browser.
    /// </summary>
    public static class ProjectBrowserGltfImporter
    {
        /// <summary>
        /// Coroutine for currently running glTF import (if any).
        /// The string returned by the IEnumerator is the
        /// target import directory.
        /// </summary>
        private static IEnumerator<string> _importTask;

        /// <summary>
        /// Import options, configurable by the user
        /// in the Piglet Options Window.  (Available
        /// under Window -> PigletOptions in the Unity
        /// menu.)
        /// </summary>
        private static PigletOptions _pigletOptions;

        [InitializeOnLoadMethod]
        private static void Setup()
        {
            _importTask = null;
            ProjectBrowserDragAndDrop.OnDragAndDrop += HandleDragAndDrop;
        }

        /// <summary>
        /// Callback that is invoked when external file(s) are
        /// dragged-and-dropped into the Project Browser.
        /// </summary>
        private static void HandleDragAndDrop(string targetPath, string[] droppedPaths)
        {
            // Hold the Control key or Command key while
            // dragging-and-dropping a .gltf/.glb/.zip to
            // copy the file into the project without
            // performing an automatic glTF import.

            if (Event.current.control || Event.current.command)
                return;

            // Read current import options from Piglet Options window.
            //
            // Note: The SaveAssets/Refresh calls ensure that any changes
            // made in the Piglet Options window are saved out to disk
            // first.

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            _pigletOptions = Resources.Load<PigletOptions>("PigletOptions");

            // Do nothing if drag-and-drop glTF import has been disabled by the user

            if (!_pigletOptions.EnableDragAndDropImport)
                return;

            // If `targetPath` is a regular file and not a directory, use
            // the parent directory as the import directory.

            string importDir = Directory.Exists(targetPath)
                ? targetPath : Path.GetDirectoryName(targetPath);
            importDir = UnityPathUtil.NormalizePathSeparators(importDir);

            // Exclude files that don't have .gltf/.glb extension.
            //
            // Note: I would prefer to pass skipped files through to Unity
            // for default drag-and-drop handling, but that does not seem
            // to be possible because `DragAndDrop.paths` is read-only.

            List<string> acceptedPaths = new List<string>();
            foreach (string path in DragAndDrop.paths)
            {
                // Don't trigger automatic glTF import when we are dragging
                // a .gltf/.glb/.zip file from within the Unity project folder.
                //
                // When the source file is inside the Unity project folder,
                // the Unity drag-and-drop machinery will report a relative path
                // starting with "Assets/".

                if (path.StartsWith("Assets/"))
                    continue;

                string _path = path.ToLower();

                if (_path.EndsWith(".gltf") || _path.EndsWith(".glb"))
                    acceptedPaths.Add(path);

                else if (_path.EndsWith(".zip") && ZipUtil.ContainsGltfFile(path))
                    acceptedPaths.Add(path);
            }

            if (acceptedPaths.Count > 0)
            {
                // Run GLTF import(s) in the background.

                StartImport(acceptedPaths, importDir);

                // Consume the `DragPerform` event, so that Unity's
                // default drag-and-drop handling, which
                // simply copies the file(s) into the target Assets
                // folder, is not performed.

                Event.current.Use();
            }
        }

        /// <summary>
        /// Import GLTF files asynchronously with Piglet's EditorGltfImporter.
        /// </summary>
        private static void StartImport(List<string> gltfPaths, string importDir)
        {
            // If there is already an import job already running don't start
            // a new one. The user must press the "Cancel" in the Editor
            // progress dialog before starting a new import.

            if (_importTask != null)
                return;

            // Reset/initialize callbacks for logging progress messages
            // during glTF import.

            ProgressLog.Instance.AddLineCallback = null;
            ProgressLog.Instance.UpdateLineCallback = null;

            if (_pigletOptions.LogProgress)
            {
                ProgressLog.Instance.AddLineCallback = Debug.Log;
                ProgressLog.Instance.UpdateLineCallback = Debug.Log;
            }

            _importTask = ImportCoroutine(gltfPaths, importDir);

           void ImporterUpdate()
            {
                if (!_importTask.MoveNext())
                {
                    EditorApplication.update -= ImporterUpdate;
                    _importTask = null;
                }
            }

            EditorApplication.update += ImporterUpdate;
        }

        /// <summary>
        /// Coroutine to import GLTF files with Piglet's EditorGltfImporter.
        /// The string value returned via the IEnumerator is the target directory
        /// for the current import, so that files from an aborted/canceled import
        /// can be easily cleaned up.
        /// </summary>
        private static IEnumerator<string> ImportCoroutine(List<string> gltfPaths, string baseImportDir)
        {
            foreach (string gltfPath in gltfPaths)
            {
                string gltfBasename = Path.GetFileName(gltfPath);
                string gltfBasenameNoExt = Path.GetFileNameWithoutExtension(gltfPath);

                bool abortImport = false;

                // callback for updating progress during glTF import
                void OnProgress(GltfImportStep type, int count, int total)
                {
                    ProgressLog.Instance.OnImportProgress(type, count, total);

                    abortImport = EditorUtility.DisplayCancelableProgressBar(
                        $"Importing {gltfBasename}...",
                        ProgressLog.Instance.GetProgressMessage(),
                        (float) count / total);
                }

                string importPath = UnityPathUtil.NormalizePathSeparators(
                    Path.Combine(baseImportDir, gltfBasenameNoExt));
                string importProjectPath = UnityPathUtil.GetProjectPath(importPath);

                if ((Directory.Exists(importPath) || File.Exists(importPath))
                    && _pigletOptions.PromptBeforeOverwritingFiles)
                {
                    if (!EditorUtility.DisplayDialog(
                        "Warning!",
                        $"Overwrite \"{importProjectPath}\"?",
                        "OK", "Cancel"))
                        yield break;

                    FileUtil.DeleteFileOrDirectory(importPath);
                    AssetDatabase.Refresh();
                }

                GltfImportTask importTask =
                    EditorGltfImporter.GetImportTask(gltfPath, importPath,
                        _pigletOptions.ImportOptions);

                importTask.OnProgress = OnProgress;

                GameObject importedPrefab = null;
                importTask.OnCompleted = (prefab) => importedPrefab = prefab;

                // restart import timer at zero
                ProgressLog.Instance.StartImport();

                while (true)
                {
                    if (abortImport)
                    {
                        importTask.Abort();
                        EditorUtility.ClearProgressBar();
                        yield break;
                    }

                    try
                    {
                        if (!importTask.MoveNext())
                            break;
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);

                        EditorUtility.ClearProgressBar();
                        EditorUtility.DisplayDialog("Import Failed",
                            String.Format("Import of {0} failed. "
                                + "See Unity console log for details.", gltfBasename),
                            "OK");

                        yield break;
                    }

                    yield return importPath;
                }

                // Before modifying the selection, store a handle to
                // the transform of the currently selected game object (if any).

                Transform selectedTransform = Selection.activeTransform;

                // Select the prefab file in the Project Browser.
                if (_pigletOptions.SelectPrefabAfterImport)
                {
                    Selection.activeObject = importedPrefab;
                    yield return importPath;
                }

                if (_pigletOptions.AddPrefabToScene)
                {
                    // If we are currently in Prefab Mode, exit
                    // back to the main scene hierarchy view.
                    //
                    // Note: Prefab Mode was introduced in Unity 2018.3.
#if UNITY_2018_3_OR_NEWER
                    if (StageUtility.GetCurrentStageHandle()
                        != StageUtility.GetMainStageHandle())
                    {
                        StageUtility.GoToMainStage();
                    }
#endif

                    GameObject instance = (GameObject)PrefabUtility
                        .InstantiatePrefab(importedPrefab);

                    // parent the prefab instance to the currently
                    // selected GameObject (if any)
                    if (selectedTransform != null)
                        instance.transform.parent = selectedTransform;

                    if (_pigletOptions.SelectPrefabInScene)
                    {
                        Selection.activeGameObject = instance;
                        yield return importPath;
                    }
                }

                if (_pigletOptions.OpenPrefabAfterImport)
                {
                    AssetDatabase.OpenAsset(importedPrefab);

                    // Note: This is the best method I could find
                    // for automatically centering the prefab in
                    // the scene view. For further info, see
                    // https://answers.unity.com/questions/813814/framing-objects-via-script-in-the-unity-editor.html
                    SceneView.FrameLastActiveSceneView();
                }

                EditorUtility.ClearProgressBar();

            }
        }

    }
}