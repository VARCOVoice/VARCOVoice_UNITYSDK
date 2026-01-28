using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace VARCOVoice.DSP
{
    /// <summary>
    /// Job wrapper that connects Data, State, and Logic
    /// </summary>
    [System.Obsolete("ParametricEQJob is deprecated. Use ParametricEQ16 instead.")]
    [BurstCompile]
    public struct ParametricEQJob : IJob
    {
        // I/O
        public NativeArray<float> Data; // Audio Data
        public int Channels;

        // Data (Params)
        [ReadOnly] public NativeArray<EQBandParams> BandParams;
        [ReadOnly] public float OutputGain;
        [ReadOnly] public int SampleRate;

        // State (Mutable)
        public NativeArray<EQBandState> States; // Length: Channels * Bands
        // We can pass coeffs if pre-calculated, or calculate them in Execute if we want one huge job.
        // For efficiency, best pattern is: 
        // 1. Job to calculate coeffs (Run once per frame if params dirty)
        // 2. Job to process audio (Run every block)
        // But for simplicity in this "Wrapper" example, we'll calculate coeffs locally 
        // or assume they are passed in via State. 
        // Let's use the pattern where we calculate coeffs once at start of block (or per buffer if needed).
        // Since Params are ReadOnly, they are constant for this block.
        // So we can calculate coeffs at the start of Execute().
        // BUT, allocating a temporary array for coeffs inside the job is tricky if we want to avoid GC.
        // Stack alloc is fine for small numbers. 16 bands x 5 floats is small.

        public void Execute()
        {
            // 1. Calculate linear output gain
            float linearGain = Mathf.Pow(10f, OutputGain / 20f);
            
            // 2. Pre-calculate coefficients for all bands
            // We can store them in a local struct array on stack if unsafe, or just re-calc (cheap enough for 16 bands? maybe).
            // Better: Use a fixed buffer or UnsafeList.
            // For now, let's just calculate them. Burst is good at optimizing this loop.
            
            // Optimization: We could have a separate "PreCalc" job, but let's keep it self-contained for now.
            var coeffs = new NativeArray<EQBandCoeffs>(BandParams.Length, Allocator.Temp);
            for (int b = 0; b < BandParams.Length; b++)
            {
                EQLogic.UpdateCoefficients(BandParams[b], SampleRate, out EQBandCoeffs c);
                coeffs[b] = c;
            }

            // 3. Process Samples
            int length = Data.Length;
            int numBands = BandParams.Length;

            for (int i = 0; i < length; i++)
            {
                int channel = i % Channels;
                float sample = Data[i];

                // Process through all bands
                for (int b = 0; b < numBands; b++)
                {
                    // Access state for this specific channel and band
                    // Layout: [Band0_Ch0, Band0_Ch1, Band1_Ch0, Band1_Ch1...] or [Ch0_Band0, Ch0_Band1...]
                    // Let's define layout as: Channel-Major to keep channel memory together? 
                    // Actually Band-Major might be better if we process bands in inner loop?
                    // Let's stick to: Index = channel * numBands + b
                    int stateIndex = channel * numBands + b;
                    
                    // We must copy the struct out to modify it, then put it back
                    // (Unless using unsafe pointers, but NativeArray access copies structs)
                    EQBandState s = States[stateIndex];
                    sample = EQLogic.ProcessBand(ref s, coeffs[b], sample);
                    States[stateIndex] = s;
                }

                Data[i] = sample * linearGain;
            }

            coeffs.Dispose();
        }
    }
}
