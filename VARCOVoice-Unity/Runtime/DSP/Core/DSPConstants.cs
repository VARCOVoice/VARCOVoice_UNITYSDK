using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace VARCOVoice.DSP
{
    /// <summary>
    /// DSP constants for performance and stability
    /// </summary>
    internal static class DSPConstants
    {
        /// <summary>
        /// Threshold below which values are considered denormal and should be flushed to zero.
        /// Denormals cause massive CPU spikes due to slow FPU handling.
        /// </summary>
        public const float DENORMAL_THRESHOLD = 1e-15f;
        
        /// <summary>
        /// Tiny DC offset to inject into feedback loops to prevent denormals.
        /// Small enough to be inaudible but large enough to prevent denormal accumulation.
        /// </summary>
        public const float DC_OFFSET = 1e-25f;
        
        /// <summary>
        /// Check if value is denormal and should be flushed
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsDenormal(float value)
        {
            return MathF.Abs(value) < DENORMAL_THRESHOLD && value != 0f;
        }
        
        /// <summary>
        /// Flush denormal values to zero
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float FlushDenormal(float value)
        {
            return MathF.Abs(value) < DENORMAL_THRESHOLD ? 0f : value;
        }
        
        /// <summary>
        /// Sanitize a value: flush NaN, Inf, and denormals
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Sanitize(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                return 0f;
            if (MathF.Abs(value) < DENORMAL_THRESHOLD)
                return 0f;
            return value;
        }

        /// <summary>
        /// Soft clip to keep feedback paths stable without hard limiting.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SoftClip(float value)
        {
            if (value > 1f) return 2f / 3f;
            if (value < -1f) return -2f / 3f;
            return value - (value * value * value) / 3f;
        }

        /// <summary>
        /// Hermite (Cubic) Interpolation for smooth delay line reading.
        /// Reduces 'zippering' artifacts compared to linear interpolation.
        /// </summary>
        /// <param name="y0">Result at x = -1</param>
        /// <param name="y1">Result at x = 0</param>
        /// <param name="y2">Result at x = 1</param>
        /// <param name="y3">Result at x = 2</param>
        /// <param name="mu">Fractional position between y1 and y2 (0.0 to 1.0)</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float HermiteInterpolation(float y0, float y1, float y2, float y3, float mu)
        {
            float m0, m1, mu2, mu3;
            float a0, a1, a2, a3;

            mu2 = mu * mu;
            mu3 = mu2 * mu;

            m0 = (y2 - y0) * 0.5f;
            m1 = (y3 - y1) * 0.5f;

            a0 = 2f * mu3 - 3f * mu2 + 1f;
            a1 = mu3 - 2f * mu2 + mu;
            a2 = mu3 - mu2;
            a3 = -2f * mu3 + 3f * mu2;

            return (a0 * y1 + a1 * m0 + a2 * m1 + a3 * y2);
        }
    }
}
