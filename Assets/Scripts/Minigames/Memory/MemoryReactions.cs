using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MemoryReactions : MonoBehaviour
{
    [System.Serializable]
    private class LineSet
    {
        [Range(0f, 1f)] public float chance = 1f;
        [TextArea(1, 2)] public List<string> lines = new List<string>();
        [System.NonSerialized] public int last = -1;
    }

    [SerializeField] private MemoryGame game;
    [SerializeField] private Barker player;
    [SerializeField] private Barker npc;
    [SerializeField] [Min(0f)] private float lineDuration = 1.1f;
    [SerializeField] [Min(0.01f)] private float bubbleScale = 1.7f;
    [SerializeField] [Min(0f)] private float minGapPerSpeaker = 0.8f;
    [SerializeField] [Min(0f)] private float npcDelay = 0.25f;

    [Header("Player")]
    [SerializeField] private LineSet playerMismatch = new LineSet
    {
        chance = 0.8f,
        lines = new List<string> { "Aww...", "So close!", "Hmm, not that one.", "Ugh!", "Wait, where was it?", "Oops." }
    };
    [SerializeField] private LineSet playerMatch = new LineSet
    {
        chance = 0.7f,
        lines = new List<string> { "Yes!", "Got it!", "Nice!", "Woo!", "Easy!", "Remembered that one!" }
    };
    [SerializeField] private LineSet playerWin = new LineSet
    {
        chance = 1f,
        lines = new List<string> { "All done!", "Nailed it!", "Too easy!" }
    };
    [SerializeField] private LineSet playerLose = new LineSet
    {
        chance = 1f,
        lines = new List<string> { "Aww, man...", "Next time!", "My brain's fried." }
    };

    [Header("NPC")]
    [SerializeField] private LineSet npcMatch = new LineSet
    {
        chance = 0.4f,
        lines = new List<string> { "Yay!", "Great memory, {name}!", "Woohoo!", "You're on a roll!", "Amazing!" }
    };
    [SerializeField] private LineSet npcMismatch = new LineSet
    {
        chance = 0.15f,
        lines = new List<string> { "Don't worry!", "You'll get it!", "Shake it off!" }
    };
    [SerializeField] private LineSet npcWin = new LineSet
    {
        chance = 1f,
        lines = new List<string> { "You did it, {name}!", "That was awesome!", "Wow, every single one!" }
    };
    [SerializeField] private LineSet npcLose = new LineSet
    {
        chance = 1f,
        lines = new List<string> { "Good try!", "So close, {name}!", "We'll get 'em next time!" }
    };

    [Header("NPC Bounce")]
    [SerializeField] private Transform npcBounceTarget;
    [SerializeField] [Min(1)] private int bounceHops = 3;
    [SerializeField] [Min(0f)] private float bounceHeight = 0.25f;
    [SerializeField] [Min(0.01f)] private float hopDuration = 0.2f;
    [SerializeField] [Range(0f, 1f)] private float hopFalloff = 0.7f;
    [SerializeField] private bool snapToPixels = true;
    [SerializeField] [Min(0f)] private float pixelsPerUnit = 16f;

    private readonly Dictionary<Barker, float> _lastSpoke = new Dictionary<Barker, float>();
    private Vector3 _bounceBase;
    private bool _bounceBaseCaptured;
    private Coroutine _bounce;

    private void Awake()
    {
        if (game == null)
            game = FindFirstObjectByType<MemoryGame>();
        if (npcBounceTarget == null && npc != null)
            npcBounceTarget = npc.transform;
    }

    private void OnEnable()
    {
        if (game == null) return;
        game.PairMatched += OnMatched;
        game.PairMismatched += OnMismatched;
        game.RunFinished += OnFinished;
    }

    private void OnDisable()
    {
        if (game != null)
        {
            game.PairMatched -= OnMatched;
            game.PairMismatched -= OnMismatched;
            game.RunFinished -= OnFinished;
        }

        StopAllCoroutines();
        ResetBounce();
    }

    private void OnMatched()
    {
        TrySay(player, playerMatch, false);
        if (Roll(npcMatch))
            StartCoroutine(Delayed(npc, npcMatch, true));
    }

    private void OnMismatched()
    {
        TrySay(player, playerMismatch, false);
        if (Roll(npcMismatch))
            StartCoroutine(Delayed(npc, npcMismatch, false));
    }

    private void OnFinished(bool success)
    {
        TrySay(player, success ? playerWin : playerLose, true);
        var npcSet = success ? npcWin : npcLose;
        if (Roll(npcSet))
            StartCoroutine(Delayed(npc, npcSet, success, true));
    }

    private IEnumerator Delayed(Barker speaker, LineSet set, bool bounce, bool force = false)
    {
        if (npcDelay > 0f)
            yield return new WaitForSeconds(npcDelay);

        if (Say(speaker, set, force) && bounce)
            Bounce();
    }

    private bool TrySay(Barker speaker, LineSet set, bool force)
    {
        return Roll(set) && Say(speaker, set, force);
    }

    private static bool Roll(LineSet set)
    {
        return set != null && set.lines.Count > 0 && Random.value <= set.chance;
    }

    private bool Say(Barker speaker, LineSet set, bool force)
    {
        if (speaker == null || set == null || set.lines.Count == 0) return false;

        if (!force && _lastSpoke.TryGetValue(speaker, out float last) && Time.time - last < minGapPerSpeaker)
            return false;

        int index = Random.Range(0, set.lines.Count);
        if (set.lines.Count > 1 && index == set.last)
            index = (index + Random.Range(1, set.lines.Count)) % set.lines.Count;
        set.last = index;

        speaker.Say(set.lines[index], lineDuration, bubbleScale);
        _lastSpoke[speaker] = Time.time;
        return true;
    }

    private void Bounce()
    {
        if (npcBounceTarget == null) return;

        if (!_bounceBaseCaptured)
        {
            _bounceBase = npcBounceTarget.localPosition;
            _bounceBaseCaptured = true;
        }

        if (_bounce != null)
            StopCoroutine(_bounce);
        _bounce = StartCoroutine(BounceRoutine());
    }

    private IEnumerator BounceRoutine()
    {
        float height = bounceHeight;

        for (int hop = 0; hop < bounceHops; hop++)
        {
            for (float t = 0f; t < hopDuration; t += Time.deltaTime)
            {
                float p = t / hopDuration;
                float y = 4f * p * (1f - p) * height;
                if (snapToPixels && pixelsPerUnit > 0f)
                    y = Mathf.Round(y * pixelsPerUnit) / pixelsPerUnit;
                npcBounceTarget.localPosition = _bounceBase + Vector3.up * y;
                yield return null;
            }
            npcBounceTarget.localPosition = _bounceBase;
            height *= hopFalloff;
        }

        _bounce = null;
    }

    private void ResetBounce()
    {
        _bounce = null;
        if (npcBounceTarget != null && _bounceBaseCaptured)
            npcBounceTarget.localPosition = _bounceBase;
    }
}