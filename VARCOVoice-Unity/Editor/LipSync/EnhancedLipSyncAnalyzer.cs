using System;
using System.Collections.Generic;
using UnityEngine;

namespace VARCOVoice.LipSync
{
    /// <summary>
    /// Enhanced phoneme-based lip sync analyzer for Korean language
    /// Uses formant analysis for more accurate viseme detection
    /// NOTE: This is an Editor-only class used for Export-time Viseme preprocessing.
    /// </summary>
    public class EnhancedLipSyncAnalyzer
    {
        #region Configuration
        
        /// <summary>
        /// FFT window size for analysis
        /// </summary>
        public int WindowSize { get; set; } = 1024;
        
        /// <summary>
        /// Samples per viseme frame
        /// </summary>
        public int SamplesPerFrame { get; set; } = 512;
        
        /// <summary>
        /// Minimum energy threshold for speech detection
        /// </summary>
        public float SilenceThreshold { get; set; } = 0.02f;
        
        /// <summary>
        /// Smoothing factor for viseme transitions
        /// </summary>
        public float Smoothing { get; set; } = 0.3f;
        
        #endregion
        
        #region Formant Definitions
        
        // Korean vowel formant frequencies (F1, F2 in Hz)
        // Based on acoustic phonetics research
        private static readonly Dictionary<char, (int f1, int f2)> KoreanVowelFormants = new Dictionary<char, (int f1, int f2)>
        {
            { '\u314F', (800, 1200) },   // ㅏ AA - open vowel
            { '\u3150', (600, 1000) },   // ㅐ similar to AA
            { '\u3157', (450, 800) },    // ㅗ OH - rounded back
            { '\u315C', (350, 800) },    // ㅜ OO - close back rounded
            { '\u3161', (400, 1500) },   // ㅡ close back unrounded
            { '\u3163', (300, 2300) },   // ㅣ EE - close front
            { '\u3154', (500, 1900) },   // ㅔ E - mid front
        };
        
        // Viseme to formant range mapping
        private static readonly Dictionary<VisemeType, (float f1Min, float f1Max, float f2Min, float f2Max)> VisemeFormants = 
            new Dictionary<VisemeType, (float, float, float, float)>
        {
            { VisemeType.AA, (600, 900, 1000, 1400) },
            { VisemeType.EE, (250, 400, 2000, 2500) },
            { VisemeType.IH, (350, 500, 1800, 2200) },
            { VisemeType.OH, (400, 550, 700, 1000) },
            { VisemeType.OO, (300, 450, 600, 900) },
        };
        
        #endregion
        
        #region Analysis Buffers
        
        private float[] _windowBuffer;
        private float[] _spectrum;
        private float[] _smoothedSpectrum;
        private float[] _hammingWindow;
        private float[] _previousVisemeWeights;
        
        #endregion
        
        #region Public API
        
        /// <summary>
        /// Analyze audio clip with enhanced phoneme detection
        /// </summary>
        public LipSyncData AnalyzeEnhanced(AudioClip clip, float frameRate = 60f)
        {
            if (clip == null) return null;
            
            var data = new LipSyncData
            {
                ClipName = clip.name,
                Duration = clip.length,
                EnergySampleRate = frameRate
            };
            
            // Get samples
            float[] samples = new float[clip.samples * clip.channels];
            clip.GetData(samples, 0);
            
            // Convert to mono
            float[] mono = ToMono(samples, clip.channels);
            
            // Initialize buffers
            InitializeBuffers();
            
            // Process frames
            int samplesPerFrame = (int)(clip.frequency / frameRate);
            int totalFrames = mono.Length / samplesPerFrame;
            
            VisemeType lastViseme = VisemeType.Silence;
            float lastWeight = 0f;
            
            for (int frame = 0; frame < totalFrames; frame++)
            {
                int startSample = frame * samplesPerFrame;
                float time = (float)startSample / clip.frequency;
                
                // Get frame samples
                int frameSamples = Mathf.Min(WindowSize, mono.Length - startSample);
                if (frameSamples < WindowSize / 2) break;
                
                Array.Copy(mono, startSample, _windowBuffer, 0, frameSamples);
                if (frameSamples < WindowSize)
                {
                    Array.Clear(_windowBuffer, frameSamples, WindowSize - frameSamples);
                }
                
                // Analyze frame
                var (viseme, weight) = AnalyzeFrame(_windowBuffer, clip.frequency);
                
                // Apply smoothing
                weight = Mathf.Lerp(lastWeight, weight, 1f - Smoothing);
                
                // Record energy
                data.EnergyLevels.Add(weight);
                
                // Add keyframe if changed
                if (viseme != lastViseme || Mathf.Abs(weight - lastWeight) > 0.1f)
                {
                    data.Keyframes.Add(new VisemeKeyframe(time, viseme, weight));
                    lastViseme = viseme;
                }
                
                lastWeight = weight;
            }
            
            // Final silence keyframe
            data.Keyframes.Add(new VisemeKeyframe(clip.length, VisemeType.Silence, 0f));
            
            return data;
        }
        
        /// <summary>
        /// Real-time frame analysis with formant detection
        /// Returns array of viseme weights
        /// </summary>
        public float[] AnalyzeFrameRealtime(float[] samples, int channels, int sampleRate)
        {
            InitializeBuffers();
            
            float[] mono = ToMono(samples, channels);
            
            // Pad or truncate to window size
            if (mono.Length < WindowSize)
            {
                Array.Copy(mono, _windowBuffer, mono.Length);
                Array.Clear(_windowBuffer, mono.Length, WindowSize - mono.Length);
            }
            else
            {
                Array.Copy(mono, _windowBuffer, WindowSize);
            }
            
            return AnalyzeFrameDetailed(_windowBuffer, sampleRate);
        }
        
        #endregion
        
        #region Private Methods
        
        private void InitializeBuffers()
        {
            if (_windowBuffer == null || _windowBuffer.Length != WindowSize)
            {
                _windowBuffer = new float[WindowSize];
                _spectrum = new float[WindowSize / 2];
                _smoothedSpectrum = new float[WindowSize / 2];
                _hammingWindow = new float[WindowSize];
                _previousVisemeWeights = new float[15];
                
                // Create Hamming window
                for (int i = 0; i < WindowSize; i++)
                {
                    _hammingWindow[i] = 0.54f - 0.46f * Mathf.Cos(2f * Mathf.PI * i / (WindowSize - 1));
                }
            }
        }
        
        private (VisemeType viseme, float weight) AnalyzeFrame(float[] buffer, int sampleRate)
        {
            // Calculate RMS energy
            float energy = CalculateEnergy(buffer);
            
            if (energy < SilenceThreshold)
            {
                return (VisemeType.Silence, 0f);
            }
            
            // Apply window
            for (int i = 0; i < WindowSize; i++)
            {
                buffer[i] *= _hammingWindow[i];
            }
            
            // Calculate spectrum
            CalculateSpectrum(buffer, _spectrum);
            
            // Smooth spectrum
            SmoothSpectrum(_spectrum, _smoothedSpectrum);
            
            // Detect formants
            var (f1, f2) = DetectFormants(_smoothedSpectrum, sampleRate);
            
            // Map to viseme
            var viseme = MapFormantsToViseme(f1, f2, energy);
            
            // Calculate weight based on energy
            float weight = Mathf.Clamp01(energy * 5f);
            
            return (viseme, weight);
        }
        
        private float[] AnalyzeFrameDetailed(float[] buffer, int sampleRate)
        {
            float[] weights = new float[15];
            
            float energy = CalculateEnergy(buffer);
            
            if (energy < SilenceThreshold)
            {
                weights[(int)VisemeType.Silence] = 1f;
                ApplySmoothing(weights);
                return weights;
            }
            
            // Apply window
            for (int i = 0; i < WindowSize; i++)
            {
                buffer[i] *= _hammingWindow[i];
            }
            
            // Calculate spectrum
            CalculateSpectrum(buffer, _spectrum);
            SmoothSpectrum(_spectrum, _smoothedSpectrum);
            
            // Detect formants
            var (f1, f2) = DetectFormants(_smoothedSpectrum, sampleRate);
            
            // Calculate weights for each vowel viseme based on formant proximity
            foreach (var kvp in VisemeFormants)
            {
                var range = kvp.Value;
                float f1Score = CalculateFormantScore(f1, range.f1Min, range.f1Max);
                float f2Score = CalculateFormantScore(f2, range.f2Min, range.f2Max);
                float score = (f1Score + f2Score) / 2f;
                
                weights[(int)kvp.Key] = score * energy * 3f;
            }
            
            // Consonant detection based on spectral characteristics
            float highFreqEnergy = CalculateHighFrequencyEnergy(_smoothedSpectrum, sampleRate);
            float lowFreqEnergy = CalculateLowFrequencyEnergy(_smoothedSpectrum, sampleRate);
            
            // Sibilants
            if (highFreqEnergy > 0.3f && energy > 0.1f)
            {
                weights[(int)VisemeType.SS] = highFreqEnergy * energy * 2f;
            }
            
            // Stops/Plosives - lip closure
            if (lowFreqEnergy > highFreqEnergy * 2f && energy > 0.15f)
            {
                weights[(int)VisemeType.PP] = energy * 1.5f;
            }
            
            // Normalize
            NormalizeWeights(weights);
            ApplySmoothing(weights);
            
            return weights;
        }
        
        private float CalculateEnergy(float[] buffer)
        {
            float sum = 0f;
            for (int i = 0; i < buffer.Length; i++)
            {
                sum += buffer[i] * buffer[i];
            }
            return Mathf.Sqrt(sum / buffer.Length);
        }
        
        private void CalculateSpectrum(float[] buffer, float[] spectrum)
        {
            // Simple DFT for spectrum calculation
            int n = buffer.Length;
            int spectrumSize = n / 2;
            
            for (int k = 0; k < spectrumSize; k++)
            {
                float real = 0f, imag = 0f;
                float freq = 2f * Mathf.PI * k / n;
                
                for (int i = 0; i < n; i++)
                {
                    real += buffer[i] * Mathf.Cos(freq * i);
                    imag += buffer[i] * Mathf.Sin(freq * i);
                }
                
                spectrum[k] = Mathf.Sqrt(real * real + imag * imag) / n;
            }
        }
        
        private void SmoothSpectrum(float[] input, float[] output)
        {
            int windowWidth = 5;
            
            for (int i = 0; i < input.Length; i++)
            {
                float sum = 0f;
                int count = 0;
                
                for (int j = -windowWidth; j <= windowWidth; j++)
                {
                    int idx = i + j;
                    if (idx >= 0 && idx < input.Length)
                    {
                        sum += input[idx];
                        count++;
                    }
                }
                
                output[i] = sum / count;
            }
        }
        
        private (float f1, float f2) DetectFormants(float[] spectrum, int sampleRate)
        {
            // Find peaks in spectrum
            List<(int bin, float magnitude)> peaks = new List<(int, float)>();
            float freqPerBin = (float)sampleRate / (spectrum.Length * 2);
            
            // F1 range: 200-900 Hz
            // F2 range: 800-2500 Hz
            int f1MinBin = (int)(200 / freqPerBin);
            int f1MaxBin = (int)(900 / freqPerBin);
            int f2MinBin = (int)(800 / freqPerBin);
            int f2MaxBin = (int)(2500 / freqPerBin);
            
            f1MaxBin = Mathf.Min(f1MaxBin, spectrum.Length - 1);
            f2MaxBin = Mathf.Min(f2MaxBin, spectrum.Length - 1);
            
            // Find F1 peak
            float f1 = 500f;
            float f1MaxMag = 0f;
            for (int i = f1MinBin; i <= f1MaxBin; i++)
            {
                if (spectrum[i] > f1MaxMag)
                {
                    f1MaxMag = spectrum[i];
                    f1 = i * freqPerBin;
                }
            }
            
            // Find F2 peak
            float f2 = 1500f;
            float f2MaxMag = 0f;
            for (int i = f2MinBin; i <= f2MaxBin; i++)
            {
                if (spectrum[i] > f2MaxMag)
                {
                    f2MaxMag = spectrum[i];
                    f2 = i * freqPerBin;
                }
            }
            
            return (f1, f2);
        }
        
        private VisemeType MapFormantsToViseme(float f1, float f2, float energy)
        {
            float bestScore = 0f;
            VisemeType bestViseme = VisemeType.AA;
            
            foreach (var kvp in VisemeFormants)
            {
                var range = kvp.Value;
                float f1Score = CalculateFormantScore(f1, range.f1Min, range.f1Max);
                float f2Score = CalculateFormantScore(f2, range.f2Min, range.f2Max);
                float score = (f1Score + f2Score) / 2f;
                
                if (score > bestScore)
                {
                    bestScore = score;
                    bestViseme = kvp.Key;
                }
            }
            
            return bestViseme;
        }
        
        private float CalculateFormantScore(float value, float min, float max)
        {
            if (value >= min && value <= max)
            {
                return 1f;
            }
            
            float distance = value < min ? min - value : value - max;
            return Mathf.Exp(-distance / 200f); // Gaussian falloff
        }
        
        private float CalculateHighFrequencyEnergy(float[] spectrum, int sampleRate)
        {
            float freqPerBin = (float)sampleRate / (spectrum.Length * 2);
            int startBin = (int)(3000 / freqPerBin);
            int endBin = Mathf.Min((int)(8000 / freqPerBin), spectrum.Length - 1);
            
            float sum = 0f;
            for (int i = startBin; i <= endBin; i++)
            {
                sum += spectrum[i];
            }
            
            return sum / (endBin - startBin + 1);
        }
        
        private float CalculateLowFrequencyEnergy(float[] spectrum, int sampleRate)
        {
            float freqPerBin = (float)sampleRate / (spectrum.Length * 2);
            int endBin = Mathf.Min((int)(500 / freqPerBin), spectrum.Length - 1);
            
            float sum = 0f;
            for (int i = 0; i <= endBin; i++)
            {
                sum += spectrum[i];
            }
            
            return sum / (endBin + 1);
        }
        
        private void NormalizeWeights(float[] weights)
        {
            float max = 0f;
            for (int i = 0; i < weights.Length; i++)
            {
                if (weights[i] > max) max = weights[i];
            }
            
            if (max > 1f)
            {
                for (int i = 0; i < weights.Length; i++)
                {
                    weights[i] /= max;
                }
            }
        }
        
        private void ApplySmoothing(float[] weights)
        {
            if (_previousVisemeWeights == null) return;
            
            for (int i = 0; i < weights.Length; i++)
            {
                weights[i] = Mathf.Lerp(_previousVisemeWeights[i], weights[i], 1f - Smoothing);
                _previousVisemeWeights[i] = weights[i];
            }
        }
        
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
        
        #endregion
    }
}
