using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(UpdatableData), true)]
public class UpdatableDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
         
        UpdatableData updatableData = (UpdatableData)target;
        if (GUILayout.Button("Update Data"))
        {
            updatableData.NotifyOfUpdatedValues();
            EditorUtility.SetDirty(updatableData);
        }
    }
}
