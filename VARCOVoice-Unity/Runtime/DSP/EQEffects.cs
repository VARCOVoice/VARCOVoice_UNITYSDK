using System;
using UnityEngine;

namespace VARCOVoice.DSP
{
    /// <summary>
    /// 5-band parametric equalizer
    /// </summary>
    [Serializable]
    public class EQEffect : DSPEffectBase
    {
        public override string Name => "EQ";
        
        /// <summary>
        /// Bass (low frequency) gain in dB (-20 to +20)
        /// </summary>
        [Range(-20f, 20f)]
        public float Bass { get; set; } = 0f;
        
        /// <summary>
        /// Low-mid frequency gain in dB
        /// </summary>
        [Range(-20f, 20f)]
        public float LowMid { get; set; } = 0f;
        
        /// <summary>
        /// Mid frequency gain in dB
        /// </summary>
        [Range(-20f, 20f)]
        public float Mid { get; set; } = 0f;
        
        /// <summary>
        /// High-mid frequency gain in dB
        /// </summary>
        [Range(-20f, 20f)]
        public float HighMid { get; set; } = 0f;
        
        /// <summary>
        /// Treble (high frequency) gain in dB
        /// </summary>
        [Range(-20f, 20f)]
        public float Treble { get; set; } = 0f;
        
        // Biquad filter states for each band (2 channels x 5 bands)
        private float[,] _x1, _x2, _y1, _y2;
        private bool _initialized;
        private int _lastSampleRate;
        
        // Filter frequencies
        private readonly float[] _frequencies = { 80f, 250f, 1000f, 4000f, 12000f };
        private readonly float _q = 1.0f;
        
        private float[,] _a0, _a1, _a2, _b0, _b1, _b2;
        
        public override void Process(float[] data, int channels, int sampleRate)
        {
            EnsureInitialized(channels, sampleRate);
            
            // Update coefficients if gains changed
            UpdateCoefficients(sampleRate);
            
            int samplesPerChannel = data.Length / channels;
            
            for (int ch = 0; ch < channels; ch++)
            {
                for (int i = 0; i < samplesPerChannel; i++)
                {
                    int idx = i * channels + ch;
                    float sample = data[idx];
                    
                    // Process through each band
                    for (int band = 0; band < 5; band++)
                    {
                        float x0 = sample;
                        
                        // Biquad transposed direct form II
                        float output = (_b0[band, ch] * x0 + _b1[band, ch] * _x1[band, ch] + _b2[band, ch] * _x2[band, ch]
                                       - _a1[band, ch] * _y1[band, ch] - _a2[band, ch] * _y2[band, ch]) / _a0[band, ch];
                        
                        // Update state
                        _x2[band, ch] = _x1[band, ch];
                        _x1[band, ch] = x0;
                        _y2[band, ch] = _y1[band, ch];
                        _y1[band, ch] = output;
                        
                        sample = output;
                    }
                    
                    data[idx] = sample;
                }
            }
        }
        
        private void EnsureInitialized(int channels, int sampleRate)
        {
            if (_initialized && _lastSampleRate == sampleRate) return;
            
            _x1 = new float[5, channels];
            _x2 = new float[5, channels];
            _y1 = new float[5, channels];
            _y2 = new float[5, channels];
            
            _a0 = new float[5, channels];
            _a1 = new float[5, channels];
            _a2 = new float[5, channels];
            _b0 = new float[5, channels];
            _b1 = new float[5, channels];
            _b2 = new float[5, channels];
            
            _lastSampleRate = sampleRate;
            _initialized = true;
            
            UpdateCoefficients(sampleRate);
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
                
                // Peaking EQ coefficients
                float b0 = 1f + alpha * A;
                float b1 = -2f * Mathf.Cos(w0);
                float b2 = 1f - alpha * A;
                float a0 = 1f + alpha / A;
                float a1 = -2f * Mathf.Cos(w0);
                float a2 = 1f - alpha / A;
                
                for (int ch = 0; ch < _a0.GetLength(1); ch++)
                {
                    _a0[band, ch] = a0;
                    _a1[band, ch] = a1;
                    _a2[band, ch] = a2;
                    _b0[band, ch] = b0;
                    _b1[band, ch] = b1;
                    _b2[band, ch] = b2;
                }
            }
        }
        
        public override void Reset()
        {
            _initialized = false;
        }
    }
    
    /// <summary>
    /// Low-pass filter effect
    /// </summary>
    [Serializable]
    public class LowPassEffect : DSPEffectBase
    {
        public override string Name => "Low Pass";
        
        /// <summary>
        /// Cutoff frequency in Hz
        /// </summary>
        [Range(100f, 20000f)]
        public float CutoffFrequency { get; set; } = 5000f;
        
        /// <summary>
        /// Resonance (Q factor)
        /// </summary>
        [Range(0.5f, 10f)]
        public float Resonance { get; set; } = 0.707f;
        
        // Biquad filter states
        private float[] _x1, _x2, _y1, _y2;
        private float _a0, _a1, _a2, _b0, _b1, _b2;
        private float _lastCutoff, _lastResonance;
        private int _lastSampleRate;
        private bool _initialized;
        
        public override void Process(float[] data, int channels, int sampleRate)
        {
            EnsureInitialized(channels, sampleRate);
            
            // Update coefficients if parameters changed
            if (!Mathf.Approximately(_lastCutoff, CutoffFrequency) ||
                !Mathf.Approximately(_lastResonance, Resonance))
            {
                UpdateCoefficients(sampleRate);
            }
            
            int samplesPerChannel = data.Length / channels;
            
            for (int ch = 0; ch < channels; ch++)
            {
                for (int i = 0; i < samplesPerChannel; i++)
                {
                    int idx = i * channels + ch;
                    float x0 = data[idx];
                    
                    float output = (_b0 * x0 + _b1 * _x1[ch] + _b2 * _x2[ch]
                                   - _a1 * _y1[ch] - _a2 * _y2[ch]) / _a0;
                    
                    _x2[ch] = _x1[ch];
                    _x1[ch] = x0;
                    _y2[ch] = _y1[ch];
                    _y1[ch] = output;
                    
                    data[idx] = output;
                }
            }
        }
        
        private void EnsureInitialized(int channels, int sampleRate)
        {
            if (_initialized && _lastSampleRate == sampleRate) return;
            
            _x1 = new float[channels];
            _x2 = new float[channels];
            _y1 = new float[channels];
            _y2 = new float[channels];
            
            _lastSampleRate = sampleRate;
            _initialized = true;
            
            UpdateCoefficients(sampleRate);
        }
        
        private void UpdateCoefficients(int sampleRate)
        {
            float w0 = 2f * Mathf.PI * CutoffFrequency / sampleRate;
            float alpha = Mathf.Sin(w0) / (2f * Resonance);
            
            _b0 = (1f - Mathf.Cos(w0)) / 2f;
            _b1 = 1f - Mathf.Cos(w0);
            _b2 = (1f - Mathf.Cos(w0)) / 2f;
            _a0 = 1f + alpha;
            _a1 = -2f * Mathf.Cos(w0);
            _a2 = 1f - alpha;
            
            _lastCutoff = CutoffFrequency;
            _lastResonance = Resonance;
        }
        
        public override void Reset()
        {
            _initialized = false;
        }
    }
    
    /// <summary>
    /// Chorus effect
    /// </summary>
    [Serializable]
    public class ChorusEffect : DSPEffectBase
    {
        public override string Name => "Chorus";
        
        /// <summary>
        /// Delay time in milliseconds
        /// </summary>
        [Range(1f, 50f)]
        public float DelayMs { get; set; } = 20f;
        
        /// <summary>
        /// Modulation depth in milliseconds
        /// </summary>
        [Range(0f, 10f)]
        public float Depth { get; set; } = 3f;
        
        /// <summary>
        /// Modulation rate in Hz
        /// </summary>
        [Range(0.1f, 5f)]
        public float Rate { get; set; } = 0.5f;
        
        /// <summary>
        /// Number of voices (1-4)
        /// </summary>
        [Range(1, 4)]
        public int Voices { get; set; } = 2;
        
        private float[] _buffer;
        private int _bufferSize;
        private int _writePosition;
        private float _phase;
        private bool _initialized;
        
        public override void Process(float[] data, int channels, int sampleRate)
        {
            EnsureInitialized(sampleRate);
            
            float delaySamples = DelayMs * sampleRate / 1000f;
            float depthSamples = Depth * sampleRate / 1000f;
            float phaseIncrement = Rate * 2f * Mathf.PI / sampleRate;
            
            int samplesPerChannel = data.Length / channels;
            
            for (int i = 0; i < samplesPerChannel; i++)
            {
                // Get mono input (average channels)
                float input = 0f;
                for (int ch = 0; ch < channels; ch++)
                {
                    input += data[i * channels + ch];
                }
                input /= channels;
                
                // Write to buffer
                _buffer[_writePosition] = input;
                
                // Calculate chorus output
                float chorusOutput = 0f;
                for (int voice = 0; voice < Voices; voice++)
                {
                    float voicePhase = _phase + (voice * Mathf.PI * 2f / Voices);
                    float modulation = Mathf.Sin(voicePhase) * depthSamples;
                    float readPosition = _writePosition - delaySamples - modulation;
                    
                    if (readPosition < 0) readPosition += _bufferSize;
                    
                    // Linear interpolation
                    int readIndex0 = (int)readPosition % _bufferSize;
                    int readIndex1 = (readIndex0 + 1) % _bufferSize;
                    float frac = readPosition - (int)readPosition;
                    
                    chorusOutput += _buffer[readIndex0] * (1f - frac) + _buffer[readIndex1] * frac;
                }
                chorusOutput /= Voices;
                
                // Apply to all channels
                for (int ch = 0; ch < channels; ch++)
                {
                    int idx = i * channels + ch;
                    data[idx] = data[idx] * (1f - Mix) + chorusOutput * Mix;
                }
                
                _writePosition = (_writePosition + 1) % _bufferSize;
                _phase += phaseIncrement;
                
                if (_phase >= Mathf.PI * 2f) _phase -= Mathf.PI * 2f;
            }
        }
        
        private void EnsureInitialized(int sampleRate)
        {
            int requiredSize = (int)((DelayMs + Depth) * 2 * sampleRate / 1000f) + 1;
            
            if (_buffer == null || _bufferSize < requiredSize)
            {
                _bufferSize = requiredSize;
                _buffer = new float[_bufferSize];
                _writePosition = 0;
                _initialized = true;
            }
        }
        
        public override void Reset()
        {
            if (_buffer != null)
                Array.Clear(_buffer, 0, _buffer.Length);
            _writePosition = 0;
            _phase = 0f;
        }
    }
}
