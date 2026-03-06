using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using Cysharp.Threading.Tasks;

namespace VARCOVoice.Editor
{
    /// <summary>
    /// Editor window for browsing and selecting VARCO voices - UI Toolkit version
    /// </summary>
    public class VoicePickerWindow : EditorWindow
    {
        #region Constants
        
        private const string UXML_PATH = "Packages/com.varco.voice/Editor/UI/VoicePickerWindow.uxml";
        private const string USS_PATH = "Packages/com.varco.voice/Editor/UI/VoicePickerWindow.uss";
        
        #endregion
        
        #region Private Fields
        
        private List<VarcoVoice> _voices = new List<VarcoVoice>();
        private List<VarcoVoice> _filteredVoices = new List<VarcoVoice>();
        
        // Filters
        private string _searchText = "";
        private Gender _genderFilter = Gender.Unknown;
        private AgeGroup _ageFilter = AgeGroup.Unknown;
        
        private enum FilterTab { All, Favorites, Recent, Male, Female }
        private FilterTab _currentTab = FilterTab.All;
        
        // Pagination
        private int _pageSize = 50;
        private int _currentPage = 0;
        private int _selectedIndex = -1;
        
        // Preview
        private AudioSource _previewSource;
        private string _previewText = "안녕하세요. 바르코 보이스 테스트입니다.";
        
        // UI Elements
        private VisualElement _root;
        private ScrollView _voiceList;
        private Label _voiceCount;
        private Label _pageLabel;
        private Label _statusText;
        private TextField _searchField;
        private TextField _previewTextField;
        private Label _selectedName;
        private Button[] _tabButtons;
        
        #endregion
        
        #region Menu Items
        
        [MenuItem("Window/VARCO Voice/Voice Picker")]
        public static void ShowWindow()
        {
            var window = GetWindow<VoicePickerWindow>();
            window.titleContent = new GUIContent("VARCO Voice Picker");
            window.minSize = new Vector2(400, 500);
            window.Show();
        }
        
        #endregion
        
        #region Unity Lifecycle
        
        private void CreateGUI()
        {
            if (_root != null)
            {
                VarcoTheme.Unsubscribe(_root);
            }

            _root = rootVisualElement;
            _root.Clear();
            
            // Load Theme (Robust Lookup)
            var themeGuids = AssetDatabase.FindAssets("Theme t:StyleSheet");
            StyleSheet themeSheet = null;
            if (themeGuids.Length > 0)
            {
                var themePath = AssetDatabase.GUIDToAssetPath(themeGuids[0]);
                themeSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(themePath);
            }
            
            if (themeSheet != null && !_root.styleSheets.Contains(themeSheet))
            {
                _root.styleSheets.Add(themeSheet);
            }
            
            // Load UXML (Robust Lookup)
            var uxmlGuids = AssetDatabase.FindAssets("VoicePickerWindow t:VisualTreeAsset");
            if (uxmlGuids.Length > 0)
            {
                var uxmlPath = AssetDatabase.GUIDToAssetPath(uxmlGuids[0]);
                var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
                if (visualTree != null)
                {
                    visualTree.CloneTree(_root);
                }
                else
                {
                    _root.Add(new Label("Failed to load UXML (Asset found but null)."));
                    return;
                }
            }
            else
            {
                _root.Add(new Label("Failed to load UI. VoicePickerWindow.uxml not found."));
                return;
            }
            
            // Load USS
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(USS_PATH);
            if (styleSheet == null)
            {
                styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                    "Assets/VARCOVoice-Unity/Editor/UI/VoicePickerWindow.uss");
            }
            if (styleSheet != null)
            {
                if (!_root.styleSheets.Contains(styleSheet))
                {
                    _root.styleSheets.Add(styleSheet);
                }
            }
            
            // Apply theme
            VarcoTheme.Subscribe(_root);
            
            CacheUIElements();
            SetupEventHandlers();
            LoadVoicesAsync().Forget();
        }
        
        private void OnDisable()
        {
            VarcoTheme.Unsubscribe(_root);
            if (_previewSource != null)
            {
                DestroyImmediate(_previewSource.gameObject);
            }
        }
        
        #endregion
        
        #region UI Setup
        
        private void CacheUIElements()
        {
            _voiceList = _root.Q<ScrollView>("voice-list");
            _voiceCount = _root.Q<Label>("voice-count");
            _pageLabel = _root.Q<Label>("page-label");
            _statusText = _root.Q<Label>("status-text");
            _searchField = _root.Q<TextField>("search-field");
            _previewTextField = _root.Q<TextField>("preview-text");
            // _selectedName = _root.Q<Label>("selected-name"); // Removed from UI
            
            if (_previewTextField != null)
            {
                _previewTextField.value = _previewText;
            }
        }
        
        private void SetupEventHandlers()
        {
            // Refresh button
            var refreshBtn = _root.Q<Button>("refresh-btn");
            if (refreshBtn != null)
            {
                refreshBtn.clicked += () => LoadVoicesAsync(forceRefresh: true).Forget();
            }

            var settingsBtn = _root.Q<Button>("settings-btn");
            if (settingsBtn != null)
            {
                settingsBtn.clicked += OpenSettingsMenu;
            }
            
            // Tab buttons
            SetupTabButton("tab-all", FilterTab.All);
            SetupTabButton("tab-favorites", FilterTab.Favorites);
            SetupTabButton("tab-recent", FilterTab.Recent);
            SetupTabButton("tab-male", FilterTab.Male);
            SetupTabButton("tab-female", FilterTab.Female);
            
            // Search field
            if (_searchField != null)
            {
                _searchField.RegisterValueChangedCallback(evt =>
                {
                    _searchText = evt.newValue;
                    ApplyFilters();
                });
            }
            
            // Filter dropdowns
            SetupFilterDropdown<Gender>("gender-filter", g => { _genderFilter = g; ApplyFilters(); });
            SetupFilterDropdown<AgeGroup>("age-filter", a => { _ageFilter = a; ApplyFilters(); });
            
            // Pagination
            var prevBtn = _root.Q<Button>("prev-page");
            var nextBtn = _root.Q<Button>("next-page");
            if (prevBtn != null) prevBtn.clicked += () => { _currentPage--; RefreshVoiceList(); };
            if (nextBtn != null) nextBtn.clicked += () => { _currentPage++; RefreshVoiceList(); };
            
            // Preview controls
            var previewBtn = _root.Q<Button>("preview-btn");
            var stopBtn = _root.Q<Button>("stop-btn");
            var copyBtn = _root.Q<Button>("copy-btn");
            
            if (previewBtn != null) previewBtn.clicked += OnPreviewClicked;
            if (stopBtn != null) stopBtn.clicked += StopPreview;
            if (copyBtn != null) copyBtn.clicked += OnCopyClicked;
            
            if (_previewTextField != null)
            {
                _previewTextField.RegisterValueChangedCallback(evt => _previewText = evt.newValue);
            }
        }
        
        private void SetupTabButton(string name, FilterTab tab)
        {
            var btn = _root.Q<Button>(name);
            if (btn != null)
            {
                btn.clicked += () =>
                {
                    _currentTab = tab;
                    UpdateTabStyles();
                    ApplyFilters();
                };
            }
        }
        
        private void SetupFilterDropdown<T>(string name, System.Action<T> onChange) where T : struct, System.Enum
        {
            var dropdown = _root.Q<DropdownField>(name);
            if (dropdown != null)
            {
                dropdown.choices = System.Enum.GetNames(typeof(T)).ToList();
                dropdown.index = 0;
                dropdown.RegisterValueChangedCallback(evt =>
                {
                    if (System.Enum.TryParse<T>(evt.newValue, out var value))
                    {
                        onChange(value);
                    }
                });
            }
        }
        
        private void UpdateTabStyles()
        {
            string[] tabNames = { "tab-all", "tab-favorites", "tab-recent", "tab-male", "tab-female" };
            FilterTab[] tabs = { FilterTab.All, FilterTab.Favorites, FilterTab.Recent, FilterTab.Male, FilterTab.Female };
            
            for (int i = 0; i < tabNames.Length; i++)
            {
                var btn = _root.Q<Button>(tabNames[i]);
                if (btn != null)
                {
                    if (tabs[i] == _currentTab)
                    {
                        btn.AddToClassList("filter-tab--active");
                    }
                    else
                    {
                        btn.RemoveFromClassList("filter-tab--active");
                    }
                }
            }
        }
        
        #endregion
        
        #region Data Loading
        
        private async UniTaskVoid LoadVoicesAsync(bool forceRefresh = false)
        {
            if (!VarcoConfig.Instance.IsValid())
            {
                UpdateStatus("API Key not configured. Go to Project Settings > VARCO Voice");
                return;
            }
            
            UpdateStatus("Loading voices...");
            
            try
            {
                var client = new VarcoApiClient();
                _voices = await client.GetVoicesAsync(forceRefresh);
                ApplyFilters();
                UpdateStatus($"Loaded {_voices.Count} voices");
            }
            catch (VarcoException ex)
            {
                UpdateStatus($"Error: {ex.Message}");
            }
        }
        
        private void ApplyFilters()
        {
            _filteredVoices.Clear();
            
            foreach (var voice in _voices)
            {
                if (voice == null) continue;
                string speakerName = voice.SpeakerName ?? string.Empty;

                // Tab filter
                switch (_currentTab)
                {
                    case FilterTab.Favorites:
                        if (!VoiceFavorites.IsFavorite(speakerName)) continue;
                        break;
                    case FilterTab.Recent:
                        if (!VoiceFavorites.RecentVoices.Contains(speakerName)) continue;
                        break;
                    case FilterTab.Male:
                        if (voice.Gender != Gender.Male) continue;
                        break;
                    case FilterTab.Female:
                        if (voice.Gender != Gender.Female) continue;
                        break;
                }
                
                // Search filter
                if (!string.IsNullOrEmpty(_searchText))
                {
                    if (speakerName.IndexOf(_searchText, System.StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }
                }
                
                // Gender filter
                if (_currentTab != FilterTab.Male && _currentTab != FilterTab.Female)
                {
                    if (_genderFilter != Gender.Unknown && voice.Gender != _genderFilter)
                        continue;
                }
                
                // Age filter
                if (_ageFilter != AgeGroup.Unknown && voice.AgeGroup != _ageFilter)
                    continue;
                
                _filteredVoices.Add(voice);
            }
            
            _currentPage = 0;
            _selectedIndex = -1;
            RefreshVoiceList();
        }
        
        private void RefreshVoiceList()
        {
            if (_voiceList == null) return;
            
            _voiceList.Clear();
            
            // Update counts
            if (_voiceCount != null)
            {
                _voiceCount.text = $"Total: {_voices.Count} | Showing: {_filteredVoices.Count}";
            }
            
            // Pagination
            int totalPages = Mathf.Max(1, Mathf.CeilToInt(_filteredVoices.Count / (float)_pageSize));
            _currentPage = Mathf.Clamp(_currentPage, 0, totalPages - 1);
            
            if (_pageLabel != null)
            {
                _pageLabel.text = $"Page {_currentPage + 1} / {totalPages}";
            }
            
            // Update pagination buttons
            var prevBtn = _root.Q<Button>("prev-page");
            var nextBtn = _root.Q<Button>("next-page");
            if (prevBtn != null) prevBtn.SetEnabled(_currentPage > 0);
            if (nextBtn != null) nextBtn.SetEnabled(_currentPage < totalPages - 1);
            
            // Populate list
            int startIndex = _currentPage * _pageSize;
            int endIndex = Mathf.Min(startIndex + _pageSize, _filteredVoices.Count);
            
            for (int i = startIndex; i < endIndex; i++)
            {
                var voice = _filteredVoices[i];
                _voiceList.Add(CreateVoiceItem(voice, i));
            }
            
            if (_filteredVoices.Count == 0)
            {
                var empty = new Label("No voices found. Try adjusting filters.");
                empty.AddToClassList("help-text");
                _voiceList.Add(empty);
            }
        }
        
        private VisualElement CreateVoiceItem(VarcoVoice voice, int index)
        {
            var item = new VisualElement();
            item.AddToClassList("voice-item");
            if (index == _selectedIndex)
            {
                item.AddToClassList("voice-item--selected");
            }
            
            // Favorite button
            bool isFavorite = VoiceFavorites.IsFavorite(voice.SpeakerName);
            var favBtn = new Label(isFavorite ? "★" : "☆");
            favBtn.AddToClassList("voice-favorite");
            if (isFavorite) favBtn.AddToClassList("voice-favorite--active");
            favBtn.RegisterCallback<ClickEvent>(_ =>
            {
                VoiceFavorites.ToggleFavorite(voice.SpeakerName);
                RefreshVoiceList();
            });
            item.Add(favBtn);
            
            // Voice info
            var info = new VisualElement();
            info.AddToClassList("voice-info");
            
            var nameLabel = new Label(voice.SpeakerName);
            nameLabel.AddToClassList("voice-name");
            info.Add(nameLabel);
            
            var tagsLabel = new Label(GetVoiceTags(voice));
            tagsLabel.AddToClassList("voice-tags");
            info.Add(tagsLabel);
            
            item.Add(info);
            
            // Actions
            var actions = new VisualElement();
            actions.AddToClassList("voice-actions");
            
            var goToTTSBtn = new Button(() => GoToTTSWithVoice(voice)) { text = "Select" }; // Changed text to Select
            goToTTSBtn.AddToClassList("btn");
            goToTTSBtn.AddToClassList("voice-select-btn");
            goToTTSBtn.AddToClassList("btn-accent-blue"); // Updated class
            actions.Add(goToTTSBtn);
            
            var checkBtn = new Button(() => SelectVoice(voice, index)) { text = "✔" };
            checkBtn.AddToClassList("btn");
            checkBtn.AddToClassList("voice-check-btn"); // New class
            if (index == _selectedIndex)
            {
                checkBtn.AddToClassList("voice-check-btn--active");
                checkBtn.text = "✔"; // Selected
            }
            else
            {
                 checkBtn.text = ""; // Empty if not selected? Or just a checkmark?
                 // User said "Change to Check button". Usually implies a button you click to check options.
                 // Let's make it always visible as a "Select" target, or a radio button style.
                 // Given the request "Change Play button to Check button", I will make it look like a toggle.
                 checkBtn.text = "✔";
                 checkBtn.style.opacity = 0.3f; // Dimmed when not selected
            }
            actions.Add(checkBtn);
            
            item.Add(actions);
            
            // Mobile-like selection on click (optional, leads to preview)
            // item.RegisterCallback<ClickEvent>(_ => SelectVoice(voice, index));
            
            return item;
        }

        private string GetVoiceTags(VarcoVoice voice)
        {
            var tags = new List<string>();
            
            if (voice.Gender != Gender.Unknown)
                tags.Add(voice.Gender == Gender.Male ? "♂" : "♀");
            
            if (voice.AgeGroup != AgeGroup.Unknown)
                tags.Add(voice.AgeGroup.ToString());
            
            if (voice.Tone != ToneType.Unknown)
                tags.Add(voice.Tone.ToString());
                
            return string.Join(" · ", tags);
        }
        
        #endregion
        
        
        #region Settings

        private void OpenSettingsMenu()
        {
            var menu = new GenericMenu();

            menu.AddItem(new GUIContent("Light Mode"), VarcoTheme.IsLightMode, () =>
            {
                VarcoTheme.IsLightMode = !VarcoTheme.IsLightMode;
            });

            menu.AddSeparator("");

            menu.AddItem(new GUIContent("API Settings"), false, () =>
            {
                SettingsService.OpenProjectSettings("Project/VARCO Voice");
            });
            
            menu.AddItem(new GUIContent("API Portal"), false, () =>
            {
                Application.OpenURL("https://api.varco.ai/ko");
            });

            menu.AddSeparator("");

            menu.AddItem(new GUIContent("Documentation"), false, () =>
            {
                EditorUtility.DisplayDialog("VARCO Voice", VarcoVersion.AboutDialogText, "OK");
            });

            menu.AddItem(new GUIContent("About VARCO Voice"), false, () =>
            {
                Application.OpenURL("https://voice.varco.ai/");
            });

            menu.ShowAsContext();
        }

        #endregion

        #region Actions
        
        private void SelectVoice(VarcoVoice voice, int index)
        {
            _selectedIndex = index;
            VoiceFavorites.AddRecentVoice(voice.SpeakerName);
            
            // No longer updating _selectedName label as it's removed from UI
            
            RefreshVoiceList();
            UpdateStatus($"Selected: {voice.SpeakerName}");
        }
        
        private void OnPreviewClicked()
        {
            if (_selectedIndex >= 0 && _selectedIndex < _filteredVoices.Count)
            {
                PreviewVoice(_filteredVoices[_selectedIndex]).Forget();
            }
            else
            {
                UpdateStatus("Please select a voice first");
            }
        }
        
        private async UniTaskVoid PreviewVoice(VarcoVoice voice)
        {
            UpdateStatus($"Generating preview for {voice.SpeakerName}...");
            
            try
            {
                var client = new VarcoApiClient();
                var clip = await client.SynthesizeAsync(_previewText, voice.SpeakerName);
                
                EnsurePreviewSource();
                _previewSource.clip = clip;
                _previewSource.Play();
                
                UpdateStatus($"Playing: {voice.SpeakerName}");
            }
            catch (VarcoException ex)
            {
                UpdateStatus($"Preview failed: {ex.Message}");
            }
        }
        
        private void StopPreview()
        {
            if (_previewSource != null)
            {
                _previewSource.Stop();
            }
            UpdateStatus("");
        }
        
        private void OnCopyClicked()
        {
            if (_selectedIndex >= 0 && _selectedIndex < _filteredVoices.Count)
            {
                var voice = _filteredVoices[_selectedIndex];
                EditorGUIUtility.systemCopyBuffer = voice.SpeakerName;
                UpdateStatus($"Copied: {voice.SpeakerName}");
            }
        }
        
        private void GoToTTSWithVoice(VarcoVoice voice)
        {
            VoiceFavorites.AddRecentVoice(voice.SpeakerName);
            
            // Open main window and set voice
            var mainWindow = VarcoVoiceMainWindow.ShowWindow();
            if (mainWindow != null)
            {
                mainWindow.SetVoiceAndSwitchToTTS(voice.SpeakerName);
            }
            
            UpdateStatus($"Opened TTS with: {voice.SpeakerName}");
        }
        
        private void EnsurePreviewSource()
        {
            if (_previewSource == null)
            {
                var go = new GameObject("[VoicePreview]");
                go.hideFlags = HideFlags.HideAndDontSave;
                _previewSource = go.AddComponent<AudioSource>();
                _previewSource.playOnAwake = false;
                _previewSource.spatialBlend = 0f;
                _previewSource.volume = 1f;
            }
        }
        
        private void UpdateStatus(string message)
        {
            if (_statusText != null)
            {
                _statusText.text = message;
            }
        }
        
        #endregion
    }
}
