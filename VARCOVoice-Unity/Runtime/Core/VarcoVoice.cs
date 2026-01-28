using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace VARCOVoice
{
    /// <summary>
    /// VARCO Voice speaker data model
    /// </summary>
    [Serializable]
    public class VarcoVoice
    {
        /// <summary>
        /// Speaker UUID (unique identifier)
        /// </summary>
        [JsonProperty("speaker_uuid")]
        public string SpeakerUuid { get; set; }
        
        /// <summary>
        /// Speaker name for API calls
        /// </summary>
        [JsonProperty("speaker_name")]
        public string SpeakerName { get; set; }
        
        /// <summary>
        /// SaaS display name (user-friendly name)
        /// </summary>
        [JsonProperty("saas_name")]
        public string SaasName { get; set; }
        
        /// <summary>
        /// Voice description (gender, age, tone, personality)
        /// </summary>
        [JsonProperty("description")]
        public string Description { get; set; }
        
        // Parsed properties from description
        public Gender Gender { get; private set; }
        public AgeGroup AgeGroup { get; private set; }
        public ToneType Tone { get; private set; }
        public string Personality { get; private set; }
        
        /// <summary>
        /// Parse description string to extract voice properties
        /// Format: "성별, 연령, 음높이, 음색, 성격"
        /// Example: "남성, 청년, 저음, 맑음, 냉정한"
        /// </summary>
        public void ParseDescription()
        {
            if (string.IsNullOrEmpty(Description)) return;
            
            var parts = Description.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries);
            
            if (parts.Length >= 1)
            {
                Gender = parts[0] switch
                {
                    "남성" => Gender.Male,
                    "여성" => Gender.Female,
                    _ => Gender.Unknown
                };
            }
            
            if (parts.Length >= 2)
            {
                AgeGroup = parts[1] switch
                {
                    "어린이" => AgeGroup.Child,
                    "청년" => AgeGroup.Young,
                    "중년" => AgeGroup.Middle,
                    "노년" => AgeGroup.Senior,
                    _ => AgeGroup.Unknown
                };
            }
            
            if (parts.Length >= 4)
            {
                Tone = parts[3] switch
                {
                    "맑음" => ToneType.Clear,
                    "거침" => ToneType.Rough,
                    "굵음" => ToneType.Thick,
                    "얇음" => ToneType.Thin,
                    _ => ToneType.Unknown
                };
            }
            
            if (parts.Length >= 5)
            {
                Personality = parts[4];
            }
        }
        
        /// <summary>
        /// Get emotion type from speaker name if available
        /// </summary>
        public EmotionType GetEmotion()
        {
            if (string.IsNullOrEmpty(SpeakerName)) return EmotionType.Neutral;
            
            if (SpeakerName.Contains("(중립)")) return EmotionType.Neutral;
            if (SpeakerName.Contains("(행복)")) return EmotionType.Happy;
            if (SpeakerName.Contains("(슬픔)")) return EmotionType.Sad;
            if (SpeakerName.Contains("(분노)")) return EmotionType.Angry;
            if (SpeakerName.Contains("(두려움)")) return EmotionType.Fear;
            if (SpeakerName.Contains("(놀람)")) return EmotionType.Surprise;
            
            return EmotionType.Neutral;
        }
        
        /// <summary>
        /// Get base name without emotion suffix
        /// </summary>
        public string GetBaseName()
        {
            if (string.IsNullOrEmpty(SpeakerName)) return "";
            
            var idx = SpeakerName.IndexOf('(');
            return idx > 0 ? SpeakerName.Substring(0, idx) : SpeakerName;
        }
        
        public override string ToString()
        {
            return $"{SpeakerName} - {Description}";
        }
    }
    
    /// <summary>
    /// Gender type
    /// </summary>
    public enum Gender
    {
        Unknown,
        Male,
        Female
    }
    
    /// <summary>
    /// Age group
    /// </summary>
    public enum AgeGroup
    {
        Unknown,
        Child,      // 어린이
        Young,      // 청년
        Middle,     // 중년
        Senior      // 노년
    }
    
    /// <summary>
    /// Voice tone type
    /// </summary>
    public enum ToneType
    {
        Unknown,
        Clear,      // 맑음
        Rough,      // 거침
        Thick,      // 굵음
        Thin        // 얇음
    }
    
    /// <summary>
    /// Emotion type
    /// </summary>
    public enum EmotionType
    {
        Neutral,    // 중립
        Happy,      // 행복
        Sad,        // 슬픔
        Angry,      // 분노
        Fear,       // 두려움
        Surprise    // 놀람
    }
    
    /// <summary>
    /// Voice filter for searching
    /// </summary>
    [Serializable]
    public class VoiceFilter
    {
        public Gender? Gender { get; set; }
        public AgeGroup? AgeGroup { get; set; }
        public ToneType? Tone { get; set; }
        public EmotionType? Emotion { get; set; }
        public string SearchText { get; set; }
        
        public bool Matches(VarcoVoice voice)
        {
            if (Gender.HasValue && voice.Gender != Gender.Value)
                return false;
            
            if (AgeGroup.HasValue && voice.AgeGroup != AgeGroup.Value)
                return false;
            
            if (Tone.HasValue && voice.Tone != Tone.Value)
                return false;
            
            if (Emotion.HasValue && voice.GetEmotion() != Emotion.Value)
                return false;
            
            if (!string.IsNullOrEmpty(SearchText))
            {
                var lowerSearch = SearchText.ToLower();
                if (!voice.SpeakerName.ToLower().Contains(lowerSearch) &&
                    !voice.Description.ToLower().Contains(lowerSearch) &&
                    (voice.SaasName == null || !voice.SaasName.ToLower().Contains(lowerSearch)))
                    return false;
            }
            
            return true;
        }
    }
}
