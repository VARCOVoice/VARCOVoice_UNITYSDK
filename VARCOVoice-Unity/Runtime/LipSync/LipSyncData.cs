using System;
using System.Collections.Generic;
using UnityEngine;

namespace VARCOVoice.LipSync
{
    /// <summary>
    /// Viseme types based on mouth shapes
    /// </summary>
    public enum VisemeType
    {
        /// <summary>Silent - mouth closed</summary>
        Silence = 0,
        
        /// <summary>AA - as in "father" (ㅏ, ㅓ)</summary>
        AA = 1,
        
        /// <summary>EE - as in "see" (ㅣ, ㅔ, ㅐ)</summary>
        EE = 2,
        
        /// <summary>IH - as in "sit"</summary>
        IH = 3,
        
        /// <summary>OH - as in "go" (ㅗ, ㅚ)</summary>
        OH = 4,
        
        /// <summary>OO - as in "too" (ㅜ, ㅟ)</summary>
        OO = 5,
        
        /// <summary>CH/SH - as in "church" (ㅈ, ㅊ, ㅅ, ㅆ)</summary>
        CH = 6,
        
        /// <summary>FF/VV - as in "five" (ㅍ, ㅎ)</summary>
        FF = 7,
        
        /// <summary>TH - as in "think" (ㄷ, ㅌ, ㄴ, ㄹ)</summary>
        TH = 8,
        
        /// <summary>PP/BB/MM - closed lips (ㅁ, ㅂ, ㅃ, ㅍ)</summary>
        PP = 9,
        
        /// <summary>KK/GG - back of throat (ㄱ, ㅋ, ㄲ)</summary>
        KK = 10,
        
        /// <summary>NN - nasal (ㄴ, ㅇ)</summary>
        NN = 11,
        
        /// <summary>RR - R sound (ㄹ)</summary>
        RR = 12,
        
        /// <summary>DD - D/T sound</summary>
        DD = 13,
        
        /// <summary>SS - S sound (ㅅ, ㅆ)</summary>
        SS = 14
    }
    
    /// <summary>
    /// Single viseme keyframe
    /// </summary>
    [Serializable]
    public struct VisemeKeyframe
    {
        /// <summary>Time in seconds from audio start</summary>
        public float Time;
        
        /// <summary>Viseme type at this keyframe</summary>
        public VisemeType Viseme;
        
        /// <summary>Weight/intensity of the viseme (0-1)</summary>
        public float Weight;
        
        public VisemeKeyframe(float time, VisemeType viseme, float weight = 1f)
        {
            Time = time;
            Viseme = viseme;
            Weight = weight;
        }
    }
    
    /// <summary>
    /// Lip sync data for an audio clip
    /// </summary>
    [Serializable]
    public class LipSyncData
    {
        /// <summary>Associated audio clip name</summary>
        public string ClipName;
        
        /// <summary>Total duration in seconds</summary>
        public float Duration;
        
        /// <summary>Viseme keyframes</summary>
        public List<VisemeKeyframe> Keyframes = new List<VisemeKeyframe>();
        
        /// <summary>Audio energy levels (for amplitude-based lip sync)</summary>
        public List<float> EnergyLevels = new List<float>();
        
        /// <summary>Energy sample rate (samples per second)</summary>
        public float EnergySampleRate = 30f;
        
        /// <summary>
        /// Get viseme at specific time
        /// </summary>
        public VisemeKeyframe GetVisemeAtTime(float time)
        {
            if (Keyframes.Count == 0)
                return new VisemeKeyframe(time, VisemeType.Silence, 0f);
            
            // Find the keyframe at or before this time
            VisemeKeyframe result = Keyframes[0];
            
            for (int i = 0; i < Keyframes.Count; i++)
            {
                if (Keyframes[i].Time <= time)
                {
                    result = Keyframes[i];
                }
                else
                {
                    break;
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Get interpolated viseme weights at specific time
        /// Returns weights for all viseme types
        /// </summary>
        public float[] GetVisemeWeightsAtTime(float time)
        {
            float[] weights = new float[15]; // 15 viseme types
            
            var current = GetVisemeAtTime(time);
            weights[(int)current.Viseme] = current.Weight;
            
            // Add energy-based intensity if available
            if (EnergyLevels.Count > 0)
            {
                int energyIndex = Mathf.FloorToInt(time * EnergySampleRate);
                energyIndex = Mathf.Clamp(energyIndex, 0, EnergyLevels.Count - 1);
                float energy = EnergyLevels[energyIndex];
                
                // Modulate weight by energy
                weights[(int)current.Viseme] *= Mathf.Clamp01(energy * 2f);
            }
            
            return weights;
        }
        
        /// <summary>
        /// Get energy level at specific time
        /// </summary>
        public float GetEnergyAtTime(float time)
        {
            if (EnergyLevels.Count == 0) return 0f;
            
            int index = Mathf.FloorToInt(time * EnergySampleRate);
            index = Mathf.Clamp(index, 0, EnergyLevels.Count - 1);
            return EnergyLevels[index];
        }
    }
    
    /// <summary>
    /// Blend shape configuration for a viseme
    /// </summary>
    [Serializable]
    public class VisemeBlendShape
    {
        public VisemeType Viseme;
        public string BlendShapeName;
        public float Weight = 100f;
    }
    
    /// <summary>
    /// Lip sync profile - maps visemes to blend shapes
    /// </summary>
    [CreateAssetMenu(fileName = "LipSyncProfile", menuName = "VARCO Voice/Lip Sync Profile")]
    public class LipSyncProfile : ScriptableObject
    {
        [Header("Blend Shape Mappings")]
        public List<VisemeBlendShape> BlendShapes = new List<VisemeBlendShape>();
        
        [Header("Settings")]
        [Range(0f, 1f)]
        public float Smoothing = 0.3f;
        
        [Range(0f, 2f)]
        public float Intensity = 1f;
        
        /// <summary>
        /// Get blend shape name for viseme
        /// </summary>
        public string GetBlendShapeName(VisemeType viseme)
        {
            foreach (var bs in BlendShapes)
            {
                if (bs.Viseme == viseme)
                    return bs.BlendShapeName;
            }
            return null;
        }
        
        /// <summary>
        /// Create default profile with standard viseme names
        /// </summary>
        public void SetupDefaultMappings()
        {
            BlendShapes.Clear();
            
            BlendShapes.Add(new VisemeBlendShape { Viseme = VisemeType.Silence, BlendShapeName = "viseme_sil" });
            BlendShapes.Add(new VisemeBlendShape { Viseme = VisemeType.AA, BlendShapeName = "viseme_aa" });
            BlendShapes.Add(new VisemeBlendShape { Viseme = VisemeType.EE, BlendShapeName = "viseme_E" });
            BlendShapes.Add(new VisemeBlendShape { Viseme = VisemeType.IH, BlendShapeName = "viseme_I" });
            BlendShapes.Add(new VisemeBlendShape { Viseme = VisemeType.OH, BlendShapeName = "viseme_O" });
            BlendShapes.Add(new VisemeBlendShape { Viseme = VisemeType.OO, BlendShapeName = "viseme_U" });
            BlendShapes.Add(new VisemeBlendShape { Viseme = VisemeType.CH, BlendShapeName = "viseme_CH" });
            BlendShapes.Add(new VisemeBlendShape { Viseme = VisemeType.FF, BlendShapeName = "viseme_FF" });
            BlendShapes.Add(new VisemeBlendShape { Viseme = VisemeType.TH, BlendShapeName = "viseme_TH" });
            BlendShapes.Add(new VisemeBlendShape { Viseme = VisemeType.PP, BlendShapeName = "viseme_PP" });
            BlendShapes.Add(new VisemeBlendShape { Viseme = VisemeType.KK, BlendShapeName = "viseme_kk" });
            BlendShapes.Add(new VisemeBlendShape { Viseme = VisemeType.NN, BlendShapeName = "viseme_nn" });
            BlendShapes.Add(new VisemeBlendShape { Viseme = VisemeType.RR, BlendShapeName = "viseme_RR" });
            BlendShapes.Add(new VisemeBlendShape { Viseme = VisemeType.DD, BlendShapeName = "viseme_DD" });
            BlendShapes.Add(new VisemeBlendShape { Viseme = VisemeType.SS, BlendShapeName = "viseme_SS" });
        }
    }
}
