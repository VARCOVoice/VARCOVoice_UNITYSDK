using System;
using UnityEngine;

namespace VARCOVoice.DSP
{
    /// <summary>
    /// Basic delay effect with filtering
    /// </summary>
    [System.Serializable]
    public class DelayEffect : DSPEffectBase, IMorphableEffect
    {
        public override string Name => "Delay";
        
        /// <summary>
        /// Delay time in milliseconds
        /// </summary>
        public float DelayTime { get; set; } = 250f;
        
        /// <summary>
        /// Feedback amount (0-1)
        /// </summary>
        [field: Range(0f, 0.95f)]
        public float Feedback { get; set; } = 0.3f;
        
        /// <summary>
        /// High-pass filter on feedback path (Hz)
        /// </summary>
        public float FeedbackHPF { get; set; } = 20f;
        
        /// <summary>
        /// Low-pass filter on feedback path (Hz)
        /// </summary>
        public float FeedbackLPF { get; set; } = 12000f;
        
        // Buffers (pre-allocated)
        private float[] _delayBuffer;
        private int _bufferSize;
        private int _writePosition;
        
        // Filter states
        private float _hpfState;
        private float _lpfState;

        private int _lastSampleRate;
        private bool _initialized;
        private bool _rampsInitialized;
        private int _morphSamplesRemaining;

        private ParamRamp _delayRamp;
        private ParamRamp _feedbackRamp;
        private ParamRamp _mixRamp;
        
        public override void Process(float[] data, int channels, int sampleRate)
        {
            if (sampleRate <= 0) return;
            EnsureInitialized(sampleRate);

            // Pre-calculate conversion constant once per block
            float msToSamples = sampleRate * 0.001f;
            
            // Filter coefficients (calculated once per block)
            float hpfHz = Mathf.Clamp(FeedbackHPF, 20f, sampleRate * 0.45f);
            float lpfHz = Mathf.Clamp(FeedbackLPF, 20f, sampleRate * 0.45f);
            if (lpfHz < hpfHz) lpfHz = hpfHz;

            float hpfCoef = 1f - Mathf.Exp(-2f * Mathf.PI * hpfHz / sampleRate);
            float lpfCoef = Mathf.Exp(-2f * Mathf.PI * lpfHz / sampleRate);
            float filterSpan = lpfHz - hpfHz;
            float maxSpan = Mathf.Max(1f, sampleRate * 0.45f - 20f);
            float openness = Mathf.Clamp01(filterSpan / maxSpan);
            float maxFeedback = Mathf.Lerp(0.95f, 0.85f, openness);
            float targetFeedback = Mathf.Clamp(Feedback, 0f, maxFeedback);
            float targetMix = Mathf.Clamp01(Mix);
            float targetDelayMs = Mathf.Max(1f, DelayTime);

            int samplesPerChannel = data.Length / channels;
            if (!_rampsInitialized)
            {
                _feedbackRamp.Reset(targetFeedback);
                _mixRamp.Reset(targetMix);
                _delayRamp.Reset(targetDelayMs);
                _rampsInitialized = true;
            }

            if (_morphSamplesRemaining > 0)
            {
                if (!_feedbackRamp.IsActive) _feedbackRamp.SetTarget(targetFeedback, _morphSamplesRemaining);
                if (!_mixRamp.IsActive) _mixRamp.SetTarget(targetMix, _morphSamplesRemaining);
                if (!_delayRamp.IsActive) _delayRamp.SetTarget(targetDelayMs, _morphSamplesRemaining);
                _morphSamplesRemaining = Mathf.Max(0, _morphSamplesRemaining - samplesPerChannel);
            }
            else
            {
                _feedbackRamp.SetTarget(targetFeedback, samplesPerChannel);
                _mixRamp.SetTarget(targetMix, samplesPerChannel);
                _delayRamp.SetTarget(targetDelayMs, samplesPerChannel);
            }

            for (int i = 0; i < samplesPerChannel; i++)
            {
                // Get mono input
                float input = 0f;
                for (int ch = 0; ch < channels; ch++)
                {
                    input += data[i * channels + ch];
                }
                input /= channels;

                float delayMs = _delayRamp.Next();
                float delaySamples = delayMs * msToSamples;
                delaySamples = Mathf.Clamp(delaySamples, 1f, _bufferSize - 2);

                float feedback = Mathf.Clamp(_feedbackRamp.Next(), 0f, 0.95f);
                float mix = _mixRamp.Next();

                // Read from delay buffer (linear interpolation)
                int readPos0 = (_writePosition - (int)delaySamples + _bufferSize) % _bufferSize;
                int readPos1 = (readPos0 - 1 + _bufferSize) % _bufferSize;
                float frac = delaySamples - (int)delaySamples;
                float delayed = _delayBuffer[readPos0] * (1f - frac) + _delayBuffer[readPos1] * frac;
                
                // Apply filters to feedback
                _hpfState += hpfCoef * (delayed - _hpfState);
                float hpFiltered = delayed - _hpfState;
                _lpfState = lpfCoef * _lpfState + (1f - lpfCoef) * hpFiltered;
                float filtered = DSPConstants.FlushDenormal(_lpfState);
                
                // Flush denormals from filter states (prevents CPU spikes)
                _hpfState = DSPConstants.FlushDenormal(_hpfState);
                _lpfState = DSPConstants.FlushDenormal(_lpfState);
                
                // Write to buffer with feedback and DC offset to prevent denormals
                float feedbackSample = DSPConstants.SoftClip(filtered * feedback);
                _delayBuffer[_writePosition] = input + feedbackSample + DSPConstants.DC_OFFSET;
                _writePosition = (_writePosition + 1) % _bufferSize;

                // Output mix (SoftClip wet signal)
                float wet = DSPConstants.SoftClip(delayed);
                float output = input * (1f - mix) + wet * mix;

                for (int ch = 0; ch < channels; ch++)
                {
                    data[i * channels + ch] = output;
                }
            }
        }

        public override void ApplyPreset(string presetName)
        {
            if (string.IsNullOrEmpty(presetName)) return;

            switch (presetName.Trim().ToLowerInvariant())
            {
                case "slapback":
                    DelayTime = 70f;
                    Feedback = 0.15f;
                    FeedbackHPF = 120f;
                    FeedbackLPF = 8000f;
                    Mix = 0.25f;
                    break;
                case "vocal double":
                    DelayTime = 120f;
                    Feedback = 0.1f;
                    FeedbackHPF = 100f;
                    FeedbackLPF = 9000f;
                    Mix = 0.2f;
                    break;
                case "rhythmic quarter":
                    DelayTime = 500f;
                    Feedback = 0.4f;
                    FeedbackHPF = 120f;
                    FeedbackLPF = 8000f;
                    Mix = 0.35f;
                    break;
                case "rhythmic triplet":
                    DelayTime = 333f;
                    Feedback = 0.3f;
                    FeedbackHPF = 120f;
                    FeedbackLPF = 8000f;
                    Mix = 0.3f;
                    break;
                case "rhythmic dotted 8th":
                    DelayTime = 375f;
                    Feedback = 0.35f;
                    FeedbackHPF = 120f;
                    FeedbackLPF = 8000f;
                    Mix = 0.3f;
                    break;
                case "long tail":
                    DelayTime = 800f;
                    Feedback = 0.5f;
                    FeedbackHPF = 80f;
                    FeedbackLPF = 9000f;
                    Mix = 0.35f;
                    break;
                case "filtered echo":
                    DelayTime = 450f;
                    Feedback = 0.45f;
                    FeedbackHPF = 500f;
                    FeedbackLPF = 3000f;
                    Mix = 0.35f;
                    break;
            }
        }

        private void EnsureInitialized(int sampleRate)
        {
            int requiredSize = (int)(2.1f * sampleRate); // 2+ seconds buffer
            
            if (!_initialized || _lastSampleRate != sampleRate || _bufferSize < requiredSize || _delayBuffer == null)
            {
                _bufferSize = requiredSize;
                _delayBuffer = new float[_bufferSize];
                _writePosition = 0;
                _hpfState = 0f;
                _lpfState = 0f;
                _lastSampleRate = sampleRate;
                _initialized = true;
            }
        }
        
        public override void Reset()
        {
            if (_delayBuffer != null)
                Array.Clear(_delayBuffer, 0, _delayBuffer.Length);
            _writePosition = 0;
            _hpfState = 0f;
            _lpfState = 0f;
            _rampsInitialized = false;
            _morphSamplesRemaining = 0;
        }

        public void SetMorphTarget(IDSPEffect target, int samples)
        {
            if (target is not DelayEffect other) return;
            DelayTime = other.DelayTime;
            Feedback = other.Feedback;
            FeedbackHPF = other.FeedbackHPF;
            FeedbackLPF = other.FeedbackLPF;
            Mix = other.Mix;
            Enabled = other.Enabled;
            _morphSamplesRemaining = Mathf.Max(0, samples);
        }
    }
    
    /// <summary>
    /// Multi-tap delay with up to 8 taps
    /// </summary>
    [Serializable]
    public class MultiTapDelay : DSPEffectBase, IMorphableEffect
    {
        public override string Name => "Multi-Tap Delay";
        
        /// <summary>
        /// Number of active taps (1-8)
        /// </summary>
        public int TapCount { get; set; } = 4;
        
        /// <summary>
        /// Base delay time in ms
        /// </summary>
        public float BaseDelay { get; set; } = 250f;
        
        /// <summary>
        /// Delay multiplier between taps
        /// </summary>
        public float TapSpacing { get; set; } = 1.0f;
        
        /// <summary>
        /// Level decay per tap
        /// </summary>
        public float TapDecay { get; set; } = 0.7f;
        
        /// <summary>
        /// Overall feedback
        /// </summary>
        [field: Range(0f, 0.95f)]
        public float Feedback { get; set; } = 0.3f;
        
        // Buffers
        private float[] _delayBuffer;
        private int _bufferSize;
        private int _writePosition;

        private int _lastSampleRate;
        private bool _initialized;
        private bool _rampsInitialized;
        private int _morphSamplesRemaining;

        private ParamRamp _mixRamp;
        private ParamRamp _feedbackRamp;
        private ParamRamp _baseDelayRamp;
        private ParamRamp _tapSpacingRamp;
        private ParamRamp _tapDecayRamp;
        
        public override void Process(float[] data, int channels, int sampleRate)
        {
            EnsureInitialized(sampleRate);

            int tapCount = Mathf.Clamp(TapCount, 1, 8);
            float targetBaseDelay = Mathf.Max(1f, BaseDelay);
            float targetTapSpacing = Mathf.Max(0.1f, TapSpacing);
            float targetTapDecay = Mathf.Clamp(TapDecay, 0f, 1f);
            float targetFeedback = Mathf.Clamp(Feedback, 0f, 0.95f);
            float targetMix = Mathf.Clamp01(Mix);

            int samplesPerChannel = data.Length / channels;
            if (!_rampsInitialized)
            {
                _baseDelayRamp.Reset(targetBaseDelay);
                _tapSpacingRamp.Reset(targetTapSpacing);
                _tapDecayRamp.Reset(targetTapDecay);
                _feedbackRamp.Reset(targetFeedback);
                _mixRamp.Reset(targetMix);
                _rampsInitialized = true;
            }
            if (_morphSamplesRemaining > 0)
            {
                if (!_baseDelayRamp.IsActive) _baseDelayRamp.SetTarget(targetBaseDelay, _morphSamplesRemaining);
                if (!_tapSpacingRamp.IsActive) _tapSpacingRamp.SetTarget(targetTapSpacing, _morphSamplesRemaining);
                if (!_tapDecayRamp.IsActive) _tapDecayRamp.SetTarget(targetTapDecay, _morphSamplesRemaining);
                if (!_feedbackRamp.IsActive) _feedbackRamp.SetTarget(targetFeedback, _morphSamplesRemaining);
                if (!_mixRamp.IsActive) _mixRamp.SetTarget(targetMix, _morphSamplesRemaining);
                _morphSamplesRemaining = Mathf.Max(0, _morphSamplesRemaining - samplesPerChannel);
            }
            else
            {
                _baseDelayRamp.SetTarget(targetBaseDelay, samplesPerChannel);
                _tapSpacingRamp.SetTarget(targetTapSpacing, samplesPerChannel);
                _tapDecayRamp.SetTarget(targetTapDecay, samplesPerChannel);
                _feedbackRamp.SetTarget(targetFeedback, samplesPerChannel);
                _mixRamp.SetTarget(targetMix, samplesPerChannel);
            }

            for (int i = 0; i < samplesPerChannel; i++)
            {
                // Get mono input
                float input = 0f;
                for (int ch = 0; ch < channels; ch++)
                {
                    input += data[i * channels + ch];
                }
                input /= channels;
                
                // Sum all taps
                float tapSum = 0f;
                float level = 1f;
                float baseDelay = _baseDelayRamp.Next();
                float tapSpacing = _tapSpacingRamp.Next();
                float tapDecay = _tapDecayRamp.Next();
                float feedback = Mathf.Clamp(_feedbackRamp.Next(), -0.95f, 0.95f);
                float mix = _mixRamp.Next();
                float delay = baseDelay;

                for (int tap = 0; tap < tapCount; tap++)
                {
                    int delaySamples = (int)(delay * sampleRate / 1000f);
                    delaySamples = Mathf.Clamp(delaySamples, 1, _bufferSize - 1);

                    int readPos = (_writePosition - delaySamples + _bufferSize) % _bufferSize;
                    tapSum += _delayBuffer[readPos] * level;

                    level *= tapDecay;
                    delay *= tapSpacing;
                }

                // Write with feedback from last tap
                int lastDelaySamples = (int)(delay / tapSpacing * sampleRate / 1000f);
                lastDelaySamples = Mathf.Clamp(lastDelaySamples, 1, _bufferSize - 1);
                int lastReadPos = (_writePosition - lastDelaySamples + _bufferSize) % _bufferSize;

                _delayBuffer[_writePosition] = input + _delayBuffer[lastReadPos] * feedback;
                _writePosition = (_writePosition + 1) % _bufferSize;
                
                // Output (SoftClip wet signal)
                float wet = DSPConstants.SoftClip(tapSum);
                float output = input * (1f - mix) + wet * mix;
                
                for (int ch = 0; ch < channels; ch++)
                {
                    data[i * channels + ch] = output;
                }
            }
        }

        public override void ApplyPreset(string presetName)
        {
            if (string.IsNullOrEmpty(presetName)) return;

            switch (presetName.Trim().ToLowerInvariant())
            {
                case "tight slap":
                    TapCount = 3;
                    BaseDelay = 80f;
                    TapSpacing = 1.0f;
                    TapDecay = 0.7f;
                    Feedback = 0.2f;
                    Mix = 0.25f;
                    break;
                case "rhythmic":
                    TapCount = 4;
                    BaseDelay = 180f;
                    TapSpacing = 1.2f;
                    TapDecay = 0.6f;
                    Feedback = 0.3f;
                    Mix = 0.35f;
                    break;
                case "cascade":
                    TapCount = 6;
                    BaseDelay = 120f;
                    TapSpacing = 1.4f;
                    TapDecay = 0.7f;
                    Feedback = 0.35f;
                    Mix = 0.4f;
                    break;
                case "wide wash":
                    TapCount = 5;
                    BaseDelay = 220f;
                    TapSpacing = 1.3f;
                    TapDecay = 0.5f;
                    Feedback = 0.35f;
                    Mix = 0.35f;
                    break;
                case "sparse":
                    TapCount = 2;
                    BaseDelay = 300f;
                    TapSpacing = 1.6f;
                    TapDecay = 0.6f;
                    Feedback = 0.25f;
                    Mix = 0.3f;
                    break;
            }
        }

        private void EnsureInitialized(int sampleRate)
        {
            // Maximum possible delay: BaseDelay * TapSpacing^8
            int requiredSize = (int)(5f * sampleRate); // 5 seconds buffer
            
            if (!_initialized || _lastSampleRate != sampleRate || _bufferSize < requiredSize)
            {
                _bufferSize = requiredSize;
                _delayBuffer = new float[_bufferSize];
                _writePosition = 0;
                _lastSampleRate = sampleRate;
                _initialized = true;
            }
        }
        
        public override void Reset()
        {
            if (_delayBuffer != null)
                Array.Clear(_delayBuffer, 0, _delayBuffer.Length);
            _writePosition = 0;
            _rampsInitialized = false;
            _morphSamplesRemaining = 0;
        }

        public void SetMorphTarget(IDSPEffect target, int samples)
        {
            if (target is not MultiTapDelay other) return;
            TapCount = other.TapCount;
            BaseDelay = other.BaseDelay;
            TapSpacing = other.TapSpacing;
            TapDecay = other.TapDecay;
            Feedback = other.Feedback;
            Mix = other.Mix;
            Enabled = other.Enabled;
            _morphSamplesRemaining = Mathf.Max(0, samples);
        }
    }
    
    /// <summary>
    /// Ping-pong stereo delay
    /// </summary>
    [Serializable]
    public class PingPongDelay : DSPEffectBase, IMorphableEffect
    {
        public override string Name => "Ping-Pong Delay";
        
        /// <summary>
        /// Delay time in milliseconds
        /// </summary>
        public float DelayTime { get; set; } = 250f;
        
        /// <summary>
        /// Feedback amount
        /// </summary>
        [field: Range(-0.95f, 0.95f)]
        public float Feedback { get; set; } = 0.5f;
        
        /// <summary>
        /// Stereo width (0 = mono, 1 = full stereo)
        /// </summary>
        public float Width { get; set; } = 1f;
        
        /// <summary>
        /// Cross-feedback between L and R
        /// </summary>
        [field: Range(-0.95f, 0.95f)]
        public float CrossFeedback { get; set; } = 0.3f;
        
        // Stereo buffers
        private float[] _leftBuffer;
        private float[] _rightBuffer;
        private int _bufferSize;
        private int _writePosition;
        
        private int _lastSampleRate;
        private bool _initialized;
        private bool _rampsInitialized;
        private int _morphSamplesRemaining;

        private ParamRamp _delayRamp;
        private ParamRamp _feedbackRamp;
        private ParamRamp _crossFeedbackRamp;
        private ParamRamp _widthRamp;
        private ParamRamp _mixRamp;

        public override void Process(float[] data, int channels, int sampleRate)
        {
            if (channels < 2) return; // Requires stereo

            EnsureInitialized(sampleRate);

            int samplesPerChannel = data.Length / channels;

            float targetDelayMs = Mathf.Max(1f, DelayTime);
            float targetFeedback = Mathf.Clamp(Feedback, -0.95f, 0.95f);
            float targetCrossFeedback = Mathf.Clamp(CrossFeedback, -0.95f, 0.95f);
            float targetWidth = Mathf.Clamp01(Width);
            float targetMix = Mathf.Clamp01(Mix);

            if (!_rampsInitialized)
            {
                _delayRamp.Reset(targetDelayMs);
                _feedbackRamp.Reset(targetFeedback);
                _crossFeedbackRamp.Reset(targetCrossFeedback);
                _widthRamp.Reset(targetWidth);
                _mixRamp.Reset(targetMix);
                _rampsInitialized = true;
            }

            if (_morphSamplesRemaining > 0)
            {
                if (!_delayRamp.IsActive) _delayRamp.SetTarget(targetDelayMs, _morphSamplesRemaining);
                if (!_feedbackRamp.IsActive) _feedbackRamp.SetTarget(targetFeedback, _morphSamplesRemaining);
                if (!_crossFeedbackRamp.IsActive) _crossFeedbackRamp.SetTarget(targetCrossFeedback, _morphSamplesRemaining);
                if (!_widthRamp.IsActive) _widthRamp.SetTarget(targetWidth, _morphSamplesRemaining);
                if (!_mixRamp.IsActive) _mixRamp.SetTarget(targetMix, _morphSamplesRemaining);
                _morphSamplesRemaining = Mathf.Max(0, _morphSamplesRemaining - samplesPerChannel);
            }
            else
            {
                _delayRamp.SetTarget(targetDelayMs, samplesPerChannel);
                _feedbackRamp.SetTarget(targetFeedback, samplesPerChannel);
                _crossFeedbackRamp.SetTarget(targetCrossFeedback, samplesPerChannel);
                _widthRamp.SetTarget(targetWidth, samplesPerChannel);
                _mixRamp.SetTarget(targetMix, samplesPerChannel);
            }

            for (int i = 0; i < samplesPerChannel; i++)
            {
                int idx = i * channels;
                float inputL = data[idx];
                float inputR = data[idx + 1];

                float delayMs = _delayRamp.Next();
                float delaySamples = delayMs * sampleRate / 1000f;
                delaySamples = Mathf.Clamp(delaySamples, 1f, _bufferSize - 2);

                float feedback = Mathf.Clamp(_feedbackRamp.Next(), -0.95f, 0.95f);
                float crossFeedback = Mathf.Clamp(_crossFeedbackRamp.Next(), -0.95f, 0.95f);
                float width = _widthRamp.Next();
                float mix = _mixRamp.Next();
                float totalFeedback = Mathf.Abs(feedback) + Mathf.Abs(crossFeedback);
                if (totalFeedback > 0.98f)
                {
                    float scale = 0.98f / totalFeedback;
                    feedback *= scale;
                    crossFeedback *= scale;
                }

                // Read from buffers (linear interpolation)
                int readPos0 = (_writePosition - (int)delaySamples + _bufferSize) % _bufferSize;
                int readPos1 = (readPos0 - 1 + _bufferSize) % _bufferSize;
                float frac = delaySamples - (int)delaySamples;
                float delayedL = _leftBuffer[readPos0] * (1f - frac) + _leftBuffer[readPos1] * frac;
                float delayedR = _rightBuffer[readPos0] * (1f - frac) + _rightBuffer[readPos1] * frac;
                
                // Ping-pong: L feeds R, R feeds L with cross-feedback
                float feedbackL = DSPConstants.SoftClip(delayedR * feedback + delayedL * crossFeedback);
                float feedbackR = DSPConstants.SoftClip(delayedL * feedback + delayedR * crossFeedback);
                float newL = inputL + feedbackL;
                float newR = inputR + feedbackR;

                // Write to buffers
                _leftBuffer[_writePosition] = newL + DSPConstants.DC_OFFSET;
                _rightBuffer[_writePosition] = newR + DSPConstants.DC_OFFSET;
                _writePosition = (_writePosition + 1) % _bufferSize;
                
                // Output with width control
                // Output with width control (SoftClip wet signal)
                float wetL = DSPConstants.SoftClip(delayedL * width + delayedR * (1f - width));
                float wetR = DSPConstants.SoftClip(delayedR * width + delayedL * (1f - width));

                data[idx] = inputL * (1f - mix) + wetL * mix;
                data[idx + 1] = inputR * (1f - mix) + wetR * mix;
            }
        }

        public override void ApplyPreset(string presetName)
        {
            if (string.IsNullOrEmpty(presetName)) return;

            switch (presetName.Trim().ToLowerInvariant())
            {
                case "wide quarter":
                    DelayTime = 500f;
                    Feedback = 0.4f;
                    CrossFeedback = 0.3f;
                    Width = 1f;
                    Mix = 0.35f;
                    break;
                case "slap ping":
                    DelayTime = 140f;
                    Feedback = 0.25f;
                    CrossFeedback = 0.2f;
                    Width = 0.9f;
                    Mix = 0.25f;
                    break;
                case "spiral":
                    DelayTime = 350f;
                    Feedback = 0.45f;
                    CrossFeedback = 0.3f;
                    Width = 1f;
                    Mix = 0.35f;
                    break;
                case "stereo echo":
                    DelayTime = 260f;
                    Feedback = 0.35f;
                    CrossFeedback = 0.25f;
                    Width = 0.8f;
                    Mix = 0.3f;
                    break;
                case "ambient":
                    DelayTime = 700f;
                    Feedback = 0.4f;
                    CrossFeedback = 0.25f;
                    Width = 1f;
                    Mix = 0.4f;
                    break;
            }
        }

        private void EnsureInitialized(int sampleRate)
        {
            int requiredSize = (int)(1.5f * sampleRate);
            
            if (!_initialized || _lastSampleRate != sampleRate || _bufferSize < requiredSize)
            {
                _bufferSize = requiredSize;
                _leftBuffer = new float[_bufferSize];
                _rightBuffer = new float[_bufferSize];
                _writePosition = 0;
                _lastSampleRate = sampleRate;
                _initialized = true;
            }
        }
        
        public override void Reset()
        {
            if (_leftBuffer != null)
                Array.Clear(_leftBuffer, 0, _leftBuffer.Length);
            if (_rightBuffer != null)
                Array.Clear(_rightBuffer, 0, _rightBuffer.Length);
            _writePosition = 0;
            _rampsInitialized = false;
            _morphSamplesRemaining = 0;
        }

        public void SetMorphTarget(IDSPEffect target, int samples)
        {
            if (target is not PingPongDelay other) return;
            DelayTime = other.DelayTime;
            Feedback = other.Feedback;
            CrossFeedback = other.CrossFeedback;
            Width = other.Width;
            Mix = other.Mix;
            Enabled = other.Enabled;
            _morphSamplesRemaining = Mathf.Max(0, samples);
        }
    }
    
    /// <summary>
    /// Modulated delay with pitch wobble (Tape/Analog style)
    /// </summary>
    [Serializable]
    public class ModulatedDelay : DSPEffectBase, IMorphableEffect
    {
        public override string Name => "Modulated Delay";
        
        /// <summary>
        /// Base delay time in ms
        /// </summary>
        public float DelayTime { get; set; } = 300f;
        
        /// <summary>
        /// Modulation depth in ms
        /// </summary>
        public float ModDepth { get; set; } = 5f;
        
        /// <summary>
        /// Modulation rate in Hz
        /// </summary>
        public float ModRate { get; set; } = 0.5f;
        
        /// <summary>
        /// Feedback amount
        /// </summary>
        [field: Range(-0.95f, 0.95f)]
        public float Feedback { get; set; } = 0.4f;

        /// <summary>
        /// High-pass filter on feedback path (Hz)
        /// </summary>
        public float FeedbackHPF { get; set; } = 20f;

        /// <summary>
        /// Low-pass filter on feedback path (Hz)
        /// </summary>
        public float FeedbackLPF { get; set; } = 20000f;

        // Buffers
        private float[] _delayBuffer;
        private int _bufferSize;
        private int _writePosition;
        private QuadratureOscillator _lfo;  // Replaces _phase with faster quadrature oscillator

        private int _lastSampleRate;
        private bool _initialized;
        private bool _rampsInitialized;
        private int _morphSamplesRemaining;

        private ParamRamp _delayRamp;
        private ParamRamp _depthRamp;
        private ParamRamp _rateRamp;
        private ParamRamp _feedbackRamp;
        private ParamRamp _mixRamp;

        private float _hpfState;
        private float _lpfState;
        
        public override void Process(float[] data, int channels, int sampleRate)
        {
            EnsureInitialized(sampleRate);

            int samplesPerChannel = data.Length / channels;
            float targetDelayMs = Mathf.Max(1f, DelayTime);
            float targetDepthMs = Mathf.Max(0f, ModDepth);
            float targetRate = Mathf.Max(0f, ModRate);
            float targetFeedback = Mathf.Clamp(Feedback, -0.95f, 0.95f);
            float targetMix = Mathf.Clamp01(Mix);

            if (!_rampsInitialized)
            {
                _delayRamp.Reset(targetDelayMs);
                _depthRamp.Reset(targetDepthMs);
                _rateRamp.Reset(targetRate);
                _feedbackRamp.Reset(targetFeedback);
                _mixRamp.Reset(targetMix);
                _rampsInitialized = true;
            }

            if (_morphSamplesRemaining > 0)
            {
                if (!_delayRamp.IsActive) _delayRamp.SetTarget(targetDelayMs, _morphSamplesRemaining);
                if (!_depthRamp.IsActive) _depthRamp.SetTarget(targetDepthMs, _morphSamplesRemaining);
                if (!_rateRamp.IsActive) _rateRamp.SetTarget(targetRate, _morphSamplesRemaining);
                if (!_feedbackRamp.IsActive) _feedbackRamp.SetTarget(targetFeedback, _morphSamplesRemaining);
                if (!_mixRamp.IsActive) _mixRamp.SetTarget(targetMix, _morphSamplesRemaining);
                _morphSamplesRemaining = Mathf.Max(0, _morphSamplesRemaining - samplesPerChannel);
            }
            else
            {
                _delayRamp.SetTarget(targetDelayMs, samplesPerChannel);
                _depthRamp.SetTarget(targetDepthMs, samplesPerChannel);
                _rateRamp.SetTarget(targetRate, samplesPerChannel);
                _feedbackRamp.SetTarget(targetFeedback, samplesPerChannel);
                _mixRamp.SetTarget(targetMix, samplesPerChannel);
            }

            // Pre-calculate conversion constants once per block
            float msToSamples = sampleRate * 0.001f;
            float hpfHz = Mathf.Clamp(FeedbackHPF, 20f, sampleRate * 0.45f);
            float lpfHz = Mathf.Clamp(FeedbackLPF, 20f, sampleRate * 0.45f);
            if (lpfHz < hpfHz) lpfHz = hpfHz;

            float hpfCoef = 1f - Mathf.Exp(-2f * Mathf.PI * hpfHz / sampleRate);
            float lpfCoef = Mathf.Exp(-2f * Mathf.PI * lpfHz / sampleRate);
            
            for (int i = 0; i < samplesPerChannel; i++)
            {
                // Get mono input
                float input = 0f;
                for (int ch = 0; ch < channels; ch++)
                {
                    input += data[i * channels + ch];
                }
                input /= channels;

                float rate = _rateRamp.Next();
                _lfo.SetFrequency(rate, sampleRate);  // Update oscillator frequency
                float baseDelaySamples = _delayRamp.Next() * msToSamples;
                float modDepthSamples = _depthRamp.Next() * msToSamples;
                float feedback = _feedbackRamp.Next();
                float mix = _mixRamp.Next();

                // Modulated delay time using quadrature oscillator (no Mathf.Sin call!)
                float modulation = _lfo.Next() * modDepthSamples;
                float delaySamples = baseDelaySamples + modulation;
                delaySamples = Mathf.Clamp(delaySamples, 1, _bufferSize - 2);
                
                // Interpolated read (for smooth modulation)
                int baseIndex = (_writePosition - (int)delaySamples + _bufferSize) % _bufferSize;
                int indexM1 = (baseIndex + 1) % _bufferSize;
                int index0 = baseIndex;
                int index1 = (baseIndex - 1 + _bufferSize) % _bufferSize;
                int index2 = (baseIndex - 2 + _bufferSize) % _bufferSize;
                float frac = delaySamples - (int)delaySamples;

                float delayed = DSPConstants.HermiteInterpolation(
                    _delayBuffer[indexM1],
                    _delayBuffer[index0],
                    _delayBuffer[index1],
                    _delayBuffer[index2],
                    frac);

                // Filter feedback
                _hpfState += hpfCoef * (delayed - _hpfState);
                float hpFiltered = delayed - _hpfState;
                _lpfState = lpfCoef * _lpfState + (1f - lpfCoef) * hpFiltered;
                float filtered = DSPConstants.FlushDenormal(_lpfState);

                _hpfState = DSPConstants.FlushDenormal(_hpfState);
                _lpfState = DSPConstants.FlushDenormal(_lpfState);

                // Write with feedback and DC offset to prevent denormals
                float feedbackSample = DSPConstants.SoftClip(filtered * feedback);
                _delayBuffer[_writePosition] = input + feedbackSample + DSPConstants.DC_OFFSET;
                _writePosition = (_writePosition + 1) % _bufferSize;

                // Output
                float output = input * (1f - mix) + DSPConstants.SoftClip(delayed) * mix;

                for (int ch = 0; ch < channels; ch++)
                {
                    data[i * channels + ch] = output;
                }
           }
        }

        public override void ApplyPreset(string presetName)
        {
            if (string.IsNullOrEmpty(presetName)) return;

            switch (presetName.Trim().ToLowerInvariant())
            {
                case "tape echo":
                    DelayTime = 320f;
                    ModDepth = 6f;
                    ModRate = 0.4f;
                    Feedback = 0.45f;
                    Mix = 0.4f;
                    break;
                case "vintage":
                    DelayTime = 260f;
                    ModDepth = 4f;
                    ModRate = 0.3f;
                    Feedback = 0.35f;
                    Mix = 0.35f;
                    break;
                case "wobble":
                    DelayTime = 200f;
                    ModDepth = 8f;
                    ModRate = 0.8f;
                    Feedback = 0.4f;
                    Mix = 0.4f;
                    break;
                case "subtle":
                    DelayTime = 180f;
                    ModDepth = 2f;
                    ModRate = 0.2f;
                    Feedback = 0.25f;
                    Mix = 0.25f;
                    break;
                case "lofi":
                    DelayTime = 420f;
                    ModDepth = 6f;
                    ModRate = 0.6f;
                    Feedback = 0.45f;
                    Mix = 0.4f;
                    break;
            }
        }



        private void EnsureInitialized(int sampleRate)
        {
            int requiredSize = (int)(1.5f * sampleRate);
            
            if (!_initialized || _lastSampleRate != sampleRate || _bufferSize < requiredSize)
            {
                _bufferSize = requiredSize;
                _delayBuffer = new float[_bufferSize];
                _writePosition = 0;
                _lfo.Init(0f);
                _hpfState = 0f;
                _lpfState = 0f;
                _lastSampleRate = sampleRate;
                _initialized = true;
            }
        }
        
        public override void Reset()
        {
            if (_delayBuffer != null)
                Array.Clear(_delayBuffer, 0, _delayBuffer.Length);
            _writePosition = 0;
            _lfo.Reset();
            _hpfState = 0f;
            _lpfState = 0f;
            _rampsInitialized = false;
            _morphSamplesRemaining = 0;
        }

        public void SetMorphTarget(IDSPEffect target, int samples)
        {
            if (target is not ModulatedDelay other) return;
            DelayTime = other.DelayTime;
            ModDepth = other.ModDepth;
            ModRate = other.ModRate;
            Feedback = other.Feedback;
            FeedbackHPF = other.FeedbackHPF;
            FeedbackLPF = other.FeedbackLPF;
            Mix = other.Mix;
            Enabled = other.Enabled;
            _morphSamplesRemaining = Mathf.Max(0, samples);
        }
    }
}
