using UnityEngine;
using TMPro;

public class InteractionPrompt : MonoBehaviour
{
    [SerializeField] private GameObject promptPrefab;
    [SerializeField] private Vector2 offset = new Vector2(0f, 0.5f);
    [SerializeField] private bool showInteractionText = true;

    [Header("References")]
    [SerializeField] private PlayerController player;
    [SerializeField] private PlayerInteractor interactor;
    [SerializeField] private Collider2D playerCollider;

    private GameObject _prompt;
    private TMP_Text _label;

    private void Awake()
    {
        if (player == null) player = GetComponent<PlayerController>();
        if (interactor == null) interactor = GetComponent<PlayerInteractor>();
        if (playerCollider == null) playerCollider = GetComponent<Collider2D>();

        if (promptPrefab == null) return;

        _prompt = Instantiate(promptPrefab, transform);
        _label = _prompt.GetComponentInChildren<TMP_Text>(true);
        _prompt.SetActive(false);
    }

    private void LateUpdate()
    {
        if (_prompt == null) return;

        var target = interactor != null ? interactor.Available : null;
        bool canShow = target != null && !IsPlayerFrozen();

        if (!canShow)
        {
            if (_prompt.activeSelf)
                _prompt.SetActive(false);
            return;
        }

        float top = playerCollider != null ? playerCollider.bounds.max.y : transform.position.y;
        _prompt.transform.position = new Vector3(interactor.CurrentCenter.x + offset.x, top + offset.y, transform.position.z);

        if (showInteractionText && _label != null && _label.text != target.InteractionPrompt)
            _label.text = target.InteractionPrompt;

        if (!_prompt.activeSelf)
            _prompt.SetActive(true);
    }

    private bool IsPlayerFrozen() =>
        player != null && player.Movement != null && !player.Movement.InputEnabled;

    private void OnDrawGizmosSelected()
    {
        var col = playerCollider != null ? playerCollider : GetComponent<Collider2D>();
        if (col == null) return;

        var point = new Vector3(col.bounds.center.x + offset.x, col.bounds.max.y + offset.y, transform.position.z);
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(point, 0.1f);
    }
}