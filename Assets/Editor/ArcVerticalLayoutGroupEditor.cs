using UnityEditor;
using UnityEditor.UI;

[CustomEditor(typeof(ArcVerticalLayoutGroup), true)]
[CanEditMultipleObjects]
public class ArcVerticalLayoutGroupEditor : HorizontalOrVerticalLayoutGroupEditor
{
    private SerializedProperty _arcAmount;
    private SerializedProperty _baseOffset;
    private SerializedProperty _flipDirection;
    private SerializedProperty _pairStagger;
    private SerializedProperty _snapToPixels;

    protected override void OnEnable()
    {
        base.OnEnable();
        _arcAmount = serializedObject.FindProperty("arcAmount");
        _baseOffset = serializedObject.FindProperty("baseOffset");
        _flipDirection = serializedObject.FindProperty("flipDirection");
        _pairStagger = serializedObject.FindProperty("pairStagger");
        _snapToPixels = serializedObject.FindProperty("snapToPixels");
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        serializedObject.Update();
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Arc", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_arcAmount);
        EditorGUILayout.PropertyField(_baseOffset);
        EditorGUILayout.PropertyField(_flipDirection);
        EditorGUILayout.PropertyField(_pairStagger);
        EditorGUILayout.PropertyField(_snapToPixels);
        serializedObject.ApplyModifiedProperties();
    }
}