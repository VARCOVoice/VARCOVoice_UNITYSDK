using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using VARCOVoice.DSP;

namespace VARCOVoice.Editor
{
    /// <summary>
    /// Enhanced custom inspector for VarcoTTS component - DAW-style UI
    /// </summary>
    [CustomEditor(typeof(VarcoTTS))]
    public class VarcoTTSInspector : UnityEditor.Editor
    {
        // Test/Preview
        private string _testText = "?àÎÖï?òÏÑ∏?? Î∞îÎ•¥ÏΩ?Î≥¥Ïù¥???åÏä§?∏ÏûÖ?àÎã§.";
        private AudioClip _previewClip;
        private AudioSource _previewSource;
        private bool _isGenerating = false;
        
        // DSP Effects (Pro)
        private bool _showDSPSection = true;
        private DSPChain _previewChain;
        private int _selectedEQBand = 7; // Default to Mid band

        private static readonly EQFilterType[] EqFilterTypeOptions =
        {
            EQFilterType.Peak,
            EQFilterType.HighPass,
            EQFilterType.LowPass
        };

        private static readonly string[] EqFilterTypeLabels = { "Bell", "LCut", "HCut" };

        public override bool RequiresConstantRepaint() => true;
        
        // Export
        private bool _showExportSection = true;
        private string _exportPath = "Assets/Audio/TTS";
        private string _exportFileName = "tts_output";
        
        // Foldouts
        private bool _showVoiceSection = true;
        private bool _showLipSyncSection = true;
        
        // LipSync
        private bool _enableLipSync = false;
        private float _lipSyncSensitivity = 0.5f;
        private LipSync.LipSyncData _lipSyncData;
        
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            var tts = (VarcoTTS)target;
            
            // === Header ===
            DrawHeader();
            
            EditorGUILayout.Space(8);
            
            // === Voice Settings Section ===
            DrawVoiceSection();
            
            EditorGUILayout.Space(8);
            
            // === DSP Effects Section ===
            DrawDSPSection();
            
            EditorGUILayout.Space(8);
            
            // === Preview & Test Section ===
            DrawPreviewSection(tts);
            
            EditorGUILayout.Space(8);
            
            // === LipSync Section ===
            DrawLipSyncSection(tts);
            
            EditorGUILayout.Space(8);
            
            // === Export Section ===
            DrawExportSection(tts);
            
            serializedObject.ApplyModifiedProperties();
        }
        
        new private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal();
            
            var headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleLeft
            };
            headerStyle.normal.textColor = VarcoEditorStyles.Mint;
            
            GUILayout.Label("?é§ VARCO TTS", headerStyle);
            
            GUILayout.FlexibleSpace();
            
            // Status indicator
            if (_isGenerating)
            {
                GUI.color = VarcoEditorStyles.Warning;
                GUILayout.Label("??Generating...");
            }
            else
            {
                GUI.color = VarcoEditorStyles.Success;
                GUILayout.Label("??Ready");
            }
            GUI.color = Color.white;
            
            EditorGUILayout.EndHorizontal();
        }
        
        private void DrawVoiceSection()
        {
            _showVoiceSection = EditorGUILayout.BeginFoldoutHeaderGroup(_showVoiceSection, "?éôÔ∏?Voice Settings");
            
            if (_showVoiceSection)
            {
                EditorGUILayout.BeginVertical(VarcoEditorStyles.CardStyle);
                
                // Voice selection with browse button
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(serializedObject.FindProperty("defaultVoice"), new GUIContent("Voice"));
                if (GUILayout.Button("?îç", GUILayout.Width(30)))
                {
                    VoicePickerWindow.ShowWindow();
                }
                EditorGUILayout.EndHorizontal();
                
                // Language
                EditorGUILayout.PropertyField(serializedObject.FindProperty("defaultLanguage"), new GUIContent("Language"));
                
                EditorGUILayout.Space(4);
                
                // Speed & Pitch sliders
                EditorGUILayout.PropertyField(serializedObject.FindProperty("defaultSpeed"), new GUIContent("Speed"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("defaultPitch"), new GUIContent("Pitch"));
                
                EditorGUILayout.Space(4);
                
                // Quality
                EditorGUILayout.PropertyField(serializedObject.FindProperty("qualityLevel"), new GUIContent("Quality"));
                
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }
        
        private void DrawDSPSection()
        {
            _showDSPSection = EditorGUILayout.BeginFoldoutHeaderGroup(_showDSPSection, "?éõÔ∏?DSP Effects (Pro)");
            
            if (_showDSPSection)
            {
                EditorGUILayout.BeginVertical(VarcoEditorStyles.CardStyle);
                
                // Add Effect dropdown
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("+ Add Effect", GUILayout.Height(25)))
                {
                    ShowAddEffectMenu();
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.Space(4);
                
                var tts = (VarcoTTS)target;
                
                if (tts.Effects.Count == 0)
                {
                    EditorGUILayout.HelpBox("No effects active. Add effects to create a processing chain.", MessageType.Info);
                }
                else
                {
                    for (int i = 0; i < tts.Effects.Count; i++)
                    {
                        var effect = tts.Effects[i];
                        if (effect == null) continue;
                        
                        DrawEffectUI(effect, i);
                    }
                }
                
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }
        
        private void ShowAddEffectMenu()
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("?ìª EQ (16-Band)"), false, () => AddEffect(new ParametricEQ16()));
            menu.AddItem(new GUIContent("?îâ Dynamics"), false, () => AddEffect(new UnifiedDynamics()));
            menu.AddItem(new GUIContent("?îä Freeverb"), false, () => AddEffect(new FDNReverb()));
            menu.AddItem(new GUIContent("?éµ Pitch Shift (WSOLA)"), false, () => AddEffect(new WSOLAPitchShift()));
            menu.AddItem(new GUIContent("?±Ô∏è Delay"), false, () => AddEffect(new UnifiedDelay()));
            menu.AddItem(new GUIContent("?î• Tube Saturation"), false, () => AddEffect(new TubeEmulation()));
            menu.ShowAsContext();
        }
        
        private void AddEffect(DSPEffectBase effect)
        {
            var tts = (VarcoTTS)target;
            tts.Effects.Add(effect);
            EditorUtility.SetDirty(target);
        }
        
        private void DrawEffectUI(DSPEffectBase effect, int index)
        {
            var tts = (VarcoTTS)target;
            
            // Header
            var boxStyle = VarcoEditorStyles.CardStyle;
            
            EditorGUILayout.BeginVertical(boxStyle);
            EditorGUILayout.BeginHorizontal();
            
            effect.Enabled = EditorGUILayout.Toggle(effect.Enabled, GUILayout.Width(20));
            GUILayout.Label(effect.Name, EditorStyles.boldLabel);
            
            GUILayout.FlexibleSpace();
            
            if (GUILayout.Button("√ó", GUILayout.Width(20)))
            {
                tts.Effects.RemoveAt(index);
                EditorUtility.SetDirty(target);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }
            EditorGUILayout.EndHorizontal();
            
            if (effect.Enabled)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.Space(4);
                
                if (effect is ParametricEQ16 eq)
                {
                    DrawEQGraph(eq);
                    // Simple gain slider
                    eq.OutputGain = EditorGUILayout.Slider("Output Gain (dB)", eq.OutputGain, -24f, 24f);
                }
                else if (effect is UnifiedDynamics dynamics)
                {
                    DrawUnifiedDynamicsUI(dynamics);
                }
                else if (effect is CompressorEffect comp)
                {
                    DrawCompressorUI(comp);
                }
                else if (effect is WSOLAPitchShift psola)
                {
                    psola.Semitones = EditorGUILayout.Slider("Semitones", psola.Semitones, -12f, 12f);
                    psola.FormantPreservation = EditorGUILayout.Slider("Formant Preserve", psola.FormantPreservation, 0f, 1f);
                    psola.Mix = EditorGUILayout.Slider("Mix", psola.Mix, 0f, 1f);
                }
                else if (effect is PhaseVocoderPitchShift pitch)
                {
                    pitch.Semitones = EditorGUILayout.Slider("Semitones", pitch.Semitones, -12f, 12f);
                }
                else if (effect is FDNReverb reverb)
                {
                    reverb.RoomSize = EditorGUILayout.Slider("Room Size", reverb.RoomSize, 10f, 100f);
                    reverb.DecayTime = EditorGUILayout.Slider("Decay Time (s)", reverb.DecayTime, 0.1f, 10f);
                    reverb.Damping = EditorGUILayout.Slider("HF Damping", reverb.Damping, 0f, 1f);
                    reverb.Mix = EditorGUILayout.Slider("Mix", reverb.Mix, 0f, 1f);
                }
                else if (effect is UnifiedDelay delay)
                {
                    DrawUnifiedDelayUI(delay);
                }
                else if (effect is DelayEffect legacyDelay)
                {
                    legacyDelay.DelayTime = EditorGUILayout.Slider("Time (ms)", legacyDelay.DelayTime, 0f, 2000f);
                    legacyDelay.Feedback = EditorGUILayout.Slider("Feedback", legacyDelay.Feedback, 0f, 1f);
                    legacyDelay.Mix = EditorGUILayout.Slider("Mix", legacyDelay.Mix, 0f, 1f);
                }
                else if (effect is TubeEmulation tube)
                {
                    tube.Drive = EditorGUILayout.Slider("Drive (dB)", tube.Drive, -12f, 24f);
                    tube.Bias = EditorGUILayout.Slider("Bias", tube.Bias, -1f, 1f);
                    tube.Presence = EditorGUILayout.Slider("Presence", tube.Presence, 0f, 1f);
                    tube.Sag = EditorGUILayout.Slider("Sag", tube.Sag, 0f, 1f);
                    tube.Output = EditorGUILayout.Slider("Output (dB)", tube.Output, -12f, 12f);
                    tube.Mix = EditorGUILayout.Slider("Mix", tube.Mix, 0f, 1f);
                }
                else if (effect is ChorusEffect chorus)
                {
                    chorus.DelayMs = EditorGUILayout.Slider("Delay (ms)", chorus.DelayMs, 0f, 50f);
                    chorus.Depth = EditorGUILayout.Slider("Depth (ms)", chorus.Depth, 0f, 10f);
                    chorus.Rate = EditorGUILayout.Slider("Rate (Hz)", chorus.Rate, 0.1f, 10f);
                    chorus.Mix = EditorGUILayout.Slider("Mix", chorus.Mix, 0f, 1f);
                }
                
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.EndVertical();
            GUILayout.Space(4);
        }
        
        private void DrawEQGraph(ParametricEQ16 eq)
        {
            var rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(100));
            EditorGUI.DrawRect(rect, new Color(0.1f, 0.1f, 0.1f));
            
            if (Event.current.type == EventType.Repaint)
            {
                // Draw Grid
                Handles.color = new Color(0.3f, 0.3f, 0.3f, 0.5f);
                float[] freqs = { 100, 1000, 10000 };
                foreach (var f in freqs)
                {
                    float x = FreqToX(f, rect);
                    Handles.DrawLine(new Vector3(x, rect.y), new Vector3(x, rect.y + rect.height));
                }
                
                // Draw Curve
                Handles.color = VarcoEditorStyles.Mint;
                Vector3[] points = new Vector3[100];
                for (int i = 0; i < 100; i++)
                {
                    float t = i / 99f;
                    float freq = XToFreq(t);
                    float mag = eq.GetMagnitudeAtFrequency(freq, 44100);
                    float db = 20f * Mathf.Log10(mag);
                    float y = rect.y + rect.height / 2f - (db * 2f); // Scale: 10dB = 20px
                    y = Mathf.Clamp(y, rect.y, rect.y + rect.height);
                    points[i] = new Vector3(rect.x + t * rect.width, y, 0);
                }
                Handles.DrawAAPolyLine(2f, points);

                // Highlight selected band
                if (_selectedEQBand >= 0 && _selectedEQBand < eq.Bands.Length)
                {
                    var band = eq.Bands[_selectedEQBand];
                    float bx = FreqToX(band.Frequency, rect);
                    float by = rect.y + rect.height/2f - (band.Gain * 2f);
                    by = Mathf.Clamp(by, rect.y, rect.y + rect.height);
                    Handles.color = Color.yellow;
                    Handles.DrawWireDisc(new Vector3(bx, by, 0), Vector3.back, 5f);
                }
            }

            EditorGUILayout.Space(8);
            
            // Band Selection UI
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Band Control", EditorStyles.boldLabel, GUILayout.Width(100));
            if (GUILayout.Button("<", GUILayout.Width(25))) _selectedEQBand = Mathf.Max(0, _selectedEQBand - 1);
            GUILayout.Label($"Band {_selectedEQBand + 1}", VarcoEditorStyles.CenteredLabel, GUILayout.Width(60));
            if (GUILayout.Button(">", GUILayout.Width(25))) _selectedEQBand = Mathf.Min(15, _selectedEQBand + 1);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            // Band Parameters
            if (_selectedEQBand >= 0 && _selectedEQBand < eq.Bands.Length)
            {
                var band = eq.Bands[_selectedEQBand];
                EditorGUI.BeginChangeCheck();
                
                EditorGUILayout.BeginHorizontal();
                bool enabled = EditorGUILayout.Toggle(band.Enabled, GUILayout.Width(20));
                EQFilterType type = DrawEQFilterPopup(band.Type);
                EditorGUILayout.EndHorizontal();

                float freq = EditorGUILayout.Slider("Frequency", band.Frequency, 20f, 20000f);
                float gain = EditorGUILayout.Slider("Gain (dB)", band.Gain, -15f, 15f);
                float q = EditorGUILayout.Slider("Q / Res", band.Q, 0.1f, 10f);

                if (EditorGUI.EndChangeCheck())
                {
                    eq.SetBandEnabled(_selectedEQBand, enabled);
                    eq.SetBand(_selectedEQBand, freq, gain, q, type);
                }
            }
        }
        
        private float FreqToX(float freq, Rect rect)
        {
            // Log scale 20Hz - 20kHz
            float minLog = Mathf.Log10(20f);
            float maxLog = Mathf.Log10(20000f);
            float fLog = Mathf.Log10(freq);
            float t = (fLog - minLog) / (maxLog - minLog);
            return rect.x + t * rect.width;
        }
        
        private float XToFreq(float t)
        {
            float minLog = Mathf.Log10(20f);
            float maxLog = Mathf.Log10(20000f);
            return Mathf.Pow(10f, minLog + t * (maxLog - minLog));
        }
        private static EQFilterType SanitizeFilterType(EQFilterType type)
        {
            return type == EQFilterType.LowShelf || type == EQFilterType.HighShelf
                ? EQFilterType.Peak
                : type;
        }

        private static EQFilterType DrawEQFilterPopup(EQFilterType currentType)
        {
            var sanitized = SanitizeFilterType(currentType);
            int index = System.Array.IndexOf(EqFilterTypeOptions, sanitized);
            if (index < 0) index = 0;
            index = EditorGUILayout.Popup(index, EqFilterTypeLabels);
            return EqFilterTypeOptions[index];
        }

        private void DrawUnifiedDynamicsUI(UnifiedDynamics dynamics)
        {
            dynamics.Mode = (UnifiedDynamics.DynamicsMode)EditorGUILayout.EnumPopup("Mode", dynamics.Mode);
            dynamics.Threshold = EditorGUILayout.Slider("Threshold (dB)", dynamics.Threshold, -60f, 0f);
            dynamics.Attack = EditorGUILayout.Slider("Attack (ms)", dynamics.Attack, 0.1f, 200f);
            dynamics.Release = EditorGUILayout.Slider("Release (ms)", dynamics.Release, 10f, 1000f);

            switch (dynamics.Mode)
            {
                case UnifiedDynamics.DynamicsMode.Limiter:
                    dynamics.Ceiling = EditorGUILayout.Slider("Ceiling (dB)", dynamics.Ceiling, -12f, 0f);
                    dynamics.Lookahead = EditorGUILayout.Slider("Lookahead (ms)", dynamics.Lookahead, 0f, 15f);
                    dynamics.TruePeak = EditorGUILayout.Toggle("True Peak", dynamics.TruePeak);
                    break;
                case UnifiedDynamics.DynamicsMode.Gate:
                    dynamics.Range = EditorGUILayout.Slider("Range (dB)", dynamics.Range, -80f, 0f);
                    dynamics.Hold = EditorGUILayout.Slider("Hold (ms)", dynamics.Hold, 0f, 500f);
                    break;
                case UnifiedDynamics.DynamicsMode.Expander:
                    dynamics.Ratio = EditorGUILayout.Slider("Ratio", dynamics.Ratio, 1f, 10f);
                    dynamics.Knee = EditorGUILayout.Slider("Knee (dB)", dynamics.Knee, 0f, 24f);
                    break;
                default:
                    dynamics.Ratio = EditorGUILayout.Slider("Ratio", dynamics.Ratio, 1f, 20f);
                    dynamics.Knee = EditorGUILayout.Slider("Knee (dB)", dynamics.Knee, 0f, 24f);
                    dynamics.MakeupGain = EditorGUILayout.Slider("Makeup Gain (dB)", dynamics.MakeupGain, 0f, 24f);
                    break;
            }
        }

        private void DrawUnifiedDelayUI(UnifiedDelay delay)
        {
            delay.Mode = (UnifiedDelay.DelayMode)EditorGUILayout.EnumPopup("Mode", delay.Mode);
            delay.Time = EditorGUILayout.Slider("Time (ms)", delay.Time, 0f, 2000f);
            delay.Feedback = EditorGUILayout.Slider("Feedback", delay.Feedback, 0f, 0.95f);
            delay.Mix = EditorGUILayout.Slider("Mix", delay.Mix, 0f, 1f);

            switch (delay.Mode)
            {
                case UnifiedDelay.DelayMode.PingPong:
                    delay.Width = EditorGUILayout.Slider("Width", delay.Width, 0f, 1f);
                    break;
                case UnifiedDelay.DelayMode.MultiTap:
                    delay.TapCount = EditorGUILayout.IntSlider("Tap Count", delay.TapCount, 1, 8);
                    delay.TapSpacing = EditorGUILayout.Slider("Tap Spacing", delay.TapSpacing, 0.1f, 2f);
                    break;
                case UnifiedDelay.DelayMode.Tape:
                    delay.ModRate = EditorGUILayout.Slider("Mod Rate (Hz)", delay.ModRate, 0f, 5f);
                    delay.ModDepth = EditorGUILayout.Slider("Mod Depth (ms)", delay.ModDepth, 0f, 20f);
                    delay.FilterLow = EditorGUILayout.Slider("HPF (Hz)", delay.FilterLow, 20f, 20000f);
                    delay.FilterHigh = EditorGUILayout.Slider("LPF (Hz)", delay.FilterHigh, 20f, 20000f);
                    break;
                default:
                    delay.FilterLow = EditorGUILayout.Slider("HPF (Hz)", delay.FilterLow, 20f, 20000f);
                    delay.FilterHigh = EditorGUILayout.Slider("LPF (Hz)", delay.FilterHigh, 20f, 20000f);
                    break;
            }
        }

        private void DrawCompressorUI(CompressorEffect comp)
        {
            // Threshold / Ratio Sliders
            comp.Threshold = EditorGUILayout.Slider("Threshold (dB)", comp.Threshold, -60f, 0f);
            comp.Ratio = EditorGUILayout.Slider("Ratio", comp.Ratio, 1f, 20f);
            comp.Attack = EditorGUILayout.Slider("Attack (ms)", comp.Attack, 0.1f, 100f);
            comp.Release = EditorGUILayout.Slider("Release (ms)", comp.Release, 10f, 1000f);
            comp.MakeupGain = EditorGUILayout.Slider("Makeup Gain (dB)", comp.MakeupGain, 0f, 24f);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Metering", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            // Input Meter
            DrawVerticalMeter("In", comp.CurrentInput, -60f, 0f, VarcoEditorStyles.Mint);
            GUILayout.Space(10);
            
            // GR Meter (Inverted, red)
            DrawVerticalMeter("GR", comp.CurrentGainReduction, -30f, 0f, new Color(1f, 0.3f, 0.3f), true);
            GUILayout.Space(10);
            
            // Output Meter
            DrawVerticalMeter("Out", comp.CurrentOutput, -60f, 0f, VarcoEditorStyles.Mint);
            
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }
        
        private void DrawPreviewSection(VarcoTTS tts)
        {
            VarcoEditorStyles.DrawSectionHeader("??Preview & Test");
            
            EditorGUILayout.BeginVertical(VarcoEditorStyles.CardStyle);
            
            // Test text
            EditorGUILayout.LabelField("Test Text:");
            _testText = EditorGUILayout.TextArea(_testText, GUILayout.Height(50));
            
            EditorGUILayout.Space(8);
            
            // Buttons
            EditorGUILayout.BeginHorizontal();
            
            GUI.enabled = !_isGenerating;
            
            // Generate & Play button
            GUI.backgroundColor = VarcoEditorStyles.Mint;
            if (GUILayout.Button("??Generate & Play", GUILayout.Height(35)))
            {
                GenerateAndPlay(tts).Forget();
            }
            GUI.backgroundColor = Color.white;
            
            // Stop button
            if (GUILayout.Button("??Stop", GUILayout.Width(80), GUILayout.Height(35)))
            {
                StopPlayback();
            }
            
            GUI.enabled = true;
            
            EditorGUILayout.EndHorizontal();
            
            // Preview clip info
            if (_previewClip != null)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField($"Duration: {_previewClip.length:F2}s | Sample Rate: {_previewClip.frequency}Hz", EditorStyles.miniLabel);
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawExportSection(VarcoTTS tts)
        {
            _showExportSection = EditorGUILayout.BeginFoldoutHeaderGroup(_showExportSection, "?íæ Export");
            
            if (_showExportSection)
            {
                EditorGUILayout.BeginVertical(VarcoEditorStyles.CardStyle);
                
                // Export path
                EditorGUILayout.BeginHorizontal();
                _exportPath = EditorGUILayout.TextField("Path:", _exportPath);
                if (GUILayout.Button("?ìÅ", GUILayout.Width(30)))
                {
                    var path = EditorUtility.OpenFolderPanel("Select Export Folder", _exportPath, "");
                    if (!string.IsNullOrEmpty(path))
                    {
                        _exportPath = path;
                    }
                }
                EditorGUILayout.EndHorizontal();
                
                // File name
                _exportFileName = EditorGUILayout.TextField("File Name:", _exportFileName);
                
                EditorGUILayout.Space(8);
                
                // Export button
                GUI.enabled = _previewClip != null;
                
                GUI.backgroundColor = VarcoEditorStyles.Blue;
                if (GUILayout.Button("?íæ Export as WAV", GUILayout.Height(30)))
                {
                    ExportWav(_previewClip);
                }
                GUI.backgroundColor = Color.white;
                
                if (_previewClip == null)
                {
                    EditorGUILayout.HelpBox("Generate audio first before exporting.", MessageType.Info);
                }
                
                GUI.enabled = true;
                
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }
        
        private async UniTaskVoid GenerateAndPlay(VarcoTTS tts)
        {
            _isGenerating = true;
            Repaint();
            
            try
            {
                var client = new VarcoApiClient();
                _previewClip = await client.SynthesizeAsync(_testText, 
                    serializedObject.FindProperty("defaultVoice").stringValue);
                
                // Setup DSP Chain for preview
                if (tts.Effects.Count > 0)
                {
                    EnsurePreviewSource();
                    
                    // Add DSPChain component if missing
                    _previewChain = _previewSource.gameObject.GetComponent<DSPChain>();
                    if (_previewChain == null) _previewChain = _previewSource.gameObject.AddComponent<DSPChain>();
                    
                    // Update chain with current effects
                    _previewChain.ClearEffects();
                    foreach (var effect in tts.Effects)
                    {
                        if (effect.Enabled) _previewChain.AddEffect(effect);
                    }
                    
                    // Note: DSPChain processes audio via OnAudioFilterRead when AudioSource plays
                }
                else
                {
                    // Clean up DSP chain if no effects
                    if (_previewSource != null)
                    {
                        var chain = _previewSource.gameObject.GetComponent<DSPChain>();
                        if (chain != null) DestroyImmediate(chain);
                    }
                }
                
                // Analyze for LipSync if enabled
                if (_enableLipSync)
                {
                    var analyzer = new LipSync.EnhancedLipSyncAnalyzer();
                    _lipSyncData = analyzer.AnalyzeEnhanced(_previewClip);
                    Debug.Log($"[VarcoTTS] LipSync analyzed: {_lipSyncData?.Keyframes.Count ?? 0} keyframes");
                }
                
                EnsurePreviewSource();
                _previewSource.clip = _previewClip;
                _previewSource.Play();
                
                Debug.Log($"[VarcoTTS] Playing: {_previewClip.length:F2}s (DSP: {tts.Effects.Count} effects)");
            }
            catch (VarcoException ex)
            {
                EditorUtility.DisplayDialog("VARCO Voice", $"Error: {ex.Message}", "OK");
            }
            finally
            {
                _isGenerating = false;
                Repaint();
            }
        }
        
        private void EnsurePreviewSource()
        {
            if (_previewSource == null)
            {
                var go = new GameObject("[VarcoPreview]");
                go.hideFlags = HideFlags.HideAndDontSave;
                _previewSource = go.AddComponent<AudioSource>();
                _previewSource.playOnAwake = false;
                _previewSource.spatialBlend = 0f;
            }
        }
        
        private void StopPlayback()
        {
            if (_previewSource != null)
            {
                _previewSource.Stop();
            }
        }

        private void DrawVerticalMeter(string label, float val, float min, float max, Color c, bool inverted = false)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(40));
            // Fixed: Use style for alignment instead of GUILayout.Alignment
            var centeredMiniLabel = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter };
            GUILayout.Label(label, centeredMiniLabel);
            
            var rect = GUILayoutUtility.GetRect(30, 100);
            EditorGUI.DrawRect(rect, new Color(0.1f, 0.1f, 0.1f)); // Background
            
            float range = max - min;
            float activeHeight = 0f;
            
            if (inverted) // GR Meter (0 at top, -30 at bottom)
            {
                // val is like -5. 
                // normalized = 5 / 30
                float normalized = Mathf.Clamp01(Mathf.Abs(val) / Mathf.Abs(min));
                activeHeight = normalized * rect.height;
                
                var barRect = new Rect(rect.x, rect.y, rect.width, activeHeight);
                EditorGUI.DrawRect(barRect, c);
                
                // Draw text value
                Rect textRect = new Rect(rect.x, rect.y + 2, rect.width, 20);
                EditorGUI.LabelField(textRect, $"{val:F1}", new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.UpperCenter, normal = { textColor = Color.white } });
            }
            else // Level meter (-60 at bottom, 0 at top)
            {
                float normalized = Mathf.Clamp01((val - min) / range);
                activeHeight = normalized * rect.height;
                
                var barRect = new Rect(rect.x, rect.y + rect.height - activeHeight, rect.width, activeHeight);
                EditorGUI.DrawRect(barRect, c);
                
                // Draw text value
                Rect textRect = new Rect(rect.x, rect.y + rect.height - 20, rect.width, 20);
                EditorGUI.LabelField(textRect, $"{val:F1}", new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.LowerCenter, normal = { textColor = Color.white } });
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void ExportWav(AudioClip clip)
        {
            if (clip == null) return;
            
            // Create directory if needed
            if (!Directory.Exists(_exportPath))
            {
                Directory.CreateDirectory(_exportPath);
            }
            
            var fullPath = Path.Combine(_exportPath, _exportFileName + ".wav");
            
            // Convert AudioClip to WAV
            var samples = new float[clip.samples * clip.channels];
            clip.GetData(samples, 0);
            
            var wavData = ConvertToWav(samples, clip.frequency, clip.channels);
            File.WriteAllBytes(fullPath, wavData);
            
            AssetDatabase.Refresh();
            
            EditorUtility.DisplayDialog("VARCO Voice", $"Exported to:\n{fullPath}", "OK");
            Debug.Log($"[VarcoTTS] Exported WAV to: {fullPath}");
        }
        
        private byte[] ConvertToWav(float[] samples, int sampleRate, int channels)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                // WAV header
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + samples.Length * 2);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
                writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
                writer.Write(16);
                writer.Write((short)1); // PCM
                writer.Write((short)channels);
                writer.Write(sampleRate);
                writer.Write(sampleRate * channels * 2);
                writer.Write((short)(channels * 2));
                writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                writer.Write(samples.Length * 2);
                
                // Convert samples
                foreach (var sample in samples)
                {
                    var intSample = (short)(sample * 32767f);
                    writer.Write(intSample);
                }
                
                return stream.ToArray();
            }
        }
        
        private void DrawLipSyncSection(VarcoTTS tts)
        {
            _showLipSyncSection = EditorGUILayout.BeginFoldoutHeaderGroup(_showLipSyncSection, "?ëÑ Lip Sync");
            
            if (_showLipSyncSection)
            {
                EditorGUILayout.BeginVertical(VarcoEditorStyles.CardStyle);
                
                // Enable toggle
                _enableLipSync = EditorGUILayout.Toggle("Enable Lip Sync", _enableLipSync);
                
                if (_enableLipSync)
                {
                    EditorGUI.indentLevel++;
                    
                    // Sensitivity slider
                    _lipSyncSensitivity = EditorGUILayout.Slider("Sensitivity", _lipSyncSensitivity, 0.1f, 1f);
                    
                    EditorGUILayout.Space(4);
                    
                    // Analyzed data info
                    if (_lipSyncData != null)
                    {
                        EditorGUILayout.LabelField($"Keyframes: {_lipSyncData.Keyframes.Count}", EditorStyles.miniLabel);
                        EditorGUILayout.LabelField($"Duration: {_lipSyncData.Duration:F2}s", EditorStyles.miniLabel);
                        
                        // Viseme preview
                        EditorGUILayout.Space(4);
                        EditorGUILayout.LabelField("Viseme Timeline:", EditorStyles.boldLabel);
                        
                        var rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(30));
                        DrawVisemeTimeline(rect);
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("Generate audio to analyze lip sync data.", MessageType.Info);
                    }
                    
                    EditorGUI.indentLevel--;
                }
                
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }
        
        private void DrawVisemeTimeline(Rect rect)
        {
            if (_lipSyncData == null || _lipSyncData.Keyframes.Count == 0) return;
            
            EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.15f));
            
            var colors = new Dictionary<LipSync.VisemeType, Color>
            {
                { LipSync.VisemeType.Silence, Color.gray },
                { LipSync.VisemeType.AA, new Color(1f, 0.4f, 0.4f) },
                { LipSync.VisemeType.EE, new Color(0.4f, 1f, 0.4f) },
                { LipSync.VisemeType.IH, new Color(0.4f, 0.4f, 1f) },
                { LipSync.VisemeType.OH, new Color(1f, 1f, 0.4f) },
                { LipSync.VisemeType.OO, new Color(1f, 0.4f, 1f) },
            };
            
            float frameWidth = rect.width / _lipSyncData.Keyframes.Count;
            for (int i = 0; i < _lipSyncData.Keyframes.Count; i++)
            {
                var frame = _lipSyncData.Keyframes[i];
                var color = colors.ContainsKey(frame.Viseme) ? colors[frame.Viseme] : Color.white;
                
                var frameRect = new Rect(rect.x + i * frameWidth, rect.y, frameWidth, rect.height * frame.Weight);
                frameRect.y = rect.y + rect.height - frameRect.height;
                
                EditorGUI.DrawRect(frameRect, color);
            }
        }
        

        
        private void OnDisable()
        {
            if (_previewSource != null)
            {
                DestroyImmediate(_previewSource.gameObject);
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
            VarcoEditorStyles.DrawSectionHeader("Lip Sync Tools");
            
            EditorGUILayout.BeginVertical(VarcoEditorStyles.CardStyle);
            
            if (VarcoEditorStyles.DrawAccentButton("Create Default Profile"))
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

