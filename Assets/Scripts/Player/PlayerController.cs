using UnityEngine;

// lives on the Player object in the World scene. Doesn't do much itself -
// just ties together the movement, stats, and interaction components so
// NPCs and UI have one place to reach the player through.
public class PlayerController : MonoBehaviour
{
    [SerializeField] private MovementController movement;
    [SerializeField] private PlayerStatsController stats;
    [SerializeField] private PlayerInteractor interactor;

    public MovementController Movement => movement;
    public PlayerStatsController Stats => stats;
    public PlayerInteractor Interactor => interactor;

    public void FreezeMovement() => movement.SetInputEnabled(false);
    public void UnfreezeMovement() => movement.SetInputEnabled(true);
}