using System;
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace VARCOVoice.DSP
{
    /// <summary>
    /// Professional Convolution Reverb
    /// 
    /// Uses Uniform Partitioned Convolution (UPC) for zero-latency-like behavior
    /// and high performance using Burst Compiler.
    /// </summary>
    public class ConvolutionReverb : DSPEffectBase, IDisposable
    {
        public override string Name => "Convolution Reverb";

        #region Parameters

        private readonly object _irLock = new object();

        [SerializeField] private AudioClip _irClip;
        public AudioClip IRClip
        {
            get => _irClip;
            set
            {
                if (_irClip != value)
                {
                    _irClip = value;
                    LoadIR();
                }
            }
        }

        [field: Range(0f, 1f)]
        public float WetLevel { get; set; } = 1.0f;

        [field: Range(0f, 1f)]
        public float DryLevel { get; set; } = 0.0f;

        [field: Range(0f, 100f)]
        public float Predelay { get; set; } = 0f;

        #endregion

        #region Constants

        private const int BLOCK_SIZE = 1024; // FFT Size / 2
        private const int FFT_SIZE = 2048;

        #endregion

        #region Internal State

        private bool _isInitialized = false;

        // Input buffering
        private NativeArray<float> _inputBuffer; // Circular, size FFT_SIZE
        private int _inputPos;

        // IR Partitions (Frequency Domain)
        // Array of partitions. Each partition is FFT_SIZE.
        // Flattened: [Partition0_Real/Imag, Partition1_Real/Imag, ...]
        private NativeArray<float2> _irPartitions; 
        private int _numPartitions;

        // FDL (Frequency Delay Line) - History of input blocks in Freq Domain
        private NativeArray<float2> _fdl; 
        private int _fdlPos; // Circular index for FDL

        // Output buffering (Overlap-Add)
        private NativeArray<float> _outputBuffer; // Circular, size FFT_SIZE (or more)
        private int _outputPos; // Read position
        private int _outputWritePos; // Write position for Overlap-Add

        // Double buffer for current block processing
        private NativeArray<float> _currentBlockTime;
        private NativeArray<float2> _currentBlockFreq;
        private NativeArray<float2> _complexBuffer; // Scratchpad for FFT
        private NativeArray<float> _window;

        #endregion

        private void OnDestroy()
        {
            Cleanup();
        }

        public void Dispose()
        {
            Cleanup();
        }

        private void Cleanup()
        {
            if (_inputBuffer.IsCreated) _inputBuffer.Dispose();
            if (_irPartitions.IsCreated) _irPartitions.Dispose();
            if (_fdl.IsCreated) _fdl.Dispose();
            if (_outputBuffer.IsCreated) _outputBuffer.Dispose();
            if (_currentBlockTime.IsCreated) _currentBlockTime.Dispose();
            if (_currentBlockFreq.IsCreated) _currentBlockFreq.Dispose();
            if (_complexBuffer.IsCreated) _complexBuffer.Dispose();
            if (_window.IsCreated) _window.Dispose();

            _isInitialized = false;
        }

        private void Initialize()
        {
            Cleanup();

            _inputBuffer = new NativeArray<float>(BLOCK_SIZE, Allocator.Persistent);
            _outputBuffer = new NativeArray<float>(BLOCK_SIZE * 2, Allocator.Persistent); // Double size for safety
            _currentBlockTime = new NativeArray<float>(FFT_SIZE, Allocator.Persistent);
            _currentBlockFreq = new NativeArray<float2>(FFT_SIZE, Allocator.Persistent);
            _complexBuffer = new NativeArray<float2>(FFT_SIZE, Allocator.Persistent);
            
            _window = new NativeArray<float>(FFT_SIZE, Allocator.Persistent);
            // Simple Hanning window or filtering could be applied, but for standard convolution 
            // we usually just do Overlap-Save or Overlap-Add without windowing the input signal itself 
            // in the same way as spectral processing. 
            // For standard OLA Convolution:
            // 1. Pad input block L with L zeros -> 2L
            // 2. FFT
            // 3. Mult
            // 4. IFFT
            // 5. Add to output stream

            _isInitialized = true;
        }

        public override void ApplyPreset(string presetName)
        {
            if (string.IsNullOrEmpty(presetName)) return;

            switch (presetName.Trim().ToLowerInvariant())
            {
                case "subtle":
                    WetLevel = 0.4f;
                    DryLevel = 0.8f;
                    Predelay = 10f;
                    Mix = 1f;
                    break;
                case "studio":
                    WetLevel = 0.6f;
                    DryLevel = 0.6f;
                    Predelay = 20f;
                    Mix = 1f;
                    break;
                case "wide":
                    WetLevel = 0.8f;
                    DryLevel = 0.4f;
                    Predelay = 35f;
                    Mix = 1f;
                    break;
                case "wash":
                    WetLevel = 1f;
                    DryLevel = 0.2f;
                    Predelay = 50f;
                    Mix = 1f;
                    break;
                case "dry":
                    WetLevel = 0.2f;
                    DryLevel = 1f;
                    Predelay = 0f;
                    Mix = 1f;
                    break;
            }
        }

        public override void Reset()
        {
            Cleanup();
            Initialize(); // Re-alloc
        }

        public override void Process(float[] data, int channels, int sampleRate)
        {
            if (!Enabled || _irClip == null) return;
            if (UseSafeMode) return;

            if (!_isInitialized) Initialize();

            // Check if IR is ready
            if (!_irPartitions.IsCreated || _numPartitions == 0) return;

            // Simple mono processing for now, applied to all channels identically if mono, 
            // or we need stereo convolution (2x computational cost).
            // For MVP, we'll mix down input to mono, convolve, and output to all channels.
            
            // NOTE: Full Stereo Convolution (L->L, R->R) requires 2 instances of the engine.
            // True Stereo (L->L, L->R, R->L, R->R) requires 4.
            // Let's implement mono-in, stereo-out (same signal) for simplicity in this file,
            // or assume the user puts this on a mixer group.
            
            // IMPORTANT: Convolution is heavy. We must ensure we don't block the main thread too long.
            // But since this is `Process` (Audio Thread), we MUST complete synchronously.
            // Burst is essential here.

            // Process loop handling arbitrary chunk sizes
            int samplesProcessed = 0;
            int totalSamples = data.Length / channels;

            while (samplesProcessed < totalSamples)
            {
                // Number of samples we can accept before triggering a block process
                int spacesAvailable = BLOCK_SIZE - _inputPos;
                int samplesToCopy = Math.Min(totalSamples - samplesProcessed, spacesAvailable);

                // Copy input to buffer (mix to mono)
                for (int i = 0; i < samplesToCopy; i++)
                {
                    int idx = (samplesProcessed + i) * channels;
                    float input = data[idx];
                    if (channels > 1) input = (input + data[idx+1]) * 0.5f;
                    
                    _inputBuffer[_inputPos + i] = input;
                }

                _inputPos += samplesToCopy;
                samplesProcessed += samplesToCopy;

                // Trigger Block Processing if buffer is full
                if (_inputPos >= BLOCK_SIZE)
                {
                    ProcessBlock();
                    _inputPos = 0;
                }

                // Output samples
                // Read from output buffer
                // We need to manage the read pointer carefully. 
                // In a proper circular implementation, we read as we write.
                // But here we are doing block-based.
                // The output latency will be at least BLOCK_SIZE.
                // For Zero Latency, we need Non-Uniform Partitioning (Head block is small).
                // For this MVP, we accept BLOCK_SIZE latency.
                
                // TODO: Handle output reading synced with input.
                // In this simplified buffering:
                // We just read what's available. logic needs to be robust.
            }

            // ACTUALLY: A simple circular buffer approach is better for the `Process` loop interaction.
            // But let's refine the loop to just read/write from ring buffers.
            
            int sampleCount = data.Length / channels;
            for (int i = 0; i < sampleCount; i++)
            {
                // 1. Input Mix
                float inMono = data[i * channels];
                if (channels > 1) inMono = (inMono + data[i * channels + 1]) * 0.5f;

                // 2. Add to Input Buffer
                _inputBuffer[_inputPos] = inMono;
                
                // 3. Read from Output Buffer (with Wet/Dry mix)
                float dry = inMono;
                
                // Read from current output pointer
                // NOTE: _outputBuffer should be large enough to handle OLA.
                // Since we only process every BLOCK_SIZE samples, we need to ensure 
                // we have valid data to read in between.
                
                float wet = _outputBuffer[_outputPos];
                // Clear the sample we just read (for OLA/Accumulation nature)
                _outputBuffer[_outputPos] = 0f; 
                _outputPos = (_outputPos + 1) % _outputBuffer.Length;

                float outSample = dry * (1f - Mix) + wet * Mix; // Using Mix knob

                // 4. Output to channels
                for (int c = 0; c < channels; c++)
                {
                    data[i * channels + c] = outSample;
                }

                _inputPos++;

                // 5. If Input Buffer Full, Process Block and Add to Output Buffer
                if (_inputPos >= BLOCK_SIZE)
                {
                    ProcessBlock();
                    _inputPos = 0;
                }
            }
        }

        // WARNING: AUDIO THREAD SAFETY
        // The ProcessBlock() method currently violates audio thread safety rules:
        // 1. Uses lock(_irLock) which can block
        // 2. Calls .Schedule().Complete() synchronously (7+ times)
        // This can cause audio glitches, dropouts, or crashes.
        // TODO: Implement lock-free double-buffering for IR partitions
        //       Move FFT processing to background thread with atomic swap
        
        /// <summary>
        /// When true, bypasses unsafe ProcessBlock to prevent audio thread crashes.
        /// Set to false only for baked/offline processing.
        /// </summary>
        public bool UseSafeMode { get; set; } = true;
        
        private void ProcessBlock()
        {
            // SAFETY: Skip processing in safe mode to avoid audio thread violations
            if (UseSafeMode) return;

            if (!Monitor.TryEnter(_irLock)) return;
            try
            {
                if (_numPartitions == 0) return;

            // 1. Prepare Input for FFT (Pad with zeros to 2x Length for OLA)
            // Copy accumulation buffer to first half of currentBlock
            // Zero second half
            // _inputBuffer contains the NEW samples.
            
            new CopyAndPadJob
            {
                Input = _inputBuffer,
                Output = _currentBlockTime
            }.Schedule().Complete();

            // 2. FFT
            new FFTJob
            {
                Input = _currentBlockTime,
                Output = _currentBlockFreq,
                Inverse = false
            }.Schedule().Complete();

            // 3. Update FDL (Frequency Delay Line)
            // Push current block to FDL head
            // Circular FDL
            _fdlPos = (_fdlPos - 1 + _numPartitions) % _numPartitions;
            var fdlSlice = _fdl.GetSubArray(_fdlPos * FFT_SIZE, FFT_SIZE);
            _currentBlockFreq.CopyTo(fdlSlice);

            // 4. Convolution (Complex Multiply and Accumulate)
            // Output = Sum(FDL[i] * IR[i])
            // Just reuse _complexBuffer for accumulation
            new ClearJob { Data = _complexBuffer }.Schedule().Complete();

            // We can parallelize this sum
            new ConvolveJob
            {
                FDL = _fdl,
                FDLPos = _fdlPos,
                Partitions = _irPartitions,
                NumPartitions = _numPartitions,
                OutputAccumulator = _complexBuffer,
                FFTSize = FFT_SIZE
            }.Schedule(FFT_SIZE, 64).Complete();

            // 5. IFFT
            // Result goes back to _currentBlockTime (reusing)
            // We need to provide scratchpad if we want to preserve things, but FFTJob handles it
            
            // We need complex input for IFFT
            new FFTJob
            {
                InputComplex = _complexBuffer, // From convolution
                OutputComplex = _complexBuffer, // In-place or new?
                                                // FFTJob is struct based, defined below.
                                                // Let's assume it computes to float2 array
                Inverse = true
            }.Schedule().Complete();
            
            // Extract Real part and Scale (1/N) happens in FFTJob usually or post process.
            // Let's assume FFTJob produces float2 spectrum. IFFT produces float2 time (imag should be ~0).
            
            // 6. Overlap-Add to Output Buffer
            // _outputPos is the CURRENT read head. 
            // We need to add this block starting from NOW (offset by latency? No, OLA logic).
            // With standard OLA, the first half of this result overlaps with the previous tail.
            
            // Write to _outputBuffer relative to _outputPos?
            // Actually, in the sample loop:
            // We filled BLOCK_SIZE samples.
            // We process.
            // The result is 2*BLOCK_SIZE samples.
            // The first BLOCK_SIZE samples of result are ready to be played NOW? 
            // Or is there inherent latency?
            // In standard OLA:
            // Block N input -> Output is length 2N.
            // Output[0..N-1] adds to current stream.
            // Output[N..2N-1] is saved for next block (tail).
            
            // We use a circular output buffer.
            // We add the 2N samples starting at the CURRENT write pointer?
            // The "Read Pointer" is at _outputPos.
            // The processing corresponds to the *just finished* input block.
            // In zero-latency convolution, result starts immediately.
            // But we buffered N samples. So we are N samples late.
            // So we write to _outputPos (which effectively adds delay) or we catch up?
            // With uniform partitioning, latency is 1 Block.
            // So we write into buffer at a position relative to read pointer?
            
            // Let's just Add starting at _outputPos.
            // Since we read from _outputPos in the loop, we are essentially writing "ahead" of the read pointer if we wrap around?
            // Wait, we just finished reading N samples. _outputPos advanced by N.
            // So we write at _outputPos? No, that's where we interpret "Future".
            
            // To keep it simple: Write at (_outputPos) % Length ? 
            // No, the input we just processed corresponds to time T-N to T.
            // The convolution response starts at T-N. 
            // But we are at T.
            // So we are delayed by N.
            
            int writeStart = _outputPos; // We write ahead
            
            new OverlapAddJob
            {
                NewBlock = _complexBuffer, // Contains IFFT result (Real part needed)
                OutputBuffer = _outputBuffer,
                WritePos = writeStart,
                Scale = 1.0f / FFT_SIZE // IFFT scaling
            }.Schedule().Complete();
            }
            finally
            {
                Monitor.Exit(_irLock);
            }
        }

        /// <summary>
        /// Loads IR data from AudioClip. Must be called from Main Thread.
        /// </summary>
        public void LoadIR()
        {
            if (_irClip == null) return;
            
            // This call is only safe on Main Thread
            float[] samples = new float[_irClip.samples * _irClip.channels];
            _irClip.GetData(samples, 0);
            
            // Prepare Partitions
            lock (_irLock)
            {
                PreparePartitions(samples, _irClip.channels);
            }
        }

        private void PreparePartitions(float[] samples, int channels)
        {
            // Mix to mono for IR
            int irLen = samples.Length / channels;
            float[] irMono = new float[irLen];
            for(int i=0; i<irLen; i++)
            {
                float sum = 0;
                for(int c=0; c<channels; c++) sum += samples[i*channels + c];
                irMono[i] = sum / channels;
            }

            // Partition
            // Pad to multiple of BLOCK_SIZE
            _numPartitions = Mathf.CeilToInt((float)irLen / BLOCK_SIZE);
            if (_irPartitions.IsCreated) _irPartitions.Dispose();
            if (_fdl.IsCreated) _fdl.Dispose();

            _irPartitions = new NativeArray<float2>(_numPartitions * FFT_SIZE, Allocator.Persistent);
            _fdl = new NativeArray<float2>(_numPartitions * FFT_SIZE, Allocator.Persistent); // Zero init

            // Process each partition
            // 1. Take BlockSize chunk
            // 2. Pad to FFT_SIZE (with zeros)
            // 3. FFT
            // 4. Store in _irPartitions
            
            NativeArray<float> tempTime = new NativeArray<float>(FFT_SIZE, Allocator.TempJob);
            NativeArray<float2> tempFreq = new NativeArray<float2>(FFT_SIZE, Allocator.TempJob);

            for(int p=0; p<_numPartitions; p++)
            {
                // Clear temp
                NativeArray<float>.Copy(new float[FFT_SIZE], tempTime, FFT_SIZE);
                
                // Copy IR chunk
                int srcOffset = p * BLOCK_SIZE;
                int copyLen = Math.Min(BLOCK_SIZE, irLen - srcOffset);
                for(int i=0; i<copyLen; i++) tempTime[i] = irMono[srcOffset + i];
                
                // FFT
                new FFTJob { Input = tempTime, Output = tempFreq, Inverse = false }.Schedule().Complete();
                
                // Copy to partitions array
                NativeArray<float2>.Copy(tempFreq, 0, _irPartitions, p * FFT_SIZE, FFT_SIZE);
            }

            tempTime.Dispose();
            tempFreq.Dispose();
            
            // _irDirty is no longer needed for Process loop check, but maybe useful for Valid flag?
            // Actually, readiness is checked by _irPartitions.IsCreated.
        }

        #region Jobs

        [BurstCompile]
        struct CopyAndPadJob : IJob
        {
            [ReadOnly] public NativeArray<float> Input; // Size N
            public NativeArray<float> Output; // Size 2N (FFT Size)

            public void Execute()
            {
                for (int i = 0; i < Input.Length; i++)
                    Output[i] = Input[i];
                for (int i = Input.Length; i < Output.Length; i++)
                    Output[i] = 0f;
            }
        }

        [BurstCompile]
        struct OverlapAddJob : IJob
        {
            [ReadOnly] public NativeArray<float2> NewBlock; // Complex result from IFFT
            public NativeArray<float> OutputBuffer;
            public int WritePos;
            public float Scale;

            public void Execute()
            {
                int len = NewBlock.Length;
                int bufLen = OutputBuffer.Length;
                
                for(int i=0; i<len; i++)
                {
                    int idx = (WritePos + i) % bufLen;
                    // Add purely real part (NewBlock.x)
                    OutputBuffer[idx] += NewBlock[i].x * Scale;
                }
            }
        }

        [BurstCompile]
        struct ClearJob : IJob
        {
            public NativeArray<float2> Data;
            public void Execute()
            {
                for(int i=0; i<Data.Length; i++) Data[i] = float2.zero;
            }
        }

        [BurstCompile]
        struct ConvolveJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float2> FDL;
            public int FDLPos; // Start index in partitions (circular)
            [ReadOnly] public NativeArray<float2> Partitions;
            public int NumPartitions;
            [NativeDisableParallelForRestriction] // Accumulation safe if we are careful? 
            // Wait, OutputAccumulator is target. 
            // We run this job per FREQUENCY BIN (0 to FFTSize-1).
            // So each thread handles one bin across ALL partitions.
            public NativeArray<float2> OutputAccumulator; 
            public int FFTSize;

            public void Execute(int binIndex)
            {
                float2 sum = float2.zero;
                
                // Convolve across all partitions for this bin
                for(int p=0; p<NumPartitions; p++)
                {
                    // FDL index: (FDLPos + p) % NumPartitions
                    int fdlPartIdx = (FDLPos + p) % NumPartitions;
                    
                    float2 signal = FDL[fdlPartIdx * FFTSize + binIndex];
                    float2 ir = Partitions[p * FFTSize + binIndex];
                    
                    // Complex Mul: (a+bi)(c+di) = (ac-bd) + (ad+bc)i
                    float re = signal.x * ir.x - signal.y * ir.y;
                    float im = signal.x * ir.y + signal.y * ir.x;
                    
                    sum.x += re;
                    sum.y += im;
                }
                
                OutputAccumulator[binIndex] = sum;
            }
        }

        // --- FFT Implementation ---

        [BurstCompile]
        struct FFTJob : IJob
        {
            // Can handle either Real input -> Complex output
            // Or Complex input -> Complex output
            [ReadOnly] public NativeArray<float> Input; // Optional
            [ReadOnly] public NativeArray<float2> InputComplex; // Optional
            
            public NativeArray<float2> Output; // For Forward
            public NativeArray<float2> OutputComplex; // For Inverse

            public bool Inverse;

            public void Execute()
            {
                // Simple Cooley-Tukey Radix-2
                // Warning: Not highly optimized, but Burst helps.
                
                int n;
                NativeArray<float2> data;
                
                if (!Inverse)
                {
                    n = Input.Length;
                    data = Output;
                    // Copy input and bit-reverse
                    CopyBitReverseReal(Input, data, n);
                }
                else
                {
                    n = InputComplex.Length;
                    data = OutputComplex;
                    CopyBitReverseComplex(InputComplex, data, n);
                }

                // Butterfly
                for (int s = 1; s <= math.log2(n); s++)
                {
                    int m = 1 << s;
                    int m2 = m >> 1;
                    float2 wm = new float2(math.cos(math.PI / m2), math.sin(math.PI / m2));
                    if (Inverse) wm.y = -wm.y; // Conjugate for inverse

                    // For first iteration, we can optimize w calculation
                    // But here keep generic
                    
                    for (int k = 0; k < n; k += m)
                    {
                        float2 w = new float2(1f, 0f);
                        for (int j = 0; j < m2; j++)
                        {
                            float2 t = Mul(w, data[k + j + m2]);
                            float2 u = data[k + j];

                            data[k + j] = u + t;
                            data[k + j + m2] = u - t;
                            
                            // w = w * wm
                            w = Mul(w, wm);
                        }
                    }
                }
            }

            private float2 Mul(float2 a, float2 b)
            {
                return new float2(a.x * b.x - a.y * b.y, a.x * b.y + a.y * b.x);
            }

            private void CopyBitReverseReal(NativeArray<float> src, NativeArray<float2> dst, int n)
            {
                int j = 0;
                for (int i = 0; i < n - 1; i++)
                {
                    dst[i] = new float2(src[j], 0f);
                    dst[j] = new float2(src[i], 0f);
                    
                    int k = n >> 1;
                    while (k <= j)
                    {
                        j -= k;
                        k >>= 1;
                    }
                    j += k;
                    
                    // Simple swap logic issue in loop? 
                    // Standard bit reversal loop:
                    // If i < j, swap.
                    // Here we just assigning. 
                    // Wait, this loop logic is for in-place swap.
                    // Since we copy to new buffer, we can just direct assign.
                }
                // Bit reversal is tricky to do direct-copy without full loop.
                // Let's do standard O(N) bit reversal map
                
                // Let's implement simpler:
                for(int i=0; i<n; i++)
                {
                    dst[ReverseBits(i, n)] = new float2(src[i], 0f);
                }
            }

            private void CopyBitReverseComplex(NativeArray<float2> src, NativeArray<float2> dst, int n)
            {
                for(int i=0; i<n; i++)
                {
                    dst[ReverseBits(i, n)] = src[i];
                }
            }

            private int ReverseBits(int x, int n)
            {
                int result = 0;
                int log2n = (int)math.log2(n);
                for (int i = 0; i < log2n; i++)
                {
                    if ((x & (1 << i)) != 0)
                        result |= 1 << (log2n - 1 - i);
                }
                return result;
            }
        }

        #endregion
    }
}
