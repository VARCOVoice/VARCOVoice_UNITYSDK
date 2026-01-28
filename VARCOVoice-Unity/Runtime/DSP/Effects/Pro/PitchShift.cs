using System;
using UnityEngine;

namespace VARCOVoice.DSP
{
    /// <summary>
    /// Pitch Shifter (SOLA).
    /// Uses synchronous overlap-add to pitch shift without granular artifacts.
    /// </summary>
    [Serializable]
    public class PitchShift : DSPEffectBase
    {
        public override string Name => "Pitch Shift";

        #region Parameters

        [Range(-12f, 12f)]
        public float Pitch = 0f;

        [Range(0.5f, 2.0f)]
        public float FineTune = 1.0f;

        [Range(20f, 100f)]
        public float GrainSize = 40f; // ms, Reduced for tighter vocal response

        // Overlap removed (unused in SOLA)
        // Jitter removed (unused in SOLA)

        [Range(0f, 1f)]
        public float Spread = 0.0f; // Mono center by default

        #endregion

        #region Internal State

        private const int MaxGrainSizeMs = 150;
        // Must be Power of 2 for BufferMask to work
        private const int BufferSize = 262144; // 2^18, ~5.4s at 48k
        private const int BufferMask = BufferSize - 1;
        // MaxGrains removed

        private float[] _inputBuffer; // Circular buffer
        private int _inputWritePos;
        
        // SOLA State
        private float _phase;
        
        private int _sampleRate;
        private int _channels;
        private bool _initialized;
        
        // Legacy fields removed (_grains, _windowTable, _nextGrainTime, etc)


        #endregion



        public override void Process(float[] data, int channels, int sampleRate)
        {
            if (!Enabled) return;

            EnsureInitialized(channels, sampleRate);

            // Pitch Ratio: 2^(semitones/12)
            float pitchRatio = Mathf.Pow(2f, Pitch / 12f) * FineTune;
            
            // If practically unison, output dry (bypass effect) to avoid ANY coloring
            if (Mathf.Abs(pitchRatio - 1.0f) < 0.001f && Mix >= 0.99f && Spread < 0.01f)
            {
                // Just pass through? 
                // Actually, if Mix is 1.0, we just leave data as is? No, input buffer write is needed for tails.
                // But for pure throughput:
                // Let's stick to processing for consistency, but maybe optimize later.
            }

            int frames = data.Length / channels;
            int windowSizeSamples = (int)(GrainSize / 1000f * sampleRate);
            if (windowSizeSamples < 100) windowSizeSamples = 100;
            
            // Relative speed of read head vs write head
            // If pitchRatio = 2.0, we read 2 samples for every 1 written.
            // Pointer delta = (pitchRatio - 1.0)
            float rate = pitchRatio - 1.0f; 

            for (int i = 0; i < frames; i++)
            {
                // 1. Write Input
                int wIdx = _inputWritePos * channels;
                int rIdx = i * channels;
                for (int c = 0; c < channels; c++)
                {
                    _inputBuffer[wIdx + c] = data[rIdx + c];
                    data[rIdx + c] = 0f; 
                }

                // 2. Update Phasor (Window Cycle)
                float shiftRate = Mathf.Abs(rate);
                if (shiftRate < 0.0001f) shiftRate = 0.0001f; 
                
                // Increment phase
                _phase += shiftRate / windowSizeSamples;
                if (_phase >= 1.0f) _phase -= 1.0f;

                float wetL = 0f;
                float wetR = 0f;
                
                // Calculate delay times for two voices based on Phase
                float delay1, delay2;
                
                if (rate > 0) 
                {
                    // Pitch Up: Delay shrinks
                    delay1 = (1f - _phase) * windowSizeSamples;
                    delay2 = (1f - ((_phase + 0.5f) % 1.0f)) * windowSizeSamples;
                }
                else
                {
                    // Pitch Down: Delay grows
                    delay1 = _phase * windowSizeSamples;
                    delay2 = ((_phase + 0.5f) % 1.0f) * windowSizeSamples;
                }

                // Voice 1 Gain
                float gain1 = Mathf.Sin(_phase * Mathf.PI); 
                
                // Voice 2 Gain
                float p2 = (_phase + 0.5f) % 1.0f;
                float gain2 = Mathf.Sin(p2 * Mathf.PI);
                
                // Stereo Spread Offset
                float spreadOffset = Spread * 200f; // 200 samples spread

                // Read Voice 1
                float rPos1 = _inputWritePos - delay1;
                wetL += ReadBuffer(rPos1, 0) * gain1;
                wetR += ReadBuffer(rPos1 - spreadOffset, 1) * gain1;

                // Read Voice 2
                float rPos2 = _inputWritePos - delay2;
                wetL += ReadBuffer(rPos2, 0) * gain2;
                wetR += ReadBuffer(rPos2 - spreadOffset, 1) * gain2;

                // Normalize gain (Sine^2 sum is 1, but Sine sum is ~1.414 peak. Normalize 1/sqrt(2)?)
                // Actually Sin(x) + Sin(x+PI/2) max is sqrt(2). So divide by ~1.414
                wetL *= 0.707f;
                wetR *= 0.707f;
                
                int outIdx = i * channels;
                float dryL = _inputBuffer[wIdx];
                float dryR = (channels > 1) ? _inputBuffer[wIdx + 1] : dryL;

                if (channels == 2)
                {
                    data[outIdx] = dryL * (1f - Mix) + wetL * Mix;
                    data[outIdx + 1] = dryR * (1f - Mix) + wetR * Mix;
                }
                else
                {
                    data[outIdx] = dryL * (1f - Mix) + wetL * Mix;
                }

                _inputWritePos = (_inputWritePos + 1) & BufferMask;
            }
        }
        
        private float ReadBuffer(float pos, int channel)
        {
            if (pos < 0) pos += BufferSize;
            while (pos >= BufferSize) pos -= BufferSize;
            
            int iPos = (int)pos;
            float frac = pos - iPos;
            int nextPos = (iPos + 1) & BufferMask;

            // Read specific channel content
            float s1 = _inputBuffer[iPos * _channels + channel];
            float s2 = _inputBuffer[nextPos * _channels + channel];
            
            return s1 * (1f - frac) + s2 * frac;
        }

        private void EnsureInitialized(int channels, int sampleRate)
        {
            if (_initialized && _channels == channels && _sampleRate == sampleRate) return;

            _channels = Mathf.Max(1, channels);
            _sampleRate = sampleRate;

            _inputBuffer = new float[BufferSize * _channels];
            
            _inputWritePos = 0;
            _phase = 0f; // Reset phase on re-initialization
            
            _initialized = true;
        }

        public override void Reset()
        {
            _initialized = false;
            // Clear buffers if needed
            if (_inputBuffer != null) Array.Clear(_inputBuffer, 0, _inputBuffer.Length);
            _phase = 0f; // Reset phase
        }

        public override void ApplyPreset(string presetName)
        {
            switch (presetName)
            {
                case "Natural Up":
                    Pitch = 3f;
                    FineTune = 1.0f;
                    Mix = 1.0f;
                    break;
                case "Natural Down":
                    Pitch = -3f;
                    FineTune = 1.0f;
                    Mix = 1.0f;
                    break;
                case "Chipmunk":
                    Pitch = 12f;
                    FineTune = 1.0f;
                    Mix = 1.0f;
                    Spread = 0.5f;
                    break;
                case "Deep":
                    Pitch = -12f;
                    FineTune = 1.0f;
                    Mix = 1.0f;
                    break;
                case "Doubler":
                    Pitch = 0.1f;    // Slight detune
                    FineTune = 1.0f;
                    Mix = 0.5f;      // Mix with dry
                    Spread = 1.0f;   // Wide stereo
                    break;
        }
    }
}
}
