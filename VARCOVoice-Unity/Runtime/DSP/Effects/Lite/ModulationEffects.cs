using System;
using UnityEngine;

namespace VARCOVoice.DSP
{
    /// <summary>
    /// LFO waveform types for modulation effects
    /// </summary>
    public enum LFOWaveform
    {
        Sine,
        Triangle,
        Square,
        Sawtooth
    }
    
    /// <summary>
    /// Phaser effect using all-pass filters
    /// </summary>
    [Serializable]
    public class PhaserEffect : DSPEffectBase
    {
        public override string Name => "Phaser";
        
        /// <summary>
        /// Number of all-pass stages (2, 4, 6, 8, 12)
        /// </summary>
        public int Stages
        {
            get => _stages;
            set => _stages = Mathf.Clamp(value / 2 * 2, 2, 12); // Even numbers only
        }
        private int _stages = 4;
        
        /// <summary>
        /// LFO rate in Hz
        /// </summary>
        public float Rate { get; set; } = 0.5f;
        
        /// <summary>
        /// LFO depth (0-100%)
        /// </summary>
        public float Depth { get; set; } = 70f;
        
        /// <summary>
        /// Center frequency in Hz
        /// </summary>
        public float CenterFreq { get; set; } = 1000f;
        
        /// <summary>
        /// Frequency range in octaves
        /// </summary>
        public float FreqRange { get; set; } = 2f;
        
        /// <summary>
        /// Feedback amount (-100% to +100%)
        /// </summary>
        [field: Range(-100f, 100f)]
        public float Feedback { get; set; } = 30f;
        
        /// <summary>
        /// LFO waveform
        /// </summary>
        public LFOWaveform Waveform { get; set; } = LFOWaveform.Sine;
        
        
        /// <summary>
        /// Stereo phase offset in degrees
        /// </summary>
        public float StereoPhase { get; set; } = 90f;
        
        // State
        private float[] _allpassState; // Max 12 stages x 2 channels
        private float _lfoPhase;
        private QuadratureOscillator _lfo;  // Fast sin-free oscillator for Sine waveform
        private float[] _feedbackSamples;
        
        private const int MAX_STAGES = 12;
        private const int MAX_CHANNELS = 2;
        
        // Feedback filter states (per channel)
        private float[] _feedbackLPF;
        private float[] _dcBlockerState;
        
        public PhaserEffect()
        {
            _allpassState = new float[MAX_STAGES * MAX_CHANNELS];
            _feedbackSamples = new float[MAX_CHANNELS];
            _feedbackLPF = new float[MAX_CHANNELS];
            _dcBlockerState = new float[MAX_CHANNELS];
            _lfo = new QuadratureOscillator();
            _lfo.Init(0f);
        }
        
        public override void Process(float[] data, int channels, int sampleRate)
        {
            float rate = Mathf.Max(0f, Rate);
            float phaseIncrement = rate * Mathf.PI * 2f / sampleRate;
            _lfo.SetFrequency(rate, sampleRate);
            
            // Stage-dependent maximum feedback: More stages = more phase shift = lower safe feedback
            // 2 stages: 0.95, 4 stages: 0.85, 6 stages: 0.75, 8 stages: 0.65, 12 stages: 0.55
            float maxFeedback = Mathf.Lerp(0.95f, 0.45f, (_stages - 2f) / 10f);
            float feedback = Mathf.Clamp(Feedback / 100f, -maxFeedback, maxFeedback);
            
            float depth = Depth / 100f;
            float stereoPhaseRad = StereoPhase * Mathf.Deg2Rad;
            
            // One-pole LPF coefficient for feedback path (8kHz cutoff to tame HF oscillation)
            float fbLpfFreq = Mathf.Min(8000f, sampleRate * 0.4f);
            float fbLpfCoef = Mathf.Exp(-2f * Mathf.PI * fbLpfFreq / sampleRate);
            
            // DC blocker coefficient (20Hz highpass)
            float dcBlockCoef = 1f - Mathf.Exp(-2f * Mathf.PI * 20f / sampleRate);
            
            int samplesPerChannel = data.Length / channels;
            
            // Ensure feedback arrays are sized correctly
            if (_feedbackLPF == null || _feedbackLPF.Length < channels)
            {
                _feedbackLPF = new float[MAX_CHANNELS];
                _dcBlockerState = new float[MAX_CHANNELS];
            }
            
            for (int i = 0; i < samplesPerChannel; i++)
            {
                for (int ch = 0; ch < channels; ch++)
                {
                    int idx = i * channels + ch;
                    float sample = data[idx];
                    
                    // Stereo LFO offset
                    float lfoPhaseOffset = ch * stereoPhaseRad;
                    float lfo = GetLFOValue(_lfoPhase + lfoPhaseOffset) * depth;
                    
                    // Calculate notch frequency
                    float freqMod = Mathf.Pow(2f, lfo * FreqRange);
                    float notchFreq = CenterFreq * freqMod;
                    notchFreq = Mathf.Clamp(notchFreq, 100f, sampleRate * 0.4f);
                    
                    // All-pass coefficient (first-order)
                    float w0 = Mathf.PI * notchFreq / sampleRate;
                    float alpha = (1f - w0) / (1f + w0);
                    
                    // Apply one-pole LPF to feedback (tames high-frequency ringing)
                    float rawFeedback = _feedbackSamples[ch] * feedback;
                    _feedbackLPF[ch] = fbLpfCoef * _feedbackLPF[ch] + (1f - fbLpfCoef) * rawFeedback;
                    
                    // Soft saturation on feedback (tanh-like, prevents hard clipping pops)
                    float fbSat = _feedbackLPF[ch];
                    float absFb = Mathf.Abs(fbSat);
                    if (absFb > 0.5f)
                    {
                        // Soft knee saturation starting at 0.5, asymptotically approaching 1.0
                        float excess = absFb - 0.5f;
                        float compressed = 0.5f + excess / (1f + excess * 2f);
                        fbSat = Mathf.Sign(fbSat) * compressed;
                    }
                    
                    float input = sample + fbSat;
                    
                    // Process through all-pass cascade
                    float output = input;
                    for (int stage = 0; stage < _stages; stage++)
                    {
                        int stateIdx = stage * MAX_CHANNELS + ch;
                        float x = output;
                        float y = alpha * x + _allpassState[stateIdx];
                        _allpassState[stateIdx] = x - alpha * y;
                        output = y;
                    }
                    
                    // DC blocker (removes any DC buildup from feedback)
                    _dcBlockerState[ch] += dcBlockCoef * (output - _dcBlockerState[ch]);
                    float dcBlocked = output - _dcBlockerState[ch];
                    
                    // Store for feedback (before final limiting)
                    _feedbackSamples[ch] = DSPConstants.FlushDenormal(dcBlocked);
                    
                    // Final output limiting and mix
                    float wet = DSPConstants.SoftClip(dcBlocked);
                    data[idx] = sample * (1f - Mix) + wet * Mix;
                }
                
                // Advance LFO
                _lfoPhase += phaseIncrement;
                if (_lfoPhase >= Mathf.PI * 2f) _lfoPhase -= Mathf.PI * 2f;
                if (Waveform == LFOWaveform.Sine) _lfo.Next();
            }
            
            // Periodic denormal flush (every block instead of every sample for performance)
            for (int s = 0; s < _stages * MAX_CHANNELS; s++)
            {
                _allpassState[s] = DSPConstants.FlushDenormal(_allpassState[s]);
            }
        }

        public override void ApplyPreset(string presetName)
        {
            if (string.IsNullOrEmpty(presetName)) return;

            switch (presetName.Trim().ToLowerInvariant())
            {
                case "vintage 4-stage":
                    Stages = 4;
                    Rate = 0.2f;
                    Depth = 35f;
                    CenterFreq = 800f;
                    FreqRange = 2f;
                    Feedback = 20f;
                    Waveform = LFOWaveform.Sine;
                    StereoPhase = 90f;
                    Mix = 0.45f;
                    break;
                case "swoosh":
                    Stages = 6;
                    Rate = 0.8f;
                    Depth = 55f;
                    CenterFreq = 900f;
                    FreqRange = 3f;
                    Feedback = 30f;
                    Waveform = LFOWaveform.Sine;
                    StereoPhase = 90f;
                    Mix = 0.5f;
                    break;
                case "comb filter":
                    Stages = 12;
                    Rate = 0f;
                    Depth = 0f;
                    CenterFreq = 1000f;
                    FreqRange = 1f;
                    Feedback = 25f;
                    Waveform = LFOWaveform.Sine;
                    StereoPhase = 0f;
                    Mix = 0.35f;
                    break;
                case "spinning speaker":
                    Stages = 6;
                    Rate = 1.2f;
                    Depth = 45f;
                    CenterFreq = 700f;
                    FreqRange = 2.5f;
                    Feedback = 25f;
                    Waveform = LFOWaveform.Triangle;
                    StereoPhase = 120f;
                    Mix = 0.45f;
                    break;
                case "subtle movement":
                    Stages = 2;
                    Rate = 0.15f;
                    Depth = 25f;
                    CenterFreq = 1200f;
                    FreqRange = 1.5f;
                    Feedback = 10f;
                    Waveform = LFOWaveform.Sine;
                    StereoPhase = 90f;
                    Mix = 0.35f;
                    break;
                case "space phaser":
                    Stages = 8;
                    Rate = 0.4f;
                    Depth = 55f;
                    CenterFreq = 900f;
                    FreqRange = 3.5f;
                    Feedback = 25f;
                    Waveform = LFOWaveform.Sine;
                    StereoPhase = 180f;
                    Mix = 0.5f;
                    break;
                case "classic sweep":
                    Stages = 4;
                    Rate = 0.4f;
                    Depth = 45f;
                    CenterFreq = 800f;
                    FreqRange = 2f;
                    Feedback = 20f;
                    Waveform = LFOWaveform.Sine;
                    StereoPhase = 90f;
                    Mix = 0.45f;
                    break;
                case "deep sweep":
                    Stages = 8;
                    Rate = 0.25f;
                    Depth = 55f;
                    CenterFreq = 700f;
                    FreqRange = 2.5f;
                    Feedback = 25f;
                    Waveform = LFOWaveform.Sine;
                    StereoPhase = 120f;
                    Mix = 0.5f;
                    break;
                case "slow sweep":
                    Stages = 6;
                    Rate = 0.15f;
                    Depth = 40f;
                    CenterFreq = 900f;
                    FreqRange = 2f;
                    Feedback = 20f;
                    Waveform = LFOWaveform.Triangle;
                    StereoPhase = 90f;
                    Mix = 0.45f;
                    break;
            }
        }

        private float GetLFOValue(float phase)
        {
            phase = phase % (Mathf.PI * 2f);
            
            switch (Waveform)
            {
                case LFOWaveform.Sine:
                    return Mathf.Sin(phase);
                case LFOWaveform.Triangle:
                    float t = phase / (Mathf.PI * 2f);
                    return t < 0.5f ? 4f * t - 1f : 3f - 4f * t;
                case LFOWaveform.Square:
                    return phase < Mathf.PI ? 1f : -1f;
                case LFOWaveform.Sawtooth:
                    return 1f - 2f * phase / (Mathf.PI * 2f);
                default:
                    return Mathf.Sin(phase);
            }
        }
        
        public override void Reset()
        {
            Array.Clear(_allpassState, 0, _allpassState.Length);
            if (_feedbackSamples != null)
                Array.Clear(_feedbackSamples, 0, _feedbackSamples.Length);
            if (_feedbackLPF != null)
                Array.Clear(_feedbackLPF, 0, _feedbackLPF.Length);
            if (_dcBlockerState != null)
                Array.Clear(_dcBlockerState, 0, _dcBlockerState.Length);
            _lfoPhase = 0f;
            _lfo.Reset();
        }
    }
    
    /// <summary>
    /// Flanger effect using short modulated delay
    /// </summary>
    [Serializable]
    public class FlangerEffect : DSPEffectBase
    {
        public override string Name => "Flanger";
        
        /// <summary>
        /// LFO rate in Hz
        /// </summary>
        public float Rate { get; set; } = 0.3f;
        
        /// <summary>
        /// Base delay time in ms
        /// </summary>
        public float BaseDelay { get; set; } = 1f;
        
        /// <summary>
        /// Modulation depth in ms
        /// </summary>
        public float Depth { get; set; } = 2f;
        
        /// <summary>
        /// Feedback amount (-100% to +100%)
        /// </summary>
        [field: Range(-100f, 100f)]
        public float Feedback { get; set; } = 50f;
        
        /// <summary>
        /// LFO waveform
        /// </summary>
        public LFOWaveform Waveform { get; set; } = LFOWaveform.Sine;
        
        /// <summary>
        /// Stereo phase offset in degrees
        /// </summary>
        public float StereoPhase { get; set; } = 90f;
        
        // Buffers
        private float[] _leftBuffer;
        private float[] _rightBuffer;
        private int _bufferSize;
        private int _writePos;
        private float _lfoPhase;
        
        private int _lastSampleRate;
        private bool _initialized;
        
        public override void Process(float[] data, int channels, int sampleRate)
        {
            EnsureInitialized(sampleRate);
            
            float rate = Mathf.Max(0f, Rate);
            float phaseIncrement = rate * Mathf.PI * 2f / sampleRate;
            float feedback = Mathf.Clamp(Feedback / 100f, -0.95f, 0.95f);
            float stereoPhaseRad = StereoPhase * Mathf.Deg2Rad;
            float baseDelay = Mathf.Max(0f, BaseDelay);
            float depth = Mathf.Max(0f, Depth);
            if (depth > 10f || rate < 0.2f)
            {
                float depthScale = depth > 10f ? 0.85f : 1f;
                float rateScale = rate < 0.2f ? 0.85f : 1f;
                feedback *= depthScale * rateScale;
            }
            
            int samplesPerChannel = data.Length / channels;
            
            for (int i = 0; i < samplesPerChannel; i++)
            {
                float lfoL = GetLFOValue(_lfoPhase) * 0.5f + 0.5f; // 0 to 1
                float lfoR = GetLFOValue(_lfoPhase + stereoPhaseRad) * 0.5f + 0.5f;
                
                for (int ch = 0; ch < Mathf.Min(channels, 2); ch++)
                {
                    int idx = i * channels + ch;
                    float sample = data[idx];
                    
                    // Get buffer reference
                    float[] buffer = ch == 0 ? _leftBuffer : _rightBuffer;
                    float lfo = ch == 0 ? lfoL : lfoR;
                    
                    // Calculate delay time
                    float delayMs = baseDelay + lfo * depth;
                    float delaySamples = delayMs * sampleRate * 0.001f;  // Pre-calculated msToSamples
                    delaySamples = Mathf.Clamp(delaySamples, 1f, _bufferSize - 2);
                    
                    // Interpolated read
                    int baseIndex = (_writePos - (int)delaySamples + _bufferSize) % _bufferSize;
                    int indexM1 = (baseIndex + 1) % _bufferSize;
                    int index0 = baseIndex;
                    int index1 = (baseIndex - 1 + _bufferSize) % _bufferSize;
                    int index2 = (baseIndex - 2 + _bufferSize) % _bufferSize;
                    float frac = delaySamples - (int)delaySamples;

                    float delayed = DSPConstants.HermiteInterpolation(
                        buffer[indexM1],
                        buffer[index0],
                        buffer[index1],
                        buffer[index2],
                        frac);
                    
                    // Write with feedback and DC offset to prevent denormals   
                    float feedbackSample = DSPConstants.SoftClip(delayed * feedback);
                    buffer[_writePos] = sample + feedbackSample + DSPConstants.DC_OFFSET;
                    
                    // Mix (Standard: Dry * (1-Mix) + Wet * Mix)
                    // Apply SoftClip to wet signal
                    float wet = DSPConstants.SoftClip(delayed);
                    data[idx] = sample * (1f - Mix) + wet * Mix;
                }
                
                _writePos = (_writePos + 1) % _bufferSize;
                _lfoPhase += phaseIncrement;
                if (_lfoPhase >= Mathf.PI * 2f) _lfoPhase -= Mathf.PI * 2f;
            }
        }

        public override void ApplyPreset(string presetName)
        {
            if (string.IsNullOrEmpty(presetName)) return;

            switch (presetName.Trim().ToLowerInvariant())
            {
                case "jet plane":
                    Rate = 0.6f;
                    BaseDelay = 1.2f;
                    Depth = 12f;
                    Feedback = 55f;
                    Waveform = LFOWaveform.Sine;
                    StereoPhase = 90f;
                    Mix = 0.5f;
                    break;
                case "gentle sweep":
                    Rate = 0.2f;
                    BaseDelay = 1f;
                    Depth = 5f;
                    Feedback = 30f;
                    Waveform = LFOWaveform.Triangle;
                    StereoPhase = 90f;
                    Mix = 0.4f;
                    break;
                case "metallic":
                    Rate = 1.2f;
                    BaseDelay = 0.8f;
                    Depth = 7f;
                    Feedback = 60f;
                    Waveform = LFOWaveform.Triangle;
                    StereoPhase = 90f;
                    Mix = 0.5f;
                    break;
                case "barber pole":
                    Rate = 0.5f;
                    BaseDelay = 1f;
                    Depth = 10f;
                    Feedback = 45f;
                    Waveform = LFOWaveform.Sawtooth;
                    StereoPhase = 90f;
                    Mix = 0.5f;
                    break;
                case "vintage":
                    Rate = 0.25f;
                    BaseDelay = 1.5f;
                    Depth = 7f;
                    Feedback = 45f;
                    Waveform = LFOWaveform.Sine;
                    StereoPhase = 90f;
                    Mix = 0.5f;
                    break;
                case "extreme":
                    Rate = 1.0f;
                    BaseDelay = 0.5f;
                    Depth = 12f;
                    Feedback = 60f;
                    Waveform = LFOWaveform.Square;
                    StereoPhase = 90f;
                    Mix = 0.6f;
                    break;
            }
        }

        private float GetLFOValue(float phase)
        {
            phase = phase % (Mathf.PI * 2f);
            
            switch (Waveform)
            {
                case LFOWaveform.Sine:
                    return Mathf.Sin(phase);
                case LFOWaveform.Triangle:
                    float t = phase / (Mathf.PI * 2f);
                    return t < 0.5f ? 4f * t - 1f : 3f - 4f * t;
                case LFOWaveform.Square:
                    return phase < Mathf.PI ? 1f : -1f;
                case LFOWaveform.Sawtooth:
                    return 1f - 2f * phase / (Mathf.PI * 2f);
                default:
                    return Mathf.Sin(phase);
            }
        }
        
        private void EnsureInitialized(int sampleRate)
        {
            int requiredSize = (int)(25f * sampleRate / 1000f) + 1; // Max 25ms
            
            if (!_initialized || _lastSampleRate != sampleRate || _bufferSize < requiredSize)
            {
                _bufferSize = requiredSize;
                _leftBuffer = new float[_bufferSize];
                _rightBuffer = new float[_bufferSize];
                _writePos = 0;
                _lastSampleRate = sampleRate;
                _initialized = true;
            }
        }
        
        public override void Reset()
        {
            if (_leftBuffer != null) Array.Clear(_leftBuffer, 0, _leftBuffer.Length);
            if (_rightBuffer != null) Array.Clear(_rightBuffer, 0, _rightBuffer.Length);
            _writePos = 0;
            _lfoPhase = 0f;
        }
    }
    
    /// <summary>
    /// Tremolo effect (amplitude modulation)
    /// </summary>
    [Serializable]
    public class TremoloEffect : DSPEffectBase
    {
        public override string Name => "Tremolo";
        
        /// <summary>
        /// LFO rate in Hz
        /// </summary>
        public float Rate { get; set; } = 4f;
        
        /// <summary>
        /// Modulation depth (0-100%)
        /// </summary>
        public float Depth { get; set; } = 50f;
        
        /// <summary>
        /// LFO waveform
        /// </summary>
        public LFOWaveform Waveform { get; set; } = LFOWaveform.Sine;
        
        /// <summary>
        /// Stereo phase offset in degrees
        /// </summary>
        public float StereoPhase { get; set; } = 0f;
        
        private float _lfoPhase;
        
        public override void Process(float[] data, int channels, int sampleRate)
        {
            float phaseIncrement = Rate * Mathf.PI * 2f / sampleRate;
            float depth = Depth / 100f;
            float stereoPhaseRad = StereoPhase * Mathf.Deg2Rad;
            
            int samplesPerChannel = data.Length / channels;
            
            for (int i = 0; i < samplesPerChannel; i++)
            {
                for (int ch = 0; ch < channels; ch++)
                {
                    int idx = i * channels + ch;
                    
                    float lfoPhaseOffset = ch * stereoPhaseRad;
                    float lfo = GetLFOValue(_lfoPhase + lfoPhaseOffset);
                    
                    // Convert LFO to gain (0.5 - depth/2 to 0.5 + depth/2)
                    float gain = 1f - depth * (1f - lfo) * 0.5f;
                    
                    // Apply Mix to gain (Mix 0 = Gain 1, Mix 1 = Gain normal)
                    gain = Mathf.Lerp(1f, gain, Mix);

                    data[idx] *= gain;
                }
                
                _lfoPhase += phaseIncrement;
                if (_lfoPhase >= Mathf.PI * 2f) _lfoPhase -= Mathf.PI * 2f;
            }
        }
        
        private float GetLFOValue(float phase)
        {
            phase = phase % (Mathf.PI * 2f);
            
            switch (Waveform)
            {
                case LFOWaveform.Sine:
                    return Mathf.Sin(phase) * 0.5f + 0.5f;
                case LFOWaveform.Triangle:
                    float t = phase / (Mathf.PI * 2f);
                    return t < 0.5f ? 2f * t : 2f - 2f * t;
                case LFOWaveform.Square:
                    return phase < Mathf.PI ? 1f : 0f;
                case LFOWaveform.Sawtooth:
                    return 1f - phase / (Mathf.PI * 2f);
                default:
                    return Mathf.Sin(phase) * 0.5f + 0.5f;
            }
        }

        public override void ApplyPreset(string presetName)
        {
            if (string.IsNullOrEmpty(presetName)) return;

            switch (presetName.Trim().ToLowerInvariant())
            {
                case "slow pulse":
                    Rate = 1f;
                    Depth = 40f;
                    Waveform = LFOWaveform.Sine;
                    StereoPhase = 0f;
                    Mix = 1f;
                    break;
                case "fast chop":
                    Rate = 8f;
                    Depth = 80f;
                    Waveform = LFOWaveform.Square;
                    StereoPhase = 0f;
                    Mix = 1f;
                    break;
                case "swirl":
                    Rate = 4f;
                    Depth = 50f;
                    Waveform = LFOWaveform.Triangle;
                    StereoPhase = 180f;
                    Mix = 1f;
                    break;
                case "subtle":
                    Rate = 2f;
                    Depth = 20f;
                    Waveform = LFOWaveform.Sine;
                    StereoPhase = 0f;
                    Mix = 1f;
                    break;
                case "stereo pan":
                    Rate = 3f;
                    Depth = 60f;
                    Waveform = LFOWaveform.Sine;
                    StereoPhase = 180f;
                    Mix = 1f;
                    break;
            }
        }

        public override void Reset()
        {
            _lfoPhase = 0f;
        }
    }
    
    /// <summary>
    /// Ring modulator effect
    /// </summary>
    [Serializable]
    public class RingModulatorEffect : DSPEffectBase
    {
        public override string Name => "Ring Mod";
        
        /// <summary>
        /// Carrier frequency in Hz
        /// </summary>
        public float Frequency { get; set; } = 440f;
        
        /// <summary>
        /// Carrier waveform
        /// </summary>
        public LFOWaveform Waveform { get; set; } = LFOWaveform.Sine;
        
        private float _phase;
        
        public override void Process(float[] data, int channels, int sampleRate)
        {
            float phaseIncrement = Frequency * Mathf.PI * 2f / sampleRate;
            
            int samplesPerChannel = data.Length / channels;
            
            for (int i = 0; i < samplesPerChannel; i++)
            {
                float carrier = GetCarrierValue(_phase);
                
                for (int ch = 0; ch < channels; ch++)
                {
                    int idx = i * channels + ch;
                    float modulated = data[idx] * carrier;
                    data[idx] = data[idx] * (1f - Mix) + modulated * Mix;
                }
                
                _phase += phaseIncrement;
                if (_phase >= Mathf.PI * 2f) _phase -= Mathf.PI * 2f;
            }
        }
        
        private float GetCarrierValue(float phase)
        {
            switch (Waveform)
            {
                case LFOWaveform.Sine:
                    return Mathf.Sin(phase);
                case LFOWaveform.Triangle:
                    float t = phase / (Mathf.PI * 2f);
                    return t < 0.5f ? 4f * t - 1f : 3f - 4f * t;
                case LFOWaveform.Square:
                    return phase < Mathf.PI ? 1f : -1f;
                case LFOWaveform.Sawtooth:
                    return 1f - 2f * phase / (Mathf.PI * 2f);
                default:
                    return Mathf.Sin(phase);
            }
        }

        public override void ApplyPreset(string presetName)
        {
            if (string.IsNullOrEmpty(presetName)) return;

            switch (presetName.Trim().ToLowerInvariant())
            {
                case "robot":
                    Frequency = 90f;
                    Waveform = LFOWaveform.Sine;
                    Mix = 0.7f;
                    break;
                case "metallic":
                    Frequency = 180f;
                    Waveform = LFOWaveform.Sine;
                    Mix = 0.8f;
                    break;
                case "am radio":
                    Frequency = 120f;
                    Waveform = LFOWaveform.Square;
                    Mix = 0.6f;
                    break;
                case "alien":
                    Frequency = 250f;
                    Waveform = LFOWaveform.Sawtooth;
                    Mix = 0.6f;
                    break;
                case "subtle":
                    Frequency = 30f;
                    Waveform = LFOWaveform.Sine;
                    Mix = 0.3f;
                    break;
            }
        }

        public override void Reset()
        {
            _phase = 0f;
        }
    }
}
