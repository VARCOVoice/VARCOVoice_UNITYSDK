using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VARCOVoice.DSP;

namespace VARCOVoice.Editor
{
    /// <summary>
    /// Controller for a single Effect Pill in the left sidebar.
    /// </summary>
    public class EffectPillController
    {
        private VisualElement _root;
        private Label _nameLabel;
        private VisualElement _statusLed;
        private VisualElement _dragHandle;
        private Button _removeBtn;
        
        private IDSPEffect _effect;
        private DSPChain _chain;
        
        public event Action<IDSPEffect> OnSelected;
        public event Action<IDSPEffect> OnRemoved;

        public IDSPEffect Effect => _effect;
        public VisualElement Root => _root;
        public VisualElement DragHandle => _dragHandle;

        // Abbreviations for long effect names (max ~10 chars)
        private static readonly Dictionary<string, string> NameAbbreviations = new()
        {
            { "FDN Reverb Pro", "FDN Rev" },
            { "Freeverb", "Freeverb" },
            { "Pro Compressor", "Compressor" },
            { "Multiband Compressor", "MB Comp" },
            { "Parametric EQ 16", "Parametric EQ" },
            { "Convolution Reverb", "Conv Rev" },
            { "HRTF Spatializer", "HRTF" },
            { "Formant Preserving Pitch Shift", "Formant PS" },
            { "Phase Vocoder Pitch Shift", "PhaseVoc" },
            { "PSOLA Pitch Shift", "PSOLA" },
            { "WSOLA Pitch Shift", "WSOLA" },
            { "Ring Modulator", "Ring Mod" },
            { "Linear Phase EQ", "Lin EQ" },
            { "Ping Pong Delay", "PP Delay" },
            { "Multi-Tap Delay", "MT Delay" },
            { "Modulated Delay", "Mod Delay" },
            { "Tape Emulation", "Tape" },
            { "Tube Emulation", "Tube" },
            { "Spatial 3D", "Spatial" },
        };

        public void Initialize(VisualElement root, IDSPEffect effect, DSPChain chain)
        {
            _root = root;
            _effect = effect;
            _chain = chain;

            _nameLabel = _root.Q<Label>("effect-name");
            _statusLed = _root.Q<VisualElement>("status-led");
            _dragHandle = _root.Q<VisualElement>("drag-handle");
            _removeBtn = _root.Q<Button>("remove-btn");
            
            // Set content with smart abbreviation
            if (_nameLabel != null)
            {
                _nameLabel.text = ShortenEffectName(effect.Name);
            }
            
            UpdateStatus();
            
            // Click handler (select)
            _root.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.target != _removeBtn)
                {
                    OnSelected?.Invoke(_effect);
                }
            });
            
            // Remove button
            _removeBtn?.RegisterCallback<ClickEvent>(evt =>
            {
                evt.StopPropagation();
                OnRemoved?.Invoke(_effect);
            });
        }

        private static string ShortenEffectName(string name)
        {
            if (NameAbbreviations.TryGetValue(name, out var abbrev))
                return abbrev;
            
            // Fallback: truncate if too long (increased limit)
            if (name.Length > 14)
                return name.Substring(0, 12) + "…";
            
            return name;
        }

        public void SetSelected(bool selected)
        {
            if (selected)
                _root?.AddToClassList("selected");
            else
                _root?.RemoveFromClassList("selected");
        }

        public void UpdateStatus()
        {
            if (_statusLed == null) return;
            
            if (_effect?.Enabled == true)
                _statusLed.RemoveFromClassList("disabled");
            else
                _statusLed.AddToClassList("disabled");
        }

        public void Destroy()
        {
            _root?.RemoveFromHierarchy();
        }
    }
}
