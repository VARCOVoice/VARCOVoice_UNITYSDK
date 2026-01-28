using UnityEngine;

namespace VARCOVoice.DSP
{
    /// <summary>
    /// Interface for DSP effects
    /// </summary>
    public interface IDSPEffect
    {
        /// <summary>
        /// Effect name for display
        /// </summary>
        string Name { get; }
        
        /// <summary>
        /// Whether the effect is currently enabled
        /// </summary>
        bool Enabled { get; set; }
        
        /// <summary>
        /// Process audio samples in-place
        /// </summary>
        /// <param name="data">Audio sample buffer (interleaved if stereo)</param>
        /// <param name="channels">Number of audio channels</param>
        /// <param name="sampleRate">Sample rate in Hz</param>
        void Process(float[] data, int channels, int sampleRate);
        
        /// <summary>
        /// Reset effect state (clear buffers, etc.)
        /// </summary>
        void Reset();
    }

    internal interface IMorphableEffect
    {
        void SetMorphTarget(IDSPEffect target, int samples);
    }
    
    /// <summary>
    /// Base class for DSP effects with common functionality
    /// </summary>
    [System.Serializable]
    public abstract class DSPEffectBase : IDSPEffect
    {
        public abstract string Name { get; }
        [SerializeField] private bool _enabled = true;
        public bool Enabled { get => _enabled; set => _enabled = value; }
        
        /// <summary>
        /// Wet/dry mix (0 = dry, 1 = fully wet)
        /// </summary>
        public float Mix { get; set; } = 1f;
        
        public abstract void Process(float[] data, int channels, int sampleRate);
        
        public virtual void Reset() { }

        /// <summary>
        /// Apply a named preset to this effect (optional).
        /// </summary>
        public virtual void ApplyPreset(string presetName) { }

        /// <summary>
        /// Apply wet/dry mix
        /// </summary>
        protected void ApplyMix(float[] original, float[] processed, int length)
        {
            if (Mix >= 1f) return;
            
            float dry = 1f - Mix;
            for (int i = 0; i < length; i++)
            {
                processed[i] = original[i] * dry + processed[i] * Mix;
            }
        }
    }
}
