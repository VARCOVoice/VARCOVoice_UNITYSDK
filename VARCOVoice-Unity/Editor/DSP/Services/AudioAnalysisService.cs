using UnityEngine;
using VARCOVoice.DSP;

namespace VARCOVoice.Editor.Services
{
    public static class AudioAnalysisService
    {
        private const int WaveformSize = 1024;
        private const int SpectrumSize = 4096;
        private const float DefaultUpdateInterval = 0.016f;

        private static readonly float[] _waveformData = new float[WaveformSize];
        private static readonly float[] _spectrumData = new float[SpectrumSize];
        private static readonly float[] _imagBuffer = new float[SpectrumSize];
        private static readonly float[] _smoothSpectrum = new float[SpectrumSize];
        private static readonly float[] _samples = new float[SpectrumSize];

        private static readonly float[] _preEQSamples = new float[SpectrumSize];
        private static readonly float[] _preEQSpectrumData = new float[SpectrumSize];
        private static readonly float[] _preEQImagBuffer = new float[SpectrumSize];
        private static readonly float[] _smoothPreEQSpectrum = new float[SpectrumSize];

        private static float _leftLevel = -60f;
        private static float _rightLevel = -60f;
        private static float _smoothLeftLevel = -60f;
        private static float _smoothRightLevel = -60f;
        private static float _peakLevel = -60f;
        private static float _peakDecay = 0.95f;

        public static float[] WaveformData => _waveformData;
        public static float[] SpectrumData => _spectrumData;
        public static float[] SmoothSpectrum => _smoothSpectrum;
        public static float[] SmoothPreEQSpectrum => _smoothPreEQSpectrum;

        public static float LeftLevel => _leftLevel;
        public static float RightLevel => _rightLevel;
        public static float SmoothLeftLevel => _smoothLeftLevel;
        public static float SmoothRightLevel => _smoothRightLevel;
        public static float PeakLevel => _peakLevel;

        public static void Update(DSPChain chain, float deltaTime, bool isPlaying)
        {
            if (chain == null) return;

            float dt = deltaTime;
            if (dt <= 0f)
            {
                dt = Application.isPlaying ? Time.deltaTime : DefaultUpdateInterval;
            }

            if (isPlaying)
            {
                Analyze(chain, dt);
            }
            else
            {
                Decay();
            }
        }

        public static bool HasActivity()
        {
            const float spectrumThreshold = 1e-5f;
            if (HasSpectrumEnergy(_smoothSpectrum, spectrumThreshold)) return true;
            if (HasSpectrumEnergy(_smoothPreEQSpectrum, spectrumThreshold)) return true;

            const float silenceDb = -59f;
            return _smoothLeftLevel > silenceDb || _smoothRightLevel > silenceDb || _peakLevel > silenceDb;
        }

        private static void Analyze(DSPChain chain, float dt)
        {
            // 1. Get processed samples directly from DSP Chain (Post-FX)
            chain.GetLatestSamples(_samples);
            SanitizeSamples(_samples);

            // 2. Waveform Stabilization (Zero-Crossing Trigger)
            int triggerIndex = 0;
            for (int i = 0; i < 2048; i++)
            {
                if (_samples[i] < 0f && _samples[i + 1] >= 0f)
                {
                    triggerIndex = i;
                    break;
                }
            }

            int step = 2;
            for (int i = 0; i < _waveformData.Length; i++)
            {
                int sampleIdx = triggerIndex + i * step;
                _waveformData[i] = sampleIdx < _samples.Length ? _samples[sampleIdx] : 0f;
            }

            // 3. Spectrum Analysis (Manual FFT)
            for (int i = 0; i < _spectrumData.Length; i++)
            {
                _spectrumData[i] = _samples[i];
                _imagBuffer[i] = 0f;
            }

            DSPUtils.ApplyWindow(_spectrumData, FFTWindow.BlackmanHarris);
            DSPUtils.FFT(_spectrumData, _imagBuffer);

            float normFactor = 1f / _spectrumData.Length;
            for (int i = 0; i < _spectrumData.Length; i++)
            {
                float r = _spectrumData[i];
                float im = _imagBuffer[i];
                _spectrumData[i] = Mathf.Sqrt(r * r + im * im) * normFactor;
            }

            // 4. Smooth Spectrum (FabFilter-style ballistics)
            float attack = 18f;
            float release = 3.5f;

            for (int i = 0; i < _spectrumData.Length; i++)
            {
                float target = _spectrumData[i];
                float current = _smoothSpectrum[i];
                _smoothSpectrum[i] = target > current
                    ? Mathf.Lerp(current, target, dt * attack)
                    : Mathf.Lerp(current, target, dt * release);
            }

            // 5. Pre-EQ Spectrum Analysis (for overlay)
            chain.GetPreEQSamples(_preEQSamples);
            SanitizeSamples(_preEQSamples);

            for (int i = 0; i < _preEQSpectrumData.Length; i++)
            {
                _preEQSpectrumData[i] = _preEQSamples[i];
                _preEQImagBuffer[i] = 0f;
            }

            DSPUtils.ApplyWindow(_preEQSpectrumData, FFTWindow.BlackmanHarris);
            DSPUtils.FFT(_preEQSpectrumData, _preEQImagBuffer);

            for (int i = 0; i < _preEQSpectrumData.Length; i++)
            {
                float r = _preEQSpectrumData[i];
                float im = _preEQImagBuffer[i];
                _preEQSpectrumData[i] = Mathf.Sqrt(r * r + im * im) * normFactor;
            }

            for (int i = 0; i < _preEQSpectrumData.Length; i++)
            {
                float target = _preEQSpectrumData[i];
                float current = _smoothPreEQSpectrum[i];
                _smoothPreEQSpectrum[i] = target > current
                    ? Mathf.Lerp(current, target, dt * attack)
                    : Mathf.Lerp(current, target, dt * release);
            }

            // 6. Stereo levels from DSPChain (real L/R from audio thread)
            chain.GetStereoLevels(out float rawLeft, out float rawRight, out float peakL, out float peakR);

            float leftDb = rawLeft > 0.00001f ? 20f * Mathf.Log10(rawLeft) : -60f;
            float rightDb = rawRight > 0.00001f ? 20f * Mathf.Log10(rawRight) : -60f;
            float peakLeftDb = peakL > 0.00001f ? 20f * Mathf.Log10(peakL) : -60f;
            float peakRightDb = peakR > 0.00001f ? 20f * Mathf.Log10(peakR) : -60f;

            leftDb = Mathf.Clamp(leftDb, -60f, 0f);
            rightDb = Mathf.Clamp(rightDb, -60f, 0f);

            if (leftDb > _smoothLeftLevel)
                _smoothLeftLevel = Mathf.Lerp(_smoothLeftLevel, leftDb, dt * attack);
            else
                _smoothLeftLevel = Mathf.Lerp(_smoothLeftLevel, leftDb, dt * release);

            if (rightDb > _smoothRightLevel)
                _smoothRightLevel = Mathf.Lerp(_smoothRightLevel, rightDb, dt * attack);
            else
                _smoothRightLevel = Mathf.Lerp(_smoothRightLevel, rightDb, dt * release);

            _leftLevel = leftDb;
            _rightLevel = rightDb;

            float currentPeak = Mathf.Max(peakLeftDb, peakRightDb);
            _peakLevel = Mathf.Max(_peakLevel, currentPeak);
            _peakLevel = Mathf.Lerp(_peakLevel, currentPeak, _peakDecay);
        }

        private static void Decay()
        {
            const float silenceDb = -60f;
            const float meterDecay = 0.1f;

            _leftLevel = Mathf.Lerp(_leftLevel, silenceDb, meterDecay);
            _rightLevel = Mathf.Lerp(_rightLevel, silenceDb, meterDecay);
            _smoothLeftLevel = Mathf.Lerp(_smoothLeftLevel, silenceDb, meterDecay);
            _smoothRightLevel = Mathf.Lerp(_smoothRightLevel, silenceDb, meterDecay);
            _peakLevel = Mathf.Lerp(_peakLevel, silenceDb, 1f - _peakDecay);

            for (int i = 0; i < _waveformData.Length; i++)
                _waveformData[i] *= 0.1f;

            for (int i = 0; i < _smoothSpectrum.Length; i++)
                _smoothSpectrum[i] *= 0.9f;

            for (int i = 0; i < _smoothPreEQSpectrum.Length; i++)
                _smoothPreEQSpectrum[i] *= 0.9f;
        }

        private static bool HasSpectrumEnergy(float[] spectrum, float threshold)
        {
            if (spectrum == null || spectrum.Length == 0) return false;
            for (int i = 0; i < spectrum.Length; i += 8)
            {
                if (spectrum[i] > threshold) return true;
            }
            return false;
        }

        private static void SanitizeSamples(float[] samples)
        {
            if (samples == null) return;
            for (int i = 0; i < samples.Length; i++)
            {
                float value = samples[i];
                if (float.IsNaN(value) || float.IsInfinity(value))
                {
                    samples[i] = 0f;
                }
            }
        }
    }
}
