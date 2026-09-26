using UnityEngine;

public abstract class MovementController : MonoBehaviour
{
    public bool InputEnabled { get; private set; } = true;

    public void SetInputEnabled(bool enabled)
    {
        InputEnabled = enabled;
        if (!enabled) StopMovement();
    }

    protected abstract void StopMovement();
}