using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
using VARCOVoice.DSP;

namespace VARCOVoice.Editor
{
    public sealed class ReverbInspector : IEffectInspector
    {
        private readonly EffectDetailController _host;

        public ReverbInspector(EffectDetailController host)
        {
            _host = host;
        }

        public Type TargetType => typeof(FDNReverb);

        public VisualElement CreateUI(IDSPEffect effect)
        {
            if (_host == null || !(effect is FDNReverb reverb)) return null;

            _host.EnsureKnobStyles();
            _host.BuildReverbUI(reverb, new HashSet<string>());
            return _host.ContentContainer;
        }

        public void OnUpdate() { }

        public void Cleanup() { }
    }
}
