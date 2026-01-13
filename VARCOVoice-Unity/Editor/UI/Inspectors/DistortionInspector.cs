using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
using VARCOVoice.DSP;

namespace VARCOVoice.Editor
{
    public sealed class DistortionInspector : IEffectInspector
    {
        private readonly EffectDetailController _host;

        public DistortionInspector(EffectDetailController host)
        {
            _host = host;
        }

        public Type TargetType => typeof(DistortionEffect);

        public VisualElement CreateUI(IDSPEffect effect)
        {
            if (_host == null || !(effect is DistortionEffect distortion)) return null;
            _host.EnsureKnobStyles();
            _host.BuildDistortionUI(distortion, new HashSet<string>());
            return _host.ContentContainer;
        }

        public void OnUpdate() { }

        public void Cleanup() { }
    }
}
