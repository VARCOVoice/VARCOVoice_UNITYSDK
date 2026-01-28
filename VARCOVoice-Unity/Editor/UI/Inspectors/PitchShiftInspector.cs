using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
using VARCOVoice.DSP;
using VARCOVoice.Editor.UI.Components;

namespace VARCOVoice.Editor
{
    public sealed class PitchShiftInspector : IEffectInspector
    {
        private readonly EffectDetailController _host;

        public PitchShiftInspector(EffectDetailController host)
        {
            _host = host;
        }

        public Type TargetType => typeof(PitchShift);

        public VisualElement CreateUI(IDSPEffect effect)
        {
            if (_host == null || !(effect is PitchShift pitch)) return null;
            _host.EnsureKnobStyles();
            _host.BuildPitchShiftUI(pitch, new HashSet<string>());
            return _host.ContentContainer;
        }

        public void OnUpdate() { }

        public void Cleanup() { }
    }
}
