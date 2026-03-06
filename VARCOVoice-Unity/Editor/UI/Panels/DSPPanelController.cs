using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using VARCOVoice.DSP;
using VARCOVoice.Editor.Services;
using Object = UnityEngine.Object;

namespace VARCOVoice.Editor
{
    /// <summary>
    /// [Partial Core] DSP Panel Controller
    /// This file contains the shared state, nested classes, and core lifecycle methods.
    /// </summary>
    public partial class DSPPanelController
    {
        private VisualElement _root;
        
        // Legacy IMGUI container (kept for partial class compatibility)
        // This is no longer actively used - EQ is now UI Toolkit based
        #pragma warning disable CS0414
        private VisualElement _visualizerContainer;
        #pragma warning restore CS0414
        
        // Target
        private DSPChain _target;
        private AudioSource _audioSource;
        
        public DSPChain Target => _target;
        public AudioClip CurrentClip => (_audioSource != null && _audioSource) ? _audioSource.clip : null;
        
        // Visualizer State
        private double _lastUpdateTime;
        private float _analysisDeltaTime;
        private const float UPDATE_INTERVAL = 0.016f;
        private int _selectedEffectIndex = -1;
        private Vector2 _scrollPos;
        
        // EQ Node System
        private const float MIN_FREQ = 20f;
        private const float MAX_FREQ = 20000f;
        private const float MIN_DB = -30f;
        private const float MAX_DB = 30f;
        private const int MAX_EQ_NODES = 16;
        
        public enum EQFilterType { Bell, LowCut, HighCut }
        
        // This is the class the compiler is complaining about!
        public class EQBandNode
        {
            public float Frequency = 1000f;
            public float Gain = 0f;
            public float Q = 1.0f;
            public EQFilterType Type = EQFilterType.Bell;
            public bool IsSelected = false;
            public bool IsDragging = false;
            public Vector2 ScreenPos;
        }
        
        private List<EQBandNode> _eqNodes = new List<EQBandNode>();
        private EQBandNode _selectedEQNode = null;
        
        // Node Graph System
        private Dictionary<IDSPEffect, Vector2> _effectPositions = new Dictionary<IDSPEffect, Vector2>();
        private Dictionary<IDSPEffect, Vector2> _effectScrollPositions = new Dictionary<IDSPEffect, Vector2>(); // For scrollable popups
        private Vector2 _inputNodePos = Vector2.zero;
        private Vector2 _outputNodePos = Vector2.zero;
        #pragma warning disable CS0414
        private bool _positionsInitialized = false;
        #pragma warning restore CS0414
        
        public class NodeConnection
        {
            public IDSPEffect FromEffect;
            public IDSPEffect ToEffect;
        }
        private List<NodeConnection> _connections = new List<NodeConnection>();
        
        // Navigation Events
        public event System.Action<int> OnRequestTabChange;
        public event System.Action OnQuickExport;
        
        // Interaction State
        private IDSPEffect _draggingEffect = null;
        private IDSPEffect _selectedEffect = null;
        private bool _showInputPopup = false;
        private bool _showOutputPopup = false;
        private string _inputSpeakerName = "Unknown";
        private bool _showNodeAddMenu = false;
        private Vector2 _nodeAddMenuPos;
        private Rect _lastCanvasRect;
        private Vector2 _nextSpawnPos;
        private Vector2 _dragOffset;
        
        // Vibe Mixer / Simplified State
        private bool _isSimplifiedView = true;
        private Vector2 _vibeGalleryScroll;
        private Vector2 _effectStackScroll;
        #pragma warning disable CS0414
        private int _dropInsertIndex = -1;
        #pragma warning restore CS0414
        
        // Wire creation state
        private bool _isCreatingWire = false;
        private IDSPEffect _wireStartEffect = null;
        private bool _wireFromOutput = true;
        private Vector2 _wireDragEndPos;
        
        private GUIStyle _popupLabelStyle;
        private GUIStyle _popupValueStyle;
        
        // DSP Integration
        private ParametricEQ16 _parametricEQ;
        
        // UI Toolkit Controllers
        private EffectStackPanelController _effectStackController;
        private VisualElement _effectStackContainer;
        private EQVisualizerController _eqVisualizerController;
        private VisualElement _eqVisualizerContainer;
        private VisualElement _playbackPanel;
        private Button _playPauseBtn;
        private Button _stopPlaybackBtn;
        private Slider _scrubSlider;
        private Label _playbackTimeLabel;
        private bool _isPaused;
        private bool _isScrubbing;
        
        public void Initialize(VisualElement root)
        {
            _root = root;
            _root.Clear();
            _root.style.paddingLeft = 0;
            _root.style.paddingRight = 0;
            _root.style.paddingTop = 0;
            _root.style.paddingBottom = 0;
            _root.style.borderTopLeftRadius = 0;
            _root.style.borderTopRightRadius = 0;
            _root.style.borderBottomLeftRadius = 0;
            _root.style.borderBottomRightRadius = 0;
            _root.style.backgroundColor = VarcoEditorStyles.BackgroundDark;    

            FindTarget();

            var playbackStyle = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Packages/com.varco.voice/Editor/UI/Components/PlaybackPanel.uss");
            if (playbackStyle != null && !_root.styleSheets.Contains(playbackStyle))
                _root.styleSheets.Add(playbackStyle);

            // === Toolbar ===
            // === Standard Header ===
            var toolbar = new VisualElement();
            toolbar.AddToClassList("panel-header");
            
            // Title
            var titleLabel = new Label("FX STUDIO");
            titleLabel.AddToClassList("panel-header__title");
            toolbar.Add(titleLabel);
            
            // Controls (Spacebar Tip & Export)
            var controls = new VisualElement();
            controls.AddToClassList("panel-header__controls");
            toolbar.Add(controls);
            


            // Export to Library button
            var exportBtn = new Button(OnExportToLibraryClicked) { text = "Export" };
            // Apply "Add FX" Ghost Blue style directly
            exportBtn.style.height = 24f;
            exportBtn.style.borderTopLeftRadius = 0;
            exportBtn.style.borderTopRightRadius = 0;
            exportBtn.style.borderBottomLeftRadius = 0;
            exportBtn.style.borderBottomRightRadius = 0;
            exportBtn.style.backgroundColor = new Color(0f, 0.43f, 1f, 0.15f);
            exportBtn.style.borderRightWidth = 1;
            exportBtn.style.borderBottomWidth = 1;
            exportBtn.style.borderLeftWidth = 1;
            exportBtn.style.borderTopWidth = 1;
            exportBtn.style.borderLeftColor = new Color(0f, 0.43f, 1f, 0.3f);
            exportBtn.style.borderRightColor = new Color(0f, 0.43f, 1f, 0.3f);
            exportBtn.style.borderTopColor = new Color(0f, 0.43f, 1f, 0.3f);
            exportBtn.style.borderBottomColor = new Color(0f, 0.43f, 1f, 0.3f);
            exportBtn.style.color = Color.white; // Unified White
            exportBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            controls.Add(exportBtn);
            
            _root.Add(toolbar);

            // Invisible IMGUI container to handle global spacebar events even when not focused
            var eventHandler = new IMGUIContainer(() => {
                var evt = Event.current;
                if (evt != null && evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Space)
                {
                    HandleSpacebar();
                    evt.Use();
                }
            });
            eventHandler.style.height = 0;
            _root.Add(eventHandler);
            
            // === EQ VISUALIZER (UI Toolkit) ===
            InitializeEQVisualizer();

            // === PLAYBACK PANEL ===
            InitializePlaybackPanel();

            // === EFFECT STACK (UI Toolkit) ===
            InitializeEffectStackPanel();

            // Keyboard shortcuts (Undo/Redo)
            _root.UnregisterCallback<KeyDownEvent>(OnKeyDown);
            _root.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            _root.focusable = true;
            _root.tabIndex = 0;
        }
        
        private void InitializeEQVisualizer()
        {
            const string PKG = "Packages/com.varco.voice/Editor/UI/Components/";
            
            var eqPanelAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(PKG + "EQVisualizerPanel.uxml");
            var eqPanelStyle = AssetDatabase.LoadAssetAtPath<StyleSheet>(PKG + "EQVisualizerPanel.uss");
            
            if (eqPanelAsset != null)
            {
                _eqVisualizerContainer = eqPanelAsset.Instantiate();
                if (eqPanelStyle != null)
                    _eqVisualizerContainer.styleSheets.Add(eqPanelStyle);
                
                _eqVisualizerContainer.style.flexGrow = 0;
                _eqVisualizerContainer.style.flexShrink = 0;
                _root.Add(_eqVisualizerContainer);
                
                // Initialize controller
                _eqVisualizerController = new EQVisualizerController();
                _eqVisualizerController.Initialize(_eqVisualizerContainer, _target?.MasterEQ);
                _eqVisualizerController.OnEQChanged += () =>
                {
                    // Note: Don't call SyncRuntimeChain() here as it would overwrite
                    // effects added via the UI Toolkit Effect Stack.
                    // EQ changes are applied directly to ParametricEQ16.
                    EditorUtility.SetDirty(_target);
                };
            }
            else
            {
                // Fallback: show placeholder
                var fallback = new Label("EQ Visualizer not found. Check asset paths.");
                fallback.style.color = Color.yellow;
                fallback.style.height = 320;
                fallback.style.unityTextAlign = TextAnchor.MiddleCenter;
                _root.Add(fallback);
            }
        }

        private void InitializePlaybackPanel()
        {
            _playbackPanel = new VisualElement();
            _playbackPanel.AddToClassList("playback-panel");

            _playPauseBtn = new Button(TogglePlayPause) { text = "▶" };
            _playPauseBtn.AddToClassList("playback-btn");
            _playPauseBtn.AddToClassList("playback-btn--primary");

            _stopPlaybackBtn = new Button(StopPlayback) { text = "■" };
            _stopPlaybackBtn.AddToClassList("playback-btn");

            _scrubSlider = new Slider(0f, 1f);
            _scrubSlider.AddToClassList("playback-scrub");
            _scrubSlider.RegisterValueChangedCallback(evt =>
            {
                if (_isScrubbing) SetScrubPosition(evt.newValue);
            });
            _scrubSlider.RegisterCallback<PointerDownEvent>(_ => _isScrubbing = true);
            _scrubSlider.RegisterCallback<PointerUpEvent>(_ => _isScrubbing = false);
            _scrubSlider.RegisterCallback<PointerCaptureOutEvent>(_ => _isScrubbing = false);

            _playbackTimeLabel = new Label("00:00.0 / 00:00.0");
            _playbackTimeLabel.AddToClassList("playback-time-label");

            _playbackPanel.Add(_playPauseBtn);
            _playbackPanel.Add(_stopPlaybackBtn);
            _playbackPanel.Add(_scrubSlider);
            _playbackPanel.Add(_playbackTimeLabel);

            _root.Add(_playbackPanel);
        }

        private void InitializeEffectStackPanel()
        {
            // Load the EffectStackPanel UXML template (Package path)
            var stackPanelAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/com.varco.voice/Editor/UI/Components/EffectStackPanel.uxml");
            var stackPanelStyle = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Packages/com.varco.voice/Editor/UI/Components/EffectStackPanel.uss");
            
            if (stackPanelAsset != null)
            {
                _effectStackContainer = stackPanelAsset.Instantiate();
                if (stackPanelStyle != null)
                    _effectStackContainer.styleSheets.Add(stackPanelStyle);
                
                // Fill remaining space below EQ zone
                _effectStackContainer.style.flexGrow = 1;
                _effectStackContainer.style.flexShrink = 1;
                _effectStackContainer.style.minHeight = 250;
                _root.Add(_effectStackContainer);
                
                // Initialize controller
                _effectStackController = new EffectStackPanelController();
                _effectStackController.Initialize(_effectStackContainer, _target);
                _effectStackController.OnChainModified += () =>
                {
                    // Note: EffectStackPanelController manages the chain directly.
                    // Don't call SyncRuntimeChain() as it would overwrite via _connections.
                    _eqVisualizerController?.ForceRefresh();
                };
            }
            else
            {
                // Fallback: show info box
                var fallbackLabel = new Label("Effect Stack Panel not found. Please check asset paths.");
                fallbackLabel.style.color = Color.yellow;
                fallbackLabel.style.paddingLeft = 10;
                fallbackLabel.style.paddingTop = 10;
                _root.Add(fallbackLabel);
            }
        }
        
        public void Cleanup()
        {
            if (_root != null)
            {
                _root.UnregisterCallback<KeyDownEvent>(OnKeyDown);
            }
        }

        public void UpdateLoop()
        {
            OnEditorUpdate();
        }

        private void FindTarget()
        {
            // Use Unity's implicit bool operator to detect destroyed objects
            if (_target == null || !_target)
            {
                _target = null; // Clear stale reference
                _audioSource = null;
                
                _target = Object.FindFirstObjectByType<DSPChain>();
                if (_target != null)
                {
                    _audioSource = _target.GetComponent<AudioSource>();
                }
                else
                {
                    var go = new GameObject("VARCO Engine");
                    _target = go.AddComponent<DSPChain>();
                    _audioSource = go.GetComponent<AudioSource>();
                }
            }
        }
        
        /// <summary>
        /// Invalidate target references (called after Play mode exit)
        /// </summary>
        public void InvalidateTarget()
        {
            _target = null;
            _audioSource = null;
            FindTarget();
            
            // Reinitialize sub-controllers with new target
            _effectStackController?.SetTarget(_target);
            _eqVisualizerController?.SetEQ(_target?.MasterEQ);
        }
        
        public void LoadAudio(AudioClip clip)
        {
            FindTarget();
            if (_audioSource != null && clip != null)
            {
                _audioSource.clip = clip;
                _audioSource.Play();
                _isPaused = false;

                if (clip.name.StartsWith("vc_"))
                {
                    var parts = clip.name.Split('_');
                    if (parts.Length >= 2) _inputSpeakerName = parts[1];
                }
                else _inputSpeakerName = "External Clip";
            }
            else
            {
                if (_target == null) Debug.LogWarning("[DSPPanel] No DSP Target found.");
            }
        }

        private void SetTarget(DSPChain newTarget)
        {
            _target = newTarget;
            _audioSource = _target?.GetComponent<AudioSource>();
            _selectedEffectIndex = -1;
        }

        private void OnEditorUpdate()
        {
            // Optimization: Skip all calculations if panel is not visible (e.g. on TTS tab)
            if (_root == null || _root.resolvedStyle.display == DisplayStyle.None) return;

            if (_target == null) FindTarget();

            double now = Application.isPlaying ? Time.realtimeSinceStartup : EditorApplication.timeSinceStartup;
            if (_lastUpdateTime > 0 && now - _lastUpdateTime < UPDATE_INTERVAL) return;
            _analysisDeltaTime = _lastUpdateTime > 0 ? (float)(now - _lastUpdateTime) : 0f;
            _lastUpdateTime = now;
            
            if (_target != null)
            {
                bool isPlaying = _audioSource != null && _audioSource.isPlaying;
                
                if (isPlaying)
                {
                    UpdateAudioAnalysis(_analysisDeltaTime);

                    // Update UI Toolkit EQ Visualizer with overlay (pre-EQ + post-EQ)
                    _eqVisualizerController?.UpdateSpectrum(
                        AudioAnalysisService.SpectrumData,
                        AudioAnalysisService.SmoothSpectrum,
                        AudioAnalysisService.SmoothPreEQSpectrum);
                    _eqVisualizerController?.UpdateMeters(
                        AudioAnalysisService.SmoothLeftLevel,
                        AudioAnalysisService.SmoothRightLevel,
                        AudioAnalysisService.PeakLevel);
                    _eqVisualizerController?.UpdateStatus(_target.EffectCount);

                    // Sync frequently when playing to catch automated changes
                    try { _eqVisualizerController?.OnUpdate(); } catch {}
                }
                else
                {
                    // Idle State Optimization
                    bool wasActive = HasVisualizerActivity();
                    DecayLevels();
                    bool isActive = HasVisualizerActivity();

                    if (wasActive || isActive)
                    {
                        // Repaint while decaying
                        _eqVisualizerController?.UpdateMeters(
                            AudioAnalysisService.SmoothLeftLevel,
                            AudioAnalysisService.SmoothRightLevel,
                            AudioAnalysisService.PeakLevel);
                        _eqVisualizerController?.UpdateSpectrum(
                            AudioAnalysisService.SpectrumData,
                            AudioAnalysisService.SmoothSpectrum,
                            AudioAnalysisService.SmoothPreEQSpectrum);
                    }
                    else
                    {
                        // Completely Idle - throttle sync to 1Hz
                        if (Time.frameCount % 60 == 0)
                        {
                             try { _eqVisualizerController?.OnUpdate(); } catch {}
                        }
                    }
                }
                
                // Original unconditional sync removed
            }

            UpdatePlaybackPanel();
        }

        private void UpdatePlaybackPanel()
        {
            if (_audioSource == null || _audioSource.clip == null)
            {
                if (_playbackTimeLabel != null) _playbackTimeLabel.text = "00:00.0 / 00:00.0";
                if (_scrubSlider != null && !_isScrubbing)
                    _scrubSlider.SetValueWithoutNotify(0f);
                UpdatePlayButtonLabel();
                return;
            }

            float currentTime = _audioSource.time;
            float totalTime = _audioSource.clip.length;
            if (totalTime <= 0f) totalTime = 1f;

            if (_playbackTimeLabel != null)
                _playbackTimeLabel.text = $"{FormatTime(currentTime)} / {FormatTime(totalTime)}";

            if (_scrubSlider != null && !_isScrubbing)
            {
                float normalized = totalTime > 0 ? currentTime / totalTime : 0f;
                _scrubSlider.SetValueWithoutNotify(Mathf.Clamp01(normalized));
            }

            HandleLooping(currentTime, totalTime);
            UpdatePlayButtonLabel();
        }

        private void TogglePlayPause()
        {
            if (_audioSource == null || _audioSource.clip == null) return;

            if (_audioSource.isPlaying)
            {
                _audioSource.Pause();
                _isPaused = true;
            }
            else
            {
                if (_audioSource.time >= _audioSource.clip.length)
                    _audioSource.time = 0f;

                if (_isPaused)
                    _audioSource.UnPause();
                else
                    _audioSource.Play();

                _isPaused = false;
            }

            UpdatePlayButtonLabel();
        }

        private void StopPlayback()
        {
            if (_audioSource == null) return;
            _audioSource.Stop();
            _audioSource.time = 0f;
            _isPaused = false;
            if (_scrubSlider != null) _scrubSlider.SetValueWithoutNotify(0f);
            UpdatePlayButtonLabel();
        }

        private void SetScrubPosition(float normalized)
        {
            if (_audioSource == null || _audioSource.clip == null) return;
            float total = _audioSource.clip.length;
            float target = Mathf.Clamp01(normalized) * total;
            _audioSource.time = Mathf.Clamp(target, 0f, total);
            UpdatePlaybackPanel();
        }

        private void HandleLooping(float currentTime, float totalTime)
        {
            if (_audioSource == null || !_audioSource.isPlaying) return;
            if (totalTime <= 0f) return;

            if (currentTime >= totalTime)
            {
                _audioSource.time = 0f;
            }
        }

        private void UpdatePlayButtonLabel()
        {
            if (_playPauseBtn == null) return;
            _playPauseBtn.text = _audioSource != null && _audioSource.isPlaying ? "||" : "▶";
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Space)
            {
                HandleSpacebar();
                evt.StopPropagation();
                return;
            }

            if (!evt.ctrlKey || evt.keyCode != KeyCode.Z) return;

            if (evt.shiftKey)
                _effectStackController?.Redo();
            else
                _effectStackController?.Undo();

            evt.StopPropagation();
        }

        private void HandleSpacebar()
        {
            if (_audioSource == null || _audioSource.clip == null) return;

            if (_audioSource.isPlaying)
            {
                StopPlayback();
                return;
            }

            _audioSource.time = 0f;
            _audioSource.Play();
            _isPaused = false;
            UpdatePlayButtonLabel();
        }

        private static string FormatTime(float time)
        {
            System.TimeSpan t = System.TimeSpan.FromSeconds(time);
            return string.Format("{0:D2}:{1:D2}.{2:D1}", t.Minutes, t.Seconds, t.Milliseconds / 100);
        }

        private void OnExportToLibraryClicked()
        {
            if (_audioSource == null || _audioSource.clip == null)
            {
                Debug.LogWarning("No audio clip to export. Generate or send audio first.");
                return;
            }

            var clip = _audioSource.clip;
            string defaultName = string.IsNullOrEmpty(clip.name)
                ? "dsp_output"
                : $"{clip.name}_fx";

            string path = EditorUtility.SaveFilePanel(
                "Export WAV",
                Application.dataPath,
                defaultName,
                "wav");

            if (string.IsNullOrEmpty(path)) return;

            try
            {
                ExportPanelController.ExportClipToPath(clip, path, _target);
                Debug.Log($"Exported WAV: {System.IO.Path.GetFileName(path)}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Export failed: {ex.Message}");
            }
        }

    }
}
