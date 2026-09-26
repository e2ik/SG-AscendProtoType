using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    private class UILayer
    {
        public Action OnBack;
        public Transform Root;
        public GameObject DefaultSelected;
        public GameObject LastSelected;
        public GameObject PreviousSelection;
        public bool BlockKeyboardNavigation;
    }

    private const float MoveDeadzone = 0.6f;

    [SerializeField] private string uiMapName = "UI";
    [SerializeField] private string backSound = "UIback";

    [Header("Selection Outline Defaults")]
    [SerializeField] private Color selectionHighlightColor = Color.white;
    [SerializeField] private Color selectionChosenColor = Color.yellow;
    [SerializeField] private Color selectionActiveCategoryColor = Color.cyan;
    [SerializeField] private float selectionPulseSpeed = 4f;

    public Color SelectionHighlightColor => selectionHighlightColor;
    public Color SelectionChosenColor => selectionChosenColor;
    public Color SelectionActiveCategoryColor => selectionActiveCategoryColor;
    public float SelectionPulseSpeed => selectionPulseSpeed;

    public event Action OnPauseRequested;
    public bool IsAnyUIOpen => _layers.Count > 0;

    private readonly Dictionary<Type, object> _registry = new Dictionary<Type, object>();
    private readonly Stack<UILayer> _layers = new Stack<UILayer>();
    private readonly List<(InputAction action, int index)> _keyboardNavOverrides = new List<(InputAction, int)>();
    private InputAction _pauseAction;
    private InputAction _cancelAction;
    private InputSystemUIInputModule _uiModule;
    private string _mapBeforeUI;
    private bool _keyboardNavBlocked;
    private int _escapeHandledFrame = -1;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        _pauseAction = InputManager.Instance.FindAction("Pause", this);
        if (_pauseAction != null)
            _pauseAction.performed += OnPausePressed;

        _cancelAction = InputManager.Instance.FindAction($"{uiMapName}/Cancel", this);
        if (_cancelAction != null)
            _cancelAction.performed += OnCancelPressed;
    }

    private void OnDestroy()
    {
        if (_pauseAction != null)
            _pauseAction.performed -= OnPausePressed;
        if (_cancelAction != null)
            _cancelAction.performed -= OnCancelPressed;

        RestoreKeyboardNavigation();
    }

    private void OnPausePressed(InputAction.CallbackContext ctx)
    {
        if (_layers.Count > 0 || _escapeHandledFrame == Time.frameCount) return;
        _escapeHandledFrame = Time.frameCount;
        OnPauseRequested?.Invoke();
    }

    private void OnCancelPressed(InputAction.CallbackContext ctx)
    {
        if (_layers.Count == 0 || _escapeHandledFrame == Time.frameCount) return;
        _escapeHandledFrame = Time.frameCount;

        if (!string.IsNullOrEmpty(backSound) && ASpawner.Instance != null)
            ASpawner.Play(backSound);

        _layers.Peek().OnBack.Invoke();
    }

    private void LateUpdate()
    {
        if (_layers.Count == 0) return;

        var eventSystem = EventSystem.current;
        if (eventSystem == null) return;

        var layer = _layers.Peek();
        var current = eventSystem.currentSelectedGameObject;

        if (IsValidSelection(current, layer.Root))
        {
            layer.LastSelected = current;
            return;
        }

        var target = FindInDirection(layer, ReadMoveInput());
        if (target == null)
            target = FindFallbackSelection(layer);

        if (target != null)
            eventSystem.SetSelectedGameObject(target);
    }

    public void PushBackHandler(Action onBack, Transform root, GameObject defaultSelected = null, bool blockKeyboardNavigation = false)
    {
        if (_layers.Count == 0)
        {
            _mapBeforeUI = InputManager.Instance.CurrentMap;
            InputManager.Instance.SwitchMap(uiMapName);
        }

        var eventSystem = EventSystem.current;

        var layer = new UILayer
        {
            OnBack = onBack,
            Root = root,
            DefaultSelected = defaultSelected,
            PreviousSelection = eventSystem != null ? eventSystem.currentSelectedGameObject : null,
            BlockKeyboardNavigation = blockKeyboardNavigation
        };

        _layers.Push(layer);
        RefreshKeyboardNavigation();

        var initial = FindFallbackSelection(layer);
        if (eventSystem != null && initial != null)
            eventSystem.SetSelectedGameObject(initial);
    }

    public void PopBackHandler(Action onBack)
    {
        if (_layers.Count == 0 || _layers.Peek().OnBack != onBack) return;

        var layer = _layers.Pop();
        RefreshKeyboardNavigation();

        var eventSystem = EventSystem.current;
        if (eventSystem != null)
        {
            var restore = layer.PreviousSelection;
            if (_layers.Count > 0 && !IsValidSelection(restore, _layers.Peek().Root))
                restore = FindFallbackSelection(_layers.Peek());

            eventSystem.SetSelectedGameObject(restore != null ? restore : null);
        }

        if (_layers.Count > 0) return;

        var input = InputManager.Instance;
        if (input != null && _mapBeforeUI != null && input.CurrentMap == uiMapName)
            input.SwitchMap(_mapBeforeUI);

        _mapBeforeUI = null;
    }

    private static bool IsValidSelection(GameObject candidate, Transform root)
    {
        if (candidate == null || root == null || !candidate.activeInHierarchy) return false;
        if (!candidate.transform.IsChildOf(root)) return false;

        var selectable = candidate.GetComponent<Selectable>();
        return selectable != null && selectable.IsInteractable();
    }

    private static GameObject FindFallbackSelection(UILayer layer)
    {
        if (IsValidSelection(layer.LastSelected, layer.Root)) return layer.LastSelected;
        if (IsValidSelection(layer.DefaultSelected, layer.Root)) return layer.DefaultSelected;
        if (layer.Root == null) return null;

        foreach (var selectable in layer.Root.GetComponentsInChildren<Selectable>())
        {
            if (selectable.IsInteractable())
                return selectable.gameObject;
        }

        return null;
    }

    private Vector2 ReadMoveInput()
    {
        var module = UIModule;
        if (module == null || module.move == null || module.move.action == null) return Vector2.zero;

        var input = module.move.action.ReadValue<Vector2>();
        return input.magnitude >= MoveDeadzone ? input : Vector2.zero;
    }

    private static GameObject FindInDirection(UILayer layer, Vector2 input)
    {
        if (input == Vector2.zero || !IsValidSelection(layer.LastSelected, layer.Root)) return null;

        var from = layer.LastSelected.transform;
        var axis = Mathf.Abs(input.x) > Mathf.Abs(input.y)
            ? new Vector3(Mathf.Sign(input.x), 0f, 0f)
            : new Vector3(0f, Mathf.Sign(input.y), 0f);

        var dir = from.rotation * axis;
        var origin = from.TransformPoint(GetPointOnRectEdge(from as RectTransform, axis));

        GameObject best = null;
        float bestScore = 0f;

        foreach (var candidate in layer.Root.GetComponentsInChildren<Selectable>())
        {
            if (candidate.gameObject == layer.LastSelected) continue;
            if (!candidate.IsInteractable() || candidate.navigation.mode == Navigation.Mode.None) continue;

            var candidateRect = candidate.transform as RectTransform;
            var candidateCenter = candidateRect != null ? (Vector3)candidateRect.rect.center : Vector3.zero;
            var toCandidate = candidate.transform.TransformPoint(candidateCenter) - origin;

            float dot = Vector3.Dot(dir, toCandidate);
            if (dot <= 0f) continue;

            float score = dot / toCandidate.sqrMagnitude;
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate.gameObject;
            }
        }

        return best;
    }

    private static Vector3 GetPointOnRectEdge(RectTransform rect, Vector2 dir)
    {
        if (rect == null) return Vector3.zero;
        if (dir != Vector2.zero)
            dir /= Mathf.Max(Mathf.Abs(dir.x), Mathf.Abs(dir.y));
        return rect.rect.center + Vector2.Scale(rect.rect.size, dir * 0.5f);
    }

    private InputSystemUIInputModule UIModule
    {
        get
        {
            if (_uiModule == null && EventSystem.current != null)
                _uiModule = EventSystem.current.GetComponent<InputSystemUIInputModule>();
            return _uiModule;
        }
    }

    private void RefreshKeyboardNavigation()
    {
        bool shouldBlock = _layers.Count > 0 && _layers.Peek().BlockKeyboardNavigation;
        if (shouldBlock == _keyboardNavBlocked) return;

        if (!shouldBlock)
        {
            RestoreKeyboardNavigation();
            return;
        }

        var module = UIModule;
        if (module == null) return;

        BlockKeyboardBindings(module.move != null ? module.move.action : null);
        BlockKeyboardBindings(module.submit != null ? module.submit.action : null);
        _keyboardNavBlocked = true;
    }

    private void BlockKeyboardBindings(InputAction action)
    {
        if (action == null) return;

        for (int i = 0; i < action.bindings.Count; i++)
        {
            var binding = action.bindings[i];
            if (binding.isComposite || string.IsNullOrEmpty(binding.path)) continue;

            if (binding.path.StartsWith("<Keyboard>"))
                action.ApplyBindingOverride(i, string.Empty);
            else if (binding.path.StartsWith("*/"))
                action.ApplyBindingOverride(i, "<Gamepad>/" + binding.path.Substring(2));
            else
                continue;

            _keyboardNavOverrides.Add((action, i));
        }
    }

    private void RestoreKeyboardNavigation()
    {
        foreach (var (action, index) in _keyboardNavOverrides)
            action.RemoveBindingOverride(index);

        _keyboardNavOverrides.Clear();
        _keyboardNavBlocked = false;
    }

    public void Register<T>(T ui) where T : class => _registry[typeof(T)] = ui;

    public void Unregister<T>(T ui) where T : class
    {
        if (_registry.TryGetValue(typeof(T), out var existing) && ReferenceEquals(existing, ui))
            _registry.Remove(typeof(T));
    }

    public bool TryGet<T>(out T ui) where T : class
    {
        if (_registry.TryGetValue(typeof(T), out var found))
        {
            ui = found as T;
            return ui != null;
        }
        ui = null;
        return false;
    }
}