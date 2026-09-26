using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private MovementController movement;
    [SerializeField] private PlayerStatsController stats;
    [SerializeField] private PlayerInteractor interactor;
    [SerializeField] private PlayerAppearance appearance;

    public MovementController Movement => movement;
    public PlayerStatsController Stats => stats;
    public PlayerInteractor Interactor => interactor;
    public PlayerAppearance Appearance => appearance;

    public void FreezeMovement() => movement.SetInputEnabled(false);
    public void UnfreezeMovement() => movement.SetInputEnabled(true);
}