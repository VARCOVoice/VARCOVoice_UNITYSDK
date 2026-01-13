using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;
using VARCOVoice.DSP;

namespace VARCOVoice.Editor
{
    /// <summary>
    /// Handles offline processing (Baking) of audio clips through the DSP Chain.
    /// Exports the result as a standard WAV file.
    /// </summary>
    public static class AudioBaker
    {
        /// <summary>
        /// Process an AudioClip through a list of DSP effects and save to WAV
        /// </summary>
        /// <param name="sourceClip">Input clip</param>
        /// <param name="effects">List of configured effects (cloned)</param>
        /// <param name="outputPath">Full path to save .wav file</param>
        public static bool Bake(AudioClip sourceClip, List<IDSPEffect> effects, string outputPath)
        {
            if (sourceClip == null)
            {
                Debug.LogError("[AudioBaker] Source clip is null.");
                return false;
            }

            int channels = sourceClip.channels;
            int sampleRate = sourceClip.frequency;
            int totalSamples = sourceClip.samples * channels;

            // 1. Get raw data
            float[] data = new float[totalSamples];
            sourceClip.GetData(data, 0);

            // 2. Prepare effects
            // Ensure all effects are reset before processing
            // Also enforce High Quality (disable PreviewMode) for LinearPhaseEQ
            foreach (var effect in effects)
            {
                effect.Reset();
            }

            // 3. Process in blocks
            // Simulating Unity's OnAudioFilterRead blocking for better stability with block-based effects (FFT)
            // 2048 is a standard buffer size
            int blockSize = 2048 * channels;
            int processed = 0;
            float[] blockBuffer = new float[blockSize];
            var limiter = new LookaheadLimiter();

            try
            {
                // ... processing loop ...
                while (processed < totalSamples)
                {
                    int count = Math.Min(blockSize, totalSamples - processed);

                    // Copy to temp block (pad with zero if last block is small, though effects might need full blocks)
                    // Just resizing/handling last block carefully
                    if (count < blockSize)
                    {
                        Array.Clear(blockBuffer, 0, blockSize);
                    }
                    Array.Copy(data, processed, blockBuffer, 0, count);

                    // Apply all effects
                    foreach (var effect in effects)
                    {
                        if (effect.Enabled)
                        {
                            effect.Process(blockBuffer, channels, sampleRate);
                            SanitizeBuffer(blockBuffer);
                        }
                    }

                    limiter.Process(blockBuffer, channels, sampleRate);

                    // Copy back
                    Array.Copy(blockBuffer, 0, data, processed, count);

                    processed += count;
                    
                    // Simple progress bar
                    if (processed % (blockSize * 10) == 0)
                    {
                        float progress = (float)processed / totalSamples;
                        if (EditorUtility.DisplayCancelableProgressBar("Baking Audio", $"Processing... {progress:P0}", progress))
                        {
                            EditorUtility.ClearProgressBar();
                            return false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AudioBaker] Error during processing: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            // 4. Save to WAV
            return WriteWavFile(data, channels, sampleRate, outputPath);
        }

        private static bool WriteWavFile(float[] data, int channels, int sampleRate, string filepath)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(filepath));

                using (FileStream fileStream = new FileStream(filepath, FileMode.Create))
                using (BinaryWriter writer = new BinaryWriter(fileStream))
                {
                    int sampleCount = data.Length;
                    short bitsPerSample = 16;
                    int subChunk2Size = sampleCount * channels * (bitsPerSample / 8); 
                    // Actually data.Length INCLUDES channels. so sampleCount IS TOTAL SAMPLES (L+R+...)
                    // sampleCount IS NOT frames. 
                    // ByteRate = SampleRate * NumChannels * BitsPerSample/8
                    // BlockAlign = NumChannels * BitsPerSample/8
                    
                    int byteRate = sampleRate * channels * (bitsPerSample / 8);
                    short blockAlign = (short)(channels * (bitsPerSample / 8));
                    
                    // data.Length is total floats. 
                    // size in bytes = total floats * 2 (16bit)
                    int dataSize = data.Length * 2;

                    // Header
                    writer.Write(Encoding.UTF8.GetBytes("RIFF"));
                    writer.Write(36 + dataSize); // ChunkSize
                    writer.Write(Encoding.UTF8.GetBytes("WAVE"));
                    
                    // Subchunk1 (fmt)
                    writer.Write(Encoding.UTF8.GetBytes("fmt "));
                    writer.Write(16); // Subchunk1Size (16 for PCM)
                    writer.Write((short)1); // AudioFormat (1 = PCM)
                    writer.Write((short)channels);
                    writer.Write(sampleRate);
                    writer.Write(byteRate);
                    writer.Write(blockAlign);
                    writer.Write(bitsPerSample);
                    
                    // Subchunk2 (data)
                    writer.Write(Encoding.UTF8.GetBytes("data"));
                    writer.Write(dataSize);
                    
                    // Convert float to Int16
                    // Clip values to -1..1, no normalization to retain original levels
                    
                    Int16[] intData = new Int16[data.Length];
                    for (int i = 0; i < data.Length; i++)
                    {
                        float sample = data[i];
                        if (float.IsNaN(sample) || float.IsInfinity(sample)) sample = 0f;
                        if (sample > 1f) sample = 1f;
                        if (sample < -1f) sample = -1f;
                        intData[i] = (short)(sample * 32767f);
                    }
                    
                    // Write
                    byte[] bytes = new byte[intData.Length * 2];
                    Buffer.BlockCopy(intData, 0, bytes, 0, bytes.Length);
                    writer.Write(bytes);
                }
                
                Debug.Log($"[AudioBaker] Saved processed audio to: {filepath}");
                AssetDatabase.Refresh();
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AudioBaker] Output error: {ex.Message}");
                return false;
            }
        }

        private static void SanitizeBuffer(float[] data)
        {
            const float DENORMAL_THRESHOLD = 1e-15f;
            for (int i = 0; i < data.Length; i++)
            {
                float value = data[i];
                // Check for NaN and Infinity
                if (float.IsNaN(value) || float.IsInfinity(value))
                {
                    data[i] = 0f;
                }
                // Check for denormals - these cause massive CPU spikes
                else if (value != 0f && MathF.Abs(value) < DENORMAL_THRESHOLD)
                {
                    data[i] = 0f;
                }
            }
        }

        private sealed class LookaheadLimiter
        {
            private float[] _delayBuffer;
            private int _writePos;
            private float _gain = 1f;
            private int _delaySamples;
            private int _channels;
            private float _releaseCoef;

            private const float Ceiling = 0.98f;
            private const float LookaheadMs = 3f;
            private const float ReleaseMs = 50f;

            public void Process(float[] data, int channels, int sampleRate)
            {
                if (data == null || data.Length == 0) return;
                EnsureInitialized(channels, sampleRate);

                int frames = data.Length / channels;
                int bufferSize = _delayBuffer.Length;
                int readBase = _writePos - _delaySamples * channels;
                if (readBase < 0) readBase += bufferSize;

                for (int frame = 0; frame < frames; frame++)
                {
                    int writeIndex = _writePos;
                    int readIndex = readBase;

                    float peak = 0f;
                    for (int ch = 0; ch < channels; ch++)
                    {
                        float sample = data[frame * channels + ch];
                        float absVal = Mathf.Abs(sample);
                        if (absVal > peak) peak = absVal;
                        _delayBuffer[writeIndex + ch] = sample;
                    }

                    float targetGain = peak > Ceiling ? Ceiling / peak : 1f;
                    if (targetGain < _gain)
                    {
                        _gain = targetGain;
                    }
                    else
                    {
                        _gain = _releaseCoef * _gain + (1f - _releaseCoef) * targetGain;
                    }

                    for (int ch = 0; ch < channels; ch++)
                    {
                        data[frame * channels + ch] = _delayBuffer[readIndex + ch] * _gain;
                    }

                    _writePos += channels;
                    if (_writePos >= bufferSize) _writePos = 0;

                    readBase += channels;
                    if (readBase >= bufferSize) readBase = 0;
                }
            }

            private void EnsureInitialized(int channels, int sampleRate)
            {
                int lookaheadSamples = Mathf.Max(1, (int)(LookaheadMs * sampleRate / 1000f));
                int bufferSamples = lookaheadSamples * channels;
                if (_delayBuffer == null || _delayBuffer.Length != bufferSamples || _channels != channels || _delaySamples != lookaheadSamples)
                {
                    _delayBuffer = new float[bufferSamples];
                    _writePos = 0;
                    _gain = 1f;
                    _delaySamples = lookaheadSamples;
                    _channels = channels;
                }

                float releaseSeconds = Mathf.Max(0.001f, ReleaseMs * 0.001f);
                _releaseCoef = Mathf.Exp(-1f / (releaseSeconds * sampleRate));
            }
        }
    }
}
