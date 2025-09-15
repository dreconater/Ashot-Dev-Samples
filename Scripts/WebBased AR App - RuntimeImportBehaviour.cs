/// <summary>
/// Loads a glTF model at runtime for a WebGL-based web browser AR application.
/// 
/// In the Unity Editor, a default model URL is used.
/// In a WebGL build, the model URL is read from the web page’s query string
/// (e.g., ?model=https://.../file.glb).
/// 
/// The model is downloaded, imported, positioned relative to the Face transform,
/// and given a collider and interaction script so it can be manipulated in AR.
/// </summary>

using Piglet;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using System.Linq;

public class RuntimeImportBehaviour : MonoBehaviour
{
    private GltfImportTask _task;
    private GameObject _model;
    private GameObject _currentModel;
    private bool _isLoadingModel = false;

    [Header("UI")]
    public GameObject LoadingScreen;

    [Header("Model Placement")]
    public Transform Face;

    private void Start()
    {
        LoadingScreen.SetActive(true);

#if UNITY_EDITOR
        string modelUrl = "https://www.dropbox.com/scl/fi/jlz3efd6o1n35czgbu8d7/sunglasses.glb?dl=1";
#else
        string url = Application.absoluteURL;
        string modelUrl = GetModelParameterFromUrl(url);
#endif

        Debug.Log("Model URL: " + modelUrl);
        DownloadModel(modelUrl);
    }

    private string GetModelParameterFromUrl(string url)
    {
        string modelKey = "model=";
        int modelStartIndex = url.IndexOf(modelKey);
        if (modelStartIndex == -1)
            return string.Empty;

        int valueStartIndex = modelStartIndex + modelKey.Length;
        return url.Substring(valueStartIndex);
    }

    private void DownloadModel(string modelUrl)
    {
        _task = RuntimeGltfImporter.GetImportTask(modelUrl);
        _task.OnProgress = OnProgress;
        _task.OnCompleted = OnComplete;
        _isLoadingModel = true;
    }

    private void OnComplete(GameObject importedModel)
    {
        _model = importedModel;
        Debug.Log("Model import successful.");
        SetupModel(_model);
    }

    private void SetupModel(GameObject model)
    {
        model.transform.parent = Face;
        model.transform.localScale = new Vector3(163f / 12f, 146f / 12f, 146f / 12f);
        model.transform.localPosition = new Vector3(0f, 2f, 4.32f);
        model.transform.localEulerAngles = Vector3.zero;

        BoxCollider collider = model.AddComponent<BoxCollider>();
        collider.center = new Vector3(0f, 0.02f, 0.03f);
        collider.size = new Vector3(0.11f, 0.05f, 0.1f);

        model.AddComponent<DragObject>();
        _currentModel = model;

        LoadingScreen.SetActive(false);
    }

    private void OnProgress(GltfImportStep step, int completed, int total)
    {
        // progress callback if needed
    }

    private void Update()
    {
        if (_isLoadingModel)
        {
            _task.MoveNext();
        }
    }
}
