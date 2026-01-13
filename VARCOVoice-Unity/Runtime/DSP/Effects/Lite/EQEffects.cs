using System;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;

namespace VARCOVoice.DSP
{
    /// <summary>
    /// 5-band parametric equalizer
    /// </summary>
    [Serializable]
    public class EQEffect : DSPEffectBase
    {
        public override string Name => "EQ";
        public float Bass { get; set; } = 0f;
        public float LowMid { get; set; } = 0f;
        public float Mid { get; set; } = 0f;
        public float HighMid { get; set; } = 0f;
        public float Treble { get; set; } = 0f;
        
        private float[,] _x1, _x2, _y1, _y2;
        private bool _initialized;
        private int _lastSampleRate;
        private readonly float[] _frequencies = { 80f, 250f, 1000f, 4000f, 12000f };
        private readonly float _q = 1.0f;
        private float[,] _a0, _a1, _a2, _b0, _b1, _b2;
        
        public override void Process(float[] data, int channels, int sampleRate)
        {
            // Legacy implementation kept for safety
            EnsureInitialized(channels, sampleRate);
            UpdateCoefficients(sampleRate);
            int samplesPerChannel = data.Length / channels;
            for (int ch = 0; ch < channels; ch++)
            {
                for (int i = 0; i < samplesPerChannel; i++)
                {
                    int idx = i * channels + ch;
                    float sample = data[idx];
                    for (int band = 0; band < 5; band++)
                    {
                        float x0 = sample;
                        float output = (_b0[band, ch] * x0 + _b1[band, ch] * _x1[band, ch] + _b2[band, ch] * _x2[band, ch]
                                       - _a1[band, ch] * _y1[band, ch] - _a2[band, ch] * _y2[band, ch]) / _a0[band, ch];
                        _x2[band, ch] = _x1[band, ch]; _x1[band, ch] = x0;
                        _y2[band, ch] = _y1[band, ch]; _y1[band, ch] = output;
                        sample = output;
                    }
                    data[idx] = sample;
                }
            }
        }
        
        private void EnsureInitialized(int channels, int sampleRate)
        {
            if (_initialized && _lastSampleRate == sampleRate && _x1 != null && _x1.GetLength(1) == channels) return;
            _x1 = new float[5, channels]; _x2 = new float[5, channels];
            _y1 = new float[5, channels]; _y2 = new float[5, channels];
            _a0 = new float[5, channels]; _a1 = new float[5, channels]; _a2 = new float[5, channels];
            _b0 = new float[5, channels]; _b1 = new float[5, channels]; _b2 = new float[5, channels];
            _lastSampleRate = sampleRate; _initialized = true; UpdateCoefficients(sampleRate);
        }
        
        private void UpdateCoefficients(int sampleRate)
        {
            float[] gains = { Bass, LowMid, Mid, HighMid, Treble };
            for (int band = 0; band < 5; band++)
            {
                float freq = _frequencies[band];
                float gain = Mathf.Pow(10f, gains[band] / 20f);
                float w0 = 2f * Mathf.PI * freq / sampleRate;
                float alpha = Mathf.Sin(w0) / (2f * _q);
                float A = Mathf.Sqrt(gain);
                float b0 = 1f + alpha * A, b1 = -2f * Mathf.Cos(w0), b2 = 1f - alpha * A;
                float a0 = 1f + alpha / A, a1 = -2f * Mathf.Cos(w0), a2 = 1f - alpha / A;
                for (int ch = 0; ch < _a0.GetLength(1); ch++)
                {
                    _a0[band, ch] = a0; _a1[band, ch] = a1; _a2[band, ch] = a2;
                    _b0[band, ch] = b0; _b1[band, ch] = b1; _b2[band, ch] = b2;
                }
            }
        }

        public override void ApplyPreset(string presetName)
        {
            if (string.IsNullOrEmpty(presetName)) return;

            switch (presetName.Trim().ToLowerInvariant())
            {
                case "flat":
                    Bass = 0f;
                    LowMid = 0f;
                    Mid = 0f;
                    HighMid = 0f;
                    Treble = 0f;
                    break;
                case "voice clarity":
                    Bass = -2f;
                    LowMid = -1f;
                    Mid = 2f;
                    HighMid = 3f;
                    Treble = 2f;
                    break;
                case "warm":
                    Bass = 3f;
                    LowMid = 2f;
                    Mid = 0f;
                    HighMid = -1f;
                    Treble = 1f;
                    break;
                case "air":
                    Bass = 0f;
                    LowMid = -1f;
                    Mid = 0f;
                    HighMid = 2f;
                    Treble = 4f;
                    break;
                case "telephone":
                    Bass = -8f;
                    LowMid = -4f;
                    Mid = 3f;
                    HighMid = 2f;
                    Treble = -6f;
                    break;
            }
        }

        public override void Reset() { _initialized = false; }
    }

    /// <summary>
    /// Low-pass filter effect
    /// </summary>
    [Serializable]
    public class LowPassEffect : DSPEffectBase
    {
        public override string Name => "Low Pass";
        public float CutoffFrequency { get; set; } = 5000f;
        public float Resonance { get; set; } = 0.707f;
        
        private float[] _x1, _x2, _y1, _y2;
        private float _a0, _a1, _a2, _b0, _b1, _b2;
        private float _lastCutoff, _lastResonance;
        private int _lastSampleRate;
        private bool _initialized;
        
        public override void Process(float[] data, int channels, int sampleRate)
        {
            EnsureInitialized(channels, sampleRate);
            if (!Mathf.Approximately(_lastCutoff, CutoffFrequency) || !Mathf.Approximately(_lastResonance, Resonance))
                UpdateCoefficients(sampleRate);
            
            int samplesPerChannel = data.Length / channels;
            for (int ch = 0; ch < channels; ch++)
            {
                for (int i = 0; i < samplesPerChannel; i++)
                {
                    int idx = i * channels + ch;
                    float x0 = data[idx];
                    float output = (_b0 * x0 + _b1 * _x1[ch] + _b2 * _x2[ch] - _a1 * _y1[ch] - _a2 * _y2[ch]) / _a0;
                    _x2[ch] = _x1[ch]; _x1[ch] = x0; _y2[ch] = _y1[ch]; _y1[ch] = output;
                    
                    // Apply Mix
                    data[idx] = x0 * (1f - Mix) + output * Mix;
                }
            }
        }
        
        public override void ApplyPreset(string presetName)
        {
            if (string.IsNullOrEmpty(presetName)) return;

            switch (presetName.Trim().ToLowerInvariant())
            {
                case "warm":
                    CutoffFrequency = 3000f;
                    Resonance = 0.5f;
                    Mix = 1.0f;
                    break;
                case "dark":
                    CutoffFrequency = 1000f;
                    Resonance = 0.1f;
                    Mix = 1.0f;
                    break;
                case "telephone":
                    CutoffFrequency = 2000f;
                    Resonance = 2.0f;
                    Mix = 0.8f;
                    break;
                case "sweep start":
                    CutoffFrequency = 200f;
                    Resonance = 4.0f;
                    Mix = 1.0f;
                    break;
            }
        }

        private void EnsureInitialized(int channels, int sampleRate)
        {
            // Re-allocation only if configuration changes
            if (_initialized && _lastSampleRate == sampleRate && _x1 != null && _x1.Length == channels) return;
            
            _x1 = new float[channels]; _x2 = new float[channels];
            _y1 = new float[channels]; _y2 = new float[channels];
            _lastSampleRate = sampleRate;
            _initialized = true;
            UpdateCoefficients(sampleRate);
        }
        
        private void UpdateCoefficients(int sampleRate)
        {
            float cutoff = Mathf.Clamp(CutoffFrequency, 20f, sampleRate * 0.49f);
            float q = Mathf.Clamp(Resonance, 0.1f, 10f);
            
            float w0 = 2f * Mathf.PI * cutoff / sampleRate;
            float alpha = Mathf.Sin(w0) / (2f * q);
            float cosW0 = Mathf.Cos(w0);
            
            _b0 = (1f - cosW0) / 2f;
            _b1 = 1f - cosW0;
            _b2 = (1f - cosW0) / 2f;
            _a0 = 1f + alpha;
            _a1 = -2f * cosW0;
            _a2 = 1f - alpha;
            
            _lastCutoff = CutoffFrequency;
            _lastResonance = Resonance;
        }
        
        public override void Reset()
        {
            if (_x1 != null) Array.Clear(_x1, 0, _x1.Length);
            if (_x2 != null) Array.Clear(_x2, 0, _x2.Length);
            if (_y1 != null) Array.Clear(_y1, 0, _y1.Length);
            if (_y2 != null) Array.Clear(_y2, 0, _y2.Length);
            _initialized = false;
        }
    }

    /// <summary>
    /// Multi-voice Chorus effect
    /// </summary>
    [Serializable]
    public class ChorusEffect : DSPEffectBase
    {
        public override string Name => "Chorus";
        
        public float DelayMs { get; set; } = 20f;
        public float Rate { get; set; } = 1.0f;
        public float Depth { get; set; } = 5f;
        public int Voices { get; set; } = 2; // 1 to 8 voices
        
        private float[] _buffer;
        private int _bufferSize;
        private int _writePosition;
        private float _phase;
        
        private bool _initialized;
        private int _lastSampleRate;
        
        public override void Process(float[] data, int channels, int sampleRate)
        {
            EnsureInitialized(sampleRate);
            
            int samplesPerChannel = data.Length / channels;
            int voiceCount = Mathf.Clamp(Voices, 1, 8);
            float depthMs = Mathf.Max(0.1f, Depth);
            float baseDelayMs = Mathf.Max(1f, DelayMs);
            
            float phaseInc = Rate * Mathf.PI * 2f / sampleRate;
            float msToSamples = sampleRate / 1000f;

            for (int i = 0; i < samplesPerChannel; i++)
            {
                // Input (mono sum for simplicity in chorus calculation, though we write stereo)
                float input = 0f;
                for (int ch = 0; ch < channels; ch++) input += data[i * channels + ch];
                input /= channels;
                
                // Write to delay line
                _buffer[_writePosition] = input; // No feedback usually for pure chorus, or minimal
                
                float chorusOutput = 0f;
                
                // Calculate voices
                for (int v = 0; v < voiceCount; v++)
                {
                    // Stagger voices in phase
                    float voicePhase = _phase + (v * (Mathf.PI * 2f / voiceCount));
                    float lfo = Mathf.Sin(voicePhase);
                    
                    float delayTime = baseDelayMs + lfo * depthMs;
                    float delaySamples = delayTime * msToSamples;
                    
                    // Read from buffer
                    float readPos = _writePosition - delaySamples;
                    while (readPos < 0) readPos += _bufferSize;
                    while (readPos >= _bufferSize) readPos -= _bufferSize;
                    
                    int index0 = (int)readPos;
                    int index1 = (index0 + 1) % _bufferSize;
                    float frac = readPos - index0;
                    
                    chorusOutput += _buffer[index0] * (1f - frac) + _buffer[index1] * frac;
                }
                
                chorusOutput /= voiceCount;
                
                // SoftClip wet signal
                float wet = DSPConstants.SoftClip(chorusOutput);
                
                for (int ch = 0; ch < channels; ch++)
                {
                    int idx = i * channels + ch;
                    data[idx] = data[idx] * (1f - Mix) + wet * Mix;
                }
                
                _writePosition = (_writePosition + 1) % _bufferSize;
                _phase += phaseInc;
                if (_phase > Mathf.PI * 2f) _phase -= Mathf.PI * 2f;
            }
        }
        
        public override void ApplyPreset(string presetName)
        {
             if (string.IsNullOrEmpty(presetName)) return;

             switch (presetName.Trim().ToLowerInvariant())
             {
                 case "subtle stereo":
                     DelayMs = 20f;
                     Depth = 2f;
                     Rate = 0.3f;
                     Voices = 2;
                     Mix = 0.35f;
                     break;
                 case "lush":
                     DelayMs = 22f;
                     Depth = 4f;
                     Rate = 0.6f;
                     Voices = 4;
                     Mix = 0.5f;
                     break;
                 case "huge ensemble":
                     DelayMs = 24f;
                     Depth = 6f;
                     Rate = 0.7f;
                     Voices = 6;
                     Mix = 0.55f;
                     break;
                 case "80s synth":
                     DelayMs = 15f;
                     Depth = 6f;
                     Rate = 1f;
                     Voices = 4;
                     Mix = 0.55f;
                     break;
                 case "detune":
                     DelayMs = 28f;
                     Depth = 5f;
                     Rate = 0.2f;
                     Voices = 2;
                     Mix = 0.45f;
                     break;
                 case "shimmer":
                     DelayMs = 30f;
                     Depth = 8f;
                     Rate = 0.8f;
                     Voices = 8;
                     Mix = 0.6f;
                     break;
             }
        }
        
        private void EnsureInitialized(int sampleRate)
        {
            int requiredSize = (int)(sampleRate * 2.0f); // 2 sec buffer is plenty
            if (_initialized && _buffer != null && _buffer.Length >= requiredSize && _lastSampleRate == sampleRate) return;

             if (_buffer == null || _buffer.Length < requiredSize || _lastSampleRate != sampleRate)
             {
                 _bufferSize = requiredSize;
                 _buffer = new float[_bufferSize];
                 _writePosition = 0;
                 _lastSampleRate = sampleRate;
                 _initialized = true;
             }
        }

        public override void Reset()
        {
            // Clear buffer
             if (_buffer != null) Array.Clear(_buffer, 0, _buffer.Length);
             _writePosition = 0;
             _phase = 0f;
        }
    }
    
    // --- NEW ARCHITECTURE START ---

    /// <summary>
    /// Professional 16-band parametric equalizer (Refactored)
    /// Implements the Data-State-Logic pattern
    /// </summary>
    [Serializable]
    public class ParametricEQ16 : DSPEffectBase, IDisposable
    {
        public override string Name => "Parametric EQ 16";

        private static readonly float[] DefaultFrequencies =
        {
            25f, 40f, 63f, 100f, 160f, 250f, 400f, 630f,
            1000f, 1600f, 2500f, 4000f, 6300f, 10000f, 16000f, 20000f
        };

        // --- Data (Params) ---
        // We use an array of structs for Unity Serialization
        // This is the "Source of Truth" for the UI
        public EQBandParams[] Bands = new EQBandParams[16];
        
        public float OutputGain { get; set; } = 0f;

        // --- Serialization Wrapper for DSPPreset ---
        // Since DSPPreset only saves Properties and doesn't handle Arrays seamlessly,
        // we expose the Bands array as a JSON string property.
        public string SerializedBands
        {
            get => JsonUtility.ToJson(new EQBandWrapper { Bands = Bands });
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    // "If EQ data is missing, reset EQ to initialized state"
                    ResetBandGains();
                    return;
                }

                try
                {
                    var wrapper = JsonUtility.FromJson<EQBandWrapper>(value);
                    if (wrapper != null && wrapper.Bands != null && wrapper.Bands.Length == 16)
                    {
                        Bands = wrapper.Bands;
                        SanitizeBands();
                    }
                    else
                    {
                        ResetBandGains();
                    }
                }
                catch
                {
                    ResetBandGains();
                }
            }
        }
        
        [Serializable]
        public class EQBandWrapper
        {
            public EQBandParams[] Bands;
        }

        private static EQFilterType SanitizeFilterType(EQFilterType type)
        {
            return type == EQFilterType.LowShelf || type == EQFilterType.HighShelf
                ? EQFilterType.Peak
                : type;
        }

        private void SanitizeBands()
        {
            for (int i = 0; i < Bands.Length; i++)
            {
                var band = Bands[i];
                var sanitizedType = SanitizeFilterType(band.Type);
                if (sanitizedType != band.Type)
                {
                    band.Type = sanitizedType;
                    Bands[i] = band;
                }
            }
        }

        // NOTE: Native state removed - using managed arrays for audio-thread safety
        // Old fields (_state, _isDisposed, _nativeParams, _lastJobHandle) are no longer used

        // Constructor
        public ParametricEQ16()
        {
            // Initialize default bands
            for (int i = 0; i < 16; i++)
            {
                Bands[i] = EQBandParams.Default();
                Bands[i].Frequency = DefaultFrequencies[i];
                Bands[i].Type = EQFilterType.Peak;
                Bands[i].Gain = 0f;
            }
        }

        // --- Audio Thread Safe Processing ---
        
        // Managed state arrays (allocated once, reused)
        // DF2T only needs 2 state vars per band/channel (s1, s2)
        private float[,] _x1, _x2;  // [band, channel] - s1, s2 state
        private EQBandCoeffs[] _coeffs;       // [band] - pre-calculated
        private bool _stateInitialized;
        private int _lastChannels;
        private int _lastSampleRate;
        
        public override void Process(float[] data, int channels, int sampleRate)
        {
            if (!Enabled) return;
            EnsureBandsInitialized();
            
            // Ensure state is initialized (non-audio thread allocation is OK on first call)
            if (!_stateInitialized || _lastChannels != channels || _lastSampleRate != sampleRate)
            {
                InitializeManagedState(channels, sampleRate);
            }
            
            // Update coefficients EVERY frame to catch parameter changes
            // This is cheap (16 bands * simple math)
            UpdateCoefficients(sampleRate);
            
            // Pre-calculate linear output gain
            float linearGain = UnityEngine.Mathf.Pow(10f, OutputGain / 20f);
            
            int frames = data.Length / channels;
            
            // Process each band
            for (int band = 0; band < 16; band++)
            {
                if (!Bands[band].Enabled)
                    continue;
                if (Bands[band].Type == EQFilterType.Peak &&
                    UnityEngine.Mathf.Abs(Bands[band].Gain) < 0.01f)
                    continue;

                var c = _coeffs[band];

                // Process each channel
                for (int ch = 0; ch < channels; ch++)
                {
                    // Direct Form II Transposed state (only 2 state vars needed)
                    float s1 = _x1[band, ch];
                    float s2 = _x2[band, ch];
                    
                    // Process samples for this channel
                    for (int i = 0; i < frames; i++)
                    {
                        int idx = i * channels + ch;
                        float x = data[idx];

                        // Direct Form II Transposed biquad (matches EQLogic.ProcessBand)
                        float y = c.b0 * x + s1;
                        s1 = s2 + c.b1 * x - c.a1 * y;
                        s2 = c.b2 * x - c.a2 * y;

                        data[idx] = y;
                    }
                    
                    // Store state back
                    _x1[band, ch] = s1;
                    _x2[band, ch] = s2;
                }
            }
            
            // Apply output gain
            if (UnityEngine.Mathf.Abs(OutputGain) > 0.01f)
            {
                for (int i = 0; i < data.Length; i++)
                {
                    data[i] *= linearGain;
                }
            }
        }
        
        private void InitializeManagedState(int channels, int sampleRate)
        {
            _x1 = new float[16, channels];
            _x2 = new float[16, channels];
            _coeffs = new EQBandCoeffs[16];
            
            // Pre-calculate coefficients
            UpdateCoefficients(sampleRate);
            
            _lastChannels = channels;
            _lastSampleRate = sampleRate;
            _stateInitialized = true;
        }

        public void Prime(int channels, int sampleRate)
        {
            if (channels <= 0 || sampleRate <= 0) return;
            if (_stateInitialized && _lastChannels == channels && _lastSampleRate == sampleRate
                && _x1 != null && _x2 != null)
            {
                return;
            }
            InitializeManagedState(channels, sampleRate);
        }
        
        /// <summary>
        /// Call this when EQ parameters change (from main thread)
        /// </summary>
        public void UpdateCoefficients(int sampleRate)
        {
            EnsureBandsInitialized();
            if (_coeffs == null) _coeffs = new EQBandCoeffs[16];
            
            for (int i = 0; i < 16; i++)
            {
                EQLogic.UpdateCoefficients(Bands[i], sampleRate, out _coeffs[i]);
            }
        }
        
        public override void Reset()
        {
            // Reset filter history (DF2T state)
            if (_x1 != null) System.Array.Clear(_x1, 0, _x1.Length);
            if (_x2 != null) System.Array.Clear(_x2, 0, _x2.Length);
            
            // Reset the high-level Band params so the UI reflects the reset state
            ResetBandGains();
        }

        public override void ApplyPreset(string presetName)
        {
            if (string.IsNullOrEmpty(presetName)) return;

            ResetBandGains();
            OutputGain = 0f;

            switch (presetName.Trim().ToLowerInvariant())
            {
                case "voice clarity":
                    SetGainAt(250f, -2.5f);
                    SetGainAt(2500f, 3f);
                    SetGainAt(4000f, 2f);
                    SetGainAt(10000f, 1.5f);
                    break;
                case "radio voice":
                    SetGainAt(100f, -6f);
                    SetGainAt(250f, -4f);
                    SetGainAt(400f, -2f);
                    SetGainAt(1600f, 2f);
                    SetGainAt(2500f, 3f);
                    SetGainAt(4000f, 4f);
                    SetGainAt(10000f, -2f);
                    break;
                case "warmth":
                    SetGainAt(160f, 2.5f);
                    SetGainAt(250f, 2f);
                    SetGainAt(400f, 1.5f);
                    SetGainAt(4000f, -1f);
                    SetGainAt(10000f, 0.5f);
                    break;
                case "air & shine":
                case "air and shine":
                    SetGainAt(6300f, 1.5f);
                    SetGainAt(10000f, 3f);
                    SetGainAt(16000f, 2f);
                    break;
                case "presence":
                    SetGainAt(250f, -1.5f);
                    SetGainAt(2500f, 3f);
                    SetGainAt(4000f, 2f);
                    SetGainAt(6300f, 1.5f);
                    break;
                case "de-muddy":
                    SetGainAt(250f, -4f);
                    SetGainAt(400f, -3f);
                    SetGainAt(630f, -2f);
                    break;
                case "telephone":
                    SetGainAt(25f, -12f);
                    SetGainAt(40f, -12f);
                    SetGainAt(63f, -10f);
                    SetGainAt(100f, -8f);
                    SetGainAt(160f, -6f);
                    SetGainAt(250f, -4f);
                    SetGainAt(400f, -3f);
                    SetGainAt(630f, -2f);
                    SetGainAt(1000f, 0f);
                    SetGainAt(1600f, 1f);
                    SetGainAt(2500f, 2f);
                    SetGainAt(4000f, -1f);
                    SetGainAt(6300f, -4f);
                    SetGainAt(10000f, -8f);
                    SetGainAt(16000f, -12f);
                    SetGainAt(20000f, -12f);
                    break;
                case "proximity effect fix":
                case "proximity fix":
                    SetGainAt(100f, -6f);
                    SetGainAt(160f, -4f);
                    SetGainAt(250f, -2f);
                    SetGainAt(2500f, 1f);
                    break;
                case "flat":
                    break;
            }
        }

        private void EnsureBandsInitialized()
        {
            if (Bands != null && Bands.Length == 16) return;
            Bands = new EQBandParams[16];
            for (int i = 0; i < 16; i++)
            {
                Bands[i] = EQBandParams.Default();
                Bands[i].Frequency = DefaultFrequencies[i];
                Bands[i].Type = EQFilterType.Peak;
                Bands[i].Gain = 0f;
            }
        }

        private void ResetBandGains()
        {
            EnsureBandsInitialized();
            for (int i = 0; i < Bands.Length; i++)
            {
                Bands[i] = EQBandParams.Default();
                Bands[i].Frequency = DefaultFrequencies[i];
                Bands[i].Type = EQFilterType.Peak;
                Bands[i].Gain = 0f;
            }
        }

        private void SetGainAt(float frequency, float gainDb)
        {
            EnsureBandsInitialized();
            int closest = 0;
            float minDiff = float.MaxValue;
            for (int i = 0; i < Bands.Length; i++)
            {
                float diff = Mathf.Abs(Bands[i].Frequency - frequency);
                if (diff < minDiff)
                {
                    minDiff = diff;
                    closest = i;
                }
            }
            var band = Bands[closest];
            band.Gain = gainDb;
            band.Enabled = true;
            Bands[closest] = band;
        }

        // Dispose pattern (simplified - no native memory used anymore)
        public void Dispose()
        {
            // Managed arrays are automatically GC'd
            // Reset state for potential reuse
            _stateInitialized = false;
            _x1 = _x2 = null;
            _coeffs = null;
        }
        
        // Destructor safety (no longer needed but kept for interface compliance)
        ~ParametricEQ16()
        {
            // No native resources to clean up
        }

        // --- Backward Compatibility / Helper API ---
        
        public void SetBand(int index, float frequency, float gain, float q, EQFilterType type)
        {
            if (index < 0 || index >= 16) return;
            EnsureBandsInitialized();
            var band = Bands[index];
            band.Frequency = frequency;
            band.Gain = gain;
            band.Q = q;
            band.Type = SanitizeFilterType(type);
            band.Enabled = true;
            Bands[index] = band;
        }

        public void SetBandEnabled(int index, bool enabled)
        {
            if (index < 0 || index >= 16) return;
            EnsureBandsInitialized();
            var band = Bands[index];
            band.Enabled = enabled;
            Bands[index] = band;
        }

        public void ClearBand(int index)
        {
            if (index < 0 || index >= 16) return;
            EnsureBandsInitialized();
            var band = EQBandParams.Default();
            band.Enabled = false;
            band.Frequency = DefaultFrequencies[index];
            band.Gain = 0f;
            band.Q = 1f;
            band.Type = EQFilterType.Peak;
            Bands[index] = band;
        }
        // This is needed for visualization in DSPPanelController
        public float GetMagnitudeAtFrequency(float frequency, int sampleRate)
        {
            float totalMagnitude = 1f;

            // Using a temporary struct to avoid ref issues
            EQBandCoeffs coeffs = new EQBandCoeffs();

            for (int i = 0; i < 16; i++)
            {
                var band = Bands[i];
                if (!band.Enabled) continue;
                band.Type = SanitizeFilterType(band.Type);
                if (Mathf.Abs(band.Gain) < 0.01f && band.Type == EQFilterType.Peak)
                {
                    continue;
                }

                EQLogic.UpdateCoefficients(band, sampleRate, out coeffs);
                float bandMagnitude = EQLogic.GetBiquadMagnitude(coeffs, frequency, sampleRate);
                totalMagnitude *= Mathf.Max(bandMagnitude, 0.0001f);
            }

            return totalMagnitude * Mathf.Pow(10f, OutputGain / 20f);
        }
    }
}
