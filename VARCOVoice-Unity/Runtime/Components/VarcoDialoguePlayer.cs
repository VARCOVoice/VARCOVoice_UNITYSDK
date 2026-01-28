using System;
using System.Collections.Generic;
using UnityEngine;
using VARCOVoice.LipSync;

namespace VARCOVoice
{
    [RequireComponent(typeof(AudioSource))]
    [AddComponentMenu("VARCO/Varco Player")]
    public class VarcoDialoguePlayer : MonoBehaviour
    {
        /// <summary>
        /// Maps a viseme to a specific blend shape by name or index.
        /// </summary>
        [Serializable]
        public class VisemeMapping
        {
            [Tooltip("Name of the blend shape in the mesh")]
            public string blendShapeName = "";
            
            [HideInInspector]
            public int cachedIndex = -1;
        }
        
        [Serializable]
        public class DialogueSlot
        {
            public string id = "";
            public AudioClip clip;
            
            [Tooltip("Auto-generated from AudioClip. Click 'Generate Viseme' in Export Panel if empty.")]
            public LipSyncData visemeData;

            public enum TriggerType { Manual, OnAwake, OnTrigger }
            public TriggerType triggerType = TriggerType.Manual;
            public float triggerRadius = 3f;

            public SkinnedMeshRenderer lipsyncTarget;
            public bool enableLipsync = true;
            
            [Range(1f, 15f)]
            [Tooltip("Multiplier for lip movement intensity. Increase if mouth doesn't move enough.")]
            public float lipsyncIntensity = 3f;
            
            [Header("Manual Blend Shape Mapping")]
            [Tooltip("Enable to manually specify which blend shapes to use for each vowel")]
            public bool useManualMapping = false;
            
            [Tooltip("Blend shape for 'A' (아) sound - open mouth")]
            public VisemeMapping mappingA = new VisemeMapping();
            
            [Tooltip("Blend shape for 'I' (이) sound - wide mouth")]
            public VisemeMapping mappingI = new VisemeMapping();
            
            [Tooltip("Blend shape for 'U' (우) sound - rounded lips")]
            public VisemeMapping mappingU = new VisemeMapping();
            
            [Tooltip("Blend shape for 'E' (에) sound - slightly open")]
            public VisemeMapping mappingE = new VisemeMapping();
            
            [Tooltip("Blend shape for 'O' (오) sound - rounded open")]
            public VisemeMapping mappingO = new VisemeMapping();
        }

        public List<DialogueSlot> dialogueSlots = new List<DialogueSlot>();

        private AudioSource _audioSource;
        
        // LipSync playback state
        private DialogueSlot _currentSlot;
        private float[] _currentWeights;
        private float[] _targetWeights;
        private int[] _blendShapeIndices;
        private bool _isLipsyncPlaying;

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource != null)
            {
                _audioSource.playOnAwake = false;
            }

            _currentWeights = new float[15];
            _targetWeights = new float[15];

            PlayOnAwakeSlot();
        }

        private void LateUpdate()
        {
            if (_isLipsyncPlaying && _currentSlot != null)
                UpdateLipSync();
        }

        private void PlayOnAwakeSlot()
        {
            for (int i = 0; i < dialogueSlots.Count; i++)
            {
                var slot = dialogueSlots[i];
                if (slot == null) continue;
                if (slot.triggerType != DialogueSlot.TriggerType.OnAwake) continue;
                Play(slot);
                break;
            }
        }

        public void Play(string id)
        {
            if (string.IsNullOrEmpty(id)) return;

            var slot = dialogueSlots.Find(s => s != null && s.id == id);
            if (slot != null)
            {
                Play(slot);
            }
        }

        public void Play(int index)
        {
            if (index < 0 || index >= dialogueSlots.Count) return;
            var slot = dialogueSlots[index];
            if (slot != null)
            {
                Play(slot);
            }
        }

        public void Stop()
        {
            if (_audioSource == null) return;
            _audioSource.Stop();
            StopLipSync();
        }

        private void Play(DialogueSlot slot)
        {
            if (_audioSource == null || slot == null || slot.clip == null) return;

            _audioSource.clip = slot.clip;
            _audioSource.Play();

            if (slot.enableLipsync && slot.lipsyncTarget != null && slot.visemeData != null)
            {
                StartLipSync(slot);
            }
        }

        #region LipSync Playback

        private void StartLipSync(DialogueSlot slot)
        {
            _currentSlot = slot;
            _isLipsyncPlaying = true;
            
            CacheBlendShapeIndices(slot.lipsyncTarget);
            
            for (int i = 0; i < _currentWeights.Length; i++)
                _currentWeights[i] = 0f;
        }

        private void StopLipSync()
        {
            _isLipsyncPlaying = false;
            
            // Reset blend shapes to neutral
            if (_currentSlot?.lipsyncTarget != null && _blendShapeIndices != null)
            {
                for (int i = 0; i < _blendShapeIndices.Length; i++)
                {
                    if (_blendShapeIndices[i] >= 0)
                    {
                        _currentSlot.lipsyncTarget.SetBlendShapeWeight(_blendShapeIndices[i], 0f);
                    }
                }
            }
            
            _currentSlot = null;
        }

        private float _debugTimer = 0f;
        
        private void UpdateLipSync()
        {
            if (!_audioSource.isPlaying)
            {
                StopLipSync();
                return;
            }

            var slot = _currentSlot;
            if (slot?.visemeData == null || slot.lipsyncTarget == null) return;

            // Get target weights from viseme data
            float currentTime = _audioSource.time;
            slot.visemeData.GetVisemeWeightsAtTime(currentTime, _targetWeights);

            // Smooth towards target
            float smoothFactor = 1f - Mathf.Pow(0.15f, Time.deltaTime * 60f);
            
            float maxWeight = 0f;

            for (int i = 0; i < 15 && i < _blendShapeIndices.Length; i++)
            {
                _currentWeights[i] = Mathf.Lerp(_currentWeights[i], _targetWeights[i], smoothFactor);

                if (_blendShapeIndices[i] >= 0)
                {
                    // Apply intensity multiplier and clamp to valid range
                    float weight = Mathf.Clamp(_currentWeights[i] * 100f * slot.lipsyncIntensity, 0f, 100f);
                    slot.lipsyncTarget.SetBlendShapeWeight(_blendShapeIndices[i], weight);
                    if (weight > maxWeight) maxWeight = weight;
                }
            }
            
            // Debug log every second
            _debugTimer += Time.deltaTime;
            if (_debugTimer >= 1f)
            {
                _debugTimer = 0f;
#if VARCO_DEBUG

#endif
            }
        }

        private void CacheBlendShapeIndices(SkinnedMeshRenderer target)
        {
            _blendShapeIndices = new int[15];
            
            if (target == null || target.sharedMesh == null)
            {
                for (int i = 0; i < 15; i++) _blendShapeIndices[i] = -1;
#if VARCO_DEBUG
                Debug.LogWarning("[VARCO LipSync] Target mesh is null!");
#endif
                return;
            }

            var mesh = target.sharedMesh;
            
            // Initialize all indices to -1
            for (int i = 0; i < 15; i++) _blendShapeIndices[i] = -1;
            
            // Check for manual mapping first
            if (_currentSlot != null && _currentSlot.useManualMapping)
            {
                // Manual mapping: A(1), E(2), I(3), O(4), U(5) - matches VisemeType enum
                ApplyManualMapping(mesh, _currentSlot.mappingA, 1); // AA
                ApplyManualMapping(mesh, _currentSlot.mappingE, 2); // EE
                ApplyManualMapping(mesh, _currentSlot.mappingI, 3); // IH
                ApplyManualMapping(mesh, _currentSlot.mappingO, 4); // OH
                ApplyManualMapping(mesh, _currentSlot.mappingU, 5); // OO
                return;
            }
            
            // Fallback: Auto-detect common viseme blend shape names
            string[][] visemeNames = new[]
            {
                new[] { "viseme_sil", "Fcl_MTH_Close", "mouth_close" }, // Silence
                new[] { "viseme_aa", "Fcl_MTH_A", "mouth_a", "A" },    // AA
                new[] { "viseme_E", "Fcl_MTH_E", "mouth_e", "E" },     // EE
                new[] { "viseme_I", "Fcl_MTH_I", "mouth_i", "I" },     // IH
                new[] { "viseme_O", "Fcl_MTH_O", "mouth_o", "O" },     // OH
                new[] { "viseme_U", "Fcl_MTH_U", "mouth_u", "U" },     // OO
                new[] { "viseme_CH", "mouth_ch" },                      // CH
                new[] { "viseme_FF", "mouth_f" },                       // FF
                new[] { "viseme_TH", "mouth_th" },                      // TH
                new[] { "viseme_PP", "Fcl_MTH_Close", "mouth_close" }, // PP
                new[] { "viseme_kk", "mouth_k" },                       // KK
                new[] { "viseme_nn", "mouth_n" },                       // NN
                new[] { "viseme_RR", "mouth_r" },                       // RR
                new[] { "viseme_DD", "mouth_d" },                       // DD
                new[] { "viseme_SS", "mouth_s" },                       // SS
            };

            for (int i = 0; i < 15; i++)
            {
                foreach (var name in visemeNames[i])
                {
                    int idx = mesh.GetBlendShapeIndex(name);
                    if (idx >= 0)
                    {
                        _blendShapeIndices[i] = idx;
                        break;
                    }
                }
            }
        }
        
        private void ApplyManualMapping(Mesh mesh, VisemeMapping mapping, int visemeIndex)
        {
            if (mapping == null || string.IsNullOrEmpty(mapping.blendShapeName)) return;
            
            int idx = mesh.GetBlendShapeIndex(mapping.blendShapeName);
            if (idx >= 0)
            {
                _blendShapeIndices[visemeIndex] = idx;
                mapping.cachedIndex = idx;
            }
        }

        #endregion

        private void OnTriggerEnter(Collider other)
        {
            if (!enabled) return;

            for (int i = 0; i < dialogueSlots.Count; i++)
            {
                var slot = dialogueSlots[i];
                if (slot == null) continue;
                if (slot.triggerType != DialogueSlot.TriggerType.OnTrigger) continue;
                if (slot.clip == null) continue;

                if (slot.triggerRadius > 0f && other != null)
                {
                    var distance = Vector3.Distance(transform.position, other.transform.position);
                    if (distance > slot.triggerRadius) continue;
                }

                Play(slot);
                break;
            }
        }
    }
}
