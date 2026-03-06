using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace VARCOVoice.Editor
{
    /// <summary>
    /// Voice Comparison Window for A/B testing voices - UI Toolkit version
    /// </summary>
    public class VoiceComparisonWindow : EditorWindow
    {
        #region Constants
        
        private const string UXML_PATH = "Packages/com.varco.voice/Editor/UI/VoiceComparisonWindow.uxml";
        private const string USS_PATH = "Packages/com.varco.voice/Editor/UI/VoiceComparisonWindow.uss";
        private const string THEME_USS_PATH = "Packages/com.varco.voice/Editor/UI/Theme.uss";
        
        #endregion
        
        #region Private Fields
        
        // Voice data
        private string _voiceA = "";
        private string _voiceB = "";
        private AudioClip _clipA;
        private AudioClip _clipB;
        private bool _isGeneratingA = false;
        private bool _isGeneratingB = false;
        
        // Parameters
        private Language _language = Language.Korean;
        private float _speed = 1.0f;
        private float _pitch = 1.0f;
        
        // Audio
        private AudioSource _audioSource;
        private bool _isPlaying = false;
        private CancellationTokenSource _playSequenceCts;
        
        // UI Elements
        private VisualElement _root;
        private TextField _comparisonText;
        private TextField _voiceAName;
        private TextField _voiceBName;
        private Label _durationA;
        private Label _durationB;
        private Label _statusText;
        private Button _generateABtn;
        private Button _generateBBtn;
        private Button _playABtn;
        private Button _playBBtn;
        
        #endregion
        
        #region Menu Item
        
        [MenuItem("Window/VARCO Voice/Voice Comparison")]
        public static void ShowWindow()
        {
            var window = GetWindow<VoiceComparisonWindow>();
            window.titleContent = new GUIContent("Voice Comparison");
            window.minSize = new Vector2(450, 400);
            window.Show();
        }
        
        #endregion
        
        #region Unity Lifecycle
        
        private void CreateGUI()
        {
            _root = rootVisualElement;
            
            // Load Theme (Robust Lookup)
            var themeGuids = AssetDatabase.FindAssets("Theme t:StyleSheet");
            StyleSheet themeSheet = null;
            if (themeGuids.Length > 0)
            {
                var themePath = AssetDatabase.GUIDToAssetPath(themeGuids[0]);
                themeSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(themePath);
            }
            
            if (themeSheet != null)
            {
                _root.styleSheets.Add(themeSheet);
            }
            else 
            {
                // Fallback or Error
                Debug.LogWarning("[VARCO] Theme.uss not found. UI styling may be broken.");
            }
            
            // Load UXML
            // Load UXML (Robust Lookup)
            var uxmlGuids = AssetDatabase.FindAssets("VoiceComparisonWindow t:VisualTreeAsset");
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
                }
            }
            else
            {
                _root.Add(new Label("Failed to load UI. VoiceComparisonWindow.uxml not found."));
            }
            
            // Load Window USS (Overrides)
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(USS_PATH);
            if (styleSheet == null)
            {
                styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                    "Assets/VARCOVoice-Unity/Editor/UI/VoiceComparisonWindow.uss");
            }
            if (styleSheet != null)
            {
                _root.styleSheets.Add(styleSheet);
            }
            
            // Apply theme
            VarcoTheme.Subscribe(_root);
            
            CacheUIElements();
            SetupEventHandlers();
        }
        
        private void OnDisable()
        {
            CancelPlaySequence();
            if (_audioSource != null)
            {
                DestroyImmediate(_audioSource.gameObject);
            }
        }
        
        #endregion
        
        #region UI Setup
        
        private void CacheUIElements()
        {
            _comparisonText = _root.Q<TextField>("comparison-text");
            _voiceAName = _root.Q<TextField>("voice-a-name");
            _voiceBName = _root.Q<TextField>("voice-b-name");
            _durationA = _root.Q<Label>("duration-a");
            _durationB = _root.Q<Label>("duration-b");
            _statusText = _root.Q<Label>("status-text");
            _generateABtn = _root.Q<Button>("generate-a-btn");
            _generateBBtn = _root.Q<Button>("generate-b-btn");
            _playABtn = _root.Q<Button>("play-a-btn");
            _playBBtn = _root.Q<Button>("play-b-btn");
            
            // Set default text
            if (_comparisonText != null)
            {
                _comparisonText.value = "안녕하세요. 바르코 보이스 비교 테스트입니다.";
            }
        }
        
        private void SetupEventHandlers()
        {
            // Voice Picker button
            var pickerBtn = _root.Q<Button>("picker-btn");
            if (pickerBtn != null)
            {
                pickerBtn.clicked += () => VoicePickerWindow.ShowWindow();
            }

            var settingsBtn = _root.Q<Button>("settings-btn");
            if (settingsBtn != null)
            {
                settingsBtn.clicked += OpenSettingsMenu;
            }
            
            // Voice selection dropdown buttons (replace paste with dropdown)
            var pasteABtn = _root.Q<Button>("paste-a-btn");
            var pasteBBtn = _root.Q<Button>("paste-b-btn");
            if (pasteABtn != null) pasteABtn.clicked += () => ShowVoiceDropdown(_voiceAName);
            if (pasteBBtn != null) pasteBBtn.clicked += () => ShowVoiceDropdown(_voiceBName);
            
            // Generate buttons
            if (_generateABtn != null) _generateABtn.clicked += () => GenerateVoiceA().Forget();
            if (_generateBBtn != null) _generateBBtn.clicked += () => GenerateVoiceB().Forget();
            
            // Play buttons
            if (_playABtn != null) _playABtn.clicked += () => PlayClip(_clipA);
            if (_playBBtn != null) _playBBtn.clicked += () => PlayClip(_clipB);
            
            // Action buttons
            var generateBothBtn = _root.Q<Button>("generate-both-btn");
            var playSequenceBtn = _root.Q<Button>("play-sequence-btn");
            var stopBtn = _root.Q<Button>("stop-btn");
            
            if (generateBothBtn != null) generateBothBtn.clicked += GenerateBoth;
            if (playSequenceBtn != null) playSequenceBtn.clicked += StartPlaySequence;
            if (stopBtn != null) stopBtn.clicked += StopPlayback;
            
            // Export buttons
            var exportABtn = _root.Q<Button>("export-a-btn");
            var exportBBtn = _root.Q<Button>("export-b-btn");
            if (exportABtn != null) exportABtn.clicked += () => ExportToLibrary(_clipA, _voiceA);
            if (exportBBtn != null) exportBBtn.clicked += () => ExportToLibrary(_clipB, _voiceB);
            
            
            // Voice name fields
            if (_voiceAName != null) _voiceAName.RegisterValueChangedCallback(evt => _voiceA = evt.newValue);
            if (_voiceBName != null) _voiceBName.RegisterValueChangedCallback(evt => _voiceB = evt.newValue);
        }
        
        /// <summary>
        /// 즐겨찾기 및 최근 사용 보이스 드롭다운 표시
        /// </summary>
        private void ShowVoiceDropdown(TextField targetField)
        {
            if (targetField == null) return;
            
            var menu = new GenericMenu();
            string currentValue = targetField.value;
            
            // 즐겨찾기
            var favorites = VoiceFavorites.Favorites;
            if (favorites.Count > 0)
            {
                menu.AddDisabledItem(new GUIContent("★ Favorites"));
                foreach (var fav in favorites)
                {
                    string voiceName = fav;
                    menu.AddItem(new GUIContent($"  {fav}"), currentValue == fav, () =>
                    {
                        targetField.value = voiceName;
                    });
                }
                menu.AddSeparator("");
            }
            
            // 최근 사용
            var recents = VoiceFavorites.RecentVoices;
            if (recents.Count > 0)
            {
                menu.AddDisabledItem(new GUIContent("Recent"));
                int count = 0;
                foreach (var recent in recents)
                {
                    if (count++ >= 10) break;
                    string voiceName = recent;
                    menu.AddItem(new GUIContent($"  {recent}"), currentValue == recent, () =>
                    {
                        targetField.value = voiceName;
                    });
                }
                menu.AddSeparator("");
            }
            
            // 전체 목록
            menu.AddItem(new GUIContent("Browse All Voices..."), false, () =>
            {
                VoicePickerWindow.ShowWindow();
            });
            
            menu.ShowAsContext();
        }
        
        #endregion
        
        #region Generation
        
        private async UniTaskVoid GenerateVoiceA()
        {
            if (string.IsNullOrEmpty(_voiceA))
            {
                UpdateStatus("Please enter Voice A name");
                return;
            }
            
            _isGeneratingA = true;
            UpdateButtonState();
            UpdateStatus($"Generating {_voiceA}...");
            
            try
            {
                var text = _comparisonText?.value ?? "";
                var client = new VarcoApiClient();
                _clipA = await client.SynthesizeAsync(text, _voiceA, _language, _speed, _pitch);
                
                if (_durationA != null) _durationA.text = $"{_clipA.length:F2}s";
                UpdateStatus($"Generated {_voiceA} successfully!");
            }
            catch (VarcoException ex)
            {
                UpdateStatus($"Error: {ex.Message}");
            }
            finally
            {
                _isGeneratingA = false;
                UpdateButtonState();
            }
        }
        
        private async UniTaskVoid GenerateVoiceB()
        {
            if (string.IsNullOrEmpty(_voiceB))
            {
                UpdateStatus("Please enter Voice B name");
                return;
            }
            
            _isGeneratingB = true;
            UpdateButtonState();
            UpdateStatus($"Generating {_voiceB}...");
            
            try
            {
                var text = _comparisonText?.value ?? "";
                var client = new VarcoApiClient();
                _clipB = await client.SynthesizeAsync(text, _voiceB, _language, _speed, _pitch);
                
                if (_durationB != null) _durationB.text = $"{_clipB.length:F2}s";
                UpdateStatus($"Generated {_voiceB} successfully!");
            }
            catch (VarcoException ex)
            {
                UpdateStatus($"Error: {ex.Message}");
            }
            finally
            {
                _isGeneratingB = false;
                UpdateButtonState();
            }
        }
        
        private void GenerateBoth()
        {
            GenerateVoiceA().Forget();
            GenerateVoiceB().Forget();
        }
        
        #endregion
        
        #region Playback
        
        private void PlayClip(AudioClip clip)
        {
            if (clip == null)
            {
                UpdateStatus("No audio generated yet");
                return;
            }
            
            EnsureAudioSource();
            
            if (_isPlaying && _audioSource.clip == clip)
            {
                StopPlayback();
            }
            else
            {
                _audioSource.clip = clip;
                _audioSource.Play();
                _isPlaying = true;
            }
        }

        private void StartPlaySequence()
        {
            CancelPlaySequence();
            _playSequenceCts = new CancellationTokenSource();
            PlaySequenceAsync(_playSequenceCts.Token).Forget();
        }

        private void CancelPlaySequence()
        {
            if (_playSequenceCts == null) return;

            if (!_playSequenceCts.IsCancellationRequested)
            {
                _playSequenceCts.Cancel();
            }

            _playSequenceCts.Dispose();
            _playSequenceCts = null;
        }

        private async UniTask PlaySequenceAsync(CancellationToken cancellationToken)
        {
            if (_clipA == null || _clipB == null)
            {
                UpdateStatus("Generate both voices first");
                return;
            }
            
            EnsureAudioSource();
            
            UpdateStatus("Playing Voice A...");
            _audioSource.clip = _clipA;
            _audioSource.Play();
            _isPlaying = true;
            
            try
            {
                await UniTask.Delay((int)(_clipA.length * 1000) + 500, cancellationToken: cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            
            if (cancellationToken.IsCancellationRequested || !_isPlaying) return;
            
            UpdateStatus("Playing Voice B...");
            _audioSource.clip = _clipB;
            _audioSource.Play();
            
            try
            {
                await UniTask.Delay((int)(_clipB.length * 1000), cancellationToken: cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (cancellationToken.IsCancellationRequested) return;
            
            _isPlaying = false;
            UpdateStatus("Comparison complete!");
        }
        
        private void StopPlayback()
        {
            if (_audioSource != null)
            {
                _audioSource.Stop();
            }
            _isPlaying = false;
            CancelPlaySequence();
            UpdateStatus("");
        }
        
        private void EnsureAudioSource()
        {
            if (_audioSource == null)
            {
                var go = new GameObject("[VoiceComparison]");
                go.hideFlags = HideFlags.HideAndDontSave;
                _audioSource = go.AddComponent<AudioSource>();
            }
        }
        
        private void ExportToLibrary(AudioClip clip, string voiceName)
        {
            if (clip == null)
            {
                UpdateStatus("No audio to export");
                return;
            }
            
            const string LIBRARY_FOLDER = "Assets/VARCOExports";
            
            if (!AssetDatabase.IsValidFolder(LIBRARY_FOLDER))
            {
                AssetDatabase.CreateFolder("Assets", "VARCOExports");
            }
            
            string safeName = string.IsNullOrEmpty(voiceName) ? "compare" : voiceName.Replace(" ", "_");
            string fileName = $"{safeName}_{System.DateTime.Now:HHmmss}.wav";
            string path = $"{LIBRARY_FOLDER}/{fileName}";
            
            SaveAudioClip(clip, path);
            AssetDatabase.Refresh();
            
            UpdateStatus($"Exported: {fileName}");
        }
        
        private void SaveAudioClip(AudioClip clip, string path)
        {
            var samples = new float[clip.samples * clip.channels];
            clip.GetData(samples, 0);
            
            var fullPath = System.IO.Path.Combine(Application.dataPath.Replace("Assets", ""), path);
            
            using (var fs = new System.IO.FileStream(fullPath, System.IO.FileMode.Create))
            using (var writer = new System.IO.BinaryWriter(fs))
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
                 // User requested "Main Settings Page's Docu Popup". 
                 // Assuming they mean the "About" dialog which contains version info.
                 EditorUtility.DisplayDialog("VARCO Voice", "VARCO Voice Unity SDK\nVersion 1.0.0\n\n(c) NC AI", "OK");
            });

            menu.AddItem(new GUIContent("About VARCO Voice"), false, () =>
            {
                Application.OpenURL("https://voice.varco.ai/");
            });

            menu.ShowAsContext();
        }

        #endregion

        #region UI Helpers
        
        private void UpdateStatus(string message)
        {
            if (_statusText != null)
            {
                _statusText.text = message;
            }
        }
        
        private void UpdateButtonState()
        {
            if (_generateABtn != null) _generateABtn.SetEnabled(!_isGeneratingA);
            if (_generateBBtn != null) _generateBBtn.SetEnabled(!_isGeneratingB);
            
            if (_generateABtn != null) _generateABtn.text = _isGeneratingA ? "Generating..." : "Generate";
            if (_generateBBtn != null) _generateBBtn.text = _isGeneratingB ? "Generating..." : "Generate";
        }
        
        #endregion
    }
}
