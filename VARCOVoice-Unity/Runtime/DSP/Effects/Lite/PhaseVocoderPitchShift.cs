using System;
using UnityEngine;

namespace VARCOVoice.DSP
{
    /// <summary>
    /// High-quality pitch shift using Phase Vocoder algorithm
    /// Enterprise-grade implementation with minimal artifacts
    /// </summary>
    [Serializable]
    public class PhaseVocoderPitchShift : DSPEffectBase
    {
        public override string Name => "Phase Vocoder Pitch";
        
        /// <summary>
        /// Pitch shift in semitones (-12 to +12)
        /// </summary>
        public float Semitones
        {
            get => _semitones;
            set
            {
                _semitones = Mathf.Clamp(value, -12f, 12f);
                _pitchRatio = Mathf.Pow(2f, _semitones / 12f);
            }
        }
        private float _semitones = 0f;
        private float _pitchRatio = 1f;
        
        /// <summary>
        /// FFT window size (larger = better quality but more latency)
        /// </summary>
        public int WindowSize
        {
            get => _windowSize;
            set
            {
                _windowSize = Mathf.ClosestPowerOfTwo(Mathf.Clamp(value, 256, 4096));
                _needsReinit = true;
            }
        }
        private int _windowSize = 2048;
        
        /// <summary>
        /// Overlap factor (higher = smoother but more CPU)
        /// </summary>
        public int OverlapFactor
        {
            get => _overlapFactor;
            set
            {
                _overlapFactor = Mathf.Clamp(value, 2, 8);
                _needsReinit = true;
            }
        }
        private int _overlapFactor = 4;
        
        // Internal buffers
        private float[] _inputBuffer;
        private float[] _outputBuffer;
        private float[] _windowFunction;
        private float[] _fftReal;
        private float[] _fftImag;
        private float[] _lastPhase;
        private float[] _sumPhase;
        private float[] _analysisFreq;
        private float[] _analysisMag;
        private float[] _synthFreq;
        private float[] _synthMag;
        
        private int _inputWritePos;
        private int _outputReadPos;
        private int _hopSize;
        private int _sampleRate;
        private bool _needsReinit = true;
        private bool _initialized;
        
        // Constants
        private const float TWO_PI = Mathf.PI * 2f;
        
        public PhaseVocoderPitchShift()
        {
            Semitones = 0f;
        }
        
        public override void Process(float[] data, int channels, int sampleRate)
        {
            if (Mathf.Approximately(_semitones, 0f)) return;
            
            EnsureInitialized(sampleRate);
            
            // Process mono or first channel only for simplicity
            // In production, process each channel separately
            int samplesPerChannel = data.Length / channels;
            
            for (int i = 0; i < samplesPerChannel; i++)
            {
                // Get mono sample
                float input = 0f;
                for (int ch = 0; ch < channels; ch++)
                {
                    input += data[i * channels + ch];
                }
                input /= channels;
                
                // Add to input buffer
                _inputBuffer[_inputWritePos] = input;
                _inputWritePos = (_inputWritePos + 1) % (_windowSize * 2);
                
                // Get output sample
                float output = _outputBuffer[_outputReadPos];
                _outputBuffer[_outputReadPos] = 0f;
                _outputReadPos = (_outputReadPos + 1) % (_windowSize * 2);
                
                // Apply to all channels
                for (int ch = 0; ch < channels; ch++)
                {
                    data[i * channels + ch] = output;
                }
                
                // Process when we have enough samples
                if (_inputWritePos % _hopSize == 0)
                {
                    ProcessFrame();
                }
            }
        }
        
        private void EnsureInitialized(int sampleRate)
        {
            if (_initialized && !_needsReinit && _sampleRate == sampleRate) return;
            
            _sampleRate = sampleRate;
            _hopSize = _windowSize / _overlapFactor;
            
            int bufferSize = _windowSize * 2;
            
            _inputBuffer = new float[bufferSize];
            _outputBuffer = new float[bufferSize];
            _windowFunction = new float[_windowSize];
            _fftReal = new float[_windowSize];
            _fftImag = new float[_windowSize];
            _lastPhase = new float[_windowSize / 2 + 1];
            _sumPhase = new float[_windowSize / 2 + 1];
            _analysisFreq = new float[_windowSize / 2 + 1];
            _analysisMag = new float[_windowSize / 2 + 1];
            _synthFreq = new float[_windowSize / 2 + 1];
            _synthMag = new float[_windowSize / 2 + 1];
            
            // Hann window
            for (int i = 0; i < _windowSize; i++)
            {
                _windowFunction[i] = 0.5f * (1f - Mathf.Cos(TWO_PI * i / _windowSize));
            }
            
            _inputWritePos = 0;
            _outputReadPos = 0;
            _needsReinit = false;
            _initialized = true;
        }

        public override void ApplyPreset(string presetName)
        {
            if (string.IsNullOrEmpty(presetName)) return;

            switch (presetName.Trim().ToLowerInvariant())
            {
                case "up 3":
                    Semitones = 3f;
                    WindowSize = 2048;
                    OverlapFactor = 4;
                    Mix = 1f;
                    break;
                case "down 3":
                    Semitones = -3f;
                    WindowSize = 2048;
                    OverlapFactor = 4;
                    Mix = 1f;
                    break;
                case "octave up":
                    Semitones = 12f;
                    WindowSize = 4096;
                    OverlapFactor = 6;
                    Mix = 1f;
                    break;
                case "octave down":
                    Semitones = -12f;
                    WindowSize = 4096;
                    OverlapFactor = 6;
                    Mix = 1f;
                    break;
                case "doubler":
                    Semitones = 7f;
                    WindowSize = 2048;
                    OverlapFactor = 4;
                    Mix = 0.5f;
                    break;
            }
        }
        
        private void ProcessFrame()
        {
            int halfWindow = _windowSize / 2;
            float freqPerBin = (float)_sampleRate / _windowSize;
            float expectedPhaseDiff = TWO_PI * _hopSize / _windowSize;
            
            // 1. Apply window and copy to FFT buffer
            int readPos = (_inputWritePos - _windowSize + _inputBuffer.Length) % _inputBuffer.Length;
            for (int i = 0; i < _windowSize; i++)
            {
                int idx = (readPos + i) % _inputBuffer.Length;
                _fftReal[i] = _inputBuffer[idx] * _windowFunction[i];
                _fftImag[i] = 0f;
            }
            
            // 2. FFT
            FFT(_fftReal, _fftImag, false);
            
            // 3. Analysis: Convert to magnitude and frequency
            for (int k = 0; k <= halfWindow; k++)
            {
                float real = _fftReal[k];
                float imag = _fftImag[k];
                
                float mag = 2f * Mathf.Sqrt(real * real + imag * imag);
                float phase = Mathf.Atan2(imag, real);
                
                // Calculate true frequency
                float phaseDiff = phase - _lastPhase[k];
                _lastPhase[k] = phase;
                
                // Wrap phase difference
                phaseDiff -= k * expectedPhaseDiff;
                phaseDiff = WrapPhase(phaseDiff);
                
                // Get deviation from bin frequency
                float freqDeviation = phaseDiff * _overlapFactor / TWO_PI;
                
                // True frequency
                _analysisFreq[k] = k * freqPerBin + freqDeviation * freqPerBin;
                _analysisMag[k] = mag;
            }
            
            // 4. Pitch shifting: Resample in frequency domain
            Array.Clear(_synthMag, 0, _synthMag.Length);
            Array.Clear(_synthFreq, 0, _synthFreq.Length);
            
            for (int k = 0; k <= halfWindow; k++)
            {
                int newBin = (int)(k * _pitchRatio);
                if (newBin >= 0 && newBin <= halfWindow)
                {
                    _synthMag[newBin] += _analysisMag[k];
                    _synthFreq[newBin] = _analysisFreq[k] * _pitchRatio;
                }
            }
            
            // 5. Synthesis: Convert back to complex
            for (int k = 0; k <= halfWindow; k++)
            {
                float mag = _synthMag[k];
                
                // Expected phase increment
                float freqDeviation = _synthFreq[k] - k * freqPerBin;
                float phaseDiff = freqDeviation / freqPerBin * TWO_PI / _overlapFactor;
                phaseDiff += k * expectedPhaseDiff;
                
                _sumPhase[k] += phaseDiff;
                float phase = _sumPhase[k];
                
                _fftReal[k] = mag * Mathf.Cos(phase);
                _fftImag[k] = mag * Mathf.Sin(phase);
                
                // Mirror for negative frequencies
                if (k > 0 && k < halfWindow)
                {
                    _fftReal[_windowSize - k] = _fftReal[k];
                    _fftImag[_windowSize - k] = -_fftImag[k];
                }
            }
            
            // 6. Inverse FFT
            FFT(_fftReal, _fftImag, true);
            
            // 7. Overlap-add to output buffer
            int writePos = _outputReadPos;
            float scale = 1f / _overlapFactor;
            
            for (int i = 0; i < _windowSize; i++)
            {
                int idx = (writePos + i) % _outputBuffer.Length;
                _outputBuffer[idx] += _fftReal[i] * _windowFunction[i] * scale;
            }
        }
        
        /// <summary>
        /// In-place Cooley-Tukey FFT
        /// </summary>
        private void FFT(float[] real, float[] imag, bool inverse)
        {
            int n = real.Length;
            int bits = (int)Mathf.Log(n, 2);
            
            // Bit-reversal permutation
            for (int i = 0; i < n; i++)
            {
                int j = BitReverse(i, bits);
                if (j > i)
                {
                    (real[i], real[j]) = (real[j], real[i]);
                    (imag[i], imag[j]) = (imag[j], imag[i]);
                }
            }
            
            // Cooley-Tukey
            for (int len = 2; len <= n; len *= 2)
            {
                float angle = (inverse ? TWO_PI : -TWO_PI) / len;
                float wReal = Mathf.Cos(angle);
                float wImag = Mathf.Sin(angle);
                
                for (int i = 0; i < n; i += len)
                {
                    float wCurReal = 1f;
                    float wCurImag = 0f;
                    
                    for (int j = 0; j < len / 2; j++)
                    {
                        int a = i + j;
                        int b = i + j + len / 2;
                        
                        float tReal = wCurReal * real[b] - wCurImag * imag[b];
                        float tImag = wCurReal * imag[b] + wCurImag * real[b];
                        
                        real[b] = real[a] - tReal;
                        imag[b] = imag[a] - tImag;
                        real[a] = real[a] + tReal;
                        imag[a] = imag[a] + tImag;
                        
                        float newWReal = wCurReal * wReal - wCurImag * wImag;
                        wCurImag = wCurReal * wImag + wCurImag * wReal;
                        wCurReal = newWReal;
                    }
                }
            }
            
            // Normalize for inverse
            if (inverse)
            {
                for (int i = 0; i < n; i++)
                {
                    real[i] /= n;
                    imag[i] /= n;
                }
            }
        }
        
        private int BitReverse(int x, int bits)
        {
            int result = 0;
            for (int i = 0; i < bits; i++)
            {
                result = (result << 1) | (x & 1);
                x >>= 1;
            }
            return result;
        }
        
        private float WrapPhase(float phase)
        {
            while (phase > Mathf.PI) phase -= TWO_PI;
            while (phase < -Mathf.PI) phase += TWO_PI;
            return phase;
        }
        
        public override void Reset()
        {
            _needsReinit = true;
            _initialized = false;
            
            if (_inputBuffer != null) Array.Clear(_inputBuffer, 0, _inputBuffer.Length);
            if (_outputBuffer != null) Array.Clear(_outputBuffer, 0, _outputBuffer.Length);
            if (_lastPhase != null) Array.Clear(_lastPhase, 0, _lastPhase.Length);
            if (_sumPhase != null) Array.Clear(_sumPhase, 0, _sumPhase.Length);
        }
    }
}
