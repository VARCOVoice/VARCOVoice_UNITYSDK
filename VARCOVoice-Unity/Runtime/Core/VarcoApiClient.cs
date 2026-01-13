using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace VARCOVoice
{
    /// <summary>
    /// TTS synthesis request
    /// </summary>
    [Serializable]
    public class TTSRequest
    {
        [JsonProperty("text")]
        public string Text { get; set; }
        
        [JsonProperty("voice")]
        public string Voice { get; set; }
        
        [JsonProperty("language")]
        public string Language { get; set; } = "korean";
        
        [JsonProperty("properties")]
        public TTSProperties Properties { get; set; }
        
        [JsonProperty("n_fm_steps")]
        public int QualityLevel { get; set; } = 8;
        
        [JsonProperty("seed")]
        public int Seed { get; set; } = -1;
        
        [JsonProperty("return_metadata")]
        public bool ReturnMetadata { get; set; } = false;
    }
    
    /// <summary>
    /// TTS properties (speed, pitch)
    /// </summary>
    [Serializable]
    public class TTSProperties
    {
        [JsonProperty("speed")]
        public float Speed { get; set; } = 1.0f;
        
        [JsonProperty("pitch")]
        public float Pitch { get; set; } = 1.0f;
    }
    
    /// <summary>
    /// TTS synthesis response
    /// </summary>
    [Serializable]
    public class TTSResponse
    {
        [JsonProperty("audio")]
        public string Audio { get; set; }
        
        [JsonProperty("ssml")]
        public string SSML { get; set; }
        
        [JsonProperty("metadata")]
        public string Metadata { get; set; }
    }
    
    /// <summary>
    /// Voice Conversion request
    /// </summary>
    [Serializable]
    public class VCRequest
    {
        [JsonProperty("audio")]
        public string Audio { get; set; }  // Base64 encoded
        
        [JsonProperty("audio_name")]
        public string AudioName { get; set; }
        
        [JsonProperty("speaker_name")]
        public string SpeakerName { get; set; }
    }
    
    /// <summary>
    /// Custom Voice Conversion request
    /// </summary>
    [Serializable]
    public class VCCustomRequest : VCRequest
    {
        [JsonProperty("speaker_audio")]
        public string SpeakerAudio { get; set; }  // Base64 encoded reference audio
    }
    
    /// <summary>
    /// VARCO Voice API Client
    /// </summary>
    public class VarcoApiClient
    {
        private readonly VarcoConfig _config;
        private readonly int _maxRetries;
        private readonly float _retryDelaySeconds;
        
        // Cached voice list
        private List<VarcoVoice> _cachedVoices;
        private DateTime _voicesCacheTime;
        private readonly TimeSpan _voicesCacheDuration = TimeSpan.FromHours(1);
        
        public VarcoApiClient(VarcoConfig config = null, int maxRetries = 3, float retryDelaySeconds = 1f)
        {
            _config = config ?? VarcoConfig.Instance;
            _maxRetries = maxRetries;
            _retryDelaySeconds = retryDelaySeconds;
        }
        
        #region TTS API
        
        /// <summary>
        /// Synthesize text to speech
        /// </summary>
        public async UniTask<AudioClip> SynthesizeAsync(
            string text,
            string voice = null,
            Language? language = null,
            float? speed = null,
            float? pitch = null,
            int? qualityLevel = null,
            int seed = -1,
            CancellationToken cancellationToken = default)
        {
            ValidateConfig();
            ValidateText(text);
            
            var request = new TTSRequest
            {
                Text = text,
                Voice = voice ?? _config.DefaultVoice,
                Language = (language ?? _config.DefaultLanguage).ToApiString(),
                Properties = new TTSProperties
                {
                    Speed = speed ?? _config.DefaultSpeed,
                    Pitch = pitch ?? _config.DefaultPitch
                },
                QualityLevel = qualityLevel ?? _config.QualityLevel,
                Seed = seed
            };
            
            var response = await PostAsync<TTSResponse>(_config.TTSEndpoint, request, cancellationToken);
            
            return DecodeAudioClip(response.Audio, $"tts_{text.GetHashCode()}");
        }
        
        /// <summary>
        /// Synthesize with TTSRequest object
        /// </summary>
        public async UniTask<AudioClip> SynthesizeAsync(TTSRequest request, CancellationToken cancellationToken = default)
        {
            ValidateConfig();
            ValidateText(request.Text);
            
            var response = await PostAsync<TTSResponse>(_config.TTSEndpoint, request, cancellationToken);
            
            return DecodeAudioClip(response.Audio, $"tts_{request.Text.GetHashCode()}");
        }
        
        /// <summary>
        /// Get raw audio bytes instead of AudioClip
        /// </summary>
        public async UniTask<byte[]> SynthesizeBytesAsync(
            string text,
            string voice = null,
            Language? language = null,
            float? speed = null,
            float? pitch = null,
            CancellationToken cancellationToken = default)
        {
            ValidateConfig();
            ValidateText(text);
            
            var request = new TTSRequest
            {
                Text = text,
                Voice = voice ?? _config.DefaultVoice,
                Language = (language ?? _config.DefaultLanguage).ToApiString(),
                Properties = new TTSProperties
                {
                    Speed = speed ?? _config.DefaultSpeed,
                    Pitch = pitch ?? _config.DefaultPitch
                },
                QualityLevel = _config.QualityLevel
            };
            
            var response = await PostAsync<TTSResponse>(_config.TTSEndpoint, request, cancellationToken);
            
            return Convert.FromBase64String(response.Audio);
        }
        
        #endregion
        
        #region Voice Conversion API
        
        /// <summary>
        /// Convert voice using pre-trained speaker
        /// </summary>
        public async UniTask<AudioClip> ConvertVoiceAsync(
            byte[] audioData,
            string speakerName,
            string audioFileName = "input.wav",
            CancellationToken cancellationToken = default)
        {
            ValidateConfig();
            
            var request = new VCRequest
            {
                Audio = Convert.ToBase64String(audioData),
                AudioName = audioFileName,
                SpeakerName = speakerName
            };
            
            var response = await PostAsync<TTSResponse>(_config.VCEndpoint, request, cancellationToken);
            
            return DecodeAudioClip(response.Audio, $"vc_{speakerName}_{audioData.GetHashCode()}");
        }
        
        /// <summary>
        /// Convert voice using custom reference audio
        /// </summary>
        public async UniTask<AudioClip> ConvertVoiceCustomAsync(
            byte[] sourceAudio,
            byte[] referenceAudio,
            string sourceFileName = "source.wav",
            string referenceFileName = "reference.wav",
            CancellationToken cancellationToken = default)
        {
            ValidateConfig();
            
            var request = new VCCustomRequest
            {
                Audio = Convert.ToBase64String(sourceAudio),
                AudioName = sourceFileName,
                SpeakerAudio = Convert.ToBase64String(referenceAudio),
                SpeakerName = referenceFileName
            };
            
            var response = await PostAsync<TTSResponse>(_config.VCCustomEndpoint, request, cancellationToken);
            
            return DecodeAudioClip(response.Audio, "vc_custom");
        }
        
        #endregion
        
        #region Voice List API
        
        /// <summary>
        /// Get available voices
        /// </summary>
        public async UniTask<List<VarcoVoice>> GetVoicesAsync(
            bool forceRefresh = false,
            CancellationToken cancellationToken = default)
        {
            ValidateConfig();
            
            // Return cached if available
            if (!forceRefresh && _cachedVoices != null && 
                DateTime.Now - _voicesCacheTime < _voicesCacheDuration)
            {
                return _cachedVoices;
            }
            
            var voices = await GetAsync<List<VarcoVoice>>(_config.VoicesEndpoint, cancellationToken);
            
            // Parse descriptions
            foreach (var voice in voices)
            {
                voice.ParseDescription();
            }
            
            // Cache results
            _cachedVoices = voices;
            _voicesCacheTime = DateTime.Now;
            
            return voices;
        }
        
        /// <summary>
        /// Search voices with filter
        /// </summary>
        public async UniTask<List<VarcoVoice>> SearchVoicesAsync(
            VoiceFilter filter,
            CancellationToken cancellationToken = default)
        {
            var allVoices = await GetVoicesAsync(cancellationToken: cancellationToken);
            var result = new List<VarcoVoice>();
            
            foreach (var voice in allVoices)
            {
                if (filter.Matches(voice))
                {
                    result.Add(voice);
                }
            }
            
            return result;
        }
        
        #endregion
        
        #region Private Methods
        
        private void ValidateConfig()
        {
            if (!_config.IsValid())
            {
                throw new VarcoAuthException();
            }
        }
        
        private void ValidateText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                throw new VarcoBadRequestException("Text cannot be empty.");
            }
            
            var byteCount = Encoding.UTF8.GetByteCount(text);
            if (byteCount > 1200)
            {
                throw new VarcoTextTooLongException(byteCount);
            }
        }
        
        private async UniTask<T> GetAsync<T>(string url, CancellationToken cancellationToken)
        {
            return await SendRequestAsync<T>(url, UnityWebRequest.kHttpVerbGET, null, cancellationToken);
        }
        
        private async UniTask<T> PostAsync<T>(string url, object body, CancellationToken cancellationToken)
        {
            var json = JsonConvert.SerializeObject(body);
            return await SendRequestAsync<T>(url, UnityWebRequest.kHttpVerbPOST, json, cancellationToken);
        }
        
        private async UniTask<T> SendRequestAsync<T>(
            string url,
            string method,
            string body,
            CancellationToken cancellationToken)
        {
            for (int attempt = 0; attempt <= _maxRetries; attempt++)
            {
                try
                {
                    using var request = new UnityWebRequest(url, method);
                    
                    if (!string.IsNullOrEmpty(body))
                    {
                        var bodyBytes = Encoding.UTF8.GetBytes(body);
                        request.uploadHandler = new UploadHandlerRaw(bodyBytes);
                    }
                    
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.SetRequestHeader("OPENAPI_KEY", _config.ApiKey);
                    request.SetRequestHeader("Content-Type", "application/json");
                    
                    await request.SendWebRequest().WithCancellation(cancellationToken);
                    
                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        var responseText = request.downloadHandler.text;
                        return JsonConvert.DeserializeObject<T>(responseText);
                    }
                    
                    HandleError(request);
                }
                catch (VarcoRateLimitException) when (attempt < _maxRetries)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(_retryDelaySeconds * (attempt + 1)), cancellationToken: cancellationToken);
                }
                catch (VarcoServerException) when (attempt < _maxRetries)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(_retryDelaySeconds * (attempt + 1)), cancellationToken: cancellationToken);
                }
            }
            
            throw new VarcoException("Max retries exceeded.");
        }
        
        private void HandleError(UnityWebRequest request)
        {
            var statusCode = (int)request.responseCode;
            var responseText = request.downloadHandler?.text ?? "";
            
            // Try to parse error message
            string errorMessage = responseText;
            string requestId = null;
            
            try
            {
                var errorObj = JObject.Parse(responseText);
                errorMessage = errorObj["message"]?.ToString() ?? responseText;
                requestId = errorObj["request_id"]?.ToString();
            }
            catch { }
            
            throw statusCode switch
            {
                401 => new VarcoAuthException(requestId),
                400 => new VarcoBadRequestException(errorMessage),
                429 => new VarcoRateLimitException(),
                >= 500 => new VarcoServerException(errorMessage),
                _ => new VarcoException($"HTTP {statusCode}: {errorMessage}", statusCode, requestId)
            };
        }
        
        /// <summary>
        /// Decode Base64 WAV to AudioClip
        /// </summary>
        private AudioClip DecodeAudioClip(string base64Audio, string clipName)
        {
            var wavBytes = Convert.FromBase64String(base64Audio);
            return WavUtility.ToAudioClip(wavBytes, clipName);
        }
        
        #endregion
    }
    
    /// <summary>
    /// WAV utility for decoding audio
    /// </summary>
    public static class WavUtility
    {
        public static AudioClip ToAudioClip(byte[] wavData, string clipName = "clip")
        {
            // Parse WAV header
            int channels = BitConverter.ToInt16(wavData, 22);
            int sampleRate = BitConverter.ToInt32(wavData, 24);
            int bitsPerSample = BitConverter.ToInt16(wavData, 34);
            
            // Find data chunk
            int dataOffset = 44;
            for (int i = 36; i < wavData.Length - 4; i++)
            {
                if (wavData[i] == 'd' && wavData[i + 1] == 'a' && 
                    wavData[i + 2] == 't' && wavData[i + 3] == 'a')
                {
                    dataOffset = i + 8;
                    break;
                }
            }
            
            int dataLength = wavData.Length - dataOffset;
            int sampleCount = dataLength / (bitsPerSample / 8) / channels;
            
            // Convert to float samples
            var samples = new float[sampleCount * channels];
            
            if (bitsPerSample == 16)
            {
                for (int i = 0; i < samples.Length; i++)
                {
                    int byteIndex = dataOffset + i * 2;
                    if (byteIndex + 1 < wavData.Length)
                    {
                        short sample = BitConverter.ToInt16(wavData, byteIndex);
                        samples[i] = sample / 32768f;
                    }
                }
            }
            else if (bitsPerSample == 8)
            {
                for (int i = 0; i < samples.Length; i++)
                {
                    int byteIndex = dataOffset + i;
                    if (byteIndex < wavData.Length)
                    {
                        samples[i] = (wavData[byteIndex] - 128) / 128f;
                    }
                }
            }
            
            // Create AudioClip
            var clip = AudioClip.Create(clipName, sampleCount, channels, sampleRate, false);
            clip.SetData(samples, 0);
            
            return clip;
        }
    }
}
