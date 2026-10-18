using System.Collections.Generic;
using UnityEngine;

public class FlagCheerBark : MonoBehaviour
{
    [SerializeField] private Barker barker;
    [SerializeField] private string requiredFlag = "almost_there";
    [SerializeField] private string rememberFlag;
    [SerializeField] [Min(0f)] private float range = 3f;
    [SerializeField] [Min(0f)] private float startDelay = 0.5f;
    [SerializeField] [Min(0f)] private float gapAfterOthers = 0.6f;
    [SerializeField] [Min(0f)] private float barkDuration = 1.6f;
    [SerializeField] [Min(0.01f)] private float bubbleScale = 1.4f;
    [SerializeField] [TextArea(1, 2)] private List<string> lines = new List<string>
    {
        "YAY! You made it!",
        "You're at the top, {name}!",
        "Woohoo! Look how far you've come!",
        "You did it! I knew you could!"
    };

    private static FlagCheerBark _speaking;
    private static float _nextSlot;

    private Transform _player;
    private float _flagSeenTime = -1f;
    private bool _done;

    private void Awake()
    {
        if (barker == null)
            barker = GetComponentInChildren<Barker>();
    }

    private void OnEnable()
    {
        _done = AlreadyCheered();
        _flagSeenTime = -1f;
    }

    private void OnDisable()
    {
        if (_speaking == this)
            _speaking = null;
    }

    private void Update()
    {
        if (_done || barker == null) return;

        if (_speaking == this)
        {
            if (!barker.IsSpeaking)
            {
                _speaking = null;
                _nextSlot = Time.time + gapAfterOthers;
                Finish();
            }
            return;
        }

        if (!FlagSet()) return;

        if (_flagSeenTime < 0f)
            _flagSeenTime = Time.time;
        if (Time.time < _flagSeenTime + startDelay) return;

        if (_speaking != null || Time.time < _nextSlot) return;
        if (Blocked() || !PlayerInRange()) return;

        if (lines.Count == 0)
        {
            Finish();
            return;
        }

        if (barker.Say(lines[Random.Range(0, lines.Count)], barkDuration, bubbleScale) != null)
            _speaking = this;
        else
            Finish();
    }

    private bool FlagSet()
    {
        var save = SaveManager.Instance != null ? SaveManager.Instance.CurrentSave : null;
        return save != null && !string.IsNullOrEmpty(requiredFlag) && save.GetFlag(requiredFlag);
    }

    private bool AlreadyCheered()
    {
        if (string.IsNullOrEmpty(rememberFlag)) return false;
        var save = SaveManager.Instance != null ? SaveManager.Instance.CurrentSave : null;
        return save != null && save.GetFlag(rememberFlag);
    }

    private void Finish()
    {
        _done = true;
        if (string.IsNullOrEmpty(rememberFlag)) return;

        var save = SaveManager.Instance != null ? SaveManager.Instance.CurrentSave : null;
        if (save == null) return;
        save.SetFlag(rememberFlag, true);
        SaveManager.Instance.SaveCurrent();
    }

    private bool PlayerInRange()
    {
        if (_player == null)
        {
            var controller = FindFirstObjectByType<PlayerController>();
            _player = controller != null ? controller.transform : null;
        }
        return _player != null && Vector2.Distance(_player.position, transform.position) <= range;
    }

    private static bool Blocked()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsTransitioning) return true;
        if (UIManager.Instance != null && UIManager.Instance.IsAnyUIOpen) return true;
        return DialogueManager.Instance != null && DialogueManager.Instance.IsActive;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, range);
    }
}
