using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class TutorialTriggerVolume : MonoBehaviour
{
    [SerializeField] private TutorialSequence sequence;
    [SerializeField] private List<TutorialSequence> requiredSequences = new List<TutorialSequence>();
    [SerializeField] private bool waitIfBusy = true;

    private void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (sequence == null || TutorialDirector.Instance == null) return;
        if (other.GetComponentInParent<PlayerController>() == null) return;

        TutorialDirector.Instance.TryStart(sequence, requiredSequences, waitIfBusy);
    }
}
