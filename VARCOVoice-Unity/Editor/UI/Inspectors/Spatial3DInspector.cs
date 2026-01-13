using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
using VARCOVoice.DSP;

namespace VARCOVoice.Editor
{
    public sealed class Spatial3DInspector : IEffectInspector
    {
        private readonly EffectDetailController _host;

        public Spatial3DInspector(EffectDetailController host)
        {
            _host = host;
        }

        public Type TargetType => typeof(Spatial3DEffect);

        public VisualElement CreateUI(IDSPEffect effect)
        {
            if (_host == null || !(effect is Spatial3DEffect spatial)) return null;
            _host.EnsureKnobStyles();
            _host.BuildSpatial3DUI(spatial, new HashSet<string>());
            return _host.ContentContainer;
        }

        public void OnUpdate() { }

        public void Cleanup() { }
    }
}
