using UnityEngine;
using UnityEditor;

namespace VARCOVoice.Editor
{
    /// <summary>
    /// Custom inspector for VarcoTTS component
    /// </summary>
    [CustomEditor(typeof(VarcoTTS))]
    public class VarcoTTSInspector : UnityEditor.Editor
    {
        private string _testText = "안녕하세요. 바르코 보이스 테스트입니다.";
        
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Quick Actions", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            _testText = EditorGUILayout.TextField("Test Text:", _testText);
            
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Test TTS"))
            {
                TestTTS();
            }
            
            if (GUILayout.Button("Open Voice Picker"))
            {
                VoicePickerWindow.ShowWindow();
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
        }
        
        private async void TestTTS()
        {
            var tts = (VarcoTTS)target;
            
            if (!Application.isPlaying)
            {
                EditorUtility.DisplayDialog("VARCO Voice", "Enter Play Mode to test TTS.", "OK");
                return;
            }
            
            try
            {
                await tts.SpeakAsync(_testText);
            }
            catch (VarcoException ex)
            {
                EditorUtility.DisplayDialog("VARCO Voice", $"TTS failed: {ex.Message}", "OK");
            }
        }
    }
    
    /// <summary>
    /// Custom inspector for VarcoAudioSource component
    /// </summary>
    [CustomEditor(typeof(Audio.VarcoAudioSource))]
    public class VarcoAudioSourceInspector : UnityEditor.Editor
    {
        private string _testText = "테스트 문장입니다.";
        private bool _showPresets = false;
        
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            
            var audioSource = (Audio.VarcoAudioSource)target;
            
            EditorGUILayout.Space(10);
            
            // Voice Presets
            _showPresets = EditorGUILayout.Foldout(_showPresets, "Voice Effect Presets", true);
            
            if (_showPresets)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("🤖 Robot"))
                {
                    audioSource.ApplyRobotVoice();
                    EditorUtility.SetDirty(target);
                }
                if (GUILayout.Button("📻 Radio"))
                {
                    audioSource.ApplyRadioVoice();
                    EditorUtility.SetDirty(target);
                }
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("🦇 Cave"))
                {
                    audioSource.ApplyCaveVoice();
                    EditorUtility.SetDirty(target);
                }
                if (GUILayout.Button("🌊 Underwater"))
                {
                    audioSource.ApplyUnderwaterVoice();
                    EditorUtility.SetDirty(target);
                }
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("👻 Ghost"))
                {
                    audioSource.ApplyGhostVoice();
                    EditorUtility.SetDirty(target);
                }
                if (GUILayout.Button("🧹 Clear Effects"))
                {
                    audioSource.ClearEffects();
                    EditorUtility.SetDirty(target);
                }
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.EndVertical();
            }
            
            EditorGUILayout.Space(5);
            
            // Test Section
            EditorGUILayout.LabelField("Test", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            _testText = EditorGUILayout.TextField("Text:", _testText);
            
            GUI.enabled = Application.isPlaying;
            if (GUILayout.Button("Speak"))
            {
                TestSpeak(audioSource);
            }
            GUI.enabled = true;
            
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to test.", MessageType.Info);
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private async void TestSpeak(Audio.VarcoAudioSource audioSource)
        {
            try
            {
                await audioSource.SpeakAsync(_testText);
            }
            catch (VarcoException ex)
            {
                EditorUtility.DisplayDialog("VARCO Voice", $"TTS failed: {ex.Message}", "OK");
            }
        }
    }
    
    /// <summary>
    /// Custom inspector for LipSyncPlayer component
    /// </summary>
    [CustomEditor(typeof(LipSync.LipSyncPlayer))]
    public class LipSyncPlayerInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            
            var player = (LipSync.LipSyncPlayer)target;
            
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Lip Sync Tools", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            if (GUILayout.Button("Create Default Profile"))
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
