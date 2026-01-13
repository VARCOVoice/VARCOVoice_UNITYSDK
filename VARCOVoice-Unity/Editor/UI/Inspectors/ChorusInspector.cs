using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
using VARCOVoice.DSP;

namespace VARCOVoice.Editor
{
    public sealed class ChorusInspector : IEffectInspector
    {
        private readonly EffectDetailController _host;

        public ChorusInspector(EffectDetailController host)
        {
            _host = host;
        }

        public Type TargetType => typeof(ChorusEffect);

        public VisualElement CreateUI(IDSPEffect effect)
        {
            if (_host == null || !(effect is ChorusEffect chorus)) return null;
            _host.EnsureKnobStyles();
            _host.BuildModulationUI(
                chorus,
                "Chorus",
                chorus.Rate,
                chorus.Depth,
                chorus.Mix,
                null,
                null,
                new HashSet<string>());
            return _host.ContentContainer;
        }

        public void OnUpdate() { }

        public void Cleanup() { }
    }
}
