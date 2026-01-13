using System;
using UnityEngine;

namespace VARCOVoice.DSP
{
    /// <summary>
    /// Pitch shift effect using simple resampling
    /// For higher quality, consider using phase vocoder algorithm
    /// </summary>
    [Serializable]
    public class PitchShiftEffect : DSPEffectBase
    {
        public override string Name => "Pitch Shift";
        
        /// <summary>
        /// Pitch shift in semitones (-12 to +12)
        /// </summary>
        [Range(-12f, 12f)]
        public float Semitones { get; set; } = 0f;
        
        /// <summary>
        /// Pitch shift ratio (calculated from semitones)
        /// </summary>
        public float PitchRatio => Mathf.Pow(2f, Semitones / 12f);
        
        // Circular buffer for resampling
        private float[] _buffer;
        private int _bufferSize = 4096;
        private float _readPosition;
        private int _writePosition;
        
        public PitchShiftEffect()
        {
            _buffer = new float[_bufferSize];
        }
        
        public override void Process(float[] data, int channels, int sampleRate)
        {
            if (Mathf.Approximately(Semitones, 0f)) return;
            
            float ratio = PitchRatio;
            int samplesPerChannel = data.Length / channels;
            
            for (int ch = 0; ch < channels; ch++)
            {
                for (int i = 0; i < samplesPerChannel; i++)
                {
                    int dataIndex = i * channels + ch;
                    
                    // Write to circular buffer
                    _buffer[_writePosition] = data[dataIndex];
                    _writePosition = (_writePosition + 1) % _bufferSize;
                    
                    // Read from circular buffer with interpolation
                    float readPos = _readPosition;
                    int readIndex0 = (int)readPos % _bufferSize;
                    int readIndex1 = (readIndex0 + 1) % _bufferSize;
                    float frac = readPos - (int)readPos;
                    
                    // Linear interpolation
                    float sample = _buffer[readIndex0] * (1f - frac) + _buffer[readIndex1] * frac;
                    data[dataIndex] = sample;
                    
                    // Advance read position by pitch ratio
                    _readPosition += ratio;
                    if (_readPosition >= _bufferSize)
                        _readPosition -= _bufferSize;
                }
            }
        }
        
        public override void Reset()
        {
            Array.Clear(_buffer, 0, _buffer.Length);
            _readPosition = 0;
            _writePosition = 0;
        }
    }
}
