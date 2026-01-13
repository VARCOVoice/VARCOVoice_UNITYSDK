using System;
using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;

namespace VARCOVoice
{
    /// <summary>
    /// Main TTS component for VARCO Voice
    /// </summary>
    [AddComponentMenu("VARCO Voice/Varco TTS")]
    public class VarcoTTS : MonoBehaviour
    {
        #region Singleton
        
        private static VarcoTTS _instance;
        public static VarcoTTS Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<VarcoTTS>();
                    if (_instance == null)
                    {
                        var go = new GameObject("[VarcoTTS]");
                        _instance = go.AddComponent<VarcoTTS>();
                        DontDestroyOnLoad(go);
                    }
                }
                return _instance;
            }
        }
        
        #endregion
        
        #region Inspector Fields
        
        [Header("Configuration")]
        [SerializeField] private VarcoConfig config;
        
        [Header("Default Settings")]
        [SerializeField] private string defaultVoice = "멀더";
        [SerializeField] private Language defaultLanguage = Language.Korean;
        
        [Range(0.5f, 1.5f)]
        [SerializeField] private float defaultSpeed = 1.0f;
        
        [Range(0.5f, 1.5f)]
        [SerializeField] private float defaultPitch = 1.0f;
        
        [Range(8, 20)]
        [SerializeField] private int qualityLevel = 8;
        
        [Header("Playback")]
        [SerializeField] private AudioSource audioSource;
        
        #endregion
        
        #region Properties
        
        public VarcoApiClient ApiClient { get; private set; }
        public bool IsPlaying => audioSource != null && audioSource.isPlaying;
        public AudioClip CurrentClip => audioSource?.clip;
        
        #endregion
        
        #region Events
        
        public event Action<AudioClip> OnSynthesisComplete;
        public event Action<VarcoException> OnError;
        public event Action OnPlaybackStarted;
        public event Action OnPlaybackComplete;
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            Initialize();
        }
        
        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }
        
        #endregion
        
        #region Initialization
        
        private void Initialize()
        {
            // Get or create config
            if (config == null)
            {
                config = VarcoConfig.Instance;
            }
            
            // Create API client
            ApiClient = new VarcoApiClient(config);
            
            // Get or create AudioSource
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                }
            }
        }
        
        #endregion
        
        #region Public API - Simple
        
        /// <summary>
        /// Synthesize text and play immediately
        /// </summary>
        public async UniTask SpeakAsync(string text, CancellationToken cancellationToken = default)
        {
            var clip = await SynthesizeAsync(text, cancellationToken: cancellationToken);
            Play(clip);
        }
        
        /// <summary>
        /// Synthesize text with specific voice and play
        /// </summary>
        public async UniTask SpeakAsync(string text, string voice, CancellationToken cancellationToken = default)
        {
            var clip = await SynthesizeAsync(text, voice, cancellationToken: cancellationToken);
            Play(clip);
        }
        
        /// <summary>
        /// Synthesize text to AudioClip
        /// </summary>
        public async UniTask<AudioClip> SynthesizeAsync(
            string text,
            string voice = null,
            Language? language = null,
            float? speed = null,
            float? pitch = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var clip = await ApiClient.SynthesizeAsync(
                    text,
                    voice ?? defaultVoice,
                    language ?? defaultLanguage,
                    speed ?? defaultSpeed,
                    pitch ?? defaultPitch,
                    qualityLevel,
                    cancellationToken: cancellationToken
                );
                
                OnSynthesisComplete?.Invoke(clip);
                return clip;
            }
            catch (VarcoException ex)
            {
                Debug.LogError($"[VarcoTTS] Synthesis failed: {ex.Message}");
                OnError?.Invoke(ex);
                throw;
            }
        }
        
        /// <summary>
        /// Synthesize using TTSRequest for full control
        /// </summary>
        public async UniTask<AudioClip> SynthesizeAsync(TTSRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                var clip = await ApiClient.SynthesizeAsync(request, cancellationToken);
                OnSynthesisComplete?.Invoke(clip);
                return clip;
            }
            catch (VarcoException ex)
            {
                Debug.LogError($"[VarcoTTS] Synthesis failed: {ex.Message}");
                OnError?.Invoke(ex);
                throw;
            }
        }
        
        #endregion
        
        #region Public API - Playback
        
        /// <summary>
        /// Play an AudioClip
        /// </summary>
        public void Play(AudioClip clip)
        {
            if (audioSource == null) return;
            
            audioSource.clip = clip;
            audioSource.Play();
            OnPlaybackStarted?.Invoke();
            
            // Track completion
            TrackPlaybackCompletion(clip.length).Forget();
        }
        
        /// <summary>
        /// Play one shot (doesn't replace current clip)
        /// </summary>
        public void PlayOneShot(AudioClip clip, float volumeScale = 1f)
        {
            if (audioSource == null) return;
            audioSource.PlayOneShot(clip, volumeScale);
        }
        
        /// <summary>
        /// Stop playback
        /// </summary>
        public void Stop()
        {
            if (audioSource != null)
            {
                audioSource.Stop();
            }
        }
        
        /// <summary>
        /// Pause playback
        /// </summary>
        public void Pause()
        {
            if (audioSource != null)
            {
                audioSource.Pause();
            }
        }
        
        /// <summary>
        /// Resume playback
        /// </summary>
        public void Resume()
        {
            if (audioSource != null)
            {
                audioSource.UnPause();
            }
        }
        
        private async UniTaskVoid TrackPlaybackCompletion(float duration)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(duration), ignoreTimeScale: true);
            OnPlaybackComplete?.Invoke();
        }
        
        #endregion
        
        #region Public API - Voice List
        
        /// <summary>
        /// Get all available voices
        /// </summary>
        public async UniTask<System.Collections.Generic.List<VarcoVoice>> GetVoicesAsync(
            CancellationToken cancellationToken = default)
        {
            return await ApiClient.GetVoicesAsync(cancellationToken: cancellationToken);
        }
        
        /// <summary>
        /// Search voices with filter
        /// </summary>
        public async UniTask<System.Collections.Generic.List<VarcoVoice>> SearchVoicesAsync(
            VoiceFilter filter,
            CancellationToken cancellationToken = default)
        {
            return await ApiClient.SearchVoicesAsync(filter, cancellationToken);
        }
        
        #endregion
        
        #region Settings
        
        /// <summary>
        /// Set default voice
        /// </summary>
        public void SetDefaultVoice(string voice)
        {
            defaultVoice = voice;
        }
        
        /// <summary>
        /// Set default language
        /// </summary>
        public void SetDefaultLanguage(Language language)
        {
            defaultLanguage = language;
        }
        
        /// <summary>
        /// Set default speed
        /// </summary>
        public void SetDefaultSpeed(float speed)
        {
            defaultSpeed = Mathf.Clamp(speed, 0.5f, 1.5f);
        }
        
        /// <summary>
        /// Set default pitch
        /// </summary>
        public void SetDefaultPitch(float pitch)
        {
            defaultPitch = Mathf.Clamp(pitch, 0.5f, 1.5f);
        }
        
        /// <summary>
        /// Set quality level
        /// </summary>
        public void SetQualityLevel(int level)
        {
            qualityLevel = Mathf.Clamp(level, 8, 20);
        }
        
        #endregion
    }
}
