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
        internal void BuildPitchShiftUI(WSOLAPitchShift pitch, HashSet<string> excluded)

        {

            excluded.Add(nameof(WSOLAPitchShift.Semitones));

            excluded.Add(nameof(WSOLAPitchShift.FormantPreservation));

            excluded.Add(nameof(WSOLAPitchShift.Mix));



            var viz = new PitchVisualizer { PitchSemitones = pitch.Semitones };



            BuildStandard3ZoneUI(pitch, "PITCH SHIFTER", null,
                left => {
                    left.Add(CreateKnob("Pitch", pitch.Semitones, -12f, 12f, "st", 75, v => { pitch.Semitones = v; viz.PitchSemitones = v; viz.MarkDirtyRepaint(); NotifyChange(); }));
                },
                center => {
                    viz.style.flexGrow = 1;
                    viz.style.minHeight = 80;
                    center.Add(viz);
                    BuildCenterPresetUI(center, pitch);
                },
                right => {
                    var formantToggle = new Toggle("Formant") { value = pitch.FormantPreservation > 0.5f };
                    formantToggle.AddToClassList("param-toggle"); 
                    formantToggle.style.marginTop = 8;
                    formantToggle.style.marginBottom = 8;
                    formantToggle.RegisterValueChangedCallback(e => { pitch.FormantPreservation = e.newValue ? 1f : 0f; NotifyChange(); });
                    right.Add(formantToggle);
                    
                    right.Add(CreateKnob("Mix", pitch.Mix * 100f, 0f, 100f, "%", 75, v => { pitch.Mix = v / 100f; NotifyChange(); }));
                }
            );

        }
    }
}
