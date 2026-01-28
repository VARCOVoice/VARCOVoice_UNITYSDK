using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using VARCOVoice.DSP;

namespace VARCOVoice.Editor
{
    /// <summary>
    [CustomEditor(typeof(LipSync.LipSyncPlayer))]
    public class LipSyncPlayerInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            
            var player = (LipSync.LipSyncPlayer)target;
            
            EditorGUILayout.Space(10);
            VarcoEditorStyles.DrawSectionHeader("Lip Sync Tools");
            
            EditorGUILayout.BeginVertical(VarcoEditorStyles.CardStyle);
            
            if (VarcoEditorStyles.DrawAccentButton("Create Default Profile"))
            {
                CreateDefaultProfile();
            }
            
            EditorGUILayout.EndVertical();
            
            // Runtime info
            if (Application.isPlaying)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Runtime Info", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Is Playing: {player.IsPlaying}");
            }
        }
        
        private void CreateDefaultProfile()
        {
            var path = EditorUtility.SaveFilePanelInProject(
                "Save Lip Sync Profile",
                "LipSyncProfile",
                "asset",
                "Choose where to save the lip sync profile"
            );
            
            if (string.IsNullOrEmpty(path)) return;
            
            var profile = ScriptableObject.CreateInstance<LipSync.LipSyncProfile>();
            profile.SetupDefaultMappings();
            
            AssetDatabase.CreateAsset(profile, path);
            AssetDatabase.SaveAssets();
            
            Selection.activeObject = profile;
        }
    }
}

