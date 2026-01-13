using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace VARCOVoice.Editor
{
    /// <summary>
    /// Settings provider for VARCO Voice in Project Settings
    /// </summary>
    public class VarcoSettingsProvider : SettingsProvider
    {
        private const string SETTINGS_PATH = "Project/VARCO Voice";
        private const string API_KEY_PREF = "VARCOVoice_ApiKey";
        
        private SerializedObject _serializedConfig;
        private VarcoConfig _config;
        
        private string _apiKey = "";
        private bool _showApiKey = false;
        
        public VarcoSettingsProvider(string path, SettingsScope scopes)
            : base(path, scopes) { }
        
        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            var provider = new VarcoSettingsProvider(SETTINGS_PATH, SettingsScope.Project)
            {
                label = "VARCO Voice",
                keywords = new HashSet<string>(new[] { "VARCO", "Voice", "TTS", "Text-to-Speech", "Audio" })
            };
            
            return provider;
        }
        
        public override void OnActivate(string searchContext, UnityEngine.UIElements.VisualElement rootElement)
        {
            LoadSettings();
        }
        
        public override void OnGUI(string searchContext)
        {
            EditorGUILayout.Space(10);
            
            DrawHeader();
            
            EditorGUILayout.Space(10);
            
            DrawApiKeySection();
            
            EditorGUILayout.Space(10);
            
            DrawDefaultsSection();
            
            EditorGUILayout.Space(10);
            
            DrawQualitySection();
            
            EditorGUILayout.Space(10);
            
            DrawCacheSection();
            
            EditorGUILayout.Space(10);
            
            DrawToolsSection();
            
            EditorGUILayout.Space(10);
            
            DrawLinksSection();
        }
        
        #region GUI Sections
        
        private void DrawHeader()
        {
            EditorGUILayout.LabelField("VARCO Voice Settings", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Configure your VARCO Voice TTS integration. You need a valid API key from the VARCO Voice Console.",
                MessageType.Info
            );
        }
        
        private void DrawApiKeySection()
        {
            EditorGUILayout.LabelField("API Configuration", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("API Key:", GUILayout.Width(80));
            
            if (_showApiKey)
            {
                _apiKey = EditorGUILayout.TextField(_apiKey);
            }
            else
            {
                EditorGUILayout.PasswordField(_apiKey);
            }
            
            if (GUILayout.Button(_showApiKey ? "Hide" : "Show", GUILayout.Width(50)))
            {
                _showApiKey = !_showApiKey;
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Save API Key"))
            {
                SaveApiKey();
            }
            
            if (GUILayout.Button("Test Connection"))
            {
                TestConnection();
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawDefaultsSection()
        {
            EditorGUILayout.LabelField("Default Settings", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            if (_config != null && _serializedConfig != null)
            {
                _serializedConfig.Update();
                
                EditorGUILayout.PropertyField(
                    _serializedConfig.FindProperty("defaultModel"),
                    new GUIContent("TTS Model", "Standard = Higher quality, Lite = Faster")
                );
                
                EditorGUILayout.PropertyField(
                    _serializedConfig.FindProperty("defaultVoice"),
                    new GUIContent("Default Voice", "Default speaker name for TTS")
                );
                
                EditorGUILayout.PropertyField(
                    _serializedConfig.FindProperty("defaultLanguage"),
                    new GUIContent("Language", "Default language for TTS")
                );
                
                _serializedConfig.ApplyModifiedProperties();
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawQualitySection()
        {
            EditorGUILayout.LabelField("Quality Settings", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            if (_config != null && _serializedConfig != null)
            {
                _serializedConfig.Update();
                
                EditorGUILayout.PropertyField(
                    _serializedConfig.FindProperty("qualityLevel"),
                    new GUIContent("Quality Level", "8 = Fast, 20 = Best quality")
                );
                
                EditorGUILayout.PropertyField(
                    _serializedConfig.FindProperty("defaultSpeed"),
                    new GUIContent("Speed", "Speech speed (0.8-1.2 recommended)")
                );
                
                EditorGUILayout.PropertyField(
                    _serializedConfig.FindProperty("defaultPitch"),
                    new GUIContent("Pitch", "Speech pitch (0.8-1.2 recommended)")
                );
                
                _serializedConfig.ApplyModifiedProperties();
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawCacheSection()
        {
            EditorGUILayout.LabelField("Cache Settings", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            if (_config != null && _serializedConfig != null)
            {
                _serializedConfig.Update();
                
                EditorGUILayout.PropertyField(
                    _serializedConfig.FindProperty("enableCache"),
                    new GUIContent("Enable Cache", "Cache generated audio locally")
                );
                
                EditorGUILayout.PropertyField(
                    _serializedConfig.FindProperty("maxCacheSizeMB"),
                    new GUIContent("Max Cache Size (MB)", "Maximum cache size in megabytes")
                );
                
                _serializedConfig.ApplyModifiedProperties();
            }
            
            if (GUILayout.Button("Clear Audio Cache"))
            {
                ClearCache();
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawToolsSection()
        {
            EditorGUILayout.LabelField("Tools", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            if (GUILayout.Button("Open Voice Picker"))
            {
                VoicePickerWindow.ShowWindow();
            }
            
            if (GUILayout.Button("Create VarcoConfig Asset"))
            {
                CreateConfigAsset();
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawLinksSection()
        {
            EditorGUILayout.LabelField("Links", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            if (GUILayout.Button("VARCO Voice Console (Get API Key)"))
            {
                Application.OpenURL("https://voice.varco.ai");
            }
            
            if (GUILayout.Button("Documentation"))
            {
                Application.OpenURL("https://github.com/your-repo/VARCOVoice-Unity");
            }
            
            EditorGUILayout.EndVertical();
        }
        
        #endregion
        
        #region Methods
        
        private void LoadSettings()
        {
            // Load API key from EditorPrefs
            _apiKey = EditorPrefs.GetString(API_KEY_PREF, "");
            
            // Find or create config
            _config = Resources.Load<VarcoConfig>("VarcoConfig");
            
            if (_config != null)
            {
                _serializedConfig = new SerializedObject(_config);
            }
        }
        
        private void SaveApiKey()
        {
            EditorPrefs.SetString(API_KEY_PREF, _apiKey);
            
            // Also update config if exists
            if (_config != null)
            {
                _config.SetApiKeyFromEditor(_apiKey);
                AssetDatabase.SaveAssets();
            }
            
            EditorUtility.DisplayDialog("VARCO Voice", "API Key saved successfully!", "OK");
        }
        
        private async void TestConnection()
        {
            if (string.IsNullOrEmpty(_apiKey))
            {
                EditorUtility.DisplayDialog("VARCO Voice", "Please enter an API key first.", "OK");
                return;
            }
            
            EditorUtility.DisplayProgressBar("VARCO Voice", "Testing connection...", 0.5f);
            
            try
            {
                // Create temporary config with the API key
                var tempConfig = ScriptableObject.CreateInstance<VarcoConfig>();
                tempConfig.SetApiKeyFromEditor(_apiKey);
                
                var client = new VarcoApiClient(tempConfig);
                var voices = await client.GetVoicesAsync();
                
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog(
                    "VARCO Voice",
                    $"Connection successful!\nFound {voices.Count} voices.",
                    "OK"
                );
                
                Object.DestroyImmediate(tempConfig);
            }
            catch (VarcoException ex)
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog(
                    "VARCO Voice",
                    $"Connection failed:\n{ex.Message}",
                    "OK"
                );
            }
        }
        
        private void CreateConfigAsset()
        {
            var path = "Assets/Resources/VarcoConfig.asset";
            
            // Ensure Resources folder exists
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }
            
            // Create config
            var config = ScriptableObject.CreateInstance<VarcoConfig>();
            config.SetApiKeyFromEditor(_apiKey);
            
            AssetDatabase.CreateAsset(config, path);
            AssetDatabase.SaveAssets();
            
            _config = config;
            _serializedConfig = new SerializedObject(_config);
            
            EditorUtility.DisplayDialog(
                "VARCO Voice",
                $"Created VarcoConfig at:\n{path}",
                "OK"
            );
            
            Selection.activeObject = config;
        }
        
        private void ClearCache()
        {
            // Clear audio cache (implement based on your caching strategy)
            var cachePath = Application.temporaryCachePath + "/VARCOVoice";
            if (System.IO.Directory.Exists(cachePath))
            {
                System.IO.Directory.Delete(cachePath, true);
            }
            
            EditorUtility.DisplayDialog("VARCO Voice", "Cache cleared!", "OK");
        }
        
        #endregion
    }
}
