using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
using VARCOVoice.DSP;

namespace VARCOVoice.Editor
{
    public sealed class PitchShiftInspector : IEffectInspector
    {
        private readonly EffectDetailController _host;

        public PitchShiftInspector(EffectDetailController host)
        {
            _host = host;
        }

        public Type TargetType => typeof(WSOLAPitchShift);

        public VisualElement CreateUI(IDSPEffect effect)
        {
            if (_host == null || !(effect is WSOLAPitchShift pitch)) return null;
            _host.EnsureKnobStyles();
            _host.BuildPitchShiftUI(pitch, new HashSet<string>());
            return _host.ContentContainer;
        }

        public void OnUpdate() { }

        public void Cleanup() { }
    }
}
