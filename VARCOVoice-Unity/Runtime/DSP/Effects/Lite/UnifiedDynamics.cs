using UnityEngine;

namespace VARCOVoice.DSP
{
    /// <summary>
    /// Unified dynamics processor with selectable modes.
    /// </summary>
    [System.Serializable]
    public class UnifiedDynamics : DSPEffectBase
    {
        public override string Name => "Dynamics";

        public enum DynamicsMode { Compressor, Limiter, Gate, Expander }

        public DynamicsMode Mode { get; set; } = DynamicsMode.Compressor;

        public float Threshold { get; set; } = -20f;
        public float Attack { get; set; } = 10f;
        public float Release { get; set; } = 200f;

        public float Ratio { get; set; } = 4f;
        public float Knee { get; set; } = 6f;
        public float MakeupGain { get; set; } = 0f;

        public float Ceiling { get; set; } = -0.3f;
        [field: Range(-80f, 0f)]
        public float Range { get; set; } = -80f;

        public float Hold { get; set; } = 50f;
        public float Lookahead { get; set; } = 5f;
        public bool TruePeak { get; set; } = true;
        public bool AutoMakeup { get; set; } = true;
        public float SidechainHPF { get; set; } = 20f;

        public float CurrentInput { get; private set; } = -100f;
        public float CurrentOutput { get; private set; } = -100f;
        public float CurrentGainReduction { get; private set; }

        private readonly CompressorEffect _compressor = new();
        private readonly LimiterEffect _limiter = new();
        private readonly GateEffect _gate = new();
        private readonly ExpanderEffect _expander = new();

        public override void Process(float[] data, int channels, int sampleRate)
        {
            switch (Mode)
            {
                case DynamicsMode.Limiter:
                    ApplyToLimiter();
                    _limiter.Process(data, channels, sampleRate);
                    CurrentGainReduction = _limiter.CurrentGainReduction;
                    break;
                case DynamicsMode.Gate:
                    ApplyToGate();
                    _gate.Process(data, channels, sampleRate);
                    CurrentGainReduction = 0f;
                    break;
                case DynamicsMode.Expander:
                    ApplyToExpander();
                    _expander.Process(data, channels, sampleRate);
                    CurrentGainReduction = 0f;
                    break;
                default:
                    ApplyToCompressor();
                    _compressor.Process(data, channels, sampleRate);
                    CurrentInput = _compressor.CurrentInput;
                    CurrentOutput = _compressor.CurrentOutput;
                    CurrentGainReduction = _compressor.CurrentGainReduction;
                    break;
            }
        }

        public override void ApplyPreset(string presetName)
        {
            if (string.IsNullOrEmpty(presetName)) return;

            switch (presetName.Trim().ToLowerInvariant())
            {
                case "vocal warmth":
                case "podcast master":
                case "transparent":
                case "punchy":
                case "glue":
                case "de-breath":
                case "safety limiter":
                    Mode = DynamicsMode.Compressor;
                    _compressor.ApplyPreset(presetName);
                    SyncFromCompressor();
                    break;
                case "mastering":
                case "podcast":
                case "safety":
                case "loud":
                    Mode = DynamicsMode.Limiter;
                    _limiter.ApplyPreset(presetName);
                    SyncFromLimiter();
                    break;
                case "voice tight":
                case "noise clean":
                case "fast cut":
                case "room gate":
                    Mode = DynamicsMode.Gate;
                    _gate.ApplyPreset(presetName);
                    SyncFromGate();
                    break;
                case "gentle":
                case "de-noise":
                case "broadcast":
                case "pump":
                    Mode = DynamicsMode.Expander;
                    _expander.ApplyPreset(presetName);
                    SyncFromExpander();
                    break;
            }
        }

        public override void Reset()
        {
            _compressor.Reset();
            _limiter.Reset();
            _gate.Reset();
            _expander.Reset();
            CurrentInput = -100f;
            CurrentOutput = -100f;
            CurrentGainReduction = 0f;
        }

        private void ApplyToCompressor()
        {
            _compressor.Threshold = Threshold;
            _compressor.Ratio = Ratio;
            _compressor.Attack = Attack;
            _compressor.Release = Release;
            _compressor.Knee = Knee;
            _compressor.MakeupGain = MakeupGain;
            _compressor.AutoMakeup = AutoMakeup;
            _compressor.SidechainHPF = SidechainHPF;
            _compressor.Mix = Mix;
        }

        private void SyncFromCompressor()
        {
            Threshold = _compressor.Threshold;
            Ratio = _compressor.Ratio;
            Attack = _compressor.Attack;
            Release = _compressor.Release;
            Knee = _compressor.Knee;
            MakeupGain = _compressor.MakeupGain;
            AutoMakeup = _compressor.AutoMakeup;
            SidechainHPF = _compressor.SidechainHPF;
            Mix = _compressor.Mix;
        }

        private void ApplyToLimiter()
        {
            _limiter.Ceiling = Ceiling;
            _limiter.Release = Release;
            _limiter.Lookahead = Lookahead;
            _limiter.TruePeak = TruePeak;
            _limiter.Mix = Mix;
        }

        private void SyncFromLimiter()
        {
            Ceiling = _limiter.Ceiling;
            Release = _limiter.Release;
            Lookahead = _limiter.Lookahead;
            TruePeak = _limiter.TruePeak;
            Mix = _limiter.Mix;
        }

        private void ApplyToGate()
        {
            _gate.Threshold = Threshold;
            _gate.Attack = Attack;
            _gate.Hold = Hold;
            _gate.Release = Release;
            _gate.Range = Range;
            _gate.Mix = Mix;
        }

        private void SyncFromGate()
        {
            Threshold = _gate.Threshold;
            Attack = _gate.Attack;
            Hold = _gate.Hold;
            Release = _gate.Release;
            Range = _gate.Range;
            Mix = _gate.Mix;
        }

        private void ApplyToExpander()
        {
            _expander.Threshold = Threshold;
            _expander.Ratio = Ratio;
            _expander.Attack = Attack;
            _expander.Release = Release;
            _expander.Knee = Knee;
            _expander.Mix = Mix;
        }

        private void SyncFromExpander()
        {
            Threshold = _expander.Threshold;
            Ratio = _expander.Ratio;
            Attack = _expander.Attack;
            Release = _expander.Release;
            Knee = _expander.Knee;
            Mix = _expander.Mix;
        }
    }
}
