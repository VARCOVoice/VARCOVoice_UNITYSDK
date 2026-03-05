using System;
using UnityEngine;

namespace VARCOVoice
{
    /// <summary>
    /// VARCO Voice API configuration
    /// </summary>
    [CreateAssetMenu(fileName = "VarcoConfig", menuName = "VARCO Voice/Configuration")]
    public class VarcoConfig : ScriptableObject
    {
        private const string DEFAULT_BASE_URL = "https://openapi.ai.nc.com";
        
        [Header("API Settings")]
        [Tooltip("VARCO Voice API Key")]
        [SerializeField] private string apiKey = "";
        
        [Tooltip("API Base URL")]
        [SerializeField] private string baseUrl = DEFAULT_BASE_URL;
        
        [Header("TTS Settings")]
        [Tooltip("Use Standard model (higher quality) or Lite model (faster)")]
        [SerializeField] private TTSModel defaultModel = TTSModel.Standard;
        
        [Tooltip("Default voice speaker name")]
        [SerializeField] private string defaultVoice = "멀더";
        
        [Tooltip("Default language")]
        [SerializeField] private Language defaultLanguage = Language.Korean;
        
        [Header("Quality Settings")]
        [Range(8, 20)]
        [Tooltip("Quality level (8-20, higher = better quality but slower)")]
        [SerializeField] private int qualityLevel = 8;
        
        [Range(0.5f, 1.5f)]
        [Tooltip("Speech speed (0.5-1.5, recommended: 0.8-1.2)")]
        [SerializeField] private float defaultSpeed = 1.0f;
        
        [Range(0.5f, 1.5f)]
        [Tooltip("Speech pitch (0.5-1.5, recommended: 0.8-1.2)")]
        [SerializeField] private float defaultPitch = 1.0f;
        
        [Header("Cache Settings")]
        [Tooltip("Enable audio caching")]
        [SerializeField] private bool enableCache = true;
        
        [Tooltip("Maximum cache size in MB")]
        [SerializeField] private int maxCacheSizeMB = 100;
        
        // Properties
        public string ApiKey
        {
            get
            {
#if UNITY_EDITOR
                // In Editor, prefer EditorPrefs over serialized field
                string editorKey = UnityEditor.EditorPrefs.GetString(API_KEY_PREF, "");
                if (!string.IsNullOrEmpty(editorKey))
                    return editorKey;
#endif
                return apiKey;
            }
        }
        public string BaseUrl => string.IsNullOrEmpty(baseUrl) ? DEFAULT_BASE_URL : baseUrl;
        public TTSModel DefaultModel => defaultModel;
        public string DefaultVoice => defaultVoice;
        public Language DefaultLanguage => defaultLanguage;
        public int QualityLevel => Mathf.Clamp(qualityLevel, 8, 20);
        public float DefaultSpeed => Mathf.Clamp(defaultSpeed, 0.5f, 1.5f);
        public float DefaultPitch => Mathf.Clamp(defaultPitch, 0.5f, 1.5f);
        public bool EnableCache => enableCache;
        public int MaxCacheSizeMB => maxCacheSizeMB;
        
        // API Endpoints
        public string TTSEndpoint => DefaultModel == TTSModel.Standard 
            ? $"{BaseUrl}/tts/standard/v1/api/synthesize"
            : $"{BaseUrl}/tts/lite/v1/api/synthesize";
        
        public string VoicesEndpoint => DefaultModel == TTSModel.Standard
            ? $"{BaseUrl}/tts/standard/v1/api/voices/varco"
            : $"{BaseUrl}/tts/lite/v1/api/voices/varco";
        
        public string VCEndpoint => $"{BaseUrl}/vc/varco/v1/api/convert-voice";
        public string VCCustomEndpoint => $"{BaseUrl}/vc/varco/v1/api/convert-voice-custom";
        
        // Singleton instance for easy access
        private static VarcoConfig _instance;
        public static VarcoConfig Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<VarcoConfig>("VarcoConfig");
                    if (_instance == null)
                    {
#if VARCO_DEBUG
                        Debug.LogWarning("[VARCOVoice] VarcoConfig not found in Resources. Creating default config.");
#endif
                        _instance = CreateInstance<VarcoConfig>();
                    }
                }
                return _instance;
            }
        }
        
        /// <summary>
        /// Validate configuration
        /// </summary>
        public bool IsValid()
        {
            if (string.IsNullOrEmpty(ApiKey))
            {
#if VARCO_DEBUG
                Debug.LogError("[VARCOVoice] API Key is not configured. Please set it in Project Settings or VarcoConfig asset.");
#endif
                return false;
            }
            return true;
        }
        
#if UNITY_EDITOR
        private const string API_KEY_PREF = "VARCOVoice_ApiKey";

        /// <summary>
        /// Set API key from editor (stored in EditorPrefs for security)
        /// </summary>
        public void SetApiKeyFromEditor(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                UnityEditor.EditorPrefs.DeleteKey(API_KEY_PREF);
            }
            else
            {
                UnityEditor.EditorPrefs.SetString(API_KEY_PREF, key);
            }

            // Keep project assets free of API keys.
            apiKey = string.Empty;
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
    
    /// <summary>
    /// TTS Model type
    /// </summary>
    public enum TTSModel
    {
        /// <summary>
        /// Standard model - Higher quality, 1,293+ voices
        /// </summary>
        Standard,
        
        /// <summary>
        /// Lite model - Faster response
        /// </summary>
        Lite
    }
    
    /// <summary>
    /// Supported languages
    /// </summary>
    public enum Language
    {
        Korean,
        English,
        Japanese,
        Taiwanese
    }
    
    /// <summary>
    /// Language extension methods
    /// </summary>
    public static class LanguageExtensions
    {
        public static string ToApiString(this Language language)
        {
            return language switch
            {
                Language.Korean => "korean",
                Language.English => "english",
                Language.Japanese => "japanese",
                Language.Taiwanese => "taiwanese",
                _ => "korean"
            };
        }
        
        public static Language FromApiString(string apiString)
        {
            return apiString?.ToLower() switch
            {
                "korean" => Language.Korean,
                "english" => Language.English,
                "japanese" => Language.Japanese,
                "taiwanese" => Language.Taiwanese,
                _ => Language.Korean
            };
        }
    }
}
