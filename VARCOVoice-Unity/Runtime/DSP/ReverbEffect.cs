using System;
using UnityEngine;

namespace VARCOVoice.DSP
{
    /// <summary>
    /// Reverb presets
    /// </summary>
    public enum ReverbPreset
    {
        Off,
        Room,
        Hall,
        Cave,
        Arena,
        Bathroom,
        Church,
        Underwater
    }
    
    /// <summary>
    /// Simple reverb effect using comb and allpass filters
    /// </summary>
    [Serializable]
    public class ReverbEffect : DSPEffectBase
    {
        public override string Name => "Reverb";
        
        /// <summary>
        /// Reverb preset
        /// </summary>
        public ReverbPreset Preset
        {
            get => _preset;
            set
            {
                _preset = value;
                ApplyPreset(value);
            }
        }
        private ReverbPreset _preset = ReverbPreset.Room;
        
        /// <summary>
        /// Room size (0-1)
        /// </summary>
        [Range(0f, 1f)]
        public float RoomSize { get; set; } = 0.5f;
        
        /// <summary>
        /// Damping (0-1) - higher values = less high frequency reflections
        /// </summary>
        [Range(0f, 1f)]
        public float Damping { get; set; } = 0.5f;
        
        /// <summary>
        /// Decay time in seconds
        /// </summary>
        [Range(0.1f, 10f)]
        public float DecayTime { get; set; } = 1.5f;
        
        // Comb filter delays (in samples at 44100 Hz)
        private readonly int[] _combDelays = { 1116, 1188, 1277, 1356, 1422, 1491, 1557, 1617 };
        private readonly int[] _allpassDelays = { 556, 441, 341, 225 };
        
        // Comb filter buffers
        private float[][] _combBuffers;
        private int[] _combPositions;
        private float[] _combFilters;
        
        // Allpass filter buffers
        private float[][] _allpassBuffers;
        private int[] _allpassPositions;
        
        private bool _initialized;
        private int _lastSampleRate;
        
        public override void Process(float[] data, int channels, int sampleRate)
        {
            if (Preset == ReverbPreset.Off || Mix <= 0) return;
            
            EnsureInitialized(sampleRate);
            
            float feedback = 0.7f + RoomSize * 0.28f;
            float damp = Damping * 0.4f;
            
            int samplesPerChannel = data.Length / channels;
            
            for (int i = 0; i < samplesPerChannel; i++)
            {
                // Mix all channels to mono for reverb processing
                float input = 0f;
                for (int ch = 0; ch < channels; ch++)
                {
                    input += data[i * channels + ch];
                }
                input /= channels;
                
                // Process through comb filters (in parallel)
                float combOutput = 0f;
                for (int c = 0; c < _combBuffers.Length; c++)
                {
                    int pos = _combPositions[c];
                    float output = _combBuffers[c][pos];
                    
                    // Lowpass filter for damping
                    _combFilters[c] = output * (1f - damp) + _combFilters[c] * damp;
                    
                    // Feedback
                    _combBuffers[c][pos] = input + _combFilters[c] * feedback;
                    
                    _combPositions[c] = (pos + 1) % _combBuffers[c].Length;
                    combOutput += output;
                }
                combOutput /= _combBuffers.Length;
                
                // Process through allpass filters (in series)
                float allpassOutput = combOutput;
                for (int a = 0; a < _allpassBuffers.Length; a++)
                {
                    int pos = _allpassPositions[a];
                    float bufferValue = _allpassBuffers[a][pos];
                    float newValue = allpassOutput + bufferValue * 0.5f;
                    
                    _allpassBuffers[a][pos] = newValue;
                    allpassOutput = bufferValue - allpassOutput * 0.5f;
                    
                    _allpassPositions[a] = (pos + 1) % _allpassBuffers[a].Length;
                }
                
                // Apply wet signal to all channels
                for (int ch = 0; ch < channels; ch++)
                {
                    int idx = i * channels + ch;
                    data[idx] = data[idx] * (1f - Mix) + allpassOutput * Mix;
                }
            }
        }
        
        private void EnsureInitialized(int sampleRate)
        {
            if (_initialized && _lastSampleRate == sampleRate) return;
            
            float sampleRateRatio = sampleRate / 44100f;
            
            // Initialize comb filters
            _combBuffers = new float[_combDelays.Length][];
            _combPositions = new int[_combDelays.Length];
            _combFilters = new float[_combDelays.Length];
            
            for (int i = 0; i < _combDelays.Length; i++)
            {
                int delay = (int)(_combDelays[i] * sampleRateRatio * (0.8f + RoomSize * 0.4f));
                _combBuffers[i] = new float[delay];
                _combPositions[i] = 0;
                _combFilters[i] = 0;
            }
            
            // Initialize allpass filters
            _allpassBuffers = new float[_allpassDelays.Length][];
            _allpassPositions = new int[_allpassDelays.Length];
            
            for (int i = 0; i < _allpassDelays.Length; i++)
            {
                int delay = (int)(_allpassDelays[i] * sampleRateRatio);
                _allpassBuffers[i] = new float[delay];
                _allpassPositions[i] = 0;
            }
            
            _lastSampleRate = sampleRate;
            _initialized = true;
        }
        
        private void ApplyPreset(ReverbPreset preset)
        {
            switch (preset)
            {
                case ReverbPreset.Off:
                    Mix = 0f;
                    break;
                case ReverbPreset.Room:
                    RoomSize = 0.3f; Damping = 0.5f; DecayTime = 0.8f; Mix = 0.3f;
                    break;
                case ReverbPreset.Hall:
                    RoomSize = 0.7f; Damping = 0.3f; DecayTime = 2.5f; Mix = 0.4f;
                    break;
                case ReverbPreset.Cave:
                    RoomSize = 0.9f; Damping = 0.2f; DecayTime = 4f; Mix = 0.5f;
                    break;
                case ReverbPreset.Arena:
                    RoomSize = 1f; Damping = 0.4f; DecayTime = 3f; Mix = 0.4f;
                    break;
                case ReverbPreset.Bathroom:
                    RoomSize = 0.2f; Damping = 0.6f; DecayTime = 0.5f; Mix = 0.5f;
                    break;
                case ReverbPreset.Church:
                    RoomSize = 0.85f; Damping = 0.25f; DecayTime = 5f; Mix = 0.45f;
                    break;
                case ReverbPreset.Underwater:
                    RoomSize = 0.6f; Damping = 0.8f; DecayTime = 2f; Mix = 0.6f;
                    break;
            }
            
            _initialized = false; // Force reinitialization
        }
        
        public override void Reset()
        {
            _initialized = false;
            
            if (_combBuffers != null)
            {
                foreach (var buffer in _combBuffers)
                    if (buffer != null) Array.Clear(buffer, 0, buffer.Length);
            }
            
            if (_allpassBuffers != null)
            {
                foreach (var buffer in _allpassBuffers)
                    if (buffer != null) Array.Clear(buffer, 0, buffer.Length);
            }
        }
    }
}
