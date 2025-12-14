using UnityEditor;
using UnityEngine;

namespace ContinuousWorld
{
    [CustomEditor(typeof(MapPreview))]
    public class MapPreviewEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            MapPreview mapPreview = (MapPreview)target;

            EditorGUI.BeginChangeCheck();

            DrawDefaultInspector();

            if (EditorGUI.EndChangeCheck())
            {
                if (mapPreview.IsAutoUpdate)
                {
                    mapPreview.DrawMapInEditor();
                }
            }

            if (GUILayout.Button("Generate Map"))
            {
                mapPreview.DrawMapInEditor();
            }
        }
    }
}