using System;
using UnityEngine;

namespace VARCOVoice.DSP
{
    /// <summary>
    /// Analog Tape Emulation
    /// 
    /// Simulates the characteristics of analog tape recording:
    /// - Soft saturation (magnetic hysteresis)
    /// - Head bump (low frequency boost)
    /// - High frequency roll-off
    /// - Wow and flutter (pitch modulation)
    /// - Tape hiss (optional noise)
    /// - Compression characteristics
    /// </summary>
    [Serializable]
    public class TapeEmulation : DSPEffectBase
    {
        public override string Name => "Tape Emulation";

        #region Parameters

        /// <summary>
        /// Input drive/gain into the tape stage (dB)
        /// Higher values = more saturation
        /// </summary>
        /// <summary>
        /// Input drive/gain into the tape stage (dB)
        /// Higher values = more saturation
        /// </summary>
        [field: Range(-12f, 24f)]
        public float InputDrive { get; set; } = 0f;

        /// <summary>
        /// Tape saturation amount (0-1)
        /// 0 = clean, 1 = heavily saturated
        /// </summary>
        [field: Range(0f, 1f)]
        public float Saturation { get; set; } = 0.5f;

        /// <summary>
        /// Tape speed simulation
        /// Affects frequency response and flutter characteristics
        /// </summary>
        public TapeSpeed Speed { get; set; } = TapeSpeed.Speed15IPS;

        /// <summary>
        /// Head bump amount (low frequency boost at ~60-100Hz)
        /// </summary>
        [field: Range(0f, 1f)]
        public float HeadBump { get; set; } = 0.3f;

        /// <summary>
        /// High frequency roll-off amount
        /// </summary>
        [field: Range(0f, 1f)]
        public float HighRolloff { get; set; } = 0.3f;

        /// <summary>
        /// Wow amount (slow pitch variation)
        /// </summary>
        [field: Range(0f, 1f)]
        public float Wow { get; set; } = 0.1f;

        /// <summary>
        /// Flutter amount (fast pitch variation)
        /// </summary>
        [field: Range(0f, 1f)]
        public float Flutter { get; set; } = 0.1f;

        /// <summary>
        /// Tape hiss level
        /// </summary>
        [field: Range(0f, 1f)]
        public float Hiss { get; set; } = 0f;

        /// <summary>
        /// Output level compensation
        /// </summary>
        [field: Range(-12f, 12f)]
        public float OutputLevel { get; set; } = 0f;

        /// <summary>
        /// Bias adjustment (affects harmonic content)
        /// </summary>
        [field: Range(-1f, 1f)]
        public float Bias { get; set; } = 0f;

        #endregion

        #region Internal State

        // Head bump filter (low shelf / resonant peak)
        private float _headBumpState1L, _headBumpState2L;
        private float _headBumpState1R, _headBumpState2R;

        // High frequency rolloff filter
        private float _hfRolloffStateL, _hfRolloffStateR;

        // Wow & Flutter LFO
        private float _wowPhase;
        private float _flutterPhase1, _flutterPhase2, _flutterPhase3;
        
        // Delay line for wow/flutter pitch modulation
        private float[][] _delayBuffer;
        private int _delayWritePos;
        private int _delayBufferSize;

        // Hysteresis state (for magnetic saturation)
        private float _hysteresisStateL, _hysteresisStateR;

        // Noise generator
        private uint _noiseState = 12345;

        private int _sampleRate;
        private bool _initialized;

        #endregion

        #region Processing

        public override void Process(float[] data, int channels, int sampleRate)
        {
            if (!Enabled) return;

            EnsureInitialized(sampleRate);

            float inputGain = Mathf.Pow(10f, InputDrive / 20f);
            float outputGain = Mathf.Pow(10f, OutputLevel / 20f);

            // Tape speed characteristics
            float headBumpFreq, hfCutoff, wowRate, flutterRate;
            GetSpeedCharacteristics(out headBumpFreq, out hfCutoff, out wowRate, out flutterRate);

            // Calculate filter coefficients
            float headBumpCoef = 2f * Mathf.PI * headBumpFreq / sampleRate;
            float hfCoef = Mathf.Exp(-2f * Mathf.PI * hfCutoff / sampleRate);

            // Modulation increments
            float wowInc = wowRate * 2f * Mathf.PI / sampleRate;
            float flutter1Inc = flutterRate * 2f * Mathf.PI / sampleRate;
            float flutter2Inc = flutterRate * 1.3f * 2f * Mathf.PI / sampleRate;
            float flutter3Inc = flutterRate * 0.7f * 2f * Mathf.PI / sampleRate;

            // Max modulation in samples
            float maxWowSamples = Wow * sampleRate * 0.003f; // ±3ms
            float maxFlutterSamples = Flutter * sampleRate * 0.0005f; // ±0.5ms

            int samplesPerChannel = data.Length / channels;

            for (int s = 0; s < samplesPerChannel; s++)
            {
                int idxL = s * channels;
                int idxR = channels > 1 ? s * channels + 1 : idxL;

                float left = data[idxL] * inputGain;
                float right = channels > 1 ? data[idxR] * inputGain : left;

                // === Wow & Flutter ===
                _wowPhase += wowInc;
                _flutterPhase1 += flutter1Inc;
                _flutterPhase2 += flutter2Inc;
                _flutterPhase3 += flutter3Inc;

                if (_wowPhase >= Mathf.PI * 2f) _wowPhase -= Mathf.PI * 2f;
                if (_flutterPhase1 >= Mathf.PI * 2f) _flutterPhase1 -= Mathf.PI * 2f;
                if (_flutterPhase2 >= Mathf.PI * 2f) _flutterPhase2 -= Mathf.PI * 2f;
                if (_flutterPhase3 >= Mathf.PI * 2f) _flutterPhase3 -= Mathf.PI * 2f;

                float wowMod = Mathf.Sin(_wowPhase) * maxWowSamples;
                float flutterMod = (Mathf.Sin(_flutterPhase1) * 0.5f +
                                   Mathf.Sin(_flutterPhase2) * 0.3f +
                                   Mathf.Sin(_flutterPhase3) * 0.2f) * maxFlutterSamples;

                float totalMod = wowMod + flutterMod;

                // Write to delay buffer
                _delayBuffer[0][_delayWritePos] = left;
                _delayBuffer[1][_delayWritePos] = right;

                // Read with modulation (interpolated)
                float readPos = _delayWritePos - 64 + totalMod;
                if (readPos < 0) readPos += _delayBufferSize;

                int readIdx1 = (int)readPos % _delayBufferSize;
                int readIdx2 = (readIdx1 + 1) % _delayBufferSize;
                float frac = readPos - Mathf.Floor(readPos);

                left = _delayBuffer[0][readIdx1] * (1f - frac) + _delayBuffer[0][readIdx2] * frac;
                right = _delayBuffer[1][readIdx1] * (1f - frac) + _delayBuffer[1][readIdx2] * frac;

                _delayWritePos = (_delayWritePos + 1) % _delayBufferSize;

                // === Head Bump (Low frequency boost) ===
                if (HeadBump > 0.01f)
                {
                    // Resonant 2-pole lowpass for the bump
                    float bump1L = _headBumpState1L + headBumpCoef * (left - _headBumpState1L);
                    float bump2L = _headBumpState2L + headBumpCoef * (bump1L - _headBumpState2L);
                    _headBumpState1L = bump1L;
                    _headBumpState2L = bump2L;
                    left += bump2L * HeadBump * 3f;

                    float bump1R = _headBumpState1R + headBumpCoef * (right - _headBumpState1R);
                    float bump2R = _headBumpState2R + headBumpCoef * (bump1R - _headBumpState2R);
                    _headBumpState1R = bump1R;
                    _headBumpState2R = bump2R;
                    right += bump2R * HeadBump * 3f;
                }

                // === Tape Saturation (Magnetic Hysteresis) ===
                left = ApplyTapeSaturation(left, ref _hysteresisStateL);
                right = ApplyTapeSaturation(right, ref _hysteresisStateR);

                // === High Frequency Rolloff ===
                if (HighRolloff > 0.01f)
                {
                    float rolloffAmount = 1f - HighRolloff * (1f - hfCoef);
                    _hfRolloffStateL = _hfRolloffStateL * rolloffAmount + left * (1f - rolloffAmount);
                    _hfRolloffStateR = _hfRolloffStateR * rolloffAmount + right * (1f - rolloffAmount);
                    left = Mathf.Lerp(left, _hfRolloffStateL, HighRolloff);
                    right = Mathf.Lerp(right, _hfRolloffStateR, HighRolloff);
                }

                // === Tape Hiss ===
                if (Hiss > 0.001f)
                {
                    float noiseL = GenerateNoise() * Hiss * 0.02f;
                    float noiseR = GenerateNoise() * Hiss * 0.02f;
                    left += noiseL;
                    right += noiseR;
                }

                // Output with mix
                data[idxL] = data[idxL] * (1f - Mix) + left * outputGain * Mix;
                if (channels > 1)
                    data[idxR] = data[idxR] * (1f - Mix) + right * outputGain * Mix;
            }
        }

        private float ApplyTapeSaturation(float input, ref float hysteresisState)
        {
            if (Saturation < 0.01f) return input;

            // Pre-emphasis based on bias
            float biased = input + Bias * 0.1f;

            // Magnetic hysteresis simulation (asymmetric soft clipping)
            float satAmount = Saturation * 0.7f;
            float x = biased * (1f + satAmount * 2f);

            // Hysteresis model (previous state affects current saturation)
            float hysteresis = (x - hysteresisState) * (1f - satAmount * 0.3f);
            hysteresisState = hysteresisState * 0.99f + x * 0.01f;

            // Soft saturation using tanh
            float saturated = (float)Math.Tanh(hysteresis * (1f + satAmount));

            // Add even harmonics (tape characteristic)
            float evenHarmonics = saturated * saturated * Mathf.Sign(saturated) * satAmount * 0.3f;
            saturated += evenHarmonics;

            // Blend
            return Mathf.Lerp(input, saturated, Saturation);
        }

        private void GetSpeedCharacteristics(out float headBumpFreq, out float hfCutoff, 
            out float wowRate, out float flutterRate)
        {
            switch (Speed)
            {
                case TapeSpeed.Speed7_5IPS:
                    headBumpFreq = 60f;
                    hfCutoff = 12000f;
                    wowRate = 0.5f;
                    flutterRate = 5f;
                    break;
                case TapeSpeed.Speed15IPS:
                    headBumpFreq = 80f;
                    hfCutoff = 18000f;
                    wowRate = 0.3f;
                    flutterRate = 3f;
                    break;
                case TapeSpeed.Speed30IPS:
                    headBumpFreq = 100f;
                    hfCutoff = 22000f;
                    wowRate = 0.2f;
                    flutterRate = 2f;
                    break;
                default:
                    headBumpFreq = 80f;
                    hfCutoff = 18000f;
                    wowRate = 0.3f;
                    flutterRate = 3f;
                    break;
            }
        }

        private float GenerateNoise()
        {
            // xorshift PRNG for pink-ish noise
            _noiseState ^= _noiseState << 13;
            _noiseState ^= _noiseState >> 17;
            _noiseState ^= _noiseState << 5;
            return (_noiseState / (float)uint.MaxValue - 0.5f) * 2f;
        }

        #endregion

        #region Initialization

        private void EnsureInitialized(int sampleRate)
        {
            if (_initialized && _sampleRate == sampleRate) return;

            _sampleRate = sampleRate;

            // Delay buffer for wow/flutter (about 20ms max modulation)
            _delayBufferSize = (int)(0.02f * sampleRate) + 128;
            _delayBuffer = new float[2][];
            _delayBuffer[0] = new float[_delayBufferSize];
            _delayBuffer[1] = new float[_delayBufferSize];
            _delayWritePos = 0;

            // Reset filter states
            _headBumpState1L = _headBumpState1R = 0f;
            _headBumpState2L = _headBumpState2R = 0f;
            _hfRolloffStateL = _hfRolloffStateR = 0f;
            _hysteresisStateL = _hysteresisStateR = 0f;

            // Reset LFOs
            _wowPhase = 0f;
            _flutterPhase1 = 0f;
            _flutterPhase2 = Mathf.PI * 0.5f;
            _flutterPhase3 = Mathf.PI;

            _initialized = true;
        }

        #endregion

        #region Reset

        public override void ApplyPreset(string presetName)
        {
            if (string.IsNullOrEmpty(presetName)) return;

            switch (presetName.Trim().ToLowerInvariant())
            {
                case "clean tape":
                    InputDrive = 0f;
                    Saturation = 0.2f;
                    Speed = TapeSpeed.Speed15IPS;
                    HeadBump = 0.2f;
                    HighRolloff = 0.2f;
                    Wow = 0.05f;
                    Flutter = 0.05f;
                    Hiss = 0f;
                    OutputLevel = 0f;
                    Bias = 0f;
                    Mix = 1f;
                    break;
                case "warm tape":
                    InputDrive = 6f;
                    Saturation = 0.6f;
                    Speed = TapeSpeed.Speed15IPS;
                    HeadBump = 0.5f;
                    HighRolloff = 0.4f;
                    Wow = 0.1f;
                    Flutter = 0.1f;
                    Hiss = 0.05f;
                    OutputLevel = -2f;
                    Bias = -0.1f;
                    Mix = 1f;
                    break;
                case "vintage":
                    InputDrive = 8f;
                    Saturation = 0.7f;
                    Speed = TapeSpeed.Speed7_5IPS;
                    HeadBump = 0.6f;
                    HighRolloff = 0.6f;
                    Wow = 0.2f;
                    Flutter = 0.15f;
                    Hiss = 0.1f;
                    OutputLevel = -3f;
                    Bias = -0.2f;
                    Mix = 1f;
                    break;
                case "bright":
                    InputDrive = 3f;
                    Saturation = 0.4f;
                    Speed = TapeSpeed.Speed30IPS;
                    HeadBump = 0.2f;
                    HighRolloff = 0.2f;
                    Wow = 0.05f;
                    Flutter = 0.05f;
                    Hiss = 0f;
                    OutputLevel = -1f;
                    Bias = 0.1f;
                    Mix = 1f;
                    break;
                case "lofi":
                    InputDrive = 10f;
                    Saturation = 0.9f;
                    Speed = TapeSpeed.Speed7_5IPS;
                    HeadBump = 0.7f;
                    HighRolloff = 0.8f;
                    Wow = 0.3f;
                    Flutter = 0.25f;
                    Hiss = 0.2f;
                    OutputLevel = -5f;
                    Bias = -0.3f;
                    Mix = 1f;
                    break;
            }
        }

        public override void Reset()
        {
            _initialized = false;
            
            if (_delayBuffer != null)
            {
                Array.Clear(_delayBuffer[0], 0, _delayBuffer[0].Length);
                Array.Clear(_delayBuffer[1], 0, _delayBuffer[1].Length);
            }
        }

        #endregion
    }

    /// <summary>
    /// Tape speed presets
    /// </summary>
    public enum TapeSpeed
    {
        /// <summary>7.5 inches per second (cassette quality)</summary>
        Speed7_5IPS,
        /// <summary>15 inches per second (studio standard)</summary>
        Speed15IPS,
        /// <summary>30 inches per second (high quality)</summary>
        Speed30IPS
    }

    /// <summary>
    /// Vacuum Tube Emulation
    /// 
    /// Simulates the characteristics of tube amplifiers:
    /// - Asymmetric soft clipping
    /// - Even harmonic generation
    /// - Frequency-dependent saturation
    /// - Grid conduction effects
    /// - Output transformer coloration
    /// </summary>
    [Serializable]
    public class TubeEmulation : DSPEffectBase
    {
        public override string Name => "Tube Emulation";

        #region Parameters

        /// <summary>
        /// Input drive into the tube stage (dB)
        /// </summary>
        /// <summary>
        /// Input drive into the tube stage (dB)
        /// </summary>
        [field: Range(-12f, 24f)]
        public float Drive { get; set; } = 0f;

        /// <summary>
        /// Tube type simulation
        /// </summary>
        public TubeType Type { get; set; } = TubeType.Triode12AX7;

        /// <summary>
        /// Bias point adjustment
        /// Negative = colder (more crossover distortion), Positive = hotter (more saturation)
        /// </summary>
        [field: Range(-1f, 1f)]
        public float Bias { get; set; } = 0f;

        /// <summary>
        /// Output transformer presence (high frequency emphasis)
        /// </summary>
        [field: Range(0f, 1f)]
        public float Presence { get; set; } = 0.3f;

        /// <summary>
        /// Sag amount (power supply compression at high levels)
        /// </summary>
        [field: Range(0f, 1f)]
        public float Sag { get; set; } = 0.2f;

        /// <summary>
        /// Even harmonic blend (2nd, 4th)
        /// </summary>
        [field: Range(0f, 1f)]
        public float EvenHarmonics { get; set; } = 0.5f;

        /// <summary>
        /// Odd harmonic blend (3rd, 5th)
        /// </summary>
        [field: Range(0f, 1f)]
        public float OddHarmonics { get; set; } = 0.3f;

        /// <summary>
        /// Output level
        /// </summary>
        [field: Range(-12f, 12f)]
        public float Output { get; set; } = 0f;

        #endregion

        #region Internal State

        // Sag envelope follower
        private float _sagEnvelopeL, _sagEnvelopeR;



        // Presence filter state
        private float _presenceStateL, _presenceStateR;

        // DC blocking filter
        private float _dcBlockStateL, _dcBlockStateR;

        private int _sampleRate;
        private bool _initialized;

        #endregion

        #region Processing

        public override void Process(float[] data, int channels, int sampleRate)
        {
            if (!Enabled) return;

            EnsureInitialized(sampleRate);

            float inputGain = Mathf.Pow(10f, Drive / 20f);
            float outputGain = Mathf.Pow(10f, Output / 20f);

            // Get tube characteristics
            float asymmetry, satPoint, gainFactor;
            GetTubeCharacteristics(out asymmetry, out satPoint, out gainFactor);

            // Sag coefficients
            float sagAttack = Mathf.Exp(-1f / (0.01f * sampleRate));
            float sagRelease = Mathf.Exp(-1f / (0.1f * sampleRate));

            // Presence filter coefficient
            float presenceCoef = Mathf.Exp(-2f * Mathf.PI * 3000f / sampleRate);

            int samplesPerChannel = data.Length / channels;

            for (int s = 0; s < samplesPerChannel; s++)
            {
                int idxL = s * channels;
                int idxR = channels > 1 ? s * channels + 1 : idxL;

                float left = data[idxL] * inputGain * gainFactor;
                float right = channels > 1 ? data[idxR] * inputGain * gainFactor : left;

                // === Power Supply Sag ===
                if (Sag > 0.01f)
                {
                    float levelL = Mathf.Abs(left);
                    float levelR = Mathf.Abs(right);
                    
                    float sagCoefL = levelL > _sagEnvelopeL ? sagAttack : sagRelease;
                    float sagCoefR = levelR > _sagEnvelopeR ? sagAttack : sagRelease;
                    
                    _sagEnvelopeL = sagCoefL * _sagEnvelopeL + (1f - sagCoefL) * levelL;
                    _sagEnvelopeR = sagCoefR * _sagEnvelopeR + (1f - sagCoefR) * levelR;
                    
                    float sagGainL = 1f - _sagEnvelopeL * Sag * 0.5f;
                    float sagGainR = 1f - _sagEnvelopeR * Sag * 0.5f;
                    
                    left *= Mathf.Max(sagGainL, 0.3f);
                    right *= Mathf.Max(sagGainR, 0.3f);
                }

                // === Tube Saturation ===
                left = ApplyTubeSaturation(left, asymmetry, satPoint);
                right = ApplyTubeSaturation(right, asymmetry, satPoint);

                // === Harmonic Generation ===
                if (EvenHarmonics > 0.01f || OddHarmonics > 0.01f)
                {
                    left = AddHarmonics(left);
                    right = AddHarmonics(right);
                }

                // === Presence (Output Transformer Character) ===
                if (Presence > 0.01f)
                {
                    _presenceStateL = presenceCoef * _presenceStateL + (1f - presenceCoef) * left;
                    _presenceStateR = presenceCoef * _presenceStateR + (1f - presenceCoef) * right;
                    
                    left += (left - _presenceStateL) * Presence * 2f;
                    right += (right - _presenceStateR) * Presence * 2f;
                }

                // === DC Blocking ===
                float dcCoef = 0.998f;
                float newDcL = left + dcCoef * _dcBlockStateL;
                float newDcR = right + dcCoef * _dcBlockStateR;
                left = newDcL - _dcBlockStateL;
                right = newDcR - _dcBlockStateR;
                _dcBlockStateL = newDcL;
                _dcBlockStateR = newDcR;

                // Output with mix
                data[idxL] = data[idxL] * (1f - Mix) + left * outputGain * Mix;
                if (channels > 1)
                    data[idxR] = data[idxR] * (1f - Mix) + right * outputGain * Mix;
            }
        }

        private float ApplyTubeSaturation(float input, float asymmetry, float satPoint)
        {
            // Apply bias offset
            float biased = input + Bias * 0.5f;

            // Asymmetric waveshaping (different curve for positive/negative)
            float x = biased / satPoint;
            float output;

            if (x >= 0)
            {
                // Positive half: softer clipping
                output = (float)Math.Tanh(x * (1f + asymmetry * 0.5f));
            }
            else
            {
                // Negative half: harder clipping (grid conduction)
                float hardness = 1f + asymmetry;
                output = -(float)Math.Tanh(-x * hardness) / hardness;
            }

            return output * satPoint;
        }

        private float AddHarmonics(float input)
        {
            float x = input;
            float output = x;

            // 2nd harmonic (even)
            if (EvenHarmonics > 0.01f)
            {
                float h2 = x * x * Mathf.Sign(x) * 0.5f;
                output += h2 * EvenHarmonics * 0.3f;
                
                // 4th harmonic
                float h4 = h2 * x * x * 0.25f;
                output += h4 * EvenHarmonics * 0.1f;
            }

            // 3rd harmonic (odd)
            if (OddHarmonics > 0.01f)
            {
                float h3 = x * x * x * 0.33f;
                output += h3 * OddHarmonics * 0.2f;
                
                // 5th harmonic
                float h5 = h3 * x * x * 0.2f;
                output += h5 * OddHarmonics * 0.05f;
            }

            return output;
        }

        private void GetTubeCharacteristics(out float asymmetry, out float satPoint, out float gainFactor)
        {
            switch (Type)
            {
                case TubeType.Triode12AX7:
                    asymmetry = 0.7f;
                    satPoint = 1.0f;
                    gainFactor = 1.0f;
                    break;
                case TubeType.Triode12AT7:
                    asymmetry = 0.5f;
                    satPoint = 1.2f;
                    gainFactor = 0.7f;
                    break;
                case TubeType.Pentode6L6:
                    asymmetry = 0.3f;
                    satPoint = 1.5f;
                    gainFactor = 1.2f;
                    break;
                case TubeType.PentodeEL34:
                    asymmetry = 0.4f;
                    satPoint = 1.3f;
                    gainFactor = 1.1f;
                    break;
                default:
                    asymmetry = 0.5f;
                    satPoint = 1.0f;
                    gainFactor = 1.0f;
                    break;
            }
        }

        #endregion

        #region Initialization

        private void EnsureInitialized(int sampleRate)
        {
            if (_initialized && _sampleRate == sampleRate) return;

            _sampleRate = sampleRate;

            _sagEnvelopeL = _sagEnvelopeR = 0f;

            _presenceStateL = _presenceStateR = 0f;
            _dcBlockStateL = _dcBlockStateR = 0f;

            _initialized = true;
        }

        #endregion

        #region Reset

        public override void ApplyPreset(string presetName)
        {
            if (string.IsNullOrEmpty(presetName)) return;

            switch (presetName.Trim().ToLowerInvariant())
            {
                case "clean":
                    Drive = 0f;
                    Type = TubeType.Triode12AT7;
                    Bias = 0f;
                    Presence = 0.2f;
                    Sag = 0.1f;
                    EvenHarmonics = 0.3f;
                    OddHarmonics = 0.2f;
                    Output = 0f;
                    Mix = 1f;
                    break;
                case "warm":
                    Drive = 6f;
                    Type = TubeType.Triode12AX7;
                    Bias = 0.1f;
                    Presence = 0.3f;
                    Sag = 0.2f;
                    EvenHarmonics = 0.6f;
                    OddHarmonics = 0.3f;
                    Output = -1f;
                    Mix = 1f;
                    break;
                case "crunch":
                    Drive = 12f;
                    Type = TubeType.PentodeEL34;
                    Bias = -0.1f;
                    Presence = 0.5f;
                    Sag = 0.4f;
                    EvenHarmonics = 0.5f;
                    OddHarmonics = 0.6f;
                    Output = -3f;
                    Mix = 1f;
                    break;
                case "vintage":
                    Drive = 8f;
                    Type = TubeType.Pentode6L6;
                    Bias = -0.05f;
                    Presence = 0.4f;
                    Sag = 0.3f;
                    EvenHarmonics = 0.7f;
                    OddHarmonics = 0.4f;
                    Output = -2f;
                    Mix = 1f;
                    break;
                case "edge":
                    Drive = 16f;
                    Type = TubeType.PentodeEL34;
                    Bias = -0.2f;
                    Presence = 0.7f;
                    Sag = 0.5f;
                    EvenHarmonics = 0.4f;
                    OddHarmonics = 0.8f;
                    Output = -4f;
                    Mix = 1f;
                    break;
            }
        }

        public override void Reset()
        {
            _initialized = false;
            _sagEnvelopeL = _sagEnvelopeR = 0f;
            _presenceStateL = _presenceStateR = 0f;
            _dcBlockStateL = _dcBlockStateR = 0f;
        }

        #endregion
    }

    /// <summary>
    /// Vacuum tube type presets
    /// </summary>
    public enum TubeType
    {
        /// <summary>12AX7 triode (high gain, preamp standard)</summary>
        Triode12AX7,
        /// <summary>12AT7 triode (medium gain, cleaner)</summary>
        Triode12AT7,
        /// <summary>6L6 pentode (American power amp, tight bass)</summary>
        Pentode6L6,
        /// <summary>EL34 pentode (British power amp, mid push)</summary>
        PentodeEL34
    }
}
