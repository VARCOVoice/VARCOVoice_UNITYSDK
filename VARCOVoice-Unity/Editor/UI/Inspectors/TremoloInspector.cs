using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
using VARCOVoice.DSP;

namespace VARCOVoice.Editor
{
    public sealed class TremoloInspector : IEffectInspector
    {
        private readonly EffectDetailController _host;

        public TremoloInspector(EffectDetailController host)
        {
            _host = host;
        }

        public Type TargetType => typeof(TremoloEffect);

        public VisualElement CreateUI(IDSPEffect effect)
        {
            if (_host == null || !(effect is TremoloEffect tremolo)) return null;
            _host.EnsureKnobStyles();
            _host.BuildTremoloUI(tremolo, new HashSet<string>());
            return _host.ContentContainer;
        }

        public void OnUpdate() { }

        public void Cleanup() { }
    }
}
