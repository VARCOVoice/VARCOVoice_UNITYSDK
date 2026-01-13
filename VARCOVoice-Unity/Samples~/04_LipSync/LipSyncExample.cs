using UnityEngine;
using VARCOVoice.LipSync;
using Cysharp.Threading.Tasks;

namespace VARCOVoice.Samples
{
    /// <summary>
    /// Lip sync example - demonstrates character lip animation with TTS
    /// </summary>
    public class LipSyncExample : MonoBehaviour
    {
        [Header("Character")]
        [SerializeField] private SkinnedMeshRenderer characterFace;
        [SerializeField] private LipSyncProfile lipSyncProfile;
        
        [Header("Lip Sync Player")]
        [SerializeField] private LipSyncPlayer lipSyncPlayer;
        
        [Header("TTS Settings")]
        [SerializeField] private string defaultVoice = "멀더";
        
        [Header("Demo Text")]
        [SerializeField] private string[] demoTexts = new[]
        {
            "안녕하세요! 저는 립싱크 캐릭터입니다.",
            "바르코 보이스와 함께라면 자연스러운 입 움직임이 가능해요.",
            "아, 에, 이, 오, 우. 다양한 발음을 테스트해보세요."
        };
        
        private int _currentTextIndex = 0;
        
        private void Start()
        {
            SetupLipSync();
        }
        
        private void SetupLipSync()
        {
            // Auto-find components if not assigned
            if (characterFace == null)
            {
                characterFace = GetComponentInChildren<SkinnedMeshRenderer>();
            }
            
            if (lipSyncPlayer == null)
            {
                lipSyncPlayer = GetComponent<LipSyncPlayer>();
                if (lipSyncPlayer == null)
                {
                    lipSyncPlayer = gameObject.AddComponent<LipSyncPlayer>();
                }
            }
            
            // Setup player
            if (characterFace != null)
            {
                lipSyncPlayer.SetTarget(characterFace);
            }
            
            if (lipSyncProfile != null)
            {
                lipSyncPlayer.SetProfile(lipSyncProfile);
            }
        }
        
        /// <summary>
        /// Speak current demo text with lip sync
        /// </summary>
        public async UniTaskVoid SpeakWithLipSync()
        {
            if (lipSyncPlayer == null) return;
            
            string text = demoTexts[_currentTextIndex];
            
            try
            {
                // Generate audio
                var clip = await VarcoTTS.Instance.SynthesizeAsync(text, defaultVoice);
                
                // Play with lip sync
                lipSyncPlayer.PlayWithLipSync(clip);
            }
            catch (VarcoException ex)
            {
                Debug.LogError($"[LipSync] TTS Error: {ex.Message}");
            }
            
            // Move to next text
            _currentTextIndex = (_currentTextIndex + 1) % demoTexts.Length;
        }
        
        /// <summary>
        /// Stop current playback
        /// </summary>
        public void Stop()
        {
            lipSyncPlayer?.Stop();
        }
        
        /// <summary>
        /// Adjust lip sync intensity
        /// </summary>
        public void SetIntensity(float intensity)
        {
            if (lipSyncPlayer != null)
            {
                lipSyncPlayer.Intensity = intensity;
            }
        }
        
        /// <summary>
        /// Adjust lip sync smoothing
        /// </summary>
        public void SetSmoothing(float smoothing)
        {
            if (lipSyncPlayer != null)
            {
                lipSyncPlayer.Smoothing = smoothing;
            }
        }
        
        /// <summary>
        /// Change voice
        /// </summary>
        public void SetVoice(string voice)
        {
            defaultVoice = voice;
        }
        
        // UI Button callbacks
        public void OnSpeakClicked() => SpeakWithLipSync().Forget();
        public void OnStopClicked() => Stop();
    }
}
