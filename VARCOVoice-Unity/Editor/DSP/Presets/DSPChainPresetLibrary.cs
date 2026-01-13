using System;
using System.Collections.Generic;
using VARCOVoice.DSP;

namespace VARCOVoice.Editor
{
    internal static class DSPChainPresetLibrary
    {
        internal sealed class ChainPreset
        {
            public string Category { get; }
            public string Name { get; }
            public string Description { get; }
            public IReadOnlyList<EffectEntry> Effects { get; }

            public string DisplayName => $"{Category} / {Name}";

            public ChainPreset(string category, string name, string description, IReadOnlyList<EffectEntry> effects)
            {
                Category = category;
                Name = name;
                Description = description;
                Effects = effects;
            }
        }

        internal sealed class EffectEntry
        {
            public Type EffectType { get; }
            public string PresetName { get; }
            public bool Enabled { get; }
            public float? Mix { get; }
            public Action<IDSPEffect> Configure { get; }

            public EffectEntry(Type effectType, string presetName, float? mix, bool enabled, Action<IDSPEffect> configure)
            {
                EffectType = effectType;
                PresetName = presetName;
                Mix = mix;
                Enabled = enabled;
                Configure = configure;
            }
        }

        private static EffectEntry Effect<T>(string presetName = null, float? mix = null, bool enabled = true,
            Action<IDSPEffect> configure = null) where T : IDSPEffect
        {
            return new EffectEntry(typeof(T), presetName, mix, enabled, configure);
        }

        private static ChainPreset Preset(string category, string name, string description, params EffectEntry[] effects)
        {
            return new ChainPreset(category, name, description, effects);
        }

        private static readonly List<ChainPreset> Presets = new()
        {
            // ===== VOICE PRESETS =====
            Preset(
                "Voice",
                "Broadcast Ready",
                "EQ clarity + compression + limiter for broadcast delivery.",
                Effect<ParametricEQ16>("Voice Clarity"),
                Effect<UnifiedDynamics>(configure: effect =>
                {
                    if (effect is UnifiedDynamics dynamics)
                    {
                        // Combined Compressor + Limiter settings
                        dynamics.Mode = UnifiedDynamics.DynamicsMode.Compressor;
                        dynamics.Threshold = -18f;
                        dynamics.Ratio = 6f;
                        dynamics.Attack = 2f;
                        dynamics.Release = 60f;
                        dynamics.MakeupGain = 6f;
                        dynamics.Knee = 1f;
                        dynamics.AutoMakeup = false;
                    }
                })
            ),
            Preset(
                "Voice",
                "Studio Vocal",
                "Warmth with gentle double and plate reverb.",
                Effect<ParametricEQ16>("Warmth"),
                Effect<UnifiedDynamics>("Vocal Warmth"),
                Effect<UnifiedDelay>("Vocal Double", mix: 0.2f),
                Effect<FDNReverb>("Plate", mix: 0.2f)
            ),
            Preset(
                "Voice",
                "Radio Voice",
                "Punchy broadcast tone with limiter.",
                Effect<ParametricEQ16>("Radio Voice"),
                Effect<UnifiedDynamics>(configure: effect =>
                {
                    if (effect is UnifiedDynamics dynamics)
                    {
                        dynamics.Mode = UnifiedDynamics.DynamicsMode.Compressor;
                        dynamics.Threshold = -24f;
                        dynamics.Ratio = 8f;
                        dynamics.Attack = 1f; // Fast attack for punch
                        dynamics.Release = 80f;
                        dynamics.MakeupGain = 8f;
                    }
                })
            ),
            Preset(
                "Voice",
                "Intimate Whisper",
                "Proximity control with subtle room.",
                Effect<ParametricEQ16>("Proximity Effect Fix"),
                Effect<UnifiedDynamics>("Transparent"),
                Effect<FDNReverb>("Small Room", mix: 0.2f)
            ),
            Preset(
                "Voice",
                "Epic Cinematic",
                "Airy gloss with long tail and cathedral space.",
                Effect<ParametricEQ16>("Air & Shine"),
                Effect<UnifiedDynamics>("Glue"),
                Effect<UnifiedDelay>("Long Tail"),
                Effect<FDNReverb>("Cathedral", mix: 0.4f)
            ),
            Preset(
                "Voice",
                "ASMR Binaural",
                "3D spatial positioning with intimate proximity.",
                Effect<ParametricEQ16>("Proximity Effect Fix"),
                Effect<Spatial3DEffect>(configure: effect =>
                {
                    if (effect is Spatial3DEffect spatial)
                    {
                        spatial.MinDistance = 0.5f;
                        spatial.Spread = 120f;
                        spatial.Mix = 1f;
                    }
                }),
                Effect<FDNReverb>("Small Room", mix: 0.1f)
            ),
            Preset(
                "Voice",
                "Podcast Pro",
                "Clean podcast voice with gate and subtle warmth.",
                Effect<UnifiedDynamics>(configure: effect =>
                {
                    if (effect is UnifiedDynamics dynamics)
                    {
                        dynamics.Mode = UnifiedDynamics.DynamicsMode.Gate;
                        dynamics.Threshold = -35f;
                        dynamics.Attack = 3f;
                        dynamics.Release = 80f;
                        dynamics.Range = -40f;
                    }
                }),
                Effect<ParametricEQ16>("De-Muddy"),
                Effect<UnifiedDynamics>("Podcast Master"),
                Effect<TubeEmulation>("Warm", mix: 0.2f)
            ),
            Preset(
                "Voice",
                "Vocal Thickener",
                "Layered doubling with pitch shift for thickness.",
                Effect<ParametricEQ16>("Warmth"),
                Effect<WSOLAPitchShift>("Doubler", mix: 0.25f),
                Effect<UnifiedDelay>(configure: effect =>
                {
                    if (effect is UnifiedDelay delay)
                    {
                        delay.Mode = UnifiedDelay.DelayMode.Standard;
                        delay.Time = 25f;
                        delay.Feedback = 0f;
                        delay.Mix = 0.25f;
                    }
                }),
                Effect<UnifiedDynamics>("Glue")
            ),
            Preset(
                "Voice",
                "Narrator Classic",
                "Rich warm tone for audiobook narration.",
                Effect<ParametricEQ16>("Warmth"),
                Effect<TubeEmulation>("Subtle", mix: 0.15f),
                Effect<UnifiedDynamics>("Transparent"),
                Effect<FDNReverb>("Small Room", mix: 0.08f)
            ),
            Preset(
                "Voice",
                "3D Surround Speaker",
                "Voice that moves around the listener.",
                Effect<ParametricEQ16>("Voice Clarity"),
                Effect<Spatial3DEffect>("Wide"),
                Effect<FDNReverb>("Large Hall", mix: 0.25f)
            ),
            Preset(
                "Voice",
                "Vintage Radio",
                "Retro AM radio tone with tape saturation.",
                Effect<ParametricEQ16>("Telephone"),
                Effect<TapeEmulation>("Vintage", mix: 0.5f),
                Effect<UnifiedDynamics>("Punchy"),
                Effect<FDNReverb>("Small Room", mix: 0.1f)
            ),
            Preset(
                "Voice",
                "Voice Enhancer",
                "Presence boost with harmonic excitement.",
                Effect<ParametricEQ16>("Presence"),
                Effect<SaturationEffect>("Warm", mix: 0.2f),
                Effect<UnifiedDynamics>("Vocal Warmth")
            ),
            Preset(
                "Voice",
                "Lo-Fi Podcast",
                "Characteristic lo-fi podcast aesthetic.",
                Effect<ParametricEQ16>("Radio Voice"),
                Effect<TapeEmulation>("Lofi", mix: 0.4f),
                Effect<UnifiedDynamics>("Punchy") // Removed redundant 'Loud'
            ),
            Preset(
                "Voice",
                "Deep Voice",
                "Pitch-shifted for deeper masculine tone.",
                Effect<WSOLAPitchShift>("Deep"),
                Effect<ParametricEQ16>("Warmth"),
                Effect<UnifiedDynamics>("Glue")
            ),
            Preset(
                "Voice",
                "High Voice",
                "Pitch-shifted for higher feminine tone.",
                Effect<WSOLAPitchShift>("Natural Up"),
                Effect<ParametricEQ16>("Air & Shine"),
                Effect<UnifiedDynamics>("Transparent")
            ),
            
            // ===== CREATIVE PRESETS =====
            Preset(
                "Creative",
                "Telephone",
                "Telephone bandpass with soft clip crunch.",
                Effect<ParametricEQ16>("Telephone"),
                Effect<DistortionEffect>(configure: effect =>
                {
                    if (effect is DistortionEffect distortion)
                    {
                        distortion.Type = DistortionType.SoftClip;
                        distortion.Drive = 45f;
                        distortion.Tone = 3000f;
                        distortion.Mix = 0.7f;
                    }
                }),
                Effect<UnifiedDynamics>(configure: effect =>
                {
                    if (effect is UnifiedDynamics dynamics)
                    {
                        dynamics.Mode = UnifiedDynamics.DynamicsMode.Compressor;
                        dynamics.Threshold = -20f;
                        dynamics.Ratio = 10f;
                        dynamics.Attack = 5f;
                        dynamics.Release = 120f;
                        dynamics.Knee = 2f;
                        dynamics.MakeupGain = 4f;
                        dynamics.AutoMakeup = false;
                        dynamics.SidechainHPF = 120f;
                        dynamics.Mix = 1f;
                    }
                })
            ),
            Preset(
                "Creative",
                "Robot Voice",
                "Metallic ring modulation with slapback.",
                Effect<RingModulatorEffect>(configure: effect =>
                {
                    if (effect is RingModulatorEffect ring)
                    {
                        ring.Frequency = 150f;
                        ring.Waveform = LFOWaveform.Sine;
                        ring.Mix = 0.7f;
                    }
                }),
                Effect<ParametricEQ16>("De-Muddy"),
                Effect<UnifiedDelay>("Slapback", mix: 0.2f)
            ),
            Preset(
                "Creative",
                "Psychedelic Space",
                "Wide modulation with long ambient tail.",
                Effect<PhaserEffect>("Space Phaser"),
                Effect<FlangerEffect>("Barber Pole"),
                Effect<FDNReverb>("Ambient", mix: 0.4f),
                Effect<UnifiedDelay>("Rhythmic Dotted 8th", mix: 0.25f)
            ),
            Preset(
                "Creative",
                "80s Synthwave",
                "Classic chorus + rhythmic delay + plate space.",
                Effect<ChorusEffect>("80s Synth"),
                Effect<UnifiedDelay>("Rhythmic Quarter"),
                Effect<FDNReverb>("Plate", mix: 0.3f)
            ),
            Preset(
                "Creative",
                "Underwater",
                "Lowpass wash with lush chorus and hall reverb.",
                Effect<LowPassEffect>(configure: effect =>
                {
                    if (effect is LowPassEffect lowpass)
                    {
                        lowpass.CutoffFrequency = 500f;
                        lowpass.Resonance = 0.8f;
                        lowpass.Mix = 1f;
                    }
                }),
                Effect<ChorusEffect>("Lush"),
                Effect<FDNReverb>("Large Hall", mix: 0.5f)
            ),
            Preset(
                "Creative",
                "Demon Voice",
                "Dark pitch-shifted with ring mod and distortion.",
                Effect<WSOLAPitchShift>(configure: effect =>
                {
                    if (effect is WSOLAPitchShift pitch)
                    {
                        pitch.Semitones = -12f;
                        pitch.FormantPreservation = 0.6f;
                        pitch.Mix = 0.6f;
                    }
                }),
                Effect<RingModulatorEffect>(configure: effect =>
                {
                    if (effect is RingModulatorEffect ring)
                    {
                        ring.Frequency = 50f;
                        ring.Waveform = LFOWaveform.Square;
                        ring.Mix = 0.2f;
                    }
                }),
                Effect<DistortionEffect>(configure: effect =>
                {
                    if (effect is DistortionEffect dist)
                    {
                        dist.Type = DistortionType.HardClip;
                        dist.Drive = 20f;
                        dist.Mix = 0.25f;
                    }
                }),
                Effect<FDNReverb>("Cathedral", mix: 0.3f)
            ),
            Preset(
                "Creative",
                "Chipmunk",
                "High pitch cartoon voice.",
                Effect<WSOLAPitchShift>("Chipmunk"),
                Effect<UnifiedDynamics>("Punchy")
            ),
            Preset(
                "Creative",
                "Ghost Whisper",
                "Ethereal whispered voice from beyond.",
                Effect<PhaserEffect>("Deep Sweep"),
                Effect<WSOLAPitchShift>(configure: effect =>
                {
                    if (effect is WSOLAPitchShift pitch)
                    {
                        pitch.Semitones = 5f;
                        pitch.FormantPreservation = 0.8f;
                        pitch.Mix = 0.3f;
                    }
                }),
                Effect<UnifiedDelay>(configure: effect =>
                {
                    if (effect is UnifiedDelay delay)
                    {
                        delay.Mode = UnifiedDelay.DelayMode.Standard;
                        delay.Time = 300f;
                        delay.Feedback = 0.35f;
                        delay.Mix = 0.25f;
                    }
                }),
                Effect<FDNReverb>("Cathedral", mix: 0.4f)
            ),
            Preset(
                "Creative",
                "Alien Transmission",
                "Sci-fi alien communication signal.",
                Effect<RingModulatorEffect>(configure: effect =>
                {
                    if (effect is RingModulatorEffect ring)
                    {
                        ring.Frequency = 250f;
                        ring.Waveform = LFOWaveform.Triangle;
                        ring.Mix = 0.5f;
                    }
                }),
                Effect<FlangerEffect>("Jet Plane"),
                Effect<LowPassEffect>(configure: effect =>
                {
                    if (effect is LowPassEffect lp)
                    {
                        lp.CutoffFrequency = 2000f;
                        lp.Resonance = 0.5f;
                        lp.Mix = 1f;
                    }
                }),
                Effect<UnifiedDelay>("Rhythmic Triplet", mix: 0.3f)
            ),
            Preset(
                "Creative",
                "Glitch Bot",
                "Stuttering glitchy robot voice.",
                Effect<RingModulatorEffect>(configure: effect =>
                {
                    if (effect is RingModulatorEffect ring)
                    {
                        ring.Frequency = 150f;
                        ring.Waveform = LFOWaveform.Square;
                        ring.Mix = 0.5f;
                    }
                }),
                Effect<TremoloEffect>(configure: effect =>
                {
                    if (effect is TremoloEffect trem)
                    {
                        trem.Rate = 12f;
                        trem.Depth = 60f;
                        trem.Mix = 1f;
                    }
                }),
                Effect<DistortionEffect>(configure: effect =>
                {
                    if (effect is DistortionEffect dist)
                    {
                        dist.Type = DistortionType.Bitcrusher;
                        dist.BitDepth = 8;
                        dist.Mix = 0.3f;
                    }
                })
            ),
            Preset(
                "Creative",
                "Hypnotic Spiral",
                "Swirling phaser and tremolo hypnosis.",
                Effect<PhaserEffect>("Classic Sweep"),
                Effect<TremoloEffect>(configure: effect =>
                {
                    if (effect is TremoloEffect trem)
                    {
                        trem.Rate = 4f;
                        trem.Depth = 50f;
                        trem.Mix = 1f;
                    }
                }),
                Effect<ChorusEffect>("Lush"),
                Effect<FDNReverb>("Ambient", mix: 0.4f)
            ),
            Preset(
                "Creative",
                "Massive Choir",
                "Voice multiplied into powerful choir.",
                Effect<ChorusEffect>("Huge Ensemble"),
                Effect<WSOLAPitchShift>("Doubler", mix: 0.25f),
                Effect<UnifiedDelay>(configure: effect =>
                {
                    if (effect is UnifiedDelay delay)
                    {
                        delay.Mode = UnifiedDelay.DelayMode.Standard;
                        delay.Time = 40f;
                        delay.Feedback = 0.2f;
                        delay.Mix = 0.3f;
                    }
                }),
                Effect<FDNReverb>("Cathedral", mix: 0.3f)
            ),
            Preset(
                "Creative",
                "Vocoder Synth",
                "Robotic synthesized voice effect.",
                Effect<RingModulatorEffect>(configure: effect =>
                {
                    if (effect is RingModulatorEffect ring)
                    {
                        ring.Frequency = 150f;
                        ring.Waveform = LFOWaveform.Sawtooth;
                        ring.Mix = 0.5f;
                    }
                }),
                Effect<FlangerEffect>("Metallic"),
                Effect<UnifiedDynamics>("Punchy")
            ),
            Preset(
                "Creative",
                "Dream Sequence",
                "Floaty dreamlike voice processing.",
                Effect<ChorusEffect>("Lush"),
                Effect<PhaserEffect>("Slow Sweep"),
                Effect<UnifiedDelay>(configure: effect =>
                {
                    if (effect is UnifiedDelay delay)
                    {
                        delay.Mode = UnifiedDelay.DelayMode.Tape;
                        delay.Time = 400f;
                        delay.Feedback = 0.3f;
                        delay.ModRate = 0.3f;
                        delay.ModDepth = 5f;
                        delay.Mix = 0.3f;
                    }
                }),
                Effect<FDNReverb>("Ambient", mix: 0.4f)
            ),
            Preset(
                "Creative",
                "Evil Twin",
                "Doubled voice with dark detuning.",
                Effect<WSOLAPitchShift>(configure: effect =>
                {
                    if (effect is WSOLAPitchShift pitch)
                    {
                        pitch.Semitones = -5f;
                        pitch.FormantPreservation = 0.6f;
                        pitch.Mix = 0.5f;
                    }
                }),
                Effect<UnifiedDelay>(configure: effect =>
                {
                    if (effect is UnifiedDelay delay)
                    {
                        delay.Mode = UnifiedDelay.DelayMode.Standard;
                        delay.Time = 30f;
                        delay.Feedback = 0f;
                        delay.Mix = 0.35f;
                    }
                }),
                Effect<FDNReverb>("Large Hall", mix: 0.3f)
            ),
            
            // ===== LABS (EXPERIMENTAL) =====
            Preset(
                "LABS",
                "Glitch Protocol",
                "Unstable robotic malfunction with rhythmic stuttering.",
                Effect<RingModulatorEffect>(configure: effect =>
                {
                    if (effect is RingModulatorEffect ring)
                    {
                        ring.Waveform = LFOWaveform.Square;
                        ring.Frequency = 30f;
                        ring.Mix = 0.4f;
                    }
                }),
                Effect<TremoloEffect>(configure: effect =>
                {
                    if (effect is TremoloEffect trem)
                    {
                        trem.Waveform = LFOWaveform.Square;
                        trem.Rate = 12f;
                        trem.Depth = 100f;
                        trem.Mix = 1f;
                    }
                }),
                Effect<DistortionEffect>(configure: effect =>
                {
                    if (effect is DistortionEffect dist)
                    {
                        dist.Type = DistortionType.Bitcrusher;
                        dist.BitDepth = 6;
                        dist.SampleRateReduction = 2;
                        dist.Mix = 1f;
                    }
                }),
                Effect<ParametricEQ16>("Glitch EQ") // Added EQ
            ),
            Preset(
                "LABS",
                "Abyssal Void",
                "Massive, terrifying voice from the deep ocean.",
                Effect<WSOLAPitchShift>(configure: effect =>
                {
                    if (effect is WSOLAPitchShift pitch)
                    {
                        pitch.Semitones = -12f;
                        pitch.FormantPreservation = 0.7f;
                        pitch.Mix = 1f;
                    }
                }),
                Effect<FlangerEffect>(configure: effect =>
                {
                    if (effect is FlangerEffect flanger)
                    {
                        flanger.Rate = 0.2f;
                        flanger.Depth = 80f;
                        flanger.Feedback = 0.6f;
                        flanger.Mix = 0.5f;
                    }
                }),
                Effect<FDNReverb>(configure: effect =>
                {
                    if (effect is FDNReverb reverb)
                    {
                        reverb.RoomSize = 100f; // Huge
                        reverb.DecayTime = 10f;
                        reverb.Mix = 0.6f;
                        reverb.Damping = 0.8f; // Dark
                    }
                })
            ),
            Preset(
                "LABS",
                "Cyber Psycho",
                "Aggressive, distorted cyborg enforcer.",
                Effect<DistortionEffect>(configure: effect =>
                {
                    if (effect is DistortionEffect dist)
                    {
                        dist.Type = DistortionType.HardClip;
                        dist.Drive = 30f;
                        dist.Mix = 1f;
                    }
                }),
                Effect<WSOLAPitchShift>(configure: effect =>
                {
                    if (effect is WSOLAPitchShift pitch)
                    {
                        pitch.Semitones = -5f;
                        pitch.FormantPreservation = 0.5f;
                        pitch.Mix = 0.8f;
                    }
                }),
                Effect<UnifiedDelay>(configure: effect =>
                {
                    if (effect is UnifiedDelay delay)
                    {
                        delay.Mode = UnifiedDelay.DelayMode.Standard;
                        delay.Time = 50f; // Slapback
                        delay.Feedback = 0.2f;
                        delay.Mix = 0.3f;
                    }
                }),
                Effect<ParametricEQ16>("Telephone") // Bandpass finish
            ),
            Preset(
                "LABS",
                "8-Bit Dungeon",
                "Retro video game boss voice.",
                Effect<DistortionEffect>(configure: effect =>
                {
                    if (effect is DistortionEffect dist)
                    {
                        dist.Type = DistortionType.Bitcrusher;
                        dist.BitDepth = 4;
                        dist.SampleRateReduction = 4;
                        dist.Mix = 1f;
                    }
                }),
                Effect<ParametricEQ16>("Bit EQ"), // Added EQ
                Effect<RingModulatorEffect>(configure: effect =>
                {
                    if (effect is RingModulatorEffect ring)
                    {
                        ring.Waveform = LFOWaveform.Triangle;
                        ring.Frequency = 100f; // Chiptune synth feel
                        ring.Mix = 0.4f;
                    }
                }),
                Effect<ParametricEQ16>("Radio Voice")
            ),
            Preset(
                "LABS",
                "Ethereal Ghost",
                "Airy, detached spirit voice.",
                Effect<ParametricEQ16>("Air & Shine"),
                Effect<UnifiedDelay>(configure: effect =>
                {
                    if (effect is UnifiedDelay delay)
                    {
                        delay.Mode = UnifiedDelay.DelayMode.PingPong;
                        delay.Time = 400f;
                        delay.Feedback = 0.6f;
                        delay.Width = 1f; // Wide
                        delay.Mix = 0.4f;
                    }
                }),
                Effect<FDNReverb>(configure: effect =>
                {
                    if (effect is FDNReverb reverb)
                    {
                        reverb.RoomSize = 80f;
                        reverb.DecayTime = 6f;
                        reverb.ModulationDepth = 0.8f; // High modulation
                        reverb.Mix = 0.5f;
                    }
                })
            ),
            Preset(
                "LABS",
                "Time Dilation",
                "Extreme tape delay feedback creating a frozen texture.",
                Effect<UnifiedDelay>(configure: effect =>
                {
                    if (effect is UnifiedDelay delay)
                    {
                        delay.Mode = UnifiedDelay.DelayMode.Tape;
                        delay.Time = 50f;
                        delay.Feedback = 0.95f; // Near freeze
                        delay.ModRate = 0.1f;
                        delay.ModDepth = 20f; // Wow/Flutter
                        delay.Mix = 0.5f;
                    }
                }),
                Effect<FDNReverb>("Large Hall", mix: 0.4f)
            ),
            Preset(
                "LABS",
                "Kaiju Warning",
                "Monstrous roar with sub-bass distortion.",
                Effect<WSOLAPitchShift>(configure: effect =>
                {
                    if (effect is WSOLAPitchShift pitch)
                    {
                        pitch.Semitones = -18f; // 1.5 octaves down
                        pitch.FormantPreservation = 0.9f;
                        pitch.Mix = 1f;
                    }
                }),
                Effect<TubeEmulation>(configure: effect =>
                {
                    if (effect is TubeEmulation tube)
                    {
                        tube.Drive = 10f;
                        tube.Bias = 0.8f;
                        tube.Mix = 1f;
                    }
                }),
                Effect<FlangerEffect>("Jet Plane", mix: 0.4f)
            ),
            Preset(
                "LABS",
                "Cursed Doll",
                "Unnaturally high pitch with spooky reverse ambience.",
                Effect<WSOLAPitchShift>(configure: effect =>
                {
                    if (effect is WSOLAPitchShift pitch)
                    {
                        pitch.Semitones = 14f;
                        pitch.FormantPreservation = 0.8f;
                        pitch.Mix = 1f;
                    }
                }),
                Effect<UnifiedDelay>("Reverse", mix: 0.5f), // Assume Reverse preset exists or generic
                Effect<FDNReverb>(configure: effect =>
                {
                    if (effect is FDNReverb reverb)
                    {
                        reverb.RoomSize = 20f;
                        reverb.DecayTime = 4f;
                        reverb.Damping = 0.2f;
                        reverb.Mix = 0.4f;
                    }
                })
            ),
            Preset(
                "LABS",
                "Data Mosh",
                "Digital corruption crashing into silence.",
                Effect<DistortionEffect>(configure: effect =>
                {
                    if (effect is DistortionEffect dist)
                    {
                        dist.Type = DistortionType.Bitcrusher;
                        dist.BitDepth = 3;
                        dist.SampleRateReduction = 3;
                        dist.Mix = 1f;
                    }
                }),
                Effect<RingModulatorEffect>(configure: effect =>
                {
                    if (effect is RingModulatorEffect ring)
                    {
                        ring.Frequency = 800f;
                        ring.Waveform = LFOWaveform.Sawtooth;
                        ring.Mix = 0.3f;
                    }
                }),
                Effect<UnifiedDynamics>(configure: effect =>
                {
                    if (effect is UnifiedDynamics gate)
                    {
                        gate.Mode = UnifiedDynamics.DynamicsMode.Gate;
                        gate.Threshold = -20f; // Chops tail aggressively
                        gate.Release = 10f; // Fast cut
                    }
                })
            ),
            Preset(
                "LABS",
                "Hyperspace",
                "Rapid panning and phasing for high speed sensation.",
                Effect<PhaserEffect>(configure: effect =>
                {
                    if (effect is PhaserEffect phaser)
                    {
                        phaser.Rate = 8f; // Fast
                        phaser.Feedback = 0.7f;
                        phaser.Mix = 0.6f;
                    }
                }),
                Effect<TremoloEffect>(configure: effect =>
                {
                    if (effect is TremoloEffect trem)
                    {
                        trem.Rate = 10f; // Pan speed (if Tremolo handles pan?)
                        // Currently Tremolo is amplitude. Maybe Autopan?
                        // If no Autopan, use Spatial3D with some automation?
                        // Let's stick to Tremolo for stutter.
                        trem.Depth = 80f; 
                        trem.Mix = 0.5f;
                    }
                }),
                Effect<UnifiedDelay>("PingPong", mix: 0.4f)
            ),
            Preset(
                "LABS",
                "Broken Receiver",
                "Old radio losing signal.",
                Effect<ParametricEQ16>("Telephone"),
                Effect<TapeEmulation>(configure: effect =>
                {
                    if (effect is TapeEmulation tape)
                    {
                        tape.InputDrive = 12f;
                        tape.Wow = 0.5f;
                        tape.Flutter = 0.2f;
                        tape.Mix = 1f;
                    }
                }),
                Effect<TremoloEffect>(configure: effect =>
                {
                    if (effect is TremoloEffect trem)
                    {
                        trem.Waveform = LFOWaveform.Square; // Random-ish chopping
                        trem.Rate = 4f;
                        trem.Depth = 100f; // Signal cuts out
                        trem.Mix = 0.7f;
                    }
                })
            ),
            Preset(
                "LABS",
                "Brain Massage",
                "Binaural swirling textures.",
                Effect<Spatial3DEffect>(configure: effect =>
                {
                    if (effect is Spatial3DEffect spatial)
                    {
                        spatial.Spread = 360f; // Full surround
                        spatial.Distance = 0.2f; // Very close
                    }
                }),
                Effect<FlangerEffect>(configure: effect =>
                {
                    if (effect is FlangerEffect flanger)
                    {
                        flanger.Rate = 0.1f; // Very slow
                        flanger.Depth = 100f;
                        flanger.Mix = 0.4f;
                    }
                }),
                Effect<ParametricEQ16>("Air & Shine")
            ),
            
            // ===== UTILITY PRESETS =====
            Preset(
                "Utility",
                "Mastering Chain",
                "Final polish with glue and limiter.",
                Effect<ParametricEQ16>("Air & Shine"),
                Effect<UnifiedDynamics>("Glue"),
                Effect<UnifiedDynamics>("Mastering")
            ),
            Preset(
                "Utility",
                "De-Harsh",
                "Tame mids and smooth dynamics.",
                Effect<ParametricEQ16>("De-Muddy"),
                Effect<UnifiedDynamics>("Transparent")
            ),
            Preset(
                "Utility",
                "Voice Restoration",
                "Gate cleanup and vocal focus.",
                Effect<UnifiedDynamics>(configure: effect =>
                {
                    if (effect is UnifiedDynamics dynamics)
                    {
                        dynamics.Mode = UnifiedDynamics.DynamicsMode.Gate;
                        dynamics.Threshold = -40f;
                        dynamics.Attack = 5f;
                        dynamics.Release = 120f;
                        dynamics.Range = -60f;
                        dynamics.Mix = 1f;
                    }
                }),
                Effect<ParametricEQ16>("Voice Clarity"),
                Effect<UnifiedDynamics>("Vocal Warmth")
            ),
            Preset(
                "Utility",
                "Parallel Compression",
                "Blend heavy compression at low mix.",
                Effect<UnifiedDynamics>(configure: effect =>
                {
                    if (effect is UnifiedDynamics dynamics)
                    {
                        dynamics.Mode = UnifiedDynamics.DynamicsMode.Compressor;
                        dynamics.Threshold = -18f;
                        dynamics.Ratio = 10f;
                        dynamics.Attack = 5f;
                        dynamics.Release = 80f;
                        dynamics.Knee = 2f;
                        dynamics.MakeupGain = 4f;
                        dynamics.AutoMakeup = false;
                        dynamics.SidechainHPF = 60f;
                        dynamics.Mix = 0.3f;
                    }
                })
            ),
            Preset(
                "Utility",
                "Warmth & Depth",
                "Gentle warmth with subtle space.",
                Effect<ParametricEQ16>("Warmth"),
                Effect<ChorusEffect>("Subtle Stereo"),
                Effect<FDNReverb>("Small Room", mix: 0.15f)
            )
        };

        public static IReadOnlyList<ChainPreset> GetPresets()
        {
            return Presets;
        }

        public static void ApplyPreset(DSPChain chain, ChainPreset preset)
        {
            if (chain == null || preset == null) return;

            var effects = new List<IDSPEffect>();
            foreach (var entry in preset.Effects)
            {
                if (entry?.EffectType == null) continue;
                var effect = Activator.CreateInstance(entry.EffectType) as IDSPEffect;
                if (effect == null) continue;

                if (effect is DSPEffectBase baseEffect)
                {
                    if (!string.IsNullOrEmpty(entry.PresetName))
                    {
                        baseEffect.ApplyPreset(entry.PresetName);
                    }
                    if (entry.Mix.HasValue)
                    {
                        baseEffect.Mix = entry.Mix.Value;
                    }
                }

                effect.Enabled = entry.Enabled;
                entry.Configure?.Invoke(effect);
                effects.Add(effect);
            }

            chain.ApplyPresetEffects(effects);
        }
    }
}
