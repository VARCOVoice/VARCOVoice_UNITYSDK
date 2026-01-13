using System;
using UnityEngine;

namespace VARCOVoice.DSP
{
    /// <summary>
    /// 3D Spatial audio effect
    /// Provides stereo panning, width control, and distance attenuation
    /// </summary>
    [Serializable]
    public class Spatial3DEffect : DSPEffectBase
    {
        public override string Name => "3D Spatial";
        
        // ===== PRIMARY CONTROLS =====
        
        /// <summary>
        /// Stereo pan position (-1 = full left, 0 = center, 1 = full right)
        /// </summary>
        public float Pan { get; set; } = 0f;
        
        /// <summary>
        /// Stereo width (0 = mono, 1 = normal stereo, 2 = enhanced width)
        /// </summary>
        public float Width { get; set; } = 1f;
        
        /// <summary>
        /// Stereo spread angle (0-360 degrees)
        /// Higher = wider stereo image
        /// </summary>
        public float Spread { get; set; } = 180f;
        
        /// <summary>
        /// Simulated distance (0-1)
        /// 0 = closest (full volume), 1 = furthest (reduced volume)
        /// Uses smooth attenuation curve
        /// </summary>
        public float Distance { get; set; } = 0f;
        
        /// <summary>
        /// Distance attenuation amount (0 = no attenuation, 1 = full attenuation)
        /// Controls how much Distance affects volume
        /// </summary>
        public float DistanceAttenuation { get; set; } = 0.5f;
        
        // ===== LEGACY PROPERTIES (for runtime) =====
        public float MaxDistance { get; set; } = 50f;
        public float MinDistance { get; set; } = 1f;
        public AudioRolloffMode RolloffMode { get; set; } = AudioRolloffMode.Logarithmic;
        public float DopplerLevel { get; set; } = 0f;
        public Vector3 SourcePosition { get; set; }
        public Vector3 ListenerPosition { get; set; }
        public Vector3 ListenerForward { get; set; } = Vector3.forward;
        public bool UsePositionBased { get; set; } = false;
        
        public Spatial3DEffect()
        {
            SourcePosition = Vector3.zero;
            ListenerPosition = Vector3.zero;
        }
        
        public void UpdateFromTransforms(Transform source, Transform listener)
        {
            UsePositionBased = true;
            if (source != null) SourcePosition = source.position;
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
            if (channels < 2) return;
            
            // ===== CALCULATE PARAMETERS =====
            float pan = Mathf.Clamp(Pan, -1f, 1f);
            float width = Mathf.Clamp(Width, 0f, 2f);
            float spread = Mathf.Clamp01(Spread / 360f);
            float distance = Mathf.Clamp01(Distance);
            
            // ===== DISTANCE ATTENUATION =====
            // Smooth curve: at Distance=0 -> gain=1, at Distance=1 -> gain = (1-DistanceAttenuation)
            // Using square root for more gradual falloff
            float distanceGain = 1f - (Mathf.Sqrt(distance) * DistanceAttenuation);
            distanceGain = Mathf.Clamp(distanceGain, 0.1f, 1f); // Never go completely silent
            
            // ===== PANNING (Constant Power Law) =====
            // Full stereo panning: pan=-1 -> left only, pan=+1 -> right only
            float panAngle = (pan + 1f) * 0.5f * Mathf.PI * 0.5f; // 0 to PI/2
            float leftPanGain = Mathf.Cos(panAngle);
            float rightPanGain = Mathf.Sin(panAngle);
            
            // Apply distance attenuation to pan gains
            leftPanGain *= distanceGain;
            rightPanGain *= distanceGain;
            
            // ===== PROCESS SAMPLES =====
            int samplesPerChannel = data.Length / channels;
            
            for (int i = 0; i < samplesPerChannel; i++)
            {
                int leftIdx = i * channels;
                int rightIdx = i * channels + 1;
                
                float left = data[leftIdx];
                float right = data[rightIdx];
                
                // Mid/Side processing for Width
                float mid = (left + right) * 0.5f;
                float side = (left - right) * 0.5f;
                
                // Apply width: 0=mono, 1=normal, 2=wide
                side *= width;
                
                // Apply spread: converts stereo towards mono
                // At spread=0 (0°), full mono
                // At spread=1 (360°), full stereo
                float monoMix = 1f - spread;
                left = Mathf.Lerp(mid, mid + side, 1f - monoMix * 0.5f);
                right = Mathf.Lerp(mid, mid - side, 1f - monoMix * 0.5f);
                
                // Apply panning
                data[leftIdx] = left * leftPanGain * 1.414f;  // Compensate for pan law
                data[rightIdx] = right * rightPanGain * 1.414f;
            }
        }

        public override void ApplyPreset(string presetName)
        {
            if (string.IsNullOrEmpty(presetName)) return;

            switch (presetName.Trim().ToLowerInvariant())
            {
                case "center":
                    Pan = 0f;
                    Width = 1f;
                    Spread = 180f;
                    Distance = 0f;
                    DistanceAttenuation = 0.5f;
                    Mix = 1f;
                    break;
                case "left":
                    Pan = -1f;
                    Width = 1f;
                    Spread = 120f;
                    Distance = 0f;
                    DistanceAttenuation = 0.5f;
                    Mix = 1f;
                    break;
                case "right":
                    Pan = 1f;
                    Width = 1f;
                    Spread = 120f;
                    Distance = 0f;
                    DistanceAttenuation = 0.5f;
                    Mix = 1f;
                    break;
                case "wide":
                    Pan = 0f;
                    Width = 1.8f;
                    Spread = 360f;
                    Distance = 0f;
                    DistanceAttenuation = 0.3f;
                    Mix = 1f;
                    break;
                case "narrow":
                    Pan = 0f;
                    Width = 0.2f;
                    Spread = 30f;
                    Distance = 0f;
                    DistanceAttenuation = 0.5f;
                    Mix = 1f;
                    break;
                case "far":
                    Pan = 0f;
                    Width = 0.6f;
                    Spread = 90f;
                    Distance = 0.8f;
                    DistanceAttenuation = 0.6f;
                    Mix = 1f;
                    break;
                case "near":
                    Pan = 0f;
                    Width = 1.3f;
                    Spread = 270f;
                    Distance = 0f;
                    DistanceAttenuation = 0.2f;
                    Mix = 1f;
                    break;
            }
        }

        public override void Reset()
        {
            Pan = 0f;
            Width = 1f;
            Spread = 180f;
            Distance = 0f;
            DistanceAttenuation = 0.5f;
        }
    }
}
