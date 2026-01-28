using UnityEditor;
using UnityEngine;
using System;

namespace VARCOVoice.Editor
{
    public class PresetNameDialog : EditorWindow
    {
        private string _presetName = "New Preset";
        private Action<string> _onConfirm;
        
        public static void ShowDialog(string title, string defaultName, Action<string> onConfirm)
        {
            var window = GetWindow<PresetNameDialog>(true, title, true);
            window._presetName = defaultName;
            window._onConfirm = onConfirm;
            window.minSize = new Vector2(300, 80);
            window.maxSize = new Vector2(300, 80);
            window.CenterOnMainWin();
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            GUI.SetNextControlName("NameField");
            _presetName = EditorGUILayout.TextField("Preset Name", _presetName);
            
            // Auto focus
            if (Event.current.type == EventType.Repaint && GUI.GetNameOfFocusedControl() == "")
            {
                EditorGUI.FocusTextInControl("NameField");
            }

            EditorGUILayout.Space(10);
            
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Cancel", GUILayout.Width(70)))
                {
                    Close();
                }
                if (GUILayout.Button("Save", GUILayout.Width(70)) || (Event.current.isKey && Event.current.keyCode == KeyCode.Return))
                {
                    if (!string.IsNullOrWhiteSpace(_presetName))
                    {
                        _onConfirm?.Invoke(_presetName);
                        Close();
                    }
                }
            }
        }
        
        private void CenterOnMainWin()
        {
            Rect main = EditorGUIUtility.GetMainWindowPosition();
            Rect pos = position;
            float centerWidth = (main.width - pos.width) * 0.5f;
            float centerHeight = (main.height - pos.height) * 0.5f;
            position = new Rect(main.x + centerWidth, main.y + centerHeight, pos.width, pos.height);
        }
    }
}
