using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
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
    private Action<string> _onConfirm;
    private bool _built;
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
        spaceButton.onClick.AddListener(() => AppendChar(' '));
        backspaceButton.onClick.AddListener(Backspace);
        doneButton.onClick.AddListener(Confirm);
        caseToggleButton.onClick.AddListener(ToggleCase);
        RefreshCaseVisuals();
    }

    private bool IsUppercase => _capsLock != _shiftHeld;

    public void Open(string initialText, Action<string> onConfirm)
    {
        _onConfirm = onConfirm;
        _initialText = initialText;
        _text.Clear();
        _hasTyped = false;
        _shiftHeld = false;
        _typedDuringShift = false;
        UpdatePreview();

        if (!_built)
            BuildKeys();

        _openedFrame = Time.frameCount;
        panel.SetActive(true);
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

                if (preview)
                    MarkAsPreview(filler);
            }
        }
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
        if (!panel.activeSelf) return;

        if (_hasTyped && Time.unscaledTime >= _nextBlinkTime)
        {
            _cursorVisible = !_cursorVisible;
            _nextBlinkTime = Time.unscaledTime + cursorBlinkInterval;
            UpdatePreview();
        }

        if (Time.frameCount == _openedFrame) return;

        UpdateShift();

        for (KeyCode key = KeyCode.A; key <= KeyCode.Z; key++)
        {
            if (Input.GetKeyDown(key))
            {
                char c = (char)('A' + (key - KeyCode.A));
                AppendChar(IsUppercase ? c : char.ToLower(c));
            }
        }

        for (KeyCode key = KeyCode.Alpha0; key <= KeyCode.Alpha9; key++)
        {
            if (Input.GetKeyDown(key))
            {
                char c = (char)('0' + (key - KeyCode.Alpha0));
                AppendChar(c);
            }
        }

        if (Input.GetKeyDown(KeyCode.Space))
            AppendChar(' ');

        if (Input.GetKeyDown(KeyCode.Backspace))
            Backspace();

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            Confirm();
    }

    private void UpdateShift()
    {
        if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift))
        {
            _shiftDownTime = Time.unscaledTime;
            _typedDuringShift = false;
        }

        bool held = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        if (held == _shiftHeld) return;

        bool released = _shiftHeld && !held;
        _shiftHeld = held;

        if (released && !_typedDuringShift && Time.unscaledTime - _shiftDownTime <= shiftTapMaxDuration)
            _capsLock = !_capsLock;

        RefreshCaseVisuals();
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

        previewText.text = _text + (_cursorVisible ? "_" : "<alpha=#00>_");
    }

    private void Confirm()
    {
        panel.SetActive(false);
        _onConfirm?.Invoke(_hasTyped ? _text.ToString() : _initialText);
    }
}