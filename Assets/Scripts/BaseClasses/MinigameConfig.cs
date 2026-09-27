using UnityEngine;

public abstract class MinigameConfig : ScriptableObject
{
    public string displayNameOverride;
    [TextArea(2, 4)] public string instructionsOverride;
}