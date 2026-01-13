using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace VARCOVoice.Editor
{
    /// <summary>
    /// Manages voice favorites and recent usage for VoicePicker
    /// </summary>
    public static class VoiceFavorites
    {
        #region Constants
        
        private const string FAVORITES_PREF_KEY = "VARCOVoice_Favorites";
        private const string RECENT_PREF_KEY = "VARCOVoice_RecentVoices";
        private const int MAX_RECENT_COUNT = 10;
        
        #endregion
        
        #region Private Fields
        
        private static HashSet<string> _favorites;
        private static List<string> _recentVoices;
        private static bool _initialized;
        
        #endregion
        
        #region Properties
        
        public static IReadOnlyCollection<string> Favorites
        {
            get
            {
                EnsureInitialized();
                return _favorites;
            }
        }
        
        public static IReadOnlyList<string> RecentVoices
        {
            get
            {
                EnsureInitialized();
                return _recentVoices;
            }
        }
        
        #endregion
        
        #region Events
        
        public static event Action OnFavoritesChanged;
        public static event Action OnRecentsChanged;
        
        #endregion
        
        #region Initialization
        
        private static void EnsureInitialized()
        {
            if (_initialized) return;
            
            Load();
            _initialized = true;
        }
        
        #endregion
        
        #region Favorites API
        
        /// <summary>
        /// Check if a voice is in favorites
        /// </summary>
        public static bool IsFavorite(string voiceName)
        {
            EnsureInitialized();
            return _favorites.Contains(voiceName);
        }
        
        /// <summary>
        /// Toggle favorite status
        /// </summary>
        public static bool ToggleFavorite(string voiceName)
        {
            EnsureInitialized();
            
            bool isFavorite;
            if (_favorites.Contains(voiceName))
            {
                _favorites.Remove(voiceName);
                isFavorite = false;
            }
            else
            {
                _favorites.Add(voiceName);
                isFavorite = true;
            }
            
            SaveFavorites();
            OnFavoritesChanged?.Invoke();
            
            return isFavorite;
        }
        
        /// <summary>
        /// Add to favorites
        /// </summary>
        public static void AddFavorite(string voiceName)
        {
            EnsureInitialized();
            
            if (_favorites.Add(voiceName))
            {
                SaveFavorites();
                OnFavoritesChanged?.Invoke();
            }
        }
        
        /// <summary>
        /// Remove from favorites
        /// </summary>
        public static void RemoveFavorite(string voiceName)
        {
            EnsureInitialized();
            
            if (_favorites.Remove(voiceName))
            {
                SaveFavorites();
                OnFavoritesChanged?.Invoke();
            }
        }
        
        /// <summary>
        /// Clear all favorites
        /// </summary>
        public static void ClearFavorites()
        {
            EnsureInitialized();
            
            _favorites.Clear();
            SaveFavorites();
            OnFavoritesChanged?.Invoke();
        }
        
        #endregion
        
        #region Recent Voices API
        
        /// <summary>
        /// Add a voice to recent usage (moves to top if exists)
        /// </summary>
        public static void AddRecentVoice(string voiceName)
        {
            EnsureInitialized();
            
            // Remove if exists (to move to front)
            _recentVoices.Remove(voiceName);
            
            // Add to front
            _recentVoices.Insert(0, voiceName);
            
            // Trim excess
            while (_recentVoices.Count > MAX_RECENT_COUNT)
            {
                _recentVoices.RemoveAt(_recentVoices.Count - 1);
            }
            
            SaveRecents();
            OnRecentsChanged?.Invoke();
        }
        
        /// <summary>
        /// Remove from recent voices
        /// </summary>
        public static void RemoveRecentVoice(string voiceName)
        {
            EnsureInitialized();
            
            if (_recentVoices.Remove(voiceName))
            {
                SaveRecents();
                OnRecentsChanged?.Invoke();
            }
        }
        
        /// <summary>
        /// Clear all recent voices
        /// </summary>
        public static void ClearRecentVoices()
        {
            EnsureInitialized();
            
            _recentVoices.Clear();
            SaveRecents();
            OnRecentsChanged?.Invoke();
        }
        
        #endregion
        
        #region Persistence
        
        private static void Load()
        {
            // Load favorites
            var favoritesJson = EditorPrefs.GetString(FAVORITES_PREF_KEY, "[]");
            try
            {
                var favArray = JsonUtility.FromJson<StringArrayWrapper>($"{{\"items\":{favoritesJson}}}");
                _favorites = new HashSet<string>(favArray?.items ?? new string[0]);
            }
            catch
            {
                _favorites = new HashSet<string>();
            }
            
            // Load recents
            var recentsJson = EditorPrefs.GetString(RECENT_PREF_KEY, "[]");
            try
            {
                var recentArray = JsonUtility.FromJson<StringArrayWrapper>($"{{\"items\":{recentsJson}}}");
                _recentVoices = new List<string>(recentArray?.items ?? new string[0]);
            }
            catch
            {
                _recentVoices = new List<string>();
            }
        }
        
        private static void SaveFavorites()
        {
            var array = new string[_favorites.Count];
            _favorites.CopyTo(array);
            var json = JsonUtility.ToJson(new StringArrayWrapper { items = array });
            // Extract just the array part
            var startIdx = json.IndexOf('[');
            var endIdx = json.LastIndexOf(']') + 1;
            if (startIdx >= 0 && endIdx > startIdx)
            {
                EditorPrefs.SetString(FAVORITES_PREF_KEY, json.Substring(startIdx, endIdx - startIdx));
            }
        }
        
        private static void SaveRecents()
        {
            var json = JsonUtility.ToJson(new StringArrayWrapper { items = _recentVoices.ToArray() });
            var startIdx = json.IndexOf('[');
            var endIdx = json.LastIndexOf(']') + 1;
            if (startIdx >= 0 && endIdx > startIdx)
            {
                EditorPrefs.SetString(RECENT_PREF_KEY, json.Substring(startIdx, endIdx - startIdx));
            }
        }
        
        [Serializable]
        private class StringArrayWrapper
        {
            public string[] items;
        }
        
        #endregion
    }
}
