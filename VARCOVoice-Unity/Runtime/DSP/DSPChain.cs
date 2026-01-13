using System;
using System.Collections.Generic;
using UnityEngine;

namespace VARCOVoice.DSP
{
    /// <summary>
    /// DSP processing chain - manages multiple audio effects
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    [AddComponentMenu("VARCO Voice/DSP Chain")]
    public class DSPChain : MonoBehaviour
    {
        #region Inspector Fields
        
        [Header("Chain Settings")]
        [SerializeField] private bool enabled = true;
        [SerializeField] private bool bypassWhenInactive = true;
        
        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = false;
        
        #endregion
        
        #region Private Fields
        
        private readonly List<IDSPEffect> _effects = new List<IDSPEffect>();
        private AudioSource _audioSource;
        private int _sampleRate;
        private int _channels;
        
        // Buffers for processing (avoid GC allocation)
        private float[] _tempBuffer;
        
        #endregion
        
        #region Properties
        
        public bool Enabled
        {
            get => enabled;
            set => enabled = value;
        }
        
        public IReadOnlyList<IDSPEffect> Effects => _effects;
        public int EffectCount => _effects.Count;
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            _sampleRate = AudioSettings.outputSampleRate;
            AudioSettings.GetDSPBufferSize(out int bufferSize, out _);
            
            // Get channel count from AudioSettings
            var speakerMode = AudioSettings.GetConfiguration().speakerMode;
            _channels = speakerMode switch
            {
                AudioSpeakerMode.Mono => 1,
                AudioSpeakerMode.Stereo => 2,
                AudioSpeakerMode.Quad => 4,
                AudioSpeakerMode.Surround => 5,
                AudioSpeakerMode.Mode5point1 => 6,
                AudioSpeakerMode.Mode7point1 => 8,
                _ => 2
            };
        }
        
        private void OnEnable()
        {
            ResetAllEffects();
        }
        
        private void OnDisable()
        {
            if (bypassWhenInactive)
            {
                ResetAllEffects();
            }
        }
        
        #endregion
        
        #region DSP Processing
        
        /// <summary>
        /// Unity audio filter callback - processes audio in real-time
        /// Called on audio thread, NOT main thread!
        /// </summary>
        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (!enabled || _effects.Count == 0)
                return;
            
            // Ensure temp buffer is correct size
            if (_tempBuffer == null || _tempBuffer.Length != data.Length)
            {
                _tempBuffer = new float[data.Length];
            }
            
            // Process through effect chain
            foreach (var effect in _effects)
            {
                if (effect.Enabled)
                {
                    try
                    {
                        effect.Process(data, channels, _sampleRate);
                    }
                    catch (Exception ex)
                    {
                        // Log on main thread to avoid threading issues
                        Debug.LogError($"[DSPChain] Effect '{effect.Name}' error: {ex.Message}");
                    }
                }
            }
        }
        
        #endregion
        
        #region Effect Management
        
        /// <summary>
        /// Add effect to the chain
        /// </summary>
        public T AddEffect<T>() where T : IDSPEffect, new()
        {
            var effect = new T();
            _effects.Add(effect);
            return effect;
        }
        
        /// <summary>
        /// Add existing effect instance to the chain
        /// </summary>
        public void AddEffect(IDSPEffect effect)
        {
            if (effect != null && !_effects.Contains(effect))
            {
                _effects.Add(effect);
            }
        }
        
        /// <summary>
        /// Insert effect at specific position
        /// </summary>
        public void InsertEffect(int index, IDSPEffect effect)
        {
            if (effect != null)
            {
                _effects.Insert(Mathf.Clamp(index, 0, _effects.Count), effect);
            }
        }
        
        /// <summary>
        /// Remove effect from chain
        /// </summary>
        public bool RemoveEffect(IDSPEffect effect)
        {
            return _effects.Remove(effect);
        }
        
        /// <summary>
        /// Remove effect at index
        /// </summary>
        public void RemoveEffectAt(int index)
        {
            if (index >= 0 && index < _effects.Count)
            {
                _effects.RemoveAt(index);
            }
        }
        
        /// <summary>
        /// Get effect by type
        /// </summary>
        public T GetEffect<T>() where T : class, IDSPEffect
        {
            foreach (var effect in _effects)
            {
                if (effect is T typed)
                    return typed;
            }
            return null;
        }
        
        /// <summary>
        /// Get or create effect by type
        /// </summary>
        public T GetOrAddEffect<T>() where T : class, IDSPEffect, new()
        {
            var existing = GetEffect<T>();
            if (existing != null) return existing;
            return AddEffect<T>();
        }
        
        /// <summary>
        /// Clear all effects
        /// </summary>
        public void ClearEffects()
        {
            foreach (var effect in _effects)
            {
                effect.Reset();
            }
            _effects.Clear();
        }
        
        /// <summary>
        /// Move effect to new position
        /// </summary>
        public void MoveEffect(int fromIndex, int toIndex)
        {
            if (fromIndex < 0 || fromIndex >= _effects.Count) return;
            if (toIndex < 0 || toIndex >= _effects.Count) return;
            
            var effect = _effects[fromIndex];
            _effects.RemoveAt(fromIndex);
            _effects.Insert(toIndex, effect);
        }
        
        /// <summary>
        /// Reset all effects (clear internal buffers)
        /// </summary>
        public void ResetAllEffects()
        {
            foreach (var effect in _effects)
            {
                effect.Reset();
            }
        }
        
        #endregion
        
        #region Presets
        
        /// <summary>
        /// Setup common effect chain for voice
        /// </summary>
        public void SetupVoicePreset()
        {
            ClearEffects();
            AddEffect<PitchShiftEffect>();
            AddEffect<ReverbEffect>();
            AddEffect<Spatial3DEffect>();
        }
        
        /// <summary>
        /// Setup effect chain for radio/walkie-talkie voice
        /// </summary>
        public void SetupRadioPreset()
        {
            ClearEffects();
            
            var eq = AddEffect<EQEffect>();
            eq.Bass = -10f;
            eq.Treble = 5f;
            
            var lowpass = AddEffect<LowPassEffect>();
            lowpass.CutoffFrequency = 3000f;
            
            // Add subtle distortion for radio effect
            var pitch = AddEffect<PitchShiftEffect>();
            pitch.Semitones = 0;
        }
        
        /// <summary>
        /// Setup effect chain for underwater/muffled voice
        /// </summary>
        public void SetupUnderwaterPreset()
        {
            ClearEffects();
            
            var lowpass = AddEffect<LowPassEffect>();
            lowpass.CutoffFrequency = 800f;
            
            var reverb = AddEffect<ReverbEffect>();
            reverb.Preset = ReverbPreset.Bathroom;
            reverb.Mix = 0.3f;
            
            var pitch = AddEffect<PitchShiftEffect>();
            pitch.Semitones = -2;
        }
        
        #endregion
        
        #region Debug
        
        private void OnGUI()
        {
            if (!showDebugInfo) return;
            
            GUILayout.BeginArea(new Rect(10, 10, 300, 400));
            GUILayout.Label($"DSP Chain - {_effects.Count} effects");
            
            foreach (var effect in _effects)
            {
                var status = effect.Enabled ? "✓" : "✗";
                GUILayout.Label($"  {status} {effect.Name}");
            }
            
            GUILayout.EndArea();
        }
        
        #endregion
    }
}
