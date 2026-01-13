using System;
using UnityEngine;

namespace VARCOVoice.DSP
{
    /// <summary>
    /// 3D Spatial audio effect
    /// Uses Unity's spatialization for positioning
    /// </summary>
    [Serializable]
    public class Spatial3DEffect : DSPEffectBase
    {
        public override string Name => "3D Spatial";
        
        /// <summary>
        /// Audio source position (for distance-based effects)
        /// </summary>
        public Vector3 SourcePosition { get; set; }
        
        /// <summary>
        /// Listener position
        /// </summary>
        public Vector3 ListenerPosition { get; set; }
        
        /// <summary>
        /// Listener forward direction
        /// </summary>
        public Vector3 ListenerForward { get; set; } = Vector3.forward;
        
        /// <summary>
        /// Maximum distance for attenuation
        /// </summary>
        [Range(1f, 500f)]
        public float MaxDistance { get; set; } = 50f;
        
        /// <summary>
        /// Minimum distance (full volume)
        /// </summary>
        [Range(0.1f, 10f)]
        public float MinDistance { get; set; } = 1f;
        
        /// <summary>
        /// Rolloff mode
        /// </summary>
        public AudioRolloffMode RolloffMode { get; set; } = AudioRolloffMode.Logarithmic;
        
        /// <summary>
        /// Stereo spread (0-360 degrees)
        /// </summary>
        [Range(0f, 360f)]
        public float Spread { get; set; } = 90f;
        
        /// <summary>
        /// Doppler level
        /// </summary>
        [Range(0f, 5f)]
        public float DopplerLevel { get; set; } = 0f;
        
        // For Doppler effect
        private Vector3 _lastSourcePosition;
        private float _lastDistance;
        
        public Spatial3DEffect()
        {
            SourcePosition = Vector3.zero;
            ListenerPosition = Vector3.zero;
        }
        
        /// <summary>
        /// Update positions from Transform components
        /// Call this from Update() in MonoBehaviour
        /// </summary>
        public void UpdateFromTransforms(Transform source, Transform listener)
        {
            if (source != null)
            {
                _lastSourcePosition = SourcePosition;
                SourcePosition = source.position;
            }
            
            if (listener != null)
            {
                ListenerPosition = listener.position;
                ListenerForward = listener.forward;
            }
            else if (Camera.main != null)
            {
                ListenerPosition = Camera.main.transform.position;
                ListenerForward = Camera.main.transform.forward;
            }
        }
        
        public override void Process(float[] data, int channels, int sampleRate)
        {
            if (channels < 2) return; // Need stereo for spatial effect
            
            // Calculate direction and distance
            Vector3 direction = SourcePosition - ListenerPosition;
            float distance = direction.magnitude;
            
            // Calculate attenuation based on distance
            float attenuation = CalculateAttenuation(distance);
            
            // Calculate panning based on direction
            Vector3 localDirection = Quaternion.Inverse(Quaternion.LookRotation(ListenerForward)) * direction.normalized;
            float pan = Mathf.Clamp(localDirection.x, -1f, 1f);
            
            // Calculate left/right gains
            float spreadRad = Spread * Mathf.Deg2Rad / 360f;
            float leftGain = Mathf.Sqrt(0.5f * (1f - pan * (1f - spreadRad))) * attenuation;
            float rightGain = Mathf.Sqrt(0.5f * (1f + pan * (1f - spreadRad))) * attenuation;
            
            // Apply Doppler effect if enabled
            float dopplerShift = 1f;
            if (DopplerLevel > 0)
            {
                float velocity = (distance - _lastDistance) * sampleRate / (data.Length / channels);
                dopplerShift = Mathf.Clamp(1f - (velocity / 343f) * DopplerLevel, 0.5f, 2f);
                _lastDistance = distance;
            }
            
            // Apply spatial effect
            int samplesPerChannel = data.Length / channels;
            for (int i = 0; i < samplesPerChannel; i++)
            {
                int leftIdx = i * channels;
                int rightIdx = i * channels + 1;
                
                // Get mono mix
                float mono = (data[leftIdx] + data[rightIdx]) * 0.5f;
                
                // Apply panning and attenuation
                data[leftIdx] = mono * leftGain;
                data[rightIdx] = mono * rightGain;
            }
        }
        
        private float CalculateAttenuation(float distance)
        {
            if (distance <= MinDistance) return 1f;
            if (distance >= MaxDistance) return 0f;
            
            return RolloffMode switch
            {
                AudioRolloffMode.Linear => 1f - (distance - MinDistance) / (MaxDistance - MinDistance),
                AudioRolloffMode.Logarithmic => MinDistance / (MinDistance + (distance - MinDistance)),
                _ => 1f
            };
        }
        
        public override void Reset()
        {
            _lastSourcePosition = SourcePosition;
            _lastDistance = 0f;
        }
    }
}
