using UnityEngine;
using UnityEditor;

namespace VARCOVoice.Editor
{
    /// <summary>
    /// VARCO Voice Editor Icons Manager
    /// </summary>
    public static class VarcoEditorIcons
    {
        #region Cached Icons
        
        private static Texture2D _playIcon;
        private static Texture2D _stopIcon;
        private static Texture2D _starIcon;
        private static Texture2D _starFilledIcon;
        private static Texture2D _refreshIcon;
        private static Texture2D _searchIcon;
        private static Texture2D _settingsIcon;
        private static Texture2D _closeIcon;
        private static Texture2D _checkIcon;
        private static Texture2D _warningIcon;
        private static Texture2D _errorIcon;
        
        private static bool _initialized;
        
        #endregion
        
        #region Properties
        
        public static Texture2D PlayIcon
        {
            get
            {
                EnsureInitialized();
                return _playIcon;
            }
        }
        
        public static Texture2D StopIcon
        {
            get
            {
                EnsureInitialized();
                return _stopIcon;
            }
        }
        
        public static Texture2D StarIcon
        {
            get
            {
                EnsureInitialized();
                return _starIcon;
            }
        }
        
        public static Texture2D StarFilledIcon
        {
            get
            {
                EnsureInitialized();
                return _starFilledIcon;
            }
        }
        
        public static Texture2D RefreshIcon
        {
            get
            {
                EnsureInitialized();
                return _refreshIcon;
            }
        }
        
        public static Texture2D SearchIcon
        {
            get
            {
                EnsureInitialized();
                return _searchIcon;
            }
        }
        
        public static Texture2D SettingsIcon
        {
            get
            {
                EnsureInitialized();
                return _settingsIcon;
            }
        }
        
        public static Texture2D CloseIcon
        {
            get
            {
                EnsureInitialized();
                return _closeIcon;
            }
        }
        
        public static Texture2D CheckIcon
        {
            get
            {
                EnsureInitialized();
                return _checkIcon;
            }
        }
        
        public static Texture2D WarningIcon
        {
            get
            {
                EnsureInitialized();
                return _warningIcon;
            }
        }
        
        public static Texture2D ErrorIcon
        {
            get
            {
                EnsureInitialized();
                return _errorIcon;
            }
        }
        
        #endregion
        
        #region Initialization
        
        private static void EnsureInitialized()
        {
            if (_initialized) return;
            
            LoadIcons();
            _initialized = true;
        }
        
        private static void LoadIcons()
        {
            // Use Unity's built-in icons where possible
            _playIcon = EditorGUIUtility.FindTexture("d_PlayButton") ?? CreatePlayIcon();
            _stopIcon = EditorGUIUtility.FindTexture("d_PauseButton") ?? CreateStopIcon();
            _starIcon = CreateStarIcon(false);
            _starFilledIcon = CreateStarIcon(true);
            _refreshIcon = EditorGUIUtility.FindTexture("d_Refresh") ?? CreateRefreshIcon();
            _searchIcon = EditorGUIUtility.FindTexture("d_Search Icon") ?? CreateSearchIcon();
            _settingsIcon = EditorGUIUtility.FindTexture("d_Settings") ?? CreateSettingsIcon();
            _closeIcon = EditorGUIUtility.FindTexture("d_winbtn_win_close") ?? CreateCloseIcon();
            _checkIcon = CreateCheckIcon();
            _warningIcon = EditorGUIUtility.FindTexture("d_console.warnicon.sml");
            _errorIcon = EditorGUIUtility.FindTexture("d_console.erroricon.sml");
        }
        
        #endregion
        
        #region Icon Generation
        
        private static Texture2D CreatePlayIcon()
        {
            var texture = new Texture2D(16, 16);
            var color = VarcoEditorStyles.Mint;
            var transparent = Color.clear;
            
            for (int x = 0; x < 16; x++)
            {
                for (int y = 0; y < 16; y++)
                {
                    // Triangle pointing right
                    float triangleX = x - 4;
                    float triangleY = Mathf.Abs(y - 8);
                    bool inside = triangleX >= 0 && triangleX <= 8 && triangleY <= triangleX * 0.7f;
                    texture.SetPixel(x, y, inside ? color : transparent);
                }
            }
            
            texture.Apply();
            texture.hideFlags = HideFlags.HideAndDontSave;
            return texture;
        }
        
        private static Texture2D CreateStopIcon()
        {
            var texture = new Texture2D(16, 16);
            var color = VarcoEditorStyles.Error;
            var transparent = Color.clear;
            
            for (int x = 0; x < 16; x++)
            {
                for (int y = 0; y < 16; y++)
                {
                    bool inside = x >= 4 && x < 12 && y >= 4 && y < 12;
                    texture.SetPixel(x, y, inside ? color : transparent);
                }
            }
            
            texture.Apply();
            texture.hideFlags = HideFlags.HideAndDontSave;
            return texture;
        }
        
        private static Texture2D CreateStarIcon(bool filled)
        {
            var texture = new Texture2D(16, 16);
            var color = filled ? VarcoEditorStyles.Warning : VarcoEditorStyles.TextSecondary;
            var transparent = Color.clear;
            
            // Simple star shape
            int[] starX = { 8, 10, 16, 11, 13, 8, 3, 5, 0, 6 };
            int[] starY = { 16, 10, 10, 6, 0, 4, 0, 6, 10, 10 };
            
            for (int x = 0; x < 16; x++)
            {
                for (int y = 0; y < 16; y++)
                {
                    // Simplified: draw a star-like pattern
                    float dx = Mathf.Abs(x - 8);
                    float dy = Mathf.Abs(y - 8);
                    bool inside = (dx + dy < 8) || (dx < 2 && dy < 7) || (dy < 2 && dx < 7);
                    
                    if (filled)
                    {
                        texture.SetPixel(x, y, inside ? color : transparent);
                    }
                    else
                    {
                        // Outline only
                        bool outline = inside && (dx + dy > 5 || Mathf.Abs(dx - 1) < 1 || Mathf.Abs(dy - 1) < 1);
                        texture.SetPixel(x, y, outline ? color : transparent);
                    }
                }
            }
            
            texture.Apply();
            texture.hideFlags = HideFlags.HideAndDontSave;
            return texture;
        }
        
        private static Texture2D CreateRefreshIcon()
        {
            var texture = new Texture2D(16, 16);
            var color = VarcoEditorStyles.Blue;
            var transparent = Color.clear;
            
            for (int x = 0; x < 16; x++)
            {
                for (int y = 0; y < 16; y++)
                {
                    float dx = x - 8;
                    float dy = y - 8;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    bool ring = dist > 4 && dist < 6;
                    texture.SetPixel(x, y, ring ? color : transparent);
                }
            }
            
            texture.Apply();
            texture.hideFlags = HideFlags.HideAndDontSave;
            return texture;
        }
        
        private static Texture2D CreateSearchIcon()
        {
            var texture = new Texture2D(16, 16);
            var color = VarcoEditorStyles.TextSecondary;
            var transparent = Color.clear;
            
            for (int x = 0; x < 16; x++)
            {
                for (int y = 0; y < 16; y++)
                {
                    float dx = x - 6;
                    float dy = y - 10;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    bool ring = dist > 3 && dist < 5;
                    bool handle = x > 9 && x < 14 && Mathf.Abs(y - (x - 4)) < 2;
                    texture.SetPixel(x, y, (ring || handle) ? color : transparent);
                }
            }
            
            texture.Apply();
            texture.hideFlags = HideFlags.HideAndDontSave;
            return texture;
        }
        
        private static Texture2D CreateSettingsIcon()
        {
            var texture = new Texture2D(16, 16);
            var color = VarcoEditorStyles.TextSecondary;
            var transparent = Color.clear;
            
            for (int x = 0; x < 16; x++)
            {
                for (int y = 0; y < 16; y++)
                {
                    float dx = x - 8;
                    float dy = y - 8;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    bool gear = (dist > 2 && dist < 4) || (dist > 5 && dist < 7 && ((x + y) % 3 == 0));
                    texture.SetPixel(x, y, gear ? color : transparent);
                }
            }
            
            texture.Apply();
            texture.hideFlags = HideFlags.HideAndDontSave;
            return texture;
        }
        
        private static Texture2D CreateCloseIcon()
        {
            var texture = new Texture2D(16, 16);
            var color = VarcoEditorStyles.Error;
            var transparent = Color.clear;
            
            for (int x = 0; x < 16; x++)
            {
                for (int y = 0; y < 16; y++)
                {
                    bool diag1 = Mathf.Abs(x - y) < 2;
                    bool diag2 = Mathf.Abs(x - (15 - y)) < 2;
                    bool inside = x > 2 && x < 14 && y > 2 && y < 14;
                    texture.SetPixel(x, y, (inside && (diag1 || diag2)) ? color : transparent);
                }
            }
            
            texture.Apply();
            texture.hideFlags = HideFlags.HideAndDontSave;
            return texture;
        }
        
        private static Texture2D CreateCheckIcon()
        {
            var texture = new Texture2D(16, 16);
            var color = VarcoEditorStyles.Success;
            var transparent = Color.clear;
            
            for (int x = 0; x < 16; x++)
            {
                for (int y = 0; y < 16; y++)
                {
                    // Checkmark shape
                    bool check1 = x >= 3 && x <= 7 && Mathf.Abs(y - (x + 2)) < 2;
                    bool check2 = x >= 6 && x <= 13 && Mathf.Abs(y - (18 - x)) < 2;
                    texture.SetPixel(x, y, (check1 || check2) ? color : transparent);
                }
            }
            
            texture.Apply();
            texture.hideFlags = HideFlags.HideAndDontSave;
            return texture;
        }
        
        #endregion
        
        #region Public Methods
        
        /// <summary>
        /// Get icon by name - returns null if not found
        /// </summary>
        public static Texture2D GetIcon(string name)
        {
            EnsureInitialized();
            
            return name?.ToLower() switch
            {
                "play" => _playIcon,
                "stop" => _stopIcon,
                "star" => _starIcon,
                "star_filled" => _starFilledIcon,
                "refresh" => _refreshIcon,
                "search" => _searchIcon,
                "settings" => _settingsIcon,
                "close" => _closeIcon,
                "check" => _checkIcon,
                "warning" => _warningIcon,
                "error" => _errorIcon,
                "varco_logo" => null, // No custom logo yet, will return null
                _ => null
            };
        }
        
        #endregion
        
        #region Helper Methods
        
        /// <summary>
        /// Draw an icon button
        /// </summary>
        public static bool DrawIconButton(Texture2D icon, string tooltip = "", int size = 24)
        {
            var content = new GUIContent(icon, tooltip);
            return GUILayout.Button(content, GUILayout.Width(size), GUILayout.Height(size));
        }
        
        /// <summary>
        /// Draw favorite toggle star
        /// </summary>
        public static bool DrawFavoriteToggle(bool isFavorite, string tooltip = "Toggle Favorite")
        {
            var icon = isFavorite ? StarFilledIcon : StarIcon;
            var prevColor = GUI.color;
            GUI.color = isFavorite ? VarcoEditorStyles.Warning : VarcoEditorStyles.TextSecondary;
            
            bool clicked = GUILayout.Button(new GUIContent(icon, tooltip), GUIStyle.none, GUILayout.Width(20), GUILayout.Height(20));
            
            GUI.color = prevColor;
            return clicked;
        }
        
        /// <summary>
        /// Draw play/stop button
        /// </summary>
        public static bool DrawPlayButton(bool isPlaying, int size = 24)
        {
            var icon = isPlaying ? StopIcon : PlayIcon;
            var tooltip = isPlaying ? "Stop" : "Play";
            return DrawIconButton(icon, tooltip, size);
        }
        
        #endregion
    }
}
