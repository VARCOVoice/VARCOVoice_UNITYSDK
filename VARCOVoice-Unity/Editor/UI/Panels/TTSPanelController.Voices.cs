using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using VARCOVoice.Editor;
using Cysharp.Threading.Tasks;

namespace VARCOVoice.Editor
{
    public partial class TTSPanelController
    {
        #region Voice Loading
        
        private bool _isLoadingVoices = false;

        private async UniTaskVoid LoadVoicesAsync(bool forceRefresh = false)
        {
            // Cache check: If voices already loaded and not forced, skip
            if (!forceRefresh && _voices != null && _voices.Count > 0)
            {
                // Ensure UI is populated even if cached
                if (_voiceADropdown != null && _voiceADropdown.choices == null)
                {
                    // Re-populate if UI was rebuilt but data persisted
                }
                else
                {
                    return;
                }
            }

            if (_isLoadingVoices) return;
            _isLoadingVoices = true;

            try
            {
                var config = VarcoConfig.Instance;
                if (!config.IsValid())
                {
                    UpdateStatus("Configure API Key in Project Settings", StatusType.Warning);
                    return;
                }
                
                var client = new VarcoApiClient(config);
                _voices = await client.GetVoicesAsync();
                
                var voiceNames = new List<string>();
                foreach (var voice in _voices)
                {
                    voiceNames.Add(voice.SpeakerName);
                }
                
                if (_voiceADropdown != null)
                {
                    _voiceADropdown.choices = voiceNames;
                    if (voiceNames.Count > 0 && string.IsNullOrEmpty(_voiceADropdown.value))
                        _voiceADropdown.value = voiceNames[0];
                }
                
                if (_voiceBDropdown != null)
                {
                    _voiceBDropdown.choices = voiceNames;
                    if (voiceNames.Count > 1 && string.IsNullOrEmpty(_voiceBDropdown.value))
                        _voiceBDropdown.value = voiceNames[1];
                }
                
                // Populate integrated voice list
                PopulateIntegratedVoiceList();
                
                // Set default selections only if not set
                if (string.IsNullOrEmpty(_selectedVoiceA) && voiceNames.Count > 0)
                {
                    _selectedVoiceA = voiceNames[0];
                }
                 UpdateSelectedVoiceDisplay(); // Always update display
                 
                if (string.IsNullOrEmpty(_selectedVoiceB) && voiceNames.Count > 1)
                {
                    _selectedVoiceB = voiceNames[1];
                }
                
                UpdateStatus($"Loaded {_voices.Count} voices", StatusType.Success);
            }
            catch (VarcoException ex)
            {
                UpdateStatus($"Failed to load voices: {ex.Message}", StatusType.Error);
            }
            finally
            {
                _isLoadingVoices = false;
            }
        }
        
        private void PopulateVoicePicker(ScrollView picker, bool isVoiceA)
        {
            if (picker == null) return;
            
            picker.Clear();
            
            foreach (var voice in _voices)
            {
                var item = new Button();
                item.AddToClassList("voice-picker-item");
                item.text = voice.SpeakerName;
                
                string voiceName = voice.SpeakerName;
                item.clicked += () => SelectVoice(voiceName, isVoiceA);
                
                picker.Add(item);
            }
        }
        
        // ListView Optimization
        private ListView _voiceList;
        private List<VarcoVoice> _filteredVoices;

        private void InitializeVoiceList()
        {
            _voiceList = _root.Q<ListView>("voice-list");
            if (_voiceList == null) return;

            // MakeItem: Create visual element structure
            _voiceList.makeItem = () =>
            {
                var item = new VisualElement();
                item.AddToClassList("voice-item");

                var info = new VisualElement();
                info.AddToClassList("voice-item__info");
                
                var nameLabel = new Label();
                nameLabel.AddToClassList("voice-item__name");
                info.Add(nameLabel);
                
                var tagsLabel = new Label();
                tagsLabel.AddToClassList("voice-item__tags");
                info.Add(tagsLabel);
                
                item.Add(info);
                
                var actions = new VisualElement();
                actions.AddToClassList("voice-item__actions");
                
                var btnA = new Button();
                btnA.text = "A";
                btnA.AddToClassList("voice-item__btn");
                btnA.AddToClassList("voice-item__btn--a");
                actions.Add(btnA);
                
                var btnB = new Button();
                btnB.text = "B";
                btnB.AddToClassList("voice-item__btn");
                btnB.AddToClassList("voice-item__btn--b");
                actions.Add(btnB);
                
                item.Add(actions);
                
                return item;
            };

            // BindItem: Bind data to visual elements
            _voiceList.bindItem = (element, index) =>
            {
                if (_filteredVoices == null || index >= _filteredVoices.Count) return;
                var voice = _filteredVoices[index];
                
                // Name
                var nameLabel = element.Q<Label>(className: "voice-item__name");
                nameLabel.text = voice.SpeakerName;
                
                // Tags
                var tagsLabel = element.Q<Label>(className: "voice-item__tags");
                if (tagsLabel != null) // Safety check if Q fails
                    tagsLabel.text = GetVoiceTags(voice);
                else
                {
                   // Try finding by walking since class might be missing in makeItem (typo check)
                   // In makeItem I wrote: nameLabel.AddToClassList("voice-item__tags"); wait.
                   // ERROR in MakeItem above: nameLabel.AddToClassList("voice-item__tags"); 
                   // I should fix the MakeItem in this file.
                }

                // Buttons
                var btnA = element.Q<Button>(className: "voice-item__btn--a");
                // Remove old callbacks to prevent stacking
                btnA.clickable = new Clickable(() => AssignVoice(voice.SpeakerName, true));

                var btnB = element.Q<Button>(className: "voice-item__btn--b");
                btnB.clickable = new Clickable(() => AssignVoice(voice.SpeakerName, false));
                
                // Selection State (Optional visual feedback)
                if (voice.SpeakerName == _selectedVoiceA) btnA.AddToClassList("selected");
                else btnA.RemoveFromClassList("selected");
                
                if (voice.SpeakerName == _selectedVoiceB) btnB.AddToClassList("selected");
                else btnB.RemoveFromClassList("selected");
            };
        }

        private void PopulateIntegratedVoiceList()
        {
            if (_voiceList == null) InitializeVoiceList();
            
            // Initial populate = all voices
            _filteredVoices = new List<VarcoVoice>(_voices);
            _voiceList.itemsSource = _filteredVoices;
            _voiceList.Rebuild();
            
            // Apply current filters immediately
            ApplyFilters();
        }
        
        private string GetVoiceTags(VarcoVoice voice)
        {
            var tags = new List<string>();
            
            if (voice.Gender != Gender.Unknown)
                tags.Add(voice.Gender == Gender.Male ? "Male" : "Female");
            
            if (voice.AgeGroup != AgeGroup.Unknown)
                tags.Add(voice.AgeGroup.ToString());
            
            var emotion = voice.GetEmotion();
            if (emotion != EmotionType.Neutral)
                tags.Add(emotion.ToString());
                
            return string.Join(" | ", tags);
        }
        
        private void AssignVoice(string voiceName, bool isSlotA)
        {
            if (isSlotA)
            {
                _selectedVoiceA = voiceName;
                if (_voiceADropdown != null)
                    _voiceADropdown.value = voiceName;
            }
            else
            {
                _selectedVoiceB = voiceName;
                if (_voiceBDropdown != null)
                    _voiceBDropdown.value = voiceName;
            }
            
            UpdateSelectedVoiceDisplay();
            UpdateStatus($"Voice {(isSlotA ? "A" : "B")}: {voiceName}", StatusType.Info);
            
            // Force ListView to refresh visual state (buttons)
            _voiceList?.Rebuild();
        }
        
        private void ClearVoice(bool isSlotA)
        {
            if (isSlotA)
            {
                _selectedVoiceA = null;
                if (_voiceADropdown != null)
                    _voiceADropdown.value = null;
            }
            else
            {
                _selectedVoiceB = null;
                if (_voiceBDropdown != null)
                    _voiceBDropdown.value = null;
            }
            
            UpdateSelectedVoiceDisplay();
            UpdateStatus($"Voice {(isSlotA ? "A" : "B")} cleared", StatusType.Info);
            
            // Force ListView to refresh visual state
            _voiceList?.Rebuild();
        }
        
        private void UpdateSelectedVoiceDisplay()
        {
            if (_voiceANameLabel != null)
                _voiceANameLabel.text = string.IsNullOrEmpty(_selectedVoiceA) ? "Select Voice..." : _selectedVoiceA;
            else if (_root != null) // Fallback retry cache
                _voiceANameLabel = _root.Q<Label>("selected-a-name");

            if (_voiceBNameLabel != null)
                _voiceBNameLabel.text = string.IsNullOrEmpty(_selectedVoiceB) ? "Select Voice..." : _selectedVoiceB;
            else if (_root != null)
                _voiceBNameLabel = _root.Q<Label>("selected-b-name");
        }
        
        private void ToggleVoicePicker(bool isVoiceA)
        {
            var picker = isVoiceA ? _voiceAPicker : _voiceBPicker;
            if (picker == null) return;
            
            bool isVisible = picker.ClassListContains("voice-picker--visible");
            
            // Close all pickers first
            _voiceAPicker?.RemoveFromClassList("voice-picker--visible");
            _voiceAPicker?.AddToClassList("voice-picker--hidden");
            _voiceBPicker?.RemoveFromClassList("voice-picker--visible");
            _voiceBPicker?.AddToClassList("voice-picker--hidden");
            
            // Toggle the target picker
            if (!isVisible)
            {
                picker.RemoveFromClassList("voice-picker--hidden");
                picker.AddToClassList("voice-picker--visible");
            }
        }
        
        private void SelectVoice(string voiceName, bool isVoiceA)
        {
            if (isVoiceA)
            {
                _selectedVoiceA = voiceName;
                if (_voiceANameLabel != null)
                    _voiceANameLabel.text = voiceName;
                if (_voiceADropdown != null)
                    _voiceADropdown.value = voiceName;
            }
            else
            {
                _selectedVoiceB = voiceName;
                if (_voiceBNameLabel != null)
                    _voiceBNameLabel.text = voiceName;
                if (_voiceBDropdown != null)
                    _voiceBDropdown.value = voiceName;
            }
            
            // Close picker
            ToggleVoicePicker(isVoiceA);
            UpdateStatus($"Selected: {voiceName}", StatusType.Info);
        }
        
        /// <summary>
        /// Sets the voice from external (e.g., VoicePickerWindow)
        /// </summary>
        public void SetVoice(string voiceName)
        {
            _selectedVoiceA = voiceName;
            VoiceFavorites.AddRecentVoice(voiceName);
            UpdateSelectedVoiceDisplay();
            UpdateStatus($"Voice set: {voiceName}", StatusType.Success);
        }

        #endregion
    }
}
