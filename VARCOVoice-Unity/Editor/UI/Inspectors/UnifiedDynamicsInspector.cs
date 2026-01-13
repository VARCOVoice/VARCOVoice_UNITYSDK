using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
using VARCOVoice.DSP;

namespace VARCOVoice.Editor
{
    public sealed class UnifiedDynamicsInspector : IEffectInspector
    {
        private readonly EffectDetailController _host;

        public UnifiedDynamicsInspector(EffectDetailController host)
        {
            _host = host;
        }

        public Type TargetType => typeof(UnifiedDynamics);

        public VisualElement CreateUI(IDSPEffect effect)
        {
            if (_host == null || !(effect is UnifiedDynamics dynamics)) return null;
            _host.EnsureKnobStyles();
            _host.BuildDynamics3ZoneUI(dynamics, new HashSet<string>());
            return _host.ContentContainer;
        }

        public void OnUpdate() { }

        public void Cleanup() { }
    }
}
