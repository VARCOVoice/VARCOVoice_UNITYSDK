using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;

namespace VARCOVoice
{
    /// <summary>
    /// Persistent audio cache with disk storage and LRU eviction
    /// </summary>
    public class AudioCacheManager
    {
        private const string CacheEnabledPrefKey = "VARCOVoice_CacheEnabled";
        private const string MaxCacheSizePrefKey = "VARCOVoice_MaxCacheSize";

        #region Singleton
        
        private static AudioCacheManager _instance;
        public static AudioCacheManager Instance => _instance ??= new AudioCacheManager();
        
        #endregion
        
        #region Configuration
        
        /// <summary>
        /// Maximum cache size in bytes (default: 500MB)
        /// </summary>
        public long MaxCacheSizeBytes { get; set; } = 500 * 1024 * 1024;
        
        /// <summary>
        /// Maximum cache age in days (default: 30)
        /// </summary>
        public int MaxCacheAgeDays { get; set; } = 30;
        
        /// <summary>
        /// Whether caching is enabled
        /// </summary>
        public bool Enabled { get; set; } = true;
        
        #endregion
        
        #region State
        
        private readonly string _cacheDirectory;
        private readonly string _indexPath;
        private CacheIndex _index;
        private readonly object _lock = new object();
        
        // In-memory LRU cache for quick access
        private readonly Dictionary<string, AudioClip> _memoryCache = new Dictionary<string, AudioClip>();
        private readonly LinkedList<string> _lruOrder = new LinkedList<string>();
        private const int MaxMemoryCacheItems = 50;
        
        #endregion
        
        #region Constructor
        
        private AudioCacheManager()
        {
            _cacheDirectory = Path.Combine(Application.persistentDataPath, "VARCOVoice", "Cache");
            _indexPath = Path.Combine(_cacheDirectory, "index.json");
            
            ApplyInitialSettings();
            EnsureDirectoryExists();
            LoadIndex();
        }
        
        #endregion
        
        #region Public API
        
        /// <summary>
        /// Generate cache key from TTS parameters
        /// </summary>
        public string GenerateKey(string text, string voice, string language, float speed, float pitch, int qualityLevel)
        {
            string combined = $"{text}|{voice}|{language}|{speed:F2}|{pitch:F2}|{qualityLevel}";
            using (var md5 = MD5.Create())
            {
                byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(combined));
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }
        
        /// <summary>
        /// Try to get cached audio clip
        /// </summary>
        public bool TryGet(string key, out AudioClip clip)
        {
            clip = null;
            if (!Enabled) return false;
            
            lock (_lock)
            {
                // Check memory cache first
                if (_memoryCache.TryGetValue(key, out clip))
                {
                    UpdateLRU(key);
                    UpdateAccessTime(key);
                    return clip != null;
                }
                
                // Check disk cache
                if (_index.Entries.TryGetValue(key, out var entry))
                {
                    string filePath = GetFilePath(key);
                    if (File.Exists(filePath))
                    {
                        try
                        {
                            clip = LoadAudioClipFromFile(filePath, key);
                            if (clip != null)
                            {
                                AddToMemoryCache(key, clip);
                                UpdateAccessTime(key);
                                return true;
                            }
                        }
                        catch (Exception)
                        {
#if VARCO_DEBUG
                            Debug.LogWarning("[VARCOCache] Failed to load cached audio");
#endif
                            RemoveEntry(key);
                        }
                    }
                    else
                    {
                        // File missing, remove from index
                        RemoveEntry(key);
                    }
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// Store audio clip in cache
        /// </summary>
        public void Store(string key, AudioClip clip, byte[] audioData = null)
        {
            if (!Enabled || clip == null) return;
            
            lock (_lock)
            {
                string filePath = GetFilePath(key);
                
                try
                {
                    // Save to disk
                    if (audioData != null && audioData.Length > 0)
                    {
                        File.WriteAllBytes(filePath, audioData);
                    }
                    else
                    {
                        // Convert AudioClip to WAV
                        byte[] wavData = AudioClipToWav(clip);
                        File.WriteAllBytes(filePath, wavData);
                    }
                    
                    // Update index (handle overwrite case for correct size tracking)
                    if (_index.Entries.TryGetValue(key, out var existingEntry))
                    {
                        _index.TotalSizeBytes -= existingEntry.SizeBytes;
                    }
                    
                    var entry = new CacheEntry
                    {
                        Key = key,
                        FileName = Path.GetFileName(filePath),
                        CreatedAtTicks = DateTime.UtcNow.Ticks,
                        LastAccessedAtTicks = DateTime.UtcNow.Ticks,
                        SizeBytes = new FileInfo(filePath).Length,
                        Duration = clip.length
                    };
                    
                    _index.Entries[key] = entry;
                    _index.TotalSizeBytes += entry.SizeBytes;
                    
                    // Add to memory cache
                    AddToMemoryCache(key, clip);
                    
                    // Save index
                    SaveIndex();
                    
                    // Enforce size limit
                    EnforceSizeLimit();
                }
                catch (Exception)
                {
#if VARCO_DEBUG
                    Debug.LogError("[VARCOCache] Failed to store audio");
#endif
                }
            }
        }
        
        /// <summary>
        /// Store raw audio bytes (more efficient)
        /// </summary>
        public void StoreBytes(string key, byte[] audioData, float duration)
        {
            if (!Enabled || audioData == null || audioData.Length == 0) return;

            lock (_lock)
            {
                string filePath = GetFilePath(key);

                try
                {
                    File.WriteAllBytes(filePath, audioData);

                    if (_index.Entries.TryGetValue(key, out var existingEntry))
                    {
                        _index.TotalSizeBytes -= existingEntry.SizeBytes;
                    }

                    var entry = new CacheEntry
                    {
                        Key = key,
                        FileName = Path.GetFileName(filePath),
                        CreatedAt = DateTime.UtcNow,
                        LastAccessedAt = DateTime.UtcNow,
                        SizeBytes = audioData.Length,
                        Duration = duration
                    };
                    
                    _index.Entries[key] = entry;
                    _index.TotalSizeBytes += entry.SizeBytes;
                    
                    SaveIndex();
                    EnforceSizeLimit();
                }
                catch (Exception)
                {
#if VARCO_DEBUG
                    Debug.LogError("[VARCOCache] Failed to store audio bytes");
#endif
                }
            }
        }
        
        /// <summary>
        /// Preload audio for specific text/voice combination
        /// </summary>
        public async UniTask PreloadAsync(string text, string voice, string language, 
            float speed = 1f, float pitch = 1f, int qualityLevel = -1)
        {
            var config = VarcoConfig.Instance;
            if (config == null) return;

            int resolvedQuality = qualityLevel > 0 ? Mathf.Clamp(qualityLevel, 8, 20) : config.QualityLevel;
            string key = GenerateKey(text, voice, language, speed, pitch, resolvedQuality);
            
            if (TryGet(key, out _))
            {
                return; // Already cached
            }
            
            var client = new VarcoApiClient(config);
            var clip = await client.SynthesizeAsync(text, voice, 
                LanguageExtensions.FromApiString(language), speed, pitch, resolvedQuality);
            
            if (clip != null)
            {
                Store(key, clip);
            }
        }
        
        /// <summary>
        /// Get cache statistics
        /// </summary>
        public CacheStats GetStats()
        {
            lock (_lock)
            {
                return new CacheStats
                {
                    TotalEntries = _index.Entries.Count,
                    TotalSizeBytes = _index.TotalSizeBytes,
                    MaxSizeBytes = MaxCacheSizeBytes,
                    MemoryCacheCount = _memoryCache.Count,
                    CacheDirectory = _cacheDirectory
                };
            }
        }
        
        /// <summary>
        /// Clear all cached audio
        /// </summary>
        public void ClearAll()
        {
            lock (_lock)
            {
                try
                {
                    if (Directory.Exists(_cacheDirectory))
                    {
                        foreach (var file in Directory.GetFiles(_cacheDirectory, "*.wav"))
                        {
                            File.Delete(file);
                        }
                    }
                    
                    _index = new CacheIndex();
                    _memoryCache.Clear();
                    _lruOrder.Clear();
                    
                    SaveIndex();
                    
#if VARCO_DEBUG

#endif
                }
                catch (Exception)
                {
#if VARCO_DEBUG
                    Debug.LogError("[VARCOCache] Failed to clear cache");
#endif
                }
            }
        }
        
        /// <summary>
        /// Remove entries older than specified age
        /// </summary>
        public void RemoveOlderThan(TimeSpan maxAge)
        {
            lock (_lock)
            {
                var cutoff = DateTime.UtcNow - maxAge;
                var keysToRemove = new List<string>();
                
                foreach (var kvp in _index.Entries)
                {
                    if (kvp.Value.LastAccessedAt < cutoff)
                    {
                        keysToRemove.Add(kvp.Key);
                    }
                }
                
                foreach (var key in keysToRemove)
                {
                    RemoveEntry(key);
                }
                
                SaveIndex();
                
#if VARCO_DEBUG

#endif
            }
        }
        
        /// <summary>
        /// Check if key exists in cache
        /// </summary>
        public bool Contains(string key)
        {
            lock (_lock)
            {
                return _index.Entries.ContainsKey(key);
            }
        }
        
        #endregion
        
        #region Private Methods

        private void ApplyInitialSettings()
        {
            var config = VarcoConfig.Instance;
            if (config != null)
            {
                Enabled = config.EnableCache;
                MaxCacheSizeBytes = Mathf.Max(1, config.MaxCacheSizeMB) * 1024L * 1024L;
            }

#if UNITY_EDITOR
            if (UnityEditor.EditorPrefs.HasKey(CacheEnabledPrefKey))
            {
                Enabled = UnityEditor.EditorPrefs.GetBool(CacheEnabledPrefKey, Enabled);
            }

            if (UnityEditor.EditorPrefs.HasKey(MaxCacheSizePrefKey))
            {
                float sizeMb = UnityEditor.EditorPrefs.GetFloat(MaxCacheSizePrefKey, MaxCacheSizeBytes / (1024f * 1024f));
                MaxCacheSizeBytes = (long)(Mathf.Max(1f, sizeMb) * 1024f * 1024f);
            }
#endif
        }
        
        private void EnsureDirectoryExists()
        {
            if (!Directory.Exists(_cacheDirectory))
            {
                Directory.CreateDirectory(_cacheDirectory);
            }
        }
        
        private void LoadIndex()
        {
            try
            {
                if (File.Exists(_indexPath))
                {
                    string json = File.ReadAllText(_indexPath);
                    _index = JsonConvert.DeserializeObject<CacheIndex>(json) ?? new CacheIndex();
                    
                    // Validate entries
                    ValidateIndex();
                }
                else
                {
                    _index = new CacheIndex();
                    // Try to recover from WAV files if index is missing
                    RecoverIndexFromDirectory();
                }
            }
            catch (Exception)
            {
#if VARCO_DEBUG
                Debug.LogWarning("[VARCOCache] Failed to load index, attempting recovery");
#endif
                _index = new CacheIndex();
                RecoverIndexFromDirectory();
            }
        }
        
        private void RecoverIndexFromDirectory()
        {
            try
            {
                var wavFiles = Directory.GetFiles(_cacheDirectory, "*.wav");
                foreach (var filePath in wavFiles)
                {
                    var fileName = Path.GetFileName(filePath);
                    var key = Path.GetFileNameWithoutExtension(fileName);
                    var fileInfo = new FileInfo(filePath);
                    
                    if (!_index.Entries.ContainsKey(key))
                    {
                        _index.Entries[key] = new CacheEntry
                        {
                            Key = key,
                            FileName = fileName,
                            CreatedAtTicks = fileInfo.CreationTimeUtc.Ticks,
                            LastAccessedAtTicks = fileInfo.LastAccessTimeUtc.Ticks,
                            SizeBytes = fileInfo.Length,
                            Duration = 0f // Unknown without loading
                        };
                        _index.TotalSizeBytes += fileInfo.Length;
                    }
                }
                
                if (_index.Entries.Count > 0)
                {
                    SaveIndex();
#if VARCO_DEBUG

#endif
                }
            }
            catch (Exception)
            {
                // Ignore recovery errors
            }
        }
        
        private void SaveIndex()
        {
            try
            {
                string json = JsonConvert.SerializeObject(_index, Formatting.Indented);
                File.WriteAllText(_indexPath, json);
            }
            catch (Exception)
            {
#if VARCO_DEBUG
                Debug.LogError("[VARCOCache] Failed to save index");
#endif
            }
        }
        
        private void ValidateIndex()
        {
            var keysToRemove = new List<string>();
            long recalculatedSize = 0;
            
            foreach (var kvp in _index.Entries)
            {
                string filePath = GetFilePath(kvp.Key);
                if (!File.Exists(filePath))
                {
                    keysToRemove.Add(kvp.Key);
                }
                else
                {
                    recalculatedSize += kvp.Value.SizeBytes;
                }
            }
            
            foreach (var key in keysToRemove)
            {
                _index.Entries.Remove(key);
            }
            
            _index.TotalSizeBytes = recalculatedSize;
            
            if (keysToRemove.Count > 0)
            {
                SaveIndex();
            }
        }
        
        private string GetFilePath(string key)
        {
            return Path.Combine(_cacheDirectory, $"{key}.wav");
        }
        
        private void AddToMemoryCache(string key, AudioClip clip)
        {
            if (_memoryCache.Count >= MaxMemoryCacheItems)
            {
                // Remove least recently used
                var lruKey = _lruOrder.Last.Value;
                _lruOrder.RemoveLast();
                _memoryCache.Remove(lruKey);
            }

            _memoryCache[key] = clip;
            _lruOrder.Remove(key);
            _lruOrder.AddFirst(key);
        }
        
        private void UpdateLRU(string key)
        {
            _lruOrder.Remove(key);
            _lruOrder.AddFirst(key);
        }
        
        private void UpdateAccessTime(string key)
        {
            if (_index.Entries.TryGetValue(key, out var entry))
            {
                entry.LastAccessedAt = DateTime.UtcNow;
            }
        }
        
        private void RemoveEntry(string key)
        {
            if (_index.Entries.TryGetValue(key, out var entry))
            {
                _index.TotalSizeBytes -= entry.SizeBytes;
                _index.Entries.Remove(key);
                
                string filePath = GetFilePath(key);
                if (File.Exists(filePath))
                {
                    try { File.Delete(filePath); } catch { }
                }
                
                _memoryCache.Remove(key);
                _lruOrder.Remove(key);
            }
        }
        
        private void EnforceSizeLimit()
        {
            while (_index.TotalSizeBytes > MaxCacheSizeBytes && _index.Entries.Count > 0)
            {
                // Find oldest accessed entry
                string oldestKey = null;
                DateTime oldestTime = DateTime.MaxValue;
                
                foreach (var kvp in _index.Entries)
                {
                    if (kvp.Value.LastAccessedAt < oldestTime)
                    {
                        oldestTime = kvp.Value.LastAccessedAt;
                        oldestKey = kvp.Key;
                    }
                }
                
                if (oldestKey != null)
                {
                    RemoveEntry(oldestKey);
                }
                else
                {
                    break;
                }
            }
            
            SaveIndex();
        }
        
        private AudioClip LoadAudioClipFromFile(string filePath, string clipName)
        {
            byte[] wavData = File.ReadAllBytes(filePath);
            return WavUtility.ToAudioClip(wavData, clipName);
        }
        
        private byte[] AudioClipToWav(AudioClip clip)
        {
            // WAV header + PCM data
            float[] samples = new float[clip.samples * clip.channels];
            clip.GetData(samples, 0);
            
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                int sampleCount = samples.Length;
                int byteRate = clip.frequency * clip.channels * 2;
                int blockAlign = clip.channels * 2;
                int dataSize = sampleCount * 2;
                
                // RIFF header
                writer.Write(Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + dataSize);
                writer.Write(Encoding.ASCII.GetBytes("WAVE"));
                
                // fmt chunk
                writer.Write(Encoding.ASCII.GetBytes("fmt "));
                writer.Write(16);
                writer.Write((short)1); // PCM
                writer.Write((short)clip.channels);
                writer.Write(clip.frequency);
                writer.Write(byteRate);
                writer.Write((short)blockAlign);
                writer.Write((short)16); // bits per sample
                
                // data chunk
                writer.Write(Encoding.ASCII.GetBytes("data"));
                writer.Write(dataSize);
                
                // Write samples
                foreach (float sample in samples)
                {
                    short intSample = (short)(Mathf.Clamp(sample, -1f, 1f) * 32767f);
                    writer.Write(intSample);
                }
                
                return stream.ToArray();
            }
        }
        
        #endregion
    }
    
    #region Data Classes
    
    /// <summary>
    /// Cache index stored on disk
    /// </summary>
    [Serializable]
    public class CacheIndex
    {
        public Dictionary<string, CacheEntry> Entries = new Dictionary<string, CacheEntry>();
        public long TotalSizeBytes;
        public string Version = "1.0";
    }
    
    /// <summary>
    /// Individual cache entry
    /// </summary>
    [Serializable]
    public class CacheEntry
    {
        public string Key;
        public string FileName;
        public long CreatedAtTicks;
        public long LastAccessedAtTicks;
        public long SizeBytes;
        public float Duration;
        
        // Helper properties for DateTime access
        [Newtonsoft.Json.JsonIgnore]
        public DateTime CreatedAt
        {
            get => new DateTime(CreatedAtTicks, DateTimeKind.Utc);
            set => CreatedAtTicks = value.Ticks;
        }
        
        [Newtonsoft.Json.JsonIgnore]
        public DateTime LastAccessedAt
        {
            get => new DateTime(LastAccessedAtTicks, DateTimeKind.Utc);
            set => LastAccessedAtTicks = value.Ticks;
        }
    }
    
    /// <summary>
    /// Cache statistics
    /// </summary>
    public struct CacheStats
    {
        public int TotalEntries;
        public long TotalSizeBytes;
        public long MaxSizeBytes;
        public int MemoryCacheCount;
        public string CacheDirectory;
        
        public float UsagePercent => MaxSizeBytes > 0 ? (float)TotalSizeBytes / MaxSizeBytes * 100f : 0f;
        public string TotalSizeFormatted => FormatBytes(TotalSizeBytes);
        public string MaxSizeFormatted => FormatBytes(MaxSizeBytes);
        
        private static string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            int order = 0;
            double size = bytes;
            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }
            return $"{size:0.##} {sizes[order]}";
        }
    }
    
    #endregion
}
