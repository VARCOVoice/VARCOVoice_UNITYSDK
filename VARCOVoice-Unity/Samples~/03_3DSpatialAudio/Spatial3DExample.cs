using UnityEngine;
using VARCOVoice.Audio;
using VARCOVoice.DSP;
using Cysharp.Threading.Tasks;

namespace VARCOVoice.Samples
{
    /// <summary>
    /// 3D Spatial Audio example - demonstrates positional TTS audio
    /// </summary>
    public class Spatial3DExample : MonoBehaviour
    {
        [Header("Audio Sources")]
        [SerializeField] private VarcoAudioSource[] speakers;
        
        [Header("Speaker Settings")]
        [SerializeField] private string[] speakerVoices = new[] { "멀더", "노수혜(중립)", "윤형우(중립)" };
        [SerializeField] private string[] speakerTexts = new[]
        {
            "안녕, 나는 왼쪽에 있어!",
            "저는 가운데에 있어요.",
            "나는 오른쪽에서 말하고 있어!"
        };
        
        [Header("Movement")]
        [SerializeField] private bool enableMovement = true;
        [SerializeField] private float moveSpeed = 2f;
        [SerializeField] private float moveRadius = 5f;
        
        private float[] _movePhases;
        
        private void Start()
        {
            // Auto-setup if no speakers assigned
            if (speakers == null || speakers.Length == 0)
            {
                SetupDefaultSpeakers();
            }
            
            _movePhases = new float[speakers.Length];
            for (int i = 0; i < speakers.Length; i++)
            {
                _movePhases[i] = i * Mathf.PI * 2f / speakers.Length;
            }
        }
        
        private void Update()
        {
            if (!enableMovement) return;
            
            // Move speakers in circles
            for (int i = 0; i < speakers.Length; i++)
            {
                if (speakers[i] == null) continue;
                
                _movePhases[i] += Time.deltaTime * moveSpeed;
                
                float x = Mathf.Cos(_movePhases[i]) * moveRadius;
                float z = Mathf.Sin(_movePhases[i]) * moveRadius;
                
                speakers[i].transform.position = new Vector3(x, 1f, z);
            }
        }
        
        private void SetupDefaultSpeakers()
        {
            speakers = new VarcoAudioSource[3];
            
            for (int i = 0; i < 3; i++)
            {
                var go = new GameObject($"Speaker_{i}");
                go.transform.parent = transform;
                go.transform.position = new Vector3((i - 1) * 5f, 1f, 0f);
                
                // Add visual indicator
                var indicator = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                indicator.transform.parent = go.transform;
                indicator.transform.localPosition = Vector3.zero;
                indicator.transform.localScale = Vector3.one * 0.5f;
                
                // Different colors for each speaker
                var renderer = indicator.GetComponent<Renderer>();
                renderer.material.color = i switch
                {
                    0 => Color.red,
                    1 => Color.green,
                    2 => Color.blue,
                    _ => Color.white
                };
                
                // Add VarcoAudioSource
                speakers[i] = go.AddComponent<VarcoAudioSource>();
                speakers[i].DefaultVoice = speakerVoices[i];
                
                // Enable 3D audio
                var audioSource = speakers[i].AudioSource;
                audioSource.spatialBlend = 1f;
                audioSource.maxDistance = 20f;
                audioSource.minDistance = 1f;
            }
        }
        
        /// <summary>
        /// Make all speakers speak simultaneously
        /// </summary>
        public void SpeakAll()
        {
            for (int i = 0; i < speakers.Length; i++)
            {
                if (speakers[i] != null && i < speakerTexts.Length)
                {
                    speakers[i].SpeakAsync(speakerTexts[i]).Forget();
                }
            }
        }
        
        /// <summary>
        /// Make speakers speak sequentially
        /// </summary>
        public async UniTaskVoid SpeakSequential()
        {
            for (int i = 0; i < speakers.Length; i++)
            {
                if (speakers[i] != null && i < speakerTexts.Length)
                {
                    await speakers[i].SpeakAsync(speakerTexts[i]);
                    await UniTask.Delay(500);
                }
            }
        }
        
        /// <summary>
        /// Apply effect preset to a speaker
        /// </summary>
        public void ApplyEffect(int speakerIndex, string effectName)
        {
            if (speakerIndex < 0 || speakerIndex >= speakers.Length) return;
            
            var speaker = speakers[speakerIndex];
            
            switch (effectName.ToLower())
            {
                case "robot":
                    speaker.ApplyRobotVoice();
                    break;
                case "cave":
                    speaker.ApplyCaveVoice();
                    break;
                case "underwater":
                    speaker.ApplyUnderwaterVoice();
                    break;
                case "radio":
                    speaker.ApplyRadioVoice();
                    break;
                case "ghost":
                    speaker.ApplyGhostVoice();
                    break;
                default:
                    speaker.ClearEffects();
                    break;
            }
        }
        
        // UI Button callbacks
        public void OnSpeakAllClicked() => SpeakAll();
        public void OnSpeakSequentialClicked() => SpeakSequential().Forget();
        public void OnApplyRobotEffect() => ApplyEffect(0, "robot");
        public void OnApplyCaveEffect() => ApplyEffect(1, "cave");
        public void OnApplyUnderwaterEffect() => ApplyEffect(2, "underwater");
    }
}
