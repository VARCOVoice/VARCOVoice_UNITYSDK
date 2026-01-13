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
        internal void BuildTubeUI(TubeEmulation tube, HashSet<string> excluded)

        {

            excluded.Add(nameof(TubeEmulation.Drive));

            excluded.Add(nameof(TubeEmulation.Bias));

            excluded.Add(nameof(TubeEmulation.Mix));

            excluded.Add(nameof(TubeEmulation.Output));

            excluded.Add(nameof(TubeEmulation.Presence));

            excluded.Add(nameof(TubeEmulation.Sag));



            var viz = new TubeVisualizer { Drive = tube.Drive, Bias = tube.Bias };



            BuildStandard3ZoneUI(tube, "TUBE SATURATION", null,

                left => {

                    left.Add(CreateKnob("Drive", tube.Drive, 0f, 48f, "dB", 75, v => { tube.Drive = v; viz.Drive = v; viz.MarkDirtyRepaint(); NotifyChange(); }));

                    left.Add(CreateKnob("Bias", tube.Bias, -1f, 1f, "", 75, v => { tube.Bias = v; viz.Bias = v; viz.MarkDirtyRepaint(); NotifyChange(); }));

                },

                center => {

                    center.Add(viz);

                    BuildCenterPresetUI(center, tube);

                },

                right => {

                     // Restored Controls

                    right.Add(CreateKnob("Pres", tube.Presence, 0f, 1f, "", 60, v => { tube.Presence = v; NotifyChange(); }));

                    right.Add(CreateKnob("Sag", tube.Sag, 0f, 1f, "", 60, v => { tube.Sag = v; NotifyChange(); }));



                    right.Add(CreateKnob("Out", tube.Output, -24f, 24f, "dB", 60, v => { tube.Output = v; NotifyChange(); }));

                    

                    var mixContainer = new VisualElement();

                    mixContainer.AddToClassList("main-knob-container");

                    mixContainer.style.borderTopWidth = 0;

                    mixContainer.style.borderBottomWidth = 0;

                    mixContainer.style.borderLeftWidth = 0;

                    mixContainer.style.borderRightWidth = 0;

                    mixContainer.style.backgroundColor = StyleKeyword.Initial;

                    

                    // Fix Mix scaling

                    mixContainer.Add(CreateKnob("Mix", tube.Mix * 100f, 0f, 100f, "%", 70, v => { tube.Mix = v / 100f; NotifyChange(); }));

                    right.Add(mixContainer);

                }

            );

        }
    }
}
