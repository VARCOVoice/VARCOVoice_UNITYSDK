using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
using VARCOVoice.DSP;

namespace VARCOVoice.Editor
{
    public sealed class SaturationInspector : IEffectInspector
    {
        private readonly EffectDetailController _host;

        public SaturationInspector(EffectDetailController host)
        {
            _host = host;
        }

        public Type TargetType => typeof(SaturationEffect);

        public VisualElement CreateUI(IDSPEffect effect)
        {
            if (_host == null || !(effect is SaturationEffect saturation)) return null;
            _host.EnsureKnobStyles();
            _host.BuildSaturationUI(saturation, new HashSet<string>());
            return _host.ContentContainer;
        }

        public void OnUpdate() { }

        public void Cleanup() { }
    }
}
