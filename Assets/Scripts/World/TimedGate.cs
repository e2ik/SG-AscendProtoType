using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class TimedGate : MonoBehaviour
{
    [Header("Door")]
    [SerializeField] private Transform door;
    [SerializeField] private Collider2D doorCollider;
    [SerializeField] private Vector2 openOffset = new Vector2(0f, 2f);
    [SerializeField] private float moveDuration = 0.25f;

    [Header("Timing (Agility)")]
    [SerializeField] private WorldMovementController playerMovement;
    [SerializeField][Min(2)] private int requiredAgility = 6;
    [SerializeField] private float accelerationAllowance = 0.1f;
    [SerializeField] private float fallbackOpenTime = 4f;

    [Header("Progress")]
    [SerializeField] private string passedFlag = "summit_reached";

    [Header("Sounds")]
    [SerializeField] private string openSound;
    [SerializeField] private string closeSound;

    private Vector3 _closedPosition;
    private float _distance;
    private bool _cycling;
    private bool _passed;

    public float OpenTime => CalculateOpenTime(_distance);

    private void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void Awake()
    {
        if (door == null) return;
        _closedPosition = door.position;
        _distance = MeasureDistance();
    }

    private void OnEnable()
    {
        SaveData.FlagChanged += HandleFlagChanged;
    }

    private void OnDisable()
    {
        SaveData.FlagChanged -= HandleFlagChanged;

        if (_cycling && !_passed)
        {
            _cycling = false;
            doorCollider.enabled = true;
            door.position = _closedPosition;
        }
    }

    private void Start()
    {
        var save = SaveManager.Instance != null ? SaveManager.Instance.CurrentSave : null;
        if (save == null || string.IsNullOrEmpty(passedFlag) || !save.GetFlag(passedFlag)) return;

        _passed = true;
        doorCollider.enabled = false;
        door.position = _closedPosition + (Vector3)openOffset;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_passed || _cycling || door == null) return;

        var player = other.GetComponentInParent<PlayerController>();
        if (player == null) return;

        int agility = player.Stats != null ? player.Stats.Get(StatType.Agility) : PlayerStats.StartingValue;
        StartCoroutine(Cycle(agility));
    }

    private IEnumerator Cycle(int agility)
    {
        _cycling = true;
        TelemetryManager.Log("timed_gate_start", name, agility, $"needs {requiredAgility}");

        PlaySound(openSound);
        doorCollider.enabled = false;
        yield return Slide(_closedPosition + (Vector3)openOffset);

        float remaining = OpenTime - moveDuration;
        for (float t = 0f; t < remaining && !_passed; t += Time.deltaTime)
            yield return null;

        if (!_passed)
        {
            TelemetryManager.Log("timed_gate_fail", name, agility, $"needs {requiredAgility}");
            PlaySound(closeSound);
            doorCollider.enabled = true;
            yield return Slide(_closedPosition);
        }

        _cycling = false;
    }

    private IEnumerator Slide(Vector3 target)
    {
        Vector3 from = door.position;
        for (float t = 0f; t < moveDuration; t += Time.deltaTime)
        {
            door.position = Vector3.Lerp(from, target, t / moveDuration);
            yield return null;
        }
        door.position = target;
    }

    private void HandleFlagChanged(string key, bool value)
    {
        if (value && key == passedFlag)
            _passed = true;
    }

    private float MeasureDistance()
    {
        float halfDoor = doorCollider != null ? doorCollider.bounds.extents.x : 0f;
        return Mathf.Abs(door.position.x - transform.position.x) + halfDoor;
    }

    private float TimeAt(int agility, float distance) => distance / playerMovement.MoveSpeedAt(agility);

    // Opens for the midpoint between "just fast enough" and "one level too slow",
    // so both sides of the requirement get the same margin.
    private float CalculateOpenTime(float distance)
    {
        if (playerMovement == null) return fallbackOpenTime;
        return (TimeAt(requiredAgility, distance) + TimeAt(requiredAgility - 1, distance)) * 0.5f + accelerationAllowance;
    }

    private static void PlaySound(string key)
    {
        if (!string.IsNullOrEmpty(key))
            ASpawner.Play(key);
    }

    private void OnDrawGizmosSelected()
    {
        if (door == null || playerMovement == null) return;

        float distance = MeasureDistance();
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, new Vector3(door.position.x, transform.position.y, 0f));

#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up,
            $"AGI {requiredAgility}: open {CalculateOpenTime(distance):0.00}s  " +
            $"(AGI {requiredAgility} needs {TimeAt(requiredAgility, distance):0.00}s, " +
            $"AGI {requiredAgility - 1} needs {TimeAt(requiredAgility - 1, distance):0.00}s)");
#endif
    }
}