using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using System.Linq;

namespace VARCOVoice.Editor
{
    public static class AudioUtilWrapper
    {
        private static MethodInfo _playClipMethod;
        private static MethodInfo _stopAllClipsMethod;
        private static MethodInfo _getClipPositionMethod;
        private static MethodInfo _isClipPlayingMethod;

        static AudioUtilWrapper()
        {
            Initialize();
        }

        private static void Initialize()
        {
            Assembly unityEditorAssembly = typeof(AudioImporter).Assembly;
            Type audioUtilClass = unityEditorAssembly.GetType("UnityEditor.AudioUtil");

            if (audioUtilClass == null)
            {
                Debug.LogError("UnityEditor.AudioUtil class not found!");
                return;
            }

            // Inspect and Log all methods to help find correct signatures (Diagnostic)
            var methods = audioUtilClass.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            // Debug.Log($"Found {methods.Length} methods in AudioUtil"); // Optional: prevent spam if too many

            // 1. PlayClip / PlayPreviewClip
            // Unity 2022.3 often uses PlayPreviewClip(AudioClip, int, bool)
            _playClipMethod = methods.FirstOrDefault(m => 
                (m.Name == "PlayPreviewClip" || m.Name == "PlayClip") && 
                m.GetParameters().Length == 3 &&
                m.GetParameters()[0].ParameterType == typeof(AudioClip));

            // 2. StopAllClips / StopAllPreviewClips
            _stopAllClipsMethod = methods.FirstOrDefault(m => 
                (m.Name == "StopAllPreviewClips" || m.Name == "StopAllClips") && 
                m.GetParameters().Length == 0);

            // 3. GetClipPosition
            // Unity 2022.3: GetPreviewClipPosition() -> returns float/double, no args?
            // Older: GetClipPosition(AudioClip)
            _getClipPositionMethod = methods.FirstOrDefault(m => 
                (m.Name == "GetPreviewClipPosition" && m.GetParameters().Length == 0) ||
                (m.Name == "GetClipPosition" && m.GetParameters().Length == 1));
            
            // 4. IsClipPlaying
            // Unity 2022.3: IsPreviewClipPlaying()
            _isClipPlayingMethod = methods.FirstOrDefault(m => 
                (m.Name == "IsPreviewClipPlaying" && m.GetParameters().Length == 0) || // Global check?
                (m.Name == "IsClipPlaying" && m.GetParameters().Length == 1));

            if (_playClipMethod == null) Debug.LogError("[AudioUtilWrapper] PlayPreviewClip/PlayClip not found");
            if (_stopAllClipsMethod == null) Debug.LogError("[AudioUtilWrapper] StopAllPreviewClips/StopAllClips not found");
            if (_getClipPositionMethod == null) Debug.LogError("[AudioUtilWrapper] GetPreviewClipPosition/GetClipPosition not found");
        }

        public static void PlayClip(AudioClip clip, int startSample = 0, bool loop = false)
        {
            if (_playClipMethod != null)
                _playClipMethod.Invoke(null, new object[] { clip, startSample, loop });
        }

        public static void StopAllClips()
        {
            if (_stopAllClipsMethod != null)
                _stopAllClipsMethod.Invoke(null, null);
        }

        public static float GetClipPosition(AudioClip clip)
        {
            if (_getClipPositionMethod != null)
            {
                // Handle different signatures
                if (_getClipPositionMethod.GetParameters().Length == 0)
                {
                    // GetPreviewClipPosition() - global position
                    object result = _getClipPositionMethod.Invoke(null, null);
                    return Convert.ToSingle(result);
                }
                else if (clip != null)
                {
                    // GetClipPosition(AudioClip)
                    object result = _getClipPositionMethod.Invoke(null, new object[] { clip });
                    return Convert.ToSingle(result);
                }
            }
            return 0f;
        }
        
        public static bool IsClipPlaying(AudioClip clip)
        {
             if (_isClipPlayingMethod != null)
             {
                 if (_isClipPlayingMethod.GetParameters().Length == 0)
                 {
                     // IsPreviewClipPlaying() - global check
                     return (bool)_isClipPlayingMethod.Invoke(null, null);
                 }
                 else if (clip != null)
                 {
                     return (bool)_isClipPlayingMethod.Invoke(null, new object[] { clip });
                 }
             }
             return false;
        }

        public static bool IsAvailable => _playClipMethod != null && _stopAllClipsMethod != null && _getClipPositionMethod != null;
    }
}
