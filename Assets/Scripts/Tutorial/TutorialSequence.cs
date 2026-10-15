using System;
using System.Collections.Generic;
using UnityEngine;

public enum TutorialStepCondition
{
    Continue,
    Timed,
    InputAction,
    GameEvent,
    Flag
}

[Serializable]
public class TutorialStep
{
    [TextArea(2, 4)] public string prompt;
    public TutorialStepCondition condition = TutorialStepCondition.Continue;
    public string actionPath;
    public string eventKey;
    [Min(1)] public int requiredCount = 1;
    public string flag;
    [Min(0f)] public float duration = 3f;
    [Min(0f)] public float minDisplayTime = 0.5f;
    [TextArea(1, 3)] public string successText;
    public bool freezePlayer;
    public bool allowMenus;
}

[CreateAssetMenu(fileName = "NewTutorial", menuName = "Tutorial/Tutorial Sequence")]
public class TutorialSequence : ScriptableObject
{
    [SerializeField] private string id;
    [SerializeField] private bool canRepeat;
    [SerializeField] private List<TutorialStep> steps = new List<TutorialStep>();

    public string Id => string.IsNullOrEmpty(id) ? name : id;
    public bool CanRepeat => canRepeat;
    public IReadOnlyList<TutorialStep> Steps => steps;
}
