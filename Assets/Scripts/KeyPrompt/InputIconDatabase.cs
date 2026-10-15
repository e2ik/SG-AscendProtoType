using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[CreateAssetMenu(fileName = "InputIconDatabase", menuName = "Input/Input Icon Database")]
public class InputIconDatabase : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public string controlPath;
        public Sprite sprite;
    }

    [Serializable]
    public class ActionEntry
    {
        public InputActionReference action;
        public string compositePart;
        public bool overrideKeyboardColour;
        public Color keyboardColour = Color.white;
        public Color keyboardLabelColour = Color.black;
        public bool overrideKeyboardSprite;
        public Sprite keyboardSprite;
        public Sprite gamepadSprite;
    }

    [Serializable]
    public class BlankKey
    {
        [Min(1)] public int maxCharacters = 1;
        public Sprite sprite;
    }

    [SerializeField] private List<BlankKey> blankKeys = new List<BlankKey>();
    [SerializeField] private Sprite keyboardFallback;
    [SerializeField] private Sprite gamepadFallback;
    [SerializeField] private List<ActionEntry> actions = new List<ActionEntry>();
    [SerializeField] private List<Entry> keyboardMouse = new List<Entry>();
    [SerializeField] private List<Entry> gamepad = new List<Entry>();

    private Dictionary<string, Sprite> _lookup;

    private void OnEnable() => _lookup = null;
    private void OnValidate() => _lookup = null;

    public bool TryGetForAction(InputAction target, string compositePart, InputDeviceType device, out Sprite sprite)
    {
        var entry = FindEntry(target, compositePart);
        sprite = entry != null && device == InputDeviceType.Gamepad ? entry.gamepadSprite : null;
        return sprite != null;
    }

    public bool TryGetKeyboardColour(InputAction target, string compositePart, out Color colour, out Color labelColour)
    {
        var entry = FindEntry(target, compositePart);
        colour = entry != null ? entry.keyboardColour : Color.white;
        labelColour = entry != null ? entry.keyboardLabelColour : Color.black;
        return entry != null && entry.overrideKeyboardColour;
    }

    public bool TryGetKeyboardSprite(InputAction target, string compositePart, out Sprite sprite)
    {
        var entry = FindEntry(target, compositePart);
        sprite = entry != null && entry.overrideKeyboardSprite ? entry.keyboardSprite : null;
        return sprite != null;
    }

    private ActionEntry FindEntry(InputAction target, string compositePart)
    {
        if (target == null) return null;

        foreach (var entry in actions)
        {
            if (entry == null || entry.action == null || entry.action.action == null) continue;
            if (!SameAction(entry.action.action, target)) continue;
            if (!string.Equals(entry.compositePart ?? string.Empty, compositePart ?? string.Empty, StringComparison.OrdinalIgnoreCase)) continue;
            return entry;
        }

        return null;
    }

    private static bool SameAction(InputAction a, InputAction b)
    {
        if (a == b) return true;
        string mapA = a.actionMap != null ? a.actionMap.name : string.Empty;
        string mapB = b.actionMap != null ? b.actionMap.name : string.Empty;
        return a.name == b.name && mapA == mapB;
    }

    public bool TryGetPath(string controlPath, out Sprite sprite)
    {
        if (_lookup == null) BuildLookup();

        sprite = null;
        return !string.IsNullOrEmpty(controlPath) && _lookup.TryGetValue(Normalise(controlPath), out sprite) && sprite != null;
    }

    public Sprite GetFallback(InputDeviceType device) => device == InputDeviceType.Gamepad ? gamepadFallback : keyboardFallback;

    public Sprite GetBlankKey(int characters)
    {
        BlankKey best = null;
        BlankKey largest = null;

        foreach (var key in blankKeys)
        {
            if (key == null || key.sprite == null) continue;

            if (largest == null || key.maxCharacters > largest.maxCharacters)
                largest = key;

            if (key.maxCharacters >= characters && (best == null || key.maxCharacters < best.maxCharacters))
                best = key;
        }

        var chosen = best ?? largest;
        return chosen != null ? chosen.sprite : null;
    }

    public Sprite Get(string controlPath, InputDeviceType device)
    {
        if (_lookup == null) BuildLookup();

        if (!string.IsNullOrEmpty(controlPath) && _lookup.TryGetValue(Normalise(controlPath), out var sprite) && sprite != null)
            return sprite;

        return device == InputDeviceType.Gamepad ? gamepadFallback : keyboardFallback;
    }

    private void BuildLookup()
    {
        _lookup = new Dictionary<string, Sprite>();
        Add(keyboardMouse);
        Add(gamepad);
    }

    private void Add(List<Entry> entries)
    {
        foreach (var entry in entries)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.controlPath)) continue;
            _lookup[Normalise(entry.controlPath)] = entry.sprite;
        }
    }

    public static string Normalise(string path)
    {
        if (string.IsNullOrEmpty(path)) return string.Empty;

        string trimmed = path.Trim().ToLowerInvariant();
        int close = trimmed.IndexOf('>');
        if (trimmed.StartsWith("<") && close > 0)
        {
            string layout = trimmed.Substring(1, close - 1);
            string control = trimmed.Substring(close + 1);

            if (layout != "keyboard" && layout != "mouse")
                layout = "gamepad";

            trimmed = $"<{layout}>{control}";
        }

        return trimmed;
    }

    [ContextMenu("Fill Common Paths")]
    private void FillCommonPaths()
    {
        string[] keys =
        {
            "<Keyboard>/space", "<Keyboard>/enter", "<Keyboard>/escape", "<Keyboard>/backspace", "<Keyboard>/tab",
            "<Keyboard>/leftShift", "<Keyboard>/leftCtrl", "<Keyboard>/leftAlt",
            "<Keyboard>/upArrow", "<Keyboard>/downArrow", "<Keyboard>/leftArrow", "<Keyboard>/rightArrow",
            "<Keyboard>/w", "<Keyboard>/a", "<Keyboard>/s", "<Keyboard>/d",
            "<Keyboard>/e", "<Keyboard>/f", "<Keyboard>/q", "<Keyboard>/r", "<Keyboard>/i", "<Keyboard>/m",
            "<Keyboard>/1", "<Keyboard>/2", "<Keyboard>/3", "<Keyboard>/4",
            "<Mouse>/leftButton", "<Mouse>/rightButton", "<Mouse>/middleButton", "<Mouse>/scroll"
        };

        string[] pad =
        {
            "<Gamepad>/buttonSouth", "<Gamepad>/buttonEast", "<Gamepad>/buttonWest", "<Gamepad>/buttonNorth",
            "<Gamepad>/leftShoulder", "<Gamepad>/rightShoulder", "<Gamepad>/leftTrigger", "<Gamepad>/rightTrigger",
            "<Gamepad>/start", "<Gamepad>/select",
            "<Gamepad>/dpad/up", "<Gamepad>/dpad/down", "<Gamepad>/dpad/left", "<Gamepad>/dpad/right",
            "<Gamepad>/leftStick", "<Gamepad>/rightStick", "<Gamepad>/leftStickPress", "<Gamepad>/rightStickPress"
        };

        AddMissing(keyboardMouse, keys);
        AddMissing(gamepad, pad);
        _lookup = null;
    }

    private static void AddMissing(List<Entry> entries, string[] paths)
    {
        foreach (var path in paths)
        {
            if (entries.Exists(e => e != null && Normalise(e.controlPath) == Normalise(path))) continue;
            entries.Add(new Entry { controlPath = path });
        }
    }
}