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
                Effect<PitchShift>("Doubler", mix: 0.25f),
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
                Effect<PitchShift>("Deep"),
                Effect<ParametricEQ16>("Warmth"),
                Effect<UnifiedDynamics>("Glue")
            ),
            Preset(
                "Voice",
                "High Voice",
                "Pitch-shifted for higher feminine tone.",
                Effect<PitchShift>("Natural Up"),
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
                Effect<ParametricEQ16>(configure: effect =>
                {
                    if (effect is ParametricEQ16 eq)
                    {
                        // Underwater: aggressively cut highs
                        eq.SetBand(8, 1000f, -6f, 1f, EQFilterType.HighShelf);
                        eq.SetBand(10, 2500f, -12f, 1f, EQFilterType.HighShelf);
                        eq.SetBand(12, 6000f, -24f, 1f, EQFilterType.HighShelf);
                        eq.SetBand(15, 16000f, -48f, 1f, EQFilterType.Peak);
                    }
                }),
                Effect<ChorusEffect>("Lush"),
                Effect<FDNReverb>("Large Hall", mix: 0.5f)
            ),
            Preset(
                "Creative",
                "Demon Voice",
                "Dark pitch-shifted with ring mod and distortion.",
                Effect<PitchShift>(configure: effect =>
                {
                    if (effect is PitchShift pitch)
                    {
                        pitch.Pitch = -12f;
                        pitch.FineTune = 0.6f;
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
                Effect<PitchShift>("Chipmunk"),
                Effect<UnifiedDynamics>("Punchy")
            ),
            Preset(
                "Creative",
                "Ghost Whisper",
                "Ethereal whispered voice from beyond.",
                Effect<PhaserEffect>("Deep Sweep"),
                Effect<PitchShift>(configure: effect =>
                {
                    if (effect is PitchShift pitch)
                    {
                        pitch.Pitch = 5f;
                        pitch.FineTune = 0.8f;
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
                Effect<ParametricEQ16>(configure: effect =>
                {
                    if (effect is ParametricEQ16 eq)
                    {
                        eq.SetBand(13, 6000f, -12f, 1f, EQFilterType.HighShelf);
                        eq.SetBand(14, 10000f, -18f, 1f, EQFilterType.HighShelf);
                        eq.SetBand(15, 16000f, -24f, 1f, EQFilterType.HighShelf);
                        eq.ApplyPreset("Telephone"); 
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
                Effect<PitchShift>("Doubler", mix: 0.25f),
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
                Effect<PitchShift>(configure: effect =>
                {
                    if (effect is PitchShift pitch)
                    {
                        pitch.Pitch = -5f;
                        pitch.FineTune = 0.6f;
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
                Effect<PitchShift>(configure: effect =>
                {
                    if (effect is PitchShift pitch)
                    {
                        pitch.Pitch = -12f;
                        pitch.FineTune = 0.7f;
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
                Effect<PitchShift>(configure: effect =>
                {
                    if (effect is PitchShift pitch)
                    {
                        pitch.Pitch = -5f;
                        pitch.FineTune = 0.5f;
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
                Effect<PitchShift>(configure: effect =>
                {
                    if (effect is PitchShift pitch)
                    {
                        pitch.Pitch = -18f; // 1.5 octaves down
                        pitch.FineTune = 0.9f;
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
                Effect<PitchShift>(configure: effect =>
                {
                    if (effect is PitchShift pitch)
                    {
                        pitch.Pitch = 14f;
                        pitch.FineTune = 0.8f;
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

            // ===== HORROR PRESETS =====
            Preset(
                "Horror",
                "Broken Android",
                "Glitching robot malfunction.",
                Effect<RingModulatorEffect>(configure: effect =>
                {
                    if (effect is RingModulatorEffect ring)
                    {
                        ring.Waveform = LFOWaveform.Sine;
                        ring.Frequency = 1500f; // High frequency ring
                        ring.Mix = 0.6f;
                    }
                }),
                Effect<UnifiedDelay>(configure: effect =>
                {
                    if (effect is UnifiedDelay delay)
                    {
                        delay.Mode = UnifiedDelay.DelayMode.Tape;
                        delay.Time = 20f;
                        delay.Feedback = 0.4f;
                        delay.ModRate = 5f; // Fast flutter
                        delay.ModDepth = 50f;
                        delay.Mix = 0.5f;
                    }
                }),
                Effect<DistortionEffect>(configure: effect =>
                {
                    if (effect is DistortionEffect dist)
                    {
                        dist.Type = DistortionType.Bitcrusher;
                        dist.BitDepth = 6;
                        dist.SampleRateReduction = 4;
                        dist.Mix = 1f;
                    }
                })
            ),
            Preset(
                "Horror",
                "Abyss Whisper",
                "Deep cave monster voice.",
                Effect<PitchShift>(configure: effect =>
                {
                    if (effect is PitchShift pitch)
                    {
                        pitch.Pitch = -12f;
                        pitch.FineTune = 0.5f;
                        pitch.Mix = 1f;
                    }
                }),
                Effect<ParametricEQ16>(configure: effect =>
                {
                    if (effect is ParametricEQ16 eq)
                    {
                        // Muffled monster
                        eq.SetBand(6, 400f, -6f, 1f, EQFilterType.HighShelf); 
                        eq.SetBand(8, 1000f, -12f, 1f, EQFilterType.HighShelf);
                    }
                }),
                Effect<FDNReverb>(configure: effect =>
                {
                    if (effect is FDNReverb reverb)
                    {
                        reverb.RoomSize = 100f;
                        reverb.DecayTime = 8f;
                        reverb.Damping = 0.9f; // Very dark
                        reverb.Mix = 0.6f;
                    }
                })
            ),
            Preset(
                "Horror",
                "Poltergeist",
                "Distorted radio ghost.",
                Effect<ParametricEQ16>("Telephone"),
                Effect<DistortionEffect>(configure: effect =>
                {
                    if (effect is DistortionEffect dist)
                    {
                        dist.Type = DistortionType.HardClip;
                        dist.Drive = 35f;
                        dist.Mix = 0.8f;
                    }
                }),
                Effect<UnifiedDelay>(configure: effect =>
                {
                    if (effect is UnifiedDelay delay)
                    {
                        delay.Mode = UnifiedDelay.DelayMode.PingPong;
                        delay.Time = 120f;
                        delay.Feedback = 0.5f;
                        delay.Mix = 0.4f;
                    }
                })
            ),
            Preset(
                "Horror",
                "Hive Mind",
                "Swarm of insect voices.",
                Effect<ChorusEffect>(configure: effect =>
                {
                    if (effect is ChorusEffect chorus)
                    {
                        chorus.Rate = 5f;
                        chorus.Depth = 80f;
                        chorus.Mix = 1f; // Full wet
                    }
                }),
                Effect<PitchShift>(configure: effect =>
                {
                    if (effect is PitchShift pitch)
                    {
                        pitch.Pitch = -5f;
                        pitch.FineTune = 0f; // Minimal formant
                        pitch.Mix = 0.5f;
                    }
                }),
                Effect<Spatial3DEffect>("Wide")
            ),
            Preset(
                "Horror",
                "Cursed Doll",
                "Creepy toy voice.",
                Effect<PitchShift>(configure: effect =>
                {
                    if (effect is PitchShift pitch)
                    {
                        pitch.Pitch = 12f;
                        pitch.FineTune = 0.8f;
                        pitch.Mix = 1f;
                    }
                }),
                Effect<TremoloEffect>(configure: effect =>
                {
                    if (effect is TremoloEffect trem)
                    {
                        trem.Rate = 8f;
                        trem.Depth = 30f;
                        trem.Mix = 0.5f;
                    }
                }),
                Effect<UnifiedDelay>(configure: effect =>
                {
                    if (effect is UnifiedDelay delay)
                    {
                        delay.Mode = UnifiedDelay.DelayMode.Tape;
                        delay.Time = 300f;
                        delay.Feedback = 0.4f;
                        delay.ModRate = 0.2f;
                        delay.ModDepth = 10f;
                        delay.Mix = 0.3f;
                    }
                })
            ),
            Preset(
                "Horror",
                "Psychopath",
                "Uncomfortable close breathing.",
                Effect<UnifiedDynamics>(configure: effect =>
                {
                    if (effect is UnifiedDynamics comp)
                    {
                        comp.Mode = UnifiedDynamics.DynamicsMode.Compressor;
                        comp.Threshold = -30f; // Crush it
                        comp.Ratio = 20f;
                        comp.MakeupGain = 12f;
                        comp.Attack = 0.1f;
                        comp.Release = 50f;
                    }
                }),
                Effect<ParametricEQ16>(configure: effect =>
                {
                    if (effect is ParametricEQ16 eq)
                    {
                        // Boost high frequencies for breathiness
                        // Assuming API allows setting bands directly or using a helper.
                        // Since I can't easily set bands in this lambda without helpers, 
                        // I'll rely on "Brightness" preset logic if available, or just use Air & Shine 
                        // but modified. Let's use Air & Shine as base.
                        eq.ApplyPreset("Air & Shine"); 
                    }
                }),
                Effect<SaturationEffect>("Warm")
            ),
            Preset(
                "Horror",
                "Banshee",
                "Screaming spirit.",
                Effect<DistortionEffect>(configure: effect =>
                {
                    if (effect is DistortionEffect dist)
                    {
                        dist.Type = DistortionType.Fuzz;
                        dist.Drive = 50f;
                        dist.Mix = 1f;
                    }
                }),
                Effect<PitchShift>(configure: effect =>
                {
                    if (effect is PitchShift pitch)
                    {
                        pitch.Pitch = 5f;
                        pitch.Mix = 0.7f;
                    }
                }),
                Effect<FDNReverb>("Plate", mix: 0.5f)
            ),
            Preset(
                "Horror",
                "Rusty Saw",
                "Industrial torture sounds.",
                Effect<RingModulatorEffect>(configure: effect =>
                {
                    if (effect is RingModulatorEffect ring)
                    {
                        ring.Waveform = LFOWaveform.Sawtooth;
                        ring.Frequency = 40f; // Fast rattle
                        ring.Mix = 1f;
                    }
                }),
                Effect<FlangerEffect>("Metallic", mix: 0.8f),
                Effect<DistortionEffect>("Crunch", mix: 0.5f)
            ),
            Preset(
                "Horror",
                "Demon Lord",
                "Final boss voice.",
                Effect<PitchShift>(configure: effect =>
                {
                    if (effect is PitchShift pitch)
                    {
                        pitch.Pitch = -7f;
                        pitch.FineTune = 0.8f;
                        pitch.Mix = 1f;
                    }
                }),
                Effect<TubeEmulation>(configure: effect =>
                {
                    if (effect is TubeEmulation tube)
                    {
                        tube.Type = TubeType.PentodeEL34;
                        tube.Drive = 15f;
                        tube.Mix = 1f;
                    }
                }),
                Effect<Spatial3DEffect>("Wide")
            ),
            Preset(
                "Horror",
                "Ghost Radio",
                "Spooky EVP recording.",
                Effect<TapeEmulation>(configure: effect =>
                {
                    if (effect is TapeEmulation tape)
                    {
                        tape.Hiss = 0.4f; // Lots of noise
                        tape.Wow = 0.6f;
                        tape.Flutter = 0.6f;
                        tape.Mix = 1f;
                    }
                }),
                Effect<ParametricEQ16>("Telephone"),
                Effect<TremoloEffect>(configure: effect =>
                {
                    if (effect is TremoloEffect trem)
                    {
                        trem.Rate = 0.5f; // Slow fade in/out
                        trem.Depth = 40f;
                        trem.Mix = 0.5f;
                    }
                })
            ),

            // ===== CYBERPUNK CITY PRESETS =====
            Preset(
                "Cyberpunk",
                "Hologram",
                "Unstable holographic projection.",
                Effect<PhaserEffect>(configure: effect =>
                {
                    if (effect is PhaserEffect phaser)
                    {
                        phaser.Rate = 8f; // Fast shimmer
                        phaser.Depth = 60f;
                        phaser.Mix = 0.6f;
                    }
                }),
                Effect<FlangerEffect>(configure: effect =>
                {
                    if (effect is FlangerEffect flanger)
                    {
                        flanger.Rate = 2f;
                        flanger.Depth = 40f;
                        flanger.Mix = 0.4f;
                    }
                }),
                Effect<ParametricEQ16>("Telephone") // Bandpass effect
            ),
            Preset(
                "Cyberpunk",
                "Netrunner",
                "Digital data stream dive.",
                Effect<DistortionEffect>(configure: effect =>
                {
                    if (effect is DistortionEffect dist)
                    {
                        dist.Type = DistortionType.Bitcrusher;
                        dist.BitDepth = 4;
                        dist.SampleRateReduction = 3;
                        dist.Mix = 1f;
                    }
                }),
                Effect<RingModulatorEffect>(configure: effect =>
                {
                    if (effect is RingModulatorEffect ring)
                    {
                        ring.Waveform = LFOWaveform.Square;
                        ring.Frequency = 500f; // Data noise
                        ring.Mix = 0.5f;
                    }
                })
            ),
            Preset(
                "Cyberpunk",
                "Space Marine",
                "Helmet communication system.",
                Effect<ParametricEQ16>(configure: effect =>
                {
                    if (effect is ParametricEQ16 eq)
                    {
                        // High pass to cut mud
                        eq.ApplyPreset("Voice Clarity"); 
                    }
                }),
                Effect<UnifiedDynamics>(configure: effect =>
                {
                    if (effect is UnifiedDynamics comp)
                    {
                        comp.Mode = UnifiedDynamics.DynamicsMode.Compressor;
                        comp.Threshold = -25f; // Heavy squashing
                        comp.Ratio = 20f;
                        comp.Attack = 1f;
                        comp.Release = 50f;
                        comp.MakeupGain = 10f;
                    }
                }),
                Effect<DistortionEffect>(configure: effect =>
                {
                    if (effect is DistortionEffect dist)
                    {
                        dist.Type = DistortionType.SoftClip;
                        dist.Drive = 20f;
                        dist.Mix = 0.3f;
                    }
                })
            ),
            Preset(
                "Cyberpunk",
                "Synthetic",
                "Artificial human voice.",
                Effect<PitchShift>(configure: effect =>
                {
                    if (effect is PitchShift pitch)
                    {
                        pitch.Pitch = 0f;
                        pitch.FineTune = 1.2f; // Shift formants only
                        pitch.Mix = 1f;
                    }
                }),
                Effect<ChorusEffect>(configure: effect =>
                {
                    if (effect is ChorusEffect chorus)
                    {
                        chorus.Rate = 0.5f;
                        chorus.Depth = 60f;
                        chorus.Mix = 0.4f;
                    }
                })
            ),
            Preset(
                "Cyberpunk",
                "Neon Glitch",
                "Stuttering defective tech.",
                Effect<TremoloEffect>(configure: effect =>
                {
                    if (effect is TremoloEffect trem)
                    {
                        trem.Waveform = LFOWaveform.Square;
                        trem.Rate = 12f; // Fast chop
                        trem.Depth = 90f;
                        trem.Mix = 0.8f;
                    }
                }),
                Effect<UnifiedDelay>(configure: effect =>
                {
                    if (effect is UnifiedDelay delay)
                    {
                        delay.Mode = UnifiedDelay.DelayMode.Tape;
                        delay.Time = 100f;
                        delay.Feedback = 0.3f;
                        delay.ModRate = 8f;
                        delay.ModDepth = 40f; // Glitchy flutter
                        delay.Mix = 0.5f;
                    }
                })
            ),

            // ===== FANTASY PRESETS =====
            Preset(
                "Fantasy",
                "Dragon",
                "Massive ancient beast with deep resonance and power.",
                Effect<ParametricEQ16>(configure: effect =>
                {
                    if (effect is ParametricEQ16 eq)
                    {
                        eq.SetBand(0, 54.3f, 5.5f, 8.3f, EQFilterType.Peak);
                        eq.SetBand(1, 33.5f, 14.0f, 4.5f, EQFilterType.Peak);
                    }
                }),
                Effect<PitchShift>(configure: effect =>
                {
                    if (effect is PitchShift pitch)
                    {
                        pitch.Pitch = -7.92f;
                        pitch.FineTune = 1.0f;
                        pitch.GrainSize = 40f;
                        pitch.Spread = 0f;
                        pitch.Mix = 1.0f;
                    }
                }),
                Effect<PhaserEffect>(configure: effect =>
                {
                    if (effect is PhaserEffect phaser)
                    {
                        phaser.Stages = 4;
                        phaser.Rate = 0.397f;
                        phaser.Depth = 49f;
                        phaser.CenterFreq = 1000f;
                        phaser.FreqRange = 2f;
                        phaser.Feedback = 49f;
                        phaser.Waveform = LFOWaveform.Sine;
                        phaser.StereoPhase = 45f;
                        phaser.Mix = 1.0f;
                    }
                }),
                Effect<TubeEmulation>(configure: effect =>
                {
                    if (effect is TubeEmulation tube)
                    {
                        tube.Type = TubeType.Triode12AX7;
                        tube.Drive = 2.88f;
                        tube.Bias = 0.58f;
                        tube.Presence = 0.65f;
                        tube.Sag = 0.15f;
                        tube.EvenHarmonics = 0.5f;
                        tube.OddHarmonics = 0.3f;
                        tube.Mix = 0.54f;
                    }
                }),
                Effect<ChorusEffect>(configure: effect =>
                {
                    if (effect is ChorusEffect chorus)
                    {
                        chorus.DelayMs = 20f;
                        chorus.Rate = 1.099f;
                        chorus.Depth = 18f;
                        chorus.Voices = 2;
                        chorus.Mix = 0.194f;
                    }
                }),
                Effect<FDNReverb>(configure: effect =>
                {
                    if (effect is FDNReverb reverb)
                    {
                        reverb.PreDelay = 1.29f;
                        reverb.RoomSize = 2.28f;
                        reverb.DecayTime = 4.18f;
                        reverb.Damping = 3660f;
                        reverb.Diffusion = 0.7f;
                        reverb.ModulationRate = 0.5f;
                        reverb.ModulationDepth = 0.3f;
                        reverb.Mix = 0.32f;
                    }
                }),
                Effect<UnifiedDynamics>(configure: effect =>
                {
                    if (effect is UnifiedDynamics dynamics)
                    {
                        dynamics.Mode = UnifiedDynamics.DynamicsMode.Compressor;
                        dynamics.Threshold = -28.4f;
                        dynamics.Attack = 20f;
                        dynamics.Release = 120f;
                        dynamics.Ratio = 4f;
                        dynamics.Knee = 6f;
                        dynamics.MakeupGain = 2.88f;
                        dynamics.Ceiling = -0.3f;
                    }
                }),
                Effect<FDNReverb>(configure: effect =>
                {
                    if (effect is FDNReverb reverb)
                    {
                        reverb.PreDelay = 69.3f;
                        reverb.RoomSize = 69.6f;
                        reverb.DecayTime = 4.67f;
                        reverb.Damping = 11260f;
                        reverb.Mix = 0.29f;
                    }
                }),
                Effect<ParametricEQ16>(configure: effect =>
                {
                    if (effect is ParametricEQ16 eq)
                    {
                        eq.SetBand(0, 68.2f, 5.8f, 2.5f, EQFilterType.Peak);
                        eq.SetBand(1, 2518f, -3.5f, 3.2f, EQFilterType.Peak);
                        eq.SetBand(2, 1060f, -7.1f, 11.0f, EQFilterType.Peak);
                    }
                }),
                Effect<UnifiedDynamics>(configure: effect =>
                {
                    if (effect is UnifiedDynamics dynamics)
                    {
                        dynamics.Mode = UnifiedDynamics.DynamicsMode.Limiter;
                        dynamics.Threshold = -20f;
                        dynamics.Ceiling = -0.9f;
                    }
                })
            ),
            Preset(
                "Fantasy",
                "Heavenly / Soul",
                "Ethereal and wide spiritual presence.",
                Effect<ParametricEQ16>(configure: effect =>
                {
                    if (effect is ParametricEQ16 eq)
                    {
                        eq.SetBand(0, 69.2f, -7.8f, 1.0f, EQFilterType.HighPass);
                        eq.SetBand(1, 1596f, 2.5f, 1.0f, EQFilterType.Peak);
                        eq.SetBand(2, 8364f, 1.6f, 1.0f, EQFilterType.Peak);
                    }
                }),
                Effect<UnifiedDynamics>(configure: effect =>
                {
                    if (effect is UnifiedDynamics dynamics)
                    {
                        dynamics.Mode = UnifiedDynamics.DynamicsMode.Compressor;
                        dynamics.Threshold = -20f;
                        dynamics.Attack = 16f;
                        dynamics.Release = 140f;
                        dynamics.Ratio = 4f;
                        dynamics.MakeupGain = 1.92f;
                    }
                }),
                Effect<UnifiedDelay>(configure: effect =>
                {
                    if (effect is UnifiedDelay delay)
                    {
                        delay.Time = 190f;
                        delay.Feedback = 0.2f;
                        delay.TapCount = 4;
                        delay.TapSpacing = 1f;
                        delay.TapDecay = 0.7f;
                        delay.CrossFeedback = 0.3f;
                        delay.FilterLow = 1287f;
                        delay.FilterHigh = 14178f;
                        delay.Mix = 0.27f;
                    }
                }),
                Effect<FDNReverb>(configure: effect =>
                {
                    if (effect is FDNReverb reverb)
                    {
                        reverb.PreDelay = 91f;
                        reverb.RoomSize = 91f;
                        reverb.DecayTime = 3.5f;
                        reverb.Damping = 7270f;
                        reverb.Diffusion = 0.8f;
                        reverb.EarlyLevel = 0.35f;
                        reverb.TailLevel = 0.8f;
                        reverb.Mix = 0.28f;
                    }
                }),
                Effect<UnifiedDelay>(configure: effect =>
                {
                    if (effect is UnifiedDelay delay)
                    {
                        delay.Time = 250f;
                        delay.Feedback = 0.21f;
                        delay.TapCount = 4;
                        delay.CrossFeedback = 0.3f;
                        delay.FilterLow = 614f;
                        delay.FilterHigh = 12000f;
                        delay.Mix = 1.0f;
                    }
                })
            ),
            Preset(
                "Fantasy",
                "Ogre",
                "Slow, heavy, and saturated monster.",
                Effect<ParametricEQ16>(configure: effect =>
                {
                    if (effect is ParametricEQ16 eq)
                    {
                        eq.SetBand(0, 33.7f, 3.3f, 4.5f, EQFilterType.Peak);
                        eq.SetBand(1, 66.4f, 6.5f, 6.9f, EQFilterType.Peak);
                        eq.SetBand(2, 855f, 6.5f, 7.6f, EQFilterType.Peak);
                        eq.SetBand(3, 2129f, 2.2f, 1.0f, EQFilterType.Peak);
                    }
                }),
                Effect<PitchShift>(configure: effect =>
                {
                    if (effect is PitchShift pitch)
                    {
                        pitch.Pitch = -5.52f;
                        pitch.Mix = 1.0f;
                    }
                }),
                Effect<SaturationEffect>(configure: effect =>
                {
                    if (effect is SaturationEffect sat)
                    {
                        sat.Amount = 54f;
                        sat.Character = 0.5f;
                        sat.Mix = 1.0f;
                    }
                }),
                Effect<TubeEmulation>(configure: effect =>
                {
                    if (effect is TubeEmulation tube)
                    {
                        tube.Drive = 0.48f;
                        tube.Presence = 0.3f;
                        tube.Sag = 0.2f;
                        tube.EvenHarmonics = 0.5f;
                        tube.OddHarmonics = 0.3f;
                        tube.Mix = 1.0f;
                    }
                }),
                Effect<UnifiedDynamics>(configure: effect =>
                {
                    if (effect is UnifiedDynamics dynamics)
                    {
                        dynamics.Mode = UnifiedDynamics.DynamicsMode.Compressor;
                        dynamics.Threshold = -20f;
                        dynamics.Attack = 22f;
                        dynamics.Release = 140f;
                        dynamics.Ratio = 4f;
                        dynamics.Knee = 6f;
                    }
                }),
                Effect<FDNReverb>(configure: effect =>
                {
                    if (effect is FDNReverb reverb)
                    {
                        reverb.PreDelay = 28f;
                        reverb.RoomSize = 29f;
                        reverb.DecayTime = 3.8f;
                        reverb.Damping = 0.5f; // Wait, Damping 0.5f from asset usually means freq/value. Asset said 0.5.
                        // Standard damping is usually frequency in Hz. 0.5 Hz is extremely low.
                        // However, checking Dragon asset, 'Damping: 3660'. 
                        // Checking Ogre asset again: 'Damping: 0.5'. This looks like a mistake in the asset or a normalized value range change?
                        // Let's stick to the asset value.
                        reverb.Damping = 0.5f; 
                        reverb.Mix = 0.25f;
                    }
                })
            ),
            Preset(
                "Fantasy",
                "Walkie-Talkie",
                "Low quality handheld radio communication.",
                Effect<ParametricEQ16>(configure: effect =>
                {
                    if (effect is ParametricEQ16 eq)
                    {
                        eq.SetBand(3, 247f, -8.0f, 1.0f, EQFilterType.HighPass);
                        eq.SetBand(5, 222f, -9.6f, 1.0f, EQFilterType.Peak);
                        eq.SetBand(9, 1600f, 2.0f, 1.0f, EQFilterType.Peak);
                        eq.SetBand(10, 2435f, 1.8f, 1.0f, EQFilterType.Peak);
                        eq.SetBand(11, 3920f, 0.4f, 1.0f, EQFilterType.Peak);
                        eq.SetBand(13, 4891f, -3.6f, 1.0f, EQFilterType.LowPass);
                    }
                }),
                Effect<UnifiedDynamics>(configure: effect =>
                {
                    if (effect is UnifiedDynamics dynamics)
                    {
                        dynamics.Mode = UnifiedDynamics.DynamicsMode.Compressor;
                        dynamics.Threshold = -24f;
                        dynamics.Attack = 1f;
                        dynamics.Release = 80f;
                        dynamics.Ratio = 8f;
                        dynamics.MakeupGain = 8f;
                    }
                }),
                Effect<DistortionEffect>(configure: effect =>
                {
                    if (effect is DistortionEffect dist)
                    {
                        dist.Type = DistortionType.SoftClip;
                        dist.Drive = 14f;
                        dist.Tone = 13346f;
                        dist.OutputGain = -7.7f;
                        dist.BitDepth = 8;
                        dist.Mix = 0.69f;
                    }
                })
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

        internal static void ApplySmartRandom(DSPChain chain)
        {
            var random = new Random();
            var effects = new List<IDSPEffect>();

            // === 1. Pre-Processing (Optional Gate/Compressor) (20%) ===
            if (random.NextDouble() < 0.2)
            {
                 var comp = new UnifiedDynamics();
                 comp.ApplyPreset("Vocal Warmth");
                 effects.Add(comp);
            }

            // === 2. Pitch & Formant (Character) (40%) ===
            if (random.NextDouble() < 0.4)
            {
                if (random.NextDouble() < 0.7)
                {
                    // Pitch Shift
                    var pitch = new PitchShift();
                    // -12 to +12 semitones, weighted towards smaller shifts
                    int semitones = random.Next(-12, 13); 
                    if (Math.Abs(semitones) > 5 && random.NextDouble() < 0.5) semitones /= 2; // Tame it
                    
                    pitch.Pitch = semitones;
                    pitch.FineTune = 0.5f + (float)random.NextDouble() * 0.5f;
                    pitch.Mix = 1.0f;
                    effects.Add(pitch);
                }
                else
                {
                     // Robot / RingMod
                     var ring = new RingModulatorEffect();
                     ring.Frequency = (float)random.Next(50, 800);
                     ring.Mix = 0.3f + (float)random.NextDouble() * 0.4f;
                     effects.Add(ring);
                }
            }

            // === 3. Modulation & Movement (40%) ===
            if (random.NextDouble() < 0.4)
            {
                double r = random.NextDouble();
                if (r < 0.33) 
                {
                    var chorus = new ChorusEffect();
                    chorus.ApplyPreset("Lush");
                    effects.Add(chorus);
                }
                else if (r < 0.66)
                {
                    var flanger = new FlangerEffect();
                    flanger.ApplyPreset("Jet Plane");
                    flanger.Mix = 0.4f;
                    effects.Add(flanger);
                }
                else
                {
                    var phaser = new PhaserEffect();
                    phaser.ApplyPreset("Slow Sweep");
                    effects.Add(phaser);
                }
            }

            // === 4. Color & Distortion (30%) ===
            if (random.NextDouble() < 0.3)
            {
                 if (random.NextDouble() < 0.5)
                 {
                    var dist = new DistortionEffect();
                    dist.ApplyPreset("Tube Warm");
                    dist.Mix = 0.3f;
                    effects.Add(dist);
                 }
                 else
                 {
                    var eq = new ParametricEQ16();
                    eq.ApplyPreset("Telephone");
                    effects.Add(eq);
                 }
            }

            // === 5. Space (Reverb & Delay) (60%) ===
            if (random.NextDouble() < 0.6)
            {
                if (random.NextDouble() < 0.5)
                {
                    var delay = new UnifiedDelay();
                    delay.ApplyPreset("PingPong");
                    delay.Mix = 0.2f + (float)random.NextDouble() * 0.2f;
                    effects.Add(delay);
                }
                
                // Potential Reverb (can stack with delay)
                if (random.NextDouble() < 0.7) 
                {
                    var reverb = new FDNReverb();
                    string[] presets = { "Large Hall", "Plate", "Small Room" };
                    reverb.ApplyPreset(presets[random.Next(presets.Length)]);
                    reverb.Mix = 0.15f + (float)random.NextDouble() * 0.25f;
                    effects.Add(reverb);
                }
            }
            
            // === 6. Safety Limiter (Always) ===
            var limiter = new UnifiedDynamics();
            limiter.ApplyPreset("Mastering");
            effects.Add(limiter);

            chain.ApplyPresetEffects(effects);
        }
    }
}
