using System;
using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;
using VARCOVoice.DSP;

namespace VARCOVoice.Audio
{
    /// <summary>
    /// Enhanced AudioSource wrapper with DSP chain and TTS integration
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    [RequireComponent(typeof(DSPChain))]
    [AddComponentMenu("VARCO Voice/Varco Audio Source")]
    public class VarcoAudioSource : MonoBehaviour
    {
        #region Inspector Fields
        
        [Header("TTS Settings")]
        [SerializeField] private string defaultVoice = "멀더";
        [SerializeField] private Language language = Language.Korean;
        
        [Range(0.5f, 1.5f)]
        [SerializeField] private float speed = 1.0f;
        
        [Range(0.5f, 1.5f)]
        [SerializeField] private float pitch = 1.0f;
        
        [Header("Audio Settings")]
        [SerializeField] private bool playOnStart = false;
        [SerializeField] private bool loop = false;
        
        [Header("3D Audio")]
        [SerializeField] private bool enable3D = false;
        [SerializeField] private float maxDistance = 50f;
        [SerializeField] private float minDistance = 1f;
        
        #endregion
        
        #region Components
        
        private AudioSource _audioSource;
        private DSPChain _dspChain;
        private Spatial3DEffect _spatialEffect;
        
        #endregion
        
        #region Properties
        
        public AudioSource AudioSource => _audioSource;
        public DSPChain DSPChain => _dspChain;
        
        public bool IsPlaying => _audioSource != null && _audioSource.isPlaying;
        public AudioClip Clip
        {
            get => _audioSource?.clip;
            set { if (_audioSource != null) _audioSource.clip = value; }
        }
        
        public float Volume
        {
            get => _audioSource?.volume ?? 0f;
            set { if (_audioSource != null) _audioSource.volume = value; }
        }
        
        public string DefaultVoice
        {
            get => defaultVoice;
            set => defaultVoice = value;
        }
        
        public float Speed
        {
            get => speed;
            set => speed = Mathf.Clamp(value, 0.5f, 1.5f);
        }
        
        public float Pitch
        {
            get => pitch;
            set => pitch = Mathf.Clamp(value, 0.5f, 1.5f);
        }
        
        #endregion
        
        #region Events
        
        public event Action OnPlayStarted;
        public event Action OnPlayCompleted;
        public event Action<float> OnPlayProgress; // Normalized progress 0-1
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            _dspChain = GetComponent<DSPChain>();
            
            SetupAudioSource();
            Setup3DAudio();
        }
        
        private void Start()
        {
            if (playOnStart && _audioSource.clip != null)
            {
                Play();
            }
        }
        
        private void Update()
        {
            // Update 3D spatial effect positions
            if (enable3D && _spatialEffect != null)
            {
                _spatialEffect.UpdateFromTransforms(transform, Camera.main?.transform);
            }
            
            // Track progress
            if (IsPlaying && _audioSource.clip != null)
            {
                float progress = _audioSource.time / _audioSource.clip.length;
                OnPlayProgress?.Invoke(progress);
            }
        }
        
        #endregion
        
        #region Setup
        
        private void SetupAudioSource()
        {
            _audioSource.playOnAwake = false;
            _audioSource.loop = loop;
        }
        
        private void Setup3DAudio()
        {
            if (enable3D)
            {
                _audioSource.spatialBlend = 1f;
                _audioSource.maxDistance = maxDistance;
                _audioSource.minDistance = minDistance;
                _audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
                
                // Add spatial effect to DSP chain
                _spatialEffect = _dspChain.GetOrAddEffect<Spatial3DEffect>();
                _spatialEffect.MaxDistance = maxDistance;
                _spatialEffect.MinDistance = minDistance;
            }
            else
            {
                _audioSource.spatialBlend = 0f;
            }
        }
        
        #endregion
        
        #region TTS API
        
        /// <summary>
        /// Synthesize and play text
        /// </summary>
        public async UniTask SpeakAsync(string text, CancellationToken cancellationToken = default)
        {
            var clip = await VarcoTTS.Instance.SynthesizeAsync(
                text, 
                defaultVoice, 
                language, 
                speed, 
                pitch,
                cancellationToken
            );
            
            Play(clip);
        }
        
        /// <summary>
        /// Synthesize and play text with specific voice
        /// </summary>
        public async UniTask SpeakAsync(string text, string voice, CancellationToken cancellationToken = default)
        {
            var clip = await VarcoTTS.Instance.SynthesizeAsync(
                text, 
                voice, 
                language, 
                speed, 
                pitch,
                cancellationToken
            );
            
            Play(clip);
        }
        
        /// <summary>
        /// Synthesize text to clip without playing
        /// </summary>
        public async UniTask<AudioClip> SynthesizeAsync(string text, CancellationToken cancellationToken = default)
        {
            return await VarcoTTS.Instance.SynthesizeAsync(
                text, 
                defaultVoice, 
                language, 
                speed, 
                pitch,
                cancellationToken
            );
        }
        
        #endregion
        
        #region Playback Control
        
        /// <summary>
        /// Play current clip
        /// </summary>
        public void Play()
        {
            if (_audioSource.clip != null)
            {
                _audioSource.Play();
                OnPlayStarted?.Invoke();
                TrackCompletion().Forget();
            }
        }
        
        /// <summary>
        /// Play specific clip
        /// </summary>
        public void Play(AudioClip clip)
        {
            _audioSource.clip = clip;
            Play();
        }
        
        /// <summary>
        /// Play one shot (doesn't replace current clip)
        /// </summary>
        public void PlayOneShot(AudioClip clip, float volumeScale = 1f)
        {
            _audioSource.PlayOneShot(clip, volumeScale);
        }
        
        /// <summary>
        /// Stop playback
        /// </summary>
        public void Stop()
        {
            _audioSource.Stop();
        }
        
        /// <summary>
        /// Pause playback
        /// </summary>
        public void Pause()
        {
            _audioSource.Pause();
        }
        
        /// <summary>
        /// Resume playback
        /// </summary>
        public void Resume()
        {
            _audioSource.UnPause();
        }
        
        private async UniTaskVoid TrackCompletion()
        {
            if (_audioSource.clip == null) return;
            
            await UniTask.Delay(TimeSpan.FromSeconds(_audioSource.clip.length), ignoreTimeScale: true);
            
            if (!loop)
            {
                OnPlayCompleted?.Invoke();
            }
        }
        
        #endregion
        
        #region DSP Effect Shortcuts
        
        /// <summary>
        /// Add pitch shift effect
        /// </summary>
        public PitchShiftEffect AddPitchShift(float semitones = 0f)
        {
            var effect = _dspChain.GetOrAddEffect<PitchShiftEffect>();
            effect.Semitones = semitones;
            return effect;
        }
        
        /// <summary>
        /// Add reverb effect
        /// </summary>
        public ReverbEffect AddReverb(ReverbPreset preset = ReverbPreset.Room)
        {
            var effect = _dspChain.GetOrAddEffect<ReverbEffect>();
            effect.Preset = preset;
            return effect;
        }
        
        /// <summary>
        /// Add EQ effect
        /// </summary>
        public EQEffect AddEQ()
        {
            return _dspChain.GetOrAddEffect<EQEffect>();
        }
        
        /// <summary>
        /// Add low pass filter
        /// </summary>
        public LowPassEffect AddLowPass(float cutoff = 5000f)
        {
            var effect = _dspChain.GetOrAddEffect<LowPassEffect>();
            effect.CutoffFrequency = cutoff;
            return effect;
        }
        
        /// <summary>
        /// Add chorus effect
        /// </summary>
        public ChorusEffect AddChorus()
        {
            return _dspChain.GetOrAddEffect<ChorusEffect>();
        }
        
        /// <summary>
        /// Clear all DSP effects
        /// </summary>
        public void ClearEffects()
        {
            _dspChain.ClearEffects();
            
            // Re-add spatial effect if 3D is enabled
            if (enable3D)
            {
                Setup3DAudio();
            }
        }
        
        #endregion
        
        #region Presets
        
        /// <summary>
        /// Apply robot voice effect
        /// </summary>
        public void ApplyRobotVoice()
        {
            ClearEffects();
            AddPitchShift(-3);
            AddChorus();
            var eq = AddEQ();
            eq.Treble = 8f;
            eq.Bass = -5f;
        }
        
        /// <summary>
        /// Apply radio/walkie-talkie effect
        /// </summary>
        public void ApplyRadioVoice()
        {
            ClearEffects();
            AddLowPass(3000f);
            var eq = AddEQ();
            eq.Bass = -15f;
            eq.Mid = 5f;
        }
        
        /// <summary>
        /// Apply cave/cavern effect
        /// </summary>
        public void ApplyCaveVoice()
        {
            ClearEffects();
            AddReverb(ReverbPreset.Cave);
            AddPitchShift(-2);
        }
        
        /// <summary>
        /// Apply underwater effect
        /// </summary>
        public void ApplyUnderwaterVoice()
        {
            ClearEffects();
            AddLowPass(800f);
            AddReverb(ReverbPreset.Underwater);
            AddChorus();
        }
        
        /// <summary>
        /// Apply ghost/spooky effect
        /// </summary>
        public void ApplyGhostVoice()
        {
            ClearEffects();
            AddPitchShift(-5);
            AddReverb(ReverbPreset.Church);
            var chorus = AddChorus();
            chorus.Depth = 8f;
            chorus.Rate = 0.3f;
        }
        
        #endregion
    }
}
