using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewCharacter", menuName = "Dialogue/Character Profile")]
public class CharacterProfile : ScriptableObject
{
    [Serializable]
    public class Expression
    {
        public string name;
        public Sprite portrait;
    }

    public string displayName = "NPC";
    public Color nameColor = Color.white;
    public Sprite defaultPortrait;
    public List<Expression> expressions = new List<Expression>();

    [Header("Voice")]
    public string blipKey;
    [Range(0.5f, 2f)] public float blipPitch = 1f;

    public bool HasExpression(string expressionName) => Find(expressionName) != null;

    public Sprite GetPortrait(string expressionName)
    {
        if (string.IsNullOrEmpty(expressionName)) return defaultPortrait;

        var expression = Find(expressionName);
        return expression != null && expression.portrait != null ? expression.portrait : defaultPortrait;
    }

    private Expression Find(string expressionName)
    {
        if (string.IsNullOrEmpty(expressionName)) return null;

        return expressions.Find(e => e != null && !string.IsNullOrEmpty(e.name) &&
            string.Equals(e.name.Trim(), expressionName.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}