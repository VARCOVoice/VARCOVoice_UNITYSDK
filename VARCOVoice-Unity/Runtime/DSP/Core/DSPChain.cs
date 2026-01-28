using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

#pragma warning disable CS0420 // A reference to a volatile field will not be treated as volatile

namespace VARCOVoice.DSP
{
    /// <summary>
    /// DSP processing chain - manages multiple audio effects
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(AudioSource))]
    [AddComponentMenu("VARCO Voice/DSP Chain")]
    public class DSPChain : MonoBehaviour
    {
        #region Inspector Fields
        
        [Header("Chain Settings")]
        [SerializeField] private bool chainEnabled = true;
        [SerializeField] private bool bypassWhenInactive = true;
        
        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = false;
        
        #endregion
        
        #region Private Fields

        private readonly List<IDSPEffect> _effects = new List<IDSPEffect>();
        private volatile IDSPEffect[] _effectsSnapshot = Array.Empty<IDSPEffect>();
        [SerializeField, HideInInspector] private ParametricEQ16 _masterEQ;
        private int _sampleRate;
        private int _channels;
        
        // Visualization Buffer (Ring Buffer)
        // Lock-free: reads may tear slightly but only affect visualization.
        private float[] _visBuffer = new float[16384];
        private float[] _preEQBuffer = new float[16384]; // Pre-EQ buffer for spectrum overlay
        private int _visHead = 0;
        private int _preEQHead = 0;
        private readonly object _lock = new object();
        
        // Stereo Level Meters (updated on audio thread)
        private volatile float _stereoLeftLevel = 0f;
        private volatile float _stereoRightLevel = 0f;
        private volatile float _stereoPeakLeft = 0f;
        private volatile float _stereoPeakRight = 0f;

        private sealed class CrossfadeState
        {
            public IDSPEffect[] From;
            public IDSPEffect[] To;
            public int RemainingFrames;
            public int TotalFrames;
            public float Progress;
            public float Step;
        }

        private volatile CrossfadeState _crossfadeState;
        private float[] _crossfadeBufferA;
        private float[] _crossfadeBufferB;
        private volatile int _pendingCrossfadeLength;
        private readonly ConcurrentQueue<IDSPEffect[]> _disposeQueue = new ConcurrentQueue<IDSPEffect[]>();

        [Header("Safety Limiter")]
        [SerializeField] private bool enableSafetyLimiter = true;
        [SerializeField] private float limiterCeiling = 0.98f;
        [SerializeField] private float limiterLookaheadMs = 3f;
        [SerializeField] private float limiterReleaseMs = 50f;

        private float[] _limiterDelayBuffer;
        private int _limiterWritePos;
        private float _limiterGain = 1f;
        private int _limiterDelaySamples;
        private int _limiterChannels;
        private float _limiterReleaseCoef;
        private volatile bool _pendingLimiterResize;
        private volatile int _pendingLimiterBufferSamples;
        private volatile int _pendingLimiterChannels;
        private volatile int _pendingLimiterSampleRate;

        [Header("Preset Crossfade")]
        [SerializeField] private bool enablePresetCrossfade = true;
        [SerializeField] private float presetCrossfadeMs = 30f;
        [SerializeField] private bool enablePresetMorph = true;

        private long _lastOverloadTick = 0;
        private long _lastClippingTick = 0;
        
        #endregion
        
        #region Properties
        
        public bool Enabled
        {
            get => chainEnabled;
            set => chainEnabled = value;
        }
        
        public IReadOnlyList<IDSPEffect> Effects
        {
            get
            {
                var snapshot = _effectsSnapshot;
                return new List<IDSPEffect>(snapshot);
            }
        }

        public ParametricEQ16 MasterEQ
        {
            get
            {
                EnsureMasterEQ();
                return _masterEQ;
            }
        }
        public int EffectCount
        {
            get
            {
                return _effectsSnapshot.Length;
            }
        }
        
        #endregion
        
        #region Unity Lifecycle
        
        private volatile bool _isPlaying = false;
        private volatile bool _allowProcessing = false;
        private AudioSource _cachedSource;
        
        // Smart tail detection: continue processing while output signal is above threshold
        private volatile float _outputLevel = 0f;
        private const float SILENCE_THRESHOLD = 0.0001f; // About -80dB
        private const float TAIL_GRACE_PERIOD = 0.1f; // Short grace period after playback stops
        private float _silenceTimer = 0f;

        private void Awake()
        {
            _cachedSource = GetComponent<AudioSource>();
            _sampleRate = AudioSettings.outputSampleRate;
            AudioSettings.GetDSPBufferSize(out int bufferSize, out _);
            _effectsSnapshot = Array.Empty<IDSPEffect>();

            // Get channel count from AudioSettings
            var speakerMode = AudioSettings.GetConfiguration().speakerMode;
            _channels = GetChannelsFromSpeakerMode(speakerMode);

            AllocateCrossfadeBuffers(bufferSize * _channels);
            AllocateLimiterBuffers(_channels, _sampleRate);
            EnsureMasterEQ();
        }

        private void OnEnable()
        {
            EnsureMasterEQ();
            ResetAllEffects();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update += OnEditorUpdate;
#endif
        }

        private void OnDisable()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update -= OnEditorUpdate;
#endif
            if (bypassWhenInactive)
            {
                ResetAllEffects();
            }
        }

        private void Update()
        {
            // Runtime update
            if (Application.isPlaying) SyncPlayState();
            DrainDisposeQueue();
            ApplyPendingAudioBufferResizes();
        }

        private void OnEditorUpdate()
        {
            // Editor update
            if (!Application.isPlaying) SyncPlayState();
        }

        private void SyncPlayState()
        {
            if (_cachedSource != null)
            {
                bool wasPlaying = _isPlaying;
                _isPlaying = _cachedSource.isPlaying;
                
                // If just stopped playing, start grace period
                if (wasPlaying && !_isPlaying)
                {
                    _silenceTimer = TAIL_GRACE_PERIOD;
                }
                
                // If playing, processing is always allowed
                if (_isPlaying)
                {
                    _allowProcessing = true;
                    _silenceTimer = 0f;
                }
                else
                {
                    // Not playing: check if there's still signal (tail) or in grace period
                    float deltaTime = Application.isPlaying ? Time.deltaTime : 0.016f;
                    if (_silenceTimer > 0f)
                    {
                        _silenceTimer -= deltaTime;
                    }
                    
                    // Allow processing if:
                    // 1. We're in grace period, OR
                    // 2. Output signal is above silence threshold (tail is still audible)
                    _allowProcessing = _silenceTimer > 0f || _outputLevel > SILENCE_THRESHOLD;
                }
            }
        }

        private void OnDestroy()
        {
            if (_masterEQ is IDisposable disposable) disposable.Dispose();
            _masterEQ = null;
            ClearEffects();
        }
        
        #endregion
        
        #region DSP Processing
        
        /// <summary>
        /// Unity audio filter callback - processes audio in real-time
        /// Called on audio thread, NOT main thread!
        /// </summary>
        private void OnAudioFilterRead(float[] data, int channels)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            // Gate processing: If not playing and no tail time remaining, silence output
            // This prevents "Infinite Noise" while allowing reverb/delay tails to decay naturally
            if (!_allowProcessing)
            {
                Array.Clear(data, 0, data.Length);
                return;
            }

            if (!chainEnabled) return;
            
            // Check crossfade state
            var xfade = _crossfadeState;
            
            int processingRate = _sampleRate > 0 ? _sampleRate : 44100;

            if (xfade != null && xfade.RemainingFrames > 0)
            {
                ProcessCrossfade(data, channels, processingRate, xfade);
            }
            else
            {
                var effects = _effectsSnapshot;
                if (effects.Length > 0)
                {
                    ProcessEffects(effects, data, channels, processingRate);
                }
            }

            if (_masterEQ != null && _masterEQ.Enabled)
            {
                // Store Pre-EQ data for spectrum overlay
                StorePreEQData(data, channels);
                
                try
                {
                    _masterEQ.Process(data, channels, processingRate);
                }
                catch (Exception)
                {
#if VARCO_DEBUG
                    Debug.LogError("[DSPChain] Master EQ error");
#endif
                }

                if (SanitizeBuffer(data))
                {
                    _masterEQ.Reset();
                }
            }

            if (enableSafetyLimiter)
            {
                ApplySafetyLimiter(data, channels, processingRate);
            }

            SanitizeBuffer(data);
            
            // Measure output level for smart tail detection
            // This determines when reverb/delay tails have decayed below threshold
            float maxLevel = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float abs = data[i] > 0 ? data[i] : -data[i];
                if (abs > maxLevel) maxLevel = abs;
            }
            _outputLevel = maxLevel;
            
            StoreVisualizationData(data, channels);

            sw.Stop();
            
            // Performance & Clipping Monitor
            double elapsedMs = sw.Elapsed.TotalMilliseconds;
            double bufferDurationMs = (data.Length / (double)channels / processingRate) * 1000.0;
            long now = System.DateTime.Now.Ticks / 10000; // current ms

            // Overload Check (> 80% usage)
            if (elapsedMs > bufferDurationMs * 0.8)
            {
                if (now - _lastOverloadTick > 3000) // 3s cooldown
                {
                    _lastOverloadTick = now;
                    Debug.LogWarning($"[DSPChain] CPU Overload! Processing took {elapsedMs:F2}ms (Buffer: {bufferDurationMs:F2}ms)");
                }
            }
            
            // Clipping Check (> 0dB)
            if (_outputLevel > 1.0f)
            {
                if (now - _lastClippingTick > 2000) // 2s cooldown
                {
                    _lastClippingTick = now;
                    float db = 20f * Mathf.Log10(_outputLevel);
                    Debug.LogWarning($"[DSPChain] Audio Clipping Detected! Peak: {db:F1}dB");
                }
            }
        }

        private static bool SanitizeBuffer(float[] data)
        {
            bool hadInvalid = false;
            for (int i = 0; i < data.Length; i++)
            {
                float value = data[i];
                // Check for NaN and Infinity
                if (float.IsNaN(value) || float.IsInfinity(value))
                {
                    data[i] = 0f;
                    hadInvalid = true;
                }
                // Check for denormals - these cause massive CPU spikes
                else if (value != 0f && MathF.Abs(value) < DSPConstants.DENORMAL_THRESHOLD)
                {
                    data[i] = 0f;
                }
            }
            return hadInvalid;
        }

        private void ProcessEffects(IDSPEffect[] effects, float[] buffer, int channels, int sampleRate)
        {
            for (int i = 0; i < effects.Length; i++)
            {
                var effect = effects[i];
                if (effect == null || !effect.Enabled) continue;

                try
                {
                    effect.Process(buffer, channels, sampleRate);
                }
                catch (Exception)
                {
#if VARCO_DEBUG
                    Debug.LogError("[DSPChain] Effect error");
#endif
                }

                if (SanitizeBuffer(buffer))
                {
                    effect.Reset();
                }
            }
        }

        private void ProcessCrossfade(float[] data, int channels, int sampleRate, CrossfadeState state)
        {
            if (state.From == null || state.To == null)
            {
                Volatile.Write(ref _crossfadeState, null);
                return;
            }

            if (!EnsureCrossfadeBuffers(data.Length))
            {
                Volatile.Write(ref _effectsSnapshot, state.To);
                _disposeQueue.Enqueue(state.From);
                Volatile.Write(ref _crossfadeState, null);
                return;
            }
            Array.Copy(data, _crossfadeBufferA, data.Length);
            Array.Copy(data, _crossfadeBufferB, data.Length);

            if (state.From.Length > 0)
            {
                ProcessEffects(state.From, _crossfadeBufferA, channels, sampleRate);
            }

            if (state.To.Length > 0)
            {
                ProcessEffects(state.To, _crossfadeBufferB, channels, sampleRate);
            }

            int frames = data.Length / channels;
            float progress = state.Progress;
            float step = state.Step;

            for (int frame = 0; frame < frames; frame++)
            {
                float t = progress;
                if (t < 0f) t = 0f;
                if (t > 1f) t = 1f;
                float inv = 1f - t;
                int baseIndex = frame * channels;

                for (int ch = 0; ch < channels; ch++)
                {
                    int idx = baseIndex + ch;
                    data[idx] = _crossfadeBufferA[idx] * inv + _crossfadeBufferB[idx] * t;
                }

                progress += step;
            }

            state.Progress = progress;
            state.RemainingFrames -= frames;

            if (state.RemainingFrames <= 0 || state.Progress >= 1f)
            {
                Volatile.Write(ref _effectsSnapshot, state.To);
                _disposeQueue.Enqueue(state.From);
                Volatile.Write(ref _crossfadeState, null);
            }
        }

        private bool EnsureCrossfadeBuffers(int length)
        {
            if (_crossfadeBufferA == null || _crossfadeBufferB == null
                || _crossfadeBufferA.Length < length || _crossfadeBufferB.Length < length)
            {
                int requested = Math.Max(length, Volatile.Read(ref _pendingCrossfadeLength));
                Volatile.Write(ref _pendingCrossfadeLength, requested);
                return false;
            }
            return true;
        }

        private void ApplySafetyLimiter(float[] data, int channels, int sampleRate)
        {
            if (limiterCeiling <= 0f) return;
            if (!EnsureLimiter(channels, sampleRate)) return;

            int frames = data.Length / channels;
            int bufferSize = _limiterDelayBuffer.Length;
            int readBase = _limiterWritePos - _limiterDelaySamples * channels;
            if (readBase < 0) readBase += bufferSize;

            for (int frame = 0; frame < frames; frame++)
            {
                int writeIndex = _limiterWritePos;
                int readIndex = readBase;

                float peak = 0f;
                for (int ch = 0; ch < channels; ch++)
                {
                    float sample = data[frame * channels + ch];
                    float absVal = Mathf.Abs(sample);
                    if (absVal > peak) peak = absVal;

                    _limiterDelayBuffer[writeIndex + ch] = sample;
                }

                float targetGain = peak > limiterCeiling ? limiterCeiling / peak : 1f;
                if (targetGain < _limiterGain)
                {
                    _limiterGain = targetGain;
                }
                else
                {
                    _limiterGain = _limiterReleaseCoef * _limiterGain + (1f - _limiterReleaseCoef) * targetGain;
                }

                for (int ch = 0; ch < channels; ch++)
                {
                    data[frame * channels + ch] = _limiterDelayBuffer[readIndex + ch] * _limiterGain;
                }

                _limiterWritePos += channels;
                if (_limiterWritePos >= bufferSize) _limiterWritePos = 0;

                readBase += channels;
                if (readBase >= bufferSize) readBase = 0;
            }
        }

        private bool EnsureLimiter(int channels, int sampleRate)
        {
            int lookaheadSamples = Mathf.Max(1, (int)(limiterLookaheadMs * sampleRate / 1000f));
            int bufferSamples = lookaheadSamples * channels;
            if (_limiterDelayBuffer == null || _limiterDelayBuffer.Length != bufferSamples || _limiterChannels != channels || _limiterDelaySamples != lookaheadSamples)
            {
                Volatile.Write(ref _pendingLimiterBufferSamples, bufferSamples);
                Volatile.Write(ref _pendingLimiterChannels, channels);
                Volatile.Write(ref _pendingLimiterSampleRate, sampleRate);
                _pendingLimiterResize = true;
                return false;
            }

            float releaseSeconds = Mathf.Max(0.001f, limiterReleaseMs * 0.001f);
            _limiterReleaseCoef = Mathf.Exp(-1f / (releaseSeconds * sampleRate));
            return true;
        }

        private void ApplyPendingAudioBufferResizes()
        {
            int pendingCrossfadeLength = Volatile.Read(ref _pendingCrossfadeLength);
            if (pendingCrossfadeLength > 0)
            {
                AllocateCrossfadeBuffers(pendingCrossfadeLength);
                Volatile.Write(ref _pendingCrossfadeLength, 0);
            }

            if (_pendingLimiterResize)
            {
                int bufferSamples = Volatile.Read(ref _pendingLimiterBufferSamples);
                int channels = Volatile.Read(ref _pendingLimiterChannels);
                int sampleRate = Volatile.Read(ref _pendingLimiterSampleRate);
                if (bufferSamples > 0 && channels > 0 && sampleRate > 0)
                {
                    AllocateLimiterBuffers(channels, sampleRate);
                }
                _pendingLimiterResize = false;
            }
        }

        private void AllocateCrossfadeBuffers(int length)
        {
            if (length <= 0) return;
            if (_crossfadeBufferA == null || _crossfadeBufferA.Length < length)
            {
                _crossfadeBufferA = new float[length];
                _crossfadeBufferB = new float[length];
            }
        }

        private void AllocateLimiterBuffers(int channels, int sampleRate)
        {
            if (channels <= 0 || sampleRate <= 0) return;
            int lookaheadSamples = Mathf.Max(1, (int)(limiterLookaheadMs * sampleRate / 1000f));
            int bufferSamples = lookaheadSamples * channels;
            if (_limiterDelayBuffer == null || _limiterDelayBuffer.Length != bufferSamples || _limiterChannels != channels || _limiterDelaySamples != lookaheadSamples)
            {
                _limiterDelayBuffer = new float[bufferSamples];
                _limiterWritePos = 0;
                _limiterGain = 1f;
                _limiterDelaySamples = lookaheadSamples;
                _limiterChannels = channels;
            }

            float releaseSeconds = Mathf.Max(0.001f, limiterReleaseMs * 0.001f);
            _limiterReleaseCoef = Mathf.Exp(-1f / (releaseSeconds * sampleRate));
        }

        private void StoreVisualizationData(float[] data, int channels)
        {
            // Mono downmix for spectrum visualization
            int head = _visHead;
            int length = _visBuffer.Length;
            
            // Calculate stereo levels
            float leftSum = 0f, rightSum = 0f;
            int frameCount = data.Length / channels;
            
            for (int i = 0; i < data.Length; i += channels)
            {
                // Mono mix for spectrum
                float sum = 0f;
                for (int c = 0; c < channels; c++) sum += data[i + c];
                float val = sum / channels;

                if (float.IsNaN(val) || float.IsInfinity(val)) val = 0f;

                _visBuffer[head] = val;
                head++;
                if (head >= length) head = 0;
                
                // Stereo level calculation
                if (channels >= 2)
                {
                    float left = Mathf.Abs(data[i]);
                    float right = Mathf.Abs(data[i + 1]);
                    leftSum += left;
                    rightSum += right;
                    
                    // Peak detection
                    if (left > _stereoPeakLeft) _stereoPeakLeft = left;
                    if (right > _stereoPeakRight) _stereoPeakRight = right;
                }
                else
                {
                    // Mono: same level for both
                    float mono = Mathf.Abs(data[i]);
                    leftSum += mono;
                    rightSum += mono;
                    if (mono > _stereoPeakLeft) _stereoPeakLeft = mono;
                    if (mono > _stereoPeakRight) _stereoPeakRight = mono;
                }
            }
            
            // Store RMS levels
            if (frameCount > 0)
            {
                _stereoLeftLevel = leftSum / frameCount;
                _stereoRightLevel = rightSum / frameCount;
            }
            
            // Peak decay (0.995 per buffer, roughly 100ms decay at 44.1kHz)
            _stereoPeakLeft *= 0.995f;
            _stereoPeakRight *= 0.995f;
            
            Volatile.Write(ref _visHead, head);
        }

        /// <summary>
        /// Retrieve the latest samples for visualization (Main Thread)
        /// </summary>
        /// <param name="output">Target buffer to fill</param>
        public void GetLatestSamples(float[] output)
        {
            if (output == null) return;

            int len = output.Length;
            if (len > _visBuffer.Length) len = _visBuffer.Length; // Clamp

            int head = Volatile.Read(ref _visHead);
            int startIdx = head - len;
            if (startIdx < 0) startIdx += _visBuffer.Length;

            for (int i = 0; i < len; i++)
            {
                output[i] = _visBuffer[(startIdx + i) % _visBuffer.Length];
            }
        }
        
        /// <summary>
        /// Get the current stereo levels for visualization (Main Thread safe)
        /// </summary>
        /// <param name="leftLevel">RMS level for left channel (0-1 linear scale)</param>
        /// <param name="rightLevel">RMS level for right channel (0-1 linear scale)</param>
        /// <param name="peakLeft">Peak level for left channel (0-1 linear scale)</param>
        /// <param name="peakRight">Peak level for right channel (0-1 linear scale)</param>
        public void GetStereoLevels(out float leftLevel, out float rightLevel, out float peakLeft, out float peakRight)
        {
            leftLevel = _stereoLeftLevel;
            rightLevel = _stereoRightLevel;
            peakLeft = _stereoPeakLeft;
            peakRight = _stereoPeakRight;
        }
        
        /// <summary>
        /// Store Pre-EQ audio data for spectrum overlay visualization
        /// Called on audio thread before EQ processing
        /// </summary>
        private void StorePreEQData(float[] data, int channels)
        {
            int head = _preEQHead;
            int length = _preEQBuffer.Length;
            for (int i = 0; i < data.Length; i += channels)
            {
                float sum = 0f;
                for (int c = 0; c < channels; c++) sum += data[i + c];
                float val = sum / channels;

                if (float.IsNaN(val) || float.IsInfinity(val)) val = 0f;

                _preEQBuffer[head] = val;
                head++;
                if (head >= length) head = 0;
            }
            Volatile.Write(ref _preEQHead, head);
        }
        
        /// <summary>
        /// Retrieve Pre-EQ samples for spectrum overlay visualization (Main Thread)
        /// </summary>
        /// <param name="output">Target buffer to fill</param>
        public void GetPreEQSamples(float[] output)
        {
            if (output == null) return;

            int len = output.Length;
            if (len > _preEQBuffer.Length) len = _preEQBuffer.Length;

            int head = Volatile.Read(ref _preEQHead);
            int startIdx = head - len;
            if (startIdx < 0) startIdx += _preEQBuffer.Length;

            for (int i = 0; i < len; i++)
            {
                output[i] = _preEQBuffer[(startIdx + i) % _preEQBuffer.Length];
            }
        }
        
        #endregion

        #region Effect Management

        private void DrainDisposeQueue()
        {
            while (_disposeQueue.TryDequeue(out var effects))
            {
                DisposeEffects(effects);
            }
        }

        private static void DisposeEffects(IDSPEffect[] effects)
        {
            if (effects == null) return;
            for (int i = 0; i < effects.Length; i++)
            {
                var effect = effects[i];
                if (effect == null) continue;
                if (effect is IDisposable disposable) disposable.Dispose();
                effect.Reset();
            }
        }

        private void UpdateSnapshotLocked()
        {
            if (_effects.Count == 0)
            {
                _effectsSnapshot = Array.Empty<IDSPEffect>();
                return;
            }

            var list = new List<IDSPEffect>(_effects.Count);
            for (int i = 0; i < _effects.Count; i++)
            {
                if (_effects[i] != null) list.Add(_effects[i]);
            }
            _effectsSnapshot = list.ToArray();
        }

        /// <summary>
        /// Add effect to the chain
        /// </summary>
        public T AddEffect<T>() where T : IDSPEffect, new()
        {
            var effect = new T();
            PrimeEffect(effect);
            lock (_lock)
            {
                _effects.Add(effect);
                UpdateSnapshotLocked();
            }
            return effect;
        }
        
        /// <summary>
        /// Add existing effect instance to the chain
        /// </summary>
        public void AddEffect(IDSPEffect effect)
        {
            if (effect != null)
            {
                PrimeEffect(effect);
                lock (_lock)
                {
                    if (!_effects.Contains(effect))
                    {
                        _effects.Add(effect);
                        UpdateSnapshotLocked();
                    }
                }
            }
        }
        
        /// <summary>
        /// Insert effect at specific position
        /// </summary>
        public void InsertEffect(int index, IDSPEffect effect)
        {
            if (effect != null)
            {
                PrimeEffect(effect);
                lock (_lock)
                {
                    _effects.Insert(Mathf.Clamp(index, 0, _effects.Count), effect);
                    UpdateSnapshotLocked();
                }
            }
        }
        
        /// <summary>
        /// Remove effect from chain
        /// </summary>
        public bool RemoveEffect(IDSPEffect effect)
        {
            lock (_lock)
            {
                if (_effects.Remove(effect))
                {
                    if (effect is IDisposable disposable) disposable.Dispose();
                    UpdateSnapshotLocked();
                    return true;
                }
                return false;
            }
        }
        
        /// <summary>
        /// Remove effect at index
        /// </summary>
        public void RemoveEffectAt(int index)
        {
            lock (_lock)
            {
                if (index >= 0 && index < _effects.Count)
                {
                    var effect = _effects[index];
                    _effects.RemoveAt(index);
                    if (effect is IDisposable disposable) disposable.Dispose();
                    UpdateSnapshotLocked();
                }
            }
        }
        
        /// <summary>
        /// Set the entire effects list (used for syncing with node graph)
        /// </summary>
        public void SetEffects(List<IDSPEffect> newEffects)
        {
            lock (_lock)
            {
                // Dispose effects that are NOT in the new list (removed ones)
                if (newEffects != null)
                {
                    foreach (var oldEffect in _effects)
                    {
                        if (!newEffects.Contains(oldEffect))
                        {
                            if (oldEffect is IDisposable disposable) disposable.Dispose();
                        }
                    }
                }
                else
                {
                    foreach (var oldEffect in _effects)
                    {
                        if (oldEffect is IDisposable disposable) disposable.Dispose();
                    }
                }

                _effects.Clear();
                if (newEffects != null)
                {
                    foreach (var effect in newEffects)
                    {
                        PrimeEffect(effect);
                        if (effect != null) _effects.Add(effect);
                    }
                }
                UpdateSnapshotLocked();
            }
        }

        /// <summary>
        /// Apply new effects with morphing when possible, otherwise crossfade.
        /// </summary>
        public void ApplyPresetEffects(List<IDSPEffect> newEffects)
        {
            if (enablePresetMorph && TryMorphEffects(newEffects, presetCrossfadeMs))
            {
                return;
            }

            ReplaceEffectsWithCrossfade(newEffects, presetCrossfadeMs);
        }

        /// <summary>
        /// Replace effects with optional crossfade (preset transitions)
        /// </summary>
        public void ReplaceEffectsWithCrossfade(List<IDSPEffect> newEffects)
        {
            ReplaceEffectsWithCrossfade(newEffects, presetCrossfadeMs);
        }

        /// <summary>
        /// Replace effects with optional crossfade (preset transitions)
        /// </summary>
        public void ReplaceEffectsWithCrossfade(List<IDSPEffect> newEffects, float crossfadeMs)
        {
            var oldSnapshot = _effectsSnapshot;

            lock (_lock)
            {
                _effects.Clear();
                if (newEffects != null)
                {
                    for (int i = 0; i < newEffects.Count; i++)
                    {
                        PrimeEffect(newEffects[i]);
                        if (newEffects[i] != null) _effects.Add(newEffects[i]);
                    }
                }
                UpdateSnapshotLocked();
            }

            var newSnapshot = _effectsSnapshot;

            if (!enablePresetCrossfade || crossfadeMs <= 0f || oldSnapshot.Length == 0)
            {
                if (oldSnapshot != newSnapshot)
                {
                    _disposeQueue.Enqueue(oldSnapshot);
                }
                return;
            }

            int sampleRate = _sampleRate > 0 ? _sampleRate : 44100;
            int totalFrames = Mathf.Max(1, (int)(crossfadeMs * sampleRate / 1000f));
            var state = new CrossfadeState
            {
                From = oldSnapshot,
                To = newSnapshot,
                TotalFrames = totalFrames,
                RemainingFrames = totalFrames,
                Progress = 0f,
                Step = 1f / totalFrames
            };
            Volatile.Write(ref _crossfadeState, state);
        }

        private bool TryMorphEffects(List<IDSPEffect> newEffects, float morphMs)
        {
            if (newEffects == null) return false;
            var current = _effectsSnapshot;
            if (current.Length != newEffects.Count || current.Length == 0) return false;

            int sampleRate = _sampleRate > 0 ? _sampleRate : 44100;
            int morphSamples = Mathf.Max(1, (int)(morphMs * sampleRate / 1000f));

            for (int i = 0; i < current.Length; i++)
            {
                if (current[i] == null || newEffects[i] == null) return false;
                if (current[i].GetType() != newEffects[i].GetType()) return false;
                if (current[i] is not IMorphableEffect) return false;
            }

            for (int i = 0; i < current.Length; i++)
            {
                var morphable = (IMorphableEffect)current[i];
                morphable.SetMorphTarget(newEffects[i], morphSamples);
            }

            DisposeEffects(newEffects.ToArray());
            return true;
        }
        
        /// <summary>
        /// Get effect by type
        /// </summary>
        public T GetEffect<T>() where T : class, IDSPEffect
        {
            lock (_lock)
            {
                foreach (var effect in _effects)
                {
                    if (effect is T typed)
                        return typed;
                }
            }
            return null;
        }
        
        /// <summary>
        /// Get or create effect by type
        /// </summary>
        public T GetOrAddEffect<T>() where T : class, IDSPEffect, new()
        {
            lock (_lock)
            {
                var existing = GetEffect<T>();
                if (existing != null) return existing;
            }
            return AddEffect<T>();
        }
        
        /// <summary>
        /// Clear all effects
        /// </summary>
        public void ClearEffects()
        {
            lock (_lock)
            {
                foreach (var effect in _effects)
                {
                    if (effect is IDisposable disposable) disposable.Dispose();
                    effect.Reset(); // Still call reset? Or just nuke?
                    // Usually Dispose cleans up everything.
                }
                _effects.Clear();
                UpdateSnapshotLocked();
            }
        }
        
        /// <summary>
        /// Move effect to new position
        /// </summary>
        public void MoveEffect(int fromIndex, int toIndex)
        {
            lock (_lock)
            {
                if (fromIndex < 0 || fromIndex >= _effects.Count) return;
                if (toIndex < 0 || toIndex >= _effects.Count) return;

                var effect = _effects[fromIndex];
                _effects.RemoveAt(fromIndex);
                _effects.Insert(toIndex, effect);
                UpdateSnapshotLocked();
            }
        }
        
        /// <summary>
        /// Reset all effects (clear internal buffers)
        /// </summary>
        public void ResetAllEffects()
        {
            lock (_lock)
            {
                foreach (var effect in _effects)
                {
                    effect.Reset();
                }
            }
        }

        private void PrimeEffect(IDSPEffect effect)
        {
            if (effect is ParametricEQ16 eq)
            {
                int sampleRate = _sampleRate > 0 ? _sampleRate : AudioSettings.outputSampleRate;
                int channels = _channels > 0 ? _channels : GetChannelsFromSpeakerMode(AudioSettings.GetConfiguration().speakerMode);
                eq.Prime(channels, sampleRate);
            }
        }

        private void EnsureMasterEQ()
        {
            if (_masterEQ == null)
            {
                _masterEQ = new ParametricEQ16();
            }
            _masterEQ.Enabled = true;
            PrimeEffect(_masterEQ);
        }

        private static int GetChannelsFromSpeakerMode(AudioSpeakerMode speakerMode)
        {
            return speakerMode switch
            {
                AudioSpeakerMode.Mono => 1,
                AudioSpeakerMode.Stereo => 2,
                AudioSpeakerMode.Quad => 4,
                AudioSpeakerMode.Surround => 5,
                AudioSpeakerMode.Mode5point1 => 6,
                AudioSpeakerMode.Mode7point1 => 8,
                _ => 2
            };
        }

        #endregion
        
        /// <summary>
        /// Apply Robot Voice preset
        /// </summary>
        public void ApplyRobotVoice()
        {
            var effects = new List<IDSPEffect>();
            var pitch = new PitchShift { Pitch = -4 };
            effects.Add(pitch);
            ApplyPresetEffects(effects);
        }

        /// <summary>
        /// Apply Radio Voice preset
        /// </summary>
        public void ApplyRadioVoice()
        {
            var effects = new List<IDSPEffect>();
            var eq = new ParametricEQ16();
            eq.ApplyPreset("Radio Voice");
            effects.Add(eq);

            // LowPass replaced by extra high frequency cut on EQ
            var lowpassEq = new ParametricEQ16();
            lowpassEq.SetBand(11, 4000f, -12f, 1f, EQFilterType.HighShelf); 
            lowpassEq.SetBand(13, 10000f, -24f, 1f, EQFilterType.HighShelf);
            effects.Add(lowpassEq);

            ApplyPresetEffects(effects);
        }

        /// <summary>
        /// Apply Cave Voice preset
        /// </summary>
        public void ApplyCaveVoice()
        {
            var effects = new List<IDSPEffect>();
            var reverb = new FDNReverb();
            reverb.ApplyPreset("concert hall"); // Large space
            reverb.Mix = 0.5f;
            effects.Add(reverb);

            var delay = new UnifiedDelay
            {
                Mode = UnifiedDelay.DelayMode.Standard,
                Time = 300f,
                Feedback = 0.4f,
                Mix = 0.2f
            };
            effects.Add(delay);

            ApplyPresetEffects(effects);
        }

        /// <summary>
        /// Apply Underwater Voice preset
        /// </summary>
        public void ApplyUnderwaterVoice()
        {
            var effects = new List<IDSPEffect>();
            var lowpassEq = new ParametricEQ16();
            // Underwater effect: aggressive high frequency reduction
            lowpassEq.SetBand(6, 630f, -6f, 1f, EQFilterType.HighShelf);
            lowpassEq.SetBand(8, 1000f, -12f, 1f, EQFilterType.HighShelf);
            lowpassEq.SetBand(10, 2500f, -24f, 1f, EQFilterType.HighShelf);
            effects.Add(lowpassEq);

            var reverb = new FDNReverb();
            reverb.ApplyPreset("plate");
            reverb.Mix = 0.3f;
            effects.Add(reverb);

            var pitch = new PitchShift { Pitch = -2 };
            effects.Add(pitch);

            ApplyPresetEffects(effects);
        }

        /// <summary>
        /// Apply Ghost Voice preset
        /// </summary>
        public void ApplyGhostVoice()
        {
            var effects = new List<IDSPEffect>();
            var pitch = new PitchShift { Pitch = -12 };
            effects.Add(pitch);

            var reverb = new FDNReverb();
            reverb.ApplyPreset("cathedral");
            reverb.Mix = 0.6f;
            reverb.DecayTime = 4.0f; // Long tail
            effects.Add(reverb);

            ApplyPresetEffects(effects);
        }
        
        #region Debug
        
        private void OnGUI()
        {
            if (!showDebugInfo) return;
            
            GUILayout.BeginArea(new Rect(10, 10, 300, 400));
            GUILayout.Label($"DSP Chain - {_effects.Count} effects");
            
            foreach (var effect in _effects)
            {
                var status = effect.Enabled ? "[ON]" : "[OFF]";
                GUILayout.Label($"  {status} {effect.Name}");
            }
            
            GUILayout.EndArea();
        }
        
        #endregion
    }
}
