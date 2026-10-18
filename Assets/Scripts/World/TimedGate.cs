using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class TimedGate : MonoBehaviour
{
    [Header("Door")]
    [SerializeField] private Transform door;
    [SerializeField] private Collider2D doorCollider;
    [SerializeField] private Vector2 openOffset = new Vector2(0f, 2f);
    [SerializeField] private float moveDuration = 0.25f;

    [Header("Plate")]
    [SerializeField] private SpriteRenderer plateRenderer;
    [SerializeField] private Sprite plateUpSprite;
    [SerializeField] private Sprite plateDownSprite;
    [SerializeField] [Min(0f)] private float retriggerDelay = 0.4f;
    [SerializeField] [Range(0.05f, 1f)] private float pressZone = 0.3f;

    [Header("Timing (Agility)")]
    [SerializeField] private WorldMovementController playerMovement;
    [SerializeField][Min(2)] private int requiredAgility = 6;
    [SerializeField] private float accelerationAllowance = 0.1f;
    [SerializeField] [Min(0f)] private float minimumMargin = 0.35f;
    [SerializeField] private float fallbackOpenTime = 4f;

    [Header("Failsafes")]
    [SerializeField] private bool holdWhilePlayerInDoorway = true;
    [SerializeField] [Min(0f)] private float doorwayPadding = 0.1f;
    [SerializeField] private bool passWhenCrossed = true;
    [SerializeField] [Min(0f)] private float crossMargin = 0.3f;

    [Header("Progress")]
    [SerializeField] private string passedFlag = "summit_reached";
    [SerializeField] private bool debugLog;

    [Header("Sounds")]
    [SerializeField] private string plateSound;
    [SerializeField] private string openSound;
    [SerializeField] private string closeSound;
    [SerializeField] private string tickSound;
    [SerializeField] [Min(0.05f)] private float tickInterval = 0.5f;
    [SerializeField] [Min(0.05f)] private float finalTickInterval = 0.15f;
    [SerializeField] private string lockedSound;
    [SerializeField] private string passSound;

    [Header("Effects")]
    [SerializeField] private string openEffect;
    [SerializeField] private string closeEffect;
    [SerializeField] private string lockedEffect;
    [SerializeField] private Vector2 effectOffset;

    [Header("Locked Door")]
    [SerializeField] [Min(0f)] private float lockedBarkDistance = 0.3f;
    [SerializeField] [Min(0f)] private float lockedCooldown = 3f;
    [SerializeField] [Min(0f)] private float lockedQuietAfterClose = 2f;
    [SerializeField] [TextArea(1, 2)] private List<string> lockedLines = new List<string>
    {
        "It's shut tight.",
        "Locked... maybe that plate opens it?",
        "Won't budge. There must be a switch."
    };

    [Header("Barks")]
    [SerializeField] [TextArea(1, 2)] private List<string> tooSlowLines = new List<string>
    {
        "Too slow! I need more Agility.",
        "Missed it... I need {value} Agility.",
        "So close. I need to be quicker."
    };
    [SerializeField] [TextArea(1, 2)] private List<string> nearlyLines = new List<string>
    {
        "Almost! Keep running, no jumping.",
        "Argh, just missed it!",
        "Go straight for it next time."
    };
    [SerializeField] [TextArea(1, 2)] private List<string> passLines = new List<string>
    {
        "Made it!",
        "Ha! Just in time."
    };
    [SerializeField] [Min(0f)] private float barkDelay = 0.2f;
    [SerializeField] [Min(0f)] private float failBarkRange = 3f;
    [SerializeField] [Min(0f)] private float barkDuration = 1.4f;
    [SerializeField] [Min(0.01f)] private float barkScale = 1f;

    private Vector3 _closedPosition;
    private bool _doorCached;
    private float _doorCentreX;
    private float _doorHalfWidth;
    private float _distance;
    private float _plateSide = 1f;
    private bool _cycling;
    private bool _passed;
    private bool _playerOnPlate;
    private int _plateContacts;
    private float _nextTriggerTime;
    private float _nextLockedTime;
    private PlayerController _player;
    private Collider2D _playerCollider;
    private Barker _playerBarker;
    private WorldMovementController _runtimeMovement;

    public float OpenTime => CalculateOpenTime(_distance, Movement);

    private WorldMovementController Movement => _runtimeMovement != null ? _runtimeMovement : playerMovement;

    private void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void Awake()
    {
        if (plateRenderer == null)
            plateRenderer = GetComponent<SpriteRenderer>();

        if (door == null) return;
        _closedPosition = door.position;
        CacheDoor();
        _plateSide = Mathf.Sign(DoorCentreX() - PlateCentre().x);
        if (_plateSide == 0f) _plateSide = 1f;
        _distance = MeasureDistance();
    }

    private void OnEnable()
    {
        SaveData.FlagChanged += HandleFlagChanged;
    }

    private void OnDisable()
    {
        SaveData.FlagChanged -= HandleFlagChanged;

        if (_cycling && !_passed && door != null)
        {
            _cycling = false;
            if (doorCollider != null) doorCollider.enabled = true;
            door.position = _closedPosition;
        }

        if (_passed && door != null)
        {
            door.position = _closedPosition + (Vector3)openOffset;
            if (doorCollider != null) doorCollider.enabled = false;
        }

        _plateContacts = 0;
        _playerOnPlate = false;
        SetPlate(_passed);
    }

    private void Start()
    {
        SetPlate(false);

        if (door == null) Debug.LogWarning($"TimedGate '{name}' has no Door assigned - it will do nothing", this);
        if (doorCollider == null) Debug.LogWarning($"TimedGate '{name}' has no Door Collider assigned", this);
        var plate = GetComponent<Collider2D>();
        if (plate != null && !plate.isTrigger) Debug.LogWarning($"TimedGate '{name}': the plate collider isn't set to Is Trigger, so stepping on it won't register", this);

        var save = SaveManager.Instance != null ? SaveManager.Instance.CurrentSave : null;
        if (save == null || string.IsNullOrEmpty(passedFlag) || !save.GetFlag(passedFlag)) return;

        Log($"'{passedFlag}' is already set in this save - the door starts open and the plate is inactive");
        _passed = true;
        if (doorCollider != null) doorCollider.enabled = false;
        if (door != null) door.position = _closedPosition + (Vector3)openOffset;
        SetPlate(true);
    }

    private void Update()
    {
        if (_passed || door == null) return;

        bool centred = _playerOnPlate && PlayerOnCentre();
        if (!_cycling) SetPlate(centred);

        if (centred && !_cycling && Time.time >= _nextTriggerTime)
            BeginCycle();

        if (!_cycling)
            CheckLockedTouch();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        var player = other.GetComponentInParent<PlayerController>();
        if (player == null) return;

        CachePlayer(player, other);
        _plateContacts++;
        if (!_playerOnPlate) Log("Player stepped onto the plate");
        _playerOnPlate = true;
    }

    private bool PlayerOnCentre()
    {
        if (_player == null) return false;
        var plate = GetComponent<Collider2D>();
        float half = plate != null ? plate.bounds.extents.x * pressZone : 0.1f;
        float playerX = _playerCollider != null ? _playerCollider.bounds.center.x : _player.transform.position.x;
        return Mathf.Abs(playerX - PlateCentre().x) <= half;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.GetComponentInParent<PlayerController>() == null) return;
        _plateContacts = Mathf.Max(0, _plateContacts - 1);
        if (_plateContacts > 0) return;
        _playerOnPlate = false;
        if (!_cycling && !_passed) SetPlate(false);
    }

    private void CachePlayer(PlayerController player, Collider2D collider)
    {
        if (_player == player && _playerCollider != null && !_playerCollider.isTrigger) return;
        _player = player;
        _playerCollider = BodyCollider(player, collider);
        _playerBarker = player.GetComponentInChildren<Barker>();
        _runtimeMovement = player.GetComponent<WorldMovementController>();
        if (_playerBarker == null) _playerBarker = player.GetComponentInParent<Barker>();
        if (_playerBarker == null) Debug.LogWarning($"TimedGate '{name}' couldn't find a Barker on the player, so it can't bark", this);
        Log($"Using player collider '{(_playerCollider != null ? _playerCollider.name : "none")}'");
    }

    private static Collider2D BodyCollider(PlayerController player, Collider2D fallback)
    {
        foreach (var candidate in player.GetComponentsInChildren<Collider2D>())
        {
            if (candidate.enabled && !candidate.isTrigger)
                return candidate;
        }
        return fallback;
    }

    private void Log(string message)
    {
        if (debugLog)
            Debug.Log($"[TimedGate {name}] {message}", this);
    }

    [ContextMenu("Reset Gate In Current Save")]
    private void ResetGate()
    {
        var save = SaveManager.Instance != null ? SaveManager.Instance.CurrentSave : null;
        if (save != null && !string.IsNullOrEmpty(passedFlag))
        {
            save.SetFlag(passedFlag, false);
            SaveManager.Instance.SaveCurrent();
        }

        StopAllCoroutines();
        _passed = false;
        _cycling = false;
        if (door != null) door.position = _closedPosition;
        if (doorCollider != null) doorCollider.enabled = true;
        SetPlate(false);
        Log("Gate reset");
    }

    private void BeginCycle()
    {
        if (_passed || _cycling || door == null) return;
        Log("Plate pressed - opening door");
        int agility = _player.Stats != null ? _player.Stats.Get(StatType.Agility) : PlayerStats.StartingValue;
        StartCoroutine(Cycle(agility));
    }

    private IEnumerator Cycle(int agility)
    {
        _cycling = true;
        SetPlate(true);
        TelemetryManager.Log("timed_gate_start", name, agility, $"needs {requiredAgility}");

        PlaySound(plateSound);
        PlaySound(openSound);
        PlayEffect(openEffect);
        if (doorCollider != null) doorCollider.enabled = false;
        yield return Slide(_closedPosition + (Vector3)openOffset);

        float remaining = Mathf.Max(0.1f, OpenTime - moveDuration);
        float nextTick = 0f;

        for (float t = 0f; t < remaining && !_passed; t += Time.deltaTime)
        {
            if (passWhenCrossed && PlayerCrossed())
            {
                MarkPassed();
                break;
            }

            if (!string.IsNullOrEmpty(tickSound) && t >= nextTick)
            {
                PlaySound(tickSound);
                float progress = t / remaining;
                nextTick = t + Mathf.Lerp(tickInterval, finalTickInterval, progress * progress);
            }

            yield return null;
        }

        if (!_passed && holdWhilePlayerInDoorway)
        {
            while (!_passed && PlayerInDoorway())
            {
                if (passWhenCrossed && PlayerCrossed())
                {
                    MarkPassed();
                    break;
                }
                yield return null;
            }
        }

        if (!_passed)
        {
            TelemetryManager.Log("timed_gate_fail", name, agility, $"needs {requiredAgility}");
            PlaySound(closeSound);
            PlayEffect(closeEffect);
            if (doorCollider != null) doorCollider.enabled = true;
            _nextLockedTime = Mathf.Max(_nextLockedTime, Time.time + moveDuration + lockedQuietAfterClose);
            if (PlayerNearDoor(failBarkRange))
                StartCoroutine(Bark(agility < requiredAgility ? tooSlowLines : nearlyLines, agility));
            yield return Slide(_closedPosition);
            _nextTriggerTime = Time.time + retriggerDelay;
        }

        _cycling = false;
        SetPlate(_passed || _playerOnPlate);
    }

    private bool PlayerCrossed()
    {
        if (_player == null || door == null) return false;
        float doorEdge = DoorFarEdgeX(_plateSide);
        return (_player.transform.position.x - doorEdge) * _plateSide > crossMargin;
    }

    private bool PlayerNearDoor(float range)
    {
        if (_player == null) return false;
        float dx = Mathf.Abs(_player.transform.position.x - DoorCentreX());
        return dx - DoorHalfWidth() <= range;
    }

    private bool PlayerInDoorway()
    {
        if (_playerCollider == null || door == null) return false;
        var player = _playerCollider.bounds;
        float half = DoorHalfWidth() + doorwayPadding;
        return player.max.x >= DoorCentreX() - half && player.min.x <= DoorCentreX() + half;
    }

    private void MarkPassed()
    {
        Log("Player made it through");
        _passed = true;
        PlaySound(passSound);
        StartCoroutine(Bark(passLines, 0));

        var save = SaveManager.Instance != null ? SaveManager.Instance.CurrentSave : null;
        if (save != null && !string.IsNullOrEmpty(passedFlag))
        {
            save.SetFlag(passedFlag, true);
            SaveManager.Instance.SaveCurrent();
        }
    }

    private void CheckLockedTouch()
    {
        if (_playerCollider == null)
        {
            var player = FindFirstObjectByType<PlayerController>();
            if (player != null)
                CachePlayer(player, null);
        }

        if (_playerCollider == null || doorCollider == null || !doorCollider.enabled) return;
        if (Time.time < _nextLockedTime) return;

        Physics2D.SyncTransforms();
        var playerBounds = _playerCollider.bounds;
        var doorBounds = doorCollider.bounds;
        if (doorBounds.size == Vector3.zero) return;

        float gapX = Mathf.Max(0f, Mathf.Max(doorBounds.min.x - playerBounds.max.x, playerBounds.min.x - doorBounds.max.x));
        bool overlapY = playerBounds.min.y < doorBounds.max.y && playerBounds.max.y > doorBounds.min.y;
        if (!overlapY || gapX > lockedBarkDistance) return;

        Log($"Player at the locked door (gap {gapX:0.00})");

        _nextLockedTime = Time.time + lockedCooldown;
        PlaySound(lockedSound);
        PlayEffect(lockedEffect);
        StartCoroutine(Bark(lockedLines, 0, 0f));
    }

    private IEnumerator Bark(List<string> lines, int agility, float delay = -1f)
    {
        if (_playerBarker == null || lines == null || lines.Count == 0) yield break;

        float wait = delay >= 0f ? delay : barkDelay;
        if (wait > 0f)
            yield return new WaitForSeconds(wait);

        if (_playerBarker == null) yield break;
        string line = lines[Random.Range(0, lines.Count)]
            .Replace("{value}", requiredAgility.ToString())
            .Replace("{current}", agility.ToString());
        _playerBarker.Say(line, barkDuration, barkScale);
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

    private void SetPlate(bool down)
    {
        if (plateRenderer == null) return;
        var sprite = down ? plateDownSprite : plateUpSprite;
        if (sprite != null) plateRenderer.sprite = sprite;
    }

    private void HandleFlagChanged(string key, bool value)
    {
        if (key != passedFlag || value == _passed) return;

        if (!value)
        {
            _passed = false;
            return;
        }

        Log($"'{passedFlag}' was set elsewhere - opening the gate");
        StopAllCoroutines();
        _passed = true;
        _cycling = false;
        if (doorCollider != null) doorCollider.enabled = false;
        if (door != null) door.position = _closedPosition + (Vector3)openOffset;
        SetPlate(true);
    }

    private Vector3 PlateCentre()
    {
        var plate = GetComponent<Collider2D>();
        return plate != null ? plate.bounds.center : transform.position;
    }

    private float DoorCentreX()
    {
        if (_doorCached) return _doorCentreX;
        if (doorCollider != null && doorCollider.enabled) return doorCollider.bounds.center.x;
        return door != null ? door.position.x : transform.position.x;
    }

    private float DoorHalfWidth()
    {
        if (_doorCached) return _doorHalfWidth;
        return doorCollider != null && doorCollider.enabled ? doorCollider.bounds.extents.x : 0f;
    }

    private float DoorFarEdgeX(float side) => DoorCentreX() + side * DoorHalfWidth();

    private void CacheDoor()
    {
        if (doorCollider == null || door == null) return;
        bool wasEnabled = doorCollider.enabled;
        doorCollider.enabled = true;
        Physics2D.SyncTransforms();
        var bounds = doorCollider.bounds;
        doorCollider.enabled = wasEnabled;

        _doorCentreX = bounds.center.x;
        _doorHalfWidth = bounds.extents.x;
        _doorCached = true;
    }

    private float MeasureDistance()
    {
        float side = Mathf.Sign(DoorCentreX() - PlateCentre().x);
        if (side == 0f) side = 1f;
        float end = DoorFarEdgeX(side) + side * crossMargin;
        return Mathf.Abs(end - PlateCentre().x);
    }

    private static float TimeAt(WorldMovementController movement, int agility, float distance) =>
        distance / Mathf.Max(0.01f, movement.MoveSpeedAt(agility));

    private float CalculateOpenTime(float distance, WorldMovementController movement)
    {
        if (movement == null) return fallbackOpenTime;
        float justFast = TimeAt(movement, requiredAgility, distance);
        float tooSlow = TimeAt(movement, requiredAgility - 1, distance);
        float midpoint = (justFast + tooSlow) * 0.5f + accelerationAllowance;
        return Mathf.Max(midpoint, justFast + minimumMargin);
    }

    private void PlaySound(string key)
    {
        if (!string.IsNullOrEmpty(key) && ASpawner.Instance != null)
            ASpawner.Play(key);
    }

    private void PlayEffect(string key)
    {
        if (string.IsNullOrEmpty(key) || door == null) return;
        Vector3 position = doorCollider != null ? doorCollider.bounds.center : door.position;
        PSpawner.Spawn(key, position + (Vector3)effectOffset);
    }

    private void OnDrawGizmosSelected()
    {
        var plateCollider = GetComponent<Collider2D>();
        if (plateCollider != null)
        {
            var b = plateCollider.bounds;
            Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.8f);
            Gizmos.DrawWireCube(b.center, new Vector3(b.size.x * pressZone, b.size.y, 0f));
        }

        if (door == null || playerMovement == null) return;

        float distance = MeasureDistance();
        Gizmos.color = Color.cyan;
        var plateCentre = PlateCentre();
        float gizmoSide = Mathf.Sign(DoorCentreX() - plateCentre.x);
        if (gizmoSide == 0f) gizmoSide = 1f;
        float endX = DoorFarEdgeX(gizmoSide) + gizmoSide * crossMargin;
        Gizmos.DrawLine(plateCentre, new Vector3(endX, plateCentre.y, 0f));
        Gizmos.DrawWireSphere(plateCentre, 0.1f);
        Gizmos.DrawWireSphere(new Vector3(endX, plateCentre.y, 0f), 0.1f);

#if UNITY_EDITOR
        float justFast = TimeAt(playerMovement, requiredAgility, distance);
        float tooSlow = TimeAt(playerMovement, requiredAgility - 1, distance);
        float open = CalculateOpenTime(distance, playerMovement);
        string warning = open >= tooSlow
            ? $"  WARNING: AGI {requiredAgility - 1} can also make it - move the plate further away or lower Minimum Margin"
            : string.Empty;
        UnityEditor.Handles.Label(plateCentre + Vector3.up,
            $"AGI {requiredAgility}: open {open:0.00}s  " +
            $"(AGI {requiredAgility} needs {justFast:0.00}s, AGI {requiredAgility - 1} needs {tooSlow:0.00}s){warning}");
#endif
    }
}