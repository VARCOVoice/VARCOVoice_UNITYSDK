using System;
using UnityEngine.UIElements;
using VARCOVoice.DSP;

namespace VARCOVoice.Editor
{
    public interface IEffectInspector
    {
        Type TargetType { get; }
        VisualElement CreateUI(IDSPEffect effect);
        void OnUpdate();
        void Cleanup();
    }
}
