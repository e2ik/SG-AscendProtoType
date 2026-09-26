using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class StateCamera : MonoBehaviour
{
    [SerializeField] private List<GameState> activeStates = new List<GameState> { GameState.World };

    private static readonly List<StateCamera> Registered = new List<StateCamera>();

    private Camera _camera;
    private AudioListener _listener;

    public bool IsLive => _camera != null && _camera.enabled;

    private void Awake()
    {
        _camera = GetComponent<Camera>();
        _listener = GetComponent<AudioListener>();
    }

    private void OnEnable()
    {
        Registered.Add(this);

        if (GameManager.Instance != null)
            GameManager.Instance.OnStateChanged += OnStateChanged;

        Refresh();
    }

    private void OnDisable()
    {
        Registered.Remove(this);

        if (GameManager.Instance != null)
            GameManager.Instance.OnStateChanged -= OnStateChanged;

        Refresh();
    }

    private void OnStateChanged(GameState previous, GameState next) => Refresh();

    private static void Refresh()
    {
        if (GameManager.Instance == null) return;

        var state = GameManager.Instance.CurrentState;
        if (!Registered.Exists(c => c.activeStates.Contains(state))) return;

        foreach (var cam in Registered)
            cam.SetLive(cam.activeStates.Contains(state));
    }

    private void SetLive(bool live)
    {
        _camera.enabled = live;
        if (_listener != null)
            _listener.enabled = live;
    }
}