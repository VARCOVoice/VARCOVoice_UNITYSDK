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
        internal void BuildSpatial3DUI(Spatial3DEffect spatial, HashSet<string> excluded)
        {
            // Exclude all parameters from auto-generation
            excluded.Add(nameof(Spatial3DEffect.Pan));
            excluded.Add(nameof(Spatial3DEffect.Width));
            excluded.Add(nameof(Spatial3DEffect.Spread));
            excluded.Add(nameof(Spatial3DEffect.Distance));
            excluded.Add(nameof(Spatial3DEffect.DistanceAttenuation));
            excluded.Add(nameof(Spatial3DEffect.MinDistance));
            excluded.Add(nameof(Spatial3DEffect.MaxDistance));
            excluded.Add(nameof(Spatial3DEffect.DopplerLevel));
            excluded.Add(nameof(Spatial3DEffect.RolloffMode));
            excluded.Add(nameof(Spatial3DEffect.Mix));
            excluded.Add(nameof(Spatial3DEffect.SourcePosition));
            excluded.Add(nameof(Spatial3DEffect.ListenerPosition));
            excluded.Add(nameof(Spatial3DEffect.ListenerForward));
            excluded.Add(nameof(Spatial3DEffect.UsePositionBased));

            // Create visualizer
            var viz = new SpatialVisualizer
            {
                Pan = spatial.Pan,
                Distance = spatial.Distance,
                Width = spatial.Width,
                Spread = spatial.Spread
            };
            viz.RegisterCallback<PointerDownEvent>(_ => BeginEditSession());
            viz.RegisterCallback<PointerUpEvent>(_ => EndEditSession());
            viz.RegisterCallback<PointerCaptureOutEvent>(_ => EndEditSession());

            // Knob references for bidirectional sync
            KnobControl panKnob = null;
            KnobControl distanceKnob = null;
            KnobControl widthKnob = null;
            KnobControl spreadKnob = null;

            // Visualizer -> Knobs sync (Pan + Distance from XY pad)
            viz.OnPanDistanceChanged = (p, d) =>
            {
                spatial.Pan = p;
                spatial.Distance = d;
                panKnob?.SetValueWithoutNotify(p * 100f);
                distanceKnob?.SetValueWithoutNotify(d * 100f);
                NotifyChange();
            };

            BuildStandard3ZoneUI(spatial, "SPATIAL", null,
                left =>
                {
                    // Pan Knob (-100% to +100%)
                    panKnob = CreateKnob("Pan", spatial.Pan * 100f, -100f, 100f, "%", 60, v =>
                    {
                        spatial.Pan = v / 100f;
                        viz.Pan = spatial.Pan;
                        viz.MarkDirtyRepaint();
                        NotifyChange();
                    });
                    left.Add(panKnob);

                    // Width Knob (0% to 200%)
                    widthKnob = CreateKnob("Width", spatial.Width * 100f, 0f, 200f, "%", 60, v =>
                    {
                        spatial.Width = v / 100f;
                        viz.Width = spatial.Width;
                        viz.MarkDirtyRepaint();
                        NotifyChange();
                    });
                    left.Add(widthKnob);
                },
                center =>
                {
                    // Presets with callback to update visualizer
                    BuildCenterPresetUI(center, spatial);
                    
                    // Store viz reference for preset updates
                    var currentViz = viz;
                    var currentPanKnob = panKnob;
                    var currentDistKnob = distanceKnob;
                    var currentWidthKnob = widthKnob;
                    var currentSpreadKnob = spreadKnob;
                    
                    // Hook into preset changes (after ApplyPreset is called)
                    _onPresetApplied = () =>
                    {
                        // Sync visualizer from effect
                        currentViz.Pan = spatial.Pan;
                        currentViz.Distance = spatial.Distance;
                        currentViz.Width = spatial.Width;
                        currentViz.Spread = spatial.Spread;
                        currentViz.MarkDirtyRepaint();
                        
                        // Sync knobs
                        currentPanKnob?.SetValueWithoutNotify(spatial.Pan * 100f);
                        currentDistKnob?.SetValueWithoutNotify(spatial.Distance * 100f);
                        currentWidthKnob?.SetValueWithoutNotify(spatial.Width * 100f);
                        currentSpreadKnob?.SetValueWithoutNotify(spatial.Spread);
                    };

                    // Visualizer (full circle XY pad)
                    viz.style.flexGrow = 1;
                    viz.style.minHeight = 120;
                    viz.style.minWidth = 120;
                    viz.style.marginTop = 8;
                    center.Add(viz);
                },
                right =>
                {
                    // Distance Knob (0% to 100%)
                    distanceKnob = CreateKnob("Dist", spatial.Distance * 100f, 0f, 100f, "%", 60, v =>
                    {
                        spatial.Distance = v / 100f;
                        viz.Distance = spatial.Distance;
                        viz.MarkDirtyRepaint();
                        NotifyChange();
                    });
                    right.Add(distanceKnob);

                    // Spread Knob (0° to 360°)
                    spreadKnob = CreateKnob("Spread", spatial.Spread, 0f, 360f, "°", 60, v =>
                    {
                        spatial.Spread = v;
                        viz.Spread = v;
                        viz.MarkDirtyRepaint();
                        NotifyChange();
                    });
                    right.Add(spreadKnob);

                    // Mix Knob
                    var mixContainer = new VisualElement();
                    mixContainer.AddToClassList("main-knob-container");
                    mixContainer.style.borderTopWidth = 0;
                    mixContainer.style.borderBottomWidth = 0;
                    mixContainer.style.borderLeftWidth = 0;
                    mixContainer.style.borderRightWidth = 0;
                    mixContainer.style.backgroundColor = StyleKeyword.Initial;

                    var mixKnob = CreateKnob("Mix", spatial.Mix * 100f, 0f, 100f, "%", 70, v =>
                    {
                        spatial.Mix = v / 100f;
                        NotifyChange();
                    });
                    mixContainer.Add(mixKnob);
                    right.Add(mixContainer);
                }
            );
        }
    }
}
