using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace VARCOVoice.DSP
{
    /// <summary>
    /// High-performance quadrature oscillator for sin-free modulation.
    /// Uses phase rotation instead of Mathf.Sin() for ~10-20x performance improvement.
    /// </summary>
    internal struct QuadratureOscillator
    {
        /// <summary>
        /// Current sine value (read this for LFO output)
        /// </summary>
        public float Sin;
        
        /// <summary>
        /// Current cosine value (used internally for phase rotation)
        /// </summary>
        public float Cos;
        
        // Rotation coefficients (calculated once per block when rate changes)
        private float _sinOmega;
        private float _cosOmega;
        private float _omega;
        
        // Counter for periodic normalization to prevent drift
        private int _normCounter;
        private const int NORM_INTERVAL = 256;
        
        /// <summary>
        /// Initialize oscillator at given phase (in radians)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Init(float phase = 0f)
        {
            Sin = Mathf.Sin(phase);
            Cos = Mathf.Cos(phase);
            _normCounter = 0;
        }
        
        /// <summary>
        /// Set the oscillator frequency. Call once per block or when rate changes.
        /// </summary>
        /// <param name="frequencyHz">Oscillator frequency in Hz</param>
        /// <param name="sampleRate">Sample rate in Hz</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetFrequency(float frequencyHz, int sampleRate)
        {
            float omega = 2f * Mathf.PI * frequencyHz / sampleRate;
            if (Math.Abs(omega - _omega) > 1e-9f)
            {
                _omega = omega;
                _sinOmega = Mathf.Sin(omega);
                _cosOmega = Mathf.Cos(omega);
            }
        }
        
        /// <summary>
        /// Advance oscillator by one sample using quadrature rotation.
        /// Much faster than Mathf.Sin() - only 4 multiplies + 2 adds.
        /// </summary>
        /// <returns>Current sine value (same as Sin property)</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Next()
        {
            // Quadrature rotation: [sin', cos'] = [sin*cosω + cos*sinω, cos*cosω - sin*sinω]
            float newSin = Sin * _cosOmega + Cos * _sinOmega;
            float newCos = Cos * _cosOmega - Sin * _sinOmega;
            
            Sin = newSin;
            Cos = newCos;
            
            // Periodic normalization to prevent amplitude drift
            _normCounter++;
            if (_normCounter >= NORM_INTERVAL)
            {
                Normalize();
                _normCounter = 0;
            }
            
            return Sin;
        }
        
        /// <summary>
        /// Normalize to unit circle to prevent drift accumulation
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Normalize()
        {
            float mag = Sin * Sin + Cos * Cos;
            if (mag > 0f && Math.Abs(mag - 1f) > 1e-6f)
            {
                float invMag = 1f / Mathf.Sqrt(mag);
                Sin *= invMag;
                Cos *= invMag;
            }
        }
        
        /// <summary>
        /// Reset oscillator to initial state
        /// </summary>
        public void Reset()
        {
            Sin = 0f;
            Cos = 1f;
            _sinOmega = 0f;
            _cosOmega = 1f;
            _omega = 0f;
            _normCounter = 0;
        }
    }
}
