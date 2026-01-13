using System;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using System.Collections.Generic;

namespace VARCOVoice.Editor
{
    /// <summary>
    /// Settings provider for VARCO Voice in Project Settings (UI Toolkit version)
    /// </summary>
    public class VarcoSettingsProvider : SettingsProvider
    {
        private const string SETTINGS_PATH = "Project/VARCO Voice";
        private const string API_KEY_PREF = "VARCOVoice_ApiKey";
        
        private SerializedObject _serializedConfig;
        private VarcoConfig _config;
        
        private string _apiKey = "";
        private bool _showApiKey = false;
        
        // Connection status
        private bool _isConnected = false;
        private int _voiceCount = 0;
        private string _lastSyncTime = "Never";
        
        // UI Elements
        private VisualElement _root;
        private TextField _apiKeyField;
        private Button _toggleKeyBtn;
        private VisualElement _statusDot;
        private Label _statusText;
        private Label _voiceCountLabel;
        private Label _lastSyncLabel;
        
        // Stats labels
        private Label _cacheEntriesLabel;
        private Label _cacheMemoryLabel;
        private ProgressBar _cacheUsageBar;
        private Label _apiCallsLabel;
        private Label _cacheHitsLabel;
        private Label _charactersLabel;
        private Label _creditsLiteLabel;
        private Label _creditsStandardLabel;
        private ProgressBar _hitRateBar;
        
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
        
        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            LoadSettings();
            
            // Load UXML
            var uxmlPath = "Packages/com.varco.voice/Editor/UI/VarcoSettingsPanel.uxml";
            var ussPath = "Packages/com.varco.voice/Editor/UI/VarcoSettingsPanel.uss";
            
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
            var uss = AssetDatabase.LoadAssetAtPath<StyleSheet>(ussPath);
            
            if (uxml == null)
            {
                // Fallback to local path
                var localPath = AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("VarcoSettingsPanel t:VisualTreeAsset")[0]);
                uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(localPath);
                
                var ussLocalPath = localPath.Replace(".uxml", ".uss");
                uss = AssetDatabase.LoadAssetAtPath<StyleSheet>(ussLocalPath);
            }
            
            if (uxml != null)
            {
                _root = uxml.Instantiate();
                if (uss != null)
                {
                    _root.styleSheets.Add(uss);
                }
                rootElement.Add(_root);
                
                CacheUIElements();
                SetupEventHandlers();
                RefreshUI();
            }
            else
            {
                // Fallback message
                var fallback = new Label("Failed to load VARCO Voice settings UI. Please reimport the package.");
                fallback.style.color = Color.red;
                rootElement.Add(fallback);
            }
        }
        
        private void CacheUIElements()
        {
            _apiKeyField = _root.Q<TextField>("api-key");
            _toggleKeyBtn = _root.Q<Button>("toggle-key-btn");
            _statusDot = _root.Q<VisualElement>("status-dot");
            _statusText = _root.Q<Label>("status-text");
            _voiceCountLabel = _root.Q<Label>("voice-count");
            _lastSyncLabel = _root.Q<Label>("last-sync");
            
            _cacheEntriesLabel = _root.Q<Label>("cache-entries");
            _cacheMemoryLabel = _root.Q<Label>("cache-memory");
            _cacheUsageBar = _root.Q<ProgressBar>("cache-usage-bar");
            
            _apiCallsLabel = _root.Q<Label>("api-calls");
            _cacheHitsLabel = _root.Q<Label>("cache-hits");
            _charactersLabel = _root.Q<Label>("characters");
            _creditsLiteLabel = _root.Q<Label>("credits-lite");
            _creditsStandardLabel = _root.Q<Label>("credits-standard");
            _hitRateBar = _root.Q<ProgressBar>("hit-rate-bar");
        }
        
        private void SetupEventHandlers()
        {
            // API Key controls
            if (_toggleKeyBtn != null)
            {
                _toggleKeyBtn.clicked += () =>
                {
                    _showApiKey = !_showApiKey;
                    if (_apiKeyField != null)
                    {
                        _apiKeyField.isPasswordField = !_showApiKey;
                    }
                    _toggleKeyBtn.text = _showApiKey ? "Hide" : "Show";
                };
            }
            
            if (_apiKeyField != null)
            {
                _apiKeyField.value = _apiKey;
                _apiKeyField.RegisterValueChangedCallback(evt => _apiKey = evt.newValue);
            }
            
            // Buttons
            var saveKeyBtn = _root.Q<Button>("save-key-btn");
            if (saveKeyBtn != null) saveKeyBtn.clicked += SaveApiKey;
            
            var testBtn = _root.Q<Button>("test-btn");
            if (testBtn != null) testBtn.clicked += TestConnection;
            
            var clearCacheBtn = _root.Q<Button>("clear-cache-btn");
            if (clearCacheBtn != null) clearCacheBtn.clicked += () =>
            {
                if (EditorUtility.DisplayDialog("Clear Cache", "Are you sure you want to delete all cached audio?", "Yes", "Cancel"))
                {
                    AudioCacheManager.Instance.ClearAll();
                    RefreshUI();
                }
            };
            
            var clearOldBtn = _root.Q<Button>("clear-old-btn");
            if (clearOldBtn != null) clearOldBtn.clicked += () =>
            {
                AudioCacheManager.Instance.RemoveOlderThan(TimeSpan.FromDays(7));
                RefreshUI();
            };
            
            var openFolderBtn = _root.Q<Button>("open-folder-btn");
            if (openFolderBtn != null) openFolderBtn.clicked += () =>
            {
                var stats = AudioCacheManager.Instance.GetStats();
                if (System.IO.Directory.Exists(stats.CacheDirectory))
                {
                    System.Diagnostics.Process.Start(stats.CacheDirectory);
                }
            };
            
            var resetMonthlyBtn = _root.Q<Button>("reset-monthly-btn");
            if (resetMonthlyBtn != null) resetMonthlyBtn.clicked += () =>
            {
                if (EditorUtility.DisplayDialog("Reset Monthly Stats", "Are you sure?", "Yes", "Cancel"))
                {
                    UsageTracker.Instance.ResetMonthlyStats();
                    RefreshUI();
                }
            };
            
            var resetAllBtn = _root.Q<Button>("reset-all-btn");
            if (resetAllBtn != null) resetAllBtn.clicked += () =>
            {
                if (EditorUtility.DisplayDialog("Reset All Statistics", "Are you sure?", "Yes", "Cancel"))
                {
                    UsageTracker.Instance.ResetStats();
                    RefreshUI();
                }
            };
            
            var openPickerBtn = _root.Q<Button>("open-picker-btn");
            if (openPickerBtn != null) openPickerBtn.clicked += () => VoicePickerWindow.ShowWindow();
            
            var createConfigBtn = _root.Q<Button>("create-config-btn");
            if (createConfigBtn != null) createConfigBtn.clicked += CreateConfigAsset;
            
            var apiPortalBtn = _root.Q<Button>("api-portal-btn");
            if (apiPortalBtn != null) apiPortalBtn.clicked += () => Application.OpenURL("https://api.varco.ai/ko");
            
            var docsBtn = _root.Q<Button>("docs-btn");
            if (docsBtn != null) docsBtn.clicked += () => LicenseViewerWindow.ShowWindow();
            
            // Reset All Data button
            var resetDataBtn = _root.Q<Button>("reset-data-btn");
            if (resetDataBtn != null) resetDataBtn.clicked += () =>
            {
                if (EditorUtility.DisplayDialog("Reset All Data", 
                    "This will clear:\n• All favorite voices\n• Recent voice history\n• Saved API key\n\nThis cannot be undone.", "Reset", "Cancel"))
                {
                    VoiceFavorites.ClearFavorites();
                    VoiceFavorites.ClearRecentVoices();
                    EditorPrefs.DeleteKey(API_KEY_PREF);
                    _apiKey = "";
                    if (_apiKeyField != null) _apiKeyField.value = "";
                    RefreshUI();
                    EditorUtility.DisplayDialog("VARCO Voice", "All data has been reset.", "OK");
                }
            };
            
            // Cache toggle
            var cacheToggle = _root.Q<Toggle>("cache-toggle");
            if (cacheToggle != null)
            {
                cacheToggle.value = AudioCacheManager.Instance.Enabled;
                cacheToggle.RegisterValueChangedCallback(evt =>
                {
                    AudioCacheManager.Instance.Enabled = evt.newValue;
                    EditorPrefs.SetBool("VARCOVoice_CacheEnabled", evt.newValue);
                });
            }
            
            // Cache size slider
            var cacheSizeSlider = _root.Q<SliderInt>("cache-size-slider");
            if (cacheSizeSlider != null)
            {
                cacheSizeSlider.value = (int)(AudioCacheManager.Instance.MaxCacheSizeBytes / (1024 * 1024));
                cacheSizeSlider.RegisterValueChangedCallback(evt =>
                {
                    AudioCacheManager.Instance.MaxCacheSizeBytes = evt.newValue * 1024L * 1024L;
                    EditorPrefs.SetFloat("VARCOVoice_MaxCacheSize", evt.newValue);
                    RefreshUI();
                });
            }
        }
        
        private void RefreshUI()
        {
            // Update connection status
            if (_statusDot != null)
            {
                _statusDot.RemoveFromClassList("status-dot--connected");
                _statusDot.RemoveFromClassList("status-dot--disconnected");
                _statusDot.AddToClassList(_isConnected ? "status-dot--connected" : "status-dot--disconnected");
            }
            
            if (_statusText != null)
            {
                _statusText.text = _isConnected ? "Connected" : "Disconnected";
            }
            
            if (_voiceCountLabel != null && _isConnected && _voiceCount > 0)
            {
                _voiceCountLabel.text = $"{_voiceCount:N0} voices available";
            }
            
            if (_lastSyncLabel != null)
            {
                _lastSyncLabel.text = $"Last sync: {_lastSyncTime}";
            }
            
            // Update cache stats
            try
            {
                var cacheStats = AudioCacheManager.Instance.GetStats();
                
                if (_cacheEntriesLabel != null)
                {
                    _cacheEntriesLabel.text = cacheStats.TotalEntries.ToString("N0");
                }
                
                if (_cacheMemoryLabel != null)
                {
                    _cacheMemoryLabel.text = cacheStats.MemoryCacheCount.ToString("N0");
                }
                
                if (_cacheUsageBar != null)
                {
                    _cacheUsageBar.value = cacheStats.UsagePercent;
                    _cacheUsageBar.title = $"{cacheStats.TotalSizeFormatted} / {cacheStats.MaxSizeFormatted} ({cacheStats.UsagePercent:F1}%)";
                }
            }
            catch { }
            
            // Update usage stats
            try
            {
                var tracker = UsageTracker.Instance;
                var monthly = tracker.GetCurrentMonthUsage();
                
                if (_apiCallsLabel != null)
                {
                    _apiCallsLabel.text = monthly.ApiCalls.ToString("N0");
                }
                
                if (_cacheHitsLabel != null)
                {
                    _cacheHitsLabel.text = monthly.CacheHits.ToString("N0");
                }
                
                if (_charactersLabel != null)
                {
                    _charactersLabel.text = monthly.Characters.ToString("N0");
                }
                
                if (_creditsLiteLabel != null)
                {
                    _creditsLiteLabel.text = tracker.CalculateCredits(monthly.Characters, true).ToString("N0");
                }
                
                if (_creditsStandardLabel != null)
                {
                    _creditsStandardLabel.text = tracker.CalculateCredits(monthly.Characters, false).ToString("N0");
                }
                
                if (_hitRateBar != null)
                {
                    float hitRate = monthly.CacheHitRate;
                    _hitRateBar.value = hitRate;
                    _hitRateBar.title = $"{hitRate:F1}% cache hit rate";
                }
            }
            catch { }
        }
        
        #region Methods
        
        private void LoadSettings()
        {
            _apiKey = EditorPrefs.GetString(API_KEY_PREF, "");
            _config = Resources.Load<VarcoConfig>("VarcoConfig");
            
            if (_config != null)
            {
                _serializedConfig = new SerializedObject(_config);
            }
        }
        
        private void SaveApiKey()
        {
            EditorPrefs.SetString(API_KEY_PREF, _apiKey);
            
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
                var tempConfig = ScriptableObject.CreateInstance<VarcoConfig>();
                tempConfig.SetApiKeyFromEditor(_apiKey);
                
                var client = new VarcoApiClient(tempConfig);
                var voices = await client.GetVoicesAsync();
                
                _isConnected = true;
                _voiceCount = voices.Count;
                _lastSyncTime = DateTime.Now.ToString("HH:mm:ss");
                
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("VARCO Voice", $"Connection successful!\nFound {voices.Count} voices.", "OK");
                
                UnityEngine.Object.DestroyImmediate(tempConfig);
                RefreshUI();
            }
            catch (VarcoException ex)
            {
                _isConnected = false;
                _voiceCount = 0;
                
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("VARCO Voice", $"Connection failed:\n{ex.Message}", "OK");
                RefreshUI();
            }
        }
        
        private void CreateConfigAsset()
        {
            var path = "Assets/Resources/VarcoConfig.asset";
            
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }
            
            var config = ScriptableObject.CreateInstance<VarcoConfig>();
            config.SetApiKeyFromEditor(_apiKey);
            
            AssetDatabase.CreateAsset(config, path);
            AssetDatabase.SaveAssets();
            
            _config = config;
            _serializedConfig = new SerializedObject(_config);
            
            EditorUtility.DisplayDialog("VARCO Voice", $"Created VarcoConfig at:\n{path}", "OK");
            Selection.activeObject = config;
        }
        
        #endregion
    }
}
