using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "MemoryGameConfig", menuName = "Minigames/Memory Game Config")]
public class MemoryGameConfig : MinigameConfig
{
    [Header("Grid")]
    [Min(1)] public int columns = 4;
    [Min(1)] public int rows = 3;

    [Header("Difficulty")]
    [Min(0f)] public float missesPerPair = 0.6f;

    [Header("Reward")]
    [Min(0)] public int maxReward = 3;
    [Min(0.1f)] public float secondsPerPair = 2f;
    [Min(0.1f)] public float rewardFalloffSeconds = 30f;

    [Header("Optional")]
    public List<Sprite> faceSprites = new List<Sprite>();
}