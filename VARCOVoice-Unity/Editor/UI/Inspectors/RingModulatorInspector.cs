using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
using VARCOVoice.DSP;

namespace VARCOVoice.Editor
{
    public sealed class RingModulatorInspector : IEffectInspector
    {
        private readonly EffectDetailController _host;

        public RingModulatorInspector(EffectDetailController host)
        {
            _host = host;
        }

        public Type TargetType => typeof(RingModulatorEffect);

        public VisualElement CreateUI(IDSPEffect effect)
        {
            if (_host == null || !(effect is RingModulatorEffect ringMod)) return null;
            _host.EnsureKnobStyles();
            _host.BuildRingModUI(ringMod, new HashSet<string>());
            return _host.ContentContainer;
        }

        public void OnUpdate() { }

        public void Cleanup() { }
    }
}
