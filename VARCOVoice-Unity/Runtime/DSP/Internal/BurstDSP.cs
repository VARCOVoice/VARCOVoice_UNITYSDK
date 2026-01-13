using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace VARCOVoice.DSP
{
    /// <summary>
    /// Burst-compiled DSP processing for maximum performance
    /// </summary>
    public static class BurstDSP
    {
        #region Utility Jobs
        
        /// <summary>
        /// Burst-optimized gain application
        /// </summary>
        [BurstCompile]
        public struct GainJob : IJobParallelFor
        {
            public NativeArray<float> Data;
            public float Gain;
            
            public void Execute(int index)
            {
                Data[index] *= Gain;
            }
        }
        
        /// <summary>
        /// Burst-optimized mix (wet/dry blend)
        /// </summary>
        [BurstCompile]
        public struct MixJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float> Dry;
            public NativeArray<float> Wet;
            public float Mix;
            
            public void Execute(int index)
            {
                Wet[index] = Dry[index] * (1f - Mix) + Wet[index] * Mix;
            }
        }
        
        #endregion
        
        #region Distortion Jobs
        
        /// <summary>
        /// Burst-optimized soft clip distortion
        /// </summary>
        [BurstCompile]
        public struct SoftClipJob : IJobParallelFor
        {
            public NativeArray<float> Data;
            public float Drive;
            
            public void Execute(int index)
            {
                float x = Data[index] * Drive;
                Data[index] = math.tanh(x);
            }
        }
        
        /// <summary>
        /// Burst-optimized hard clip distortion
        /// </summary>
        [BurstCompile]
        public struct HardClipJob : IJobParallelFor
        {
            public NativeArray<float> Data;
            public float Drive;
            public float Threshold;
            
            public void Execute(int index)
            {
                float x = Data[index] * Drive;
                Data[index] = math.clamp(x, -Threshold, Threshold);
            }
        }
        
        /// <summary>
        /// Burst-optimized bitcrusher
        /// </summary>
        [BurstCompile]
        public struct BitcrusherJob : IJobParallelFor
        {
            public NativeArray<float> Data;
            public float BitDepthLevels;
            
            public void Execute(int index)
            {
                float x = Data[index];
                Data[index] = math.round(x * BitDepthLevels) / BitDepthLevels;
            }
        }
        
        #endregion
        
        #region Filter Jobs
        
        /// <summary>
        /// Burst-optimized biquad filter (single sample, for sequential processing)
        /// </summary>
        [BurstCompile]
        public struct BiquadFilterJob : IJob
        {
            public NativeArray<float> Data;
            public float B0, B1, B2, A1, A2;
            public NativeArray<float> State; // x1, x2, y1, y2
            
            public void Execute()
            {
                float x1 = State[0];
                float x2 = State[1];
                float y1 = State[2];
                float y2 = State[3];
                
                for (int i = 0; i < Data.Length; i++)
                {
                    float x0 = Data[i];
                    float y0 = B0 * x0 + B1 * x1 + B2 * x2 - A1 * y1 - A2 * y2;
                    
                    Data[i] = y0;
                    
                    x2 = x1;
                    x1 = x0;
                    y2 = y1;
                    y1 = y0;
                }
                
                State[0] = x1;
                State[1] = x2;
                State[2] = y1;
                State[3] = y2;
            }
        }
        
        /// <summary>
        /// Burst-optimized one-pole lowpass filter
        /// </summary>
        [BurstCompile]
        public struct OnePoleLP : IJob
        {
            public NativeArray<float> Data;
            public float Coefficient;
            public NativeArray<float> State;
            
            public void Execute()
            {
                float state = State[0];
                float oneMinusCoef = 1f - Coefficient;
                
                for (int i = 0; i < Data.Length; i++)
                {
                    state = Coefficient * state + oneMinusCoef * Data[i];
                    Data[i] = state;
                }
                
                State[0] = state;
            }
        }
        
        #endregion
        
        #region Dynamics Jobs
        
        /// <summary>
        /// Burst-optimized envelope follower
        /// </summary>
        [BurstCompile]
        public struct EnvelopeFollower : IJob
        {
            [ReadOnly] public NativeArray<float> Input;
            public NativeArray<float> Output;
            public float AttackCoef;
            public float ReleaseCoef;
            public NativeArray<float> State;
            
            public void Execute()
            {
                float envelope = State[0];
                
                for (int i = 0; i < Input.Length; i++)
                {
                    float absInput = math.abs(Input[i]);
                    float coef = absInput > envelope ? AttackCoef : ReleaseCoef;
                    envelope = coef * envelope + (1f - coef) * absInput;
                    Output[i] = envelope;
                }
                
                State[0] = envelope;
            }
        }
        
        /// <summary>
        /// Burst-optimized compressor gain calculation
        /// </summary>
        [BurstCompile]
        public struct CompressorGainJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float> Envelope;
            public NativeArray<float> GainReduction;
            public float Threshold;
            public float Ratio;
            public float Knee;
            
            public void Execute(int index)
            {
                float envDb = 20f * math.log10(math.max(Envelope[index], 0.00001f));
                float halfKnee = Knee * 0.5f;
                
                float gainDb;
                if (envDb <= Threshold - halfKnee)
                {
                    gainDb = 0f;
                }
                else if (envDb >= Threshold + halfKnee)
                {
                    float excess = envDb - Threshold;
                    gainDb = -excess * (1f - 1f / Ratio);
                }
                else
                {
                    float x = envDb - Threshold + halfKnee;
                    float y = x * x / (2f * Knee);
                    gainDb = -y * (1f - 1f / Ratio);
                }
                
                GainReduction[index] = math.pow(10f, gainDb / 20f);
            }
        }
        
        /// <summary>
        /// Burst-optimized limiter
        /// </summary>
        [BurstCompile]
        public struct LimiterJob : IJob
        {
            public NativeArray<float> Data;
            public float Ceiling;
            public float ReleaseCoef;
            public NativeArray<float> State; // Current gain
            
            public void Execute()
            {
                float gain = State[0];
                
                for (int i = 0; i < Data.Length; i++)
                {
                    float absVal = math.abs(Data[i]);
                    float targetGain = absVal > Ceiling ? Ceiling / absVal : 1f;
                    
                    // Instant attack, smooth release
                    if (targetGain < gain)
                        gain = targetGain;
                    else
                        gain = ReleaseCoef * gain + (1f - ReleaseCoef) * targetGain;
                    
                    Data[i] *= gain;
                }
                
                State[0] = gain;
            }
        }
        
        #endregion
        
        #region Modulation Jobs
        
        /// <summary>
        /// Burst-optimized tremolo
        /// </summary>
        [BurstCompile]
        public struct TremoloJob : IJob
        {
            public NativeArray<float> Data;
            public float Rate;
            public float Depth;
            public float SampleRate;
            public NativeArray<float> State; // Phase
            
            public void Execute()
            {
                float phase = State[0];
                float phaseInc = Rate * 2f * math.PI / SampleRate;
                
                for (int i = 0; i < Data.Length; i++)
                {
                    float lfo = (math.sin(phase) * 0.5f + 0.5f);
                    float gain = 1f - Depth * (1f - lfo);
                    Data[i] *= gain;
                    
                    phase += phaseInc;
                    if (phase >= 2f * math.PI) phase -= 2f * math.PI;
                }
                
                State[0] = phase;
            }
        }
        
        /// <summary>
        /// Burst-optimized ring modulator
        /// </summary>
        [BurstCompile]
        public struct RingModJob : IJob
        {
            public NativeArray<float> Data;
            public float Frequency;
            public float SampleRate;
            public float Mix;
            public NativeArray<float> State;
            
            public void Execute()
            {
                float phase = State[0];
                float phaseInc = Frequency * 2f * math.PI / SampleRate;
                
                for (int i = 0; i < Data.Length; i++)
                {
                    float carrier = math.sin(phase);
                    float dry = Data[i];
                    float wet = dry * carrier;
                    Data[i] = dry * (1f - Mix) + wet * Mix;
                    
                    phase += phaseInc;
                    if (phase >= 2f * math.PI) phase -= 2f * math.PI;
                }
                
                State[0] = phase;
            }
        }
        
        #endregion
        
        #region FFT Jobs (for Spectrum Analysis)
        
        /// <summary>
        /// Burst-optimized magnitude spectrum calculation
        /// </summary>
        [BurstCompile]
        public struct MagnitudeSpectrumJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float> Real;
            [ReadOnly] public NativeArray<float> Imag;
            public NativeArray<float> Magnitude;
            
            public void Execute(int index)
            {
                float r = Real[index];
                float i = Imag[index];
                Magnitude[index] = math.sqrt(r * r + i * i);
            }
        }
        
        /// <summary>
        /// Burst-optimized RMS calculation
        /// </summary>
        [BurstCompile]
        public struct RMSJob : IJob
        {
            [ReadOnly] public NativeArray<float> Data;
            public NativeArray<float> Result;
            
            public void Execute()
            {
                float sum = 0f;
                for (int i = 0; i < Data.Length; i++)
                {
                    sum += Data[i] * Data[i];
                }
                Result[0] = math.sqrt(sum / Data.Length);
            }
        }
        
        #endregion
        
        #region Helper Methods
        
        /// <summary>
        /// Process array with Burst-optimized soft clip
        /// </summary>
        public static void ProcessSoftClip(float[] data, float drive)
        {
            using (var nativeData = new NativeArray<float>(data, Allocator.TempJob))
            {
                var job = new SoftClipJob
                {
                    Data = nativeData,
                    Drive = drive
                };
                
                job.Schedule(data.Length, 64).Complete();
                nativeData.CopyTo(data);
            }
        }
        
        /// <summary>
        /// Apply gain with Burst optimization
        /// </summary>
        public static void ProcessGain(float[] data, float gain)
        {
            using (var nativeData = new NativeArray<float>(data, Allocator.TempJob))
            {
                var job = new GainJob
                {
                    Data = nativeData,
                    Gain = gain
                };
                
                job.Schedule(data.Length, 64).Complete();
                nativeData.CopyTo(data);
            }
        }
        
        /// <summary>
        /// Calculate RMS level with Burst optimization
        /// </summary>
        public static float CalculateRMS(float[] data)
        {
            using (var nativeData = new NativeArray<float>(data, Allocator.TempJob))
            using (var result = new NativeArray<float>(1, Allocator.TempJob))
            {
                var job = new RMSJob
                {
                    Data = nativeData,
                    Result = result
                };
                
                job.Schedule().Complete();
                return result[0];
            }
        }
        
        #endregion
    }
}
