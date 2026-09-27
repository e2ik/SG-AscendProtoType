using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance { get; private set; }

    [SerializeField] private CanvasGroup fadeCanvas;
    [SerializeField] private float fadeDuration = 0.3f;
    [SerializeField] private bool startBlack = true;

    private readonly List<string> _loadedScenes = new List<string>();
    private readonly Dictionary<string, List<GameObject>> _hiddenRoots = new Dictionary<string, List<GameObject>>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (fadeCanvas != null)
            SetFade(startBlack ? 1f : 0f);
    }

    public IEnumerator FadeOut() => Fade(1f);
    public IEnumerator FadeIn() => Fade(0f);

    public IEnumerator ReplaceAll(string sceneName)
    {
        foreach (var loaded in new List<string>(_loadedScenes))
            yield return Unload(loaded);

        yield return LoadAdditive(sceneName);
    }

    public IEnumerator LoadAdditive(string sceneName)
    {
        if (_loadedScenes.Contains(sceneName))
        {
            Debug.LogWarning($"{sceneName} already loaded");
            yield break;
        }

        var loadOp = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        if (loadOp == null)
        {
            Debug.LogError($"Could not load scene '{sceneName}' - is it added to Build Settings?");
            yield break;
        }

        while (!loadOp.isDone) yield return null;

        _loadedScenes.Add(sceneName);
        SetActiveScene(sceneName);
    }

    public IEnumerator Unload(string sceneName)
    {
        if (!_loadedScenes.Contains(sceneName)) yield break;

        var unloadOp = SceneManager.UnloadSceneAsync(sceneName);
        while (unloadOp != null && !unloadOp.isDone) yield return null;

        _loadedScenes.Remove(sceneName);
        _hiddenRoots.Remove(sceneName);

        if (_loadedScenes.Count > 0)
            SetActiveScene(_loadedScenes[_loadedScenes.Count - 1]);
    }

    public void HideScene(string sceneName)
    {
        if (_hiddenRoots.ContainsKey(sceneName)) return;

        var scene = SceneManager.GetSceneByName(sceneName);
        if (!scene.IsValid() || !scene.isLoaded) return;

        var hidden = new List<GameObject>();
        foreach (var root in scene.GetRootGameObjects())
        {
            if (!root.activeSelf) continue;
            root.SetActive(false);
            hidden.Add(root);
        }

        _hiddenRoots[sceneName] = hidden;
    }

    public void ShowScene(string sceneName)
    {
        if (!_hiddenRoots.TryGetValue(sceneName, out var hidden)) return;

        foreach (var root in hidden)
        {
            if (root != null)
                root.SetActive(true);
        }

        _hiddenRoots.Remove(sceneName);
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

        fadeCanvas.gameObject.SetActive(true);
        fadeCanvas.blocksRaycasts = true;

        float start = fadeCanvas.alpha;
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            fadeCanvas.alpha = Mathf.Lerp(start, targetAlpha, t / fadeDuration);
            yield return null;
        }

        SetFade(targetAlpha);
    }

    private void SetFade(float alpha)
    {
        fadeCanvas.alpha = alpha;
        fadeCanvas.blocksRaycasts = alpha > 0f;
        fadeCanvas.interactable = false;
        fadeCanvas.gameObject.SetActive(alpha > 0f);
    }
}