using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;

namespace VARCOVoice.Editor
{
    /// <summary>
    /// Static helper for VARCO theme management
    /// </summary>
    public static class VarcoTheme
    {
        private const string PREF_LIGHT_MODE = "VARCOVoice_LightMode";
        
        /// <summary>
        /// Gets whether light mode is enabled
        /// </summary>
        public static bool IsLightMode
        {
            get => EditorPrefs.GetBool(PREF_LIGHT_MODE, false);
            set
            {
                EditorPrefs.SetBool(PREF_LIGHT_MODE, value);
                OnThemeChanged?.Invoke(value);
            }
        }
        
        /// <summary>
        /// Event fired when theme changes
        /// </summary>
        public static event System.Action<bool> OnThemeChanged;
        
        /// <summary>
        /// Apply theme to a root VisualElement
        /// </summary>
        public static void Apply(VisualElement root)
        {
            if (root == null) return;
            root.EnableInClassList("theme-light", IsLightMode);
        }
        
        /// <summary>
        /// Subscribe a window to theme changes and apply current theme
        /// </summary>
        public static void Subscribe(VisualElement root)
        {
            if (root == null) return;
            
            Apply(root);
            
            // Create a weak reference to avoid memory leaks
            var weakRoot = new System.WeakReference<VisualElement>(root);
            OnThemeChanged += (isLight) =>
            {
                if (weakRoot.TryGetTarget(out var target))
                {
                    target.EnableInClassList("theme-light", isLight);
                }
            };
        }
    }
}
