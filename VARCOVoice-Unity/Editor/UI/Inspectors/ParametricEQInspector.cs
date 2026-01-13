using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
using VARCOVoice.DSP;

namespace VARCOVoice.Editor
{
    public sealed class ParametricEQInspector : IEffectInspector
    {
        private readonly EffectDetailController _host;

        public ParametricEQInspector(EffectDetailController host)
        {
            _host = host;
        }

        public Type TargetType => typeof(ParametricEQ16);

        public VisualElement CreateUI(IDSPEffect effect)
        {
            if (_host == null || !(effect is ParametricEQ16 eq)) return null;

            _host.EnsureKnobStyles();
            _host.BuildParametricEQUI(eq, new HashSet<string>());
            return _host.ContentContainer;
        }

        public void OnUpdate() { }

        public void Cleanup() { }
    }
}
