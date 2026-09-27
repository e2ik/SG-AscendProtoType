using System.Collections.Generic;
using UnityEngine;

public class RunnerObstacleSpawner : MonoBehaviour
{
    [SerializeField] private List<RunnerObstacle> obstaclePrefabs = new List<RunnerObstacle>();
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private float despawnDistance = 30f;

    [Header("Spacing")]
    [SerializeField] private float minGap = 5f;
    [SerializeField] private float maxGap = 11f;
    [SerializeField] private float landingMargin = 1.25f;

    [Header("Fairness")]
    [SerializeField] [Range(0.3f, 1f)] private float clearanceFactor = 0.8f;

    private readonly List<Transform> _active = new List<Transform>();
    private readonly List<RunnerObstacle> _allowed = new List<RunnerObstacle>();
    private float _distanceUntilNext;

    public void Setup(float playerJumpHeight)
    {
        Clear();
        _allowed.Clear();

        float clearable = playerJumpHeight * clearanceFactor;
        RunnerObstacle smallest = null;

        foreach (var prefab in obstaclePrefabs)
        {
            if (prefab == null) continue;

            if (prefab.Height <= clearable)
                _allowed.Add(prefab);

            if (smallest == null || prefab.Height < smallest.Height)
                smallest = prefab;
        }

        if (_allowed.Count == 0 && smallest != null)
        {
            Debug.LogWarning($"No obstacle is short enough for a {playerJumpHeight:0.00} jump - using the smallest one", this);
            _allowed.Add(smallest);
        }

        _distanceUntilNext = minGap;
    }

    public void Tick(float speed, float deltaTime, float playerAirTime)
    {
        float distance = speed * deltaTime;
        float despawnX = SpawnPosition.x - despawnDistance;

        for (int i = _active.Count - 1; i >= 0; i--)
        {
            var obstacle = _active[i];
            if (obstacle == null)
            {
                _active.RemoveAt(i);
                continue;
            }

            obstacle.position += Vector3.left * distance;

            if (obstacle.position.x < despawnX)
            {
                Destroy(obstacle.gameObject);
                _active.RemoveAt(i);
            }
        }

        _distanceUntilNext -= distance;
        if (_distanceUntilNext > 0f || _allowed.Count == 0) return;

        Spawn();

        float fairGap = speed * playerAirTime * landingMargin;
        float low = Mathf.Max(minGap, fairGap);
        float high = Mathf.Max(maxGap, low + 1f);
        _distanceUntilNext = Random.Range(low, high);
    }

    public void Clear()
    {
        foreach (var obstacle in _active)
        {
            if (obstacle != null)
                Destroy(obstacle.gameObject);
        }
        _active.Clear();
    }

    private void Spawn()
    {
        var prefab = _allowed[Random.Range(0, _allowed.Count)];
        var instance = Instantiate(prefab, SpawnPosition, Quaternion.identity, transform);
        _active.Add(instance.transform);
    }

    private Vector3 SpawnPosition => spawnPoint != null ? spawnPoint.position : transform.position;

    private void OnDrawGizmosSelected()
    {
        var spawn = SpawnPosition;
        Gizmos.color = Color.green;
        Gizmos.DrawLine(spawn + Vector3.down, spawn + Vector3.up * 4f);
        Gizmos.color = Color.red;
        var despawn = spawn + Vector3.left * despawnDistance;
        Gizmos.DrawLine(despawn + Vector3.down, despawn + Vector3.up * 4f);
    }
}