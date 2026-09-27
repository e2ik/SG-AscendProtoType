using UnityEngine;

public class LoopingScroller : MonoBehaviour
{
    [SerializeField] private float speedMultiplier = 1f;
    [SerializeField] private float segmentWidth = 20f;

    public void Tick(float speed, float deltaTime)
    {
        int count = transform.childCount;
        if (count == 0 || segmentWidth <= 0f) return;

        float distance = speed * speedMultiplier * deltaTime;

        for (int i = 0; i < count; i++)
        {
            var child = transform.GetChild(i);
            var position = child.localPosition;
            position.x -= distance;

            if (position.x <= -segmentWidth)
                position.x += segmentWidth * count;

            child.localPosition = position;
        }
    }
}