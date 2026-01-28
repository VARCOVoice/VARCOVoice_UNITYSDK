// 레거시 코드입니다. 더이상 사용하지 않습니다.
using System;
using UnityEngine;

namespace VARCOVoice.DSP
{
    /// <summary>
    /// Noise gate effect
    /// </summary>
    [Serializable]
    public class GateEffect : DSPEffectBase
    {
        public override string Name => "Gate";
        
        /// <summary>
        /// Threshold in dB
        /// </summary>
        public float Threshold { get; set; } = -40f;
        
        /// <summary>
        /// Attack time in ms
        /// </summary>
        public float Attack { get; set; } = 1f;
        
        /// <summary>
        /// Hold time in ms
        /// </summary>
        public float Hold { get; set; } = 50f;
        
        /// <summary>
        /// Release time in ms
        /// </summary>
        public float Release { get; set; } = 100f;
        
        /// <summary>
        /// Range/depth in dB (how much to attenuate when closed)
        /// </summary>
        public float Range { get; set; } = -80f;
        
        // State
        private float _envelope;
        private float _gateGain;
        private float _holdCounter;
        private bool _gateOpen;
        
        public override void Process(float[] data, int channels, int sampleRate)
        {
            float thresholdLinear = Mathf.Pow(10f, Threshold / 20f);
            float rangeLinear = Mathf.Pow(10f, Range / 20f);
            float attackCoef = Mathf.Exp(-1f / (Attack * sampleRate / 1000f));
            float releaseCoef = Mathf.Exp(-1f / (Release * sampleRate / 1000f));
            float holdSamples = Hold * sampleRate / 1000f;
            
            int samplesPerChannel = data.Length / channels;
            
            for (int i = 0; i < samplesPerChannel; i++)
            {
                // Get peak level from all channels
                float peak = 0f;
                for (int ch = 0; ch < channels; ch++)
                {
                    peak = Mathf.Max(peak, Mathf.Abs(data[i * channels + ch]));
                }
                
                // Envelope follower
                if (peak > _envelope)
                    _envelope = attackCoef * _envelope + (1f - attackCoef) * peak;
                else
                    _envelope = releaseCoef * _envelope;
                
                // Gate logic
                if (_envelope > thresholdLinear)
                {
                    _gateOpen = true;
                    _holdCounter = holdSamples;
                }
                else if (_holdCounter > 0)
                {
                    _holdCounter--;
                }
                else
                {
                    _gateOpen = false;
                }
                
                // Smooth gate gain
                float targetGain = _gateOpen ? 1f : rangeLinear;
                float coef = _gateOpen ? attackCoef : releaseCoef;
                _gateGain = coef * _gateGain + (1f - coef) * targetGain;
                
                // Apply gain
                for (int ch = 0; ch < channels; ch++)
                {
                    data[i * channels + ch] *= _gateGain;
                }
            }
        }

        public override void ApplyPreset(string presetName)
        {
            if (string.IsNullOrEmpty(presetName)) return;

            switch (presetName.Trim().ToLowerInvariant())
            {
                case "voice tight":
                    Threshold = -35f;
                    Attack = 5f;
                    Hold = 60f;
                    Release = 120f;
                    Range = -60f;
                    Mix = 1f;
                    break;
                case "noise clean":
                    Threshold = -45f;
                    Attack = 2f;
                    Hold = 100f;
                    Release = 200f;
                    Range = -80f;
                    Mix = 1f;
                    break;
                case "fast cut":
                    Threshold = -30f;
                    Attack = 0.5f;
                    Hold = 20f;
                    Release = 80f;
                    Range = -80f;
                    Mix = 1f;
                    break;
                case "room gate":
                    Threshold = -40f;
                    Attack = 5f;
                    Hold = 150f;
                    Release = 300f;
                    Range = -40f;
                    Mix = 1f;
                    break;
            }
        }

        public override void Reset()
        {
            _envelope = 0f;
            _gateGain = 1f;
            _holdCounter = 0f;
            _gateOpen = false;
        }
    }
    
    /// <summary>
    /// Expander/Downward expander effect
    /// </summary>
    [Serializable]
    public class ExpanderEffect : DSPEffectBase
    {
        public override string Name => "Expander";
        
        /// <summary>
        /// Threshold in dB
        /// </summary>
        public float Threshold { get; set; } = -30f;
        
        /// <summary>
        /// Expansion ratio (1:1 to 1:10)
        /// </summary>
        public float Ratio { get; set; } = 2f;
        
        /// <summary>
        /// Attack time in ms
        /// </summary>
        public float Attack { get; set; } = 5f;
        
        /// <summary>
        /// Release time in ms
        /// </summary>
        public float Release { get; set; } = 100f;
        
        /// <summary>
        /// Knee width in dB
        /// </summary>
        public float Knee { get; set; } = 6f;
        
        // State
        private float _envelope;
        
        public override void Process(float[] data, int channels, int sampleRate)
        {
            float attackCoef = Mathf.Exp(-1f / (Attack * sampleRate / 1000f));
            float releaseCoef = Mathf.Exp(-1f / (Release * sampleRate / 1000f));
            
            int samplesPerChannel = data.Length / channels;
            
            for (int i = 0; i < samplesPerChannel; i++)
            {
                // Get peak level
                float peak = 0f;
                for (int ch = 0; ch < channels; ch++)
                {
                    peak = Mathf.Max(peak, Mathf.Abs(data[i * channels + ch]));
                }
                
                // Convert to dB
                float peakDb = peak > 0.00001f ? 20f * Mathf.Log10(peak) : -100f;
                
                // Envelope follower in dB domain
                float coef = peakDb > _envelope ? attackCoef : releaseCoef;
                _envelope = coef * _envelope + (1f - coef) * peakDb;
                
                // Calculate gain reduction
                float gainDb = CalculateExpansion(_envelope);
                float gain = Mathf.Pow(10f, gainDb / 20f);
                
                // Apply gain
                for (int ch = 0; ch < channels; ch++)
                {
                    data[i * channels + ch] *= gain;
                }
            }
        }
        
        private float CalculateExpansion(float inputDb)
        {
            float halfKnee = Knee / 2f;
            
            if (inputDb >= Threshold)
            {
                // Above threshold: no expansion
                return 0f;
            }
            else if (inputDb > Threshold - Knee)
            {
                // Soft knee region
                float x = inputDb - Threshold + halfKnee;
                float y = x * x / (2f * Knee);
                return -(1f - 1f / Ratio) * y;
            }
            else
            {
                // Below threshold: full expansion
                float excess = Threshold - inputDb;
                return -excess * (1f - 1f / Ratio);
            }
        }

        public override void ApplyPreset(string presetName)
        {
            if (string.IsNullOrEmpty(presetName)) return;

            switch (presetName.Trim().ToLowerInvariant())
            {
                case "gentle":
                    Threshold = -35f;
                    Ratio = 1.5f;
                    Attack = 10f;
                    Release = 150f;
                    Knee = 6f;
                    Mix = 1f;
                    break;
                case "de-noise":
                    Threshold = -45f;
                    Ratio = 3f;
                    Attack = 5f;
                    Release = 200f;
                    Knee = 8f;
                    Mix = 1f;
                    break;
                case "broadcast":
                    Threshold = -30f;
                    Ratio = 2.5f;
                    Attack = 5f;
                    Release = 120f;
                    Knee = 4f;
                    Mix = 1f;
                    break;
                case "pump":
                    Threshold = -25f;
                    Ratio = 4f;
                    Attack = 2f;
                    Release = 80f;
                    Knee = 2f;
                    Mix = 1f;
                    break;
            }
        }

        public override void Reset()
        {
            _envelope = -100f;
        }
    }
    
    /// <summary>
    /// Professional compressor with sidechain filter
    /// </summary>
    [Serializable]
    public class CompressorEffect : DSPEffectBase
    {
        public override string Name => "Compressor";
        
        /// <summary>
        /// Threshold in dB
        /// </summary>
        public float Threshold { get; set; } = -20f;
        
        /// <summary>
        /// Compression ratio (1:1 to 20:1)
        /// </summary>
        public float Ratio { get; set; } = 4f;
        
        /// <summary>
        /// Attack time in ms
        /// </summary>
        public float Attack { get; set; } = 10f;
        
        /// <summary>
        /// Release time in ms
        /// </summary>
        public float Release { get; set; } = 200f;
        
        /// <summary>
        /// Knee width in dB (0 = hard knee)
        /// </summary>
        public float Knee { get; set; } = 6f;
        
        /// <summary>
        /// Makeup gain in dB
        /// </summary>
        public float MakeupGain { get; set; } = 0f;
        
        /// <summary>
        /// Auto makeup gain
        /// </summary>
        public bool AutoMakeup { get; set; } = true;
        
        /// <summary>
        /// Sidechain high-pass filter frequency
        /// </summary>
        public float SidechainHPF { get; set; } = 20f;
        
        // State
        private float _envelope;
        private float _scHpfState;
        
        /// <summary>
        /// Current input level in dB (for metering)
        /// </summary>
        public float CurrentInput { get; private set; } = -100f;

        /// <summary>
        /// Current output level in dB (for metering)
        /// </summary>
        public float CurrentOutput { get; private set; } = -100f;

        /// <summary>
        /// Current gain reduction in dB (for metering)
        /// </summary>
        public float CurrentGainReduction { get; private set; }

        public override void Process(float[] data, int channels, int sampleRate)
        {
            float attackCoef = Mathf.Exp(-1f / (Attack * sampleRate / 1000f));
            float releaseCoef = Mathf.Exp(-1f / (Release * sampleRate / 1000f));
            float scHpfCoef = 1f - Mathf.Exp(-2f * Mathf.PI * SidechainHPF / sampleRate);
            
            // Calculate auto makeup if enabled
            float makeupDb = MakeupGain;
            if (AutoMakeup)
            {
                // Rough estimate based on threshold and ratio
                makeupDb += -Threshold * (1f - 1f / Ratio) * 0.5f;
            }
            float makeupLinear = Mathf.Pow(10f, makeupDb / 20f);
            
            int samplesPerChannel = data.Length / channels;
            
            float maxInput = 0f;
            float maxOutput = 0f;
            float maxGR = 0f;

            for (int i = 0; i < samplesPerChannel; i++)
            {
                // Get sidechain signal (mono sum)
                float sidechain = 0f;
                // Get max input for this sample frame
                float currentInputMax = 0f;

                for (int ch = 0; ch < channels; ch++)
                {
                    float val = data[i * channels + ch];
                    sidechain += val;
                    float absVal = Mathf.Abs(val);
                    if (absVal > currentInputMax) currentInputMax = absVal;
                }
                if (currentInputMax > maxInput) maxInput = currentInputMax;

                sidechain /= channels;
                
                // Sidechain HPF
                _scHpfState += scHpfCoef * (sidechain - _scHpfState);
                sidechain -= _scHpfState;
                
                // Get level in dB
                float level = Mathf.Abs(sidechain);
                float levelDb = level > 0.00001f ? 20f * Mathf.Log10(level) : -100f;
                
                // Envelope follower
                float coef = levelDb > _envelope ? attackCoef : releaseCoef;
                _envelope = coef * _envelope + (1f - coef) * levelDb;
                
                // Calculate gain reduction
                float gainReductionDb = CalculateCompression(_envelope);
                if (gainReductionDb < maxGR) maxGR = gainReductionDb;

                float gain = Mathf.Pow(10f, gainReductionDb / 20f) * makeupLinear;
                
                // Apply gain
                for (int ch = 0; ch < channels; ch++)
                {
                    int idx = i * channels + ch;
                    data[idx] *= gain;
                    float absOut = Mathf.Abs(data[idx]);
                    if (absOut > maxOutput) maxOutput = absOut;
                }
            }

            // Update meters (decaying peak)
            float inputDb = maxInput > 0.00001f ? 20f * Mathf.Log10(maxInput) : -100f;
            float outputDb = maxOutput > 0.00001f ? 20f * Mathf.Log10(maxOutput) : -100f;

            // Smooth updates
            CurrentInput = inputDb > CurrentInput ? inputDb : Mathf.Lerp(CurrentInput, inputDb, 0.1f);
            CurrentOutput = outputDb > CurrentOutput ? outputDb : Mathf.Lerp(CurrentOutput, outputDb, 0.1f);
            CurrentGainReduction = maxGR; // GR is negative
        }
        
        private float CalculateCompression(float inputDb)
        {
            float halfKnee = Knee / 2f;
            
            if (inputDb <= Threshold - halfKnee)
            {
                // Below threshold: no compression
                return 0f;
            }
            else if (inputDb >= Threshold + halfKnee)
            {
                // Above threshold: full compression
                float excess = inputDb - Threshold;
                return -excess * (1f - 1f / Ratio);
            }
            else
            {
                // Soft knee region
                float x = inputDb - Threshold + halfKnee;
                float y = x * x / (2f * Knee);
                return -y * (1f - 1f / Ratio);
            }
        }
        
        public override void ApplyPreset(string presetName)
        {
            if (string.IsNullOrEmpty(presetName)) return;

            switch (presetName.Trim().ToLowerInvariant())
            {
                case "vocal warmth":
                    Threshold = -20f;
                    Ratio = 3f;
                    Attack = 10f;
                    Release = 100f;
                    Knee = 6f;
                    MakeupGain = 6f;
                    AutoMakeup = false;
                    SidechainHPF = 60f;
                    Mix = 1f;
                    break;
                case "podcast master":
                    Threshold = -18f;
                    Ratio = 4f;
                    Attack = 5f;
                    Release = 80f;
                    Knee = 3f;
                    MakeupGain = 8f;
                    AutoMakeup = false;
                    SidechainHPF = 80f;
                    Mix = 1f;
                    break;
                case "transparent":
                    Threshold = -24f;
                    Ratio = 2f;
                    Attack = 20f;
                    Release = 250f;
                    Knee = 12f;
                    MakeupGain = 3f;
                    AutoMakeup = false;
                    SidechainHPF = 50f;
                    Mix = 1f;
                    break;
                case "punchy":
                    Threshold = -16f;
                    Ratio = 6f;
                    Attack = 3f;
                    Release = 80f;
                    Knee = 2f;
                    MakeupGain = 4f;
                    AutoMakeup = false;
                    SidechainHPF = 70f;
                    Mix = 1f;
                    break;
                case "glue":
                    Threshold = -22f;
                    Ratio = 2f;
                    Attack = 30f;
                    Release = 300f;
                    Knee = 9f;
                    MakeupGain = 2f;
                    AutoMakeup = false;
                    SidechainHPF = 60f;
                    Mix = 1f;
                    break;
                case "de-breath":
                    Threshold = -35f;
                    Ratio = 4f;
                    Attack = 1f;
                    Release = 120f;
                    Knee = 2f;
                    MakeupGain = 0f;
                    AutoMakeup = false;
                    SidechainHPF = 120f;
                    Mix = 1f;
                    break;
                case "safety limiter":
                    Threshold = -8f;
                    Ratio = 20f;
                    Attack = 0.5f;
                    Release = 100f;
                    Knee = 0f;
                    MakeupGain = 0f;
                    AutoMakeup = false;
                    SidechainHPF = 20f;
                    Mix = 1f;
                    break;
            }
        }

        public override void Reset()
        {
            _envelope = -100f;
            _scHpfState = 0f;
            CurrentGainReduction = 0f;
        }
    }
    
    /// <summary>
    /// 3-band multiband compressor
    /// </summary>
    [Serializable]
    public class MultibandCompressor : DSPEffectBase
    {
        public override string Name => "Multiband Comp";
        
        /// <summary>
        /// Low/Mid crossover frequency
        /// </summary>
        public float LowCrossover { get; set; } = 200f;
        
        /// <summary>
        /// Mid/High crossover frequency
        /// </summary>
        public float HighCrossover { get; set; } = 4000f;
        
        // Band thresholds
        public float LowThreshold { get; set; } = -20f;
        public float MidThreshold { get; set; } = -20f;
        public float HighThreshold { get; set; } = -20f;
        
        // Band ratios
        public float LowRatio { get; set; } = 4f;
        public float MidRatio { get; set; } = 3f;
        public float HighRatio { get; set; } = 3f;
        
        // Band gains
        public float LowGain { get; set; } = 0f;
        public float MidGain { get; set; } = 0f;
        public float HighGain { get; set; } = 0f;
        
        // Filter states (per channel, assuming stereo max)
        private float[] _lowLp1 = new float[2];
        private float[] _lowLp2 = new float[2];
        private float[] _highHp1 = new float[2];
        private float[] _highHp2 = new float[2];
        
        // Envelope states per band
        private float[] _envLow = new float[2];
        private float[] _envMid = new float[2];
        private float[] _envHigh = new float[2];
        
        private int _lastSampleRate;
        
        public override void Process(float[] data, int channels, int sampleRate)
        {
            channels = Mathf.Min(channels, 2); // Max stereo
            
            float lowCoef = Mathf.Exp(-2f * Mathf.PI * LowCrossover / sampleRate);
            float highCoef = 1f - Mathf.Exp(-2f * Mathf.PI * HighCrossover / sampleRate);
            float attackCoef = Mathf.Exp(-1f / (10f * sampleRate / 1000f)); // 10ms attack
            float releaseCoef = Mathf.Exp(-1f / (100f * sampleRate / 1000f)); // 100ms release
            
            float lowGainLinear = Mathf.Pow(10f, LowGain / 20f);
            float midGainLinear = Mathf.Pow(10f, MidGain / 20f);
            float highGainLinear = Mathf.Pow(10f, HighGain / 20f);
            
            int samplesPerChannel = data.Length / channels;
            
            for (int i = 0; i < samplesPerChannel; i++)
            {
                for (int ch = 0; ch < channels; ch++)
                {
                    int idx = i * channels + ch;
                    float sample = data[idx];
                    
                    // Band splitting using Linkwitz-Riley style
                    // Low band
                    _lowLp1[ch] = lowCoef * _lowLp1[ch] + (1f - lowCoef) * sample;
                    _lowLp2[ch] = lowCoef * _lowLp2[ch] + (1f - lowCoef) * _lowLp1[ch];
                    float low = _lowLp2[ch];
                    
                    // High band  
                    _highHp1[ch] += highCoef * (sample - _highHp1[ch]);
                    float highInput = sample - _highHp1[ch];
                    _highHp2[ch] += highCoef * (highInput - _highHp2[ch]);
                    float high = highInput - _highHp2[ch];
                    
                    // Mid band (what's left)
                    float mid = sample - low - high;
                    
                    // Compress each band
                    low = CompressBand(low, LowThreshold, LowRatio, ref _envLow[ch], attackCoef, releaseCoef) * lowGainLinear;
                    mid = CompressBand(mid, MidThreshold, MidRatio, ref _envMid[ch], attackCoef, releaseCoef) * midGainLinear;
                    high = CompressBand(high, HighThreshold, HighRatio, ref _envHigh[ch], attackCoef, releaseCoef) * highGainLinear;
                    
                    // Recombine
                    data[idx] = low + mid + high;
                }
            }
        }
        
        private float CompressBand(float sample, float threshold, float ratio, ref float envelope, float attack, float release)
        {
            float level = Mathf.Abs(sample);
            float levelDb = level > 0.00001f ? 20f * Mathf.Log10(level) : -100f;
            
            float coef = levelDb > envelope ? attack : release;
            envelope = coef * envelope + (1f - coef) * levelDb;
            
            float gainDb = 0f;
            if (envelope > threshold)
            {
                float excess = envelope - threshold;
                gainDb = -excess * (1f - 1f / ratio);
            }

            return sample * Mathf.Pow(10f, gainDb / 20f);
        }

        public override void ApplyPreset(string presetName)
        {
            if (string.IsNullOrEmpty(presetName)) return;

            switch (presetName.Trim().ToLowerInvariant())
            {
                case "vocal balance":
                    LowCrossover = 200f;
                    HighCrossover = 4000f;
                    LowThreshold = -22f;
                    MidThreshold = -20f;
                    HighThreshold = -18f;
                    LowRatio = 3f;
                    MidRatio = 2.5f;
                    HighRatio = 2.5f;
                    LowGain = 0f;
                    MidGain = 1f;
                    HighGain = 1f;
                    Mix = 1f;
                    break;
                case "bass control":
                    LowCrossover = 180f;
                    HighCrossover = 3500f;
                    LowThreshold = -28f;
                    MidThreshold = -20f;
                    HighThreshold = -18f;
                    LowRatio = 4f;
                    MidRatio = 2.5f;
                    HighRatio = 2f;
                    LowGain = 2f;
                    MidGain = 0f;
                    HighGain = 0f;
                    Mix = 1f;
                    break;
                case "air tame":
                    LowCrossover = 220f;
                    HighCrossover = 5000f;
                    LowThreshold = -20f;
                    MidThreshold = -20f;
                    HighThreshold = -24f;
                    LowRatio = 2.5f;
                    MidRatio = 2.5f;
                    HighRatio = 4f;
                    LowGain = 0f;
                    MidGain = 0f;
                    HighGain = -1f;
                    Mix = 1f;
                    break;
                case "mix glue":
                    LowCrossover = 250f;
                    HighCrossover = 4500f;
                    LowThreshold = -18f;
                    MidThreshold = -18f;
                    HighThreshold = -18f;
                    LowRatio = 2f;
                    MidRatio = 2f;
                    HighRatio = 2f;
                    LowGain = 0f;
                    MidGain = 0f;
                    HighGain = 0f;
                    Mix = 1f;
                    break;
                case "punch":
                    LowCrossover = 160f;
                    HighCrossover = 5500f;
                    LowThreshold = -24f;
                    MidThreshold = -18f;
                    HighThreshold = -20f;
                    LowRatio = 3.5f;
                    MidRatio = 2.5f;
                    HighRatio = 3f;
                    LowGain = 1f;
                    MidGain = 1f;
                    HighGain = 0.5f;
                    Mix = 1f;
                    break;
            }
        }

        public override void Reset()
        {
            Array.Clear(_lowLp1, 0, _lowLp1.Length);
            Array.Clear(_lowLp2, 0, _lowLp2.Length);
            Array.Clear(_highHp1, 0, _highHp1.Length);
            Array.Clear(_highHp2, 0, _highHp2.Length);
            Array.Clear(_envLow, 0, _envLow.Length);
            Array.Clear(_envMid, 0, _envMid.Length);
            Array.Clear(_envHigh, 0, _envHigh.Length);
        }
    }
    
    /// <summary>
    /// True peak limiter with lookahead
    /// </summary>
    [Serializable]
    public class LimiterEffect : DSPEffectBase
    {
        public override string Name => "Limiter";
        
        /// <summary>
        /// Ceiling/threshold in dB
        /// </summary>
        public float Ceiling { get; set; } = -0.3f;
        
        /// <summary>
        /// Release time in ms
        /// </summary>
        public float Release { get; set; } = 100f;
        
        /// <summary>
        /// Lookahead time in ms (adds latency)
        /// </summary>
        public float Lookahead { get; set; } = 5f;
        
        /// <summary>
        /// True peak detection (oversampled)
        /// </summary>
        public bool TruePeak { get; set; } = true;
        
        // State
        private float[] _lookBuffer;
        private int _lookBufferSize;
        private int _writePos;
        private float _gainReduction;
        
        private int _lastSampleRate;
        private bool _initialized;
        
        /// <summary>
        /// Current gain reduction in dB (for metering)
        /// </summary>
        public float CurrentGainReduction => 20f * Mathf.Log10(Mathf.Max(_gainReduction, 0.0001f));
        
        public override void Process(float[] data, int channels, int sampleRate)
        {
            EnsureInitialized(sampleRate);
            
            float ceilingLinear = Mathf.Pow(10f, Ceiling / 20f);
            float releaseCoef = Mathf.Exp(-1f / (Release * sampleRate / 1000f));
            int lookaheadSamples = (int)(Lookahead * sampleRate / 1000f);
            lookaheadSamples = Mathf.Clamp(lookaheadSamples, 0, _lookBufferSize - 1);
            
            int samplesPerChannel = data.Length / channels;
            
            for (int i = 0; i < samplesPerChannel; i++)
            {
                // Find peak across channels
                float peak = 0f;
                for (int ch = 0; ch < channels; ch++)
                {
                    float sample = data[i * channels + ch];
                    
                    // True peak detection (simple 2x oversampling approximation)
                    if (TruePeak && ch < 2)
                    {
                        int prevIdx = ((i - 1) * channels + ch + data.Length) % data.Length;
                        float interpolated = (sample + data[prevIdx]) * 0.5f * 1.1f; // Approximate intersample peak
                        peak = Mathf.Max(peak, Mathf.Abs(interpolated));
                    }
                    
                    peak = Mathf.Max(peak, Mathf.Abs(sample));
                }
                
                // Store in lookahead buffer
                _lookBuffer[_writePos] = peak;
                
                // Find max in lookahead window
                float maxInWindow = 0f;
                for (int j = 0; j <= lookaheadSamples; j++)
                {
                    int idx = (_writePos - j + _lookBufferSize) % _lookBufferSize;
                    maxInWindow = Mathf.Max(maxInWindow, _lookBuffer[idx]);
                }
                
                _writePos = (_writePos + 1) % _lookBufferSize;
                
                // Calculate required gain
                float targetGain = 1f;
                if (maxInWindow > ceilingLinear)
                {
                    targetGain = ceilingLinear / maxInWindow;
                }
                
                // Smooth gain (instant attack, smooth release)
                if (targetGain < _gainReduction)
                {
                    _gainReduction = targetGain; // Instant attack
                }
                else
                {
                    _gainReduction = releaseCoef * _gainReduction + (1f - releaseCoef) * targetGain;
                }
                
                // Apply gain
                for (int ch = 0; ch < channels; ch++)
                {
                    data[i * channels + ch] *= _gainReduction;
                }
            }
        }

        public override void ApplyPreset(string presetName)
        {
            if (string.IsNullOrEmpty(presetName)) return;

            switch (presetName.Trim().ToLowerInvariant())
            {
                case "mastering":
                    Ceiling = -0.3f;
                    Release = 80f;
                    Lookahead = 5f;
                    TruePeak = true;
                    Mix = 1f;
                    break;
                case "podcast":
                    Ceiling = -1f;
                    Release = 60f;
                    Lookahead = 4f;
                    TruePeak = true;
                    Mix = 1f;
                    break;
                case "safety":
                    Ceiling = -0.1f;
                    Release = 120f;
                    Lookahead = 5f;
                    TruePeak = true;
                    Mix = 1f;
                    break;
                case "loud":
                    Ceiling = -0.5f;
                    Release = 40f;
                    Lookahead = 5f;
                    TruePeak = true;
                    Mix = 1f;
                    break;
            }
        }

        private void EnsureInitialized(int sampleRate)
        {
            int requiredSize = (int)(15f * sampleRate / 1000f) + 1; // Max 15ms lookahead
            
            if (!_initialized || _lastSampleRate != sampleRate || _lookBufferSize < requiredSize)
            {
                _lookBufferSize = requiredSize;
                _lookBuffer = new float[_lookBufferSize];
                _writePos = 0;
                _gainReduction = 1f;
                _lastSampleRate = sampleRate;
                _initialized = true;
            }
        }
        
        public override void Reset()
        {
            if (_lookBuffer != null)
                Array.Clear(_lookBuffer, 0, _lookBuffer.Length);
            _writePos = 0;
            _gainReduction = 1f;
        }
    }
}
