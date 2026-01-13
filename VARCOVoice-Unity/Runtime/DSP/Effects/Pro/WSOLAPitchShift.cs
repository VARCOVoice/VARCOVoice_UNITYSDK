using System;
using UnityEngine;

namespace VARCOVoice.DSP
{
    /// <summary>
    /// WSOLA (Waveform Similarity Overlap-Add) pitch shifter.
    /// Keeps PSOLA interface for compatibility.
    /// </summary>
    [Serializable]
    public class WSOLAPitchShift : DSPEffectBase
    {
        public override string Name => "Pitch Shift";

        #region Parameters

        [Range(-12f, 12f)]
        public float Semitones = 0f;

        [Range(0f, 1f)]
        public float FormantPreservation = 1.0f;

        [Range(50f, 400f)]
        public float MinPitch = 70f;

        [Range(200f, 1000f)]
        public float MaxPitch = 600f;

        #endregion

        #region Internal State

        private const int BufferSize = 65536;
        private const int BufferMask = BufferSize - 1;
        private const int MinWindowSize = 512;
        private const int MaxWindowSize = 4096;

        private float[] _inputBuffer;
        private float[] _outputBuffer;
        private float[] _window;
        private float[] _prevWindow;
        private bool _hasPrevWindow;

        private int _windowSize;
        private int _searchRange;
        private int _analysisHop;
        private int _outputLatency;

        private double _analysisPos;
        private double _synthesisPos;
        private int _inputWritePos;
        private double _outputReadPos;
        private double _outputReadStep;
        private int _outputClearPos;

        private int _channels;
        private int _sampleRate;
        private float _lastMinPitch;
        private float _lastMaxPitch;
        private float _lastFormant;
        private bool _initialized;

        #endregion

        public override void Process(float[] data, int channels, int sampleRate)
        {
            if (!Enabled || Mathf.Abs(Semitones) < 0.01f) return;

            EnsureInitialized(channels, sampleRate);
            UpdateWindowConfig(sampleRate, false);

            float pitchRatio = Mathf.Pow(2f, Semitones / 12f);
            float timeStretchRatio = Mathf.Max(0.001f, pitchRatio);
            double synthesisHop = _analysisHop * timeStretchRatio;
            _outputReadStep = timeStretchRatio;
            int frames = data.Length / channels;

            double analysisPos = _analysisPos;
            double synthesisPos = _synthesisPos;
            int inputWritePos = _inputWritePos;
            double outputReadPos = _outputReadPos;

            for (int i = 0; i < frames; i++)
            {
                int writeIndex = (inputWritePos & BufferMask) * channels;
                int dataIndex = i * channels;
                for (int c = 0; c < channels; c++)
                {
                    _inputBuffer[writeIndex + c] = data[dataIndex + c];
                }

                while (FramesAvailable(inputWritePos, (int)analysisPos) >= _windowSize + _searchRange)
                {
                    int analysisCenter = ((int)analysisPos) & BufferMask;
                    int offset = FindBestOffset(analysisCenter, channels);
                    int readPos = (analysisCenter + offset) & BufferMask;
                    int writePos = ((int)synthesisPos) & BufferMask;

                    OverlapAdd(readPos, writePos, channels);

                    analysisPos += _analysisHop;
                    if (analysisPos >= BufferSize) analysisPos -= BufferSize;
                    synthesisPos += synthesisHop;
                    while (synthesisPos >= BufferSize) synthesisPos -= BufferSize;
                }

                bool outputReady = OutputReady(synthesisPos, outputReadPos);
                for (int c = 0; c < channels; c++)
                {
                    float dry = data[dataIndex + c];
                    if (outputReady && Mix < 0.999f)
                    {
                        int dryReadPos = (inputWritePos - 1 - _outputLatency) & BufferMask;
                        dry = _inputBuffer[dryReadPos * channels + c];
                    }

                    float wet = outputReady
                        ? GetOutputSample(outputReadPos, c, channels)
                        : dry;

                    data[dataIndex + c] = dry * (1f - Mix) + wet * Mix;
                }

                inputWritePos = (inputWritePos + 1) & BufferMask;
                if (outputReady)
                {
                    outputReadPos = AdvanceOutputRead(outputReadPos, _outputReadStep, channels);
                }
            }

            _analysisPos = analysisPos;
            _synthesisPos = synthesisPos;
            _inputWritePos = inputWritePos;
            _outputReadPos = outputReadPos;
        }

        private void EnsureInitialized(int channels, int sampleRate)
        {
            if (_initialized && _channels == channels && _sampleRate == sampleRate) return;

            _channels = Mathf.Max(1, channels);
            _sampleRate = sampleRate;

            _inputBuffer = new float[BufferSize * _channels];
            _outputBuffer = new float[BufferSize * _channels];
            _analysisPos = 0.0;
            _synthesisPos = 0.0;
            _inputWritePos = 0;
            _outputReadPos = 0.0;
            _outputClearPos = -1;
            _hasPrevWindow = false;
            _initialized = true;

            UpdateWindowConfig(sampleRate, true);
        }

        private void UpdateWindowConfig(int sampleRate, bool force)
        {
            int newWindowSize = ComputeWindowSize(sampleRate);
            bool sizeChanged = newWindowSize != _windowSize;
            bool paramChanged =
                !Mathf.Approximately(_lastMinPitch, MinPitch) ||
                !Mathf.Approximately(_lastMaxPitch, MaxPitch) ||
                !Mathf.Approximately(_lastFormant, FormantPreservation);

            if (!force && !sizeChanged && !paramChanged) return;

            _lastMinPitch = MinPitch;
            _lastMaxPitch = MaxPitch;
            _lastFormant = FormantPreservation;

            _windowSize = newWindowSize;
            _analysisHop = Mathf.Max(1, _windowSize / 4);

            float minPitch = Mathf.Max(50f, MinPitch);
            float maxPitch = Mathf.Max(minPitch + 1f, MaxPitch);
            float avgPitch = Mathf.Sqrt(minPitch * maxPitch);
            float basePeriod = sampleRate / avgPitch;
            _searchRange = Mathf.Clamp((int)(basePeriod * 0.5f), 64, 256);
            _outputLatency = Mathf.Clamp(_windowSize + _searchRange, 64, BufferSize / 2);

            _window = new float[_windowSize];
            _prevWindow = new float[_windowSize];
            for (int i = 0; i < _windowSize; i++)
            {
                float t = (float)i / (_windowSize - 1);
                _window[i] = 0.5f * (1f - Mathf.Cos(2f * Mathf.PI * t));
            }

            if (force || sizeChanged)
            {
                _analysisPos = 0.0;
                _synthesisPos = _windowSize;
                _inputWritePos = 0;
                _outputReadPos = WrapPos(_synthesisPos - _outputLatency);
                _outputClearPos = (int)Mathf.Floor((float)_outputReadPos) & BufferMask;
                _hasPrevWindow = false;
            }
        }

        private int ComputeWindowSize(int sampleRate)
        {
            float minPitch = Mathf.Max(50f, MinPitch);
            float maxPitch = Mathf.Max(minPitch + 1f, MaxPitch);
            float avgPitch = Mathf.Sqrt(minPitch * maxPitch);
            float period = sampleRate / avgPitch;

            int baseWindow = (int)(period * 2.0f);
            float scale = Mathf.Lerp(1.0f, 2.2f, Mathf.Clamp01(FormantPreservation));
            int size = (int)(baseWindow * scale);
            return Mathf.Clamp(size, MinWindowSize, MaxWindowSize);
        }

        private int FramesAvailable(int writePos, int readPos)
        {
            int diff = writePos - readPos;
            if (diff < 0) diff += BufferSize;
            return diff;
        }

        private int FindBestOffset(int analysisCenter, int channels)
        {
            if (!_hasPrevWindow) return 0;

            int bestOffset = 0;
            float bestCorr = float.MinValue;
            int step = 2;

            for (int offset = -_searchRange; offset <= _searchRange; offset += step)
            {
                float corr = 0f;
                int basePos = (analysisCenter + offset) & BufferMask;
                for (int i = 0; i < _windowSize; i += 4)
                {
                    int frame = (basePos + i) & BufferMask;
                    float sample = GetMonoSample(frame, channels);
                    corr += _prevWindow[i] * sample;
                }
                if (corr > bestCorr)
                {
                    bestCorr = corr;
                    bestOffset = offset;
                }
            }

            return bestOffset;
        }

        private float GetMonoSample(int frameIndex, int channels)
        {
            int idx = (frameIndex & BufferMask) * channels;
            float sum = 0f;
            for (int c = 0; c < channels; c++)
                sum += _inputBuffer[idx + c];
            return sum / channels;
        }

        private void OverlapAdd(int readPos, int writePos, int channels)
        {
            for (int i = 0; i < _windowSize; i++)
            {
                float win = _window[i];
                int r = (readPos + i) & BufferMask;
                int w = (writePos + i) & BufferMask;
                int readIndex = r * channels;
                int writeIndex = w * channels;

                float mono = 0f;
                for (int c = 0; c < channels; c++)
                {
                    float sample = _inputBuffer[readIndex + c];
                    _outputBuffer[writeIndex + c] += sample * win;
                    mono += sample;
                }
                _prevWindow[i] = mono / channels;
            }

            _hasPrevWindow = true;
        }

        private bool OutputReady(double synthesisPos, double outputReadPos)
        {
            int synth = (int)synthesisPos;
            int read = (int)outputReadPos;
            int diff = synth - read;
            if (diff < 0) diff += BufferSize;
            return diff >= _outputLatency;
        }

        private float GetOutputSample(double pos, int channel, int channels)
        {
            int index0 = (int)pos & BufferMask;
            int index1 = (index0 + 1) & BufferMask;
            float frac = (float)(pos - Math.Floor(pos));

            int base0 = index0 * channels + channel;
            int base1 = index1 * channels + channel;
            return Mathf.Lerp(_outputBuffer[base0], _outputBuffer[base1], frac);
        }

        private double AdvanceOutputRead(double pos, double step, int channels)
        {
            double newPos = pos + step;
            while (newPos >= BufferSize) newPos -= BufferSize;
            while (newPos < 0) newPos += BufferSize;

            int oldFloor = (int)Math.Floor(pos) & BufferMask;
            int newFloor = (int)Math.Floor(newPos) & BufferMask;
            if (_outputClearPos < 0) _outputClearPos = oldFloor;

            int clearPos = _outputClearPos;
            while (clearPos != newFloor)
            {
                int idx = (clearPos & BufferMask) * channels;
                for (int c = 0; c < channels; c++)
                {
                    _outputBuffer[idx + c] = 0f;
                }
                clearPos = (clearPos + 1) & BufferMask;
            }

            _outputClearPos = newFloor;
            return newPos;
        }

        private double WrapPos(double pos)
        {
            while (pos < 0) pos += BufferSize;
            while (pos >= BufferSize) pos -= BufferSize;
            return pos;
        }

        public override void ApplyPreset(string presetName)
        {
            if (string.IsNullOrEmpty(presetName)) return;
            switch (presetName.Trim().ToLowerInvariant())
            {
                case "deep":
                    Semitones = -5f; FormantPreservation = 0.9f; Mix = 0.85f; break;
                case "chipmunk":
                    Semitones = 7f; FormantPreservation = 0.4f; Mix = 0.8f; break;
                case "demon":
                    Semitones = -12f; FormantPreservation = 0.7f; Mix = 0.6f; break;
                case "doubler":
                    Semitones = 0.5f; FormantPreservation = 1f; Mix = 0.35f; break;
                case "natural up":
                    Semitones = 2f; FormantPreservation = 0.9f; Mix = 0.85f; break;
                case "natural down":
                    Semitones = -2f; FormantPreservation = 0.9f; Mix = 0.85f; break;
            }
        }

        public override void Reset()
        {
            _initialized = false;
            if (_inputBuffer != null) Array.Clear(_inputBuffer, 0, _inputBuffer.Length);
            if (_outputBuffer != null) Array.Clear(_outputBuffer, 0, _outputBuffer.Length);
            if (_prevWindow != null) Array.Clear(_prevWindow, 0, _prevWindow.Length);
            _hasPrevWindow = false;
        }
    }
}
