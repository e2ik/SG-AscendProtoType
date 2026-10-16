using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.Serialization;
using UnityEngine.UI;
using TMPro;

public class KeyPrompt : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private InputActionReference action;
    [SerializeField] private string compositePart;
    [SerializeField] private InputIconDatabase database;

    [Header("Targets (auto-found if empty)")]
    [SerializeField] private Image image;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [FormerlySerializedAs("fallbackLabel")]
    [SerializeField] private TMP_Text label;

    [Header("Behaviour")]
    [SerializeField] private bool showTextWhenNoIcon = true;
    [SerializeField] private bool hideWhenUnbound;

    [Header("Mouse")]
    [SerializeField] private bool clickable = true;
    [SerializeField] private bool triggerActionOnClick = true;
    [SerializeField] private string clickSound = "UIclick";
    [SerializeField] [Min(1f)] private float hoverScale = 1.1f;
    [SerializeField] [Range(0f, 1f)] private float hoverBrighten = 0.2f;
    [SerializeField] private UnityEvent onClicked;

    public InputDeviceType Device { get; private set; }

    private static readonly Dictionary<string, string> ShortNames = new Dictionary<string, string>
    {
        { "escape", "Esc" }, { "space", "Space" }, { "enter", "Enter" }, { "numpadenter", "Enter" },
        { "backspace", "Bksp" }, { "tab", "Tab" }, { "capslock", "Caps" },
        { "leftshift", "Shift" }, { "rightshift", "Shift" }, { "shift", "Shift" },
        { "leftctrl", "Ctrl" }, { "rightctrl", "Ctrl" }, { "ctrl", "Ctrl" },
        { "leftalt", "Alt" }, { "rightalt", "Alt" }, { "alt", "Alt" }, { "altgr", "AltGr" },
        { "leftmeta", "Win" }, { "rightmeta", "Win" }, { "leftwindows", "Win" }, { "rightwindows", "Win" },
        { "leftcommand", "Cmd" }, { "rightcommand", "Cmd" }, { "contextmenu", "Menu" },
        { "delete", "Del" }, { "insert", "Ins" }, { "home", "Home" }, { "end", "End" },
        { "pageup", "PgUp" }, { "pagedown", "PgDn" },
        { "uparrow", "Up" }, { "downarrow", "Down" }, { "leftarrow", "Left" }, { "rightarrow", "Right" },
        { "printscreen", "PrtSc" }, { "scrolllock", "ScrLk" }, { "numlock", "NumLk" }, { "pause", "Pause" },
        { "backquote", "`" }, { "minus", "-" }, { "equals", "=" }, { "leftbracket", "[" }, { "rightbracket", "]" },
        { "backslash", "\\" }, { "semicolon", ";" }, { "quote", "'" }, { "comma", "," }, { "period", "." }, { "slash", "/" },
        { "numpadplus", "Num+" }, { "numpadminus", "Num-" }, { "numpadmultiply", "Num*" }, { "numpaddivide", "Num/" },
        { "numpadperiod", "Num." }, { "numpadequals", "Num=" },
        { "leftbutton", "LMB" }, { "rightbutton", "RMB" }, { "middlebutton", "MMB" },
        { "forwardbutton", "M5" }, { "backbutton", "M4" }, { "scroll", "Scroll" }, { "scroll/up", "Scroll" }, { "scroll/down", "Scroll" }
    };

    private Color _imageBaseColour = Color.white;
    private Color _rendererBaseColour = Color.white;
    private Color _labelBaseColour = Color.black;

    private Vector3 _baseScale = Vector3.one;
    private Color _appliedImageColour = Color.white;
    private Color _appliedRendererColour = Color.white;
    private bool _hovered;

    private void Awake()
    {
        _baseScale = transform.localScale;

        if (image == null && spriteRenderer == null)
        {
            image = GetComponent<Image>();
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        if (image != null) _imageBaseColour = image.color;
        if (spriteRenderer != null) _rendererBaseColour = spriteRenderer.color;
        if (label != null) _labelBaseColour = label.color;
    }

    private void OnEnable()
    {
        var input = InputManager.Instance;
        if (input != null)
            input.OnDeviceChanged += Refresh;

        Refresh(input != null ? input.ActiveDevice : InputDeviceType.KeyboardMouse);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!clickable || eventData.button != PointerEventData.InputButton.Left) return;

        if (!string.IsNullOrEmpty(clickSound) && ASpawner.Instance != null)
            ASpawner.Play(clickSound);

        onClicked?.Invoke();

        if (triggerActionOnClick)
            TriggerAction();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!clickable || !UIManager.HoverAllowed) return;

        _hovered = true;
        transform.localScale = _baseScale * hoverScale;
        ApplyHoverColour();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _hovered = false;
        transform.localScale = _baseScale;
        ApplyHoverColour();
    }

    private void ApplyHoverColour()
    {
        if (image != null)
            image.color = _hovered ? Brighten(_appliedImageColour) : _appliedImageColour;

        if (spriteRenderer != null)
            spriteRenderer.color = _hovered ? Brighten(_appliedRendererColour) : _appliedRendererColour;
    }

    private Color Brighten(Color colour)
    {
        var bright = Color.Lerp(colour, Color.white, hoverBrighten);
        bright.a = colour.a;
        return bright;
    }

    public void TriggerAction() => ActionPresser.Press(ResolveAction(), this);

    private void OnDisable()
    {
        _hovered = false;
        transform.localScale = _baseScale;

        if (InputManager.Instance != null)
            InputManager.Instance.OnDeviceChanged -= Refresh;
    }

    public void SetAction(InputActionReference newAction, string part = null)
    {
        action = newAction;
        compositePart = part;
        Refresh();
    }

    public void Refresh() => Refresh(InputManager.Instance != null ? InputManager.Instance.ActiveDevice : Device);

    private void Refresh(InputDeviceType device)
    {
        Device = device;

        var resolved = ResolveAction();
        string path = null;
        int index = resolved != null ? FindBindingIndex(resolved, device, out path) : -1;
        string text = index >= 0 ? ShortLabel(path, resolved, index) : string.Empty;

        if (device == InputDeviceType.KeyboardMouse)
        {
            if (index < 0)
            {
                Apply(null, string.Empty, false, !hideWhenUnbound);
                return;
            }

            Color keyColour = Color.white;
            Color labelColour = Color.black;
            bool coloured = database != null && database.TryGetKeyboardColour(resolved, compositePart, out keyColour, out labelColour);
            Color? tint = coloured ? keyColour : (Color?)null;
            Color? labelTint = coloured ? labelColour : (Color?)null;

            if (database != null && database.TryGetKeyboardSprite(resolved, compositePart, out var keySprite))
            {
                Apply(keySprite, text, false, true, tint);
                return;
            }

            var blank = database != null ? database.GetBlankKey(text.Length) : null;
            Apply(blank, text, true, true, tint, labelTint);
            return;
        }

        if (database != null && database.TryGetForAction(resolved, compositePart, device, out var actionSprite))
        {
            Apply(actionSprite, text, false, true);
            return;
        }

        if (index < 0)
        {
            Apply(null, string.Empty, false, !hideWhenUnbound);
            return;
        }

        if (database != null && database.TryGetPath(path, out var pathSprite))
        {
            Apply(pathSprite, text, false, true);
            return;
        }

        var fallback = database != null ? database.GetFallback(device) : null;
        Apply(fallback, text, fallback != null || showTextWhenNoIcon, true);
    }

    public static string ShortLabel(string path, InputAction source = null, int bindingIndex = -1)
    {
        if (!string.IsNullOrEmpty(path))
        {
            int close = path.IndexOf('>');
            string control = (close >= 0 ? path.Substring(close + 1) : path).TrimStart('/').ToLowerInvariant();

            if (ShortNames.TryGetValue(control, out var shortName))
                return shortName;

            if (control.Length == 1)
                return control.ToUpperInvariant();

            if (control.StartsWith("numpad") && control.Length == 7 && char.IsDigit(control[6]))
                return "Num" + control[6];

            if (control.Length <= 3 && control[0] == 'f' && int.TryParse(control.Substring(1), out _))
                return control.ToUpperInvariant();
        }

        if (source != null && bindingIndex >= 0)
            return source.GetBindingDisplayString(bindingIndex, InputBinding.DisplayStringOptions.DontIncludeInteractions);

        return string.Empty;
    }

    private static string EffectivePath(InputBinding binding) =>
        string.IsNullOrEmpty(binding.effectivePath) ? binding.path : binding.effectivePath;

    private InputAction ResolveAction()
    {
        if (action == null || action.action == null) return null;

        var source = action.action;
        var input = InputManager.Instance;
        if (input == null || input.PlayerInput == null) return source;

        string actionPath = source.actionMap != null ? $"{source.actionMap.name}/{source.name}" : source.name;
        return input.PlayerInput.actions.FindAction(actionPath) ?? source;
    }

    private int FindBindingIndex(InputAction target, InputDeviceType device, out string resolvedPath)
    {
        var bindings = target.bindings;
        bool wantPart = !string.IsNullOrEmpty(compositePart);
        resolvedPath = null;

        for (int i = 0; i < bindings.Count; i++)
        {
            var binding = bindings[i];
            if (binding.isComposite) continue;
            if (binding.isPartOfComposite != wantPart) continue;
            if (wantPart && !string.Equals(binding.name, compositePart, System.StringComparison.OrdinalIgnoreCase)) continue;

            resolvedPath = ResolvePath(EffectivePath(binding), device);
            if (resolvedPath != null) return i;
        }

        if (!wantPart)
        {
            for (int i = 0; i < bindings.Count; i++)
            {
                var binding = bindings[i];
                if (!binding.isPartOfComposite) continue;

                resolvedPath = ResolvePath(EffectivePath(binding), device);
                if (resolvedPath != null) return i;
            }
        }

        resolvedPath = null;
        return -1;
    }

    private static string ResolvePath(string path, InputDeviceType device)
    {
        if (string.IsNullOrEmpty(path)) return null;

        string layout = InputControlPath.TryGetDeviceLayout(path);

        if (string.IsNullOrEmpty(layout) || layout == "*")
        {
            var candidates = device == InputDeviceType.Gamepad
                ? new InputDevice[] { Gamepad.current }
                : new InputDevice[] { Keyboard.current, Mouse.current };

            foreach (var candidate in candidates)
            {
                if (candidate == null) continue;

                var control = InputControlPath.TryFindControl(candidate, path);
                if (control == null) continue;

                string relative = control.path.Substring(candidate.path.Length);
                string generic = candidate is Gamepad ? "Gamepad" : candidate is Mouse ? "Mouse" : "Keyboard";
                return $"<{generic}>{relative}";
            }

            return null;
        }

        bool isGamepad = layout == "Gamepad" || InputSystem.IsFirstLayoutBasedOnSecond(layout, "Gamepad");
        bool isKeyboardMouse = layout == "Keyboard" || layout == "Mouse"
                               || InputSystem.IsFirstLayoutBasedOnSecond(layout, "Keyboard")
                               || InputSystem.IsFirstLayoutBasedOnSecond(layout, "Mouse");

        bool matches = device == InputDeviceType.Gamepad ? isGamepad : isKeyboardMouse;
        return matches ? path : null;
    }

    private void Apply(Sprite sprite, string text, bool showText, bool visible, Color? tint = null, Color? labelTint = null)
    {
        bool hasSprite = sprite != null;

        if (image != null)
        {
            image.enabled = visible && hasSprite;
            if (hasSprite) image.sprite = sprite;
            _appliedImageColour = tint ?? _imageBaseColour;
            image.color = _hovered ? Brighten(_appliedImageColour) : _appliedImageColour;
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = visible && hasSprite;
            if (hasSprite) spriteRenderer.sprite = sprite;
            _appliedRendererColour = tint ?? _rendererBaseColour;
            spriteRenderer.color = _hovered ? Brighten(_appliedRendererColour) : _appliedRendererColour;
        }

        if (label != null)
        {
            bool show = visible && showText && !string.IsNullOrEmpty(text);
            label.gameObject.SetActive(show);
            if (show) label.text = text;
            label.color = labelTint ?? _labelBaseColour;
        }
    }
}