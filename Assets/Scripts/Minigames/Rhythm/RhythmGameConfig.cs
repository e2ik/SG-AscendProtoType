using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "RhythmGameConfig", menuName = "Minigames/Rhythm Game Config")]
public class RhythmGameConfig : MinigameConfig
{
    [Header("Attempts")]
    [Min(1)] public int attempts = 3;

    [Header("Marker Speed (full up-and-down cycles per second)")]
    [Min(0.05f)] public float cyclesPerSecond = 0.8f;
    [Min(0f)] public float speedIncreasePerAttempt = 0.15f;

    [Header("Zones (fraction of the track)")]
    [Range(0.02f, 1f)] public float okaySize = 0.3f;
    [Range(0.01f, 1f)] public float goodSize = 0.16f;
    [Range(0.01f, 1f)] public float perfectSize = 0.06f;
    public bool randomizeZone = true;

    [Header("Reward")]
    [Min(0)] public int maxReward = 3;
    public List<float> rewardThresholds = new List<float> { 0.3f, 0.6f, 0.9f };
}