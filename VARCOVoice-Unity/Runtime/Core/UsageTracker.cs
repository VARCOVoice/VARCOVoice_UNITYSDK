using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace VARCOVoice
{
    /// <summary>
    /// API usage statistics and cost tracking
    /// </summary>
    public class UsageTracker
    {
        #region Singleton
        
        private static UsageTracker _instance;
        public static UsageTracker Instance => _instance ??= new UsageTracker();
        
        #endregion
        
        #region Configuration
        
        /// <summary>
        /// Cost per API call in KRW (adjust based on your plan)
        /// </summary>
        public float CostPerCallKRW { get; set; } = 5f;
        
        /// <summary>
        /// Cost per character in KRW
        /// </summary>
        public float CostPerCharacterKRW { get; set; } = 0.1f;
        
        /// <summary>
        /// Characters per credit for TTS Lite
        /// Official: 20 chars = 1 credit
        /// </summary>
        public const int CHARS_PER_CREDIT_LITE = 20;
        
        /// <summary>
        /// Characters per credit for TTS Standard
        /// Official: 10 chars = 1 credit
        /// </summary>
        public const int CHARS_PER_CREDIT_STANDARD = 10;
        
        #endregion
        
        #region State
        
        private readonly string _statsPath;
        private UsageStats _stats;
        private readonly object _lock = new object();
        
        #endregion
        
        #region Constructor
        
        private UsageTracker()
        {
            _statsPath = Path.Combine(Application.persistentDataPath, "VARCOVoice", "usage_stats.json");
            LoadStats();
        }
        
        #endregion
        
        #region Public API
        
        /// <summary>
        /// Record an API call
        /// </summary>
        public void RecordCall(string endpoint, int characterCount, bool fromCache, float duration = 0f)
        {
            lock (_lock)
            {
                var today = DateTime.UtcNow.Date.ToString("yyyy-MM-dd");
                
                // Update totals
                _stats.TotalCalls++;
                _stats.TotalCharacters += characterCount;
                _stats.TotalDurationSeconds += duration;
                
                if (fromCache)
                {
                    _stats.CacheHits++;
                }
                else
                {
                    _stats.ApiCalls++;
                    _stats.CharactersProcessed += characterCount;
                }
                
                // Update daily
                if (!_stats.DailyStats.ContainsKey(today))
                {
                    _stats.DailyStats[today] = new DailyUsage();
                }
                
                var daily = _stats.DailyStats[today];
                daily.Calls++;
                daily.Characters += characterCount;
                if (fromCache) daily.CacheHits++;
                else daily.ApiCalls++;
                
                // Track endpoint usage
                if (!_stats.EndpointCalls.ContainsKey(endpoint))
                {
                    _stats.EndpointCalls[endpoint] = 0;
                }
                _stats.EndpointCalls[endpoint]++;
                
                _stats.LastUpdated = DateTime.UtcNow;
                
                SaveStats();
            }
        }
        
        /// <summary>
        /// Get current usage statistics
        /// </summary>
        public UsageStats GetStats()
        {
            lock (_lock)
            {
                return _stats;
            }
        }
        
        /// <summary>
        /// Get estimated cost in KRW
        /// </summary>
        public float GetEstimatedCostKRW()
        {
            lock (_lock)
            {
                float callCost = _stats.ApiCalls * CostPerCallKRW;
                float charCost = _stats.CharactersProcessed * CostPerCharacterKRW;
                return callCost + charCost;
            }
        }
        
        /// <summary>
        /// Get cache hit rate (0-100%)
        /// </summary>
        public float GetCacheHitRate()
        {
            lock (_lock)
            {
                if (_stats.TotalCalls == 0) return 0f;
                return (float)_stats.CacheHits / _stats.TotalCalls * 100f;
            }
        }
        
        /// <summary>
        /// Get savings from cache
        /// </summary>
        public float GetCacheSavingsKRW()
        {
            lock (_lock)
            {
                // Estimate savings based on cache hits
                return _stats.CacheHits * CostPerCallKRW;
            }
        }
        
        /// <summary>
        /// Calculate credits used based on character count and model type
        /// </summary>
        public int CalculateCredits(int characters, bool isLiteModel = true)
        {
            int charsPerCredit = isLiteModel ? CHARS_PER_CREDIT_LITE : CHARS_PER_CREDIT_STANDARD;
            return (int)Math.Ceiling((double)characters / charsPerCredit);
        }
        
        /// <summary>
        /// Get estimated credits used this month
        /// </summary>
        public int GetMonthlyCreditsUsed(bool isLiteModel = true)
        {
            var monthly = GetCurrentMonthUsage();
            return CalculateCredits(monthly.Characters, isLiteModel);
        }
        
        /// <summary>
        /// Get usage for current month
        /// </summary>
        public MonthlyUsage GetCurrentMonthUsage()
        {
            lock (_lock)
            {
                var now = DateTime.UtcNow;
                string monthPrefix = now.ToString("yyyy-MM");
                
                var monthly = new MonthlyUsage
                {
                    Month = monthPrefix,
                    TotalCalls = 0,
                    ApiCalls = 0,
                    CacheHits = 0,
                    Characters = 0
                };
                
                foreach (var kvp in _stats.DailyStats)
                {
                    if (kvp.Key.StartsWith(monthPrefix))
                    {
                        monthly.TotalCalls += kvp.Value.Calls;
                        monthly.ApiCalls += kvp.Value.ApiCalls;
                        monthly.CacheHits += kvp.Value.CacheHits;
                        monthly.Characters += kvp.Value.Characters;
                    }
                }
                
                monthly.EstimatedCostKRW = monthly.ApiCalls * CostPerCallKRW + 
                                           monthly.Characters * CostPerCharacterKRW;
                
                return monthly;
            }
        }
        
        /// <summary>
        /// Get daily usage for the past N days
        /// </summary>
        public List<DailyUsage> GetDailyUsage(int days = 30)
        {
            lock (_lock)
            {
                var result = new List<DailyUsage>();
                var today = DateTime.UtcNow.Date;
                
                for (int i = days - 1; i >= 0; i--)
                {
                    var date = today.AddDays(-i).ToString("yyyy-MM-dd");
                    if (_stats.DailyStats.TryGetValue(date, out var daily))
                    {
                        daily.Date = date;
                        result.Add(daily);
                    }
                    else
                    {
                        result.Add(new DailyUsage { Date = date });
                    }
                }
                
                return result;
            }
        }
        
        /// <summary>
        /// Reset all statistics
        /// </summary>
        public void ResetStats()
        {
            lock (_lock)
            {
                _stats = new UsageStats();
                SaveStats();
            }
        }
        
        /// <summary>
        /// Reset monthly statistics
        /// </summary>
        public void ResetMonthlyStats()
        {
            lock (_lock)
            {
                var now = DateTime.UtcNow;
                string monthPrefix = now.ToString("yyyy-MM");
                
                var keysToRemove = new List<string>();
                foreach (var key in _stats.DailyStats.Keys)
                {
                    if (key.StartsWith(monthPrefix))
                    {
                        keysToRemove.Add(key);
                    }
                }
                
                foreach (var key in keysToRemove)
                {
                    _stats.DailyStats.Remove(key);
                }
                
                SaveStats();
            }
        }
        
        #endregion
        
        #region Private Methods
        
        private void LoadStats()
        {
            try
            {
                if (File.Exists(_statsPath))
                {
                    string json = File.ReadAllText(_statsPath);
                    _stats = JsonUtility.FromJson<UsageStats>(json) ?? new UsageStats();
                }
                else
                {
                    _stats = new UsageStats();
                }
            }
            catch (Exception)
            {
#if VARCO_DEBUG
                Debug.LogWarning("[UsageTracker] Failed to load stats");
#endif
                _stats = new UsageStats();
            }
        }
        
        private void SaveStats()
        {
            try
            {
                var dir = Path.GetDirectoryName(_statsPath);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                
                string json = JsonUtility.ToJson(_stats, true);
                File.WriteAllText(_statsPath, json);
            }
            catch (Exception)
            {
#if VARCO_DEBUG
                Debug.LogError("[UsageTracker] Failed to save stats");
#endif
            }
        }
        
        #endregion
    }
    
    #region Data Classes
    
    /// <summary>
    /// Overall usage statistics
    /// </summary>
    [Serializable]
    public class UsageStats
    {
        public int TotalCalls;
        public int ApiCalls;         // Actual API calls (not from cache)
        public int CacheHits;        // Served from cache
        public int TotalCharacters;
        public int CharactersProcessed;  // Characters sent to API
        public float TotalDurationSeconds;
        public long LastUpdatedTicks;

        public DateTime LastUpdated
        {
            get => new DateTime(LastUpdatedTicks, DateTimeKind.Utc);
            set => LastUpdatedTicks = value.Ticks;
        }
        
        public SerializableDictionary<string, DailyUsage> DailyStats = new SerializableDictionary<string, DailyUsage>();
        public SerializableDictionary<string, int> EndpointCalls = new SerializableDictionary<string, int>();
    }
    
    /// <summary>
    /// Daily usage breakdown
    /// </summary>
    [Serializable]
    public class DailyUsage
    {
        public string Date;
        public int Calls;
        public int ApiCalls;
        public int CacheHits;
        public int Characters;
    }
    
    /// <summary>
    /// Monthly usage summary
    /// </summary>
    [Serializable]
    public class MonthlyUsage
    {
        public string Month;
        public int TotalCalls;
        public int ApiCalls;
        public int CacheHits;
        public int Characters;
        public float EstimatedCostKRW;
        
        public float CacheHitRate => TotalCalls > 0 ? (float)CacheHits / TotalCalls * 100f : 0f;
    }
    
    /// <summary>
    /// Serializable dictionary for JSON serialization
    /// </summary>
    [Serializable]
    public class SerializableDictionary<TKey, TValue> : Dictionary<TKey, TValue>, ISerializationCallbackReceiver
    {
        [SerializeField] private List<TKey> keys = new List<TKey>();
        [SerializeField] private List<TValue> values = new List<TValue>();
        
        public void OnBeforeSerialize()
        {
            keys.Clear();
            values.Clear();
            foreach (var kvp in this)
            {
                keys.Add(kvp.Key);
                values.Add(kvp.Value);
            }
        }
        
        public void OnAfterDeserialize()
        {
            Clear();
            for (int i = 0; i < Math.Min(keys.Count, values.Count); i++)
            {
                this[keys[i]] = values[i];
            }
        }
    }
    
    #endregion
}
