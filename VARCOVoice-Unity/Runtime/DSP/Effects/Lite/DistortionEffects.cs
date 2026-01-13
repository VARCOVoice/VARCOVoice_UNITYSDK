using System;
using UnityEngine;

namespace VARCOVoice.DSP
{
    /// <summary>
    /// Distortion type enumeration
    /// </summary>
    public enum DistortionType
    {
        SoftClip,
        HardClip,
        Tube,
        Tape,
        Fuzz,
        Bitcrusher
    }
    
    /// <summary>
    /// Multi-algorithm distortion/saturation effect
    /// </summary>
    [Serializable]
    public class DistortionEffect : DSPEffectBase
    {
        public override string Name => "Distortion";
        
        /// <summary>
        /// Distortion algorithm type
        /// </summary>
        public DistortionType Type { get; set; } = DistortionType.SoftClip;
        
        /// <summary>
        /// Drive amount (0-100)
        /// </summary>
        public float Drive { get; set; } = 50f;
        
        /// <summary>
        /// Tone control (low-pass filter frequency)
        /// </summary>
        public float Tone { get; set; } = 8000f;
        
        /// <summary>
        /// Output level compensation
        /// </summary>
        public float OutputGain { get; set; } = 0f;
        
        // Bitcrusher specific
        /// <summary>
        /// Bit depth for bitcrusher (1-16)
        /// </summary>
        public int BitDepth { get; set; } = 8;
        
        /// <summary>
        /// Sample rate reduction for bitcrusher
        /// </summary>
        public int SampleRateReduction { get; set; } = 1;
        
        // Filter state
        private float _lpfState;
        private float _lastSample;
        private int _sampleCounter;
        
        // LUT for performance
        private static readonly float[] _dbToLinear = PrecomputeDbTable();
        
        private static float[] PrecomputeDbTable()
        {
            var table = new float[73]; // -36 to +36 dB
            for (int i = 0; i < table.Length; i++)
            {
                float db = i - 36f;
                table[i] = Mathf.Pow(10f, db / 20f);
            }
            return table;
        }
        
        private float DbToLinear(float db)
        {
            int idx = Mathf.Clamp((int)(db + 36f), 0, 72);
            return _dbToLinear[idx];
        }
        
        public override void Process(float[] data, int channels, int sampleRate)
        {
            float driveAmount = 1f + Drive * 0.1f; // 1x to 11x gain
            float outputLevel = DbToLinear(OutputGain);
            float lpfCoef = Mathf.Exp(-2f * Mathf.PI * Tone / sampleRate);
            
            for (int i = 0; i < data.Length; i++)
            {
                float sample = data[i];
                
                // Apply drive
                sample *= driveAmount;
                
                // Apply distortion based on type
                sample = ApplyDistortion(sample);
                
                // Bitcrusher sample rate reduction
                if (Type == DistortionType.Bitcrusher && SampleRateReduction > 1)
                {
                    _sampleCounter++;
                    if (_sampleCounter >= SampleRateReduction)
                    {
                        _lastSample = sample;
                        _sampleCounter = 0;
                    }
                    sample = _lastSample;
                }
                
                // Tone filter (low-pass)
                _lpfState = lpfCoef * _lpfState + (1f - lpfCoef) * sample;
                sample = _lpfState;
                
                // Output gain and mix
                sample *= outputLevel;
                data[i] = data[i] * (1f - Mix) + sample * Mix;
            }
        }
        
        private float ApplyDistortion(float x)
        {
            switch (Type)
            {
                case DistortionType.SoftClip:
                    // Hyperbolic tangent soft clipper
                    return (float)Math.Tanh(x);
                    
                case DistortionType.HardClip:
                    // Hard clipper at ±1
                    return Mathf.Clamp(x, -1f, 1f);
                    
                case DistortionType.Tube:
                    // Tube-style asymmetric saturation
                    if (x >= 0)
                        return 1f - Mathf.Exp(-x);
                    else
                        return -1f + Mathf.Exp(x);
                        
                case DistortionType.Tape:
                    // Tape saturation (soft knee)
                    float absX = Mathf.Abs(x);
                    if (absX < 0.5f)
                        return x;
                    else if (absX < 1.5f)
                        return Mathf.Sign(x) * (3f * absX - absX * absX) / 2f;
                    else
                        return Mathf.Sign(x);
                        
                case DistortionType.Fuzz:
                    // Aggressive fuzz with harmonics
                    float fuzz = x * Mathf.Abs(x);
                    return Mathf.Clamp(fuzz + x * 0.5f, -1f, 1f);
                    
                case DistortionType.Bitcrusher:
                    // Bit depth reduction
                    float levels = Mathf.Pow(2f, BitDepth);
                    return Mathf.Round(x * levels) / levels;
                    
                default:
                    return x;
            }
        }

        public override void ApplyPreset(string presetName)
        {
            if (string.IsNullOrEmpty(presetName)) return;

            switch (presetName.Trim().ToLowerInvariant())
            {
                case "soft drive":
                    Type = DistortionType.SoftClip;
                    Drive = 35f;
                    Tone = 8000f;
                    OutputGain = -2f;
                    Mix = 0.6f;
                    break;
                case "crunch":
                    Type = DistortionType.HardClip;
                    Drive = 50f;
                    Tone = 6000f;
                    OutputGain = -4f;
                    Mix = 0.6f;
                    break;
                case "tube warm":
                    Type = DistortionType.Tube;
                    Drive = 45f;
                    Tone = 7000f;
                    OutputGain = -2f;
                    Mix = 0.5f;
                    break;
                case "tape grit":
                    Type = DistortionType.Tape;
                    Drive = 40f;
                    Tone = 9000f;
                    OutputGain = -1f;
                    Mix = 0.5f;
                    break;
                case "fuzz":
                    Type = DistortionType.Fuzz;
                    Drive = 60f;
                    Tone = 5000f;
                    OutputGain = -6f;
                    Mix = 0.6f;
                    break;
                case "bitcrush":
                    Type = DistortionType.Bitcrusher;
                    Drive = 30f;
                    Tone = 12000f;
                    OutputGain = -3f;
                    BitDepth = 8;
                    SampleRateReduction = 4;
                    Mix = 0.5f;
                    break;
            }
        }

        public override void Reset()
        {
            _lpfState = 0f;
            _lastSample = 0f;
            _sampleCounter = 0;
        }
    }
    
    /// <summary>
    /// Warm tube-style saturation
    /// </summary>
    [Serializable]
    public class SaturationEffect : DSPEffectBase
    {
        public override string Name => "Saturation";
        
        /// <summary>
        /// Saturation amount (0-100)
        /// </summary>
        public float Amount { get; set; } = 30f;
        
        /// <summary>
        /// Harmonic character (0 = even, 1 = odd)
        /// </summary>
        public float Character { get; set; } = 0.5f;
        
        /// <summary>
        /// High frequency enhancement
        /// </summary>
        public float Presence { get; set; } = 0f;
        
        // Filter states
        private float _hpfState;
        
        public override void Process(float[] data, int channels, int sampleRate)
        {
            float drive = 1f + Amount * 0.05f;
            float presenceGain = Mathf.Pow(10f, Presence / 20f);
            float hpfCoef = 1f - Mathf.Exp(-2f * Mathf.PI * 80f / sampleRate);
            
            for (int i = 0; i < data.Length; i++)
            {
                float sample = data[i];
                float dry = sample;
                
                // Drive
                sample *= drive;
                
                // Saturation curve blend (even vs odd harmonics)
                float even = sample / (1f + Mathf.Abs(sample)); // Asymmetric
                float odd = (float)Math.Tanh(sample);           // Symmetric
                sample = even * (1f - Character) + odd * Character;
                
                // High-frequency presence (high-shelf boost)
                _hpfState += hpfCoef * (sample - _hpfState);
                float highFreq = sample - _hpfState;
                sample += highFreq * (presenceGain - 1f);
                
                // Auto-gain compensation
                sample /= drive * 0.5f + 0.5f;
                
                // Mix
                data[i] = dry * (1f - Mix) + sample * Mix;
            }
        }

        public override void ApplyPreset(string presetName)
        {
            if (string.IsNullOrEmpty(presetName)) return;

            switch (presetName.Trim().ToLowerInvariant())
            {
                case "subtle":
                    Amount = 15f;
                    Character = 0.4f;
                    Presence = 0f;
                    Mix = 0.4f;
                    break;
                case "warm":
                    Amount = 30f;
                    Character = 0.5f;
                    Presence = 1.5f;
                    Mix = 0.6f;
                    break;
                case "bright":
                    Amount = 25f;
                    Character = 0.7f;
                    Presence = 3f;
                    Mix = 0.6f;
                    break;
                case "tape":
                    Amount = 30f;
                    Character = 0.6f;
                    Presence = 1f;
                    Mix = 0.6f;
                    break;
                case "heavy":
                    Amount = 45f;
                    Character = 0.8f;
                    Presence = 2f;
                    Mix = 0.6f;
                    break;
            }
        }

        public override void Reset()
        {
            _hpfState = 0f;
        }
    }
    
    /// <summary>
    /// Waveshaper with custom curve
    /// </summary>
    [Serializable]
    public class WaveshaperEffect : DSPEffectBase
    {
        public override string Name => "Waveshaper";
        
        /// <summary>
        /// Curve amount (-1 to 1, negative = expand, positive = compress)
        /// </summary>
        public float Curve { get; set; } = 0.5f;
        
        /// <summary>
        /// Input gain in dB
        /// </summary>
        public float InputGain { get; set; } = 0f;
        
        /// <summary>
        /// Output gain in dB
        /// </summary>
        public float OutputGain { get; set; } = 0f;
        
        /// <summary>
        /// Asymmetry (0 = symmetric, 1 = full asymmetric)
        /// </summary>
        public float Asymmetry { get; set; } = 0f;
        
        public override void Process(float[] data, int channels, int sampleRate)
        {
            float inGain = Mathf.Pow(10f, InputGain / 20f);
            float outGain = Mathf.Pow(10f, OutputGain / 20f);
            
            for (int i = 0; i < data.Length; i++)
            {
                float sample = data[i] * inGain;
                float dry = data[i];
                
                // Apply waveshaping
                sample = ShapeSample(sample);
                
                // Output
                sample *= outGain;
                data[i] = dry * (1f - Mix) + sample * Mix;
            }
        }
        
        private float ShapeSample(float x)
        {
            // Chebyshev polynomial-based waveshaping
            float k = Curve * 0.99f; // Avoid division by zero
            float absK = Mathf.Abs(k);
            
            float shaped;
            if (k > 0)
            {
                // Compression curve
                shaped = (1f + k) * x / (1f + k * Mathf.Abs(x));
            }
            else
            {
                // Expansion curve
                shaped = Mathf.Sign(x) * Mathf.Pow(Mathf.Abs(x), 1f + absK);
            }
            
            // Apply asymmetry
            if (Asymmetry > 0 && x > 0)
            {
                float asymShaped = x * x * Mathf.Sign(x);
                shaped = shaped * (1f - Asymmetry) + asymShaped * Asymmetry;
            }

            return Mathf.Clamp(shaped, -1f, 1f);
        }

        public override void ApplyPreset(string presetName)
        {
            if (string.IsNullOrEmpty(presetName)) return;

            switch (presetName.Trim().ToLowerInvariant())
            {
                case "smooth":
                    Curve = 0.3f;
                    InputGain = 2f;
                    OutputGain = -1f;
                    Asymmetry = 0f;
                    Mix = 0.5f;
                    break;
                case "punch":
                    Curve = 0.6f;
                    InputGain = 3f;
                    OutputGain = -2f;
                    Asymmetry = 0.1f;
                    Mix = 0.55f;
                    break;
                case "expand":
                    Curve = -0.4f;
                    InputGain = 3f;
                    OutputGain = -1f;
                    Asymmetry = 0f;
                    Mix = 0.5f;
                    break;
                case "asym drive":
                    Curve = 0.5f;
                    InputGain = 4f;
                    OutputGain = -3f;
                    Asymmetry = 0.5f;
                    Mix = 0.55f;
                    break;
                case "hard":
                    Curve = 0.65f;
                    InputGain = 4f;
                    OutputGain = -4f;
                    Asymmetry = 0.2f;
                    Mix = 0.6f;
                    break;
            }
        }

        public override void Reset() { }
    }
}
