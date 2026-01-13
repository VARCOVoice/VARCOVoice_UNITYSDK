using System;
using NUnit.Framework;
using VARCOVoice;

namespace VARCOVoice.Tests
{
    /// <summary>
    /// Unit tests for AudioCacheManager
    /// </summary>
    [TestFixture]
    public class AudioCacheManagerTests
    {
        private AudioCacheManager _cache;
        
        [SetUp]
        public void Setup()
        {
            // We can't use the singleton in tests, so we'll test the key generation
        }
        
        [TearDown]
        public void TearDown()
        {
        }
        
        [Test]
        public void GenerateKey_SameInputs_ReturnsSameKey()
        {
            // Arrange
            var cache = AudioCacheManager.Instance;
            string text = "안녕하세요";
            string voice = "멀더";
            float speed = 1.0f;
            float pitch = 1.0f;
            int quality = 8;
            
            // Act
            string key1 = cache.GenerateKey(text, voice, speed, pitch, quality);
            string key2 = cache.GenerateKey(text, voice, speed, pitch, quality);
            
            // Assert
            Assert.AreEqual(key1, key2);
        }
        
        [Test]
        public void GenerateKey_DifferentText_ReturnsDifferentKey()
        {
            // Arrange
            var cache = AudioCacheManager.Instance;
            
            // Act
            string key1 = cache.GenerateKey("Hello", "멀더", 1.0f, 1.0f, 8);
            string key2 = cache.GenerateKey("World", "멀더", 1.0f, 1.0f, 8);
            
            // Assert
            Assert.AreNotEqual(key1, key2);
        }
        
        [Test]
        public void GenerateKey_DifferentVoice_ReturnsDifferentKey()
        {
            // Arrange
            var cache = AudioCacheManager.Instance;
            
            // Act
            string key1 = cache.GenerateKey("Test", "멀더", 1.0f, 1.0f, 8);
            string key2 = cache.GenerateKey("Test", "수혜", 1.0f, 1.0f, 8);
            
            // Assert
            Assert.AreNotEqual(key1, key2);
        }
        
        [Test]
        public void GenerateKey_KeyLength_Is32Characters()
        {
            // Arrange
            var cache = AudioCacheManager.Instance;
            
            // Act
            string key = cache.GenerateKey("Test", "Voice", 1.0f, 1.0f, 8);
            
            // Assert
            Assert.AreEqual(32, key.Length);
        }
        
        [Test]
        public void GetStatistics_ReturnsValidStats()
        {
            // Arrange
            var cache = AudioCacheManager.Instance;
            
            // Act
            var stats = cache.GetStatistics();
            
            // Assert
            Assert.GreaterOrEqual(stats.MemoryEntries, 0);
            Assert.GreaterOrEqual(stats.MaxMemoryBytes, 0);
            Assert.IsFalse(string.IsNullOrEmpty(stats.DiskCachePath));
        }
    }
    
    /// <summary>
    /// Unit tests for VoiceFilter
    /// </summary>
    [TestFixture]
    public class VoiceFilterTests
    {
        [Test]
        public void Matches_EmptyFilter_ReturnsTrue()
        {
            // Arrange
            var filter = new VoiceFilter();
            var voice = CreateTestVoice("멀더", "남성, 청년, 저음, 맑음, 냉정한");
            
            // Act
            bool result = filter.Matches(voice);
            
            // Assert
            Assert.IsTrue(result);
        }
        
        [Test]
        public void Matches_GenderFilter_FiltersCorrectly()
        {
            // Arrange
            var filter = new VoiceFilter { Gender = Gender.Male };
            var maleVoice = CreateTestVoice("멀더", "남성, 청년, 저음, 맑음, 냉정한");
            var femaleVoice = CreateTestVoice("수혜", "여성, 청년, 중음, 맑음, 친절한");
            
            maleVoice.ParseDescription();
            femaleVoice.ParseDescription();
            
            // Act & Assert
            Assert.IsTrue(filter.Matches(maleVoice));
            Assert.IsFalse(filter.Matches(femaleVoice));
        }
        
        [Test]
        public void Matches_SearchText_MatchesName()
        {
            // Arrange
            var filter = new VoiceFilter { SearchText = "멀더" };
            var voice = CreateTestVoice("멀더", "남성, 청년, 저음, 맑음, 냉정한");
            
            // Act
            bool result = filter.Matches(voice);
            
            // Assert
            Assert.IsTrue(result);
        }
        
        [Test]
        public void Matches_SearchText_NoMatch()
        {
            // Arrange
            var filter = new VoiceFilter { SearchText = "존재하지않음" };
            var voice = CreateTestVoice("멀더", "남성, 청년, 저음, 맑음, 냉정한");
            
            // Act
            bool result = filter.Matches(voice);
            
            // Assert
            Assert.IsFalse(result);
        }
        
        [Test]
        public void Matches_AgeGroupFilter_FiltersCorrectly()
        {
            // Arrange
            var filter = new VoiceFilter { AgeGroup = AgeGroup.Young };
            var youngVoice = CreateTestVoice("젊은이", "남성, 청년, 저음, 맑음, 활발한");
            var seniorVoice = CreateTestVoice("어르신", "남성, 노년, 저음, 거침, 점잖은");
            
            youngVoice.ParseDescription();
            seniorVoice.ParseDescription();
            
            // Act & Assert
            Assert.IsTrue(filter.Matches(youngVoice));
            Assert.IsFalse(filter.Matches(seniorVoice));
        }
        
        private VarcoVoice CreateTestVoice(string name, string description)
        {
            return new VarcoVoice
            {
                SpeakerUuid = Guid.NewGuid().ToString(),
                SpeakerName = name,
                SaasName = name,
                Description = description
            };
        }
    }
    
    /// <summary>
    /// Unit tests for VarcoVoice model
    /// </summary>
    [TestFixture]
    public class VarcoVoiceTests
    {
        [Test]
        public void ParseDescription_ValidDescription_SetsProperties()
        {
            // Arrange
            var voice = new VarcoVoice
            {
                SpeakerName = "멀더",
                Description = "남성, 청년, 저음, 맑음, 냉정한"
            };
            
            // Act
            voice.ParseDescription();
            
            // Assert
            Assert.AreEqual(Gender.Male, voice.Gender);
            Assert.AreEqual(AgeGroup.Young, voice.AgeGroup);
            Assert.AreEqual(ToneType.Clear, voice.Tone);
            Assert.AreEqual("냉정한", voice.Personality);
        }
        
        [Test]
        public void GetEmotion_NeutralVoice_ReturnsNeutral()
        {
            // Arrange
            var voice = new VarcoVoice { SpeakerName = "멀더(중립)" };
            
            // Act
            var emotion = voice.GetEmotion();
            
            // Assert
            Assert.AreEqual(EmotionType.Neutral, emotion);
        }
        
        [Test]
        public void GetEmotion_HappyVoice_ReturnsHappy()
        {
            // Arrange
            var voice = new VarcoVoice { SpeakerName = "수혜(행복)" };
            
            // Act
            var emotion = voice.GetEmotion();
            
            // Assert
            Assert.AreEqual(EmotionType.Happy, emotion);
        }
        
        [Test]
        public void GetEmotion_AngryVoice_ReturnsAngry()
        {
            // Arrange
            var voice = new VarcoVoice { SpeakerName = "빌런(분노)" };
            
            // Act
            var emotion = voice.GetEmotion();
            
            // Assert
            Assert.AreEqual(EmotionType.Angry, emotion);
        }
        
        [Test]
        public void GetBaseName_WithEmotion_ReturnsNameOnly()
        {
            // Arrange
            var voice = new VarcoVoice { SpeakerName = "멀더(중립)" };
            
            // Act
            string baseName = voice.GetBaseName();
            
            // Assert
            Assert.AreEqual("멀더", baseName);
        }
        
        [Test]
        public void GetBaseName_WithoutEmotion_ReturnsFullName()
        {
            // Arrange
            var voice = new VarcoVoice { SpeakerName = "멀더" };
            
            // Act
            string baseName = voice.GetBaseName();
            
            // Assert
            Assert.AreEqual("멀더", baseName);
        }
    }
    
    /// <summary>
    /// Unit tests for exception types
    /// </summary>
    [TestFixture]
    public class VarcoExceptionTests
    {
        [Test]
        public void VarcoAuthException_HasCorrectStatusCode()
        {
            // Arrange & Act
            var ex = new VarcoAuthException();
            
            // Assert
            Assert.AreEqual(401, ex.StatusCode);
        }
        
        [Test]
        public void VarcoRateLimitException_HasRetryAfter()
        {
            // Arrange & Act
            var ex = new VarcoRateLimitException(30);
            
            // Assert
            Assert.AreEqual(429, ex.StatusCode);
            Assert.AreEqual(30, ex.RetryAfterSeconds);
        }
        
        [Test]
        public void VarcoTextTooLongException_ContainsActualBytes()
        {
            // Arrange & Act
            var ex = new VarcoTextTooLongException(1500);
            
            // Assert
            Assert.AreEqual(400, ex.StatusCode);
            Assert.AreEqual(1500, ex.ActualBytes);
            Assert.AreEqual(1200, ex.MaxBytes);
        }
        
        [Test]
        public void VarcoVoiceNotFoundException_ContainsVoiceName()
        {
            // Arrange & Act
            var ex = new VarcoVoiceNotFoundException("UnknownVoice");
            
            // Assert
            Assert.AreEqual("UnknownVoice", ex.VoiceName);
            Assert.IsTrue(ex.Message.Contains("UnknownVoice"));
        }
    }
}
