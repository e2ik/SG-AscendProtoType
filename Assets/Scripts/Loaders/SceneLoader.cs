using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance { get; private set; }

    [Header("Optional transition UI")]
    [SerializeField] private CanvasGroup fadeCanvas;
    [SerializeField] private float fadeDuration = 0.3f;

    public bool IsTransitioning { get; private set; }

    private readonly List<string> _loadedScenes = new List<string>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void TransitionTo(string sceneName, Action onComplete = null)
    {
        if (IsTransitioning)
        {
            Debug.LogWarning($"SceneLoader busy, ignoring request to load {sceneName}");
            return;
        }
        StartCoroutine(TransitionRoutine(sceneName, onComplete));
    }

    public void LoadAdditive(string sceneName, Action onComplete = null)
    {
        if (_loadedScenes.Contains(sceneName))
        {
            Debug.LogWarning($"{sceneName} already loaded");
            onComplete?.Invoke();
            return;
        }
        StartCoroutine(LoadAdditiveRoutine(sceneName, onComplete));
    }

    public void UnloadScene(string sceneName, Action onComplete = null)
    {
        if (!_loadedScenes.Contains(sceneName))
        {
            onComplete?.Invoke();
            return;
        }
        StartCoroutine(UnloadRoutine(sceneName, onComplete));
    }

    private IEnumerator TransitionRoutine(string sceneName, Action onComplete)
    {
        IsTransitioning = true;

        yield return Fade(1f);

        foreach (var loaded in new List<string>(_loadedScenes))
        {
            var unloadOp = SceneManager.UnloadSceneAsync(loaded);
            while (unloadOp != null && !unloadOp.isDone) yield return null;
        }
        _loadedScenes.Clear();

        yield return LoadAdditiveRoutine(sceneName, null);
        yield return Fade(0f);

        IsTransitioning = false;
        onComplete?.Invoke();
    }

    private IEnumerator LoadAdditiveRoutine(string sceneName, Action onComplete)
    {
        var loadOp = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        if (loadOp == null)
        {
            Debug.LogError($"Could not load scene '{sceneName}' - is it added to Build Settings?");
            yield break;
        }

        while (!loadOp.isDone) yield return null;

        _loadedScenes.Add(sceneName);
        SetActiveScene(sceneName);
        onComplete?.Invoke();
    }

    private IEnumerator UnloadRoutine(string sceneName, Action onComplete)
    {
        var unloadOp = SceneManager.UnloadSceneAsync(sceneName);
        while (unloadOp != null && !unloadOp.isDone) yield return null;

        _loadedScenes.Remove(sceneName);

        if (_loadedScenes.Count > 0)
            SetActiveScene(_loadedScenes[_loadedScenes.Count - 1]);

        onComplete?.Invoke();
    }

    private static void SetActiveScene(string sceneName)
    {
        var scene = SceneManager.GetSceneByName(sceneName);
        if (scene.IsValid())
            SceneManager.SetActiveScene(scene);
    }

    private IEnumerator Fade(float targetAlpha)
    {
        if (fadeCanvas == null) yield break;

        float start = fadeCanvas.alpha;
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            fadeCanvas.alpha = Mathf.Lerp(start, targetAlpha, t / fadeDuration);
            yield return null;
        }
        fadeCanvas.alpha = targetAlpha;
    }
}