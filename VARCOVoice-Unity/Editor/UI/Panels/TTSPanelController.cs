using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using System.Threading.Tasks;

namespace VARCOVoice.Editor
{
    /// <summary>
    /// TTS Panel Controller - handles UI logic for TTS generation panel
    /// </summary>
    public partial class TTSPanelController
    {
        #region Private Fields
        
        private VisualElement _root;
        private TextField _textInput;
        private DropdownField _voiceADropdown;
        private DropdownField _voiceBDropdown;
        private Button _abToggleBtn;
        private bool _abEnabled = true;
        private VisualElement _voiceBSection;
        private VisualElement _abComparison;
        private TextField _voiceSearchField;
        
        // Voice Picker
        private Button _voiceAExpandBtn;
        private Button _voiceBExpandBtn;
        private ScrollView _voiceAPicker;
        private ScrollView _voiceBPicker;
        private Label _voiceANameLabel;
        private Label _voiceBNameLabel;
        private string _selectedVoiceA;
        private string _selectedVoiceB;
        
        // Language Selection
        private DropdownField _languageDropdown;
        private Language _selectedLanguage = Language.Korean;
        
        private Slider _speedSlider;
        private Slider _pitchSlider;
        private Slider _volumeSlider;
        private Label _speedValue;
        private Label _pitchValue;
        private Label _volumeValue;
        
        private Button _generateBtn;
        private Button _toDspBtn; // Promoted
        private Button _playBtn;
        private Button _stopBtn;
        private VisualElement _progressFill;
        private Label _timeLabel;
        private Label _statusText;
        private VisualElement _statusIndicator;
        private ProgressBar _generationProgress;
        
        private List<VarcoVoice> _voices = new List<VarcoVoice>();
        private AudioClip _generatedClipA;
        private AudioClip _generatedClipB;
        private AudioClip _currentClip;


        private AudioSource _previewSource; // Managed AudioSource for reliable sound
        private bool _isPlaying;
        private bool _isGenerating;
        
        // Rotary Knob Controllers
        private RotaryKnobController _speedKnob;
        private RotaryKnobController _pitchKnob;
        private RotaryKnobController _volumeKnob;
        
        // Waveform Visualization
        private WaveformRenderController _waveformRenderer;

        // Playback UI
        private Label _playbackTimeLabel;
        private Button _playPauseBtn;
        private Button _stopPlaybackBtn;
        private Button _setLoopABtn;
        private Button _setLoopBBtn;
        private Button _clearLoopBtn;
        private Slider _scrubSlider;
        private bool _isPaused;
        private bool _isScrubbing;
        private bool _hasLoopA;
        private bool _hasLoopB;
        private float _loopASeconds;
        private float _loopBSeconds;
        
        // Zoom & Pan
        private VisualElement _waveformContainer;
        private VisualElement _waveformContent;
        private float _zoomLevel = 1.0f;
        private float _scrollRatio = 0.0f; // 0.0 to 1.0
        private bool _isPanningWaveform;
        private Vector2 _lastMousePos;
        private double _lastUpdateTime;

                // Events
        public event System.Action<AudioClip> OnSendToDSP;
        public AudioClip CurrentClip => _currentClip ?? _generatedClipA ?? _generatedClipB;

        #endregion
        
        #region Initialization
        
        public void Initialize(VisualElement root)
        {
            _root = root;

            // Standardize padding (override .panel style)
            _root.style.paddingTop = 0;
            _root.style.paddingBottom = 0;
            _root.style.paddingLeft = 0;
            _root.style.paddingRight = 0;

            try
            {
                CacheUIElements();
                InitializeSliderValues();
                InitializeRotaryKnobs();

                InitializeWaveform();
                EnsureAudioSource(); // Ensure AudioSource exists
                SetupEventHandlers();
                _ = LoadVoicesAsync();

                // Force A/B comparison visible
                UpdateABVisibility();
                
                // Register update loop for Playback
                EditorApplication.update -= OnEditorUpdate;
                EditorApplication.update += OnEditorUpdate;
                
                // Clean up on detach
                _root.RegisterCallback<DetachFromPanelEvent>(evt => 
                {
                    EditorApplication.update -= OnEditorUpdate;
                    _root.UnregisterCallback<KeyDownEvent>(OnKeyDown);
                });
                
                // Register Keyboard Events (Spacebar)
                _root.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
                // Ensure focusable for key events
                _root.focusable = true;
                _root.tabIndex = 0;

                UpdateStatus("Ready", StatusType.Success);
            }
            catch (System.Exception ex) 
            {
                 Debug.LogError($"[TTSPanel] Init Error: {ex.Message}");
            }
        }
        
        private void EnsureAudioSource()
        {
            if (_previewSource == null)
            {
                var existing = GameObject.Find("[VARCO_PreviewSource]");
                if (existing != null)
                {
                    _previewSource = existing.GetComponent<AudioSource>();
                }
                else
                {
                    var go = new GameObject("[VARCO_PreviewSource]");
                    go.hideFlags = HideFlags.HideAndDontSave;
                    _previewSource = go.AddComponent<AudioSource>();
                    _previewSource.playOnAwake = false;
                }
            }
        }

        
        private void InitializeSliderValues()
        {
            // Set slider default values programmatically (UXML values don't always apply)
            if (_speedSlider != null)
            {
                _speedSlider.lowValue = 0.5f;
                _speedSlider.highValue = 1.5f; // API Limit
                _speedSlider.value = 1.0f;
            }
            
            if (_pitchSlider != null)
            {
                _pitchSlider.lowValue = 0.5f;
                _pitchSlider.highValue = 1.5f; // API Limit safe range
                _pitchSlider.value = 1.0f;
            }
            
            if (_volumeSlider != null)
            {
                _volumeSlider.lowValue = 0f;
                _volumeSlider.highValue = 1.0f;
                _volumeSlider.value = 1.0f;
            }
            
            // Update value labels
            if (_speedValue != null) _speedValue.text = "1.0x";
            if (_pitchValue != null) _pitchValue.text = "1.0x";
            if (_volumeValue != null) _volumeValue.text = "100%";
        }
        
        private void InitializeRotaryKnobs()
        {
            // Speed Knob
            var speedContainer = _root.Q<VisualElement>("rotary-knob")?.parent?.Q<VisualElement>(className: "rotary-knob");
            if (speedContainer == null)
            {
                // Find by traversing knobs-row
                var knobsRow = _root.Q<VisualElement>(className: "knobs-row");
                if (knobsRow != null && knobsRow.childCount >= 3)
                {
                    // Speed is first knob
                    var speedKnobEl = knobsRow[0];
                    var speedIndicator = speedKnobEl.Q<VisualElement>(name: "speed-indicator") 
                        ?? speedKnobEl.Q<VisualElement>(className: "rotary-knob__indicator");
                    var speedArc = speedKnobEl.Q<VisualElement>(name: "speed-arc") 
                        ?? speedKnobEl.Q<VisualElement>(className: "rotary-knob__progress-arc");
                    var speedDialContainer = speedKnobEl.Q<VisualElement>(className: "rotary-knob__dial-container");
                    
                    _speedKnob = new RotaryKnobController();
                    _speedKnob.Initialize(speedDialContainer, speedIndicator, speedArc, 
                        _speedSlider, _speedValue, 0.5f, 1.5f, 1.0f, "{0:F1}x"); // API Limit
                    
                    // Pitch is second knob
                    var pitchKnobEl = knobsRow[1];
                    var pitchIndicator = pitchKnobEl.Q<VisualElement>(name: "pitch-indicator") 
                        ?? pitchKnobEl.Q<VisualElement>(className: "rotary-knob__indicator");
                    var pitchArc = pitchKnobEl.Q<VisualElement>(name: "pitch-arc") 
                        ?? pitchKnobEl.Q<VisualElement>(className: "rotary-knob__progress-arc");
                    var pitchDialContainer = pitchKnobEl.Q<VisualElement>(className: "rotary-knob__dial-container");
                    
                    _pitchKnob = new RotaryKnobController();
                    _pitchKnob.Initialize(pitchDialContainer, pitchIndicator, pitchArc, 
                        _pitchSlider, _pitchValue, 0.5f, 1.5f, 1.0f, "{0:F1}x"); // API Limit
                    
                    // Volume is third knob
                    var volumeKnobEl = knobsRow[2];
                    var volumeIndicator = volumeKnobEl.Q<VisualElement>(name: "volume-indicator") 
                        ?? volumeKnobEl.Q<VisualElement>(className: "rotary-knob__indicator");
                    var volumeArc = volumeKnobEl.Q<VisualElement>(name: "volume-arc") 
                        ?? volumeKnobEl.Q<VisualElement>(className: "rotary-knob__progress-arc");
                    var volumeDialContainer = volumeKnobEl.Q<VisualElement>(className: "rotary-knob__dial-container");
                    
                    _volumeKnob = new RotaryKnobController();
                    _volumeKnob.Initialize(volumeDialContainer, volumeIndicator, volumeArc, 
                        _volumeSlider, _volumeValue, 0f, 1.0f, 1.0f, "{0:P0}");
                }
            }
        }
        
        private void InitializeWaveform()
        {
            _waveformContainer = _root.Q<VisualElement>("waveform-container");
            if (_waveformContainer == null) return;
            
            _waveformContent = _root.Q<VisualElement>("waveform-content");
            var waveformImage = _root.Q<VisualElement>("waveform-image");
            var playhead = _root.Q<VisualElement>("waveform-playhead");
            
            // Register Zoom/Pan Events
            _waveformContainer.RegisterCallback<WheelEvent>(OnWaveformZoom);
            _waveformContainer.RegisterCallback<PointerDownEvent>(OnWaveformPointerDown);
            _waveformContainer.RegisterCallback<PointerMoveEvent>(OnWaveformPointerMove);
            _waveformContainer.RegisterCallback<PointerUpEvent>(OnWaveformPointerUp);
            _waveformContainer.RegisterCallback<PointerLeaveEvent>(OnWaveformPointerLeave); // Stop panning on leave
            
            _waveformRenderer = new WaveformRenderController();
            _waveformRenderer.Initialize(waveformImage, playhead);
        }

        private void OnWaveformZoom(WheelEvent evt)
        {
            if (evt.delta.y == 0) return;
            
            float zoomDelta = -Mathf.Sign(evt.delta.y) * 0.2f;
            float newZoom = Mathf.Clamp(_zoomLevel + zoomDelta, 1.0f, 10.0f);
            
            if (Mathf.Abs(newZoom - _zoomLevel) > 0.001f)
            {
                // Zoom towards mouse position? For now simple center/current ratio logic
                // To keep mouse focused: calculate mouse pct, adjust scrollRatio to keep mouse pct at same screen position
                // Implementation: Simple zoom for now, user can pan
                
                _zoomLevel = newZoom;
                UpdateWaveformZoom();
                evt.StopPropagation();
            }
        }
        
        private void OnWaveformPointerDown(PointerDownEvent evt)
        {
            if (evt.button == 2 || (evt.button == 0 && evt.altKey)) // Middle mouse or Alt+Left to pan
            {
                _isPanningWaveform = true;
                _lastMousePos = evt.position;
                _waveformContainer.CapturePointer(evt.pointerId);
                evt.StopPropagation();
            }
        }
        
        private void OnWaveformPointerMove(PointerMoveEvent evt)
        {
            if (_isPanningWaveform)
            {
                float deltaX = evt.position.x - _lastMousePos.x;
                _lastMousePos = evt.position;
                
                if (_zoomLevel > 1.0f)
                {
                    float containerWidth = _waveformContainer.resolvedStyle.width;
                    if (containerWidth > 0)
                    {
                        // Calculate scroll delta in ratio space
                        // Total width = zoom * containerWidth
                        // Scrollable width = Total - container = (zoom - 1) * container
                        float maxScrollWidth = (_zoomLevel - 1.0f) * containerWidth;
                        float offsetDelta = -deltaX; // Drag left moves view right (increases scroll)
                        
                        // Convert pixel delta to ratio: ratio = offset / maxScrollWidth
                        float ratioDelta = offsetDelta / maxScrollWidth;
                        
                        _scrollRatio = Mathf.Clamp01(_scrollRatio + ratioDelta);
                        UpdateWaveformZoom();
                    }
                }
                evt.StopPropagation();
            }
        }
        
        private void OnWaveformPointerUp(PointerUpEvent evt)
        {
            if (_isPanningWaveform)
            {
                _isPanningWaveform = false;
                _waveformContainer.ReleasePointer(evt.pointerId);
                evt.StopPropagation();
            }
        }
        
        private void OnWaveformPointerLeave(PointerLeaveEvent evt)
        {
             // Handled by Up/Out usually, duplicate fallback
             if (_isPanningWaveform)
             {
                 _isPanningWaveform = false; 
                 _waveformContainer.ReleasePointer(evt.pointerId);
             }
        }

        private void UpdateWaveformZoom()
        {
            if (_waveformContent == null) return;
            
            // Width %
            _waveformContent.style.width = new Length(_zoomLevel * 100f, LengthUnit.Percent);
            
            // Left offset %
            // Max offset is (Width% - 100%)
            // left should be negative
            float maxOffsetPercent = (_zoomLevel * 100f) - 100f;
            float currentLeftPercent = -(_scrollRatio * maxOffsetPercent);
            
            _waveformContent.style.left = new Length(currentLeftPercent, LengthUnit.Percent);
        }
        
        private void CacheUIElements()
        {
            _textInput = _root.Q<TextField>("tts-text-input");
            
            _voiceADropdown = _root.Q<DropdownField>("voice-a-dropdown");
            _voiceBDropdown = _root.Q<DropdownField>("voice-b-dropdown");
            _voiceBSection = _root.Q<VisualElement>("voice-b-section");
            _abComparison = _root.Q<VisualElement>("ab-comparison");
            
            // Voice Name Labels (NEW IDs from updated UXML)
            _voiceANameLabel = _root.Q<Label>("selected-a-name");
            _voiceBNameLabel = _root.Q<Label>("selected-b-name");
            
            // Voice Search and Sort
            _voiceSearchField = _root.Q<TextField>("voice-search");
            if (_voiceSearchField != null)
            {
                _voiceSearchField.RegisterValueChangedCallback(OnVoiceSearchChanged);
            }
            
            // Gender Filter Dropdown (replaces old buttons)
            var genderDropdown = _root.Q<DropdownField>("filter-gender");
            if (genderDropdown != null)
            {
                genderDropdown.choices = new List<string> { "All", "M", "F" };
                genderDropdown.value = "All";
                genderDropdown.RegisterValueChangedCallback(OnGenderDropdownChanged);
            }
            
            // Language Filter Dropdown
            _languageDropdown = _root.Q<DropdownField>("filter-language");
            if (_languageDropdown != null)
            {
                // Restricted to KR for 1.0.1
                _languageDropdown.choices = new List<string> { "KR", "EN (Locked)", "JP (Locked)", "TW (Locked)" };
                _languageDropdown.value = "KR";
                _languageDropdown.style.width = 80; // Adjusted width for locked labels
                _languageDropdown.style.flexGrow = 0;
                _languageDropdown.RegisterValueChangedCallback(OnLanguageChanged);
            }
            
            // Age Filter Dropdown
            var ageFilter = _root.Q<DropdownField>("filter-age");
            if (ageFilter != null)
            {
                ageFilter.choices = new List<string> { "All", "Child", "Young", "Mid", "Old" };
                ageFilter.value = "All";
                ageFilter.RegisterValueChangedCallback(OnAgeFilterChanged);
            }
            
            // Emotion Filter Dropdown
            var emotionFilter = _root.Q<DropdownField>("filter-emotion");
            if (emotionFilter != null)
            {
                emotionFilter.choices = new List<string> { "All", "Neutral", "Happy", "Sad", "Angry" };
                emotionFilter.value = "All";
                emotionFilter.RegisterValueChangedCallback(OnEmotionFilterChanged);
            }

            // Refresh Button
            var refreshBtn = _root.Q<Button>("voice-refresh-btn");
            if (refreshBtn != null)
            {
                refreshBtn.clicked += () => _ = LoadVoicesAsync(true);
            }
            
            // Parameters
            _speedSlider = _root.Q<Slider>("speed-slider");
            _pitchSlider = _root.Q<Slider>("pitch-slider");
            _volumeSlider = _root.Q<Slider>("volume-slider");
            _speedValue = _root.Q<Label>("speed-value");
            _pitchValue = _root.Q<Label>("pitch-value");
            _volumeValue = _root.Q<Label>("volume-value");
            
            // Playback
            _generateBtn = _root.Q<Button>("generate-btn");
            _playbackTimeLabel = _root.Q<Label>("time-label");
            _playPauseBtn = _root.Q<Button>("playback-play");
            _stopPlaybackBtn = _root.Q<Button>("playback-stop");
            _setLoopABtn = _root.Q<Button>("playback-set-a");
            _setLoopBBtn = _root.Q<Button>("playback-set-b");
            _clearLoopBtn = _root.Q<Button>("playback-clear");
            _scrubSlider = _root.Q<Slider>("playback-scrub");
        
            // Status
            _statusText = _root.Q<Label>("status-text");
            _statusIndicator = _root.Q<VisualElement>("status-indicator");
            _generationProgress = _root.Q<ProgressBar>("generation-progress");  
        }

        
        private void SetupEventHandlers()
        {
            // Text input
            if (_textInput != null)
            {
                _textInput.RegisterValueChangedCallback(OnTextChanged);
            }
            
            // A/B Toggle Button
            _abToggleBtn = _root.Q<Button>("ab-toggle");
            if (_abToggleBtn != null)
            {
                _abToggleBtn.clicked += OnABToggleClicked;
            }
            
            // Voice Picker Expand Buttons
            if (_voiceAExpandBtn != null)
            {
                _voiceAExpandBtn.clicked += () => ToggleVoicePicker(true);
            }
            
            if (_voiceBExpandBtn != null)
            {
                _voiceBExpandBtn.clicked += () => ToggleVoicePicker(false);
            }
            
            // Clear Voice Buttons
            var clearABtn = _root.Q<Button>("clear-a-btn");
            if (clearABtn != null)
            {
                clearABtn.clicked += () => ClearVoice(true);
            }
            
            var clearBBtn = _root.Q<Button>("clear-b-btn");
            if (clearBBtn != null)
            {
                clearBBtn.clicked += () => ClearVoice(false);
            }
            
            // Parameter sliders
            if (_speedSlider != null)
            {
                _speedSlider.RegisterValueChangedCallback(e => {
                    if (_speedValue != null) _speedValue.text = $"{e.newValue:F1}x";
                });
            }
            
            if (_pitchSlider != null)
            {
                _pitchSlider.RegisterValueChangedCallback(e => {
                    if (_pitchValue != null) _pitchValue.text = $"{e.newValue:F1}x";
                });
            }
            
            if (_volumeSlider != null)
            {
                _volumeSlider.RegisterValueChangedCallback(e => {
                    if (_volumeValue != null) _volumeValue.text = $"{(int)(e.newValue * 100)}%";
                });
            }
            
            // Generate button
            if (_generateBtn != null)
            {
                _generateBtn.clicked += OnGenerateClicked;
            }

            // Playback controls
            if (_playPauseBtn != null)
            {
                _playPauseBtn.clicked += TogglePlayPause;
            }
            if (_stopPlaybackBtn != null)
            {
                _stopPlaybackBtn.clicked += StopPlayback;
            }
            if (_setLoopABtn != null)
            {
                _setLoopABtn.clicked += SetLoopPointA;
            }
            if (_setLoopBBtn != null)
            {
                _setLoopBBtn.clicked += SetLoopPointB;
            }
            if (_clearLoopBtn != null)
            {
                _clearLoopBtn.clicked += ClearLoopPoints;
            }
            if (_scrubSlider != null)
            {
                // FX Studio EQ 아래 슬라이더와 동일한 스타일 적용
                _scrubSlider.AddToClassList("playback-scrub");
                
                _scrubSlider.lowValue = 0f;
                _scrubSlider.highValue = 1f;
                _scrubSlider.SetValueWithoutNotify(0f);
                _scrubSlider.RegisterValueChangedCallback(evt =>
                {
                    if (_isScrubbing)
                    {
                        SetScrubPosition(evt.newValue);
                    }
                });
                _scrubSlider.RegisterCallback<PointerDownEvent>(_ => _isScrubbing = true);
                _scrubSlider.RegisterCallback<PointerUpEvent>(_ => _isScrubbing = false);
                _scrubSlider.RegisterCallback<PointerCaptureOutEvent>(_ => _isScrubbing = false);
            }

            // Legacy Play/Stop buttons removed (Spacebar control)
            /*
            if (_playBtn != null)
            {
                _playBtn.clicked += OnPlayClicked;
            }
            
            if (_stopBtn != null)
            {
                _stopBtn.clicked += OnStopClicked;
            }
            */
            
            // Voice picker buttons
            var voiceAPickBtn = _root.Q<Button>("voice-a-pick");
            if (voiceAPickBtn != null)
            {
                voiceAPickBtn.clicked += () => VoicePickerWindow.ShowWindow();
            }
            
            var voiceBPickBtn = _root.Q<Button>("voice-b-pick");
            if (voiceBPickBtn != null)
            {
                voiceBPickBtn.clicked += () => VoicePickerWindow.ShowWindow();
            }
            
            // Quick action buttons
            var saveBtnfirst = _root.Q<Button>("save-clip-btn");
            if (saveBtnfirst != null)
            {
                saveBtnfirst.clicked += OnSaveClipClicked;
            }

            // Export to Library button
            var exportLibBtn = _root.Q<Button>("export-library-btn");
            if (exportLibBtn != null)
            {
                exportLibBtn.clicked += OnExportToLibraryClicked;
            }
            
            _toDspBtn = _root.Q<Button>("to-dsp-btn");
            if (_toDspBtn != null)
            {
                _toDspBtn.clicked += () => {
                    if (!_abEnabled)
                    {
                        // Single mode - Send A
                        if (_generatedClipA != null) OnSendToDSP?.Invoke(_generatedClipA);
                        else UpdateStatus("Generate audio first", StatusType.Warning);
                    }
                    else
                    {
                        // A/B Mode - Show Menu
                        var menu = new GenericMenu();
                        if (_generatedClipA != null)
                            menu.AddItem(new GUIContent("Send Voice A to DSP"), false, () => OnSendToDSP?.Invoke(_generatedClipA));
                        else
                            menu.AddDisabledItem(new GUIContent("Send Voice A to DSP (Empty)"));
                            
                        if (_generatedClipB != null)
                            menu.AddItem(new GUIContent("Send Voice B to DSP"), false, () => OnSendToDSP?.Invoke(_generatedClipB));
                        else
                            menu.AddDisabledItem(new GUIContent("Send Voice B to DSP (Empty)"));
                            
                        menu.ShowAsContext();
                    }
                };
            }
            
            // A/B Comparison buttons
            var playABtn = _root.Q<Button>("play-a-btn");
            if (playABtn != null)
            {
                playABtn.clicked += () => {
                    if (_generatedClipA != null)
                    {
                        PlayAudio(_generatedClipA);
                        UpdateStatus("Playing Voice A", StatusType.Info);
                    }
                    else
                    {
                        UpdateStatus("Generate audio first", StatusType.Warning);
                    }
                };
            }
            
            var playBBtn = _root.Q<Button>("play-b-btn");
            if (playBBtn != null)
            {
                playBBtn.clicked += () => {
                    if (_generatedClipB != null)
                    {
                        PlayAudio(_generatedClipB);
                        UpdateStatus("Playing Voice B", StatusType.Info);
                    }
                    else
                    {
                        UpdateStatus("Generate Voice B first (enable A/B testing)", StatusType.Warning);
                    }
                };
            }
            
            var playABBtn = _root.Q<Button>("play-ab-btn");
            if (playABBtn != null)
            {
                playABBtn.clicked += () => _ = PlaySequenceAsync();
            }
            
            // Voice preview buttons
            var voiceAPreviewBtn = _root.Q<Button>("voice-a-preview");
            if (voiceAPreviewBtn != null)
            {
                voiceAPreviewBtn.clicked += () => {
                    if (_generatedClipA != null)
                        PlayAudio(_generatedClipA);
                };
            }
            
            var voiceBPreviewBtn = _root.Q<Button>("voice-b-preview");
            if (voiceBPreviewBtn != null)
            {
                voiceBPreviewBtn.clicked += () => {
                    if (_generatedClipB != null)
                        PlayAudio(_generatedClipB);
                };
            }
        }
        
        private async Task PlaySequenceAsync()
        {
            if (_generatedClipA == null || _generatedClipB == null)
            {
                UpdateStatus("Both Voice A and B must be generated", StatusType.Warning);
                return;
            }
            
            UpdateStatus("Playing A ??B sequence", StatusType.Info);
            
            // Play A
            PlayAudio(_generatedClipA);
            await AsyncUtils.WaitWhile(() => _previewSource != null && _previewSource.isPlaying);
            
            // Small gap
            await Task.Delay(500);
            
            // Play B
            PlayAudio(_generatedClipB);
            UpdateStatus("Playing Voice B", StatusType.Info);
        }
        
        private void OnEditorUpdate()
        {
            if (_root == null || _root.resolvedStyle.display == DisplayStyle.None) return;

            // Throttle to ~60fps (Smoother playback, still CPU safe)
            double now = EditorApplication.timeSinceStartup;
            if (now - _lastUpdateTime < 0.016f) return;
            _lastUpdateTime = now;

            UpdatePlaybackUI();
        }
        
        private void UpdateWaveformDisplay()
        {
            // Placeholder logic removed as element does not exist
        }
        
        #endregion
        
        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Space)
            {
                 // Consume event to prevent scrolling/typing spaces if focused elsewhere
                evt.StopPropagation();
                if (_previewSource != null && _previewSource.clip != null)
                    TogglePlayPause();
                else if (_generatedClipA != null)
                    PlayAudio(_generatedClipA);
                else
                    UpdateStatus("Nothing to play", StatusType.Warning);
            }
        }
        
        
        #region Event Handlers
        
        private void OnTextChanged(ChangeEvent<string> evt)
        {
            var text = evt.newValue ?? "";
            int charCount = text.Length;
            int wordCount = string.IsNullOrWhiteSpace(text) ? 0 : text.Split(new[] { ' ', '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries).Length;
            
            var charLabel = _root.Q<Label>("char-count");
            var wordLabel = _root.Q<Label>("word-count");
            
            if (charLabel != null) charLabel.text = $"{charCount} characters";
            if (wordLabel != null) wordLabel.text = $"{wordCount} words";
        }
        
        private void OnVoiceSearchChanged(ChangeEvent<string> evt)
        {
            // ApplyFilters reads search text directly from the field
            ApplyFilters();
        }
        
        private void UpdateABVisibility()
        {
            // Always show A/B comparison elements
            if (_voiceBSection != null)
            {
                _voiceBSection.style.display = DisplayStyle.Flex;
            }
            
            if (_abComparison != null)
            {
                _abComparison.style.display = DisplayStyle.Flex;
                
                // Ensure ab-visible class is present just in case
                _abComparison.AddToClassList("ab-visible");
                _abComparison.RemoveFromClassList("ab-hidden");
            }
        }
        
        private void OnVoiceSortChanged(ChangeEvent<string> evt)
        {
            bool ascending = evt.newValue == "A-Z";
            SortVoices(ascending);
        }
        
        private void FilterVoicePickers(string filter)
        {
            // Filter Voice A picker
            if (_voiceAPicker != null)
            {
                foreach (var child in _voiceAPicker.Children())
                {
                    if (child is Button btn)
                    {
                        bool matches = string.IsNullOrEmpty(filter) || 
                                       btn.text.ToLower().Contains(filter);
                        btn.style.display = matches ? DisplayStyle.Flex : DisplayStyle.None;
                    }
                }
            }
            
            // Filter Voice B picker
            if (_voiceBPicker != null)
            {
                foreach (var child in _voiceBPicker.Children())
                {
                    if (child is Button btn)
                    {
                        bool matches = string.IsNullOrEmpty(filter) || 
                                       btn.text.ToLower().Contains(filter);
                        btn.style.display = matches ? DisplayStyle.Flex : DisplayStyle.None;
                    }
                }
            }
        }
        
        private void SortVoices(bool ascending)
        {
            // Sort the voices list
            if (ascending)
                _voices.Sort((a, b) => string.Compare(a.SpeakerName, b.SpeakerName, System.StringComparison.Ordinal));
            else
                _voices.Sort((a, b) => string.Compare(b.SpeakerName, a.SpeakerName, System.StringComparison.Ordinal));
            
            // Repopulate pickers
            PopulateVoicePicker(_voiceAPicker, true);
            PopulateVoicePicker(_voiceBPicker, false);
        }
        
        // Filter state
        private Gender _currentGenderFilter = Gender.Unknown;
        private string _currentAgeFilter = "All";
        private string _currentEmotionFilter = "All";
        
        private void SetGenderFilter(Gender gender, Button tabAll, Button tabMale, Button tabFemale)
        {
            _currentGenderFilter = gender;
            
            // Update tab visuals
            tabAll?.RemoveFromClassList("filter-btn--active");
            tabMale?.RemoveFromClassList("filter-btn--active");
            tabFemale?.RemoveFromClassList("filter-btn--active");
            
            switch (gender)
            {
                case Gender.Unknown:
                    tabAll?.AddToClassList("filter-btn--active");
                    break;
                case Gender.Male:
                    tabMale?.AddToClassList("filter-btn--active");
                    break;
                case Gender.Female:
                    tabFemale?.AddToClassList("filter-btn--active");
                    break;
            }
            
            ApplyFilters();
        }
        
        private void OnAgeFilterChanged(ChangeEvent<string> evt)
        {
            _currentAgeFilter = evt.newValue ?? "All";
            ApplyFilters();
        }
        
        private void OnEmotionFilterChanged(ChangeEvent<string> evt)
        {
            _currentEmotionFilter = evt.newValue ?? "All";
            ApplyFilters();
        }
        
        private void OnGenderDropdownChanged(ChangeEvent<string> evt)
        {
            string val = evt.newValue ?? "All";
            _currentGenderFilter = val switch
            {
                "M" => Gender.Male,
                "F" => Gender.Female,
                _ => Gender.Unknown // "All" = Unknown means no filter
            };
            ApplyFilters();
        }
        
        private void OnLanguageChanged(ChangeEvent<string> evt)
        {
            string val = evt.newValue ?? "KR";
            
            if (val != "KR")
            {
                UpdateStatus("Only Korean (KR) is supported in this version.", StatusType.Warning);
                _languageDropdown.SetValueWithoutNotify("KR");
                return;
            }
            
            _selectedLanguage = Language.Korean;
            UpdateStatus($"Language: {val}", StatusType.Info);
        }

        
        private void ApplyFilters()
        {
            if (_voiceList == null) return;
            
            string searchText = _voiceSearchField?.value?.ToLower() ?? "";
            
            // Filter the data list directly
            _filteredVoices = new List<VarcoVoice>();
            
            foreach (var voice in _voices)
            {
                bool matches = true;
                
                // Search filter
                if (!string.IsNullOrEmpty(searchText))
                {
                     matches = matches && voice.SpeakerName.ToLower().Contains(searchText);
                }
                
                // Gender filter
                if (_currentGenderFilter != Gender.Unknown)
                {
                    matches = matches && voice.Gender == _currentGenderFilter;
                }
                
                // Age filter
                if (_currentAgeFilter != "All")
                {
                     var targetAge = _currentAgeFilter switch
                    {
                        "Child" => AgeGroup.Child,
                        "Young" => AgeGroup.Young,
                        "Middle" => AgeGroup.Middle,
                        "Senior" => AgeGroup.Senior,
                        _ => AgeGroup.Unknown
                    };
                    matches = matches && voice.AgeGroup == targetAge;
                }

                // Emotion Filter
                if (_currentEmotionFilter != "All")
                {
                    var emotion = voice.GetEmotion();
                    matches = matches && emotion.ToString() == _currentEmotionFilter;
                }
                
                if (matches)
                {
                    _filteredVoices.Add(voice);
                }
            }
            
            // Update ListView
            _voiceList.itemsSource = _filteredVoices;
            _voiceList.Rebuild(); // or RefreshItems() if only data changed
        }

        
        private void OnABToggleClicked()
        {
            _abEnabled = !_abEnabled;
            
            // Toggle button visual state
            if (_abToggleBtn != null)
            {
                if (_abEnabled)
                    _abToggleBtn.AddToClassList("switch--active");
                else
                    _abToggleBtn.RemoveFromClassList("switch--active");
            }
            
            // Show/hide Voice B section
            if (_voiceBSection != null)
            {
                if (_abEnabled)
                {
                    _voiceBSection.RemoveFromClassList("voice-slot--hidden");
                    _voiceBSection.AddToClassList("voice-slot--visible");
                }
                else
                {
                    _voiceBSection.RemoveFromClassList("voice-slot--visible");
                    _voiceBSection.AddToClassList("voice-slot--hidden");
                }
            }
            
            // Show/hide A/B comparison panel
            if (_abComparison != null)
            {
                if (_abEnabled)
                {
                    _abComparison.RemoveFromClassList("ab-hidden");
                    _abComparison.AddToClassList("ab-visible");
                }
                else
                {
                    _abComparison.RemoveFromClassList("ab-visible");
                    _abComparison.AddToClassList("ab-hidden");
                }
            }
            
            UpdateStatus(_abEnabled ? "A/B Testing Enabled" : "A/B Testing Disabled", StatusType.Info);
        }
        
        private void OnGenerateClicked()
        {
            GenerateTTSAsync().Forget();
        }
        
        private void OnPlayClicked()
        {
            PlayAudio(_generatedClipA);
        }
        
        private void OnStopClicked()
        {
            StopAudio();
        }
        
        private void OnSaveClipClicked()
        {
            if (_generatedClipA == null)
            {
                UpdateStatus("No audio to save", StatusType.Warning);
                return;
            }
            
            // Ask user for save location
            var path = EditorUtility.SaveFilePanelInProject(
                "Save Audio Clip", 
                "tts_output", 
                "wav", 
                "Choose where to save the generated audio");
            
            if (string.IsNullOrEmpty(path))
                return;
            
            try
            {
                // Save Voice A
                SaveAudioClipToWav(_generatedClipA, path);
                UpdateStatus($"Saved A: {System.IO.Path.GetFileName(path)}", StatusType.Success);
                
                // Save Voice B if A/B testing is enabled
                if (_abEnabled && _generatedClipB != null)
                {
                    string dirName = System.IO.Path.GetDirectoryName(path);
                    string fileName = System.IO.Path.GetFileNameWithoutExtension(path);
                    string pathB = System.IO.Path.Combine(dirName, fileName + "_B.wav");
                    
                    SaveAudioClipToWav(_generatedClipB, pathB);
                    UpdateStatus($"Saved A & B clips", StatusType.Success);
                }
                
                AssetDatabase.Refresh();
            }
            catch (System.Exception ex)
            {
                UpdateStatus($"Save failed: {ex.Message}", StatusType.Error);
            }
        }

        private void OnExportToLibraryClicked()
        {
            var clip = CurrentClip;
            if (clip == null)
            {
                UpdateStatus("No audio to export", StatusType.Warning);
                return;
            }

            // Show simple input dialog for name
            ExportNamePopup.Show(defaultName: "dialogue_01", onConfirm: (name) =>
            {
                if (string.IsNullOrEmpty(name))
                {
                    name = "dialogue_" + System.DateTime.Now.ToString("HHmmss");
                }

                try
                {
                    ExportPanelController.ExportClipToLibrary(clip, name);
                    UpdateStatus($"Exported: {name}.wav", StatusType.Success);
                }
                catch (System.Exception ex)
                {
                    UpdateStatus($"Export failed: {ex.Message}", StatusType.Error);
                }
            });
        }
        
        private void SaveAudioClipToWav(AudioClip clip, string path)
        {
            if (clip == null) return;

            // Convert AudioClip to WAV bytes
            var samples = new float[clip.samples * clip.channels];
            clip.GetData(samples, 0);
            
            // Create WAV file
            using (var stream = new System.IO.FileStream(path, System.IO.FileMode.Create))
            using (var writer = new System.IO.BinaryWriter(stream))
            {
                int sampleCount = samples.Length;
                int channels = clip.channels;
                int sampleRate = clip.frequency;
                
                // WAV Header (using byte[] for cross-platform safety)
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + sampleCount * 2);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
                writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
                writer.Write(16);
                writer.Write((short)1); // PCM
                writer.Write((short)channels);
                writer.Write(sampleRate);
                writer.Write(sampleRate * channels * 2);
                writer.Write((short)(channels * 2));
                writer.Write((short)16); // bits per sample
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                writer.Write(sampleCount * 2);
                
                // Audio data with clamping to prevent distortion
                foreach (float sample in samples)
                {
                    float clamped = sample < -1f ? -1f : (sample > 1f ? 1f : sample);
                    short intSample = (short)(clamped * 32767f);
                    writer.Write(intSample);
                }
            }
        }
        
        #endregion
        
        #region TTS Generation
        
        private async UniTaskVoid GenerateTTSAsync()
        {
            if (_isGenerating) return;
            
            var text = _textInput?.value ?? "";
            if (string.IsNullOrWhiteSpace(text))
            {
                UpdateStatus("Please enter text to generate", StatusType.Warning);
                return;
            }
            
            var voiceName = _voiceADropdown?.value;
            if (string.IsNullOrEmpty(voiceName))
            {
                UpdateStatus("Please select a voice", StatusType.Warning);
                return;
            }
            
            _isGenerating = true;
            UpdateStatus("Generating...", StatusType.Info);
            SetProgress(0);
            
            try
            {
                var config = VarcoConfig.Instance;
                if (!config.IsValid())
                {
                    UpdateStatus("API Key not configured", StatusType.Error);
                    return;
                }
                
                var client = new VarcoApiClient(config);
                
                float speed = _speedSlider?.value ?? 1f;
                float pitch = _pitchSlider?.value ?? 1f;
                
                SetProgress(0.3f);
                
                _generatedClipA = await client.SynthesizeAsync(
                    text, 
                    voiceName, 
                    language: _selectedLanguage,
                    speed: speed, 
                    pitch: pitch);
                
                SetProgress(0.8f);
                
                // Generate B if A/B testing enabled
                if (_abEnabled)
                {
                    var voiceBName = _voiceBDropdown?.value;
                    if (!string.IsNullOrEmpty(voiceBName))
                    {
                        _generatedClipB = await client.SynthesizeAsync(
                            text, 
                            voiceBName, 
                            language: _selectedLanguage,
                            speed: speed, 
                            pitch: pitch);
                    }
                }

                
                SetProgress(1f);
                UpdateStatus("Generation complete!", StatusType.Success);
                
                // Auto-play
                PlayAudio(_generatedClipA);
                
                // Highlight DSP Button
                if (_toDspBtn != null)
                {
                    _toDspBtn.AddToClassList("glow-anim"); // Add visual cue
                    // Optional: Remove glow after some time? For now, let it glow to indicate "Next Step"
                }
            }
            catch (VarcoException ex)
            {
                UpdateStatus($"Error: {ex.Message}", StatusType.Error);
            }
            catch (System.Exception ex)
            {
                // Check for 400 Bad Request - likely language not supported
                if (ex.Message.Contains("400") || ex.Message.Contains("Bad Request") || ex.Message.Contains("처리할 수 없는"))
                {
                    UpdateStatus($"Language not supported for this voice!", StatusType.Error);
                }
                else
                {
                    UpdateStatus($"Error: {ex.Message}", StatusType.Error);
                }
            }
            finally
            {
                _isGenerating = false;
            }
        }
        
        #endregion
        
        #region Audio Playback
        // Logic moved to TTSPanelController.Playback.cs
        #endregion
        
        #region Status Updates
        
        private enum StatusType { Success, Warning, Error, Info }
        
        private void UpdateStatus(string message, StatusType type)
        {
            if (_statusText != null)
            {
                _statusText.text = message;
            }
            
            if (_statusIndicator != null)
            {
                _statusIndicator.RemoveFromClassList("status-dot--error");
                _statusIndicator.RemoveFromClassList("status-dot--warning");
                
                switch (type)
                {
                    case StatusType.Error:
                        _statusIndicator.AddToClassList("status-dot--error");
                        break;
                    case StatusType.Warning:
                        _statusIndicator.AddToClassList("status-dot--warning");
                        break;
                }
            }
        }
        
        private void SetProgress(float value)
        {
            if (_generationProgress != null)
            {
                _generationProgress.value = value;
            }
        }
        
        #endregion
        
        #region Cleanup
        
        public void Cleanup()
        {
            EditorApplication.update -= OnEditorUpdate;
            
            if (_previewSource != null)
            {
                Object.DestroyImmediate(_previewSource.gameObject);
            }
        }
        
        // SetVoice moved to TTSPanelController.Voices.cs
        
        #endregion
    }
}



