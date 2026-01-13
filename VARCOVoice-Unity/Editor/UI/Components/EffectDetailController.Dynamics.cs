using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VARCOVoice.DSP;
using VARCOVoice.Editor.UI.Components;

namespace VARCOVoice.Editor
{
    public partial class EffectDetailController
    {
        internal void BuildDynamics3ZoneUI(UnifiedDynamics dynamics, HashSet<string> excluded)
        {
            ApplyUnifiedDynamicsExclusions(dynamics, excluded);

            BuildStandard3ZoneUI(dynamics, "DYNAMICS", null,
                // Left Zone Builder
                (leftZone) =>
                {
                    // Store knob references for parameter updates
                    KnobControl ratioKnob = null;

                    // Left knobs: Threshold, Ratio
                    var thresholdKnob = CreateKnob("Threshold", dynamics.Threshold, -60f, 0f, "dB", 75,
                        v => { dynamics.Threshold = v; EditorUtility.SetDirty(_chain); OnEffectChanged?.Invoke(); });
                    leftZone.Add(thresholdKnob);

                    if (dynamics.Mode != UnifiedDynamics.DynamicsMode.Limiter &&
                        dynamics.Mode != UnifiedDynamics.DynamicsMode.Gate)
                    {
                        ratioKnob = CreateKnob("Ratio", dynamics.Ratio, 1f, 20f, ":1", 75,
                            v => { dynamics.Ratio = v; EditorUtility.SetDirty(_chain); OnEffectChanged?.Invoke(); });
                        leftZone.Add(ratioKnob);
                    }

                    // Attack, Release (smaller)
                    var attackKnob = CreateKnob("Attack", dynamics.Attack, 0.1f, 200f, "ms", 60,
                        v => { dynamics.Attack = v; EditorUtility.SetDirty(_chain); OnEffectChanged?.Invoke(); });
                    leftZone.Add(attackKnob);

                    var releaseKnob = CreateKnob("Release", dynamics.Release, 10f, 2000f, "ms", 60,
                        v => { dynamics.Release = v; EditorUtility.SetDirty(_chain); OnEffectChanged?.Invoke(); });
                    leftZone.Add(releaseKnob);
                    
                    // We need to capture the ratioKnob reference for the preset callback.
                    // Since ratioKnob is local, we need to ensure the preset buttons can access it.
                    // The preset logic is in the Center Zone, which is built separately.
                    // However, we can use 'leftZone.Q<KnobControl>("Ratio")' or similar if needed,
                    // but simpler is to just rebuild the UI on preset change (which DisplayEffect does).
                    // Or, we can rely on the fact that DisplayEffect is called, or OnEffectChanged reformats.
                    // Actually, the previous implementation updated knobs directly. 
                    // Let's keep it simple: The preset buttons will directly update the 'dynamics' object 
                    // and then call OnEffectChanged + DisplayEffect to refresh the whole UI.
                },
                
                // Center Zone Builder (Visualizer & Presets)
                (visualizer) =>
                {
                    // Preset Grid Container
                    var presetGrid = new VisualElement();
                    presetGrid.AddToClassList("preset-grid-container");
                    visualizer.Add(presetGrid);

                    // Define Presets (8 per mode)
                    var presets = new Dictionary<UnifiedDynamics.DynamicsMode, List<(string Name, float Th, float Ratio, float Atk, float Rel)>>()
                    {
                        { UnifiedDynamics.DynamicsMode.Compressor, new List<(string, float, float, float, float)> {
                            ("Natural Voc", -20f, 3f, 20f, 100f),
                            ("Punchy Voc", -18f, 4f, 5f, 80f),
                            ("Squash Voc", -30f, 8f, 2f, 150f),
                            ("De-Esser", -24f, 6f, 1f, 50f),
                            ("Slow Level", -22f, 2.5f, 50f, 400f),
                            ("Fast Rap", -18f, 4f, 2f, 60f),
                            ("Background", -25f, 5f, 10f, 200f),
                            ("Parallel", -40f, 10f, 0.5f, 100f)
                        }},
                        { UnifiedDynamics.DynamicsMode.Limiter, new List<(string, float, float, float, float)> {
                            ("Safe Voc", -1.0f, 20f, 2f, 100f),
                            ("Loud Voc", -0.1f, 20f, 1f, 50f),
                            ("Brickwall", 0.0f, 20f, 0.1f, 20f),
                            ("Clip Voc", -3.0f, 20f, 0.1f, 10f),
                            ("Soft Knee", -2.0f, 10f, 5f, 150f),
                            ("Broadcast", -2.0f, 20f, 1f, 100f),
                            ("Sustain", -6.0f, 20f, 5f, 500f),
                            ("Peak Catch", -0.5f, 20f, 0.1f, 10f)
                        }},
                        { UnifiedDynamics.DynamicsMode.Gate, new List<(string, float, float, float, float)> {
                            ("Breath Rem", -40f, 10f, 5f, 200f),
                            ("Tight Voc", -30f, 20f, 1f, 50f),
                            ("De-Bleed", -50f, 5f, 10f, 300f),
                            ("Fast Gate", -35f, 20f, 0.1f, 10f),
                            ("Slow Gate", -45f, 5f, 20f, 500f),
                            ("Noise Flr", -60f, 4f, 10f, 100f),
                            ("Stutter", -20f, 20f, 1f, 20f),
                            ("Clean Up", -40f, 2f, 10f, 150f)
                        }},
                        { UnifiedDynamics.DynamicsMode.Expander, new List<(string, float, float, float, float)> {
                            ("Clean Voc", -30f, 2f, 10f, 100f),
                            ("Soft Exp", -40f, 1.5f, 20f, 200f),
                            ("Range", -25f, 4f, 5f, 80f),
                            ("Dynamic", -50f, 1.2f, 50f, 500f),
                            ("Hard Exp", -35f, 8f, 2f, 50f),
                            ("Downward", -30f, 2f, 5f, 150f),
                            ("Upward Sim", -40f, 1.1f, 100f, 100f),
                            ("Room Redux", -45f, 3f, 5f, 120f)
                        }}
                    };

                    if (presets.TryGetValue(dynamics.Mode, out var modePresets))
                    {
                        foreach (var p in modePresets)
                        {
                            var btn = new Button(() => {
                                dynamics.Threshold = p.Th;
                                dynamics.Ratio = p.Ratio;
                                dynamics.Attack = p.Atk;
                                dynamics.Release = p.Rel;
                                
                                EditorUtility.SetDirty(_chain);
                                DisplayEffect(dynamics); // Full refresh to update all knobs
                                OnEffectChanged?.Invoke();
                            });
                            btn.text = p.Name;
                            btn.AddToClassList("dynamics-preset-btn");
                            presetGrid.Add(btn);
                        }
                    }

                    // Mode buttons
                    var modeButtons = new VisualElement();
                    modeButtons.AddToClassList("mode-buttons");
                    visualizer.Add(modeButtons);

                    foreach (UnifiedDynamics.DynamicsMode mode in Enum.GetValues(typeof(UnifiedDynamics.DynamicsMode)))
                    {
                        var btn = new Button(() =>
                        {
                            dynamics.Mode = mode;
                            EditorUtility.SetDirty(_chain);
                            OnEffectChanged?.Invoke();
                            DisplayEffect(dynamics);
                        });
                        btn.text = mode.ToString().ToUpper();
                        btn.AddToClassList("mode-btn");
                        if (dynamics.Mode == mode) btn.AddToClassList("selected");
                        modeButtons.Add(btn);
                    }
                },
                
                // Right Zone Builder
                (rightZone) =>
                {
                    // Knee
                    if (dynamics.Mode != UnifiedDynamics.DynamicsMode.Limiter &&
                        dynamics.Mode != UnifiedDynamics.DynamicsMode.Gate)
                    {
                        var kneeKnob = CreateKnob("Knee", dynamics.Knee, 0f, 24f, "dB", 45,
                            v => { dynamics.Knee = v; EditorUtility.SetDirty(_chain); OnEffectChanged?.Invoke(); });
                        rightZone.Add(kneeKnob);
                    }

                    // Main knob (Makeup/Ceiling/Range)
                    if (dynamics.Mode == UnifiedDynamics.DynamicsMode.Compressor)
                    {
                        var mainKnobContainer = new VisualElement();
                        mainKnobContainer.AddToClassList("main-knob-container");
                        var makeupKnob = CreateKnob("Makeup", dynamics.MakeupGain, 0f, 24f, "dB", 70,
                            v => { dynamics.MakeupGain = v; EditorUtility.SetDirty(_chain); OnEffectChanged?.Invoke(); });
                        mainKnobContainer.Add(makeupKnob);
                        rightZone.Add(mainKnobContainer);
                    }
                    else if (dynamics.Mode == UnifiedDynamics.DynamicsMode.Limiter)
                    {
                        var ceilingKnob = CreateKnob("Ceiling", dynamics.Ceiling, -12f, 0f, "dB", 70,
                            v => { dynamics.Ceiling = v; EditorUtility.SetDirty(_chain); OnEffectChanged?.Invoke(); });
                        rightZone.Add(ceilingKnob);
                    }
                    else if (dynamics.Mode == UnifiedDynamics.DynamicsMode.Gate ||
                             dynamics.Mode == UnifiedDynamics.DynamicsMode.Expander)
                    {
                        var rangeKnob = CreateKnob("Range", dynamics.Range, -80f, 0f, "dB", 60,
                            v => { dynamics.Range = v; EditorUtility.SetDirty(_chain); OnEffectChanged?.Invoke(); });
                        rightZone.Add(rangeKnob);
                    }

                    // Meter display
                    var meterBox = new VisualElement();
                    meterBox.style.width = Length.Percent(100);
                    meterBox.style.marginTop = 8;
                    rightZone.Add(meterBox);

                    meterBox.Add(CreateMeterRow("Input",
                        () => Mathf.Clamp(dynamics.CurrentInput, -60f, 0f),
                        () => $"{dynamics.CurrentInput:F1}dB", -60f, 0f));
                    meterBox.Add(CreateMeterRow("GR",
                        () => Mathf.Clamp(-dynamics.CurrentGainReduction, 0f, 30f),
                        () => $"{dynamics.CurrentGainReduction:F1}dB", 0f, 30f));
                    meterBox.Add(CreateMeterRow("Output",
                        () => Mathf.Clamp(dynamics.CurrentOutput, -60f, 0f),
                        () => $"{dynamics.CurrentOutput:F1}dB", -60f, 0f));
                        
                    // Exclude meters from generic list if used
                    excluded.Add(nameof(UnifiedDynamics.CurrentInput));
                    excluded.Add(nameof(UnifiedDynamics.CurrentOutput));
                    excluded.Add(nameof(UnifiedDynamics.CurrentGainReduction));
                }
            );
        }

        private void ApplyUnifiedDynamicsExclusions(UnifiedDynamics dynamics, HashSet<string> excluded)
        {
            excluded.Add(nameof(UnifiedDynamics.Mode));
            excluded.Add(nameof(UnifiedDynamics.AutoMakeup));
            excluded.Add(nameof(UnifiedDynamics.SidechainHPF));
            excluded.Add(nameof(UnifiedDynamics.Hold));
            excluded.Add(nameof(UnifiedDynamics.Lookahead));
            excluded.Add(nameof(UnifiedDynamics.TruePeak));
            excluded.Add(nameof(UnifiedDynamics.CurrentInput));
            excluded.Add(nameof(UnifiedDynamics.CurrentOutput));
            excluded.Add(nameof(UnifiedDynamics.CurrentGainReduction));
            excluded.Add(nameof(DSPEffectBase.Mix));

            switch (dynamics.Mode)
            {
                case UnifiedDynamics.DynamicsMode.Limiter:
                    excluded.Add(nameof(UnifiedDynamics.Ratio));
                    excluded.Add(nameof(UnifiedDynamics.Knee));
                    excluded.Add(nameof(UnifiedDynamics.MakeupGain));
                    excluded.Add(nameof(UnifiedDynamics.Range));
                    break;
                case UnifiedDynamics.DynamicsMode.Gate:
                    excluded.Add(nameof(UnifiedDynamics.Ratio));
                    excluded.Add(nameof(UnifiedDynamics.Knee));
                    excluded.Add(nameof(UnifiedDynamics.MakeupGain));
                    excluded.Add(nameof(UnifiedDynamics.Ceiling));
                    break;
                case UnifiedDynamics.DynamicsMode.Expander:
                    excluded.Add(nameof(UnifiedDynamics.MakeupGain));
                    excluded.Add(nameof(UnifiedDynamics.Ceiling));
                    excluded.Add(nameof(UnifiedDynamics.Range));
                    break;
                default:
                    excluded.Add(nameof(UnifiedDynamics.Ceiling));
                    excluded.Add(nameof(UnifiedDynamics.Range));
                    break;
            }
        }

        private void BuildUnifiedDynamicsMeterUI(UnifiedDynamics dynamics, VisualElement meterContainer, HashSet<string> excluded)
        {
            if (meterContainer == null) return;

            meterContainer.Add(CreateMeterRow(
                "Input",
                () => Mathf.Clamp(dynamics.CurrentInput, -60f, 0f),
                () => $"{dynamics.CurrentInput:F1} dB",
                -60f,
                0f));
            meterContainer.Add(CreateMeterRow(
                "Gain Reduction",
                () => Mathf.Clamp(-dynamics.CurrentGainReduction, 0f, 30f),
                () => $"{dynamics.CurrentGainReduction:F1} dB",
                0f,
                30f));
            meterContainer.Add(CreateMeterRow(
                "Output",
                () => Mathf.Clamp(dynamics.CurrentOutput, -60f, 0f),
                () => $"{dynamics.CurrentOutput:F1} dB",
                -60f,
                0f));

            excluded.Add(nameof(UnifiedDynamics.CurrentInput));
            excluded.Add(nameof(UnifiedDynamics.CurrentOutput));
            excluded.Add(nameof(UnifiedDynamics.CurrentGainReduction));
        }

        private void BuildCompressorUI(CompressorEffect compressor, VisualElement meterContainer, HashSet<string> excluded)
        {
            if (meterContainer == null) return;
            meterContainer.Add(CreateMeterRow(
                "Input",
                () => compressor.CurrentInput,
                () => $"{compressor.CurrentInput:F1} dB",
                -60f,
                0f));
            meterContainer.Add(CreateMeterRow(
                "Gain Reduction",
                () => Mathf.Clamp(-compressor.CurrentGainReduction, 0f, 30f),
                () => $"{compressor.CurrentGainReduction:F1} dB",
                0f,
                30f));
            meterContainer.Add(CreateMeterRow(
                "Output",
                () => compressor.CurrentOutput,
                () => $"{compressor.CurrentOutput:F1} dB",
                -60f,
                0f));

            excluded.Add(nameof(CompressorEffect.CurrentInput));
            excluded.Add(nameof(CompressorEffect.CurrentOutput));
            excluded.Add(nameof(CompressorEffect.CurrentGainReduction));
        }
    }
}
