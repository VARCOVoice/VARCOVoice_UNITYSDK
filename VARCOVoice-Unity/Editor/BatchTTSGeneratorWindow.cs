using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using Cysharp.Threading.Tasks;

namespace VARCOVoice.Editor
{
    /// <summary>
    /// Batch TTS Generator Window - UI Toolkit version
    /// </summary>
    public class BatchTTSGeneratorWindow : EditorWindow
    {
        #region Constants
        
        private const string UXML_PATH = "Packages/com.varco.voice/Editor/UI/BatchTTSGeneratorWindow.uxml";
        private const string USS_PATH = "Packages/com.varco.voice/Editor/UI/BatchTTSGeneratorWindow.uss";
        
        #endregion
        
        #region Private Fields
        
        // Source
        private TextAsset _sourceTextAsset;
        private List<BatchItem> _items = new List<BatchItem>();
        
        // Configuration
        private string _selectedVoice = "";
        private Language _language = Language.Korean;
        private float _speed = 1.0f;
        private float _pitch = 1.0f;
        private string _outputPath = "Assets/Audio/TTS";
        
        // Multi-speaker voice mapping: Character Name -> VARCO Voice
        private Dictionary<string, string> _voiceMapping = new Dictionary<string, string>();
        private List<string> _voiceNames = new List<string>();
        private bool _voicesLoaded = false;
        private string _scriptSeparator = ":";
        
        // Processing
        private bool _isProcessing = false;
        private int _currentIndex = 0;
        private int _completedCount = 0;
        private int _errorCount = 0;
        
        // Logs
        private List<string> _logs = new List<string>();
        
        // UI Elements
        private VisualElement _root;
        private ScrollView _queueList;
        private ScrollView _logList;
        private ProgressBar _progressBar;
        private Label _itemCount;
        private Label _completedLabel;
        private Label _errorLabel;
        private Label _statusLabel;
        private TextField _voiceNameField;
        private TextField _outputPathField;
        private TextField _separatorField;
        private Button _generateAllBtn;
        private Button _cancelBtn;
        private ScrollView _voiceMappingList;
        
        // Tabs
        private VisualElement _viewQueue;
        private VisualElement _viewMapping;
        private Label _tabQueueLbl;
        private Label _tabMappingLbl;
        
        #endregion
        
        #region Batch Item
        
        private enum ItemStatus { Pending, Processing, Completed, Error }
        
        private class BatchItem
        {
            public int Index;
            public string Text;
            public string Character;       // Character name from script
            public string VoiceName;       // Mapped VARCO voice name
            public string OutputFileName;
            public ItemStatus Status;
            public string ErrorMessage;
            public AudioClip GeneratedClip;
        }
        
        #endregion
        
        #region Menu Item
        
        [MenuItem("Window/VARCO Voice/Batch TTS Generator")]
        public static void ShowWindow()
        {
            var window = GetWindow<BatchTTSGeneratorWindow>();
            window.titleContent = new GUIContent("Batch TTS Generator");
            window.minSize = new Vector2(500, 600);
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
            var uxmlGuids = AssetDatabase.FindAssets("BatchTTSGeneratorWindow t:VisualTreeAsset");
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
                _root.Add(new Label("Failed to load UI. BatchTTSGeneratorWindow.uxml not found."));
                return;
            }
            
            // Load USS
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(USS_PATH);
            if (styleSheet == null)
            {
                styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                    "Assets/VARCOVoice-Unity/Editor/UI/BatchTTSGeneratorWindow.uss");
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
        }

        private void OnDisable()
        {
            VarcoTheme.Unsubscribe(_root);
        }
        
        #endregion
        
        #region UI Setup
        
        private void CacheUIElements()
        {
            _queueList = _root.Q<ScrollView>("queue-list");
            _logList = _root.Q<ScrollView>("log-list");
            _progressBar = _root.Q<ProgressBar>("progress-bar");
            _itemCount = _root.Q<Label>("item-count");
            _completedLabel = _root.Q<Label>("completed-count");
            _errorLabel = _root.Q<Label>("error-count");
            _statusLabel = _root.Q<Label>("current-status");
            _voiceNameField = _root.Q<TextField>("voice-name");
            _outputPathField = _root.Q<TextField>("output-path");
            _separatorField = _root.Q<TextField>("script-separator");
            _generateAllBtn = _root.Q<Button>("generate-all-btn");
            _cancelBtn = _root.Q<Button>("cancel-btn");
            
            if (_outputPathField != null)
            {
                _outputPathField.value = _outputPath;
            }
            
            _voiceMappingList = _root.Q<ScrollView>("voice-mapping-list");
            
            // Tabs
            _viewQueue = _root.Q<VisualElement>("view-queue");
            _viewMapping = _root.Q<VisualElement>("view-mapping");
            _tabQueueLbl = _root.Q<Label>("tab-queue-label");
            _tabMappingLbl = _root.Q<Label>("tab-mapping-label");
        }
        
        private void SetupEventHandlers()
        {
            var settingsBtn = _root.Q<Button>("settings-btn");
            if (settingsBtn != null)
            {
                settingsBtn.clicked += OpenSettingsMenu;
            }

            // Source controls
            var textAssetField = _root.Q<ObjectField>("text-asset");
            if (textAssetField != null)
            {
                textAssetField.objectType = typeof(TextAsset);
                textAssetField.RegisterValueChangedCallback(evt =>
                {
                    _sourceTextAsset = evt.newValue as TextAsset;
                    if (_sourceTextAsset != null)
                    {
                        ParseTextAsset();
                    }
                });
            }
            
            var loadFileBtn = _root.Q<Button>("load-file-btn");
            if (loadFileBtn != null) loadFileBtn.clicked += LoadFromFile;
            
            // Configuration
            if (_voiceNameField != null)
            {
                _voiceNameField.RegisterValueChangedCallback(evt => _selectedVoice = evt.newValue);
            }
            
            var pickVoiceBtn = _root.Q<Button>("pick-voice-btn");
            if (pickVoiceBtn != null) pickVoiceBtn.clicked += ShowDefaultVoiceDropdown;
            
            if (_outputPathField != null)
            {
                _outputPathField.RegisterValueChangedCallback(evt => _outputPath = evt.newValue);
            }
            
            var browseBtn = _root.Q<Button>("browse-btn");
            if (browseBtn != null) browseBtn.clicked += BrowseOutputPath;
            
            // Voice mapping controls
            var addMappingBtn = _root.Q<Button>("add-mapping-btn");
            if (addMappingBtn != null) addMappingBtn.clicked += AddVoiceMapping;
            
            // Queue controls
            var clearAllBtn = _root.Q<Button>("clear-all-btn");
            if (clearAllBtn != null) clearAllBtn.clicked += ClearQueue;
            
            // Action buttons
            if (_generateAllBtn != null) _generateAllBtn.clicked += StartGeneration;
            if (_cancelBtn != null) _cancelBtn.clicked += CancelGeneration;
            
            var exportLibraryBtn = _root.Q<Button>("export-library-btn");
            if (exportLibraryBtn != null) exportLibraryBtn.clicked += ExportAllToLibrary;
            
            // Log controls
            var clearLogBtn = _root.Q<Button>("clear-log-btn");
            if (clearLogBtn != null) clearLogBtn.clicked += () => { _logs.Clear(); RefreshLogList(); };
            
            SetupTabs();
        }
        
        #endregion
        
        #region Voice Mapping
        
        private void AddVoiceMapping()
        {
            // Generate unique key
            int i = 1;
            string key = "Character1";
            while (_voiceMapping.ContainsKey(key))
            {
                i++;
                key = $"Character{i}";
            }
            _voiceMapping[key] = "";
            
            // Load voices if not loaded
            if (!_voicesLoaded)
            {
                LoadVoiceListAsync().Forget();
            }
            else
            {
                RefreshVoiceMappingList();
            }
        }
        
        private async UniTaskVoid LoadVoiceListAsync()
        {
            try
            {
                if (!VarcoConfig.Instance.IsValid())
                {
                    AddLog("[ERROR] API Key not configured");
                    return;
                }
                
                AddLog("Loading voice list...");
                var client = new VarcoApiClient();
                var voices = await client.GetVoicesAsync(false);
                
                _voiceNames.Clear();
                _voiceNames.Add(""); // Empty option for default
                foreach (var v in voices)
                {
                    _voiceNames.Add(v.SpeakerName);
                }
                
                _voicesLoaded = true;
                AddLog($"Loaded {voices.Count} voices");
                RefreshVoiceMappingList();
            }
            catch (System.Exception ex)
            {
                AddLog($"[ERROR] Failed to load voices: {ex.Message}");
            }
        }
        
        private void RefreshVoiceMappingList()
        {
            if (_voiceMappingList == null) return;
            
            _voiceMappingList.Clear();
            
            if (_voiceMapping.Count == 0)
            {
                var empty = new Label("No mappings. Characters will use default voice.");
                empty.AddToClassList("help-text");
                _voiceMappingList.Add(empty);
                return;
            }
            
            var keysToUpdate = new List<string>(_voiceMapping.Keys);
            foreach (var character in keysToUpdate)
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.marginBottom = 4;
                
                var charField = new TextField();
                charField.value = character;
                charField.style.width = 100;
                charField.style.marginRight = 8;
                string oldKey = character;
                charField.RegisterValueChangedCallback(evt =>
                {
                    if (_voiceMapping.TryGetValue(oldKey, out var voice))
                    {
                        _voiceMapping.Remove(oldKey);
                        _voiceMapping[evt.newValue] = voice;
                        UpdateItemsVoiceForCharacter(oldKey, evt.newValue); // Update character name in items
                        oldKey = evt.newValue;
                    }
                });
                row.Add(charField);
                
                // Voice field + Pick button
                var voiceField = new TextField();
                voiceField.value = _voiceMapping[character];
                voiceField.style.flexGrow = 1;
                voiceField.style.marginRight = 4;
                voiceField.RegisterValueChangedCallback(evt =>
                {
                    string newVoice = evt.newValue ?? "";
                    _voiceMapping[oldKey] = newVoice;
                    UpdateItemsVoiceForCharacter(oldKey, null, newVoice); // Update voice only
                });
                row.Add(voiceField);
                
                var pickBtn = new Button(() =>
                {
                    // Open VoicePicker and set callback
                    ShowVoicePickerPopup(oldKey, voiceField);
                }) { text = "Pick" };
                pickBtn.AddToClassList("btn-secondary");
                pickBtn.style.width = 40;
                pickBtn.style.marginRight = 4;
                row.Add(pickBtn);
                
                var removeBtn = new Button(() =>
                {
                    _voiceMapping.Remove(oldKey);
                    RefreshVoiceMappingList();
                }) { text = "✕" };
                removeBtn.AddToClassList("queue-remove");
                row.Add(removeBtn);
                
                _voiceMappingList.Add(row);
            }
        }
        
        /// <summary>
        /// Default Voice 선택용 즐겨찾기 드롭다운 표시
        /// </summary>
        private void ShowDefaultVoiceDropdown()
        {
            var menu = new GenericMenu();
            
            // 즐겨찾기 추가
            var favorites = VoiceFavorites.Favorites;
            if (favorites.Count > 0)
            {
                menu.AddDisabledItem(new GUIContent("★ Favorites"));
                foreach (var fav in favorites)
                {
                    string voiceName = fav;
                    menu.AddItem(new GUIContent($"  {fav}"), _selectedVoice == fav, () =>
                    {
                        _selectedVoice = voiceName;
                        if (_voiceNameField != null) _voiceNameField.value = voiceName;
                    });
                }
                menu.AddSeparator("");
            }
            
            // 최근 사용 추가
            var recents = VoiceFavorites.RecentVoices;
            if (recents.Count > 0)
            {
                menu.AddDisabledItem(new GUIContent("Recent"));
                int count = 0;
                foreach (var recent in recents)
                {
                    if (count++ >= 10) break;
                    string voiceName = recent;
                    menu.AddItem(new GUIContent($"  {recent}"), _selectedVoice == recent, () =>
                    {
                        _selectedVoice = voiceName;
                        if (_voiceNameField != null) _voiceNameField.value = voiceName;
                    });
                }
                menu.AddSeparator("");
            }
            
            // 전체 목록 열기 옵션
            menu.AddItem(new GUIContent("Browse All Voices..."), false, () =>
            {
                VoicePickerWindow.ShowWindow();
            });
            
            menu.ShowAsContext();
        }
        
        private void ShowVoicePickerPopup(string characterKey, TextField voiceField)
        {
            // Use GenericMenu for a quick picker with favorites and recent
            var menu = new GenericMenu();
            
            // Add favorites
            var favorites = VoiceFavorites.Favorites;
            if (favorites.Count > 0)
            {
                menu.AddDisabledItem(new GUIContent("★ Favorites"));
                foreach (var fav in favorites)
                {
                    string voiceName = fav;
                    menu.AddItem(new GUIContent($"  {fav}"), false, () =>
                    {
                        _voiceMapping[characterKey] = voiceName;
                        voiceField.value = voiceName;
                        UpdateItemsVoiceForCharacter(characterKey, null, voiceName);
                    });
                }
                menu.AddSeparator("");
            }
            
            // Add recent
            var recents = VoiceFavorites.RecentVoices;
            if (recents.Count > 0)
            {
                menu.AddDisabledItem(new GUIContent("Recent"));
                int count = 0;
                foreach (var recent in recents)
                {
                    if (count++ >= 10) break;
                    string voiceName = recent;
                    menu.AddItem(new GUIContent($"  {recent}"), false, () =>
                    {
                        _voiceMapping[characterKey] = voiceName;
                        voiceField.value = voiceName;
                        UpdateItemsVoiceForCharacter(characterKey, null, voiceName);
                    });
                }
                menu.AddSeparator("");
            }
            
            // Open full picker option
            menu.AddItem(new GUIContent("Browse All Voices..."), false, () =>
            {
                VoicePickerWindow.ShowWindow();
            });
            
            menu.ShowAsContext();
        }
        
        #endregion
        
        #region Queue Management
        
        private void LoadFromFile()
        {
            var path = EditorUtility.OpenFilePanel("Load Text File", "", "txt,csv");
            if (string.IsNullOrEmpty(path)) return;
            
            try
            {
                var lines = File.ReadAllLines(path);
                _items.Clear();
                
                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i].Trim();
                    if (string.IsNullOrEmpty(line)) continue;
                    
                    ParseLine(line, i);
                }
                
                AddLog($"Loaded {_items.Count} items from file");
                RefreshQueueList();
            }
            catch (Exception ex)
            {
                AddLog($"[ERROR] Failed to load file: {ex.Message}");
            }
        }
        
        private void ParseTextAsset()
        {
            if (_sourceTextAsset == null) return;
            
            var lines = _sourceTextAsset.text.Split('\n');
            _items.Clear();
            
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;
                
                ParseLine(line, i);
            }
            
            AddLog($"Parsed {_items.Count} items from TextAsset");
            RefreshQueueList();
            RefreshVoiceMappingList(); // Refresh mapping list once after full parse
        }
        
        private void ParseLine(string line, int index)
        {
            if (string.IsNullOrWhiteSpace(line)) return;

            string filename, text, character = "";
            
            // Priority 1: User-defined Script Separator (e.g. "Lucas:Text")
            if (!string.IsNullOrEmpty(_scriptSeparator) && line.Contains(_scriptSeparator))
            {
                var idx = line.IndexOf(_scriptSeparator);
                character = line.Substring(0, idx).Trim();
                text = line.Substring(idx + _scriptSeparator.Length).Trim();
                filename = string.IsNullOrEmpty(character) ? $"audio_{index:D3}" : $"{character}_{index:D3}";
            }
            // Priority 2: Standard Pipe Format (filename|character|text)
            else if (line.Contains("|"))
            {
                var parts = line.Split('|');
                if (parts.Length >= 3)
                {
                    filename = parts[0].Trim();
                    character = parts[1].Trim();
                    text = parts[2].Trim();
                }
                else if (parts.Length == 2)
                {
                    filename = parts[0].Trim();
                    text = parts[1].Trim();
                }
                else
                {
                    filename = $"audio_{index:D3}";
                    text = line;
                }
            }
            else if (line.Contains(":"))
            {
                // Fallback for default colon
                var colonIdx = line.IndexOf(':');
                character = line.Substring(0, colonIdx).Trim();
                text = line.Substring(colonIdx + 1).Trim();
                filename = $"{character}_{index:D3}";
            }
            // Fallback: Just text
            else
            {
                filename = $"audio_{index:D3}";
                text = line;
            }
            
            // Auto-register character in mapping list if it's new
            if (!string.IsNullOrEmpty(character) && !_voiceMapping.ContainsKey(character))
            {
                _voiceMapping[character] = "";
            }
            
            // Get current voice for this character
            string voiceName = _selectedVoice;
            if (!string.IsNullOrEmpty(character) && _voiceMapping.TryGetValue(character, out var mappedVoice))
            {
                voiceName = mappedVoice;
            }
            
            // Create item
            _items.Add(new BatchItem
            {
                Index = _items.Count,
                Text = text,
                Character = character,
                VoiceName = voiceName,
                OutputFileName = filename,
                Status = ItemStatus.Pending
            });
        }
        
        private void AddManualEntry()
        {
            _items.Add(new BatchItem
            {
                Index = _items.Count,
                Text = "Enter text here",
                OutputFileName = $"audio_{_items.Count:D3}",
                Status = ItemStatus.Pending
            });
            RefreshQueueList();
        }
        
        private void ClearQueue()
        {
            _items.Clear();
            RefreshQueueList();
        }
        
        private void UpdateItemsVoiceForCharacter(string oldChar, string newChar = null, string newVoice = null)
        {
            foreach (var item in _items)
            {
                if (item.Character == oldChar)
                {
                    if (newChar != null) item.Character = newChar;
                    if (newVoice != null) item.VoiceName = newVoice;
                    else if (_voiceMapping.TryGetValue(item.Character, out var mappedVoice))
                    {
                        item.VoiceName = mappedVoice;
                    }
                }
            }
            RefreshQueueList();
        }
        
        private void RefreshQueueList()
        {
            if (_queueList == null) return;
            
            _queueList.Clear();
            
            if (_itemCount != null)
            {
                _itemCount.text = $"{_items.Count} items";
            }
            
            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                _queueList.Add(CreateQueueItem(item, i));
            }
            
            if (_items.Count == 0)
            {
                var empty = new Label("No items. Load a text file or add entries.");
                empty.AddToClassList("help-text");
                _queueList.Add(empty);
            }
        }
        
        private VisualElement CreateQueueItem(BatchItem item, int index)
        {
            var container = new VisualElement();
            container.AddToClassList("queue-item");
            
            // Status icon
            var status = new Label(GetStatusIcon(item.Status));
            status.AddToClassList("queue-status");
            status.AddToClassList($"queue-status--{item.Status.ToString().ToLower()}");
            container.Add(status);
            
            // Index
            var indexLabel = new Label($"#{index + 1}");
            indexLabel.AddToClassList("queue-index");
            container.Add(indexLabel);
            
            // Filename
            var filename = new Label(item.OutputFileName);
            filename.AddToClassList("queue-filename");
            container.Add(filename);
            
            // Text (truncated)
            var displayText = item.Text.Length > 40 ? item.Text.Substring(0, 40) + "..." : item.Text;
            var textLabel = new Label(displayText);
            textLabel.AddToClassList("queue-text");
            container.Add(textLabel);
            
            // Remove button
            var removeBtn = new Button(() =>
            {
                _items.RemoveAt(index);
                RefreshQueueList();
            }) { text = "✕" };
            removeBtn.AddToClassList("queue-remove");
            container.Add(removeBtn);
            
            return container;
        }
        
        private string GetStatusIcon(ItemStatus status)
        {
            return status switch
            {
                ItemStatus.Pending => "○",
                ItemStatus.Processing => "◐",
                ItemStatus.Completed => "●",
                ItemStatus.Error => "✕",
                _ => "○"
            };
        }
        
        #endregion
        
        #region Generation
        
        private void StartGeneration()
        {
            if (_items.Count == 0 || string.IsNullOrEmpty(_selectedVoice))
            {
                AddLog("[ERROR] Select a voice and add items first");
                return;
            }
            
            _isProcessing = true;
            _completedCount = 0;
            _errorCount = 0;
            _currentIndex = 0;
            
            foreach (var item in _items)
            {
                item.Status = ItemStatus.Pending;
                item.ErrorMessage = null;
            }
            
            EnsureOutputDirectory();
            
            AddLog($"Starting batch generation of {_items.Count} items...");
            UpdateProgress();
            RefreshQueueList();
            
            ProcessNextItem().Forget();
        }
        
        private async UniTaskVoid ProcessNextItem()
        {
            while (_isProcessing && _currentIndex < _items.Count)
            {
                var item = _items[_currentIndex];
                item.Status = ItemStatus.Processing;
                
                if (_statusLabel != null)
                {
                    _statusLabel.text = $"Processing: {item.OutputFileName}";
                }
                
                RefreshQueueList();
                
                try
                {
                    var client = new VarcoApiClient();
                    // Use item's voice (mapped from character) or default
                    var voiceToUse = !string.IsNullOrEmpty(item.VoiceName) ? item.VoiceName : _selectedVoice;
                    var clip = await client.SynthesizeAsync(item.Text, voiceToUse, _language, _speed, _pitch);
                    
                    var filePath = $"{_outputPath}/{item.OutputFileName}.wav";
                    SaveAudioClip(clip, filePath);
                    
                    item.Status = ItemStatus.Completed;
                    item.GeneratedClip = clip;
                    _completedCount++;
                    
                    AddLog($"[OK] {item.OutputFileName}");
                }
                catch (Exception ex)
                {
                    item.Status = ItemStatus.Error;
                    item.ErrorMessage = ex.Message;
                    _errorCount++;
                    
                    AddLog($"[ERROR] {item.OutputFileName}: {ex.Message}");
                }
                
                _currentIndex++;
                UpdateProgress();
                RefreshQueueList();
                
                await UniTask.Delay(100);
            }
            
            _isProcessing = false;
            
            if (_statusLabel != null)
            {
                _statusLabel.text = $"Complete! {_completedCount} generated, {_errorCount} errors";
            }
            
            AssetDatabase.Refresh();
            AddLog($"Batch generation complete: {_completedCount} success, {_errorCount} errors");
        }
        
        private void CancelGeneration()
        {
            _isProcessing = false;
            if (_statusLabel != null)
            {
                _statusLabel.text = "Cancelled";
            }
            AddLog("Generation cancelled by user");
        }
        
        private void ExportAllToLibrary()
        {
            const string LIBRARY_FOLDER = "Assets/VARCOExports";
            
            var completedItems = _items.FindAll(i => i.Status == ItemStatus.Completed && i.GeneratedClip != null);
            
            if (completedItems.Count == 0)
            {
                AddLog("[ERROR] No completed items to export");
                return;
            }
            
            if (!AssetDatabase.IsValidFolder(LIBRARY_FOLDER))
            {
                AssetDatabase.CreateFolder("Assets", "VARCOExports");
            }
            
            int exported = 0;
            foreach (var item in completedItems)
            {
                string fileName = $"{item.OutputFileName}.wav";
                string path = $"{LIBRARY_FOLDER}/{fileName}";
                SaveAudioClip(item.GeneratedClip, path);
                exported++;
            }
            
            AssetDatabase.Refresh();
            AddLog($"[OK] Exported {exported} items to Library");
            
            if (_statusLabel != null)
            {
                _statusLabel.text = $"Exported {exported} items to VARCOExports";
            }
        }
        
        private void UpdateProgress()
        {
            if (_progressBar != null)
            {
                float progress = _items.Count > 0 ? (float)(_completedCount + _errorCount) / _items.Count : 0;
                _progressBar.value = progress * 100;
                _progressBar.title = $"{progress * 100:F0}%";
            }
            
            if (_completedLabel != null)
            {
                _completedLabel.text = $"Completed: {_completedCount}";
            }
            
            if (_errorLabel != null)
            {
                _errorLabel.text = $"Errors: {_errorCount}";
            }
        }
        
        #endregion
        
        #region Helpers
        
        private void BrowseOutputPath()
        {
            var path = EditorUtility.OpenFolderPanel("Select Output Folder", "Assets", "");
            if (!string.IsNullOrEmpty(path))
            {
                if (path.StartsWith(Application.dataPath))
                {
                    _outputPath = "Assets" + path.Substring(Application.dataPath.Length);
                    if (_outputPathField != null)
                    {
                        _outputPathField.value = _outputPath;
                    }
                }
            }
        }
        
        private void EnsureOutputDirectory()
        {
            if (!AssetDatabase.IsValidFolder(_outputPath))
            {
                var parts = _outputPath.Split('/');
                var current = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    var next = current + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(next))
                    {
                        AssetDatabase.CreateFolder(current, parts[i]);
                    }
                    current = next;
                }
            }
        }
        
        private void SaveAudioClip(AudioClip clip, string path)
        {
            var samples = new float[clip.samples * clip.channels];
            clip.GetData(samples, 0);
            
            var fullPath = Path.Combine(Application.dataPath.Replace("Assets", ""), path);
            var dir = Path.GetDirectoryName(fullPath);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            
            using (var fs = new FileStream(fullPath, FileMode.Create))
            using (var writer = new BinaryWriter(fs))
            {
                int hz = clip.frequency;
                int channels = clip.channels;
                int sampleCount = samples.Length;
                
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + sampleCount * 2);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
                writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)channels);
                writer.Write(hz);
                writer.Write(hz * channels * 2);
                writer.Write((short)(channels * 2));
                writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                writer.Write(sampleCount * 2);
                
                foreach (var sample in samples)
                {
                    writer.Write((short)(sample * 32767f));
                }
            }
        }
        
        private void AddLog(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            _logs.Add($"[{timestamp}] {message}");
            
            while (_logs.Count > 100)
            {
                _logs.RemoveAt(0);
            }
            
            RefreshLogList();
        }
        
        private void RefreshLogList()
        {
            if (_logList == null) return;
            
            _logList.Clear();
            
            foreach (var log in _logs)
            {
                var entry = new Label(log);
                entry.AddToClassList("log-entry");
                
                if (log.Contains("[ERROR]"))
                    entry.AddToClassList("log-entry--error");
                else if (log.Contains("[OK]"))
                    entry.AddToClassList("log-entry--success");
                
                _logList.Add(entry);
            }
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
            
            menu.AddItem(new GUIContent("Documentation"), false, () =>
            {
                // Open README in default text editor or browser
                var readmePath = "Packages/com.varco.voice/README.md";
                var fullPath = System.IO.Path.GetFullPath(readmePath);
                if (System.IO.File.Exists(fullPath))
                {
                    EditorUtility.RevealInFinder(fullPath);
                }
                else
                {
                    Application.OpenURL("https://api.varco.ai/ko");
                }
            });

            menu.AddSeparator("");

            menu.AddItem(new GUIContent("About VARCO Voice"), false, () =>
            {
                EditorUtility.DisplayDialog("VARCO Voice", VarcoVersion.AboutDialogText, "OK");
            });

            menu.ShowAsContext();
        }

        #endregion

        #region Tabs
        
        private void SetupTabs()
        {
            if (_tabQueueLbl != null) _tabQueueLbl.RegisterCallback<ClickEvent>(_ => SwitchTab(true));
            if (_tabMappingLbl != null) _tabMappingLbl.RegisterCallback<ClickEvent>(_ => SwitchTab(false));
            
            // Default to Queue
            SwitchTab(true);
        }
        
        private void SwitchTab(bool showQueue)
        {
            if (_viewQueue != null) _viewQueue.style.display = showQueue ? DisplayStyle.Flex : DisplayStyle.None;
            if (_viewMapping != null) _viewMapping.style.display = showQueue ? DisplayStyle.None : DisplayStyle.Flex;
            
            if (_tabQueueLbl != null)
            {
                if (showQueue) _tabQueueLbl.AddToClassList("tab-label--active");
                else _tabQueueLbl.RemoveFromClassList("tab-label--active");
            }
            
            if (_tabMappingLbl != null)
            {
                if (!showQueue) _tabMappingLbl.AddToClassList("tab-label--active");
                else _tabMappingLbl.RemoveFromClassList("tab-label--active");
            }
        }
        
        #endregion
        
    }
}
