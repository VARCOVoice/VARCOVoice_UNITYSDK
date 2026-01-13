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
        internal void BuildReverbUI(FDNReverb reverb, HashSet<string> excluded)
        {
            excluded.Add(nameof(FDNReverb.RoomSize));
            excluded.Add(nameof(FDNReverb.DecayTime));
            excluded.Add(nameof(FDNReverb.Mix));
            excluded.Add(nameof(FDNReverb.Damping));
            excluded.Add(nameof(FDNReverb.PreDelay));

            // XY Pad: X = Size/Space, Y = Decay/Time
            var xyPad = new XYPadControl();
            xyPad.XLabel = "SIZE";
            xyPad.YLabel = "DECAY";
            xyPad.RegisterCallback<PointerDownEvent>(_ => BeginEditSession());
            xyPad.RegisterCallback<PointerUpEvent>(_ => EndEditSession());
            xyPad.RegisterCallback<PointerCaptureOutEvent>(_ => EndEditSession());
            
            // Knob references for bidirectional sync
            KnobControl sizeKnob = null;
            KnobControl decayKnob = null;
            KnobControl preDelayKnob = null;
            
            // Normalize initial values
            xyPad.XValue = Mathf.InverseLerp(1f, 100f, reverb.RoomSize);
            xyPad.YValue = Mathf.InverseLerp(0.1f, 10f, reverb.DecayTime);
            
            // XY Pad changes -> update reverb and knobs
            xyPad.OnValueChanged = (x, y) => {
                // X controls Size and derives PreDelay (bigger room = more predelay)
                float newSize = Mathf.Lerp(1f, 100f, x);
                float newPreDelay = Mathf.Lerp(0f, 100f, x); // 0-100ms based on size
                float newDecay = Mathf.Lerp(0.1f, 10f, y);
                
                reverb.RoomSize = newSize;
                reverb.PreDelay = newPreDelay;
                reverb.DecayTime = newDecay;
                
                // Update knobs if they exist
                sizeKnob?.SetValueWithoutNotify(newSize);
                decayKnob?.SetValueWithoutNotify(newDecay);
                preDelayKnob?.SetValueWithoutNotify(newPreDelay);
                
                NotifyChange();
            };

            BuildStandard3ZoneUI(reverb, "REVERB", null,
                left => {
                    sizeKnob = CreateKnob("Size", reverb.RoomSize, 1f, 100f, "m", 75, v => { 
                        reverb.RoomSize = v; 
                        xyPad.XValue = Mathf.InverseLerp(1f, 100f, v);
                        // Also update predelay to stay in sync
                        float derivedPreDelay = Mathf.Lerp(0f, 100f, xyPad.XValue);
                        reverb.PreDelay = derivedPreDelay;
                        preDelayKnob?.SetValueWithoutNotify(derivedPreDelay);
                        NotifyChange(); 
                    });
                    left.Add(sizeKnob);
                    
                    decayKnob = CreateKnob("Decay", reverb.DecayTime, 0.1f, 10f, "s", 75, v => { 
                        reverb.DecayTime = v; 
                        xyPad.YValue = Mathf.InverseLerp(0.1f, 10f, v);
                        NotifyChange(); 
                    });
                    left.Add(decayKnob);
                },
                center => {
                    // Presets on top
                    BuildCenterPresetUI(center, reverb);
                    
                    // XY Pad fills remaining space
                    xyPad.style.marginTop = 8;
                    center.Add(xyPad);
                },
                right => {
                    preDelayKnob = CreateKnob("PreDelay", reverb.PreDelay, 0f, 200f, "ms", 60, v => { 
                        reverb.PreDelay = v; 
                        // Update X based on predelay (inverse mapping for 0-100 range)
                        if (v <= 100f) {
                            xyPad.XValue = Mathf.InverseLerp(0f, 100f, v);
                            float derivedSize = Mathf.Lerp(1f, 100f, xyPad.XValue);
                            reverb.RoomSize = derivedSize;
                            sizeKnob?.SetValueWithoutNotify(derivedSize);
                        }
                        NotifyChange(); 
                    });
                    right.Add(preDelayKnob);
                    
                    right.Add(CreateKnob("Damping", reverb.Damping, 1000f, 20000f, "Hz", 60, v => { reverb.Damping = v; NotifyChange(); }));
                    
                    var mixContainer = new VisualElement();
                    mixContainer.AddToClassList("main-knob-container");
                    mixContainer.style.borderTopWidth = 0;
                    mixContainer.style.borderBottomWidth = 0;
                    mixContainer.style.borderLeftWidth = 0;
                    mixContainer.style.borderRightWidth = 0;
                    mixContainer.style.backgroundColor = StyleKeyword.Initial;
                    
                    // Fix Mix scaling
                    mixContainer.Add(CreateKnob("Mix", reverb.Mix * 100f, 0f, 100f, "%", 70, v => { reverb.Mix = v / 100f; NotifyChange(); }));
                    right.Add(mixContainer);
                }
            );
        }

        private void BuildFDNReverbUI(FDNReverb reverb, VisualElement spaceContainer, VisualElement presetContainer, HashSet<string> excluded)
        {
            if (spaceContainer != null)
            {
                spaceContainer.Add(CreateSectionHeader("Space"));
                spaceContainer.Add(CreateReverbSpacePad(reverb));
            }
            // Presets now handled by standard dropdown, no chip container needed
            excluded.Add(nameof(FDNReverb.RoomSize));
            excluded.Add(nameof(FDNReverb.DecayTime));
        }

        private VisualElement CreateReverbPresetRow(FDNReverb reverb)
        {
            var row = new VisualElement();
            row.AddToClassList("preset-row");

            AddPresetChip(row, "Small Room", "small room", reverb);
            AddPresetChip(row, "Large Hall", "large hall", reverb);
            AddPresetChip(row, "Cathedral", "cathedral", reverb);
            AddPresetChip(row, "Plate", "plate", reverb);
            AddPresetChip(row, "Ambient", "ambient", reverb);

            return row;
        }

        private void AddPresetChip(VisualElement row, string label, string presetName, FDNReverb reverb)
        {
            var button = new Button(() => ApplyPresetAndRefresh(reverb, presetName))
            {
                text = label
            };
            button.AddToClassList("preset-chip");
            row.Add(button);
        }

        private VisualElement CreateReverbSpacePad(FDNReverb reverb)
        {
            var pad = new VisualElement();
            pad.AddToClassList("reverb-pad");

            var handle = new VisualElement();
            handle.AddToClassList("reverb-pad__handle");

            var label = new Label();
            label.AddToClassList("reverb-pad__label");

            pad.Add(handle);
            pad.Add(label);

            void UpdateFromParams()
            {
                var rect = pad.contentRect;
                if (rect.width <= 0 || rect.height <= 0) return;

                float xNorm = Mathf.InverseLerp(10f, 100f, reverb.RoomSize);
                float yNorm = Mathf.InverseLerp(0.1f, 10f, reverb.DecayTime);

                float x = Mathf.Lerp(0f, rect.width, xNorm);
                float y = Mathf.Lerp(rect.height, 0f, yNorm);

                float handleSize = handle.resolvedStyle.width > 0 ? handle.resolvedStyle.width : 12f;
                float half = handleSize * 0.5f;
                handle.style.left = x - half;
                handle.style.top = y - half;

                label.text = $"Room {reverb.RoomSize:F0}m / Decay {reverb.DecayTime:F1}s";
            }

            void ApplyPointer(Vector2 localPos)
            {
                var rect = pad.contentRect;
                if (rect.width <= 0 || rect.height <= 0) return;

                float xNorm = Mathf.Clamp01(localPos.x / rect.width);
                float yNorm = Mathf.Clamp01(1f - (localPos.y / rect.height));

                reverb.RoomSize = Mathf.Lerp(10f, 100f, xNorm);
                reverb.DecayTime = Mathf.Lerp(0.1f, 10f, yNorm);

                EditorUtility.SetDirty(_chain);
                OnEffectChanged?.Invoke();
                UpdateFromParams();
            }

            pad.RegisterCallback<PointerDownEvent>(evt =>
            {
                ApplyPointer(evt.localPosition);
                pad.CapturePointer(evt.pointerId);
                evt.StopPropagation();
            });

            pad.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (pad.HasPointerCapture(evt.pointerId))
                {
                    ApplyPointer(evt.localPosition);
                }
            });

            pad.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (pad.HasPointerCapture(evt.pointerId))
                    pad.ReleasePointer(evt.pointerId);
            });

            pad.RegisterCallback<GeometryChangedEvent>(_ => UpdateFromParams());

            var updateItem = pad.schedule.Execute(UpdateFromParams).Every(200);
            _scheduledUpdates.Add(updateItem);

            return pad;
        }
    }
}
