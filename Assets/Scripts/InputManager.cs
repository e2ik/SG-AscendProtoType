using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

public enum InputDeviceType
{
    KeyboardMouse,
    Gamepad
}

public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }

    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private string globalMapName = "Global";

    public PlayerInput PlayerInput => playerInput;
    public string CurrentMap { get; private set; }
    public event Action<string> OnMapChanged;

    public InputDeviceType ActiveDevice { get; private set; } = InputDeviceType.KeyboardMouse;
    public event Action<InputDeviceType> OnDeviceChanged;

    private IDisposable _anyButtonListener;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (playerInput == null)
            playerInput = GetComponent<PlayerInput>();

        var globalMap = playerInput.actions.FindActionMap(globalMapName, throwIfNotFound: false);
        if (globalMap != null)
            globalMap.Enable();
        else
            Debug.LogWarning($"No '{globalMapName}' action map found - skipping persistent global input");
    }

    private void OnEnable()
    {
        if (Instance != null && Instance != this) return;
        _anyButtonListener = InputSystem.onAnyButtonPress.Call(OnAnyButtonPressed);
    }

    private void OnDisable()
    {
        _anyButtonListener?.Dispose();
        _anyButtonListener = null;
    }

    private void OnAnyButtonPressed(InputControl control)
    {
        var device = control.device;
        InputDeviceType type;

        if (device is Gamepad)
            type = InputDeviceType.Gamepad;
        else if (device is Keyboard || device is Mouse)
            type = InputDeviceType.KeyboardMouse;
        else
            return;

        if (type == ActiveDevice) return;

        ActiveDevice = type;
        OnDeviceChanged?.Invoke(type);
    }

    public InputAction FindAction(string path, UnityEngine.Object context = null)
    {
        var action = playerInput.actions.FindAction(path);
        if (action == null)
        {
            string source = context != null ? $" (requested by '{context.name}')" : string.Empty;
            Debug.LogError($"No input action '{path}' found in the Input Actions asset{source}", context);
        }
        return action;
    }

    public void DisableModeMaps()
    {
        foreach (var map in playerInput.actions.actionMaps)
        {
            if (map.name != globalMapName)
                map.Disable();
        }

        CurrentMap = null;
        OnMapChanged?.Invoke(null);
    }

    public void SwitchMap(string mapName)
    {
        if (CurrentMap == mapName) return;

        var nextMap = playerInput.actions.FindActionMap(mapName, throwIfNotFound: false);
        if (nextMap == null)
        {
            Debug.LogError($"No action map named '{mapName}' found in the Input Actions asset");
            return;
        }

        foreach (var map in playerInput.actions.actionMaps)
        {
            if (map != nextMap && map.name != globalMapName)
                map.Disable();
        }

        nextMap.Enable();
        CurrentMap = mapName;
        OnMapChanged?.Invoke(mapName);
    }
}