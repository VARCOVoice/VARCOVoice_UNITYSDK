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
        internal void BuildPitchShiftUI(PitchShift pitch, HashSet<string> excluded)
        {
            excluded.Add(nameof(PitchShift.Pitch));
            excluded.Add(nameof(PitchShift.FineTune));
            excluded.Add(nameof(PitchShift.Mix));
            // Jitter & Overlap removed
            excluded.Add(nameof(PitchShift.GrainSize));
            excluded.Add(nameof(PitchShift.Spread));

            var viz = new PitchVisualizer { PitchSemitones = pitch.Pitch };

            BuildStandard3ZoneUI(pitch, "PITCH SHIFT", null,
                left => {
                    left.Add(CreateKnob("Pitch", pitch.Pitch, -12f, 12f, "st", 75, v => { pitch.Pitch = v; viz.PitchSemitones = v; viz.MarkDirtyRepaint(); NotifyChange(); }));
                    // Jitter removed
                },
                center => {
                    viz.style.flexGrow = 1;
                    viz.style.minHeight = 80;
                    center.Add(viz);
                    BuildCenterPresetUI(center, pitch);
                },
                right => {
                    right.Add(CreateKnob("Fine Tune", pitch.FineTune * 100f, 50f, 200f, "%", 75, v => { pitch.FineTune = v / 100f; NotifyChange(); }));
                    right.Add(CreateKnob("Width", pitch.Spread * 100f, 0f, 100f, "%", 60, v => { pitch.Spread = v / 100f; NotifyChange(); }));
                    right.Add(CreateKnob("Mix", pitch.Mix * 100f, 0f, 100f, "%", 75, v => { pitch.Mix = v / 100f; NotifyChange(); }));
                }
            );
        }
    }
}
