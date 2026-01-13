using System;
using UnityEngine;

namespace VARCOVoice.DSP
{
    public static class DSPUtils
    {
        /// <summary>
        /// Perform FFT (Cooley-Tukey) in-place.
        /// Length of arrays must be a power of 2.
        /// </summary>
        /// <param name="real">Real parts (input/output)</param>
        /// <param name="imag">Imaginary parts (input/output)</param>
        public static void FFT(float[] real, float[] imag)
        {
            int n = real.Length;
            int m = (int)Mathf.Log(n, 2);

            // Bit Reversal
            int j = 0;
            for (int i = 0; i < n - 1; i++)
            {
                if (i < j)
                {
                    float tr = real[j];
                    float ti = imag[j];
                    real[j] = real[i];
                    imag[j] = imag[i];
                    real[i] = tr;
                    imag[i] = ti;
                }
                int k = n / 2;
                while (k <= j)
                {
                    j -= k;
                    k /= 2;
                }
                j += k;
            }

            // Butterfly Ops
            float c1 = -1.0f;
            float c2 = 0.0f;
            int l2 = 1;

            for (int l = 0; l < m; l++)
            {
                int l1 = l2;
                l2 <<= 1;
                float u1 = 1.0f;
                float u2 = 0.0f;

                for (int j2 = 0; j2 < l1; j2++)
                {
                    for (int i = j2; i < n; i += l2)
                    {
                        int ip = i + l1;
                        float tr = u1 * real[ip] - u2 * imag[ip];
                        float ti = u1 * imag[ip] + u2 * real[ip];
                        real[ip] = real[i] - tr;
                        imag[ip] = imag[i] - ti;
                        real[i] += tr;
                        imag[i] += ti;
                    }

                    float z = u1 * c1 - u2 * c2;
                    u2 = u1 * c2 + u2 * c1;
                    u1 = z;
                }

                c2 = Mathf.Sqrt((1.0f - c1) / 2.0f);
                c1 = Mathf.Sqrt((1.0f + c1) / 2.0f);
                c2 = -c2;
            }
        }
        
        private static System.Collections.Generic.Dictionary<int, float[]> _windowCache = new System.Collections.Generic.Dictionary<int, float[]>();

        /// <summary>
        /// Apply Window function to array
        /// Uses caching to avoid expensive Trigonometry every frame.
        /// </summary>
        public static void ApplyWindow(float[] data, FFTWindow windowType)
        {
            int n = data.Length;
            // Generate unique key for (Type + Length)
            // Assuming max length < 100000
            int key = (int)windowType * 100000 + n;

            if (!_windowCache.TryGetValue(key, out float[] window))
            {
                // Generate and cache
                window = new float[n];
                for (int i = 0; i < n; i++)
                {
                    float w = 1f;
                    float phi = 2f * Mathf.PI * i / (n - 1);

                    switch (windowType)
                    {
                        case FFTWindow.Hamming:
                            w = 0.54f - 0.46f * Mathf.Cos(phi);
                            break;
                        case FFTWindow.Hanning:
                            w = 0.5f * (1f - Mathf.Cos(phi));
                            break;
                        case FFTWindow.Blackman:
                            w = 0.42f - 0.5f * Mathf.Cos(phi) + 0.08f * Mathf.Cos(2f * phi);
                            break;
                        case FFTWindow.BlackmanHarris:
                            w = 0.35875f - 0.48829f * Mathf.Cos(phi) + 0.14128f * Mathf.Cos(2f * phi) - 0.01168f * Mathf.Cos(3f * phi);
                            break;
                    }
                    window[i] = w;
                }
                _windowCache[key] = window;
            }

            // Apply cached window (Vectorizable by JIT)
            for (int i = 0; i < n; i++)
            {
                data[i] *= window[i];
            }
        }
    }
}
