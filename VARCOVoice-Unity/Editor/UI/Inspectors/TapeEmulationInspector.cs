using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
using VARCOVoice.DSP;

namespace VARCOVoice.Editor
{
    public sealed class TapeEmulationInspector : IEffectInspector
    {
        private readonly EffectDetailController _host;

        public TapeEmulationInspector(EffectDetailController host)
        {
            _host = host;
        }

        public Type TargetType => typeof(TapeEmulation);

        public VisualElement CreateUI(IDSPEffect effect)
        {
            if (_host == null || !(effect is TapeEmulation tape)) return null;
            _host.EnsureKnobStyles();
            _host.BuildTapeUI(tape, new HashSet<string>());
            return _host.ContentContainer;
        }

        public void OnUpdate() { }

        public void Cleanup() { }
    }
}
