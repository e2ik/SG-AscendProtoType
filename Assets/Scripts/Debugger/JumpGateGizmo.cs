using UnityEngine;

public class JumpGateGizmo : MonoBehaviour
{
    [SerializeField] private WorldMovementController playerMovement;
    [SerializeField] private Collider2D ledge;
    [SerializeField][Min(2)] private int requiredStrength = 3;
    [SerializeField] private float margin = 0.15f;

    private void OnDrawGizmos()
    {
        if (playerMovement == null || ledge == null) return;

        Vector3 origin = transform.position;
        float height = ledge.bounds.max.y - origin.y;
        float tooWeak = playerMovement.JumpHeightAt(requiredStrength - 1);
        float strongEnough = playerMovement.JumpHeightAt(requiredStrength);
        bool valid = height > tooWeak + margin && height < strongEnough - margin;

        Gizmos.color = new Color(1f, 0.6f, 0f, 0.8f);
        DrawReach(origin, tooWeak);
        Gizmos.color = Color.cyan;
        DrawReach(origin, strongEnough);
        Gizmos.color = valid ? Color.green : Color.red;
        Gizmos.DrawLine(origin, new Vector3(origin.x, ledge.bounds.max.y, origin.z));

#if UNITY_EDITOR
        UnityEditor.Handles.Label(origin + Vector3.up * (height + 0.3f),
            $"STR {requiredStrength}: ledge {height:0.00}  (window {tooWeak:0.00}-{strongEnough:0.00})");
#endif
    }

    private static void DrawReach(Vector3 origin, float height)
    {
        Gizmos.DrawLine(origin + new Vector3(-0.75f, height), origin + new Vector3(0.75f, height));
    }
}