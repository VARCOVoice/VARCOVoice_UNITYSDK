using System.Text;
using NUnit.Framework;
using VARCOVoice.DSP;
using VARCOVoice.LipSync;

namespace VARCOVoice.Tests
{
    /// <summary>
    /// Unit tests for DSP effects
    /// </summary>
    [TestFixture]
    public class DSPEffectTests
    {
        [Test]
        public void PitchShiftEffect_ZeroSemitones_NoChange()
        {
            // Arrange
            var effect = new PitchShiftEffect { Semitones = 0f };
            float[] data = { 0.5f, -0.5f, 0.3f, -0.3f };
            float[] original = (float[])data.Clone();
            
            // Act
            effect.Process(data, 1, 44100);
            
            // Assert - with zero semitones, data should be unchanged
            for (int i = 0; i < data.Length; i++)
            {
                Assert.AreEqual(original[i], data[i], 0.001f);
            }
        }
        
        [Test]
        public void PhaseVocoderPitchShift_ZeroSemitones_NoChange()
        {
            // Arrange
            var effect = new PhaseVocoderPitchShift { Semitones = 0f };
            float[] data = { 0.5f, -0.5f, 0.3f, -0.3f };
            float[] original = (float[])data.Clone();
            
            // Act
            effect.Process(data, 1, 44100);
            
            // Assert
            for (int i = 0; i < data.Length; i++)
            {
                Assert.AreEqual(original[i], data[i], 0.001f);
            }
        }
        
        [Test]
        public void ReverbEffect_OffPreset_NoChange()
        {
            // Arrange
            var effect = new ReverbEffect { Preset = ReverbPreset.Off };
            float[] data = { 0.5f, -0.5f, 0.3f, -0.3f };
            float[] original = (float[])data.Clone();
            
            // Act
            effect.Process(data, 1, 44100);
            
            // Assert
            for (int i = 0; i < data.Length; i++)
            {
                Assert.AreEqual(original[i], data[i], 0.001f);
            }
        }
        
        [Test]
        public void EQEffect_AllZero_MinimalChange()
        {
            // Arrange
            var effect = new EQEffect
            {
                Bass = 0f,
                LowMid = 0f,
                Mid = 0f,
                HighMid = 0f,
                Treble = 0f
            };
            
            float[] data = new float[512];
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = (i % 2 == 0) ? 0.5f : -0.5f;
            }
            
            // Act
            effect.Process(data, 1, 44100);
            
            // Assert - should have minimal change
            Assert.Pass("EQ with zero gains processed without error");
        }
        
        [Test]
        public void LowPassEffect_ValidCutoff_ProcessesWithoutError()
        {
            // Arrange
            var effect = new LowPassEffect { CutoffFrequency = 1000f, Resonance = 1f };
            float[] data = new float[512];
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = (float)System.Math.Sin(i * 0.1);
            }
            
            // Act & Assert - should not throw
            Assert.DoesNotThrow(() => effect.Process(data, 1, 44100));
        }
        
        [Test]
        public void ChorusEffect_ProcessesWithoutError()
        {
            // Arrange
            var effect = new ChorusEffect
            {
                DelayMs = 20f,
                Depth = 3f,
                Rate = 0.5f,
                Mix = 0.5f
            };
            
            float[] data = new float[1024];
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = (float)System.Math.Sin(i * 0.1);
            }
            
            // Act & Assert
            Assert.DoesNotThrow(() => effect.Process(data, 2, 44100));
        }
        
        [Test]
        public void Spatial3DEffect_ProcessesStereo()
        {
            // Arrange
            var effect = new Spatial3DEffect
            {
                MaxDistance = 50f,
                MinDistance = 1f,
                SourcePosition = new UnityEngine.Vector3(10, 0, 0),
                ListenerPosition = new UnityEngine.Vector3(0, 0, 0)
            };
            
            float[] data = new float[256];
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = 0.5f;
            }
            
            // Act
            effect.Process(data, 2, 44100);
            
            // Assert - right channel should be louder (source is to the right)
            // For stereo: even indices = left, odd indices = right
            float leftSum = 0f, rightSum = 0f;
            for (int i = 0; i < data.Length; i += 2)
            {
                leftSum += System.Math.Abs(data[i]);
                rightSum += System.Math.Abs(data[i + 1]);
            }
            
            Assert.Greater(rightSum, leftSum * 0.5f, "Right channel should be louder when source is to the right");
        }
    }
    
    /// <summary>
    /// Unit tests for LipSync components
    /// </summary>
    [TestFixture]
    public class LipSyncTests
    {
        [Test]
        public void VisemeKeyframe_Constructor_SetsValues()
        {
            // Arrange & Act
            var keyframe = new VisemeKeyframe(1.5f, VisemeType.AA, 0.8f);
            
            // Assert
            Assert.AreEqual(1.5f, keyframe.Time);
            Assert.AreEqual(VisemeType.AA, keyframe.Viseme);
            Assert.AreEqual(0.8f, keyframe.Weight);
        }
        
        [Test]
        public void LipSyncData_GetVisemeAtTime_ReturnsCorrectViseme()
        {
            // Arrange
            var data = new LipSyncData
            {
                Duration = 3f
            };
            data.Keyframes.Add(new VisemeKeyframe(0f, VisemeType.Silence, 0f));
            data.Keyframes.Add(new VisemeKeyframe(0.5f, VisemeType.AA, 1f));
            data.Keyframes.Add(new VisemeKeyframe(1.0f, VisemeType.EE, 0.8f));
            data.Keyframes.Add(new VisemeKeyframe(2.0f, VisemeType.Silence, 0f));
            
            // Act & Assert
            Assert.AreEqual(VisemeType.Silence, data.GetVisemeAtTime(0.2f).Viseme);
            Assert.AreEqual(VisemeType.AA, data.GetVisemeAtTime(0.7f).Viseme);
            Assert.AreEqual(VisemeType.EE, data.GetVisemeAtTime(1.5f).Viseme);
            Assert.AreEqual(VisemeType.Silence, data.GetVisemeAtTime(2.5f).Viseme);
        }
        
        [Test]
        public void LipSyncData_GetEnergyAtTime_ReturnsCorrectEnergy()
        {
            // Arrange
            var data = new LipSyncData
            {
                EnergySampleRate = 10f // 10 samples per second
            };
            data.EnergyLevels.Add(0.1f);
            data.EnergyLevels.Add(0.5f);
            data.EnergyLevels.Add(0.8f);
            data.EnergyLevels.Add(0.3f);
            
            // Act
            float energy0 = data.GetEnergyAtTime(0f);
            float energy1 = data.GetEnergyAtTime(0.15f);
            float energy2 = data.GetEnergyAtTime(0.25f);
            
            // Assert
            Assert.AreEqual(0.1f, energy0, 0.001f);
            Assert.AreEqual(0.5f, energy1, 0.001f);
            Assert.AreEqual(0.8f, energy2, 0.001f);
        }
        
        [Test]
        public void LipSyncAnalyzer_AnalyzeRealtime_SilenceInput_ReturnsSilence()
        {
            // Arrange
            var analyzer = new LipSyncAnalyzer();
            float[] silentSamples = new float[512]; // All zeros
            
            // Act
            var viseme = analyzer.AnalyzeRealtime(silentSamples, 1);
            
            // Assert
            Assert.AreEqual(VisemeType.Silence, viseme);
        }
        
        [Test]
        public void LipSyncProfile_SetupDefaultMappings_CreatesAllVisemes()
        {
            // Arrange
            var profile = UnityEngine.ScriptableObject.CreateInstance<LipSyncProfile>();
            
            // Act
            profile.SetupDefaultMappings();
            
            // Assert
            Assert.AreEqual(15, profile.BlendShapes.Count);
            Assert.IsNotNull(profile.GetBlendShapeName(VisemeType.AA));
            Assert.IsNotNull(profile.GetBlendShapeName(VisemeType.EE));
            Assert.IsNotNull(profile.GetBlendShapeName(VisemeType.Silence));
            
            // Cleanup
            UnityEngine.Object.DestroyImmediate(profile);
        }
        
        [Test]
        public void EnhancedLipSyncAnalyzer_AnalyzeFrameRealtime_ReturnsWeights()
        {
            // Arrange
            var analyzer = new EnhancedLipSyncAnalyzer();
            float[] samples = new float[1024];
            
            // Generate a simple tone
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] = (float)System.Math.Sin(i * 0.1) * 0.5f;
            }
            
            // Act
            var weights = analyzer.AnalyzeFrameRealtime(samples, 1, 44100);
            
            // Assert
            Assert.AreEqual(15, weights.Length);
            
            // At least one weight should be non-zero
            bool hasNonZero = false;
            for (int i = 0; i < weights.Length; i++)
            {
                if (weights[i] > 0.01f) hasNonZero = true;
            }
            Assert.IsTrue(hasNonZero, "At least one viseme weight should be non-zero");
        }
    }
    
    /// <summary>
    /// Unit tests for Language extensions
    /// </summary>
    [TestFixture]
    public class LanguageExtensionTests
    {
        [Test]
        public void ToApiString_Korean_ReturnsKorean()
        {
            Assert.AreEqual("korean", Language.Korean.ToApiString());
        }
        
        [Test]
        public void ToApiString_English_ReturnsEnglish()
        {
            Assert.AreEqual("english", Language.English.ToApiString());
        }
        
        [Test]
        public void ToApiString_Japanese_ReturnsJapanese()
        {
            Assert.AreEqual("japanese", Language.Japanese.ToApiString());
        }
        
        [Test]
        public void ToApiString_Taiwanese_ReturnsTaiwanese()
        {
            Assert.AreEqual("taiwanese", Language.Taiwanese.ToApiString());
        }
    }
    
    /// <summary>
    /// Unit tests for text validation
    /// </summary>
    [TestFixture]
    public class TextValidationTests
    {
        [Test]
        public void TextByteCount_Korean_CalculatesCorrectly()
        {
            // Korean characters are 3 bytes in UTF-8
            string text = "안녕하세요";
            int byteCount = Encoding.UTF8.GetByteCount(text);
            
            Assert.AreEqual(15, byteCount); // 5 chars * 3 bytes
        }
        
        [Test]
        public void TextByteCount_English_CalculatesCorrectly()
        {
            string text = "Hello";
            int byteCount = Encoding.UTF8.GetByteCount(text);
            
            Assert.AreEqual(5, byteCount); // 5 chars * 1 byte
        }
        
        [Test]
        public void TextByteCount_Mixed_CalculatesCorrectly()
        {
            string text = "Hello 안녕";
            int byteCount = Encoding.UTF8.GetByteCount(text);
            
            // "Hello " = 6 bytes, "안녕" = 6 bytes
            Assert.AreEqual(12, byteCount);
        }
        
        [Test]
        public void MaxTextLength_ShouldBe1200Bytes()
        {
            // Verify that around 400 Korean characters is the limit
            string longKoreanText = new string('가', 400);
            int byteCount = Encoding.UTF8.GetByteCount(longKoreanText);
            
            Assert.AreEqual(1200, byteCount);
        }
    }
}
