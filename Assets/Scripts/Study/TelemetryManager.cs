using System;
using System.IO;
using UnityEngine;

[Serializable]
public class TelemetryEvent
{
    public string sessionId;
    public string participantId;
    public string condition;
    public string utc;
    public float sessionTime;
    public string state;
    public string type;
    public string target;
    public float value;
    public string detail;
}

public class TelemetryManager : MonoBehaviour
{
    public static TelemetryManager Instance { get; private set; }

    [SerializeField] private bool enableTelemetry = true;
    [SerializeField] private string folderName = "Telemetry";

    public string SessionId { get; private set; }
    public string FilePath { get; private set; }
    public float SessionTime => Time.realtimeSinceStartup - _sessionStart;

    private StreamWriter _writer;
    private float _sessionStart;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnStateChanged += HandleStateChanged;

        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.DialogueStarted += HandleDialogueStarted;
            DialogueManager.Instance.DialogueEnded += HandleDialogueEnded;
        }
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnStateChanged -= HandleStateChanged;

        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.DialogueStarted -= HandleDialogueStarted;
            DialogueManager.Instance.DialogueEnded -= HandleDialogueEnded;
        }

        CloseSession();
    }

    private void OnApplicationQuit()
    {
        Log("session_end", value: SessionTime);
        CloseSession();
    }

    public static void Log(string type, string target = null, float value = 0f, string detail = null)
    {
        if (Instance != null)
            Instance.Write(type, target, value, detail);
    }

    private void Write(string type, string target, float value, string detail)
    {
        if (!enableTelemetry) return;
        if (_writer == null && !OpenSession()) return;

        var study = StudySettings.Instance;
        var entry = new TelemetryEvent
        {
            sessionId = SessionId,
            participantId = study != null ? study.ParticipantId : "UNKNOWN",
            condition = study != null ? study.Condition.ToString() : StudyCondition.Full.ToString(),
            utc = DateTime.UtcNow.ToString("o"),
            sessionTime = SessionTime,
            state = GameManager.Instance != null ? GameManager.Instance.CurrentState.ToString() : string.Empty,
            type = type,
            target = target ?? string.Empty,
            value = value,
            detail = detail ?? string.Empty
        };

        _writer.WriteLine(JsonUtility.ToJson(entry));
    }

    private bool OpenSession()
    {
        try
        {
            string folder = Path.Combine(Application.persistentDataPath, folderName);
            Directory.CreateDirectory(folder);

            SessionId = Guid.NewGuid().ToString("N").Substring(0, 8);
            FilePath = Path.Combine(folder, $"{DateTime.UtcNow:yyyyMMdd_HHmmss}_{SessionId}.jsonl");
            _writer = new StreamWriter(FilePath, append: true) { AutoFlush = true };
            _sessionStart = Time.realtimeSinceStartup;
        }
        catch (Exception e)
        {
            Debug.LogError($"Telemetry disabled - could not open log file: {e.Message}");
            enableTelemetry = false;
            return false;
        }

        Debug.Log($"Telemetry session {SessionId} -> {FilePath}");
        Write("session_start", Application.version, 0f, Application.platform.ToString());
        return true;
    }

    private void CloseSession()
    {
        _writer?.Dispose();
        _writer = null;
    }

    private void HandleStateChanged(GameState previous, GameState next) =>
        Log("state_change", next.ToString(), detail: previous.ToString());

    private void HandleDialogueStarted(DialogueData dialogue) =>
        Log("dialogue_start", dialogue != null ? dialogue.name : null);

    private void HandleDialogueEnded(DialogueData dialogue) =>
        Log("dialogue_end", dialogue != null ? dialogue.name : null);
}