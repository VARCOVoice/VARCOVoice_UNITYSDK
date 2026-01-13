using System;
using System.Collections.Generic;
using UnityEngine;

namespace VARCOVoice.LipSync
{
    /// <summary>
    /// Analyzes audio to generate lip sync data
    /// </summary>
    public class LipSyncAnalyzer
    {
        /// <summary>
        /// Analyze audio clip and generate lip sync data
        /// </summary>
        public LipSyncData Analyze(AudioClip clip, float sampleRate = 30f)
        {
            if (clip == null) return null;
            
            var data = new LipSyncData
            {
                ClipName = clip.name,
                Duration = clip.length,
                EnergySampleRate = sampleRate
            };
            
            // Get audio samples
            float[] samples = new float[clip.samples * clip.channels];
            clip.GetData(samples, 0);
            
            // Convert to mono if stereo
            float[] monoSamples = ToMono(samples, clip.channels);
            
            // Analyze energy levels
            AnalyzeEnergy(monoSamples, clip.frequency, data, sampleRate);
            
            // Generate viseme keyframes based on energy
            GenerateVisemeKeyframes(data);
            
            return data;
        }
        
        /// <summary>
        /// Real-time viseme analysis from audio samples
        /// </summary>
        public VisemeType AnalyzeRealtime(float[] samples, int channels)
        {
            float[] mono = ToMono(samples, channels);
            
            // Calculate RMS energy
            float energy = CalculateRMS(mono);
            
            // Calculate spectral characteristics
            float[] spectrum = CalculateSpectrum(mono);
            float lowEnergy = GetBandEnergy(spectrum, 0, 8);      // 0-500 Hz
            float midEnergy = GetBandEnergy(spectrum, 8, 32);     // 500-2000 Hz
            float highEnergy = GetBandEnergy(spectrum, 32, 64);   // 2000-4000 Hz
            
            // Determine viseme based on spectral content
            if (energy < 0.01f)
            {
                return VisemeType.Silence;
            }
            
            // Simple classification based on frequency bands
            if (lowEnergy > midEnergy && lowEnergy > highEnergy)
            {
                // Low frequency dominant - probably M, B, P sounds
                if (energy > 0.2f) return VisemeType.PP;
                return VisemeType.OO;
            }
            else if (highEnergy > midEnergy)
            {
                // High frequency dominant - S, CH sounds
                if (highEnergy > 0.3f) return VisemeType.SS;
                return VisemeType.CH;
            }
            else
            {
                // Mid frequency dominant - vowels
                float ratio = lowEnergy / (midEnergy + 0.001f);
                
                if (ratio > 0.8f) return VisemeType.OO;
                if (ratio > 0.5f) return VisemeType.OH;
                if (ratio > 0.3f) return VisemeType.AA;
                return VisemeType.EE;
            }
        }
        
        /// <summary>
        /// Get current viseme weight based on real-time analysis
        /// Returns weights for all viseme types
        /// </summary>
        public float[] AnalyzeRealtimeWeights(float[] samples, int channels)
        {
            float[] weights = new float[15];
            float[] mono = ToMono(samples, channels);
            
            float energy = CalculateRMS(mono);
            
            if (energy < 0.005f)
            {
                weights[(int)VisemeType.Silence] = 1f;
                return weights;
            }
            
            // Calculate spectrum
            float[] spectrum = CalculateSpectrum(mono);
            
            // Map spectral bands to viseme weights
            float veryLow = GetBandEnergy(spectrum, 0, 4);    // Vowel formants
            float low = GetBandEnergy(spectrum, 4, 12);
            float mid = GetBandEnergy(spectrum, 12, 32);
            float high = GetBandEnergy(spectrum, 32, 64);
            float veryHigh = GetBandEnergy(spectrum, 64, 128);
            
            // Normalize
            float total = veryLow + low + mid + high + veryHigh + 0.001f;
            veryLow /= total;
            low /= total;
            mid /= total;
            high /= total;
            veryHigh /= total;
            
            // Map to visemes
            weights[(int)VisemeType.OO] = veryLow * energy * 3f;
            weights[(int)VisemeType.OH] = low * energy * 3f;
            weights[(int)VisemeType.AA] = mid * energy * 3f;
            weights[(int)VisemeType.EE] = high * energy * 2f;
            weights[(int)VisemeType.SS] = veryHigh * energy * 2f;
            
            // Lip closure for energy drops
            if (energy > 0.1f)
            {
                weights[(int)VisemeType.PP] = Mathf.Max(0, (0.3f - high) * energy);
            }
            
            // Normalize weights
            float maxWeight = 0f;
            for (int i = 0; i < weights.Length; i++)
            {
                maxWeight = Mathf.Max(maxWeight, weights[i]);
            }
            
            if (maxWeight > 1f)
            {
                for (int i = 0; i < weights.Length; i++)
                {
                    weights[i] /= maxWeight;
                }
            }
            
            return weights;
        }
        
        #region Private Methods
        
        private float[] ToMono(float[] samples, int channels)
        {
            if (channels == 1) return samples;
            
            int monoLength = samples.Length / channels;
            float[] mono = new float[monoLength];
            
            for (int i = 0; i < monoLength; i++)
            {
                float sum = 0f;
                for (int ch = 0; ch < channels; ch++)
                {
                    sum += samples[i * channels + ch];
                }
                mono[i] = sum / channels;
            }
            
            return mono;
        }
        
        private void AnalyzeEnergy(float[] samples, int sampleRate, LipSyncData data, float outputSampleRate)
        {
            int windowSize = sampleRate / (int)outputSampleRate;
            int outputSamples = Mathf.CeilToInt(samples.Length / (float)windowSize);
            
            for (int i = 0; i < outputSamples; i++)
            {
                int start = i * windowSize;
                int end = Mathf.Min(start + windowSize, samples.Length);
                
                float energy = 0f;
                for (int j = start; j < end; j++)
                {
                    energy += samples[j] * samples[j];
                }
                energy = Mathf.Sqrt(energy / (end - start));
                
                data.EnergyLevels.Add(energy);
            }
        }
        
        private void GenerateVisemeKeyframes(LipSyncData data)
        {
            if (data.EnergyLevels.Count == 0) return;
            
            float timeStep = 1f / data.EnergySampleRate;
            VisemeType lastViseme = VisemeType.Silence;
            
            for (int i = 0; i < data.EnergyLevels.Count; i++)
            {
                float energy = data.EnergyLevels[i];
                float time = i * timeStep;
                
                VisemeType viseme;
                float weight;
                
                if (energy < 0.01f)
                {
                    viseme = VisemeType.Silence;
                    weight = 0f;
                }
                else
                {
                    // Simple energy-based viseme selection
                    // In production, you'd want better analysis
                    if (energy < 0.05f)
                    {
                        viseme = VisemeType.PP; // Quiet = lips together
                        weight = energy * 10f;
                    }
                    else if (energy < 0.15f)
                    {
                        viseme = VisemeType.AA;
                        weight = Mathf.Clamp01(energy * 5f);
                    }
                    else if (energy < 0.3f)
                    {
                        viseme = VisemeType.OH;
                        weight = Mathf.Clamp01(energy * 3f);
                    }
                    else
                    {
                        viseme = VisemeType.AA;
                        weight = 1f;
                    }
                }
                
                // Only add keyframe if viseme changed
                if (viseme != lastViseme)
                {
                    data.Keyframes.Add(new VisemeKeyframe(time, viseme, weight));
                    lastViseme = viseme;
                }
            }
            
            // Add final keyframe
            data.Keyframes.Add(new VisemeKeyframe(data.Duration, VisemeType.Silence, 0f));
        }
        
        private float CalculateRMS(float[] samples)
        {
            float sum = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                sum += samples[i] * samples[i];
            }
            return Mathf.Sqrt(sum / samples.Length);
        }
        
        private float[] CalculateSpectrum(float[] samples)
        {
            // Simple power spectrum using DFT
            // For performance, use FFT in production
            int spectrumSize = 128;
            float[] spectrum = new float[spectrumSize];
            
            int windowSize = Mathf.Min(samples.Length, 512);
            
            for (int k = 0; k < spectrumSize; k++)
            {
                float real = 0f, imag = 0f;
                float freq = 2 * Mathf.PI * k / windowSize;
                
                for (int n = 0; n < windowSize; n++)
                {
                    float window = 0.54f - 0.46f * Mathf.Cos(2 * Mathf.PI * n / windowSize); // Hamming
                    float sample = samples[n] * window;
                    real += sample * Mathf.Cos(freq * n);
                    imag += sample * Mathf.Sin(freq * n);
                }
                
                spectrum[k] = Mathf.Sqrt(real * real + imag * imag) / windowSize;
            }
            
            return spectrum;
        }
        
        private float GetBandEnergy(float[] spectrum, int start, int end)
        {
            float sum = 0f;
            end = Mathf.Min(end, spectrum.Length);
            
            for (int i = start; i < end; i++)
            {
                sum += spectrum[i];
            }
            
            return sum / (end - start);
        }
        
        #endregion
    }
}
