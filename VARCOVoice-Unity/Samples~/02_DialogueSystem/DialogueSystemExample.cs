using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;

namespace VARCOVoice.Samples
{
    /// <summary>
    /// Game dialogue system example with TTS
    /// </summary>
    public class DialogueSystemExample : MonoBehaviour
    {
        [Header("Dialogue Data")]
        [SerializeField] private List<DialogueLine> dialogueLines = new List<DialogueLine>();
        
        [Header("Components")]
        [SerializeField] private Audio.VarcoAudioSource varcoAudio;
        [SerializeField] private UnityEngine.UI.Text speakerNameText;
        [SerializeField] private UnityEngine.UI.Text dialogueText;
        [SerializeField] private UnityEngine.UI.Button nextButton;
        [SerializeField] private UnityEngine.UI.Button skipButton;
        
        [Header("Typewriter Effect")]
        [SerializeField] private bool useTypewriter = true;
        [SerializeField] private float typewriterSpeed = 0.05f;
        
        private int _currentLine = 0;
        private bool _isPlaying = false;
        
        [System.Serializable]
        public class DialogueLine
        {
            public string speakerName;
            public string voiceName;
            public string text;
            
            [Range(0.5f, 1.5f)]
            public float speed = 1.0f;
        }
        
        private void Start()
        {
            // Default dialogue for demo
            if (dialogueLines.Count == 0)
            {
                dialogueLines.Add(new DialogueLine
                {
                    speakerName = "멀더",
                    voiceName = "멀더",
                    text = "안녕, 나는 멀더야. 환영해!"
                });
                
                dialogueLines.Add(new DialogueLine
                {
                    speakerName = "수혜",
                    voiceName = "노수혜(중립)",
                    text = "반가워요. 저는 수혜입니다."
                });
                
                dialogueLines.Add(new DialogueLine
                {
                    speakerName = "멀더",
                    voiceName = "멀더",
                    text = "바르코 보이스로 만든 대화 시스템이야. 멋지지?"
                });
            }
            
            // Setup audio component
            if (varcoAudio == null)
            {
                varcoAudio = gameObject.AddComponent<Audio.VarcoAudioSource>();
            }
            
            // Setup buttons
            if (nextButton != null)
            {
                nextButton.onClick.AddListener(() => NextLine().Forget());
            }
            
            if (skipButton != null)
            {
                skipButton.onClick.AddListener(Skip);
            }
            
            // Start dialogue
            ShowCurrentLine().Forget();
        }
        
        public async UniTaskVoid NextLine()
        {
            if (_isPlaying)
            {
                Skip();
                return;
            }
            
            _currentLine++;
            
            if (_currentLine >= dialogueLines.Count)
            {
                // Dialogue complete
                _currentLine = 0;
                if (speakerNameText != null) speakerNameText.text = "";
                if (dialogueText != null) dialogueText.text = "대화가 끝났습니다. 다시 시작하려면 Next를 누르세요.";
                return;
            }
            
            await ShowCurrentLine();
        }
        
        private async UniTask ShowCurrentLine()
        {
            if (_currentLine >= dialogueLines.Count) return;
            
            var line = dialogueLines[_currentLine];
            _isPlaying = true;
            
            // Update speaker name
            if (speakerNameText != null)
            {
                speakerNameText.text = line.speakerName;
            }
            
            // Show text (with optional typewriter effect)
            if (useTypewriter)
            {
                TypewriterEffect(line.text).Forget();
            }
            else if (dialogueText != null)
            {
                dialogueText.text = line.text;
            }
            
            // Speak
            varcoAudio.DefaultVoice = line.voiceName;
            varcoAudio.Speed = line.speed;
            
            try
            {
                await varcoAudio.SpeakAsync(line.text);
            }
            catch (VarcoException ex)
            {
                Debug.LogError($"[Dialogue] TTS Error: {ex.Message}");
            }
            
            _isPlaying = false;
        }
        
        private async UniTaskVoid TypewriterEffect(string text)
        {
            if (dialogueText == null) return;
            
            dialogueText.text = "";
            
            foreach (char c in text)
            {
                dialogueText.text += c;
                await UniTask.Delay((int)(typewriterSpeed * 1000));
                
                if (!_isPlaying) break;
            }
            
            dialogueText.text = text;
        }
        
        private void Skip()
        {
            varcoAudio.Stop();
            _isPlaying = false;
            
            if (_currentLine < dialogueLines.Count && dialogueText != null)
            {
                dialogueText.text = dialogueLines[_currentLine].text;
            }
        }
    }
}
