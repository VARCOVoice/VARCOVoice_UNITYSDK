using UnityEngine;
using VARCOVoice.Audio;
using VARCOVoice.DSP;
using Cysharp.Threading.Tasks;

namespace VARCOVoice.Samples
{
    /// <summary>
    /// DSP Effects demonstration
    /// </summary>
    public class DSPEffectsExample : MonoBehaviour
    {
        [Header("Audio")]
        [SerializeField] private VarcoAudioSource varcoAudio;
        
        [Header("Demo")]
        [SerializeField] private string demoText = "안녕하세요! DSP 이펙트 테스트입니다.";
        [SerializeField] private string demoVoice = "멀더";
        
        private void Start()
        {
            if (varcoAudio == null)
            {
                varcoAudio = gameObject.AddComponent<VarcoAudioSource>();
            }
        }
        
        // Pitch controls
        public void SetPitchUp() => SetPitch(3);
        public void SetPitchDown() => SetPitch(-3);
        public void SetPitchNormal() => SetPitch(0);
        
        private void SetPitch(float semitones)
        {
            varcoAudio.ClearEffects();
            varcoAudio.AddPitchShift(semitones);
            Speak();
        }
        
        // Reverb presets
        public void SetReverbRoom() => SetReverb(ReverbPreset.Room);
        public void SetReverbHall() => SetReverb(ReverbPreset.Hall);
        public void SetReverbCave() => SetReverb(ReverbPreset.Cave);
        public void SetReverbChurch() => SetReverb(ReverbPreset.Church);
        
        private void SetReverb(ReverbPreset preset)
        {
            varcoAudio.ClearEffects();
            varcoAudio.AddReverb(preset);
            Speak();
        }
        
        // Filter controls
        public void SetLowPassHigh() => SetLowPass(8000f);
        public void SetLowPassMedium() => SetLowPass(3000f);
        public void SetLowPassLow() => SetLowPass(1000f);
        
        private void SetLowPass(float cutoff)
        {
            varcoAudio.ClearEffects();
            varcoAudio.AddLowPass(cutoff);
            Speak();
        }
        
        // Voice presets
        public void ApplyRobotPreset()
        {
            varcoAudio.ApplyRobotVoice();
            Speak();
        }
        
        public void ApplyRadioPreset()
        {
            varcoAudio.ApplyRadioVoice();
            Speak();
        }
        
        public void ApplyCavePreset()
        {
            varcoAudio.ApplyCaveVoice();
            Speak();
        }
        
        public void ApplyUnderwaterPreset()
        {
            varcoAudio.ApplyUnderwaterVoice();
            Speak();
        }
        
        public void ApplyGhostPreset()
        {
            varcoAudio.ApplyGhostVoice();
            Speak();
        }
        
        public void ClearAllEffects()
        {
            varcoAudio.ClearEffects();
            Speak();
        }
        
        private void Speak()
        {
            varcoAudio.DefaultVoice = demoVoice;
            varcoAudio.SpeakAsync(demoText).Forget();
        }
        
        public void Stop()
        {
            varcoAudio.Stop();
        }
    }
}
