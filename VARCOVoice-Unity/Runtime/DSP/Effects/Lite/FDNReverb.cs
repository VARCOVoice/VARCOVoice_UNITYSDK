using System;
using UnityEngine;

namespace VARCOVoice.DSP
{
    /// <summary>
    /// Freeverb-based reverb (legacy FDNReverb slot).
    /// Keeps existing parameters and presets for compatibility.
    /// </summary>
    [Serializable]
    public class FDNReverb : DSPEffectBase
    {
        public override string Name => "Reverb";

        #region Parameters

        [field: Range(0f, 100f)]
        public float PreDelay { get; set; } = 20f;

        [field: Range(10f, 100f)]
        public float RoomSize { get; set; } = 30f;

        [field: Range(0.1f, 10f)]
        public float DecayTime { get; set; } = 2.0f;

        [field: Range(0f, 1f)]
        public float Damping { get; set; } = 0.5f;

        [field: Range(0f, 1f)]
        public float Diffusion { get; set; } = 0.7f;

        [field: Range(0f, 2f)]
        public float ModulationRate { get; set; } = 0.5f;

        [field: Range(0f, 1f)]
        public float ModulationDepth { get; set; } = 0.3f;

        [field: Range(0.5f, 2f)]
        public float LowDecayMultiplier { get; set; } = 1.0f;

        [field: Range(0f, 1f)]
        public float EarlyLevel { get; set; } = 0.5f;

        [field: Range(0f, 1f)]
        public float TailLevel { get; set; } = 0.7f;

        [field: Range(0f, 1f)]
        public float StereoWidth { get; set; } = 1.0f;

        public bool Freeze { get; set; } = false;

        #endregion

        #region Constants

        private const int NumCombs = 8;
        private const int NumAllpass = 4;
        private const float TwoPi = 6.283185f;
        private const float MaxPreDelaySec = 0.2f;
        private const float MaxEarlySec = 0.1f;

        private static readonly int[] CombTunings =
        {
            1116, 1188, 1277, 1356, 1422, 1491, 1557, 1617
        };

        private static readonly int[] AllpassTunings =
        {
            556, 441, 341, 225
        };

        private const int StereoSpread = 23;

        private static readonly float[] ErDelaysMs =
        {
            4.3f, 7.2f, 10.8f, 16.1f, 21.5f, 27.3f,
            31.4f, 38.9f, 44.7f, 52.3f, 61.8f, 73.2f
        };

        private static readonly float[] ErGains =
        {
            0.841f, 0.782f, 0.723f, 0.664f, 0.605f, 0.546f,
            0.487f, 0.428f, 0.369f, 0.310f, 0.251f, 0.192f
        };

        private static readonly float[] ErPan =
        {
            -0.8f, 0.6f, -0.4f, 0.9f, -0.2f, 0.7f,
            -0.5f, 0.3f, -0.9f, 0.5f, -0.6f, 0.8f
        };

        #endregion

        #region Internal State

        private CombFilter[] _combL;
        private CombFilter[] _combR;
        private AllpassFilter[] _allpassL;
        private AllpassFilter[] _allpassR;

        private float[] _preDelayBuffer;
        private int _preDelayWritePos;
        private int _preDelayMaxSamples;

        private float[] _erBuffer;
        private int _erWritePos;
        private int _erBufferSize;

        private int _sampleRate;
        private bool _initialized;
        private float _modPhase;

        #endregion

        #region Processing

        public override void Process(float[] data, int channels, int sampleRate)
        {
            if (!Enabled || Mix <= 0f) return;

            EnsureInitialized(sampleRate);

            int preDelaySamples = Mathf.Min(
                (int)(PreDelay * sampleRate / 1000f),
                _preDelayMaxSamples - 1);

            float roomNorm = Mathf.InverseLerp(10f, 100f, RoomSize);
            float decayNorm = Mathf.InverseLerp(0.1f, 10f, DecayTime);
            float feedbackBase = Mathf.Lerp(0.4f, 0.98f, Mathf.Clamp01((roomNorm + decayNorm) * 0.5f));
            if (Freeze) feedbackBase = 0.99f;

            float damp = Mathf.Clamp01(Damping / Mathf.Max(0.5f, LowDecayMultiplier));
            float allpassGain = Mathf.Lerp(0.3f, 0.7f, Mathf.Clamp01(Diffusion));
            float modDepth = ModulationDepth * 0.02f;
            float modPhaseStep = ModulationRate * TwoPi / Mathf.Max(1f, sampleRate);

            for (int i = 0; i < NumCombs; i++)
            {
                _combL[i].SetDamp(damp);
                _combR[i].SetDamp(damp);
            }

            for (int i = 0; i < NumAllpass; i++)
            {
                _allpassL[i].SetFeedback(allpassGain);
                _allpassR[i].SetFeedback(allpassGain);
            }

            float dry = 1f - Mix;
            float width = Mathf.Clamp01(StereoWidth);
            int samplesPerChannel = data.Length / channels;

            for (int s = 0; s < samplesPerChannel; s++)
            {
                int idxL = s * channels;
                int idxR = channels > 1 ? idxL + 1 : idxL;

                float inputL = data[idxL];
                float inputR = channels > 1 ? data[idxR] : inputL;
                float inputMono = (inputL + inputR) * 0.5f;

                _preDelayBuffer[_preDelayWritePos] = inputMono;
                int preReadPos = _preDelayWritePos - preDelaySamples;
                if (preReadPos < 0) preReadPos += _preDelayMaxSamples;
                float preDelayed = Freeze ? 0f : _preDelayBuffer[preReadPos];
                _preDelayWritePos++;
                if (_preDelayWritePos >= _preDelayMaxSamples) _preDelayWritePos = 0;

                float earlyL = 0f;
                float earlyR = 0f;
                _erBuffer[_erWritePos] = preDelayed;
                if (EarlyLevel > 0.0001f)
                {
                    for (int t = 0; t < ErDelaysMs.Length; t++)
                    {
                        int delaySamples = (int)(ErDelaysMs[t] * sampleRate / 1000f);
                        if (delaySamples >= _erBufferSize) delaySamples = _erBufferSize - 1;
                        int readPos = _erWritePos - delaySamples;
                        if (readPos < 0) readPos += _erBufferSize;

                        float tap = _erBuffer[readPos] * ErGains[t];
                        float panL = Mathf.Clamp01(0.5f - ErPan[t] * 0.5f);
                        float panR = Mathf.Clamp01(0.5f + ErPan[t] * 0.5f);
                        earlyL += tap * panL;
                        earlyR += tap * panR;
                    }
                }
                _erWritePos++;
                if (_erWritePos >= _erBufferSize) _erWritePos = 0;

                _modPhase += modPhaseStep;
                if (_modPhase >= TwoPi) _modPhase -= TwoPi;
                float mod = 1f + Mathf.Sin(_modPhase) * modDepth;
                float feedback = Mathf.Clamp(feedbackBase * mod, 0f, 0.99f);
                for (int i = 0; i < NumCombs; i++)
                {
                    _combL[i].SetFeedback(feedback);
                    _combR[i].SetFeedback(feedback);
                }

                float outL = 0f;
                float outR = 0f;
                for (int i = 0; i < NumCombs; i++)
                {
                    outL += _combL[i].Process(preDelayed);
                    outR += _combR[i].Process(preDelayed);
                }

                float combScale = 1f / NumCombs;
                outL *= combScale;
                outR *= combScale;

                for (int i = 0; i < NumAllpass; i++)
                {
                    outL = _allpassL[i].Process(outL);
                    outR = _allpassR[i].Process(outR);
                }

                float wetL = earlyL * EarlyLevel + outL * TailLevel;
                float wetR = earlyR * EarlyLevel + outR * TailLevel;

                float mid = (wetL + wetR) * 0.5f;
                float side = (wetL - wetR) * 0.5f * width;
                wetL = mid + side;
                wetR = mid - side;

                data[idxL] = inputL * dry + wetL * Mix;
                if (channels > 1)
                    data[idxR] = inputR * dry + wetR * Mix;
            }
        }

        #endregion

        #region Initialization

        private void EnsureInitialized(int sampleRate)
        {
            if (_initialized && _sampleRate == sampleRate) return;

            _sampleRate = sampleRate;
            float sampleRateRatio = sampleRate / 44100f;

            _combL = new CombFilter[NumCombs];
            _combR = new CombFilter[NumCombs];
            for (int i = 0; i < NumCombs; i++)
            {
                int baseDelay = Mathf.Max(1, (int)(CombTunings[i] * sampleRateRatio));
                _combL[i] = new CombFilter(baseDelay);
                _combR[i] = new CombFilter(baseDelay + StereoSpread);
            }

            _allpassL = new AllpassFilter[NumAllpass];
            _allpassR = new AllpassFilter[NumAllpass];
            for (int i = 0; i < NumAllpass; i++)
            {
                int baseDelay = Mathf.Max(1, (int)(AllpassTunings[i] * sampleRateRatio));
                _allpassL[i] = new AllpassFilter(baseDelay);
                _allpassR[i] = new AllpassFilter(baseDelay + StereoSpread);
            }

            _preDelayMaxSamples = Mathf.Max(1, (int)(MaxPreDelaySec * sampleRate));
            _preDelayBuffer = new float[_preDelayMaxSamples];
            _preDelayWritePos = 0;

            _erBufferSize = Mathf.Max(1, (int)(MaxEarlySec * sampleRate));
            _erBuffer = new float[_erBufferSize];
            _erWritePos = 0;

            _modPhase = 0f;
            _initialized = true;
        }

        #endregion

        #region Reset

        public override void Reset()
        {
            _initialized = false;

            if (_preDelayBuffer != null)
                Array.Clear(_preDelayBuffer, 0, _preDelayBuffer.Length);
            if (_erBuffer != null)
                Array.Clear(_erBuffer, 0, _erBuffer.Length);

            if (_combL != null)
            {
                for (int i = 0; i < _combL.Length; i++)
                    _combL[i]?.Mute();
            }
            if (_combR != null)
            {
                for (int i = 0; i < _combR.Length; i++)
                    _combR[i]?.Mute();
            }
            if (_allpassL != null)
            {
                for (int i = 0; i < _allpassL.Length; i++)
                    _allpassL[i]?.Mute();
            }
            if (_allpassR != null)
            {
                for (int i = 0; i < _allpassR.Length; i++)
                    _allpassR[i]?.Mute();
            }
        }

        #endregion

        #region Presets

        public override void ApplyPreset(string presetName)
        {
            switch (presetName.ToLowerInvariant())
            {
                case "small room":
                    PreDelay = 5f; RoomSize = 15f; DecayTime = 0.6f;
                    Damping = 0.6f; Diffusion = 0.5f; Mix = 0.25f;
                    EarlyLevel = 0.6f; TailLevel = 0.5f; StereoWidth = 0.9f;
                    break;
                case "large hall":
                    PreDelay = 30f; RoomSize = 60f; DecayTime = 3.5f;
                    Damping = 0.4f; Diffusion = 0.8f; Mix = 0.35f;
                    EarlyLevel = 0.35f; TailLevel = 0.8f; StereoWidth = 1.0f;
                    break;
                case "cathedral":
                    PreDelay = 50f; RoomSize = 90f; DecayTime = 6.0f;
                    Damping = 0.3f; Diffusion = 0.9f; Mix = 0.45f;
                    EarlyLevel = 0.25f; TailLevel = 0.9f; StereoWidth = 1.0f;
                    break;
                case "plate":
                    PreDelay = 0f; RoomSize = 30f; DecayTime = 2.0f;
                    Damping = 0.5f; Diffusion = 0.95f; ModulationDepth = 0.3f; Mix = 0.4f;
                    EarlyLevel = 0.4f; TailLevel = 0.7f; StereoWidth = 0.9f;
                    break;
                case "ambient":
                    PreDelay = 80f; RoomSize = 50f; DecayTime = 8.0f;
                    Damping = 0.7f; Diffusion = 0.6f; EarlyLevel = 0.2f; TailLevel = 0.9f; Mix = 0.5f;
                    StereoWidth = 1.0f;
                    break;
                default:
#if VARCO_DEBUG
                    Debug.LogWarning($"[Freeverb] Unknown preset: {presetName}");
#endif
                    break;
            }

            _initialized = false;
        }

        #endregion

        #region Filters

        private sealed class CombFilter
        {
            private readonly float[] _buffer;
            private int _index;
            private float _filterStore;
            private float _feedback;
            private float _damp1;
            private float _damp2;

            public CombFilter(int size)
            {
                _buffer = new float[Mathf.Max(1, size)];
            }

            public void SetFeedback(float value)
            {
                _feedback = value;
            }

            public void SetDamp(float value)
            {
                _damp1 = value;
                _damp2 = 1f - value;
            }

            public float Process(float input)
            {
                float output = _buffer[_index];
                _filterStore = (output * _damp2) + (_filterStore * _damp1);
                _buffer[_index] = input + (_filterStore * _feedback);
                _index++;
                if (_index >= _buffer.Length) _index = 0;
                return output;
            }

            public void Mute()
            {
                Array.Clear(_buffer, 0, _buffer.Length);
                _filterStore = 0f;
                _index = 0;
            }
        }

        private sealed class AllpassFilter
        {
            private readonly float[] _buffer;
            private int _index;
            private float _feedback = 0.5f;

            public AllpassFilter(int size)
            {
                _buffer = new float[Mathf.Max(1, size)];
            }

            public void SetFeedback(float value)
            {
                _feedback = value;
            }

            public float Process(float input)
            {
                float bufOut = _buffer[_index];
                float output = -input + bufOut;
                _buffer[_index] = input + (bufOut * _feedback);
                _index++;
                if (_index >= _buffer.Length) _index = 0;
                return output;
            }

            public void Mute()
            {
                Array.Clear(_buffer, 0, _buffer.Length);
                _index = 0;
            }
        }

        #endregion
    }
}
