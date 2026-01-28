using System;
using UnityEngine.UIElements;
using VARCOVoice.DSP;

namespace VARCOVoice.Editor
{
    public sealed class GenericEffectInspector : IEffectInspector
    {
        private readonly EffectDetailController _host;

        public GenericEffectInspector(EffectDetailController host)
        {
            _host = host;
        }

        public Type TargetType => typeof(IDSPEffect);

        public VisualElement CreateUI(IDSPEffect effect)
        {
            if (_host == null || effect == null) return null;
            _host.BuildParameterUI(effect);
            return _host.ContentContainer;
        }

        public void OnUpdate() { }

        public void Cleanup() { }
    }
}
