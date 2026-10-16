using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

public class OnScreenKeyboardController : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text previewText;
    [SerializeField] private RectTransform keysParent;
    [SerializeField] private Vector2 cellSize = new Vector2(50f, 50f);
    [SerializeField] private Vector2 spacing = new Vector2(4f, 4f);
    [SerializeField] private Button keyButtonPrefab;
    [SerializeField] private GameObject fillerPrefab;
    [SerializeField] [Range(0f, 1f)] private float fillerAlpha;
    [SerializeField] private Button spaceButton;
    [SerializeField] private Button backspaceButton;
    [SerializeField] private Button doneButton;
    [SerializeField] private Button caseToggleButton;
    [SerializeField] private Color caseActiveColor = Color.yellow;
    [SerializeField] private Color caseInactiveColor = Color.white;
    [SerializeField] private int maxLength = 16;
    [SerializeField] private string placeholderText = "What's your name?";
    [SerializeField] private float cursorBlinkInterval = 0.5f;
    [SerializeField] private float shiftTapMaxDuration = 0.3f;

    [SerializeField] private bool keyboardSelectsOnNavigate = true;

    [Header("Key Press Flash")]
    [SerializeField] private bool flashKeysOnType = true;
    [SerializeField] [Min(0f)] private float flashDuration = 0.1f;
    [SerializeField] [Min(0f)] private float flashScale = 0.92f;

    [Header("Gamepad")]
    [SerializeField] private bool gamepadStartsShifted = true;
    [SerializeField] private bool gamepadShiftOffAfterTyping = true;
    [SerializeField] private bool gamepadNorthTogglesShift = true;
    [SerializeField] private bool gamepadWestBackspace = true;

    private const int Columns = 10;

    private static readonly string[] KeyRows =
    {
        "1234567890",
        "QWERTYUIOP",
        "ASDFGHJKL",
        "ZXCVBNM"
    };

    private readonly StringBuilder _text = new StringBuilder();
    private readonly List<(TMP_Text label, char baseChar)> _letterLabels = new List<(TMP_Text, char)>();
    private readonly Dictionary<char, Button> _keyButtons = new Dictionary<char, Button>();
    private readonly Dictionary<Button, Coroutine> _flashRoutines = new Dictionary<Button, Coroutine>();
    private readonly Dictionary<Button, Vector3> _flashBaseScales = new Dictionary<Button, Vector3>();
    private Action<string> _onConfirm;
    private Func<string, string> _displayFilter;
    private bool _built;
    private UIPanelAnimator _panelAnimator;
    private bool _capsLock = true;
    private bool _shiftHeld;
    private bool _typedDuringShift;
    private float _shiftDownTime;
    private int _openedFrame = -1;
    private string _initialText;
    private bool _hasTyped;
    private bool _cursorVisible = true;
    private float _nextBlinkTime;

    private void Awake()
    {
        panel.SetActive(false);
        _panelAnimator = panel.GetComponent<UIPanelAnimator>();

        var uiPanel = panel.GetComponent<UIPanel>();
        if (uiPanel != null)
            uiPanel.KeyboardSelectsOnNavigate = keyboardSelectsOnNavigate;
        else
            Debug.LogWarning($"OnScreenKeyboardController on '{name}' has no UIPanel on its panel", this);
        spaceButton.onClick.AddListener(() => AppendChar(' '));
        backspaceButton.onClick.AddListener(Backspace);
        doneButton.onClick.AddListener(Confirm);
        caseToggleButton.onClick.AddListener(ToggleCase);
        RefreshCaseVisuals();
    }

    private void Start()
    {
        if (InputManager.Instance != null)
            InputManager.Instance.OnDeviceChanged += OnDeviceChanged;
    }

    private void OnDestroy()
    {
        if (InputManager.Instance != null)
            InputManager.Instance.OnDeviceChanged -= OnDeviceChanged;
    }

    private static bool UsingGamepad => InputManager.Instance != null && InputManager.Instance.ActiveDevice == InputDeviceType.Gamepad;

    private bool IsOpen => panel.activeSelf && (_panelAnimator == null || !_panelAnimator.IsHiding);

    private void OnDeviceChanged(InputDeviceType device)
    {
        if (!IsOpen) return;

        if (device == InputDeviceType.KeyboardMouse)
            _capsLock = false;
        else if (gamepadStartsShifted && _text.Length == 0)
            _capsLock = true;

        RefreshCaseVisuals();
    }

    private bool IsUppercase => _capsLock != _shiftHeld;

    public void Open(string initialText, Action<string> onConfirm) => Open(initialText, null, onConfirm);

    public void Open(string initialText, Func<string, string> displayFilter, Action<string> onConfirm)
    {
        _onConfirm = onConfirm;
        _displayFilter = displayFilter;
        _initialText = initialText;
        _text.Clear();
        _hasTyped = false;
        _shiftHeld = false;
        _typedDuringShift = false;
        _capsLock = gamepadStartsShifted && UsingGamepad;
        RefreshCaseVisuals();
        UpdatePreview();

        if (!_built)
            BuildKeys();

        _openedFrame = Time.frameCount;
        UIPanelAnimator.SetVisible(panel, true);
    }

    private void BuildKeys()
    {
        ClearPreview();
        SpawnKeys(false);
        _built = true;
        RefreshCaseVisuals();
    }

    private void ConfigureGrid()
    {
        if (keysParent == null || !keysParent.TryGetComponent<GridLayoutGroup>(out var grid)) return;

        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = Columns;
        grid.cellSize = cellSize;
        grid.spacing = spacing;
    }

    private void SpawnKeys(bool preview)
    {
        ConfigureGrid();

        foreach (var row in KeyRows)
        {
            bool isLetterRow = char.IsLetter(row[0]);

            foreach (char c in row)
            {
                var button = Instantiate(keyButtonPrefab, keysParent);
                var label = button.GetComponentInChildren<TMP_Text>();
                if (label != null)
                    label.text = c.ToString();

                if (preview)
                {
                    MarkAsPreview(button.gameObject);
                    continue;
                }

                _keyButtons[char.ToUpperInvariant(c)] = button;

                if (isLetterRow)
                {
                    char baseChar = c;
                    if (label != null)
                        _letterLabels.Add((label, baseChar));
                    button.onClick.AddListener(() => AppendChar(IsUppercase ? char.ToUpper(baseChar) : char.ToLower(baseChar)));
                }
                else
                {
                    char fixedChar = c;
                    button.onClick.AddListener(() => AppendChar(fixedChar));
                }
            }

            for (int i = row.Length; i < Columns; i++)
            {
                var filler = Instantiate(fillerPrefab, keysParent);
                filler.SetActive(true);
                ApplyFillerAlpha(filler);

                if (preview)
                    MarkAsPreview(filler);
            }
        }
    }

    private void ApplyFillerAlpha(GameObject filler)
    {
        if (!filler.TryGetComponent<CanvasGroup>(out var group))
            group = filler.AddComponent<CanvasGroup>();

        group.alpha = fillerAlpha;
        group.interactable = false;
        group.blocksRaycasts = false;
    }

    [ContextMenu("Preview Keys")]
    private void PreviewKeys()
    {
        if (keysParent == null || keyButtonPrefab == null || fillerPrefab == null)
        {
            Debug.LogWarning("Assign Keys Parent, Key Button Prefab and Filler Prefab before previewing", this);
            return;
        }

        ClearPreview();
        SpawnKeys(true);
    }

    [ContextMenu("Clear Preview")]
    private void ClearPreview()
    {
        if (keysParent == null) return;

        for (int i = keysParent.childCount - 1; i >= 0; i--)
        {
            var child = keysParent.GetChild(i).gameObject;
            if ((child.hideFlags & HideFlags.DontSave) == 0) continue;

            if (Application.isPlaying)
                Destroy(child);
            else
                DestroyImmediate(child);
        }
    }

    private static void MarkAsPreview(GameObject root)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            t.gameObject.hideFlags = HideFlags.DontSave;
    }

    private void OnValidate()
    {
        ConfigureGrid();
    }

    private void Update()
    {
        if (!IsOpen) return;

        if (_hasTyped && Time.unscaledTime >= _nextBlinkTime)
        {
            _cursorVisible = !_cursorVisible;
            _nextBlinkTime = Time.unscaledTime + cursorBlinkInterval;
            UpdatePreview();
        }

        if (Time.frameCount == _openedFrame) return;

        UpdateShift();
        UpdateGamepad();

        for (KeyCode key = KeyCode.A; key <= KeyCode.Z; key++)
        {
            if (Input.GetKeyDown(key))
            {
                char c = (char)('A' + (key - KeyCode.A));
                PressPhysicalKey(KeyFor(c));
                AppendChar(IsUppercase ? c : char.ToLower(c));
            }
        }

        for (KeyCode key = KeyCode.Alpha0; key <= KeyCode.Alpha9; key++)
        {
            if (Input.GetKeyDown(key))
            {
                char c = (char)('0' + (key - KeyCode.Alpha0));
                PressPhysicalKey(KeyFor(c));
                AppendChar(c);
            }
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            PressPhysicalKey(spaceButton);
            AppendChar(' ');
        }

        if (Input.GetKeyDown(KeyCode.Backspace))
        {
            PressPhysicalKey(backspaceButton);
            Backspace();
        }

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            PressPhysicalKey(doneButton);
            Confirm();
        }
    }

    private void UpdateShift()
    {
        if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift))
        {
            _shiftDownTime = Time.unscaledTime;
            _typedDuringShift = false;
            PressPhysicalKey(caseToggleButton);
        }

        bool held = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        if (held == _shiftHeld) return;

        bool released = _shiftHeld && !held;
        _shiftHeld = held;

        if (released && !_typedDuringShift && Time.unscaledTime - _shiftDownTime <= shiftTapMaxDuration)
            _capsLock = !_capsLock;

        RefreshCaseVisuals();
    }

    private void UpdateGamepad()
    {
        var pad = Gamepad.current;
        if (pad == null) return;

        if (gamepadNorthTogglesShift && pad.buttonNorth.wasPressedThisFrame)
        {
            Flash(caseToggleButton);
            ToggleCase();
        }

        if (gamepadWestBackspace && pad.buttonWest.wasPressedThisFrame)
        {
            Flash(backspaceButton);
            Backspace();
        }
    }

    private void PressPhysicalKey(Button button)
    {
        Flash(button);

        if (UsingGamepad || EventSystem.current == null) return;

        var selected = EventSystem.current.currentSelectedGameObject;
        if (selected != null && selected.transform.IsChildOf(panel.transform))
            EventSystem.current.SetSelectedGameObject(null);
    }

    private Button KeyFor(char c) => _keyButtons.TryGetValue(char.ToUpperInvariant(c), out var button) ? button : null;

    private void Flash(Button button)
    {
        if (!flashKeysOnType || button == null || !isActiveAndEnabled) return;

        if (_flashRoutines.TryGetValue(button, out var running) && running != null)
            StopCoroutine(running);

        if (!_flashBaseScales.ContainsKey(button))
            _flashBaseScales[button] = button.transform.localScale;

        _flashRoutines[button] = StartCoroutine(FlashRoutine(button));
    }

    private IEnumerator FlashRoutine(Button button)
    {
        var colours = button.colors;
        var graphic = button.targetGraphic;
        bool tint = graphic != null && button.transition == Selectable.Transition.ColorTint;
        var baseScale = _flashBaseScales[button];

        if (tint)
            graphic.CrossFadeColor(colours.pressedColor * colours.colorMultiplier, 0f, true, true);
        button.transform.localScale = baseScale * flashScale;

        float end = Time.unscaledTime + flashDuration;
        while (Time.unscaledTime < end)
            yield return null;

        button.transform.localScale = baseScale;

        if (tint)
        {
            bool selected = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == button.gameObject;
            var rest = button.interactable ? (selected ? colours.selectedColor : colours.normalColor) : colours.disabledColor;
            graphic.CrossFadeColor(rest * colours.colorMultiplier, colours.fadeDuration, true, true);
        }

        _flashRoutines[button] = null;
    }

    private void ToggleCase()
    {
        _capsLock = !_capsLock;
        RefreshCaseVisuals();
    }

    private void RefreshCaseVisuals()
    {
        bool upper = IsUppercase;
        caseToggleButton.image.color = upper ? caseActiveColor : caseInactiveColor;

        foreach (var (label, baseChar) in _letterLabels)
            label.text = upper ? char.ToUpper(baseChar).ToString() : char.ToLower(baseChar).ToString();
    }

    private void AppendChar(char c)
    {
        RegisterKeystroke();
        if (_text.Length < maxLength)
            _text.Append(c);

        if (gamepadShiftOffAfterTyping && _capsLock && UsingGamepad)
        {
            _capsLock = false;
            RefreshCaseVisuals();
        }

        UpdatePreview();
    }

    private void Backspace()
    {
        RegisterKeystroke();
        if (_text.Length > 0)
            _text.Remove(_text.Length - 1, 1);
        UpdatePreview();
    }

    private void RegisterKeystroke()
    {
        if (_shiftHeld)
            _typedDuringShift = true;

        _hasTyped = true;
        _cursorVisible = true;
        _nextBlinkTime = Time.unscaledTime + cursorBlinkInterval;
    }

    private void UpdatePreview()
    {
        if (!_hasTyped)
        {
            previewText.text = placeholderText;
            return;
        }

        string typed = _text.ToString();
        if (_displayFilter != null)
            typed = _displayFilter(typed);

        previewText.text = typed + (_cursorVisible ? "_" : "<alpha=#00>_");
    }

    private void Confirm()
    {
        UIPanelAnimator.SetVisible(panel, false);
        _onConfirm?.Invoke(_hasTyped ? _text.ToString() : _initialText);
    }
}