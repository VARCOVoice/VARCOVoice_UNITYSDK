using System.Collections.Generic;
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
        private static readonly Dictionary<VisualElement, System.Action<bool>> Subscriptions = new Dictionary<VisualElement, System.Action<bool>>();
        
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

            Unsubscribe(root);
            Apply(root);

            System.Action<bool> handler = isLight =>
            {
                if (root.panel != null)
                {
                    root.EnableInClassList("theme-light", isLight);
                }
            };

            Subscriptions[root] = handler;
            OnThemeChanged += handler;
        }

        public static void Unsubscribe(VisualElement root)
        {
            if (root == null) return;

            if (Subscriptions.TryGetValue(root, out var handler))
            {
                OnThemeChanged -= handler;
                Subscriptions.Remove(root);
            }
        }
    }
}
