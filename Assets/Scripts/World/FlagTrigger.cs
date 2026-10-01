using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class FlagTrigger : MonoBehaviour
{
    [SerializeField] private string flag = "tier1_west_reached";
    [SerializeField] private string telemetryEvent = "area_reached";

    [Header("Optional Dialogue")]
    [SerializeField] private DialogueData dialogue;
    [SerializeField] private CharacterProfile speaker;

    private void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        var player = other.GetComponentInParent<PlayerController>();
        var save = SaveManager.Instance != null ? SaveManager.Instance.CurrentSave : null;
        if (player == null || save == null || string.IsNullOrEmpty(flag) || save.GetFlag(flag)) return;

        save.SetFlag(flag, true);
        SaveManager.Instance.SaveCurrent();
        TelemetryManager.Log(telemetryEvent, flag);

        if (dialogue == null || DialogueManager.Instance == null || DialogueManager.Instance.IsActive) return;

        player.FreezeMovement();
        if (!DialogueManager.Instance.StartDialogue(dialogue, player.UnfreezeMovement, speaker))
            player.UnfreezeMovement();
    }
}