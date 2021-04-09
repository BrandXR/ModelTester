using Piglet;
using UnityEngine;

/// <summary>
/// This MonoBehaviour provides a minimal example for
/// importing and playing glTF animations at runtime.
/// </summary>
public class RuntimeAnimationBehaviour : MonoBehaviour
{
    /// <summary>
    /// The currently running glTF import task.
    /// </summary>
    private GltfImportTask _task;

    /// <summary>
    /// Unity callback that is invoked before the first frame.
    /// Create the glTF import task and set up callback for
    /// successful completion.
    /// </summary>
    void Start()
    {
        // Uniformly scale the model such that the longest
        // dimension of its world-space axis-aligned bounding
        // box becomes 4.0 units.
        var importOptions = new GltfImportOptions();
        importOptions.AutoScale = true;
        importOptions.AutoScaleSize = 4.0f;

        // Note: To import a local .gltf/.glb/.zip file, you may
        // instead pass an absolute file path to GetImportTask
        // (e.g. "C:/Users/Joe/Desktop/piggleston.glb"), or a byte[]
        // array containing the raw byte content of the file.
        _task = RuntimeGltfImporter.GetImportTask(
            "https://awesomesaucelabs.github.io/piglet-webgl-demo/StreamingAssets/cartoon_hartman.zip",
            importOptions);

        _task.OnCompleted = OnComplete;
    }

    /// <summary>
    /// Callback that is invoked by the glTF import task
    /// after it has successfully completed.
    /// </summary>
    /// <param name="importedModel">
    /// the root GameObject of the imported glTF model
    /// </param>
    private void OnComplete(GameObject importedModel)
    {
        var anim = importedModel.GetComponent<Animation>();
        var animList = importedModel.GetComponent<AnimationList>();

        // Note: Imported animation clips always start
        // at index 1, because index "0" is reserved for
        // the "Static Pose" clip.
        var clipKey = animList.Clips[1].name;
        anim.Play(clipKey);

        Debug.Log("Success!");
    }

    /// <summary>
    /// Unity callback that is invoked after every frame.
    /// Here we call MoveNext() to advance execution
    /// of the glTF import task.
    /// </summary>
    void Update()
    {
        // advance execution of glTF import task
        _task.MoveNext();
    }
}
