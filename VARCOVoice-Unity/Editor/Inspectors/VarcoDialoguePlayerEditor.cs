using UnityEditor;
using UnityEngine;

namespace VARCOVoice.Editor
{
    [CustomEditor(typeof(VarcoDialoguePlayer))]
    public class VarcoDialoguePlayerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var source = (VarcoDialoguePlayer)target;
            int slotCount = source.dialogueSlots != null ? source.dialogueSlots.Count : 0;

            EditorGUILayout.LabelField("VARCO Audio Source", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Dialogue Slots: {slotCount}");
            EditorGUILayout.Space();

            if (GUILayout.Button("Open in Export Panel"))
            {
                var window = EditorWindow.GetWindow<VarcoVoiceMainWindow>();
                window.Focus();
                window.OpenExportFor(source);
            }

            EditorGUILayout.Space();
            DrawDefaultInspector();
        }
    }
}
