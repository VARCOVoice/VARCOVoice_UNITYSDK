using System.Runtime.CompilerServices;
using Unity.Burst;
using UnityEngine;

namespace VARCOVoice.DSP
{
    /// <summary>
    /// Pure Logic for EQ processing (No state, pure math)
    /// </summary>
    [BurstCompile]
    public static class EQLogic
    {
        private const float QMin = 0.1f;
        private const float QMax = 30f;
        private const float QBandwidthCurve = 1.2f; // Pro-Q style bandwidth easing
        private const float Ln2 = 0.69314718f;
        private static readonly float BwMax = QToBandwidthOctaves(QMin);
        private static readonly float BwMin = QToBandwidthOctaves(QMax);

        /// <summary>
        /// Calculate Biquad coefficients based on Params and SampleRate
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void UpdateCoefficients(in EQBandParams paramsSrc, int sampleRate, out EQBandCoeffs coeffs)
        {
            // Defaults (Pass-through)
            coeffs.b0 = 1f; coeffs.b1 = 0f; coeffs.b2 = 0f;
            coeffs.a1 = 0f; coeffs.a2 = 0f;
            
            // SVF Defaults (Passthrough)
            coeffs.g = 0f; coeffs.k = 1f; 
            coeffs.m0 = 0f; coeffs.m1 = 0f; coeffs.m2 = 0f; coeffs.m3 = 1f;

            if (!paramsSrc.Enabled) return;

            EQFilterType type = paramsSrc.Type;
            float frequency = paramsSrc.Frequency;
            float gainDb = paramsSrc.Gain;
            float q = MapQ(paramsSrc.Q, type);

            // Nyquist limit check
            if (frequency > sampleRate * 0.49f) frequency = sampleRate * 0.49f;
            if (frequency < 20f) frequency = 20f; // Lower bound

            float w0 = 2f * Mathf.PI * frequency / sampleRate;
            float cosW0 = Mathf.Cos(w0);
            float sinW0 = Mathf.Sin(w0);
            float qSafe = Mathf.Max(q, 0.01f);
            float alpha = sinW0 / (2f * qSafe);
            float A = Mathf.Pow(10f, gainDb / 40f); // A = 10^(dB/40) => A^2 = 10^(dB/20)

            float b0 = 1f, b1 = 0f, b2 = 0f, a0 = 1f, a1 = 0f, a2 = 0f;

            switch (type)
            {
                case EQFilterType.Peak:
                    b0 = 1f + alpha * A;
                    b1 = -2f * cosW0;
                    b2 = 1f - alpha * A;
                    a0 = 1f + alpha / A;
                    a1 = -2f * cosW0;
                    a2 = 1f - alpha / A;
                    break;

                case EQFilterType.LowShelf:
                    float sqrtA = Mathf.Sqrt(A);
                    b0 = A * ((A + 1f) - (A - 1f) * cosW0 + 2f * sqrtA * alpha);
                    b1 = 2f * A * ((A - 1f) - (A + 1f) * cosW0);
                    b2 = A * ((A + 1f) - (A - 1f) * cosW0 - 2f * sqrtA * alpha);
                    a0 = (A + 1f) + (A - 1f) * cosW0 + 2f * sqrtA * alpha;
                    a1 = -2f * ((A - 1f) + (A + 1f) * cosW0);
                    a2 = (A + 1f) + (A - 1f) * cosW0 - 2f * sqrtA * alpha;
                    break;

                case EQFilterType.HighShelf:
                    sqrtA = Mathf.Sqrt(A);
                    b0 = A * ((A + 1f) + (A - 1f) * cosW0 + 2f * sqrtA * alpha);
                    b1 = -2f * A * ((A - 1f) + (A + 1f) * cosW0);
                    b2 = A * ((A + 1f) + (A - 1f) * cosW0 - 2f * sqrtA * alpha);
                    a0 = (A + 1f) - (A - 1f) * cosW0 + 2f * sqrtA * alpha;
                    a1 = 2f * ((A - 1f) - (A + 1f) * cosW0);
                    a2 = (A + 1f) - (A - 1f) * cosW0 - 2f * sqrtA * alpha;
                    break;

                case EQFilterType.LowPass:
                    b0 = (1f - cosW0) / 2f;
                    b1 = 1f - cosW0;
                    b2 = (1f - cosW0) / 2f;
                    a0 = 1f + alpha;
                    a1 = -2f * cosW0;
                    a2 = 1f - alpha;
                    break;

                case EQFilterType.HighPass:
                    b0 = (1f + cosW0) / 2f;
                    b1 = -(1f + cosW0);
                    b2 = (1f + cosW0) / 2f;
                    a0 = 1f + alpha;
                    a1 = -2f * cosW0;
                    a2 = 1f - alpha;
                    break;
                    
                 case EQFilterType.BandPass:
                    b0 = alpha;
                    b1 = 0f;
                    b2 = -alpha;
                    a0 = 1f + alpha;
                    a1 = -2f * cosW0;
                    a2 = 1f - alpha;
                    break;
                    
                case EQFilterType.Notch:
                    b0 = 1f;
                    b1 = -2f * cosW0;
                    b2 = 1f;
                    a0 = 1f + alpha;
                    a1 = -2f * cosW0;
                    a2 = 1f - alpha;
                    break;
            }

            // Normalize Biquad (Keep for Visualization)
            float invA0 = 1f / a0;
            coeffs.b0 = b0 * invA0;
            coeffs.b1 = b1 * invA0;
            coeffs.b2 = b2 * invA0;
            coeffs.a1 = a1 * invA0;
            coeffs.a2 = a2 * invA0;

            // --- TPT SVF Coefficients (Legacy/Visualization) ---
            // g = tan(pi * f / fs)
            // k = 1 / Q
            // Mix: m0*hp + m1*bp + m2*lp + m3*in

            // Pre-warp frequency
            float wd = Mathf.PI * frequency / sampleRate;
            float g = Mathf.Tan(wd);

            // Limit g to prevent instability at Nyquist
            // (Already constrained frequency < 0.49fs above)
            
            float k = 1f / qSafe;
            float linearGain = Mathf.Pow(10f, gainDb / 20f); 

            coeffs.g = g;
            coeffs.k = k;
            coeffs.m3 = 0f; // Input mix not used by default

            switch (type)
            {
                case EQFilterType.Peak:
                    // Peak: H(s) = (s^2 + (A/Q)s + 1) / (s^2 + (1/Q)s + 1)
                    // Mix: HP + LP + m1 * BP (m1 solved to hit target gain at center)
                    coeffs.m0 = 1f;
                    coeffs.m2 = 1f;
                    coeffs.m1 = SolvePeakMix(linearGain, g, k, cosW0, sinW0);
                    break;

                case EQFilterType.LowShelf:
                    // LowShelf: Boost Low
                    // Mix: HP + sqrt(A)*k*BP + A*LP
                    coeffs.m0 = 1f;
                    coeffs.m2 = linearGain;
                    coeffs.m1 = Mathf.Sqrt(linearGain) * k;
                    break;

                case EQFilterType.HighShelf:
                    // HighShelf: Boost High
                    // Mix: A*HP + sqrt(A)*k*BP + LP
                    coeffs.m0 = linearGain;
                    coeffs.m2 = 1f;
                    coeffs.m1 = Mathf.Sqrt(linearGain) * k;
                    break;

                case EQFilterType.LowPass:
                    // 12dB/oct LowPass
                    coeffs.m0 = 0f;
                    coeffs.m1 = 0f;
                    coeffs.m2 = 1f;
                    break;

                case EQFilterType.HighPass:
                    // 12dB/oct HighPass
                    coeffs.m0 = 1f;
                    coeffs.m1 = 0f;
                    coeffs.m2 = 0f;
                    break;

                case EQFilterType.BandPass:
                    // Constant Peak Gain BandPass
                    coeffs.m0 = 0f;
                    coeffs.m1 = 1f; // Unity gain at center? Depends on Q. 
                    // TPT BP peak is 1/k. If we want 0dB peak, m1 = k.
                    // Standard BP usually implies 0dB peak gain.
                    coeffs.m1 = k; 
                    coeffs.m2 = 0f;
                    break;

                case EQFilterType.Notch:
                    coeffs.m0 = 1f;
                    coeffs.m1 = 0f;
                    coeffs.m2 = 1f;
                    break;
                
                default:
                    coeffs.m0 = 1f; coeffs.m1 = 0f; coeffs.m2 = 0f;
                    // Default to bypass: Input
                    coeffs.m3 = 1f; coeffs.m0 = 0f; coeffs.m1 = 0f; coeffs.m2 = 0f;
                    break;
            }
        } // Closing UpdateCoefficients

        private static float MapQ(float q, EQFilterType type)
        {
            if (type != EQFilterType.Peak && type != EQFilterType.BandPass && type != EQFilterType.Notch)
                return Mathf.Max(q, 0.01f);

            q = Mathf.Clamp(q, QMin, QMax);
            float bw = QToBandwidthOctaves(q);
            float t = (BwMax - bw) / (BwMax - BwMin);
            t = Mathf.Clamp01(t);
            t = Mathf.Pow(t, QBandwidthCurve);
            float bwMapped = Mathf.Lerp(BwMax, BwMin, t);
            return BandwidthToQ(bwMapped);
        }

        private static float QToBandwidthOctaves(float q)
        {
            float qSafe = Mathf.Max(q, 1e-4f);
            float s = (1f + Mathf.Sqrt(1f + 4f * qSafe * qSafe)) / (2f * qSafe);
            return 2f * (Mathf.Log(s) / Ln2);
        }

        private static float BandwidthToQ(float bw)
        {
            float x = 0.5f * Ln2 * bw;
            float sinh = 0.5f * (Mathf.Exp(x) - Mathf.Exp(-x));
            return 1f / (2f * Mathf.Max(sinh, 1e-6f));
        }

        private static float SolvePeakMix(float targetGain, float g, float k, float cosW0, float sinW0)
        {
            if (targetGain <= 0f) return 0f;

            ComputeSvfResponse(g, k, cosW0, sinW0, out var v1, out var v2, out var v3);
            ComplexF n = v1 + v3; // Notch (HP + LP)
            ComplexF b = v2;      // Bandpass

            float br = b.Re;
            float bi = b.Im;
            float nr = n.Re;
            float ni = n.Im;

            float a = br * br + bi * bi;
            if (a < 1e-8f) return targetGain * k;

            float bcoef = 2f * (nr * br + ni * bi);
            float c = (nr * nr + ni * ni) - targetGain * targetGain;
            float disc = bcoef * bcoef - 4f * a * c;
            if (disc <= 0f) return targetGain * k;

            float sqrt = Mathf.Sqrt(disc);
            float inv2a = 0.5f / a;
            float m1a = (-bcoef + sqrt) * inv2a;
            float m1b = (-bcoef - sqrt) * inv2a;

            float errA = Mathf.Abs((n + ComplexF.Mul(b, new ComplexF(m1a, 0f))).Abs() - targetGain);
            float errB = Mathf.Abs((n + ComplexF.Mul(b, new ComplexF(m1b, 0f))).Abs() - targetGain);
            return errA <= errB ? m1a : m1b;
        }

        private static void ComputeSvfResponse(float g, float k, float cosW0, float sinW0,
            out ComplexF v1, out ComplexF v2, out ComplexF v3)
        {
            float gSafe = Mathf.Max(g, 1e-6f);
            ComplexF z = new ComplexF(cosW0, sinW0);
            ComplexF denomZ = new ComplexF(z.Re + 1f, z.Im);
            ComplexF r = ComplexF.Div(new ComplexF(2f, 0f), denomZ);
            ComplexF one = new ComplexF(1f, 0f);
            ComplexF oneMinusR = one - r;

            float invDenom = 1f / (1f + gSafe * (gSafe + k));
            ComplexF term1 = ComplexF.Div(oneMinusR, new ComplexF(gSafe, 0f));
            ComplexF term2 = ComplexF.Mul(r, new ComplexF(k, 0f));
            ComplexF term3 = ComplexF.Div(ComplexF.Mul(r, new ComplexF(gSafe, 0f)), oneMinusR);
            ComplexF denom = term1 + ComplexF.Mul(new ComplexF(invDenom, 0f), term2 + term3);

            v2 = ComplexF.Div(new ComplexF(invDenom, 0f), denom);
            v1 = ComplexF.Mul(term1, v2);
            v3 = ComplexF.Mul(new ComplexF(gSafe, 0f), ComplexF.Div(v2, oneMinusR));
        }

        /// <summary>
        /// Calculate SVF magnitude response at a target frequency.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetSvfMagnitude(in EQBandCoeffs coeffs, float frequency, int sampleRate)
        {
            if (sampleRate <= 0 || frequency <= 0f) return 0f;

            float w = 2f * Mathf.PI * frequency / sampleRate;
            ComplexF z = new ComplexF(Mathf.Cos(w), Mathf.Sin(w));
            ComplexF denomZ = new ComplexF(z.Re + 1f, z.Im);
            ComplexF r = ComplexF.Div(new ComplexF(2f, 0f), denomZ);
            ComplexF one = new ComplexF(1f, 0f);
            ComplexF oneMinusR = one - r;

            float g = Mathf.Max(coeffs.g, 1e-6f);
            float k = coeffs.k;
            float invDenom = 1f / (1f + g * (g + k));

            ComplexF term1 = ComplexF.Div(oneMinusR, new ComplexF(g, 0f)); // (1-r)/g
            ComplexF term2 = ComplexF.Mul(r, new ComplexF(k, 0f)); // k*r
            ComplexF term3 = ComplexF.Div(ComplexF.Mul(r, new ComplexF(g, 0f)), oneMinusR); // r*g/(1-r)

            ComplexF denom = term1 + ComplexF.Mul(new ComplexF(invDenom, 0f), term2 + term3);
            ComplexF v2 = ComplexF.Div(new ComplexF(invDenom, 0f), denom);
            ComplexF v1 = ComplexF.Mul(term1, v2);
            ComplexF v3 = ComplexF.Mul(new ComplexF(g, 0f), ComplexF.Div(v2, oneMinusR));

            ComplexF y = ComplexF.Mul(v1, new ComplexF(coeffs.m0, 0f))
                        + ComplexF.Mul(v2, new ComplexF(coeffs.m1, 0f))
                        + ComplexF.Mul(v3, new ComplexF(coeffs.m2, 0f))
                        + new ComplexF(coeffs.m3, 0f);

            return y.Abs();
        }

        /// <summary>
        /// Calculate biquad magnitude response at a target frequency.
        /// Matches the ParametricEQ16 audio path.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetBiquadMagnitude(in EQBandCoeffs coeffs, float frequency, int sampleRate)
        {
            if (sampleRate <= 0 || frequency <= 0f) return 0f;

            float w = 2f * Mathf.PI * frequency / sampleRate;
            float cosW = Mathf.Cos(w);
            float sinW = Mathf.Sin(w);
            float cos2W = (cosW * cosW) - (sinW * sinW);
            float sin2W = 2f * sinW * cosW;

            float numRe = coeffs.b0 + coeffs.b1 * cosW + coeffs.b2 * cos2W;
            float numIm = -coeffs.b1 * sinW - coeffs.b2 * sin2W;
            float denRe = 1f + coeffs.a1 * cosW + coeffs.a2 * cos2W;
            float denIm = -coeffs.a1 * sinW - coeffs.a2 * sin2W;

            float denMag = denRe * denRe + denIm * denIm;
            if (denMag < 1e-12f) denMag = 1e-12f;
            float numMag = numRe * numRe + numIm * numIm;
            return Mathf.Sqrt(numMag / denMag);
        }

        /// <summary>
        /// Process a single sample through a single Biquad band (Legacy)       
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ProcessBand(ref EQBandState state, in EQBandCoeffs coeffs, float input)
        {
            // Direct Form II Transposed
            float output = coeffs.b0 * input + state.x1;
            
            state.x1 = state.x2 + coeffs.b1 * input - coeffs.a1 * output;
            state.x2 = coeffs.b2 * input - coeffs.a2 * output;

            return output;
        }

        private struct ComplexF
        {
            public readonly float Re;
            public readonly float Im;

            public ComplexF(float re, float im)
            {
                Re = re;
                Im = im;
            }

            public float Abs()
            {
                return Mathf.Sqrt(Re * Re + Im * Im);
            }

            public static ComplexF operator +(ComplexF a, ComplexF b)
            {
                return new ComplexF(a.Re + b.Re, a.Im + b.Im);
            }

            public static ComplexF operator -(ComplexF a, ComplexF b)
            {
                return new ComplexF(a.Re - b.Re, a.Im - b.Im);
            }

            public static ComplexF Mul(ComplexF a, ComplexF b)
            {
                return new ComplexF(a.Re * b.Re - a.Im * b.Im, a.Re * b.Im + a.Im * b.Re);
            }

            public static ComplexF Div(ComplexF a, ComplexF b)
            {
                float denom = b.Re * b.Re + b.Im * b.Im;
                if (denom < 1e-12f) denom = 1e-12f;
                return new ComplexF(
                    (a.Re * b.Re + a.Im * b.Im) / denom,
                    (a.Im * b.Re - a.Re * b.Im) / denom
                );
            }
        }
    }
}
