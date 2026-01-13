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
        internal void BuildModulationUI(IDSPEffect effect, string name,

            float rate, float depth, float mix, float? feedback, float? delayMs, 

            HashSet<string> excluded)

        {

            excluded.Add("Rate");

            excluded.Add("Depth");

            excluded.Add("Mix");

            excluded.Add("StereoPhase"); // Common to all

            if (feedback.HasValue) excluded.Add("Feedback");

            if (delayMs.HasValue) excluded.Add("BaseDelay");

            if (effect is PhaserEffect) excluded.Add("Stages");



            // Create Visualizer

            var viz = new LFOVisualizer { Rate = rate, Depth = depth };



            Action<float> setRate = v => {

                if (effect is ChorusEffect c) c.Rate = v;

                else if (effect is PhaserEffect p) p.Rate = v;

                else if (effect is FlangerEffect f) f.Rate = v;

                viz.Rate = v; viz.MarkDirtyRepaint();

                NotifyChange();

            };

            Action<float> setDepth = v => { // ... (same as before) ...

                if (effect is ChorusEffect c) c.Depth = v;

                else if (effect is PhaserEffect p) p.Depth = v;

                else if (effect is FlangerEffect f) f.Depth = v;

                viz.Depth = v; viz.MarkDirtyRepaint();

                NotifyChange();

            };

            Action<float> setMix = v => { // ... (same as before) ...

                float val = v / 100f;

                if (effect is ChorusEffect c) c.Mix = val;

                else if (effect is PhaserEffect p) p.Mix = val;

                else if (effect is FlangerEffect f) f.Mix = val;

                NotifyChange();

            };

            Action<float> setFdbk = v => { // ... (same as before) ...

                 if (effect is PhaserEffect p) p.Feedback = v;

                else if (effect is FlangerEffect f) f.Feedback = v;

                NotifyChange();

            };



            BuildStandard3ZoneUI(effect, name.ToUpper(), null,

                left => {

                    left.Add(CreateKnob("Rate", rate, 0.1f, 10f, "Hz", 75, setRate));
                    left.Add(CreateKnob("Depth", depth, 0f, 100f, "%", 75, setDepth));
                    
                    // Add Stages for Phaser
                    if (effect is PhaserEffect phaser)
                    {
                        var stagesContainer = new VisualElement();
                        stagesContainer.style.position = Position.Absolute;
                        stagesContainer.style.bottom = 12;
                        stagesContainer.style.left = 0;
                        stagesContainer.style.right = 0;
                        stagesContainer.style.flexDirection = FlexDirection.Column;
                        stagesContainer.style.alignItems = Align.Center;
                        stagesContainer.style.justifyContent = Justify.Center;
                        stagesContainer.style.marginTop = 0;
                        stagesContainer.style.marginLeft = 0;
                        stagesContainer.style.marginRight = 0;

                        

                        var stagesLabel = new Label("STAGES");

                        stagesLabel.style.fontSize = 10;

                        stagesLabel.style.color = new Color(0.6f, 0.6f, 0.6f);

                        stagesLabel.style.marginBottom = 4;

                        stagesContainer.Add(stagesLabel);

                        

                        var stages = new SliderInt(2, 12); 

                        stages.value = phaser.Stages;

                        stages.RegisterValueChangedCallback(e => { phaser.Stages = e.newValue; NotifyChange(); });

                        stages.style.width = 70;

                        stages.style.minWidth = 50;

                        stages.style.maxWidth = 80;

                        stages.labelElement.style.display = DisplayStyle.None; // Hide the default label

                        stagesContainer.Add(stages);

                        

                        var stagesValue = new Label(phaser.Stages.ToString());

                        stagesValue.style.fontSize = 11;

                        stagesValue.style.color = Color.white;

                        stagesValue.style.marginTop = 2;

                        stages.RegisterValueChangedCallback(e => stagesValue.text = e.newValue.ToString());

                        stagesContainer.Add(stagesValue);

                        

                        left.Add(stagesContainer);

                    }

                },

                center => {

                    viz.style.flexGrow = 1;

                    viz.style.minHeight = 80;

                    center.Add(viz);

                    BuildCenterPresetUI(center, effect);

                },

                right => {

                    // Fix StereoPhase : Check strictly

                    float currentPhase = 0f;

                    Action<float> phaseSetter = null;



                    if (effect is PhaserEffect p) {

                        currentPhase = p.StereoPhase;

                        phaseSetter = v => { p.StereoPhase = v; NotifyChange(); };

                    }

                    else if (effect is FlangerEffect f) {

                        currentPhase = f.StereoPhase;

                        phaseSetter = v => { f.StereoPhase = v; NotifyChange(); };

                    }

                    else if (effect is ChorusEffect c) {

                        // ChorusEffect does not have StereoPhase currently. Skip it.

                    }



                    if (phaseSetter != null)

                    {

                        right.Add(CreateKnob("Phase", currentPhase, 0f, 180f, "°", 60, phaseSetter));

                    }



                    if (delayMs.HasValue)

                    {

                        Action<float> setDelay = v => { if (effect is FlangerEffect f) f.BaseDelay = v; NotifyChange(); };

                        right.Add(CreateKnob("Delay", delayMs.Value, 0.1f, 10f, "ms", 60, setDelay));

                    }

                    if (feedback.HasValue)

                    {

                         right.Add(CreateKnob("Fdbk", feedback.Value, 0f, 95f, "%", 60, setFdbk));

                    }

                    

                    var mixContainer = new VisualElement();

                    mixContainer.AddToClassList("main-knob-container");

                    mixContainer.style.borderTopWidth = 0;

                    mixContainer.style.borderBottomWidth = 0;

                    mixContainer.style.borderLeftWidth = 0;

                    mixContainer.style.borderRightWidth = 0;

                    mixContainer.style.backgroundColor = StyleKeyword.Initial;

                    mixContainer.Add(CreateKnob("Mix", mix, 0f, 100f, "%", 70, setMix));

                    right.Add(mixContainer);

                }

            );

        }
    }
}
