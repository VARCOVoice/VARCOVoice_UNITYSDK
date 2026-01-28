using System.Runtime.CompilerServices;

namespace VARCOVoice.DSP
{
    /// <summary>
    /// Linear parameter interpolation with target tracking to prevent residual errors.
    /// Used for smooth parameter changes without zipper noise.
    /// </summary>
    internal struct ParamRamp
    {
        /// <summary>
        /// Current interpolated value
        /// </summary>
        public float Current;
        
        /// <summary>
        /// Target value we're ramping towards
        /// </summary>
        public float Target;
        
        private float _step;
        private int _remaining;

        /// <summary>
        /// True if currently ramping to target
        /// </summary>
        public bool IsActive => _remaining > 0;

        /// <summary>
        /// Reset to a specific value immediately (no ramping)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset(float value)
        {
            Current = value;
            Target = value;
            _step = 0f;
            _remaining = 0;
        }

        /// <summary>
        /// Set new target with specified ramp duration in samples
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetTarget(float target, int samples)
        {
            Target = target;
            
            if (samples <= 0)
            {
                Current = target;
                _step = 0f;
                _remaining = 0;
                return;
            }

            _step = (target - Current) / samples;
            _remaining = samples;
        }

        /// <summary>
        /// Get next interpolated value. Snaps to target when ramping completes
        /// to prevent residual accumulation errors.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Next()
        {
            if (_remaining > 0)
            {
                Current += _step;
                _remaining--;
                
                // Snap to target when done to prevent floating point accumulation errors
                if (_remaining == 0)
                {
                    Current = Target;
                }
            }
            return Current;
        }
    }
}
