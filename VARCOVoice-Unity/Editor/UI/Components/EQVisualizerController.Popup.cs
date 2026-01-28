using System;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using VARCOVoice.DSP;

namespace VARCOVoice.Editor
{
    public partial class EQVisualizerController
    {
        #region Node Popup Logic

        private void UpdatePopupPosition(EQNodeElement node)
        {
            if (_nodePopup == null || _selectedNode != node || _root == null) return;

            float popupWidth = _nodePopup.resolvedStyle.width > 0 ? _nodePopup.resolvedStyle.width : 185f;
            float popupHeight = _nodePopup.resolvedStyle.height > 0 ? _nodePopup.resolvedStyle.height : 130f;

            var nodeWorldPos = node.LocalToWorld(new Vector2(6, 6)); // Center of node
            var rootPos = _root.WorldToLocal(nodeWorldPos);

            float popupX = rootPos.x + 25;
            float popupY = rootPos.y - 40;

            var rootRect = _root.contentRect;

            if (popupX + popupWidth > rootRect.width - 10)
            {
                popupX = rootPos.x - popupWidth - 25;
            }

            if (popupY + popupHeight > rootRect.height - 10)
            {
                popupY = rootRect.height - popupHeight - 10;
            }

            popupY = Mathf.Max(10, popupY);
            popupX = Mathf.Max(10, popupX);

            _nodePopup.style.left = popupX;
            _nodePopup.style.top = popupY;
        }

        private void ShowNodePopup(EQNodeElement node)
        {
            // Remove existing popup
            _nodePopup?.RemoveFromHierarchy();
            
            // Load UXML & USS
            var template = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/com.varco.voice/Editor/UI/Components/EQNodePopup.uxml");
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Packages/com.varco.voice/Editor/UI/Components/EQNodePopup.uss");
            
            if (template == null)
            {
                // Fallback for development
                template = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                    "Assets/VARCOVoice-Unity/Editor/UI/Components/EQNodePopup.uxml");
                styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                    "Assets/VARCOVoice-Unity/Editor/UI/Components/EQNodePopup.uss");
            }
            
            if (template == null)
            {
                Debug.LogError("EQNodePopup.uxml not found!");
                return;
            }

            // Create popup from template
            _nodePopup = template.Instantiate();
            _nodePopup.AddToClassList("eq-node-popup-container"); // Optional wrapper class
            
            // Apply styles
            if (styleSheet != null) _nodePopup.styleSheets.Add(styleSheet);
            
            // --- Element Binding ---
            var nodeRoot = _nodePopup.Q("node-popup");
            if (nodeRoot != null)
            {
                 // Move actual logic here if needed, but we can bind directly
            }
            
            // Filter Buttons
            var btnBell = _nodePopup.Q<Button>("btn-bell");
            var btnLCut = _nodePopup.Q<Button>("btn-lcut");
            var btnHCut = _nodePopup.Q<Button>("btn-hcut");
            
            Action updateFilterUI = () => {
                btnBell?.EnableInClassList("selected", node.FilterType == EQFilterType.Peak);
                btnLCut?.EnableInClassList("selected", node.FilterType == EQFilterType.HighPass);
                btnHCut?.EnableInClassList("selected", node.FilterType == EQFilterType.LowPass);
            };
            
            btnBell?.RegisterCallback<ClickEvent>(_ => { 
                node.FilterType = EQFilterType.Peak; 
                ApplyNodeToEQ(node); 
                updateFilterUI(); 
            });
            
            btnLCut?.RegisterCallback<ClickEvent>(_ => { 
                node.FilterType = EQFilterType.HighPass; 
                ApplyNodeToEQ(node); 
                updateFilterUI(); 
            });
            
            btnHCut?.RegisterCallback<ClickEvent>(_ => { 
                node.FilterType = EQFilterType.LowPass; 
                ApplyNodeToEQ(node); 
                updateFilterUI(); 
            });
            
            updateFilterUI(); // Init state

            // Sliders & Labels
            _popupFreqSlider = _nodePopup.Q<Slider>("slider-freq");
            _popupGainSlider = _nodePopup.Q<Slider>("slider-gain");
            _popupQSlider = _nodePopup.Q<Slider>("slider-q");
            
            _popupFreqValue = _nodePopup.Q<Label>("value-freq");
            _popupGainValue = _nodePopup.Q<Label>("value-gain");
            _popupQValue = _nodePopup.Q<Label>("value-q");

            // Setup Frequency
            if (_popupFreqSlider != null) {
                _popupFreqSlider.value = node.Frequency;
                _popupFreqSlider.RegisterValueChangedCallback(evt => {
                    node.Frequency = evt.newValue;
                    UpdateNodePosition(node);
                    ApplyNodeToEQ(node);
                    UpdatePopupValues(node); // Update label text from logic
                });
            }
            
            // Setup Gain
            if (_popupGainSlider != null) {
                _popupGainSlider.value = node.Gain;
                _popupGainSlider.RegisterValueChangedCallback(evt => {
                    node.Gain = evt.newValue;
                    UpdateNodePosition(node);
                    ApplyNodeToEQ(node);
                    UpdatePopupValues(node);
                });
            }
            
            // Setup Q
            if (_popupQSlider != null) {
                _popupQSlider.value = node.Q;
                _popupQSlider.RegisterValueChangedCallback(evt => {
                    node.Q = evt.newValue;
                    ApplyNodeToEQ(node);
                    UpdatePopupValues(node);
                });
            }

            // Initial Value Update
            UpdatePopupValues(node);

            // Position Popup Logic (Reused)
            // Get popup dimensions - matches CSS (200px wide + padding)
            float popupWidth = 220f; 
            float popupHeight = 150f;
            
            var canvasRect = _spectrumCanvas.contentRect;
            float nodeX = FreqToX(node.Frequency, canvasRect.width);
            float nodeY = DbToY(node.Gain, canvasRect.height);
            
            var canvasWorldPos = _spectrumCanvas.LocalToWorld(new Vector2(nodeX + 6, nodeY + 6));
            var rootPos = _root.WorldToLocal(canvasWorldPos);
            
            float popupX = rootPos.x + 25;
            float popupY = rootPos.y - 40;
            var rootRect = _root.contentRect;
            
            if (popupX + popupWidth > rootRect.width - 10) popupX = rootPos.x - popupWidth - 25;
            if (popupY + popupHeight > rootRect.height - 10) popupY = rootRect.height - popupHeight - 10;
            popupY = Mathf.Max(10, popupY);
            popupX = Mathf.Max(10, popupX);
            
            _nodePopup.style.left = popupX;
            _nodePopup.style.top = popupY;
            _nodePopup.style.position = Position.Absolute;
            
            _root?.Add(_nodePopup);
            _nodePopup.BringToFront();
        }
        
        /// <summary>
        /// Updates popup slider and label values during node drag
        /// </summary>
        private void UpdatePopupValues(EQNodeElement node)
        {
            // Note: UXML instantiated popup might not share same member variable references 
            // if we don't query them again or keep them alive.
            // But we assigned them in ShowNodePopup: _popupFreqSlider, etc.
            
            if (_nodePopup == null || _selectedNode != node) return;
            
            // Update sliders without triggering callbacks (if SetValueWithoutNotify is available)
            _popupFreqSlider?.SetValueWithoutNotify(node.Frequency);
            _popupGainSlider?.SetValueWithoutNotify(node.Gain);
            _popupQSlider?.SetValueWithoutNotify(node.Q);
            
            // Update value labels with format and color
            if (_popupFreqValue != null) 
                _popupFreqValue.text = FormatFrequency(node.Frequency);
                
            if (_popupGainValue != null)
            {
                _popupGainValue.text = FormatGain(node.Gain);
                // Use a helper or inline color logic. Neutral or Color?
                // Let's stick to UXML classes or inline style for specific color logic (Gain Boost/Cut)
                _popupGainValue.style.color = GetGainColor(node.Gain);
            }
            
            if (_popupQValue != null)
                _popupQValue.text = FormatQ(node.Q);
        }
        
        // CreatePopupRow methods removed as we now use UXML + Query
        // (Cleaned up procedural UI code)

        #endregion
    }
}
