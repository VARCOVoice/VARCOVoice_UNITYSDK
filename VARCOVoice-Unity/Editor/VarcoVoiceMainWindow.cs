using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using VARCOVoice.DSP;

namespace VARCOVoice.Editor
{
    /// <summary>
    /// VARCO Voice Main Window - UI Toolkit based unified editor
    /// Phase 1: Foundation with tab navigation
    /// </summary>
    public class VarcoVoiceMainWindow : EditorWindow
    {
        #region Constants
        
        private const string WINDOW_TITLE = "VARCO Voice";
        private const string UXML_PATH = "Packages/com.varco.voice/Editor/UI/VarcoMainWindow.uxml";
        private const string USS_PATH = "Packages/com.varco.voice/Editor/UI/VarcoMainWindow.uss";
        private const string PREF_LIGHT_MODE = "VARCO_LightMode";
        
        // Tab indices
        private const int TAB_TTS = 0;
        private const int TAB_DSP = 1;
        private const int TAB_EXPORT = 2;
        
        #endregion
        
        #region UI Elements
        
        private VisualElement _root;
        private Button[] _tabButtons;
        private VisualElement[] _panels;
        private VisualElement _connectionIndicator;
        private Label _connectionStatus;
        private Label _statusMessage;
        
        private int _currentTab = TAB_TTS;
        
        private TTSPanelController _ttsPanelController;
        private DSPPanelController _dspPanelController;
        private ExportPanelController _exportPanelController;
        
        #endregion
        
        #region Menu Item
        
        [MenuItem("Window/VARCO Voice/Main Window", false, 0)]
        public static VarcoVoiceMainWindow ShowWindow()
        {
            var window = GetWindow<VarcoVoiceMainWindow>();
            window.titleContent = new GUIContent(WINDOW_TITLE, VarcoEditorIcons.GetIcon("varco_logo"));
            window.minSize = new Vector2(1280, 720); // 16:9 Aspect Ratio (HD standard)
            window.Show();
            return window;
        }
        
        #endregion
        
        #region Unity Lifecycle
        
        private void CreateGUI()
        {
            _root = rootVisualElement;
            
            // Load UXML
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UXML_PATH);
            if (visualTree == null)
            {
                // Fallback: try loading from relative path in project
                visualTree = LoadUXMLFromProjectPath();
            }
            
            if (visualTree != null)
            {
                visualTree.CloneTree(_root);
            }
            else
            {
                // Create fallback UI if UXML not found
                CreateFallbackUI();
                // Even nicely created fallback UI needs caching and setup
                // But CreateFallbackUI below manually adds elements to _root.
                // If visualTree is null, we assume CreateFallbackUI set up the structure.
                // To avoid double-init or null refs, we check if we need to proceed.
                if (visualTree == null) return; 
            }
            
            // Load USS
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(USS_PATH);
            if (styleSheet == null)
            {
                styleSheet = LoadUSSFromProjectPath();
            }
            
            if (styleSheet != null)
            {
                _root.styleSheets.Add(styleSheet);
            }
            
            // Cache UI elements
            CacheUIElements();
            
            // Setup event handlers
            SetupEventHandlers();
            
            // Initialize state
            UpdateConnectionStatus(false, "Checking connection...");
            SelectTab(TAB_TTS);
            
            // Apply saved theme (uses VarcoTheme for global sync)
            VarcoTheme.Subscribe(_root);
            
            // Initialize panel controllers
            InitializePanelControllers();
            
            // Check API connection
            CheckConnectionAsync();
        }
        
        private void InitializePanelControllers()
        {
            // Initialize TTS Panel
            var ttsPanel = _panels[TAB_TTS];
            if (ttsPanel != null)
            {
                // Load TTS Panel UXML
                var ttsPanelUxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                    "Packages/com.varco.voice/Editor/UI/Panels/TTSPanel.uxml");
                
                if (ttsPanelUxml == null)
                {
                    // Fallback path search
                     ttsPanelUxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                        "Assets/VARCOVoice-Unity/Editor/UI/Panels/TTSPanel.uxml");
                }

                if (ttsPanelUxml != null)
                {
                    ttsPanel.Clear();
                    ttsPanelUxml.CloneTree(ttsPanel);
                    
                    // Load TTS Panel styles
                    var ttsPanelStyle = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                        "Packages/com.varco.voice/Editor/UI/Panels/TTSPanel.uss");
                    if (ttsPanelStyle != null)
                    {
                        ttsPanel.styleSheets.Add(ttsPanelStyle);
                    }
                    else
                    {
                         // Fallback style
                         ttsPanelStyle = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                        "Assets/VARCOVoice-Unity/Editor/UI/Panels/TTSPanel.uss");
                        if (ttsPanelStyle != null) ttsPanel.styleSheets.Add(ttsPanelStyle);
                    }
                    
                    // Initialize controller
                    _ttsPanelController = new TTSPanelController();
                    _ttsPanelController.Initialize(ttsPanel);
                    
                    // Connect TTS -> DSP flow
                    _ttsPanelController.OnSendToDSP += HandleSendToDSP;
                }
            }

            // Initialize DSP Panel (Integrated)
            var dspPanel = _panels[TAB_DSP];
            if (dspPanel != null)
            {
                _dspPanelController = new DSPPanelController();
                _dspPanelController.Initialize(dspPanel);
                
                _dspPanelController.OnRequestTabChange += SelectTab;
                _dspPanelController.OnQuickExport += HandleQuickExport;
            }

            // Initialize Export Panel
            var exportPanel = _panels[TAB_EXPORT];
            if (exportPanel != null)
            {
                var exportPanelUxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                    "Packages/com.varco.voice/Editor/UI/Panels/ExportPanel.uxml");

                if (exportPanelUxml == null)
                {
                    exportPanelUxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                        "Assets/VARCOVoice-Unity/Editor/UI/Panels/ExportPanel.uxml");
                }

                if (exportPanelUxml != null)
                {
                    exportPanel.Clear();
                    exportPanelUxml.CloneTree(exportPanel);
 
                    var exportPanelStyle = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                        "Packages/com.varco.voice/Editor/UI/Panels/ExportPanel.uss");
                    if (exportPanelStyle == null)
                    {
                        exportPanelStyle = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                            "Assets/VARCOVoice-Unity/Editor/UI/Panels/ExportPanel.uss");
                    }
                    if (exportPanelStyle != null)
                    {
                        exportPanel.styleSheets.Add(exportPanelStyle);
                    }

                    _exportPanelController = new ExportPanelController();
                    _exportPanelController.Initialize(exportPanel, GetCurrentExportClip());
                }
            }
        }
        
        private void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
        }
        
        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            
            // Cleanup panel controllers
            // Cleanup panel controllers
            if (_ttsPanelController != null)
            {
                _ttsPanelController.OnSendToDSP -= HandleSendToDSP;
                _ttsPanelController.Cleanup();
            }
            if (_dspPanelController != null)
            {
                _dspPanelController.OnRequestTabChange -= SelectTab;
                _dspPanelController.OnQuickExport -= HandleQuickExport;
                _dspPanelController.Cleanup();
            }
            _exportPanelController?.Cleanup();
        }
        
        private void OnEditorUpdate()
        {
            // Periodic updates can be added here
            if (_dspPanelController != null)
            {
                _dspPanelController.UpdateLoop();
            }
        }
        
        #endregion
        
        #region UI Setup
        
        private void CacheUIElements()
        {
            // Tab buttons (3 tabs)
            _tabButtons = new Button[3];
            _tabButtons[TAB_TTS] = _root.Q<Button>("tab-tts");
            _tabButtons[TAB_DSP] = _root.Q<Button>("tab-dsp");
            _tabButtons[TAB_EXPORT] = _root.Q<Button>("tab-export");
            
            // Panels (3 tabs)
            _panels = new VisualElement[3];
            _panels[TAB_TTS] = _root.Q<VisualElement>("panel-tts");
            _panels[TAB_DSP] = _root.Q<VisualElement>("panel-dsp");
            _panels[TAB_EXPORT] = _root.Q<VisualElement>("panel-export");

            // Enforce Full Layout
            foreach (var panel in _panels)
            {
                if (panel != null)
                {
                    panel.style.alignItems = Align.Stretch;
                    panel.style.justifyContent = Justify.FlexStart;
                }
            }
            
            // Status bar
            _connectionIndicator = _root.Q<VisualElement>("connection-indicator");
            _connectionStatus = _root.Q<Label>("connection-status");
            _statusMessage = _root.Q<Label>("status-message");
            
            // Clean up LipSync items if they exist in UXML
            var lipSyncTab = _root.Q<Button>("tab-lipsync");
            if (lipSyncTab != null) lipSyncTab.style.display = DisplayStyle.None;
            var lipSyncPanel = _root.Q<VisualElement>("panel-lipsync");
            if (lipSyncPanel != null) lipSyncPanel.style.display = DisplayStyle.None;
        }
        
        private void SetupEventHandlers()
        {
            // Tab navigation
            if (_tabButtons[TAB_TTS] != null)
                _tabButtons[TAB_TTS].clicked += () => SelectTab(TAB_TTS);
            if (_tabButtons[TAB_DSP] != null)
                _tabButtons[TAB_DSP].clicked += () => SelectTab(TAB_DSP);
            if (_tabButtons[TAB_EXPORT] != null)
                _tabButtons[TAB_EXPORT].clicked += () => SelectTab(TAB_EXPORT);
            
            // Settings button
            var settingsBtn = _root.Q<Button>("settings-btn");
            if (settingsBtn != null)
            {
                settingsBtn.clicked += OpenSettings;
            }
        }
        
        private void HandleSendToDSP(AudioClip clip)
        {
            if (_dspPanelController != null)
            {
                SelectTab(TAB_DSP);
                _dspPanelController.LoadAudio(clip);
                UpdateStatusMessage($"Loaded '{clip.name}' into DSP Studio");
            }
        }

        private void HandleQuickExport()
        {
            if (_dspPanelController == null || _dspPanelController.Target == null || _dspPanelController.CurrentClip == null)
            {
                UpdateStatusMessage("Error: No audio or DSP chain to export");
                return;
            }

            var clip = _dspPanelController.CurrentClip;
            var chain = _dspPanelController.Target;
            
            string filename = $"QuickExport_{clip.name}";
            string path = EditorUtility.SaveFilePanel("Quick Export WAV", Application.dataPath, filename, "wav");
            
            if (string.IsNullOrEmpty(path)) return;

            // Get active effects from chain
            List<IDSPEffect> activeEffects = new List<IDSPEffect>();
            foreach (var effect in chain.Effects)
            {
                if (effect.Enabled) activeEffects.Add(effect);
            }

            if (AudioBaker.Bake(clip, activeEffects, path))
            {
                UpdateStatusMessage($"Quick Export Success: {System.IO.Path.GetFileName(path)}");
            }
            else
            {
                UpdateStatusMessage("Quick Export Failed");
            }
        }

        private AudioClip GetCurrentExportClip()
        {
            if (_ttsPanelController != null && _ttsPanelController.CurrentClip != null)
            {
                return _ttsPanelController.CurrentClip;
            }

            if (_dspPanelController != null && _dspPanelController.CurrentClip != null)
            {
                return _dspPanelController.CurrentClip;
            }

            return null;
        }

        public void OpenExportFor(VarcoDialoguePlayer source)
        {
            if (source == null) return;

            SelectTab(TAB_EXPORT);

            if (_exportPanelController != null)
            {
                _exportPanelController.SetCurrentClip(GetCurrentExportClip());
                _exportPanelController.SelectSource(source);
            }
            else
            {
                EditorApplication.delayCall += () =>
                {
                    if (_exportPanelController == null) return;
                    _exportPanelController.SetCurrentClip(GetCurrentExportClip());
                    _exportPanelController.SelectSource(source);
                };
            }
        }



        public void OpenExportTab()
        {
            SelectTab(TAB_EXPORT);
        }

        public void OpenTab(int tabIndex)
        {
            SelectTab(tabIndex);
        }
        
        /// <summary>
        /// Sets the voice in TTS panel and switches to TTS tab
        /// Called from VoicePickerWindow
        /// </summary>
        public void SetVoiceAndSwitchToTTS(string voiceName)
        {
            SelectTab(TAB_TTS);
            
            if (_ttsPanelController != null)
            {
                _ttsPanelController.SetVoice(voiceName);
            }
            else
            {
                // Delay if controller not yet initialized
                EditorApplication.delayCall += () =>
                {
                    _ttsPanelController?.SetVoice(voiceName);
                };
            }
        }

        #endregion
        
        #region Tab Navigation
        
        private void SelectTab(int tabIndex)
        {
            _currentTab = tabIndex;
            
            // Update tab button styles
            for (int i = 0; i < _tabButtons.Length; i++)
            {
                if (_tabButtons[i] == null) continue;
                
                if (i == tabIndex)
                {
                    _tabButtons[i].AddToClassList("tab-button--active");
                }
                else
                {
                    _tabButtons[i].RemoveFromClassList("tab-button--active");
                }
            }
            
            // Show/hide panels
            for (int i = 0; i < _panels.Length; i++)
            {
                if (_panels[i] == null) continue;
                
                if (i == tabIndex)
                {
                    _panels[i].AddToClassList("panel--active");
                    _panels[i].style.display = DisplayStyle.Flex; // Fallback ensure visible
                }
                else
                {
                    _panels[i].RemoveFromClassList("panel--active");
                    _panels[i].style.display = DisplayStyle.None; // Fallback ensure hidden
                }
            }
            
            if (tabIndex == TAB_EXPORT && _exportPanelController != null)
            {
                _exportPanelController.SetCurrentClip(GetCurrentExportClip());
                _exportPanelController.RefreshObjectList();
            }

            UpdateStatusMessage(GetTabName(tabIndex) + " panel selected");
        }
        
        private string GetTabName(int tabIndex)
        {
            return tabIndex switch
            {
                TAB_TTS => "TTS",
                TAB_DSP => "FX Studio",
                TAB_EXPORT => "Export",
                _ => "Unknown"
            };
        }
        
        #endregion
        
        #region Status Bar
        
        private void UpdateConnectionStatus(bool isConnected, string message = null)
        {
            if (_connectionIndicator != null)
            {
                _connectionIndicator.RemoveFromClassList("status-dot--connected");
                _connectionIndicator.RemoveFromClassList("status-dot--disconnected");
                _connectionIndicator.AddToClassList(isConnected ? "status-dot--connected" : "status-dot--disconnected");
            }
            
            if (_connectionStatus != null)
            {
                _connectionStatus.text = isConnected ? "Connected" : "Disconnected";
            }
            
            if (message != null)
            {
                UpdateStatusMessage(message);
            }
        }
        
        private void UpdateStatusMessage(string message)
        {
            if (_statusMessage != null)
            {
                _statusMessage.text = message;
            }
        }
        
        #endregion
        
        #region Settings
        
        private void OpenSettings()
        {
            var menu = new GenericMenu();
            
            // Theme toggle - uses VarcoTheme for global sync
            menu.AddItem(new GUIContent("Light Mode"), VarcoTheme.IsLightMode, () =>
            {
                VarcoTheme.IsLightMode = !VarcoTheme.IsLightMode;
            });
            
            menu.AddSeparator("");
            
            menu.AddItem(new GUIContent("API Settings"), false, () =>
            {
                SettingsService.OpenProjectSettings("Project/VARCO Voice");
            });

            menu.AddSeparator("");

            menu.AddItem(new GUIContent("Documentation"), false, () =>
            {
                EditorUtility.DisplayDialog("VARCO Voice", "VARCO Voice Unity SDK\nVersion 1.0.0\n\n(c) NC AI", "OK");
            });
            
            menu.AddItem(new GUIContent("About VARCO Voice"), false, () =>
            {
                Application.OpenURL("https://voice.varco.ai/");
            });
            
            menu.ShowAsContext();
        }
        
        [MenuItem("Window/VARCO Voice/Settings", false, 100)]
        public static void OpenSettingsWindow()
        {
            SettingsService.OpenProjectSettings("Project/VARCO Voice");
        }
        
        #endregion
        
        #region Connection
        
        private async void CheckConnectionAsync()
        {
            try
            {
                var config = VarcoConfig.Instance;
                if (string.IsNullOrEmpty(config?.ApiKey))
                {
                    UpdateConnectionStatus(false, "API Key not configured");
                    return;
                }
                
                // Try to check connection via existing API client
                var client = new VarcoApiClient(config);
                if (client != null)
                {
                    var voices = await client.GetVoicesAsync();
                    if (voices != null && voices.Count > 0)
                    {
                        UpdateConnectionStatus(true, $"Ready - {voices.Count} voices available");
                    }
                    else
                    {
                        UpdateConnectionStatus(false, "No voices found");
                    }
                }
                else
                {
                    UpdateConnectionStatus(false, "API client not initialized");
                }
            }
            catch (System.Exception ex)
            {
                UpdateConnectionStatus(false, $"Connection error: {ex.Message}");
            }
        }
        
        #endregion
        
        #region Fallback UI
        
        private VisualTreeAsset LoadUXMLFromProjectPath()
        {
            // Try common project paths
            string[] paths = {
                "Assets/VARCOVoice-Unity/Editor/UI/VarcoMainWindow.uxml",
                "Assets/Plugins/VARCOVoice/Editor/UI/VarcoMainWindow.uxml"
            };
            
            foreach (var path in paths)
            {
                var asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path);
                if (asset != null) return asset;
            }
            
            return null;
        }
        
        private StyleSheet LoadUSSFromProjectPath()
        {
            string[] paths = {
                "Assets/VARCOVoice-Unity/Editor/UI/VarcoMainWindow.uss",
                "Assets/Plugins/VARCOVoice/Editor/UI/VarcoMainWindow.uss"
            };
            
            foreach (var path in paths)
            {
                var asset = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
                if (asset != null) return asset;
            }
            
            return null;
        }
        
        private void CreateFallbackUI()
        {
            _root.style.backgroundColor = VarcoEditorStyles.BackgroundDark;
            _root.style.flexGrow = 1;
            
            // Header
            var header = new VisualElement();
            header.style.height = 48;
            header.style.backgroundColor = VarcoEditorStyles.BackgroundSecondary;
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.paddingLeft = 16;
            
            var title = new Label("VARCO VOICE");
            title.style.fontSize = 16;
            title.style.color = Color.white;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.Add(title);
            
            var version = new Label("v1.1.0");
            version.style.fontSize = 10;
            version.style.color = new Color(0.44f, 0.44f, 0.48f);
            version.style.marginLeft = 8;
            header.Add(version);
            
            _root.Add(header);
            
            // Tab bar
            var tabBar = new VisualElement();
            tabBar.style.height = 36;
            tabBar.style.backgroundColor = VarcoEditorStyles.BackgroundSecondary;
            tabBar.style.flexDirection = FlexDirection.Row;
            tabBar.style.paddingLeft = 16;
            tabBar.style.paddingRight = 16;
            
            // 3 Tabs
            _tabButtons = new Button[3];
            string[] tabNames = { "1. TTS", "2. DSP", "3. Export" };
            
            for (int i = 0; i < 3; i++)
            {
                int tabIndex = i;
                var btn = new Button(() => SelectTab(tabIndex));
                btn.text = tabNames[i];
                btn.style.flexGrow = 1;
                btn.style.backgroundColor = Color.clear;
                btn.style.borderBottomWidth = 2;
                btn.style.borderBottomColor = Color.clear;
                btn.style.color = new Color(0.63f, 0.63f, 0.67f);
                btn.style.unityFontStyleAndWeight = FontStyle.Bold;
                btn.style.fontSize = 12;
                _tabButtons[i] = btn;
                tabBar.Add(btn);
            }
            
            _root.Add(tabBar);
            
            // Content area
            var contentArea = new VisualElement();
            contentArea.style.flexGrow = 1;
            contentArea.style.paddingTop = 16;
            contentArea.style.paddingRight = 16;
            contentArea.style.paddingBottom = 16;
            contentArea.style.paddingLeft = 16;
            
            _panels = new VisualElement[3];
            string[] panelIcons = { "TTS", "DSP", "EXP" };
            string[] panelDescs = { "TTS Generation", "DSP Effects", "Export" };
            
            for (int i = 0; i < 3; i++)
            {
                var panel = new VisualElement();
                panel.style.flexGrow = 1;
                panel.style.backgroundColor = new Color(0.176f, 0.176f, 0.267f, 0.85f);
                panel.style.borderTopLeftRadius = 8;
                panel.style.borderTopRightRadius = 8;
                panel.style.borderBottomLeftRadius = 8;
                panel.style.borderBottomRightRadius = 8;
                panel.style.borderBottomLeftRadius = 8;
                panel.style.borderBottomRightRadius = 8;
                panel.style.justifyContent = Justify.FlexStart;
                panel.style.alignItems = Align.Stretch;
                panel.style.display = DisplayStyle.None;
                
                var icon = new Label(panelIcons[i]);
                icon.style.fontSize = 48;
                icon.style.color = new Color(0.63f, 0.63f, 0.67f);
                panel.Add(icon);
                
                var desc = new Label(panelDescs[i] + " Panel");
                desc.style.fontSize = 18;
                desc.style.color = new Color(0.63f, 0.63f, 0.67f);
                desc.style.marginTop = 8;
                panel.Add(desc);
                
                _panels[i] = panel;
                contentArea.Add(panel);
            }
            
            _root.Add(contentArea);
            
            // Status bar
            var statusBar = new VisualElement();
            statusBar.style.height = 28;
            statusBar.style.backgroundColor = VarcoEditorStyles.BackgroundSecondary;
            statusBar.style.flexDirection = FlexDirection.Row;
            statusBar.style.alignItems = Align.Center;
            statusBar.style.justifyContent = Justify.SpaceBetween;
            statusBar.style.paddingLeft = 16;
            statusBar.style.paddingRight = 16;
            
            var connectionSection = new VisualElement();
            connectionSection.style.flexDirection = FlexDirection.Row;
            connectionSection.style.alignItems = Align.Center;
            
            _connectionIndicator = new VisualElement();
            _connectionIndicator.style.width = 8;
            _connectionIndicator.style.height = 8;
            _connectionIndicator.style.borderTopLeftRadius = 4;
            _connectionIndicator.style.borderTopRightRadius = 4;
            _connectionIndicator.style.borderBottomLeftRadius = 4;
            _connectionIndicator.style.borderBottomRightRadius = 4;
            _connectionIndicator.style.backgroundColor = VarcoEditorStyles.Error;
            _connectionIndicator.style.marginRight = 6;
            connectionSection.Add(_connectionIndicator);
            
            _connectionStatus = new Label("Disconnected");
            _connectionStatus.style.fontSize = 11;
            _connectionStatus.style.color = VarcoEditorStyles.TextSecondary;
            connectionSection.Add(_connectionStatus);
            
            statusBar.Add(connectionSection);
            
            _statusMessage = new Label("Ready");
            _statusMessage.style.fontSize = 11;
            _statusMessage.style.color = VarcoEditorStyles.TextMuted;
            statusBar.Add(_statusMessage);
            
            _root.Add(statusBar);
            
            // Initialize for Fallback mode
            SelectTab(TAB_TTS);
            CheckConnectionAsync();
        }
        
        #endregion
    }
}
