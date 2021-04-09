using System;
using UnityEditor;
using UnityEngine;

namespace Piglet
{
    /// <summary>
    /// Editor window for setting import options regarding
    /// drag-and-drop import of .gltf/.glb/.zip files in
    /// the Project Browser.
    /// </summary>
    public class PigletOptionsWindow : EditorWindow
    {
        private PigletOptions _pigletOptions;

        private class Styles
        {
            public GUIStyle Button;
            public GUIStyle Label;
            public GUIStyle TextField;
            public GUIStyle Title;
            public GUIStyle ToggleLevel1;
            public GUIStyle ToggleLevel2;
            public GUIStyle ToggleLevel3;
        }

        private Styles _styles;

        private void InitStyles()
        {
            if (_styles != null)
                return;

#if UNITY_2019_3_OR_NEWER
            const int fontSize = 14;
            const int titleFontSize = 20;
#else
            const int fontSize = 12;
            const int titleFontSize = 18;
#endif

            _styles = new Styles();

            _styles.Button = new GUIStyle(GUI.skin.button);
            _styles.Button.fontSize = fontSize;
            _styles.Button.padding.left = 0;
            _styles.Button.margin.left = 0;

            _styles.Label = new GUIStyle(GUI.skin.label);
            _styles.Label.padding.left = 0;
            _styles.Label.fontSize = fontSize;

            _styles.TextField = new GUIStyle(GUI.skin.textField);
            _styles.TextField.fontSize = fontSize;

            _styles.Title = new GUIStyle(GUI.skin.label);
            _styles.Title.alignment = TextAnchor.MiddleLeft;
            _styles.Title.padding.left = 0;
            _styles.Title.margin = new RectOffset(0, 0, 15, 15);
            _styles.Title.fontSize = titleFontSize;

            // Note: For toggle controls, `padding.left` sets
            // the distance from the left edge of the control
            // to start of the text. This value needs to
            // be large enough to ensure that the text does
            // not overlap the checkbox graphic on the left
            // side of the control.

            _styles.ToggleLevel1 = new GUIStyle(GUI.skin.toggle);
            _styles.ToggleLevel1.margin.left = 0;
            _styles.ToggleLevel1.padding.left = 20;
            _styles.ToggleLevel1.fontSize = fontSize;

            _styles.ToggleLevel2 = new GUIStyle(_styles.ToggleLevel1);
            _styles.ToggleLevel2.margin.left += 20;

            _styles.ToggleLevel3 = new GUIStyle(_styles.ToggleLevel2);
            _styles.ToggleLevel3.margin.left += 20;
        }

        private void OnEnable()
        {
            _pigletOptions = Resources.Load<PigletOptions>("PigletOptions");
        }

        [MenuItem("Window/Piglet Options")]
        public static void ShowWindow()
        {
            EditorWindow window = GetWindow(typeof(PigletOptionsWindow),
                false, "Piglet Options");

            window.minSize = new Vector2(310f, 430f);
            window.maxSize = window.minSize;
        }

        void OnGUI()
        {
            InitStyles();

            const int MARGIN = 15;

            Rect contentRect = new Rect(
                MARGIN, MARGIN,
                position.width - 2 * MARGIN,
                position.height - 2 * MARGIN);

            GUILayout.BeginArea(contentRect);

                GUILayout.Label("Global Options", _styles.Title);

                    _pigletOptions.EnableDragAndDropImport
                        = GUILayout.Toggle(_pigletOptions.EnableDragAndDropImport,
                        new GUIContent("Enable drag-and-drop glTF imports",
                            "Enable automatic glTF imports when dragging " +
                            ".gltf/.glb/.zip files onto the Project Browser window"),
                        _styles.ToggleLevel2);

                GUI.enabled = _pigletOptions.EnableDragAndDropImport;

                GUILayout.Label("Import Options", _styles.Title);

                     _pigletOptions.ImportOptions.AutoScale
                         = GUILayout.Toggle(
                             _pigletOptions.ImportOptions.AutoScale,
                             new GUIContent("Scale model to standard size",
                                "Automatically scale the imported glTF model so that " +
                                "its longest dimension is equal to the given size"),
                             _styles.ToggleLevel2);

                         GUI.enabled = _pigletOptions.EnableDragAndDropImport
                             && _pigletOptions.ImportOptions.AutoScale;

                         GUILayout.BeginHorizontal(GUILayout.Height(20));
                            GUILayout.Space(40);

                            GUILayout.BeginVertical();
                                GUILayout.FlexibleSpace();
                                GUILayout.Label("Size", _styles.Label);
                                GUILayout.FlexibleSpace();
                            GUILayout.EndVertical();

                            GUILayout.BeginVertical();
                                GUILayout.FlexibleSpace();
                                _pigletOptions.ImportOptions.AutoScaleSize =
                                    EditorGUILayout.FloatField(
                                        _pigletOptions.ImportOptions.AutoScaleSize,
                                        _styles.TextField,  GUILayout.Width(50));
                                GUILayout.FlexibleSpace();
                            GUILayout.EndVertical();

                            GUILayout.FlexibleSpace();
                         GUILayout.EndHorizontal();

                         GUI.enabled = _pigletOptions.EnableDragAndDropImport;

                    _pigletOptions.ImportOptions.ImportAnimations
                        = GUILayout.Toggle(
                            _pigletOptions.ImportOptions.ImportAnimations,
                             new GUIContent("Import animations",
                                "Import animations from glTF file as Unity AnimationClips"),
                             _styles.ToggleLevel2);

                        GUI.enabled = _pigletOptions.EnableDragAndDropImport
                            && _pigletOptions.ImportOptions.ImportAnimations;

                        GUILayout.BeginHorizontal(GUILayout.Height(20));
                            GUILayout.Space(40);

                            GUILayout.BeginVertical();
                                GUILayout.FlexibleSpace();
                                GUILayout.Label("Animation clip type", _styles.Label);
                                GUILayout.FlexibleSpace();
                            GUILayout.EndVertical();

                            GUILayout.BeginVertical();
                                GUILayout.FlexibleSpace();
                                _pigletOptions.ImportOptions.AnimationClipType
                                    = (AnimationClipType) EditorGUILayout.Popup(
                                        (int) _pigletOptions.ImportOptions.AnimationClipType,
                                        Enum.GetNames(typeof(AnimationClipType)),
                                        GUILayout.Width(100));
                                GUILayout.FlexibleSpace();
                            GUILayout.EndVertical();

                            GUILayout.FlexibleSpace();
                        GUILayout.EndHorizontal();

                        GUI.enabled = _pigletOptions.EnableDragAndDropImport;

                GUILayout.Label("Editor Options", _styles.Title);

                     _pigletOptions.PromptBeforeOverwritingFiles
                        = GUILayout.Toggle(
                            _pigletOptions.PromptBeforeOverwritingFiles,
                            new GUIContent("Prompt before overwriting files",
                                "Show confirmation prompt if glTF import directory " +
                                "already exists"),
                            _styles.ToggleLevel2);

                     _pigletOptions.LogProgress
                        = GUILayout.Toggle(
                            _pigletOptions.LogProgress,
                            new GUIContent("Print progress messages in Console",
                               "Log progress messages to Unity Console window during " +
                               "glTF imports (useful for debugging)"),
                            _styles.ToggleLevel2);

                     _pigletOptions.SelectPrefabAfterImport
                        = GUILayout.Toggle(
                            _pigletOptions.SelectPrefabAfterImport,
                            new GUIContent("Select prefab in Project Browser",
                                "After a glTF import has completed, select/highlight " +
                                "the generated prefab in the Project Browser window"),
                            _styles.ToggleLevel2);

                     _pigletOptions.AddPrefabToScene
                        = GUILayout.Toggle(
                            _pigletOptions.AddPrefabToScene,
                            new GUIContent("Add prefab instance to scene",
                                "After a glTF import has completed, add the generated prefab to " +
                                "the current Unity scene, as a child of the currently selected " +
                                "game object. If no game object is selected in the scene, add " +
                                "the prefab at the root of the scene instead."),
                            _styles.ToggleLevel2);

                     GUI.enabled = _pigletOptions.EnableDragAndDropImport
                         && _pigletOptions.AddPrefabToScene;

                         _pigletOptions.SelectPrefabInScene
                            = GUILayout.Toggle(
                                _pigletOptions.SelectPrefabInScene,
                                new GUIContent("Select prefab instance in scene",
                                    "Select/highlight the prefab in the scene hierarchy " +
                                    "after adding it to the scene"),
                                _styles.ToggleLevel3);

                     GUI.enabled = _pigletOptions.EnableDragAndDropImport;

                     _pigletOptions.OpenPrefabAfterImport
                        = GUILayout.Toggle(
                            _pigletOptions.OpenPrefabAfterImport,
                            new GUIContent("Open prefab in Prefab View",
                                "After a glTF import has completed, open the generated " +
                                "prefab in the Prefab View. (This is equivalent to " +
                                "double-clicking the prefab in the Project Browser.)"),
                            _styles.ToggleLevel2);

                GUI.enabled = true;

                GUILayout.Space(20);

                if (GUILayout.Button(new GUIContent("Reset to Defaults",
                    "Reset all options to their default values"),
                    _styles.Button, GUILayout.Width(150)))
                {
                    _pigletOptions.Reset();
                }

            GUILayout.EndArea();

            // Tell Unity that _pigletOptions needs to be saved
            // to disk on the next call to AssetDatabase.SaveAssets().
            //
            // Note: With respect to the GUI code above, it's not very
            // convenient to check if the _pigletOptions values have
            // actually changed since they were first loaded from disk.
            // Instead I just set the dirty flag unconditionally,
            // with the hope that this does not hurt Editor performance.

            EditorUtility.SetDirty(_pigletOptions);

        }
    }
}