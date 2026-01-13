using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
using VARCOVoice.DSP;

namespace VARCOVoice.Editor
{
    public sealed class PhaserInspector : IEffectInspector
    {
        private readonly EffectDetailController _host;

        public PhaserInspector(EffectDetailController host)
        {
            _host = host;
        }

        public Type TargetType => typeof(PhaserEffect);

        public VisualElement CreateUI(IDSPEffect effect)
        {
            if (_host == null || !(effect is PhaserEffect phaser)) return null;
            _host.EnsureKnobStyles();
            _host.BuildModulationUI(
                phaser,
                "Phaser",
                phaser.Rate,
                phaser.Depth,
                phaser.Mix,
                phaser.Feedback,
                null,
                new HashSet<string>());
            return _host.ContentContainer;
        }

        public void OnUpdate() { }

        public void Cleanup() { }
    }
}
