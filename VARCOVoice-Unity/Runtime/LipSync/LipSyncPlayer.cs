using System;
using UnityEngine;

namespace VARCOVoice.LipSync
{
    /// <summary>
    /// Plays lip sync data on a character
    /// </summary>
    [AddComponentMenu("VARCO Voice/Lip Sync Player")]
    public class LipSyncPlayer : MonoBehaviour
    {
        #region Inspector Fields
        
        [Header("Target")]
        [SerializeField] private SkinnedMeshRenderer targetRenderer;
        [SerializeField] private LipSyncProfile profile;
        
        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        
        [Header("Settings")]
        [SerializeField] private bool useRealtimeAnalysis = true;
        
        [Range(0f, 1f)]
        [SerializeField] private float smoothing = 0.15f;
        
        [Range(0f, 2f)]
        [SerializeField] private float intensity = 1f;
        
        [Header("Debug")]
        [SerializeField] private bool showDebug = false;
        
        #endregion
        
        #region Private Fields
        
        private LipSyncData _lipSyncData;
        private LipSyncAnalyzer _analyzer;
        private float[] _currentWeights;
        private float[] _targetWeights;
        private float[] _realtimeBuffer;
        private int[] _blendShapeIndices;
        private bool _isPlaying;
        private float _playStartTime;
        
        #endregion
        
        #region Properties
        
        public bool IsPlaying => _isPlaying;
        
        public float Smoothing
        {
            get => smoothing;
            set => smoothing = Mathf.Clamp01(value);
        }
        
        public float Intensity
        {
            get => intensity;
            set => intensity = Mathf.Clamp(value, 0f, 2f);
        }
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            _analyzer = new LipSyncAnalyzer();
            _currentWeights = new float[15];
            _targetWeights = new float[15];
            
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }
            
            CacheBlendShapeIndices();
        }
        
        private void Update()
        {
            if (!_isPlaying && (!audioSource || !audioSource.isPlaying))
            {
                ResetVisemes();
                return;
            }
            
            UpdateLipSync();
        }
        
        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (!useRealtimeAnalysis || !_isPlaying) return;
            
            // Store buffer for analysis (careful: this is on audio thread)
            if (_realtimeBuffer == null || _realtimeBuffer.Length != data.Length)
            {
                _realtimeBuffer = new float[data.Length];
            }
            
            Array.Copy(data, _realtimeBuffer, data.Length);
        }
        
        #endregion
        
        #region Public API
        
        /// <summary>
        /// Set lip sync data for pre-analyzed audio
        /// </summary>
        public void SetLipSyncData(LipSyncData data)
        {
            _lipSyncData = data;
        }
        
        /// <summary>
        /// Analyze audio clip and play with lip sync
        /// </summary>
        public void PlayWithLipSync(AudioClip clip)
        {
            if (clip == null) return;
            
            // Analyze clip
            _lipSyncData = _analyzer.Analyze(clip);
            
            // Play audio
            audioSource.clip = clip;
            audioSource.Play();
            
            _isPlaying = true;
            _playStartTime = Time.time;
        }
        
        /// <summary>
        /// Start playing with current audio source clip
        /// </summary>
        public void Play()
        {
            if (audioSource.clip != null)
            {
                PlayWithLipSync(audioSource.clip);
            }
        }
        
        /// <summary>
        /// Stop lip sync playback
        /// </summary>
        public void Stop()
        {
            _isPlaying = false;
            audioSource?.Stop();
            ResetVisemes();
        }
        
        /// <summary>
        /// Set target renderer
        /// </summary>
        public void SetTarget(SkinnedMeshRenderer renderer)
        {
            targetRenderer = renderer;
            CacheBlendShapeIndices();
        }
        
        /// <summary>
        /// Set lip sync profile
        /// </summary>
        public void SetProfile(LipSyncProfile newProfile)
        {
            profile = newProfile;
            CacheBlendShapeIndices();
        }
        
        #endregion
        
        #region Private Methods
        
        private void CacheBlendShapeIndices()
        {
            if (targetRenderer == null || profile == null)
            {
                _blendShapeIndices = null;
                return;
            }
            
            var mesh = targetRenderer.sharedMesh;
            if (mesh == null) return;
            
            _blendShapeIndices = new int[15];
            
            for (int i = 0; i < 15; i++)
            {
                var viseme = (VisemeType)i;
                string blendShapeName = profile.GetBlendShapeName(viseme);
                
                if (string.IsNullOrEmpty(blendShapeName))
                {
                    _blendShapeIndices[i] = -1;
                    continue;
                }
                
                _blendShapeIndices[i] = mesh.GetBlendShapeIndex(blendShapeName);
            }
        }
        
        private void UpdateLipSync()
        {
            if (targetRenderer == null || _blendShapeIndices == null) return;
            
            // Get target weights
            if (useRealtimeAnalysis && _realtimeBuffer != null)
            {
                // Real-time analysis from audio buffer
                _targetWeights = _analyzer.AnalyzeRealtimeWeights(_realtimeBuffer, 2);
            }
            else if (_lipSyncData != null && audioSource != null)
            {
                // Pre-analyzed data
                float currentTime = audioSource.time;
                _targetWeights = _lipSyncData.GetVisemeWeightsAtTime(currentTime);
            }
            else
            {
                return;
            }
            
            // Smooth towards target
            float smoothFactor = 1f - Mathf.Pow(smoothing, Time.deltaTime * 60f);
            
            for (int i = 0; i < 15; i++)
            {
                _currentWeights[i] = Mathf.Lerp(_currentWeights[i], _targetWeights[i], smoothFactor);
                
                // Apply to blend shape if mapped
                if (_blendShapeIndices[i] >= 0)
                {
                    float weight = _currentWeights[i] * intensity * 100f;
                    targetRenderer.SetBlendShapeWeight(_blendShapeIndices[i], weight);
                }
            }
            
            // Check if playback is complete
            if (_lipSyncData != null && audioSource.time >= _lipSyncData.Duration)
            {
                _isPlaying = false;
            }
        }
        
        private void ResetVisemes()
        {
            if (targetRenderer == null || _blendShapeIndices == null) return;
            
            for (int i = 0; i < 15; i++)
            {
                _currentWeights[i] = Mathf.Lerp(_currentWeights[i], 0f, Time.deltaTime * 5f);
                
                if (_blendShapeIndices[i] >= 0)
                {
                    targetRenderer.SetBlendShapeWeight(_blendShapeIndices[i], _currentWeights[i] * 100f);
                }
            }
        }
        
        #endregion
        
        #region Debug
        
        private void OnGUI()
        {
            if (!showDebug) return;
            
            GUILayout.BeginArea(new Rect(10, 10, 250, 400));
            GUILayout.Label("Lip Sync Debug");
            GUILayout.Label($"Playing: {_isPlaying}");
            
            for (int i = 0; i < 15; i++)
            {
                var viseme = (VisemeType)i;
                GUILayout.Label($"{viseme}: {_currentWeights[i]:F2}");
            }
            
            GUILayout.EndArea();
        }
        
        #endregion
    }
}
