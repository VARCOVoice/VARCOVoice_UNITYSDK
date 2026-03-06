using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine.UIElements;
using VARCOVoice.DSP;
using VARCOVoice.Editor.UI.Components;

namespace VARCOVoice.Editor
{
    public partial class EffectDetailController
    {
        private Action _onPresetApplied;

        private static readonly string[] CompressorPresets =
        {
            "Vocal Warmth",
            "Podcast Master",
            "Transparent",
            "Punchy",
            "Glue",
            "De-Breath",
            "Safety Limiter"
        };

        private static readonly string[] GatePresets =
        {
            "Voice Tight",
            "Noise Clean",
            "Fast Cut",
            "Room Gate"
        };

        private static readonly string[] ExpanderPresets =
        {
            "Gentle",
            "De-Noise",
            "Broadcast",
            "Pump"
        };

        private static readonly string[] MultibandPresets =
        {
            "Vocal Balance",
            "Bass Control",
            "Air Tame",
            "Mix Glue",
            "Punch"
        };

        private static readonly string[] DelayPresets =
        {
            "Slapback",
            "Vocal Double",
            "Rhythmic Quarter",
            "Rhythmic Dotted 8th",
            "Long Tail",
            "Filtered Echo"
        };

        private static readonly string[] MultiTapDelayPresets =
        {
            "Tight Slap",
            "Rhythmic",
            "Cascade",
            "Wide Wash",
            "Sparse"
        };

        private static readonly string[] PingPongDelayPresets =
        {
            "Wide Quarter",
            "Slap Ping",
            "Spiral",
            "Stereo Echo",
            "Ambient"
        };

        private static readonly string[] ModulatedDelayPresets =
        {
            "Tape Echo",
            "Vintage",
            "Wobble",
            "Subtle",
            "Lofi"
        };

        private static readonly string[] ChorusPresets =
        {
            "Subtle Stereo",
            "Lush",
            "80s Synth",
            "Detune",
            "Shimmer"
        };

        private static readonly string[] FlangerPresets =
        {
            "Jet Plane",
            "Gentle Sweep",
            "Metallic",
            "Barber Pole",
            "Vintage",
            "Extreme"
        };

        private static readonly string[] PhaserPresets =
        {
            "Vintage 4-Stage",
            "Swoosh",
            "Comb Filter",
            "Spinning Speaker",
            "Subtle Movement",
            "Space Phaser"
        };

        private static readonly string[] EqPresets =
        {
            "Voice Clarity",
            "Radio Voice",
            "Warmth",
            "Air & Shine",
            "De-Muddy",
            "Telephone",
            "Proximity Effect Fix",
            "Flat"
        };

        private static readonly string[] Eq5BandPresets =
        {
            "Flat",
            "Voice Clarity",
            "Warm",
            "Air",
            "Telephone"
        };

        // LowPass Presets Removed


        private static readonly string[] LimiterPresets =
        {
            "Mastering",
            "Podcast",
            "Safety",
            "Loud"
        };

        private static readonly string[] DistortionPresets =
        {
            "Soft Drive",
            "Crunch",
            "Tube Warm",
            "Tape Grit",
            "Fuzz",
            "Bitcrush"
        };

        private static readonly string[] SaturationPresets =
        {
            "Subtle",
            "Warm",
            "Bright",
            "Tape",
            "Heavy"
        };

        private static readonly string[] WaveshaperPresets =
        {
            "Smooth",
            "Punch",
            "Expand",
            "Asym Drive",
            "Hard"
        };

        private static readonly string[] TremoloPresets =
        {
            "Slow Pulse",
            "Fast Chop",
            "Swirl",
            "Subtle",
            "Stereo Pan"
        };

        private static readonly string[] RingModPresets =
        {
            "Robot",
            "Metallic",
            "AM Radio",
            "Alien",
            "Subtle"
        };

        private static readonly string[] SpatialPresets =
        {
            "Center",
            "Left",
            "Right",
            "Wide",
            "Narrow",
            "Far",
            "Near"
        };

        private static readonly string[] PhaseVocoderPresets =
        {
            "Up 3",
            "Down 3",
            "Octave Up",
            "Octave Down",
            "Doubler"
        };

        private static readonly string[] PSOLAPresets =
        {
            "Natural Up",
            "Natural Down",
            "Chipmunk",
            "Deep",
            "Doubler"
        };



        private static readonly string[] LinearPhaseEqPresets =
        {
            "Flat",
            "Warmth",
            "Air",
            "Telephone",
            "Presence"
        };

        private static readonly string[] FDNReverbPresets =
        {
            "Small Room",
            "Large Hall",
            "Cathedral",
            "Plate",
            "Ambient"
        };

        private static readonly string[] HrtfPresets =
        {
            "Front",
            "Left",
            "Right",
            "Behind",
            "Above",
            "Far"
        };

        private static readonly string[] TapePresets =
        {
            "Clean Tape",
            "Warm Tape",
            "Vintage",
            "Bright",
            "Lofi"
        };

        private static readonly string[] TubePresets =
        {
            "Clean",
            "Warm",
            "Crunch",
            "Vintage",
            "Edge"
        };

        private IReadOnlyList<string> GetPresetOptions(IDSPEffect effect)
        {
            if (effect is UnifiedDynamics unifiedDynamics)
            {
                switch (unifiedDynamics.Mode)
                {
                    case UnifiedDynamics.DynamicsMode.Limiter:
                        return LimiterPresets;
                    case UnifiedDynamics.DynamicsMode.Gate:
                        return GatePresets;
                    case UnifiedDynamics.DynamicsMode.Expander:
                        return ExpanderPresets;
                    default:
                        return CompressorPresets;
                }
            }
            if (effect is UnifiedDelay unifiedDelay)
            {
                switch (unifiedDelay.Mode)
                {
                    case UnifiedDelay.DelayMode.MultiTap:
                        return MultiTapDelayPresets;
                    case UnifiedDelay.DelayMode.PingPong:
                        return PingPongDelayPresets;
                    case UnifiedDelay.DelayMode.Tape:
                        return ModulatedDelayPresets;
                    default:
                        return DelayPresets;
                }
            }
            if (effect is GateEffect) return GatePresets;
            if (effect is ExpanderEffect) return ExpanderPresets;
            if (effect is CompressorEffect) return CompressorPresets;
            if (effect is MultibandCompressor) return MultibandPresets;
            if (effect is LimiterEffect) return LimiterPresets;
            if (effect is DelayEffect) return DelayPresets;
            if (effect is MultiTapDelay) return MultiTapDelayPresets;
            if (effect is PingPongDelay) return PingPongDelayPresets;
            if (effect is ModulatedDelay) return ModulatedDelayPresets;
            if (effect is ChorusEffect) return ChorusPresets;
            if (effect is FlangerEffect) return FlangerPresets;
            if (effect is PhaserEffect) return PhaserPresets;
            if (effect is ParametricEQ16) return EqPresets;
            if (effect is EQEffect) return Eq5BandPresets;
            if (effect is DistortionEffect) return DistortionPresets;
            if (effect is SaturationEffect) return SaturationPresets;
            if (effect is WaveshaperEffect) return WaveshaperPresets;
            if (effect is TremoloEffect) return TremoloPresets;
            if (effect is RingModulatorEffect) return RingModPresets;
            if (effect is Spatial3DEffect) return SpatialPresets;
            if (effect is PitchShift) return PSOLAPresets;

            if (effect is TapeEmulation) return TapePresets;
            if (effect is TubeEmulation) return TubePresets;
            if (effect is FDNReverb) return FDNReverbPresets;

            return Array.Empty<string>();
        }

        private void ApplyPresetAndRefresh(IDSPEffect effect, string presetName)
        {
            var baseEffect = effect as DSPEffectBase;
            if (baseEffect == null) return;

            baseEffect.ApplyPreset(presetName);
            DisplayEffect(effect);
            _onPresetApplied?.Invoke();
            NotifyChange();
        }

        private void BuildCenterPresetUI(VisualElement parent, IDSPEffect effect)
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.marginTop = 10;
            container.style.marginBottom = 5;
            container.style.justifyContent = Justify.Center;
            container.style.alignItems = Align.Center;

            // Get Presets
            var presets = GetPresetOptions(effect);
            var choiceList = new List<string>(presets.Count + 1) { PresetPlaceholder };
            choiceList.AddRange(presets);
            
            var dropdown = new DropdownField(choiceList, 0);
            dropdown.style.flexGrow = 1;
            dropdown.style.maxWidth = 140;
            dropdown.style.marginRight = 5;
            dropdown.RegisterCallback<PointerDownEvent>(_ => BeginEditSession());
            dropdown.RegisterCallback<PointerUpEvent>(_ => EndEditSession());
            dropdown.RegisterCallback<PointerCaptureOutEvent>(_ => EndEditSession());
            dropdown.RegisterValueChangedCallback(e => {
                if (e.newValue != PresetPlaceholder)
                {
                    ApplyPresetAndRefresh(effect, e.newValue);
                }
            });
            
            // Set current if needed (simplified)
            dropdown.SetValueWithoutNotify(PresetPlaceholder);
            
            container.Add(dropdown);

            var saveBtn = new Button(ShowSavePresetDialog) { text = "S" };
            saveBtn.style.width = 24;
            container.Add(saveBtn);
            
            parent.Add(container);
        }

        private void NotifyChange()
        {
            EditorUtility.SetDirty(_chain);
            OnEffectChanged?.Invoke();
        }

        private void BeginEditSession()
        {
            if (_editSessionActive) return;
            _editSessionActive = true;
            OnEditSessionBegin?.Invoke();
        }

        private void EndEditSession()
        {
            if (!_editSessionActive) return;
            _editSessionActive = false;
            OnEditSessionEnd?.Invoke();
        }

        private KnobControl CreateKnob(string label, float value, float min, float max, string unit, int size, Action<float> onChange)
        {
            var knob = new KnobControl(size);
            knob.label = label;
            knob.minValue = min;
            knob.maxValue = max;
            knob.value = value;
            knob.unit = unit;
            knob.RegisterCallback<PointerDownEvent>(_ => BeginEditSession());
            knob.RegisterCallback<PointerUpEvent>(_ => EndEditSession());
            knob.RegisterCallback<PointerCaptureOutEvent>(_ => EndEditSession());
            knob.onValueChanged += onChange;
            return knob;
        }
    }
}
