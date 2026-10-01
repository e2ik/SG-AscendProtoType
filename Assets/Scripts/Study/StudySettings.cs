using System;
using UnityEngine;

public enum StudyCondition
{
    Full,
    Comparison
}

public class StudySettings : MonoBehaviour
{
    public static StudySettings Instance { get; private set; }

    [SerializeField] private StudyCondition condition = StudyCondition.Full;
    [SerializeField] private string participantId = "TEST";

    public StudyCondition Condition => condition;
    public string ParticipantId => participantId;

    public static bool IsComparison => Instance != null && Instance.condition == StudyCondition.Comparison;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        ReadCommandLine();
    }

    public void SetParticipantId(string id)
    {
        if (!string.IsNullOrWhiteSpace(id))
            participantId = id.Trim();
    }

    private void ReadCommandLine()
    {
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "-participant":
                    SetParticipantId(args[i + 1]);
                    break;
                case "-condition":
                    if (Enum.TryParse(args[i + 1], true, out StudyCondition parsed))
                        condition = parsed;
                    break;
            }
        }
    }
}