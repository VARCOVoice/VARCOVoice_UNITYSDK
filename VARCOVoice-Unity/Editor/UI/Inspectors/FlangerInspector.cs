using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
using VARCOVoice.DSP;

namespace VARCOVoice.Editor
{
    public sealed class FlangerInspector : IEffectInspector
    {
        private readonly EffectDetailController _host;

        public FlangerInspector(EffectDetailController host)
        {
            _host = host;
        }

        public Type TargetType => typeof(FlangerEffect);

        public VisualElement CreateUI(IDSPEffect effect)
        {
            if (_host == null || !(effect is FlangerEffect flanger)) return null;
            _host.EnsureKnobStyles();
            _host.BuildModulationUI(
                flanger,
                "Flanger",
                flanger.Rate,
                flanger.Depth,
                flanger.Mix,
                flanger.Feedback,
                flanger.BaseDelay,
                new HashSet<string>());
            return _host.ContentContainer;
        }

        public void OnUpdate() { }

        public void Cleanup() { }
    }
}
