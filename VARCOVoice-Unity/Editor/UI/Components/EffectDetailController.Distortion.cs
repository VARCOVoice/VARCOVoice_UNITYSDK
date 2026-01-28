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
        /// <summary>
        /// UI for DistortionEffect
        /// </summary>
        internal void BuildDistortionUI(DistortionEffect distortion, HashSet<string> excluded)
        {
            excluded.Add(nameof(DistortionEffect.Type));
            excluded.Add(nameof(DistortionEffect.Drive));
            excluded.Add(nameof(DistortionEffect.Tone));
            excluded.Add(nameof(DistortionEffect.OutputGain));
            excluded.Add(nameof(DistortionEffect.Mix));
            excluded.Add(nameof(DistortionEffect.BitDepth));
            excluded.Add(nameof(DistortionEffect.SampleRateReduction));

            var viz = new DistortionVisualizer { Type = distortion.Type, Drive = distortion.Drive };

            BuildStandard3ZoneUI(distortion, "DISTORTION", null,
                left =>
                {
                    // Type dropdown
                    var typeContainer = new VisualElement();
                    typeContainer.style.marginBottom = 8;
                    typeContainer.style.alignItems = Align.Center;

                    var typeLabel = new Label("TYPE");
                    typeLabel.style.fontSize = 10;
                    typeLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
                    typeLabel.style.marginBottom = 4;
                    typeContainer.Add(typeLabel);

                    var typeDropdown = new DropdownField();
                    typeDropdown.choices = new System.Collections.Generic.List<string>
                    {
                        "Soft Clip", "Hard Clip", "Tube", "Tape", "Fuzz", "Bitcrusher"
                    };
                    typeDropdown.index = (int)distortion.Type;
                    typeDropdown.style.width = 90;
                    typeDropdown.RegisterValueChangedCallback(evt =>
                    {
                        distortion.Type = (DistortionType)typeDropdown.index;
                        viz.Type = distortion.Type;
                        viz.MarkDirtyRepaint();
                        NotifyChange();
                        // Refresh UI if switching to/from Bitcrusher
                        DisplayEffect(distortion);
                    });
                    typeContainer.Add(typeDropdown);
                    left.Add(typeContainer);

                    left.Add(CreateKnob("Drive", distortion.Drive, 0f, 100f, "", 75, v =>
                    {
                        distortion.Drive = v;
                        viz.Drive = v;
                        viz.MarkDirtyRepaint();
                        NotifyChange();
                    }));

                    left.Add(CreateKnob("Tone", distortion.Tone, 200f, 20000f, "Hz", 75, v =>
                    {
                        distortion.Tone = v;
                        NotifyChange();
                    }));
                },
                center =>
                {
                    viz.style.flexGrow = 1;
                    viz.style.minHeight = 80;
                    center.Add(viz);

                    // Show Bitcrusher controls in center if type is Bitcrusher
                    if (distortion.Type == DistortionType.Bitcrusher)
                    {
                        var bitcrushContainer = new VisualElement();
                        bitcrushContainer.style.flexDirection = FlexDirection.Row;
                        bitcrushContainer.style.justifyContent = Justify.Center;
                        bitcrushContainer.style.marginTop = 8;

                        bitcrushContainer.Add(CreateKnob("Bits", distortion.BitDepth, 1f, 16f, "", 55, v =>
                        {
                            distortion.BitDepth = (int)v;
                            NotifyChange();
                        }));
                        bitcrushContainer.Add(CreateKnob("SR÷", distortion.SampleRateReduction, 1f, 32f, "", 55, v =>
                        {
                            distortion.SampleRateReduction = (int)v;
                            NotifyChange();
                        }));
                        center.Add(bitcrushContainer);
                    }

                    BuildCenterPresetUI(center, distortion);
                },
                right =>
                {
                    right.Add(CreateKnob("Out", distortion.OutputGain, -36f, 12f, "dB", 60, v =>
                    {
                        distortion.OutputGain = v;
                        NotifyChange();
                    }));

                    var mixContainer = new VisualElement();
                    mixContainer.AddToClassList("main-knob-container");
                    mixContainer.style.borderTopWidth = 0;
                    mixContainer.style.borderBottomWidth = 0;
                    mixContainer.style.borderLeftWidth = 0;
                    mixContainer.style.borderRightWidth = 0;
                    mixContainer.style.backgroundColor = StyleKeyword.Initial;
                    mixContainer.Add(CreateKnob("Mix", distortion.Mix * 100f, 0f, 100f, "%", 70, v =>
                    {
                        distortion.Mix = v / 100f;
                        NotifyChange();
                    }));
                    right.Add(mixContainer);
                }
            );
        }

        /// <summary>
        /// UI for SaturationEffect
        /// </summary>
        internal void BuildSaturationUI(SaturationEffect saturation, HashSet<string> excluded)
        {
            excluded.Add(nameof(SaturationEffect.Amount));
            excluded.Add(nameof(SaturationEffect.Character));
            excluded.Add(nameof(SaturationEffect.Presence));
            excluded.Add(nameof(SaturationEffect.Mix));

            var viz = new SaturationVisualizer { Amount = saturation.Amount, Character = saturation.Character };

            BuildStandard3ZoneUI(saturation, "SATURATION", null,
                left =>
                {
                    left.Add(CreateKnob("Amount", saturation.Amount, 0f, 100f, "", 75, v =>
                    {
                        saturation.Amount = v;
                        viz.Amount = v;
                        viz.MarkDirtyRepaint();
                        NotifyChange();
                    }));

                    left.Add(CreateKnob("Char", saturation.Character, 0f, 1f, "", 75, v =>
                    {
                        saturation.Character = v;
                        viz.Character = v;
                        viz.MarkDirtyRepaint();
                        NotifyChange();
                    }));
                },
                center =>
                {
                    viz.style.flexGrow = 1;
                    viz.style.minHeight = 80;
                    center.Add(viz);
                    BuildCenterPresetUI(center, saturation);
                },
                right =>
                {
                    right.Add(CreateKnob("Pres", saturation.Presence, -12f, 12f, "dB", 60, v =>
                    {
                        saturation.Presence = v;
                        NotifyChange();
                    }));

                    var mixContainer = new VisualElement();
                    mixContainer.AddToClassList("main-knob-container");
                    mixContainer.style.borderTopWidth = 0;
                    mixContainer.style.borderBottomWidth = 0;
                    mixContainer.style.borderLeftWidth = 0;
                    mixContainer.style.borderRightWidth = 0;
                    mixContainer.style.backgroundColor = StyleKeyword.Initial;
                    mixContainer.Add(CreateKnob("Mix", saturation.Mix * 100f, 0f, 100f, "%", 70, v =>
                    {
                        saturation.Mix = v / 100f;
                        NotifyChange();
                    }));
                    right.Add(mixContainer);
                }
            );
        }

        /// <summary>
        /// UI for TapeEmulation
        /// </summary>
        internal void BuildTapeUI(TapeEmulation tape, HashSet<string> excluded)
        {
            excluded.Add(nameof(TapeEmulation.InputDrive));
            excluded.Add(nameof(TapeEmulation.Saturation));
            excluded.Add(nameof(TapeEmulation.Speed));
            excluded.Add(nameof(TapeEmulation.HeadBump));
            excluded.Add(nameof(TapeEmulation.HighRolloff));
            excluded.Add(nameof(TapeEmulation.Wow));
            excluded.Add(nameof(TapeEmulation.Flutter));
            excluded.Add(nameof(TapeEmulation.Hiss));
            excluded.Add(nameof(TapeEmulation.OutputLevel));
            excluded.Add(nameof(TapeEmulation.Bias));
            excluded.Add(nameof(TapeEmulation.Mix));

            var viz = new TapeVisualizer { Saturation = tape.Saturation, Wow = tape.Wow, Flutter = tape.Flutter };

            BuildStandard3ZoneUI(tape, "TAPE EMULATION", null,
                left =>
                {
                    left.Add(CreateKnob("Input", tape.InputDrive, -12f, 24f, "dB", 75, v =>
                    {
                        tape.InputDrive = v;
                        NotifyChange();
                    }));

                    left.Add(CreateKnob("Sat", tape.Saturation, 0f, 1f, "", 75, v =>
                    {
                        tape.Saturation = v;
                        viz.Saturation = v;
                        viz.MarkDirtyRepaint();
                        NotifyChange();
                    }));

                    // Speed dropdown at bottom
                    var speedContainer = new VisualElement();
                    speedContainer.style.position = Position.Absolute;
                    speedContainer.style.bottom = 12;
                    speedContainer.style.left = 0;
                    speedContainer.style.right = 0;
                    speedContainer.style.alignItems = Align.Center;

                    var speedLabel = new Label("SPEED");
                    speedLabel.style.fontSize = 10;
                    speedLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
                    speedLabel.style.marginBottom = 4;
                    speedContainer.Add(speedLabel);

                    var speedDropdown = new DropdownField();
                    speedDropdown.choices = new System.Collections.Generic.List<string> { "7.5 IPS", "15 IPS", "30 IPS" };
                    speedDropdown.index = (int)tape.Speed;
                    speedDropdown.style.width = 70;
                    speedDropdown.RegisterValueChangedCallback(evt =>
                    {
                        tape.Speed = (TapeSpeed)speedDropdown.index;
                        NotifyChange();
                    });
                    speedContainer.Add(speedDropdown);
                    left.Add(speedContainer);
                },
                center =>
                {
                    viz.style.flexGrow = 1;
                    viz.style.minHeight = 80;
                    center.Add(viz);

                    // Wow & Flutter row
                    var wfContainer = new VisualElement();
                    wfContainer.style.flexDirection = FlexDirection.Row;
                    wfContainer.style.justifyContent = Justify.Center;
                    wfContainer.style.marginTop = 4;

                    wfContainer.Add(CreateKnob("Wow", tape.Wow, 0f, 1f, "", 50, v =>
                    {
                        tape.Wow = v;
                        viz.Wow = v;
                        viz.MarkDirtyRepaint();
                        NotifyChange();
                    }));
                    wfContainer.Add(CreateKnob("Flut", tape.Flutter, 0f, 1f, "", 50, v =>
                    {
                        tape.Flutter = v;
                        viz.Flutter = v;
                        viz.MarkDirtyRepaint();
                        NotifyChange();
                    }));
                    center.Add(wfContainer);

                    BuildCenterPresetUI(center, tape);
                },
                right =>
                {
                    right.Add(CreateKnob("Bump", tape.HeadBump, 0f, 1f, "", 55, v =>
                    {
                        tape.HeadBump = v;
                        NotifyChange();
                    }));
                    right.Add(CreateKnob("HF", tape.HighRolloff, 0f, 1f, "", 55, v =>
                    {
                        tape.HighRolloff = v;
                        NotifyChange();
                    }));
                    right.Add(CreateKnob("Hiss", tape.Hiss, 0f, 1f, "", 55, v =>
                    {
                        tape.Hiss = v;
                        NotifyChange();
                    }));
                    right.Add(CreateKnob("Out", tape.OutputLevel, -12f, 12f, "dB", 55, v =>
                    {
                        tape.OutputLevel = v;
                        NotifyChange();
                    }));

                    var mixContainer = new VisualElement();
                    mixContainer.AddToClassList("main-knob-container");
                    mixContainer.style.borderTopWidth = 0;
                    mixContainer.style.borderBottomWidth = 0;
                    mixContainer.style.borderLeftWidth = 0;
                    mixContainer.style.borderRightWidth = 0;
                    mixContainer.style.backgroundColor = StyleKeyword.Initial;
                    mixContainer.Add(CreateKnob("Mix", tape.Mix * 100f, 0f, 100f, "%", 60, v =>
                    {
                        tape.Mix = v / 100f;
                        NotifyChange();
                    }));
                    right.Add(mixContainer);
                }
            );
        }

        /// <summary>
        /// UI for TremoloEffect
        /// </summary>
        internal void BuildTremoloUI(TremoloEffect tremolo, HashSet<string> excluded)
        {
            excluded.Add(nameof(TremoloEffect.Rate));
            excluded.Add(nameof(TremoloEffect.Depth));
            excluded.Add(nameof(TremoloEffect.Waveform));
            excluded.Add(nameof(TremoloEffect.StereoPhase));
            excluded.Add(nameof(TremoloEffect.Mix));

            var viz = new LFOVisualizer { Rate = tremolo.Rate, Depth = tremolo.Depth };

            BuildStandard3ZoneUI(tremolo, "TREMOLO", null,
                left =>
                {
                    left.Add(CreateKnob("Rate", tremolo.Rate, 0.1f, 20f, "Hz", 75, v =>
                    {
                        tremolo.Rate = v;
                        viz.Rate = v;
                        viz.MarkDirtyRepaint();
                        NotifyChange();
                    }));

                    left.Add(CreateKnob("Depth", tremolo.Depth, 0f, 100f, "%", 75, v =>
                    {
                        tremolo.Depth = v;
                        viz.Depth = v;
                        viz.MarkDirtyRepaint();
                        NotifyChange();
                    }));
                },
                center =>
                {
                    viz.style.flexGrow = 1;
                    viz.style.minHeight = 80;
                    center.Add(viz);

                    // Waveform selector
                    var waveContainer = new VisualElement();
                    waveContainer.style.flexDirection = FlexDirection.Row;
                    waveContainer.style.justifyContent = Justify.Center;
                    waveContainer.style.marginTop = 8;

                    var waveLabel = new Label("Wave:");
                    waveLabel.style.marginRight = 8;
                    waveLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
                    waveContainer.Add(waveLabel);

                    var waveDropdown = new DropdownField();
                    waveDropdown.choices = new System.Collections.Generic.List<string> { "Sine", "Triangle", "Square", "Sawtooth" };
                    waveDropdown.index = (int)tremolo.Waveform;
                    waveDropdown.style.width = 90;
                    waveDropdown.RegisterValueChangedCallback(evt =>
                    {
                        tremolo.Waveform = (LFOWaveform)waveDropdown.index;
                        NotifyChange();
                    });
                    waveContainer.Add(waveDropdown);
                    center.Add(waveContainer);

                    BuildCenterPresetUI(center, tremolo);
                },
                right =>
                {
                    right.Add(CreateKnob("Phase", tremolo.StereoPhase, 0f, 180f, "°", 60, v =>
                    {
                        tremolo.StereoPhase = v;
                        NotifyChange();
                    }));

                    var mixContainer = new VisualElement();
                    mixContainer.AddToClassList("main-knob-container");
                    mixContainer.style.borderTopWidth = 0;
                    mixContainer.style.borderBottomWidth = 0;
                    mixContainer.style.borderLeftWidth = 0;
                    mixContainer.style.borderRightWidth = 0;
                    mixContainer.style.backgroundColor = StyleKeyword.Initial;
                    mixContainer.Add(CreateKnob("Mix", tremolo.Mix * 100f, 0f, 100f, "%", 70, v =>
                    {
                        tremolo.Mix = v / 100f;
                        NotifyChange();
                    }));
                    right.Add(mixContainer);
                }
            );
        }

        /// <summary>
        /// UI for RingModulatorEffect
        /// </summary>
        internal void BuildRingModUI(RingModulatorEffect ringMod, HashSet<string> excluded)
        {
            excluded.Add(nameof(RingModulatorEffect.Frequency));
            excluded.Add(nameof(RingModulatorEffect.Waveform));
            excluded.Add(nameof(RingModulatorEffect.Mix));

            var viz = new RingModVisualizer { Frequency = ringMod.Frequency, Waveform = ringMod.Waveform };

            BuildStandard3ZoneUI(ringMod, "RING MODULATOR", null,
                left =>
                {
                    left.Add(CreateKnob("Freq", ringMod.Frequency, 10f, 1000f, "Hz", 80, v =>
                    {
                        ringMod.Frequency = v;
                        viz.Frequency = v;
                        viz.MarkDirtyRepaint();
                        NotifyChange();
                    }));
                },
                center =>
                {
                    viz.style.flexGrow = 1;
                    viz.style.minHeight = 80;
                    center.Add(viz);

                    // Waveform selector
                    var waveContainer = new VisualElement();
                    waveContainer.style.flexDirection = FlexDirection.Row;
                    waveContainer.style.justifyContent = Justify.Center;
                    waveContainer.style.marginTop = 8;

                    var waveLabel = new Label("Carrier:");
                    waveLabel.style.marginRight = 8;
                    waveLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
                    waveContainer.Add(waveLabel);

                    var waveDropdown = new DropdownField();
                    waveDropdown.choices = new System.Collections.Generic.List<string> { "Sine", "Triangle", "Square", "Sawtooth" };
                    waveDropdown.index = (int)ringMod.Waveform;
                    waveDropdown.style.width = 90;
                    waveDropdown.RegisterValueChangedCallback(evt =>
                    {
                        ringMod.Waveform = (LFOWaveform)waveDropdown.index;
                        viz.Waveform = ringMod.Waveform;
                        viz.MarkDirtyRepaint();
                        NotifyChange();
                    });
                    waveContainer.Add(waveDropdown);
                    center.Add(waveContainer);

                    BuildCenterPresetUI(center, ringMod);
                },
                right =>
                {
                    var mixContainer = new VisualElement();
                    mixContainer.AddToClassList("main-knob-container");
                    mixContainer.style.borderTopWidth = 0;
                    mixContainer.style.borderBottomWidth = 0;
                    mixContainer.style.borderLeftWidth = 0;
                    mixContainer.style.borderRightWidth = 0;
                    mixContainer.style.backgroundColor = StyleKeyword.Initial;
                    mixContainer.Add(CreateKnob("Mix", ringMod.Mix * 100f, 0f, 100f, "%", 70, v =>
                    {
                        ringMod.Mix = v / 100f;
                        NotifyChange();
                    }));
                    right.Add(mixContainer);
                }
            );
        }
    }
}
