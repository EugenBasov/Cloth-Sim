using UnityEngine;
using UnityEditor;

[CustomPropertyDrawer(typeof(BoneSetting))]
public class BoneSettingDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUI.GetPropertyHeight(property, label, true);
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var nameProp = property.FindPropertyRelative("name");
        string headerName = "Element";
        if (!string.IsNullOrEmpty(nameProp.stringValue))
        {
            headerName = nameProp.stringValue;
        }
        EditorGUI.PropertyField(position, property, new GUIContent(headerName), true);
    }
}