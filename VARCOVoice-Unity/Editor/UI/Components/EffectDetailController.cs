using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VARCOVoice.DSP;

namespace VARCOVoice.Editor
{
    // ... rest of namespace ...

    /// <summary>
    /// Controller for the Effect Detail Panel.
    /// Dynamically generates UI controls based on effect properties using reflection.
    /// </summary>
    public partial class EffectDetailController
    {
        private VisualElement _root;
        private VisualElement _contentContainer;
        private Label _titleLabel;
        private VisualElement _emptyState;
        private Toggle _bypassToggle;
        private Button _resetBtn;
        private Button _popoutBtn;
        private Button _savePresetBtn;
        private VisualElement _presetControls;
        private DropdownField _presetDropdown;
        private string _currentPresetName = null;  // Track selected preset name
        private const string PresetPlaceholder = "Preset...";

        private IDSPEffect _currentEffect;
        private IEffectInspector _currentInspector;
        private DSPChain _chain;
        private readonly List<IVisualElementScheduledItem> _scheduledUpdates = new();
        private bool _editSessionActive;





        public event Action OnEffectChanged;
        public event Action OnEditSessionBegin;
        public event Action OnEditSessionEnd;

        internal VisualElement ContentContainer => _contentContainer;

        public void Initialize(VisualElement root, DSPChain chain)
        {
            _root = root;
            _chain = chain;

            _titleLabel = _root.Q<Label>("effect-title");
            _contentContainer = _root.Q<VisualElement>("detail-content");
            _emptyState = _root.Q<VisualElement>("empty-state");
            _bypassToggle = _root.Q<Toggle>("bypass-toggle");
            _resetBtn = _root.Q<Button>("reset-btn");
            _popoutBtn = _root.Q<Button>("popout-btn");
            _savePresetBtn = _root.Q<Button>("save-preset-btn");
            _presetControls = _root.Q<VisualElement>("preset-controls");
            _presetDropdown = _root.Q<DropdownField>("preset-dropdown");

            // Setup callbacks
            _bypassToggle?.RegisterValueChangedCallback(evt =>
            {
                BeginEditSession();
                if (_currentEffect != null)
                {
                    _currentEffect.Enabled = !evt.newValue; // Bypass = NOT enabled
                    NotifyChange();
                }
                EndEditSession();
            });

            _resetBtn?.RegisterCallback<ClickEvent>(evt =>
            {
                BeginEditSession();
                if (_currentEffect != null)
                {
                    _currentEffect.Reset();
                    DisplayEffect(_currentEffect); // Refresh UI
                    NotifyChange();
                }
                EndEditSession();
            });

            _presetDropdown?.RegisterValueChangedCallback(evt =>
            {
                if (_currentEffect == null) return;
                if (string.IsNullOrEmpty(evt.newValue) || evt.newValue == PresetPlaceholder) return;
                BeginEditSession();
                _currentPresetName = evt.newValue;  // Save selection
                ApplyPresetAndRefresh(_currentEffect, evt.newValue);
                EndEditSession();
            });

            // Save preset button
            _savePresetBtn?.RegisterCallback<ClickEvent>(evt =>
            {
                if (_currentEffect == null) return;
                ShowSavePresetDialog();
            });

            ShowEmptyState();
        }

        public void DisplayEffect(IDSPEffect effect)
        {
            _currentEffect = effect;
            _currentInspector?.Cleanup();
            _currentInspector = null;
            ClearScheduledUpdates();

            if (effect == null)
            {
                ShowEmptyState();
                return;
            }

            HideEmptyState();

            if (_titleLabel != null)
                _titleLabel.text = effect.Name;

            if (_bypassToggle != null)
                _bypassToggle.SetValueWithoutNotify(!effect.Enabled);

            UpdatePresetDropdown(effect);

            // Clear existing content
            _contentContainer?.Clear();

            _currentInspector = EffectInspectorFactory.Create(this, effect);
            _currentInspector?.CreateUI(effect);
        }

        private void UpdatePresetDropdown(IDSPEffect effect)
        {
            if (_presetControls == null || _presetDropdown == null)
                return;

            if (effect == null)
            {
                SetPresetControlsVisible(false);
                return;
            }

            var options = GetPresetOptions(effect);
            if (options == null || options.Count == 0)
            {
                SetPresetControlsVisible(false);
                return;
            }

            SetPresetControlsVisible(true);

            var choices = new List<string>(options.Count + 1) { PresetPlaceholder };
            choices.AddRange(options);
            _presetDropdown.choices = choices;
            
            // Restore current preset selection if it exists in choices
            if (!string.IsNullOrEmpty(_currentPresetName) && choices.Contains(_currentPresetName))
            {
                _presetDropdown.SetValueWithoutNotify(_currentPresetName);
            }
            else
            {
                _presetDropdown.SetValueWithoutNotify(PresetPlaceholder);
            }
        }



        private void ShowSavePresetDialog()
        {
            if (_currentEffect == null) return;
            
            // Simple save dialog using Unity's built-in
            var effectName = _currentEffect.Name.Replace(" ", "");
            var defaultName = $"{effectName}_Custom";
            
            var path = EditorUtility.SaveFilePanel(
                "Save Effect Preset",
                "",
                defaultName,
                "");
            
            if (!string.IsNullOrEmpty(path))
            {
                var presetName = System.IO.Path.GetFileNameWithoutExtension(path);
                var baseEffect = _currentEffect as DSPEffectBase;
                if (baseEffect != null)
                {
                    // Store current values as a new preset (future: save to JSON)
                    Debug.Log($"[EffectDetail] Saved preset: {presetName} for {_currentEffect.Name}");
                    
                    // Update dropdown to show new preset name
                    if (_presetDropdown != null)
                    {
                        var choices = _presetDropdown.choices;
                        if (!choices.Contains(presetName))
                        {
                            choices.Add(presetName);
                            _presetDropdown.choices = choices;
                        }
                        _presetDropdown.SetValueWithoutNotify(presetName);
                    }
                }
            }
        }

        private void SetPresetControlsVisible(bool visible)
        {
            if (_presetControls == null) return;
            _presetControls.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        internal void EnsureKnobStyles()
        {
            if (_root == null) return;

            var knobStyle = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Packages/com.varco.voice/Editor/UI/Common/KnobControl.uss");
            if (knobStyle != null && !_root.styleSheets.Contains(knobStyle))
                _root.styleSheets.Add(knobStyle);
        }

        internal void BuildParameterUI(IDSPEffect effect)
        {
            if (_contentContainer == null) return;

            EnsureKnobStyles();

            var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Special 3-zone layout dispatch
            if (TryBuild3ZoneUI(effect, excluded))
            {
                return;
            }

            // Fallback simple layout for other effects
            BuildSimpleParameterUI(effect, excluded);
        }

        private bool TryBuild3ZoneUI(IDSPEffect effect, HashSet<string> excluded)
        {
            if (effect is UnifiedDynamics dynamics)
            {
                BuildDynamics3ZoneUI(dynamics, excluded);
                return true;
            }
            if (effect is UnifiedDelay delay)
            {
                BuildDelayUI(delay, excluded);
                return true;
            }
            if (effect is ChorusEffect chorus)
            {
                BuildModulationUI(chorus, "Chorus", chorus.Rate, chorus.Depth, chorus.Mix, null, null, excluded);
                return true;
            }
            if (effect is PhaserEffect phaser)
            {
                BuildModulationUI(phaser, "Phaser", phaser.Rate, phaser.Depth, phaser.Mix, phaser.Feedback, null, excluded);
                return true;
            }
            if (effect is FlangerEffect flanger)
            {
                BuildModulationUI(flanger, "Flanger", flanger.Rate, flanger.Depth, flanger.Mix, flanger.Feedback, flanger.BaseDelay, excluded);
                return true;
            }
            if (effect is FDNReverb reverb)
            {
                BuildReverbUI(reverb, excluded);
                return true;
            }
            if (effect is WSOLAPitchShift pitch)
            {
                BuildPitchShiftUI(pitch, excluded);
                return true;
            }
            if (effect is TubeEmulation tube)
            {
                BuildTubeUI(tube, excluded);
                return true;
            }
            // New effects
            if (effect is DistortionEffect distortion)
            {
                BuildDistortionUI(distortion, excluded);
                return true;
            }
            if (effect is SaturationEffect saturation)
            {
                BuildSaturationUI(saturation, excluded);
                return true;
            }
            if (effect is TapeEmulation tape)
            {
                BuildTapeUI(tape, excluded);
                return true;
            }
            if (effect is TremoloEffect tremolo)
            {
                BuildTremoloUI(tremolo, excluded);
                return true;
            }
            if (effect is RingModulatorEffect ringMod)
            {
                BuildRingModUI(ringMod, excluded);
                return true;
            }
            if (effect is Spatial3DEffect spatial)
            {
                BuildSpatial3DUI(spatial, excluded);
                return true;
            }
            if (effect is ParametricEQ16 eq)
            {
                BuildParametricEQUI(eq, excluded);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Generic helper to build the Standard 3-Zone Layout
        /// </summary>
        private void BuildStandard3ZoneUI(
            IDSPEffect effect, 
            string effectName, 
            Texture2D iconTexture,
            Action<VisualElement> buildLeftZone,
            Action<VisualElement> buildCenterZone,
            Action<VisualElement> buildRightZone)
        {
            // Main horizontal container
            var mainContainer = new VisualElement();
            mainContainer.style.flexDirection = FlexDirection.Row;
            mainContainer.style.flexGrow = 1;
            mainContainer.style.height = Length.Percent(100);
            _contentContainer.Add(mainContainer);

            // === SIDEBAR (Icon) ===
            var sidebar = new VisualElement();
            sidebar.AddToClassList("effect-sidebar");
            mainContainer.Add(sidebar);

            // Spacer to push label to center
            var topSpacer = new VisualElement { style = { flexGrow = 1 } };
            sidebar.Add(topSpacer);

            // Add Sidebar Label
            var sideLabel = new Label(effectName.ToUpper());
            sideLabel.AddToClassList("effect-sidebar-label");
            // Override margins for centering
            sideLabel.style.marginTop = 0;
            sideLabel.style.marginBottom = 0;
            sidebar.Add(sideLabel);

            var bottomSpacer = new VisualElement { style = { flexGrow = 1 } };
            sidebar.Add(bottomSpacer);

            // Power (Bypass) Toggle
            var powerBtn = new Button(() => {
                BeginEditSession();
                effect.Enabled = !effect.Enabled;
                DisplayEffect(effect);
                NotifyChange();
                EndEditSession();
            });
            powerBtn.AddToClassList("effect-power-btn");
            if (!effect.Enabled) powerBtn.AddToClassList("bypassed");
            powerBtn.tooltip = "Enable/Disable Effect";
            
            // Override positioning for absolute bottom placement via spacers
            powerBtn.style.marginTop = 0; 
            powerBtn.style.marginBottom = 24;
            
            sidebar.Add(powerBtn);

            // === LEFT ZONE (Knobs) ===
            var leftZone = new VisualElement();
            leftZone.AddToClassList("knob-zone-left");
            // Ensure connection for legacy CSS
            leftZone.style.justifyContent = Justify.Center; 
            buildLeftZone?.Invoke(leftZone);
            mainContainer.Add(leftZone);

            // === CENTER ZONE (Visualizer) ===
            var visualizer = new VisualElement();
            visualizer.AddToClassList("visualizer-zone");
            
            // Add Label
            var vizLabel = new Label(effectName.ToUpper());
            vizLabel.AddToClassList("visualizer-label");
            visualizer.Add(vizLabel);

            buildCenterZone?.Invoke(visualizer);
            mainContainer.Add(visualizer);

            // === RIGHT ZONE (Knobs/Mix) ===
            var rightZone = new VisualElement();
            rightZone.AddToClassList("knob-zone-right");
            buildRightZone?.Invoke(rightZone);
            mainContainer.Add(rightZone);
        }


        /// <summary>
        /// Simple fallback UI for non-Dynamics effects
        /// </summary>
        private void BuildSimpleParameterUI(IDSPEffect effect, HashSet<string> excluded)
        {
            var container = new VisualElement();
            container.AddToClassList("detail-scroll");
            container.style.flexDirection = FlexDirection.Column;
            container.style.paddingTop = 12;
            container.style.paddingBottom = 12;
            container.style.paddingLeft = 12;
            container.style.paddingRight = 12;
            _contentContainer.Add(container);

            var parameters = GetParameters(effect);

            // Mode dropdown for unified effects
            if (effect is UnifiedDelay unifiedDelay)
            {
                AddModeRow(container, unifiedDelay.Mode, newValue =>
                {
                    BeginEditSession();
                    unifiedDelay.Mode = (UnifiedDelay.DelayMode)newValue;       
                    DisplayEffect(effect);
                    NotifyChange();
                    EndEditSession();
                });
                ApplyUnifiedDelayExclusions(unifiedDelay, excluded);
            }

            if (effect is FDNReverb reverb)
            {
                container.Add(CreateReverbSpacePad(reverb));
                excluded.Add(nameof(FDNReverb.RoomSize));
                excluded.Add(nameof(FDNReverb.DecayTime));
            }

            // Simple vertical list
            foreach (var param in parameters)
            {
                if (excluded.Contains(param.Name)) continue;
                var row = CreateParameterRow(effect, param);
                if (row != null)
                    container.Add(row);
            }
        }

        private void AddModeRow(VisualElement container, Enum currentValue, Action<Enum> onChanged)
        {
            if (container == null) return;

            var row = new VisualElement();
            row.AddToClassList("param-row");

            var label = new Label("Mode");
            label.AddToClassList("param-label");
            row.Add(label);

            var dropdown = new EnumField(currentValue);
            dropdown.AddToClassList("param-dropdown");
            dropdown.RegisterCallback<PointerDownEvent>(_ => BeginEditSession());
            dropdown.RegisterCallback<PointerUpEvent>(_ => EndEditSession());
            dropdown.RegisterCallback<PointerCaptureOutEvent>(_ => EndEditSession());
            dropdown.RegisterValueChangedCallback(evt =>
            {
                onChanged?.Invoke((Enum)evt.newValue);
            });

            row.Add(dropdown);
            container.Add(row);
        }

        private void ClearScheduledUpdates()
        {
            foreach (var item in _scheduledUpdates)
                item.Pause();
            _scheduledUpdates.Clear();
        }

        internal void TrackScheduledItem(IVisualElementScheduledItem item)
        {
            if (item != null)
                _scheduledUpdates.Add(item);
        }


        private void ShowEmptyState()
        {
            _emptyState?.RemoveFromClassList("hidden");
            if (_contentContainer != null) _contentContainer.style.display = DisplayStyle.None;
            if (_titleLabel != null) _titleLabel.text = "Select an Effect";
            SetPresetControlsVisible(false);
        }

        private void HideEmptyState()
        {
            _emptyState?.AddToClassList("hidden");
            if (_contentContainer != null) _contentContainer.style.display = DisplayStyle.Flex;
        }

        public void Clear()
        {
            _currentEffect = null;
            _currentInspector?.Cleanup();
            _currentInspector = null;
            ClearScheduledUpdates();
            ShowEmptyState();
        }

        internal VisualElement CreateParameterRow(IDSPEffect effect, string paramName)
        {
            if (effect == null || string.IsNullOrEmpty(paramName)) return null;
            var param = GetParameters(effect).Find(p => p.Name == paramName);
            return param != null ? CreateParameterRow(effect, param) : null;
        }
    }
}

