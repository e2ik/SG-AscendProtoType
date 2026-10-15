using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class StatBarrier : MonoBehaviour, IInteractable
{
    [Header("Requirement")]
    [SerializeField] private StatType stat = StatType.Strength;
    [SerializeField][Min(1)] private int requiredValue = 5;
    [SerializeField] private string openedFlag = "gate_tier2_open";

    [Header("Prompt")]
    [SerializeField] private string promptText = "Push ({0} {1})";

    [Header("Opening")]
    [SerializeField] private Vector2 openOffset = new Vector2(0f, 3f);
    [SerializeField] private float openDuration = 1.2f;
    [SerializeField] private AnimationCurve openCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Too Weak")]
    [SerializeField] private Transform visual;
    [SerializeField] private float wobbleDistance = 0.06f;
    [SerializeField] private float wobbleDuration = 0.3f;

    [Header("Sounds")]
    [SerializeField] private string strainSound;
    [SerializeField] private string openSound;

    public bool IsOpen { get; private set; }

    public string InteractionPrompt => string.Format(promptText, stat, requiredValue);
    public bool CanInteract => !IsOpen && !_wobbling;

    private Collider2D _collider;
    private Vector3 _closedPosition;
    private bool _wobbling;

    private void Awake()
    {
        _collider = GetComponent<Collider2D>();
        _closedPosition = transform.position;
        if (visual == null) visual = transform;
    }

    private void Start()
    {
        var save = SaveManager.Instance != null ? SaveManager.Instance.CurrentSave : null;
        if (save == null || string.IsNullOrEmpty(openedFlag) || !save.GetFlag(openedFlag)) return;

        IsOpen = true;
        _collider.enabled = false;
        transform.position = _closedPosition + (Vector3)openOffset;
    }

    public void Interact(PlayerController interactor)
    {
        if (!CanInteract || interactor == null) return;

        int current = interactor.Stats != null ? interactor.Stats.Get(stat) : PlayerStats.StartingValue;
        if (current >= requiredValue) Open(current);
        else Strain(current);
    }

    private void Open(int current)
    {
        IsOpen = true;
        _collider.enabled = false;

        var save = SaveManager.Instance != null ? SaveManager.Instance.CurrentSave : null;
        if (save != null && !string.IsNullOrEmpty(openedFlag))
        {
            save.SetFlag(openedFlag, true);
            SaveManager.Instance.SaveCurrent();
        }

        TelemetryManager.Log("barrier_open", openedFlag, current, $"{stat} {requiredValue}");
        PlaySound(openSound);
        StartCoroutine(Slide());
    }

    private void Strain(int current)
    {
        TelemetryManager.Log("barrier_blocked", openedFlag, current, $"{stat} {requiredValue}");
        PlaySound(strainSound);
        StartCoroutine(Wobble());
    }

    private IEnumerator Slide()
    {
        Vector3 from = transform.position;
        Vector3 to = _closedPosition + (Vector3)openOffset;

        for (float t = 0f; t < openDuration; t += Time.deltaTime)
        {
            transform.position = Vector3.LerpUnclamped(from, to, openCurve.Evaluate(t / openDuration));
            yield return null;
        }

        transform.position = to;
    }

    private IEnumerator Wobble()
    {
        _wobbling = true;
        Vector3 origin = visual.localPosition;

        for (float t = 0f; t < wobbleDuration; t += Time.deltaTime)
        {
            float damping = 1f - t / wobbleDuration;
            visual.localPosition = origin + Vector3.right * (Mathf.Sin(t * 60f) * wobbleDistance * damping);
            yield return null;
        }

        visual.localPosition = origin;
        _wobbling = false;
    }

    private static void PlaySound(string key)
    {
        if (!string.IsNullOrEmpty(key))
            ASpawner.Play(key);
    }
}