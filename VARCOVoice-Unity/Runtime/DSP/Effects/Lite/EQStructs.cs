using System;
using Unity.Collections;
using UnityEngine;

namespace VARCOVoice.DSP
{
    /// <summary>
    /// Type of EQ filter
    /// </summary>
    [Serializable]
    public enum EQFilterType
    {
        Peak = 0,
        LowShelf = 1,
        HighShelf = 2,
        LowPass = 3,
        HighPass = 4,
        BandPass = 5,
        Notch = 6
    }

    /// <summary>
    /// Parameters for a single EQ band (Pure Data)
    /// </summary>
    [Serializable]
    public struct EQBandParams
    {
        public bool Enabled;
        public EQFilterType Type;
        [Range(20f, 20000f)] public float Frequency;
        [Range(-30f, 30f)] public float Gain; // dB
        [Range(0.1f, 30f)] public float Q;

        // Default constructor
        public static EQBandParams Default()
        {
            return new EQBandParams
            {
                Enabled = true,
                Type = EQFilterType.Peak,
                Frequency = 1000f,
                Gain = 0f,
                Q = 1.0f
            };
        }
    }

    /// <summary>
    /// Pre-calculated Biquad coefficients (Derived Data)
    /// </summary>
    public struct EQBandCoeffs
    {
        // Standard Biquad Coeffs (Used for Visualization & Legacy)
        public float b0, b1, b2;
        public float a1, a2; 

        // SVF Parameters (Used for Processing)
        public float g;   // tan(w0/2)
        public float k;   // 1/Q or damping
        public float m0;  // mix high-pass
        public float m1;  // mix band-pass
        public float m2;  // mix low-pass
        public float m3;  // mix input (if needed)
    }

    /// <summary>
    /// State for a single EQ band (Allocated Memory)
    /// </summary>
    public struct EQBandState
    {
        // History for Direct Form II Transposed
        // x1, x2, y1, y2 for stereo (or simply 2x history if processing stereo interleaved)
        // If we process mono per channel, we need 2 history vars.
        // For simple stereo implementations often researchers use 4 vars per band (L_x1, L_x2, R_x1, R_x2)
        // or just maintain 2 states.
        // Let's keep it simple: State holds history for ONE channel.
        // The JobWrapper handles multiple channels by having multiple States or interleaving.
        
        public float x1, x2;
        public float y1, y2;

        public void Reset()
        {
            x1 = x2 = y1 = y2 = 0f;
        }
    }
    
    /// <summary>
    /// Parameter wrapper for 16-band EQ (Serializable for Unity)
    /// </summary>
    [Serializable]
    public struct ParametricEQParams
    {
        public float OutputGain; // dB
        // We can't easily seralize NativeArray, so we use a fixed array or wrapper in the MonoBehaviour/ScriptableObject.
        // But for the "Mega Struct" passed to the job, we can use a BlobAsset or NativeArray.
        // For this definition, we'll assume the Job takes NativeArray<EQBandParams>.
    }

    /// <summary>
    /// Memory container for 16-band EQ (The 'Heavy' part)
    /// </summary>
    public struct ParametricEQState : IDisposable
    {
        // Flattened arrays for efficient Burst access.
        // Indexing: [channel * TotalBands + bandIndex]
        public NativeArray<EQBandState> BandStates; // History
        public NativeArray<EQBandCoeffs> BandCoeffs; // Cached Coefficients
        
        // We track dirty state externally or just recalculate when params change.
        
        public bool IsCreated => BandStates.IsCreated;

        public void Allocate(int channels, int bands)
        {
            BandStates = new NativeArray<EQBandState>(channels * bands, Allocator.Persistent);
            BandCoeffs = new NativeArray<EQBandCoeffs>(bands, Allocator.Persistent); // Coeffs are shared across channels usually!
        }

        public void Dispose()
        {
            if (BandStates.IsCreated) BandStates.Dispose();
            if (BandCoeffs.IsCreated) BandCoeffs.Dispose();
        }

        public void Reset()
        {
            // Zero out history
            for (int i = 0; i < BandStates.Length; i++)
            {
                var s = BandStates[i];
                s.Reset();
                BandStates[i] = s;
            }
        }
    }
}
