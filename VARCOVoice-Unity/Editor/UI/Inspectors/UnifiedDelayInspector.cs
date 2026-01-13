using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
using VARCOVoice.DSP;

namespace VARCOVoice.Editor
{
    public sealed class UnifiedDelayInspector : IEffectInspector
    {
        private readonly EffectDetailController _host;

        public UnifiedDelayInspector(EffectDetailController host)
        {
            _host = host;
        }

        public Type TargetType => typeof(UnifiedDelay);

        public VisualElement CreateUI(IDSPEffect effect)
        {
            if (_host == null || !(effect is UnifiedDelay delay)) return null;
            _host.EnsureKnobStyles();
            _host.BuildDelayUI(delay, new HashSet<string>());
            return _host.ContentContainer;
        }

        public void OnUpdate() { }

        public void Cleanup() { }
    }
}
