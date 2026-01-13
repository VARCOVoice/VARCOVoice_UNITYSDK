using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Cysharp.Threading.Tasks;

namespace VARCOVoice.Editor
{
    /// <summary>
    /// Editor window for browsing and selecting VARCO voices
    /// </summary>
    public class VoicePickerWindow : EditorWindow
    {
        #region Private Fields
        
        private List<VarcoVoice> _voices = new List<VarcoVoice>();
        private List<VarcoVoice> _filteredVoices = new List<VarcoVoice>();
        
        // Filters
        private string _searchText = "";
        private Gender _genderFilter = Gender.Unknown;
        private AgeGroup _ageFilter = AgeGroup.Unknown;
        private EmotionType _emotionFilter = EmotionType.Neutral;
        private bool _filterByEmotion = false;
        
        // UI State
        private Vector2 _scrollPosition;
        private int _selectedIndex = -1;
        private bool _isLoading = false;
        private string _statusMessage = "";
        
        // Preview
        private AudioSource _previewSource;
        private AudioClip _previewClip;
        private string _previewText = "안녕하세요. 바르코 보이스 테스트입니다.";
        
        // Styling
        private GUIStyle _headerStyle;
        private GUIStyle _voiceButtonStyle;
        private GUIStyle _selectedStyle;
        
        // Pagination
        private int _pageSize = 50;
        private int _currentPage = 0;
        
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
        
        [MenuItem("Window/VARCO Voice/Settings")]
        public static void ShowSettings()
        {
            SettingsService.OpenProjectSettings("Project/VARCO Voice");
        }
        
        #endregion
        
        #region Unity Lifecycle
        
        private void OnEnable()
        {
            InitializeStyles();
            LoadVoicesAsync().Forget();
        }
        
        private void OnDisable()
        {
            if (_previewSource != null)
            {
                DestroyImmediate(_previewSource.gameObject);
            }
        }
        
        #endregion
        
        #region GUI
        
        private void OnGUI()
        {
            InitializeStyles();
            
            EditorGUILayout.Space(5);
            
            DrawHeader();
            
            EditorGUILayout.Space(5);
            
            DrawFilters();
            
            EditorGUILayout.Space(5);
            
            DrawVoiceList();
            
            EditorGUILayout.Space(5);
            
            DrawPreviewSection();
            
            EditorGUILayout.Space(5);
            
            DrawStatusBar();
        }
        
        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal();
            
            GUILayout.Label("VARCO Voice Picker", _headerStyle);
            
            GUILayout.FlexibleSpace();
            
            if (GUILayout.Button("Refresh", GUILayout.Width(80)))
            {
                LoadVoicesAsync(forceRefresh: true).Forget();
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.LabelField($"Total: {_voices.Count} voices | Filtered: {_filteredVoices.Count}");
        }
        
        private void DrawFilters()
        {
            EditorGUILayout.LabelField("Filters", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            // Search
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Search:", GUILayout.Width(60));
            var newSearch = EditorGUILayout.TextField(_searchText);
            if (newSearch != _searchText)
            {
                _searchText = newSearch;
                ApplyFilters();
            }
            EditorGUILayout.EndHorizontal();
            
            // Gender
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Gender:", GUILayout.Width(60));
            var newGender = (Gender)EditorGUILayout.EnumPopup(_genderFilter);
            if (newGender != _genderFilter)
            {
                _genderFilter = newGender;
                ApplyFilters();
            }
            EditorGUILayout.EndHorizontal();
            
            // Age
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Age:", GUILayout.Width(60));
            var newAge = (AgeGroup)EditorGUILayout.EnumPopup(_ageFilter);
            if (newAge != _ageFilter)
            {
                _ageFilter = newAge;
                ApplyFilters();
            }
            EditorGUILayout.EndHorizontal();
            
            // Emotion
            EditorGUILayout.BeginHorizontal();
            _filterByEmotion = EditorGUILayout.Toggle(_filterByEmotion, GUILayout.Width(20));
            EditorGUILayout.LabelField("Emotion:", GUILayout.Width(55));
            GUI.enabled = _filterByEmotion;
            var newEmotion = (EmotionType)EditorGUILayout.EnumPopup(_emotionFilter);
            if (newEmotion != _emotionFilter)
            {
                _emotionFilter = newEmotion;
                ApplyFilters();
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
            
            // Clear filters
            if (GUILayout.Button("Clear Filters"))
            {
                _searchText = "";
                _genderFilter = Gender.Unknown;
                _ageFilter = AgeGroup.Unknown;
                _emotionFilter = EmotionType.Neutral;
                _filterByEmotion = false;
                ApplyFilters();
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawVoiceList()
        {
            EditorGUILayout.LabelField("Voices", EditorStyles.boldLabel);
            
            if (_isLoading)
            {
                EditorGUILayout.HelpBox("Loading voices...", MessageType.Info);
                return;
            }
            
            if (_filteredVoices.Count == 0)
            {
                EditorGUILayout.HelpBox("No voices found. Try adjusting filters or check API settings.", MessageType.Warning);
                return;
            }
            
            // Pagination
            int totalPages = Mathf.CeilToInt(_filteredVoices.Count / (float)_pageSize);
            int startIndex = _currentPage * _pageSize;
            int endIndex = Mathf.Min(startIndex + _pageSize, _filteredVoices.Count);
            
            // Page controls
            EditorGUILayout.BeginHorizontal();
            GUI.enabled = _currentPage > 0;
            if (GUILayout.Button("◀ Prev", GUILayout.Width(70)))
            {
                _currentPage--;
            }
            GUI.enabled = true;
            
            GUILayout.FlexibleSpace();
            GUILayout.Label($"Page {_currentPage + 1} / {totalPages}");
            GUILayout.FlexibleSpace();
            
            GUI.enabled = _currentPage < totalPages - 1;
            if (GUILayout.Button("Next ▶", GUILayout.Width(70)))
            {
                _currentPage++;
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
            
            // Voice list
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(250));
            
            for (int i = startIndex; i < endIndex; i++)
            {
                var voice = _filteredVoices[i];
                bool isSelected = i == _selectedIndex;
                
                var style = isSelected ? _selectedStyle : _voiceButtonStyle;
                
                EditorGUILayout.BeginHorizontal(style);
                
                // Voice info
                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField(voice.SpeakerName, EditorStyles.boldLabel);
                EditorGUILayout.LabelField(voice.Description, EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();
                
                // Select button
                if (GUILayout.Button("Select", GUILayout.Width(60)))
                {
                    _selectedIndex = i;
                    Repaint();
                }
                
                // Preview button
                if (GUILayout.Button("▶", GUILayout.Width(30)))
                {
                    PreviewVoice(voice).Forget();
                }
                
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.EndScrollView();
        }
        
        private void DrawPreviewSection()
        {
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            _previewText = EditorGUILayout.TextField("Text:", _previewText);
            
            EditorGUILayout.BeginHorizontal();
            
            GUI.enabled = _selectedIndex >= 0;
            if (GUILayout.Button("Preview Selected Voice"))
            {
                if (_selectedIndex >= 0 && _selectedIndex < _filteredVoices.Count)
                {
                    PreviewVoice(_filteredVoices[_selectedIndex]).Forget();
                }
            }
            GUI.enabled = true;
            
            if (GUILayout.Button("Stop"))
            {
                StopPreview();
            }
            
            EditorGUILayout.EndHorizontal();
            
            // Show selected voice info
            if (_selectedIndex >= 0 && _selectedIndex < _filteredVoices.Count)
            {
                var selected = _filteredVoices[_selectedIndex];
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Selected:", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Name: {selected.SpeakerName}");
                EditorGUILayout.LabelField($"UUID: {selected.SpeakerUuid}");
                
                if (GUILayout.Button("Copy Voice Name to Clipboard"))
                {
                    EditorGUIUtility.systemCopyBuffer = selected.SpeakerName;
                    _statusMessage = $"Copied: {selected.SpeakerName}";
                }
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawStatusBar()
        {
            if (!string.IsNullOrEmpty(_statusMessage))
            {
                EditorGUILayout.HelpBox(_statusMessage, MessageType.Info);
            }
        }
        
        #endregion
        
        #region Methods
        
        private void InitializeStyles()
        {
            if (_headerStyle == null)
            {
                _headerStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 16
                };
            }
            
            if (_voiceButtonStyle == null)
            {
                _voiceButtonStyle = new GUIStyle(EditorStyles.helpBox)
                {
                    padding = new RectOffset(5, 5, 5, 5),
                    margin = new RectOffset(0, 0, 2, 2)
                };
            }
            
            if (_selectedStyle == null)
            {
                _selectedStyle = new GUIStyle(_voiceButtonStyle);
                _selectedStyle.normal.background = MakeTexture(2, 2, new Color(0.3f, 0.5f, 0.8f, 0.3f));
            }
        }
        
        private Texture2D MakeTexture(int width, int height, Color color)
        {
            var pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }
            
            var texture = new Texture2D(width, height);
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }
        
        private async UniTaskVoid LoadVoicesAsync(bool forceRefresh = false)
        {
            if (!VarcoConfig.Instance.IsValid())
            {
                _statusMessage = "API Key not configured. Go to Project Settings > VARCO Voice";
                return;
            }
            
            _isLoading = true;
            _statusMessage = "Loading voices...";
            Repaint();
            
            try
            {
                var client = new VarcoApiClient();
                _voices = await client.GetVoicesAsync(forceRefresh);
                ApplyFilters();
                _statusMessage = $"Loaded {_voices.Count} voices";
            }
            catch (VarcoException ex)
            {
                _statusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                _isLoading = false;
                Repaint();
            }
        }
        
        private void ApplyFilters()
        {
            var filter = new VoiceFilter
            {
                SearchText = _searchText,
                Gender = _genderFilter == Gender.Unknown ? null : _genderFilter,
                AgeGroup = _ageFilter == AgeGroup.Unknown ? null : _ageFilter,
                Emotion = _filterByEmotion ? _emotionFilter : null
            };
            
            _filteredVoices.Clear();
            foreach (var voice in _voices)
            {
                if (filter.Matches(voice))
                {
                    _filteredVoices.Add(voice);
                }
            }
            
            _currentPage = 0;
            _selectedIndex = -1;
            Repaint();
        }
        
        private async UniTaskVoid PreviewVoice(VarcoVoice voice)
        {
            _statusMessage = $"Generating preview for {voice.SpeakerName}...";
            Repaint();
            
            try
            {
                var client = new VarcoApiClient();
                _previewClip = await client.SynthesizeAsync(_previewText, voice.SpeakerName);
                
                EnsurePreviewSource();
                _previewSource.PlayOneShot(_previewClip);
                
                _statusMessage = $"Playing: {voice.SpeakerName}";
            }
            catch (VarcoException ex)
            {
                _statusMessage = $"Preview failed: {ex.Message}";
            }
            
            Repaint();
        }
        
        private void EnsurePreviewSource()
        {
            if (_previewSource == null)
            {
                var go = new GameObject("[VoicePreview]");
                go.hideFlags = HideFlags.HideAndDontSave;
                _previewSource = go.AddComponent<AudioSource>();
            }
        }
        
        private void StopPreview()
        {
            if (_previewSource != null)
            {
                _previewSource.Stop();
            }
            _statusMessage = "";
        }
        
        #endregion
    }
}
