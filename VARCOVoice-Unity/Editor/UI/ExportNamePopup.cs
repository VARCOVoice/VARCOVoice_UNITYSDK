using UnityEditor;
using UnityEngine;

namespace VARCOVoice.Editor
{
    /// <summary>
    /// Simple popup window for entering export name
    /// </summary>
    public class ExportNamePopup : EditorWindow
    {
        private string _name;
        private System.Action<string> _onConfirm;
        private bool _focusSet;

        public static void Show(string defaultName, System.Action<string> onConfirm)
        {
            var window = CreateInstance<ExportNamePopup>();
            window._name = defaultName;
            window._onConfirm = onConfirm;
            window.titleContent = new GUIContent("Export to Library");
            window.minSize = new Vector2(300, 80);
            window.maxSize = new Vector2(300, 80);
            window.ShowUtility();
            window.position = new Rect(
                (Screen.currentResolution.width - 300) / 2,
                (Screen.currentResolution.height - 80) / 2,
                300, 80
            );
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8);

            EditorGUILayout.LabelField("Export Name:", EditorStyles.boldLabel);
            
            GUI.SetNextControlName("NameField");
            _name = EditorGUILayout.TextField(_name);

            if (!_focusSet)
            {
                EditorGUI.FocusTextInControl("NameField");
                _focusSet = true;
            }

            EditorGUILayout.Space(8);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Cancel", GUILayout.Width(80)))
            {
                Close();
            }

            if (GUILayout.Button("Export", GUILayout.Width(80)))
            {
                _onConfirm?.Invoke(_name);
                Close();
            }

            EditorGUILayout.EndHorizontal();

            // Handle Enter key
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return)
            {
                _onConfirm?.Invoke(_name);
                Close();
                Event.current.Use();
            }

            // Handle Escape key
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                Close();
                Event.current.Use();
            }
        }
    }
}
