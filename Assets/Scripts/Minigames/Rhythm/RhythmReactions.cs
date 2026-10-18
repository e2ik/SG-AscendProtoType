using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RhythmReactions : MonoBehaviour
{
    [System.Serializable]
    private class LineSet
    {
        [Range(0f, 1f)] public float chance = 1f;
        [TextArea(1, 2)] public List<string> lines = new List<string>();
        [System.NonSerialized] public int last = -1;
    }

    [System.Serializable]
    private class GradeLines
    {
        public LineSet perfect = new LineSet();
        public LineSet good = new LineSet();
        public LineSet okay = new LineSet();
        public LineSet bad = new LineSet();

        public LineSet For(RhythmGame.Grade grade) => grade switch
        {
            RhythmGame.Grade.Perfect => perfect,
            RhythmGame.Grade.Good => good,
            RhythmGame.Grade.Okay => okay,
            _ => bad
        };
    }

    [SerializeField] private RhythmGame game;
    [SerializeField] private Barker player;
    [SerializeField] private Barker npc;
    [SerializeField] [Min(0f)] private float lineDuration = 1.2f;
    [SerializeField] [Min(0.01f)] private float bubbleScale = 1.7f;
    [SerializeField] [Min(0f)] private float minGapPerSpeaker = 0.8f;
    [SerializeField] [Min(0f)] private float npcGradeDelay = 1.2f;
    [SerializeField] [Min(0f)] private float playerGradeDelay = 0.5f;
    [SerializeField] [Range(0f, 1f)] private float successThreshold = 0.6f;

    [Header("NPC Guide")]
    [SerializeField] private LineSet npcStart = new LineSet
    {
        chance = 1f,
        lines = new List<string> { "Let's breathe together.", "Nice and slow, {name}.", "Follow the ring with me." }
    };
    [SerializeField] private LineSet npcBreatheIn = new LineSet
    {
        chance = 1f,
        lines = new List<string> { "Breathe in...", "Deep breath in...", "In through the nose..." }
    };
    [SerializeField] private LineSet npcHold = new LineSet
    {
        chance = 0.4f,
        lines = new List<string> { "And hold...", "Steady...", "Almost there..." }
    };
    [SerializeField] private LineSet npcBreatheOut = new LineSet
    {
        chance = 1f,
        lines = new List<string> { "Breathe out...", "And out...", "Let it all go..." }
    };

    [Header("NPC Comments")]
    [SerializeField] private GradeLines npcGrades = new GradeLines
    {
        perfect = new LineSet { chance = 0.6f, lines = new List<string> { "Beautiful!", "That's it!", "Perfect rhythm." } },
        good = new LineSet { chance = 0.4f, lines = new List<string> { "Nice and steady.", "Good breath." } },
        okay = new LineSet { chance = 0.3f, lines = new List<string> { "Getting there.", "A little smoother next time." } },
        bad = new LineSet { chance = 0.6f, lines = new List<string> { "That's okay, take your time.", "No rush. Try again.", "Don't force it." } }
    };
    [SerializeField] private LineSet npcFinishGood = new LineSet
    {
        chance = 1f,
        lines = new List<string> { "You did great, {name}.", "Feel calmer?", "Lovely breathing." }
    };
    [SerializeField] private LineSet npcFinishBad = new LineSet
    {
        chance = 1f,
        lines = new List<string> { "It takes practice.", "We'll try again sometime.", "Every breath helps." }
    };

    [Header("Player")]
    [SerializeField] private GradeLines playerGrades = new GradeLines
    {
        perfect = new LineSet { chance = 0.5f, lines = new List<string> { "Ahh, much better.", "Woo... I feel calm." } },
        good = new LineSet { chance = 0.3f, lines = new List<string> { "Not bad.", "Okay, that felt nice." } },
        okay = new LineSet { chance = 0.2f, lines = new List<string> { "Hmm, okay.", "Bit shaky." } },
        bad = new LineSet { chance = 0.5f, lines = new List<string> { "Oops, rushed it.", "Whoa, too quick.", "Hmm..." } }
    };
    [SerializeField] private LineSet playerFinishGood = new LineSet
    {
        chance = 1f,
        lines = new List<string> { "I feel so much calmer.", "That actually helped." }
    };
    [SerializeField] private LineSet playerFinishBad = new LineSet
    {
        chance = 1f,
        lines = new List<string> { "I need more practice...", "Still a bit tense." }
    };

    private readonly Dictionary<Barker, float> _lastSpoke = new Dictionary<Barker, float>();
    private Coroutine _npcPending;
    private Coroutine _playerPending;

    private void Awake()
    {
        if (game == null)
            game = FindFirstObjectByType<RhythmGame>();
    }

    private void OnEnable()
    {
        if (game == null) return;
        game.Began += OnBegan;
        game.HoldStarted += OnHoldStarted;
        game.TargetRevealed += OnTargetRevealed;
        game.HoldReleased += OnHoldReleased;
        game.RunFinished += OnRunFinished;
    }

    private void OnDisable()
    {
        if (game != null)
        {
            game.Began -= OnBegan;
            game.HoldStarted -= OnHoldStarted;
            game.TargetRevealed -= OnTargetRevealed;
            game.HoldReleased -= OnHoldReleased;
            game.RunFinished -= OnRunFinished;
        }
        StopAllCoroutines();
    }

    private void OnBegan()
    {
        CancelPending();
        TrySay(npc, npcStart, true);
    }

    private void OnHoldStarted()
    {
        CancelPending();
        TrySay(npc, npcBreatheIn, true);
    }

    private void OnTargetRevealed()
    {
        TrySay(npc, npcHold, true);
    }

    private void OnHoldReleased(RhythmGame.Grade grade)
    {
        CancelPending();
        TrySay(npc, npcBreatheOut, true);

        bool last = game != null && game.AttemptIndex >= game.Attempts - 1;
        if (last) return;

        _npcPending = StartCoroutine(Delayed(npc, npcGrades.For(grade), npcGradeDelay, true));
        _playerPending = StartCoroutine(Delayed(player, playerGrades.For(grade), playerGradeDelay, false));
    }

    private void OnRunFinished(float average)
    {
        CancelPending();
        bool success = average >= successThreshold;
        _npcPending = StartCoroutine(Delayed(npc, success ? npcFinishGood : npcFinishBad, npcGradeDelay, true));
        _playerPending = StartCoroutine(Delayed(player, success ? playerFinishGood : playerFinishBad, playerGradeDelay, true));
    }

    private void CancelPending()
    {
        if (_npcPending != null) StopCoroutine(_npcPending);
        if (_playerPending != null) StopCoroutine(_playerPending);
        _npcPending = null;
        _playerPending = null;
    }

    private IEnumerator Delayed(Barker speaker, LineSet set, float delay, bool force)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);
        TrySay(speaker, set, force);
    }

    private bool TrySay(Barker speaker, LineSet set, bool force)
    {
        if (speaker == null || set == null || set.lines.Count == 0) return false;
        if (Random.value > set.chance) return false;
        if (!force && _lastSpoke.TryGetValue(speaker, out float last) && Time.time - last < minGapPerSpeaker) return false;

        int index = Random.Range(0, set.lines.Count);
        if (set.lines.Count > 1 && index == set.last)
            index = (index + Random.Range(1, set.lines.Count)) % set.lines.Count;
        set.last = index;

        speaker.Say(set.lines[index], lineDuration, bubbleScale);
        _lastSpoke[speaker] = Time.time;
        return true;
    }
}
