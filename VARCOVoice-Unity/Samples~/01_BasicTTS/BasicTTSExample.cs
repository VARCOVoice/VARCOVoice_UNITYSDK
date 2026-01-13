using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;

namespace VARCOVoice.Samples
{
    /// <summary>
    /// Basic TTS example - demonstrates simple text-to-speech functionality
    /// </summary>
    public class BasicTTSExample : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private InputField textInput;
        [SerializeField] private Button speakButton;
        [SerializeField] private Button stopButton;
        [SerializeField] private Dropdown voiceDropdown;
        [SerializeField] private Slider speedSlider;
        [SerializeField] private Slider pitchSlider;
        [SerializeField] private Text statusText;
        
        [Header("Settings")]
        [SerializeField] private string[] popularVoices = new[]
        {
            "멀더",
            "노수혜(중립)",
            "윤형우(중립)",
            "이예원(중립)",
            "현서(중립)",
            "영훈(중립)"
        };
        
        private VarcoTTS _tts;
        
        private void Start()
        {
            _tts = VarcoTTS.Instance;
            
            // Setup UI
            SetupUI();
            
            // Setup events
            _tts.OnSynthesisComplete += OnSynthesisComplete;
            _tts.OnPlaybackComplete += OnPlaybackComplete;
            _tts.OnError += OnError;
        }
        
        private void OnDestroy()
        {
            if (_tts != null)
            {
                _tts.OnSynthesisComplete -= OnSynthesisComplete;
                _tts.OnPlaybackComplete -= OnPlaybackComplete;
                _tts.OnError -= OnError;
            }
        }
        
        private void SetupUI()
        {
            // Default text
            if (textInput != null)
            {
                textInput.text = "안녕하세요! 바르코 보이스 TTS 테스트입니다.";
            }
            
            // Populate voice dropdown
            if (voiceDropdown != null)
            {
                voiceDropdown.ClearOptions();
                voiceDropdown.AddOptions(new System.Collections.Generic.List<string>(popularVoices));
            }
            
            // Setup sliders
            if (speedSlider != null)
            {
                speedSlider.minValue = 0.5f;
                speedSlider.maxValue = 1.5f;
                speedSlider.value = 1.0f;
            }
            
            if (pitchSlider != null)
            {
                pitchSlider.minValue = 0.5f;
                pitchSlider.maxValue = 1.5f;
                pitchSlider.value = 1.0f;
            }
            
            // Setup buttons
            if (speakButton != null)
            {
                speakButton.onClick.AddListener(() => SpeakAsync().Forget());
            }
            
            if (stopButton != null)
            {
                stopButton.onClick.AddListener(Stop);
            }
            
            SetStatus("Ready");
        }
        
        private async UniTaskVoid SpeakAsync()
        {
            if (string.IsNullOrEmpty(textInput?.text))
            {
                SetStatus("Please enter some text!");
                return;
            }
            
            string voice = popularVoices[voiceDropdown?.value ?? 0];
            float speed = speedSlider?.value ?? 1f;
            float pitch = pitchSlider?.value ?? 1f;
            
            SetStatus($"Generating speech with voice: {voice}...");
            
            try
            {
                var clip = await _tts.SynthesizeAsync(
                    textInput.text,
                    voice,
                    speed: speed,
                    pitch: pitch
                );
                
                _tts.Play(clip);
            }
            catch (VarcoException ex)
            {
                SetStatus($"Error: {ex.Message}");
            }
        }
        
        private void Stop()
        {
            _tts.Stop();
            SetStatus("Stopped");
        }
        
        private void OnSynthesisComplete(AudioClip clip)
        {
            SetStatus($"Playing... ({clip.length:F1}s)");
        }
        
        private void OnPlaybackComplete()
        {
            SetStatus("Playback complete");
        }
        
        private void OnError(VarcoException ex)
        {
            SetStatus($"Error: {ex.Message}");
        }
        
        private void SetStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = message;
            }
            Debug.Log($"[BasicTTS] {message}");
        }
    }
}
