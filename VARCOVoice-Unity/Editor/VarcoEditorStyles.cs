using UnityEngine;
using UnityEditor;

namespace VARCOVoice.Editor
{
    /// <summary>
    /// VARCO Voice Editor Style System
    /// Brand colors: Mint → Blue → Purple gradient
    /// </summary>
    public static class VarcoEditorStyles
    {
        #region Brand Colors
        
        // Primary Brand Colors
        public static readonly Color Mint = new Color32(0, 110, 255, 255);
        public static readonly Color Blue = new Color32(77, 155, 255, 255);
        public static readonly Color Purple = new Color32(255, 255, 255, 255);
        
        // Background Colors
        public static readonly Color BackgroundDark = new Color32(0, 0, 0, 255);
        public static readonly Color BackgroundSecondary = new Color32(12, 12, 12, 255);
        public static readonly Color CardBackground = new Color32(18, 18, 18, 217);
        
        // State Colors
        public static readonly Color Success = new Color32(0, 110, 255, 255);
        public static readonly Color Warning = new Color32(77, 155, 255, 255);
        public static readonly Color Error = new Color32(255, 255, 255, 255);
        
        // Text Colors
        public static readonly Color TextPrimary = new Color32(255, 255, 255, 255);
        public static readonly Color TextSecondary = new Color32(201, 201, 201, 255);
        public static readonly Color TextMuted = new Color32(138, 138, 138, 255);
        
        #endregion
        
        #region Cached Styles
        
        private const string InterFontPath = "Packages/com.varco.voice/Runtime/Inter/Inter-VariableFont_opsz,wght.ttf";
        private const string GoogleSansFontPath = "Packages/com.varco.voice/Runtime/Google_Sans_Flex/GoogleSansFlex-VariableFont_GRAD,ROND,opsz,slnt,wdth,wght.ttf";
        private static Font _bodyFont;
        private static Font _displayFont;

        private static GUIStyle _headerStyle;
        private static GUIStyle _cardStyle;
        private static GUIStyle _selectedCardStyle;
        private static GUIStyle _pillButtonStyle;
        private static GUIStyle _pillButtonActiveStyle;
        private static GUIStyle _accentButtonStyle;
        private static GUIStyle _statusBadgeStyle;
        private static GUIStyle _tabStyle;
        private static GUIStyle _tabActiveStyle;
        private static GUIStyle _centeredLabel;
        
        private static Texture2D _gradientTexture;
        private static Texture2D _cardTexture;
        private static Texture2D _selectedCardTexture;
        private static Texture2D _pillTexture;
        private static Texture2D _pillActiveTexture;
        
        private static bool _initialized;
        
        #endregion
        
        #region Properties
        
        public static GUIStyle HeaderStyle
        {
            get
            {
                EnsureInitialized();
                return _headerStyle;
            }
        }
        
        public static GUIStyle CardStyle
        {
            get
            {
                EnsureInitialized();
                return _cardStyle;
            }
        }
        
        public static GUIStyle SelectedCardStyle
        {
            get
            {
                EnsureInitialized();
                return _selectedCardStyle;
            }
        }
        
        public static GUIStyle PillButtonStyle
        {
            get
            {
                EnsureInitialized();
                return _pillButtonStyle;
            }
        }
        
        public static GUIStyle PillButtonActiveStyle
        {
            get
            {
                EnsureInitialized();
                return _pillButtonActiveStyle;
            }
        }
        
        public static GUIStyle AccentButtonStyle
        {
            get
            {
                EnsureInitialized();
                return _accentButtonStyle;
            }
        }
        
        public static GUIStyle StatusBadgeStyle
        {
            get
            {
                EnsureInitialized();
                return _statusBadgeStyle;
            }
        }
        
        public static GUIStyle TabStyle
        {
            get
            {
                EnsureInitialized();
                return _tabStyle;
            }
        }
        
        public static GUIStyle TabActiveStyle
        {
            get
            {
                EnsureInitialized();
                return _tabActiveStyle;
            }
        }
        
        public static GUIStyle CenteredLabel
        {
            get
            {
                EnsureInitialized();
                return _centeredLabel;
            }
        }
        
        public static Texture2D GradientTexture
        {
            get
            {
                EnsureInitialized();
                return _gradientTexture;
            }
        }
        
        #endregion
        
        #region Initialization
        
        private static void EnsureInitialized()
        {
            if (_initialized) return;
            
            LoadFonts();
            CreateTextures();
            CreateStyles();
            
            _initialized = true;
        }
        

        private static void LoadFonts()
        {
            if (_bodyFont == null) _bodyFont = AssetDatabase.LoadAssetAtPath<Font>(InterFontPath);
            if (_displayFont == null) _displayFont = AssetDatabase.LoadAssetAtPath<Font>(GoogleSansFontPath);
        }

        private static void CreateTextures()
        {
            // Brand gradient texture (Mint → Blue → Purple)
            _gradientTexture = CreateHorizontalGradient(256, 4, Mint, Blue, Purple);
            
            // Card background
            _cardTexture = CreateSolidTexture(2, 2, CardBackground);
            
            // Selected card with gradient border effect
            _selectedCardTexture = CreateBorderTexture(32, 32, CardBackground, Mint, 2);
            
            // Pill button
            _pillTexture = CreateRoundedTexture(64, 24, new Color32(60, 60, 80, 200));
            
            // Active pill with gradient
            _pillActiveTexture = CreateRoundedGradientTexture(64, 24, Mint, Blue);
        }
        
        private static void CreateStyles()
        {
            // Header Style
            _headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                fixedHeight = 28,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(8, 8, 4, 8)
            };
            _headerStyle.normal.textColor = TextPrimary;
            if (_displayFont != null) _headerStyle.font = _displayFont;
            
            // Card Style
            _cardStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(12, 12, 8, 8),
                margin = new RectOffset(0, 0, 4, 4),
                border = new RectOffset(4, 4, 4, 4)
            };
            _cardStyle.normal.background = _cardTexture;
            
            // Selected Card Style
            _selectedCardStyle = new GUIStyle(_cardStyle);
            _selectedCardStyle.normal.background = _selectedCardTexture;
            
            // Pill Button Style
            _pillButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                padding = new RectOffset(12, 12, 4, 4),
                margin = new RectOffset(2, 2, 2, 2),
                alignment = TextAnchor.MiddleCenter,
                fixedHeight = 24
            };
            _pillButtonStyle.normal.background = _pillTexture;
            _pillButtonStyle.normal.textColor = TextSecondary;
            if (_bodyFont != null) _pillButtonStyle.font = _bodyFont;
            _pillButtonStyle.hover.background = _pillActiveTexture;
            _pillButtonStyle.hover.textColor = TextPrimary;
            
            // Active Pill Style
            _pillButtonActiveStyle = new GUIStyle(_pillButtonStyle);
            _pillButtonActiveStyle.normal.background = _pillActiveTexture;
            _pillButtonActiveStyle.normal.textColor = TextPrimary;
            if (_bodyFont != null) _pillButtonActiveStyle.font = _bodyFont;
            
            // Accent Button Style
            _accentButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(16, 16, 8, 8),
                alignment = TextAnchor.MiddleCenter
            };
            _accentButtonStyle.normal.background = _pillActiveTexture;
            _accentButtonStyle.normal.textColor = TextPrimary;
            if (_displayFont != null) _accentButtonStyle.font = _displayFont;
            else if (_bodyFont != null) _accentButtonStyle.font = _bodyFont;
            _accentButtonStyle.hover.background = _gradientTexture;
            
            // Status Badge Style
            _statusBadgeStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 10,
                padding = new RectOffset(8, 8, 2, 2),
                alignment = TextAnchor.MiddleCenter
            };
            _statusBadgeStyle.normal.textColor = TextPrimary;
            if (_bodyFont != null) _statusBadgeStyle.font = _bodyFont;
            
            // Tab Style
            _tabStyle = new GUIStyle(_pillButtonStyle)
            {
                fixedHeight = 28
            };
            if (_displayFont != null) _tabStyle.font = _displayFont;
            else if (_bodyFont != null) _tabStyle.font = _bodyFont;
            
            // Tab Active Style  
            _tabActiveStyle = new GUIStyle(_pillButtonActiveStyle)
            {
                fixedHeight = 28
            };
            if (_displayFont != null) _tabActiveStyle.font = _displayFont;
            else if (_bodyFont != null) _tabActiveStyle.font = _bodyFont;

            // Centered Label
            _centeredLabel = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter
            };
            if (_bodyFont != null) _centeredLabel.font = _bodyFont;
        }
        
        #endregion
        
        #region Texture Generation
        
        public static Texture2D CreateSolidTexture(int width, int height, Color color)
        {
            var texture = new Texture2D(width, height);
            var pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }
            texture.SetPixels(pixels);
            texture.Apply();
            texture.hideFlags = HideFlags.HideAndDontSave;
            return texture;
        }
        
        public static Texture2D CreateHorizontalGradient(int width, int height, Color start, Color mid, Color end)
        {
            var texture = new Texture2D(width, height);
            
            for (int x = 0; x < width; x++)
            {
                float t = (float)x / width;
                Color color;
                
                if (t < 0.5f)
                {
                    color = Color.Lerp(start, mid, t * 2f);
                }
                else
                {
                    color = Color.Lerp(mid, end, (t - 0.5f) * 2f);
                }
                
                for (int y = 0; y < height; y++)
                {
                    texture.SetPixel(x, y, color);
                }
            }
            
            texture.Apply();
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.hideFlags = HideFlags.HideAndDontSave;
            return texture;
        }
        
        public static Texture2D CreateBorderTexture(int width, int height, Color fill, Color border, int borderWidth)
        {
            var texture = new Texture2D(width, height);
            
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    bool isBorder = x < borderWidth || x >= width - borderWidth || 
                                   y < borderWidth || y >= height - borderWidth;
                    texture.SetPixel(x, y, isBorder ? border : fill);
                }
            }
            
            texture.Apply();
            texture.hideFlags = HideFlags.HideAndDontSave;
            return texture;
        }
        
        public static Texture2D CreateRoundedTexture(int width, int height, Color color)
        {
            var texture = new Texture2D(width, height);
            float radius = height / 2f;
            
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    float dx = 0, dy = Mathf.Abs(y - height / 2f);
                    
                    if (x < radius) dx = radius - x;
                    else if (x > width - radius) dx = x - (width - radius);
                    
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = 1f - Mathf.Clamp01((dist - radius + 1f) / 2f);
                    
                    var c = color;
                    c.a *= alpha;
                    texture.SetPixel(x, y, c);
                }
            }
            
            texture.Apply();
            texture.hideFlags = HideFlags.HideAndDontSave;
            return texture;
        }
        
        public static Texture2D CreateRoundedGradientTexture(int width, int height, Color start, Color end)
        {
            var texture = new Texture2D(width, height);
            float radius = height / 2f;
            
            for (int x = 0; x < width; x++)
            {
                float t = (float)x / width;
                Color color = Color.Lerp(start, end, t);
                
                for (int y = 0; y < height; y++)
                {
                    float dx = 0, dy = Mathf.Abs(y - height / 2f);
                    
                    if (x < radius) dx = radius - x;
                    else if (x > width - radius) dx = x - (width - radius);
                    
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = 1f - Mathf.Clamp01((dist - radius + 1f) / 2f);
                    
                    var c = color;
                    c.a *= alpha;
                    texture.SetPixel(x, y, c);
                }
            }
            
            texture.Apply();
            texture.hideFlags = HideFlags.HideAndDontSave;
            return texture;
        }
        
        #endregion
        
        #region Helper Methods
        
        /// <summary>
        /// Draw a status indicator dot
        /// </summary>
        public static void DrawStatusDot(bool isConnected)
        {
            var color = isConnected ? Success : Error;
            var prevColor = GUI.color;
            GUI.color = color;
            GUILayout.Label("●", GUILayout.Width(16));
            GUI.color = prevColor;
        }
        
        /// <summary>
        /// Draw connection status badge
        /// </summary>
        public static void DrawConnectionBadge(bool isConnected, int voiceCount = 0)
        {
            EditorGUILayout.BeginHorizontal(CardStyle);
            
            DrawStatusDot(isConnected);
            
            var statusText = isConnected ? "Connected" : "Disconnected";
            GUILayout.Label(statusText, EditorStyles.boldLabel);
            
            GUILayout.FlexibleSpace();
            
            if (isConnected && voiceCount > 0)
            {
                GUILayout.Label($"{voiceCount:N0} voices", EditorStyles.miniLabel);
            }
            
            EditorGUILayout.EndHorizontal();
        }
        
        /// <summary>
        /// Draw a visual slider with gradient track
        /// </summary>
        public static float DrawVisualSlider(string label, float value, float min, float max, float labelWidth = 60f)
        {
            EditorGUILayout.BeginHorizontal();
            
            GUILayout.Label(label, GUILayout.Width(labelWidth));
            
            // Draw slider background
            var rect = GUILayoutUtility.GetRect(GUIContent.none, GUI.skin.horizontalSlider, GUILayout.ExpandWidth(true));
            
            // Draw gradient track
            float fillWidth = (value - min) / (max - min) * rect.width;
            var fillRect = new Rect(rect.x, rect.y + 2, fillWidth, rect.height - 4);
            GUI.DrawTexture(fillRect, GradientTexture, ScaleMode.StretchToFill);
            
            // Draw slider
            value = GUI.HorizontalSlider(rect, value, min, max);
            
            // Value label
            GUILayout.Label($"{value:F2}", GUILayout.Width(40));
            
            EditorGUILayout.EndHorizontal();
            
            return value;
        }
        
        /// <summary>
        /// Draw accent button with gradient
        /// </summary>
        public static bool DrawAccentButton(string text, params GUILayoutOption[] options)
        {
            return GUILayout.Button(text, AccentButtonStyle, options);
        }
        
        /// <summary>
        /// Draw pill/tab button
        /// </summary>
        public static bool DrawPillButton(string text, bool isActive, params GUILayoutOption[] options)
        {
            var style = isActive ? PillButtonActiveStyle : PillButtonStyle;
            return GUILayout.Button(text, style, options);
        }
        
        /// <summary>
        /// Draw section header with underline
        /// </summary>
        public static void DrawSectionHeader(string title)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField(title, HeaderStyle);
            
            EditorGUILayout.Space(4);  // Space between text and line
            
            var rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(2));
            GUI.DrawTexture(rect, GradientTexture, ScaleMode.StretchToFill);
            
            EditorGUILayout.Space(4);
        }
        
        /// <summary>
        /// Force style reinitialization
        /// </summary>
        public static void Reinitialize()
        {
            _initialized = false;
        }
        
        #endregion
    }
}
