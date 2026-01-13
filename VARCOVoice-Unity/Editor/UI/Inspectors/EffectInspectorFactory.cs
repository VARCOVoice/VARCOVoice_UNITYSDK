using System;
using VARCOVoice.DSP;

namespace VARCOVoice.Editor
{
    public static class EffectInspectorFactory
    {
        private static readonly (Type TargetType, Func<EffectDetailController, IEffectInspector> Factory)[] Inspectors =
        {
            (typeof(UnifiedDynamics), host => new UnifiedDynamicsInspector(host)),
            (typeof(UnifiedDelay), host => new UnifiedDelayInspector(host)),
            (typeof(ChorusEffect), host => new ChorusInspector(host)),
            (typeof(PhaserEffect), host => new PhaserInspector(host)),
            (typeof(FlangerEffect), host => new FlangerInspector(host)),
            (typeof(WSOLAPitchShift), host => new PitchShiftInspector(host)),
            (typeof(TubeEmulation), host => new TubeInspector(host)),
            (typeof(DistortionEffect), host => new DistortionInspector(host)),
            (typeof(SaturationEffect), host => new SaturationInspector(host)),
            (typeof(TapeEmulation), host => new TapeEmulationInspector(host)),
            (typeof(TremoloEffect), host => new TremoloInspector(host)),
            (typeof(RingModulatorEffect), host => new RingModulatorInspector(host)),
            (typeof(Spatial3DEffect), host => new Spatial3DInspector(host)),
            (typeof(ParametricEQ16), host => new ParametricEQInspector(host)),
            (typeof(FDNReverb), host => new ReverbInspector(host))
        };

        public static IEffectInspector Create(EffectDetailController host, IDSPEffect effect)
        {
            if (host == null || effect == null) return null;

            var effectType = effect.GetType();
            foreach (var (targetType, factory) in Inspectors)
            {
                if (targetType.IsAssignableFrom(effectType))
                    return factory(host);
            }

            return new GenericEffectInspector(host);
        }
    }
}
