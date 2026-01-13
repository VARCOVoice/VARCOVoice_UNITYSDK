using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VARCOVoice.DSP;

namespace VARCOVoice.Editor
{
    /// <summary>
    /// Static utility class for managing DSP Presets.
    /// Handles Save, Load, Delete operations in a standardized folder.
    /// </summary>
    public static class DSPPresetManager
    {
        private const string PRESET_FOLDER = "Assets/VARCOVoice/Presets";

        /// <summary>
        /// Ensures the preset folder exists.
        /// </summary>
        private static void EnsurePresetFolder()
        {
            if (!AssetDatabase.IsValidFolder(PRESET_FOLDER))
            {
                // Create folder hierarchy
                string[] parts = PRESET_FOLDER.Split('/');
                string current = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    string next = current + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(next))
                    {
                        AssetDatabase.CreateFolder(current, parts[i]);
                    }
                    current = next;
                }
            }
        }

        /// <summary>
        /// Saves the current chain as a new preset asset.
        /// </summary>
        public static DSPPreset SavePreset(DSPChain chain, string presetName)
        {
            if (chain == null || string.IsNullOrEmpty(presetName)) return null;
            
            EnsurePresetFolder();
            
            string safeName = SanitizeFileName(presetName);
            string path = $"{PRESET_FOLDER}/{safeName}.asset";
            
            // Check if exists
            var existing = AssetDatabase.LoadAssetAtPath<DSPPreset>(path);
            if (existing != null)
            {
                // Update existing
                existing.PresetName = presetName;
                existing.CaptureFromChain(chain);
                EditorUtility.SetDirty(existing);
                AssetDatabase.SaveAssets();
                Debug.Log($"[DSPPresetManager] Updated preset: {presetName}");
                return existing;
            }
            
            // Create new
            var preset = ScriptableObject.CreateInstance<DSPPreset>();
            preset.PresetName = presetName;
            preset.CaptureFromChain(chain);
            
            AssetDatabase.CreateAsset(preset, path);
            AssetDatabase.SaveAssets();
            
            Debug.Log($"[DSPPresetManager] Saved new preset: {presetName} at {path}");
            return preset;
        }

        /// <summary>
        /// Loads a preset and applies it to the target chain.
        /// </summary>
        public static void LoadPreset(DSPPreset preset, DSPChain chain)
        {
            if (preset == null || chain == null) return;
            
            preset.ApplyToChain(chain);
            EditorUtility.SetDirty(chain);
            Debug.Log($"[DSPPresetManager] Loaded preset: {preset.PresetName}");
        }

        /// <summary>
        /// Deletes a preset asset.
        /// </summary>
        public static void DeletePreset(DSPPreset preset)
        {
            if (preset == null) return;
            
            string path = AssetDatabase.GetAssetPath(preset);
            if (!string.IsNullOrEmpty(path))
            {
                AssetDatabase.DeleteAsset(path);
                Debug.Log($"[DSPPresetManager] Deleted preset: {preset.PresetName}");
            }
        }

        /// <summary>
        /// Gets all available user presets.
        /// </summary>
        public static List<DSPPreset> GetAllPresets()
        {
            EnsurePresetFolder();
            
            var guids = AssetDatabase.FindAssets("t:DSPPreset", new[] { PRESET_FOLDER });
            return guids
                .Select(guid => AssetDatabase.LoadAssetAtPath<DSPPreset>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(p => p != null)
                .OrderBy(p => p.PresetName)
                .ToList();
        }

        /// <summary>
        /// Gets preset names for dropdown UI.
        /// </summary>
        public static string[] GetPresetNames()
        {
            return GetAllPresets().Select(p => p.PresetName).ToArray();
        }

        private static string SanitizeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }
            return name.Trim();
        }
    }
}
