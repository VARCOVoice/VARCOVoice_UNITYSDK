using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using VARCOVoice.DSP;
using VARCOVoice.Editor.UI.Components;
using VARCOVoice.Editor.UI;

namespace VARCOVoice.Editor
{
    public partial class EffectDetailController
    {
        internal void BuildDelayUI(UnifiedDelay delay, HashSet<string> excluded)
        {
            excluded.Add(nameof(UnifiedDelay.Time));
            excluded.Add(nameof(UnifiedDelay.Feedback));
            excluded.Add(nameof(UnifiedDelay.Mix));
            excluded.Add(nameof(UnifiedDelay.FilterLow));
            excluded.Add(nameof(UnifiedDelay.FilterHigh));

            // Create Visualizer
            const float feedbackMax = 0.95f;
            float feedbackPercent = Mathf.Clamp01(delay.Feedback / feedbackMax) * 100f;
            var viz = new DelayVisualizer { TimeMs = delay.Time, Feedback = feedbackPercent };

            BuildStandard3ZoneUI(delay, "DELAY", null,
                left => {
                    left.Add(CreateKnob("Time", delay.Time, 10f, 2000f, "ms", 75, v => { delay.Time = v; viz.TimeMs = v; viz.MarkDirtyRepaint(); NotifyChange(); }));
                    // UI uses percent, DSP uses 0..0.95 feedback
                    left.Add(CreateKnob("Fdbk", feedbackPercent, 0f, 100f, "%", 75, v => {
                        float clampedPercent = Mathf.Clamp(v, 0f, 100f);
                        delay.Feedback = (clampedPercent / 100f) * feedbackMax;
                        viz.Feedback = clampedPercent;
                        viz.MarkDirtyRepaint();
                        NotifyChange();
                    }));
                },
                center => {
                    viz.style.flexGrow = 1;
                    viz.style.minHeight = 80;
                    center.Add(viz);
                    BuildCenterPresetUI(center, delay);
                },
                right => {
                    right.Add(CreateKnob("Low Cut", delay.FilterLow, 20f, 2000f, "Hz", 60, v => { delay.FilterLow = v; NotifyChange(); }));
                    right.Add(CreateKnob("High Cut", delay.FilterHigh, 200f, 20000f, "Hz", 60, v => { delay.FilterHigh = v; NotifyChange(); }));
                    
                    var mixContainer = new VisualElement();
                    mixContainer.AddToClassList("main-knob-container");
                    mixContainer.style.borderTopWidth = 0;
                    mixContainer.style.borderBottomWidth = 0;
                    mixContainer.style.borderLeftWidth = 0;
                    mixContainer.style.borderRightWidth = 0;
                    mixContainer.style.backgroundColor = StyleKeyword.Initial;

                    // Fix Mix scaling (0-100 UI -> 0-1 DSP)
                    mixContainer.Add(CreateKnob("Mix", delay.Mix * 100f, 0f, 100f, "%", 70, v => { delay.Mix = v / 100f; NotifyChange(); }));
                    right.Add(mixContainer);
                }
            );
        }

        private void ApplyUnifiedDelayExclusions(UnifiedDelay delay, HashSet<string> excluded)
        {
            excluded.Add(nameof(UnifiedDelay.Mode));
            excluded.Add(nameof(UnifiedDelay.CrossFeedback));
            excluded.Add(nameof(UnifiedDelay.TapDecay));

            switch (delay.Mode)
            {
                case UnifiedDelay.DelayMode.PingPong:
                    excluded.Add(nameof(UnifiedDelay.FilterLow));
                    excluded.Add(nameof(UnifiedDelay.FilterHigh));
                    excluded.Add(nameof(UnifiedDelay.TapCount));
                    excluded.Add(nameof(UnifiedDelay.TapSpacing));
                    excluded.Add(nameof(UnifiedDelay.ModRate));
                    excluded.Add(nameof(UnifiedDelay.ModDepth));
                    break;
                case UnifiedDelay.DelayMode.MultiTap:
                    excluded.Add(nameof(UnifiedDelay.Width));
                    excluded.Add(nameof(UnifiedDelay.FilterLow));
                    excluded.Add(nameof(UnifiedDelay.FilterHigh));
                    excluded.Add(nameof(UnifiedDelay.ModRate));
                    excluded.Add(nameof(UnifiedDelay.ModDepth));
                    break;
                case UnifiedDelay.DelayMode.Tape:
                    excluded.Add(nameof(UnifiedDelay.Width));
                    excluded.Add(nameof(UnifiedDelay.TapCount));
                    excluded.Add(nameof(UnifiedDelay.TapSpacing));
                    break;
                default:
                    excluded.Add(nameof(UnifiedDelay.Width));
                    excluded.Add(nameof(UnifiedDelay.TapCount));
                    excluded.Add(nameof(UnifiedDelay.TapSpacing));
                    excluded.Add(nameof(UnifiedDelay.ModRate));
                    excluded.Add(nameof(UnifiedDelay.ModDepth));
                    break;
            }
        }
    }
}
