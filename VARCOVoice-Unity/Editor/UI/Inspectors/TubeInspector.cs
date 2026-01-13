using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
using VARCOVoice.DSP;

namespace VARCOVoice.Editor
{
    public sealed class TubeInspector : IEffectInspector
    {
        private readonly EffectDetailController _host;

        public TubeInspector(EffectDetailController host)
        {
            _host = host;
        }

        public Type TargetType => typeof(TubeEmulation);

        public VisualElement CreateUI(IDSPEffect effect)
        {
            if (_host == null || !(effect is TubeEmulation tube)) return null;
            _host.EnsureKnobStyles();
            _host.BuildTubeUI(tube, new HashSet<string>());
            return _host.ContentContainer;
        }

        public void OnUpdate() { }

        public void Cleanup() { }
    }
}
