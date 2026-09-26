using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }

    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private string globalMapName = "Global";

    public PlayerInput PlayerInput => playerInput;
    public string CurrentMap { get; private set; }
    public event Action<string> OnMapChanged;

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

    public InputAction FindAction(string path)
    {
        var action = playerInput.actions.FindAction(path);
        if (action == null)
            Debug.LogError($"No input action '{path}' found in the Input Actions asset");
        return action;
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