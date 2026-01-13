using UnityEngine;

namespace VARCOVoice.DSP
{
    /// <summary>
    /// Unified delay effect with selectable modes.
    /// </summary>
    [System.Serializable]
    public class UnifiedDelay : DSPEffectBase
    {
        public override string Name => "Delay";

        public enum DelayMode { Standard, PingPong, MultiTap, Tape }

        public DelayMode Mode { get; set; } = DelayMode.Standard;

        [field: Range(0f, 2000f)]
        public float Time { get; set; } = 250f;

        [field: Range(0f, 0.95f)]
        public float Feedback { get; set; } = 0.3f;

        [field: Range(0f, 1f)]
        public float Width { get; set; } = 1f;

        public int TapCount { get; set; } = 4;

        [field: Range(0.1f, 2f)]
        public float TapSpacing { get; set; } = 1.0f;

        [field: Range(0f, 1f)]
        public float TapDecay { get; set; } = 0.7f;

        [field: Range(0f, 1f)]
        public float CrossFeedback { get; set; } = 0.3f;

        public float ModRate { get; set; } = 0.5f;
        public float ModDepth { get; set; } = 5f;

        [field: Range(20f, 20000f)]
        public float FilterLow { get; set; } = 20f;

        [field: Range(20f, 20000f)]
        public float FilterHigh { get; set; } = 12000f;

        private readonly DelayEffect _standard = new();
        private readonly PingPongDelay _pingPong = new();
        private readonly MultiTapDelay _multiTap = new();
        private readonly ModulatedDelay _tape = new();

        public override void Process(float[] data, int channels, int sampleRate)
        {
            switch (Mode)
            {
                case DelayMode.PingPong:
                    ApplyPingPong(data, channels, sampleRate);
                    break;
                case DelayMode.MultiTap:
                    ApplyMultiTap(data, channels, sampleRate);
                    break;
                case DelayMode.Tape:
                    ApplyTape(data, channels, sampleRate);
                    break;
                default:
                    ApplyStandard(data, channels, sampleRate);
                    break;
            }
        }

        private void ApplyStandard(float[] data, int channels, int sampleRate)
        {
            _standard.DelayTime = Time;
            _standard.Feedback = Feedback;
            _standard.Mix = Mix;
            ApplyFilters(_standard, sampleRate);
            _standard.Process(data, channels, sampleRate);
        }

        private void ApplyPingPong(float[] data, int channels, int sampleRate)
        {
            if (channels < 2)
            {
                ApplyStandard(data, channels, sampleRate);
                return;
            }

            _pingPong.DelayTime = Time;
            _pingPong.Feedback = Feedback;
            _pingPong.CrossFeedback = CrossFeedback;
            _pingPong.Width = Width;
            _pingPong.Mix = Mix;
            _pingPong.Process(data, channels, sampleRate);
        }

        private void ApplyMultiTap(float[] data, int channels, int sampleRate)
        {
            _multiTap.BaseDelay = Time;
            _multiTap.TapCount = TapCount;
            _multiTap.TapSpacing = TapSpacing;
            _multiTap.TapDecay = TapDecay;
            _multiTap.Feedback = Feedback;
            _multiTap.Mix = Mix;
            _multiTap.Process(data, channels, sampleRate);
        }

        private void ApplyTape(float[] data, int channels, int sampleRate)
        {
            _tape.DelayTime = Time;
            _tape.ModRate = ModRate;
            _tape.ModDepth = ModDepth;
            _tape.Feedback = Feedback;
            _tape.Mix = Mix;
            ApplyFilters(_tape, sampleRate);
            _tape.Process(data, channels, sampleRate);
        }

        private void ApplyFilters(DelayEffect delay, int sampleRate)
        {
            var (low, high) = ClampFilter(sampleRate);
            delay.FeedbackHPF = low;
            delay.FeedbackLPF = high;
        }

        private void ApplyFilters(ModulatedDelay delay, int sampleRate)
        {
            var (low, high) = ClampFilter(sampleRate);
            delay.FeedbackHPF = low;
            delay.FeedbackLPF = high;
        }

        private (float low, float high) ClampFilter(int sampleRate)
        {
            float nyquist = sampleRate > 0 ? sampleRate * 0.45f : 22050f;
            float low = Mathf.Clamp(FilterLow, 20f, nyquist);
            float high = Mathf.Clamp(FilterHigh, 20f, nyquist);
            if (high < low) high = low;
            return (low, high);
        }

        public override void ApplyPreset(string presetName)
        {
            if (string.IsNullOrEmpty(presetName)) return;

            switch (presetName.Trim().ToLowerInvariant())
            {
                case "slapback":
                    Mode = DelayMode.Standard;
                    Time = 70f;
                    Feedback = 0.15f;
                    FilterLow = 120f;
                    FilterHigh = 8000f;
                    Mix = 0.25f;
                    break;
                case "vocal double":
                    Mode = DelayMode.Standard;
                    Time = 120f;
                    Feedback = 0.1f;
                    FilterLow = 100f;
                    FilterHigh = 9000f;
                    Mix = 0.2f;
                    break;
                case "rhythmic quarter":
                    Mode = DelayMode.Standard;
                    Time = 500f;
                    Feedback = 0.4f;
                    FilterLow = 120f;
                    FilterHigh = 8000f;
                    Mix = 0.35f;
                    break;
                case "rhythmic triplet":
                    Mode = DelayMode.Standard;
                    Time = 333f;
                    Feedback = 0.3f;
                    FilterLow = 120f;
                    FilterHigh = 8000f;
                    Mix = 0.3f;
                    break;
                case "rhythmic dotted 8th":
                    Mode = DelayMode.Standard;
                    Time = 375f;
                    Feedback = 0.35f;
                    FilterLow = 120f;
                    FilterHigh = 8000f;
                    Mix = 0.3f;
                    break;
                case "long tail":
                    Mode = DelayMode.Standard;
                    Time = 800f;
                    Feedback = 0.5f;
                    FilterLow = 80f;
                    FilterHigh = 9000f;
                    Mix = 0.35f;
                    break;
                case "filtered echo":
                    Mode = DelayMode.Standard;
                    Time = 450f;
                    Feedback = 0.45f;
                    FilterLow = 500f;
                    FilterHigh = 3000f;
                    Mix = 0.35f;
                    break;
                case "tight slap":
                    Mode = DelayMode.MultiTap;
                    TapCount = 3;
                    Time = 80f;
                    TapSpacing = 1.0f;
                    TapDecay = 0.7f;
                    Feedback = 0.2f;
                    Mix = 0.25f;
                    break;
                case "rhythmic":
                    Mode = DelayMode.MultiTap;
                    TapCount = 4;
                    Time = 180f;
                    TapSpacing = 1.2f;
                    TapDecay = 0.6f;
                    Feedback = 0.3f;
                    Mix = 0.35f;
                    break;
                case "cascade":
                    Mode = DelayMode.MultiTap;
                    TapCount = 6;
                    Time = 120f;
                    TapSpacing = 1.4f;
                    TapDecay = 0.7f;
                    Feedback = 0.35f;
                    Mix = 0.4f;
                    break;
                case "wide wash":
                    Mode = DelayMode.MultiTap;
                    TapCount = 5;
                    Time = 220f;
                    TapSpacing = 1.3f;
                    TapDecay = 0.5f;
                    Feedback = 0.35f;
                    Mix = 0.35f;
                    break;
                case "sparse":
                    Mode = DelayMode.MultiTap;
                    TapCount = 2;
                    Time = 300f;
                    TapSpacing = 1.6f;
                    TapDecay = 0.6f;
                    Feedback = 0.25f;
                    Mix = 0.3f;
                    break;
                case "wide quarter":
                    Mode = DelayMode.PingPong;
                    Time = 500f;
                    Feedback = 0.4f;
                    CrossFeedback = 0.3f;
                    Width = 1f;
                    Mix = 0.35f;
                    break;
                case "slap ping":
                    Mode = DelayMode.PingPong;
                    Time = 140f;
                    Feedback = 0.25f;
                    CrossFeedback = 0.2f;
                    Width = 0.9f;
                    Mix = 0.25f;
                    break;
                case "spiral":
                    Mode = DelayMode.PingPong;
                    Time = 350f;
                    Feedback = 0.45f;
                    CrossFeedback = 0.3f;
                    Width = 1f;
                    Mix = 0.35f;
                    break;
                case "stereo echo":
                    Mode = DelayMode.PingPong;
                    Time = 260f;
                    Feedback = 0.35f;
                    CrossFeedback = 0.25f;
                    Width = 0.8f;
                    Mix = 0.3f;
                    break;
                case "ambient":
                    Mode = DelayMode.PingPong;
                    Time = 700f;
                    Feedback = 0.4f;
                    CrossFeedback = 0.25f;
                    Width = 1f;
                    Mix = 0.4f;
                    break;
                case "tape echo":
                    Mode = DelayMode.Tape;
                    Time = 320f;
                    ModDepth = 6f;
                    ModRate = 0.4f;
                    Feedback = 0.45f;
                    FilterLow = 20f;
                    FilterHigh = 20000f;
                    Mix = 0.4f;
                    break;
                case "vintage":
                    Mode = DelayMode.Tape;
                    Time = 260f;
                    ModDepth = 4f;
                    ModRate = 0.3f;
                    Feedback = 0.35f;
                    FilterLow = 20f;
                    FilterHigh = 20000f;
                    Mix = 0.35f;
                    break;
                case "wobble":
                    Mode = DelayMode.Tape;
                    Time = 200f;
                    ModDepth = 8f;
                    ModRate = 0.8f;
                    Feedback = 0.4f;
                    FilterLow = 20f;
                    FilterHigh = 20000f;
                    Mix = 0.4f;
                    break;
                case "subtle":
                    Mode = DelayMode.Tape;
                    Time = 180f;
                    ModDepth = 2f;
                    ModRate = 0.2f;
                    Feedback = 0.25f;
                    FilterLow = 20f;
                    FilterHigh = 20000f;
                    Mix = 0.25f;
                    break;
                case "lofi":
                    Mode = DelayMode.Tape;
                    Time = 420f;
                    ModDepth = 6f;
                    ModRate = 0.6f;
                    Feedback = 0.45f;
                    FilterLow = 20f;
                    FilterHigh = 20000f;
                    Mix = 0.4f;
                    break;
            }
        }

        public override void Reset()
        {
            _standard.Reset();
            _pingPong.Reset();
            _multiTap.Reset();
            _tape.Reset();
        }
    }
}
