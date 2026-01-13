using System;
using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;

namespace VARCOVoice
{
    /// <summary>
    /// Voice Conversion component for VARCO Voice
    /// </summary>
    [AddComponentMenu("VARCO Voice/Varco VC")]
    public class VarcoVC : MonoBehaviour
    {
        #region Inspector Fields
        
        [Header("Configuration")]
        [SerializeField] private VarcoConfig config;
        
        [Header("Default Settings")]
        [SerializeField] private string defaultTargetVoice = "멀더";
        
        [Header("Playback")]
        [SerializeField] private AudioSource audioSource;
        
        #endregion
        
        #region Properties
        
        public VarcoApiClient ApiClient { get; private set; }
        public bool IsPlaying => audioSource != null && audioSource.isPlaying;
        
        #endregion
        
        #region Events
        
        public event Action<AudioClip> OnConversionComplete;
        public event Action<VarcoException> OnError;
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            Initialize();
        }
        
        #endregion
        
        #region Initialization
        
        private void Initialize()
        {
            if (config == null)
            {
                config = VarcoConfig.Instance;
            }
            
            ApiClient = new VarcoApiClient(config);
            
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
        
        #region Public API - Voice Conversion
        
        /// <summary>
        /// Convert audio clip to target voice
        /// </summary>
        public async UniTask<AudioClip> ConvertAsync(
            AudioClip sourceClip,
            string targetVoice = null,
            CancellationToken cancellationToken = default)
        {
            var audioData = AudioClipToWav(sourceClip);
            return await ConvertAsync(audioData, targetVoice, cancellationToken);
        }
        
        /// <summary>
        /// Convert audio bytes to target voice
        /// </summary>
        public async UniTask<AudioClip> ConvertAsync(
            byte[] audioData,
            string targetVoice = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var clip = await ApiClient.ConvertVoiceAsync(
                    audioData,
                    targetVoice ?? defaultTargetVoice,
                    cancellationToken: cancellationToken
                );
                
                OnConversionComplete?.Invoke(clip);
                return clip;
            }
            catch (VarcoException ex)
            {
                Debug.LogError($"[VarcoVC] Conversion failed: {ex.Message}");
                OnError?.Invoke(ex);
                throw;
            }
        }
        
        /// <summary>
        /// Convert using custom reference voice
        /// </summary>
        public async UniTask<AudioClip> ConvertWithReferenceAsync(
            AudioClip sourceClip,
            AudioClip referenceClip,
            CancellationToken cancellationToken = default)
        {
            var sourceData = AudioClipToWav(sourceClip);
            var referenceData = AudioClipToWav(referenceClip);
            
            try
            {
                var clip = await ApiClient.ConvertVoiceCustomAsync(
                    sourceData,
                    referenceData,
                    cancellationToken: cancellationToken
                );
                
                OnConversionComplete?.Invoke(clip);
                return clip;
            }
            catch (VarcoException ex)
            {
                Debug.LogError($"[VarcoVC] Custom conversion failed: {ex.Message}");
                OnError?.Invoke(ex);
                throw;
            }
        }
        
        /// <summary>
        /// Convert and play immediately
        /// </summary>
        public async UniTask ConvertAndPlayAsync(
            AudioClip sourceClip,
            string targetVoice = null,
            CancellationToken cancellationToken = default)
        {
            var clip = await ConvertAsync(sourceClip, targetVoice, cancellationToken);
            Play(clip);
        }
        
        #endregion
        
        #region Playback
        
        public void Play(AudioClip clip)
        {
            if (audioSource == null) return;
            audioSource.clip = clip;
            audioSource.Play();
        }
        
        public void Stop()
        {
            if (audioSource != null)
            {
                audioSource.Stop();
            }
        }
        
        #endregion
        
        #region Audio Conversion Utilities
        
        /// <summary>
        /// Convert AudioClip to WAV bytes
        /// </summary>
        private byte[] AudioClipToWav(AudioClip clip)
        {
            var samples = new float[clip.samples * clip.channels];
            clip.GetData(samples, 0);
            
            return EncodeAsWav(samples, clip.channels, clip.frequency);
        }
        
        /// <summary>
        /// Encode float samples as WAV
        /// </summary>
        private byte[] EncodeAsWav(float[] samples, int channels, int sampleRate)
        {
            var stream = new System.IO.MemoryStream();
            var writer = new System.IO.BinaryWriter(stream);
            
            int bitsPerSample = 16;
            int byteRate = sampleRate * channels * bitsPerSample / 8;
            int blockAlign = channels * bitsPerSample / 8;
            int dataSize = samples.Length * bitsPerSample / 8;
            
            // RIFF header
            writer.Write(new char[] { 'R', 'I', 'F', 'F' });
            writer.Write(36 + dataSize);
            writer.Write(new char[] { 'W', 'A', 'V', 'E' });
            
            // fmt chunk
            writer.Write(new char[] { 'f', 'm', 't', ' ' });
            writer.Write(16);  // chunk size
            writer.Write((short)1);  // PCM
            writer.Write((short)channels);
            writer.Write(sampleRate);
            writer.Write(byteRate);
            writer.Write((short)blockAlign);
            writer.Write((short)bitsPerSample);
            
            // data chunk
            writer.Write(new char[] { 'd', 'a', 't', 'a' });
            writer.Write(dataSize);
            
            // Write samples
            foreach (var sample in samples)
            {
                writer.Write((short)(sample * 32767));
            }
            
            writer.Flush();
            return stream.ToArray();
        }
        
        #endregion
        
        #region Settings
        
        public void SetDefaultTargetVoice(string voice)
        {
            defaultTargetVoice = voice;
        }
        
        #endregion
    }
}
