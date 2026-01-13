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
            
            // Create popup
            _nodePopup = new VisualElement();
            _nodePopup.AddToClassList("eq-node-popup");
            
            // Get popup dimensions - matches CSS (220px wide)
            float popupWidth = 220f;
            float popupHeight = 145f;
            
            // Calculate position directly from node's frequency/gain
            // This works even before layout is complete
            var canvasRect = _spectrumCanvas.contentRect;
            float nodeX = FreqToX(node.Frequency, canvasRect.width);
            float nodeY = DbToY(node.Gain, canvasRect.height);
            
            // Convert canvas-relative position to root-relative
            var canvasWorldPos = _spectrumCanvas.LocalToWorld(new Vector2(nodeX + 6, nodeY + 6));
            var rootPos = _root.WorldToLocal(canvasWorldPos);
            
            // Calculate popup position (to the right of node, with some offset)
            float popupX = rootPos.x + 25;
            float popupY = rootPos.y - 40;
            
            var rootRect = _root.contentRect;
            
            if (popupX + popupWidth > rootRect.width - 10)
                popupX = rootPos.x - popupWidth - 25;
            if (popupY + popupHeight > rootRect.height - 10)
                popupY = rootRect.height - popupHeight - 10;
            popupY = Mathf.Max(10, popupY);
            popupX = Mathf.Max(10, popupX);
            
            _nodePopup.style.left = popupX;
            _nodePopup.style.top = popupY;
            
            // === Filter Type Buttons Row (NO title - Freq value shows frequency) ===
            var filterRow = new VisualElement();
            filterRow.style.flexDirection = FlexDirection.Row;
            filterRow.style.marginBottom = 8;
            filterRow.style.marginTop = 2;
            
            string[] typeNames = { "Bell", "LCut", "HCut" };
            EQFilterType[] types = { EQFilterType.Peak, EQFilterType.HighPass, EQFilterType.LowPass };

            // Store buttons for later update
            var filterButtons = new Button[types.Length];

            for (int i = 0; i < types.Length; i++)
            {
                int idx = i; // Capture for closure
                var btn = new Button();
                btn.text = typeNames[i];
                btn.clicked += () => {

                    node.FilterType = types[idx];
                    ApplyNodeToEQ(node);
                    MarkCurveDirty();
                  _spectrumCanvas?.MarkDirtyRepaint();
                    // Update all button styles
                    for (int j = 0; j < filterButtons.Length; j++)
                    {
                        bool sel = j == idx;
                        filterButtons[j].style.backgroundColor = sel ? new Color(0.4f, 0.86f, 0.7f) : new Color(0.2f, 0.2f, 0.22f);
                        filterButtons[j].style.color = sel ? Color.white : new Color(0.6f, 0.6f, 0.6f);
                    }
                };
                
                btn.style.flexGrow = 1;
                btn.style.height = 20;
                btn.style.fontSize = 9;
                btn.style.marginRight = 2;
                btn.style.borderTopLeftRadius = btn.style.borderTopRightRadius = 
                    btn.style.borderBottomLeftRadius = btn.style.borderBottomRightRadius = 2;
                btn.style.borderTopWidth = btn.style.borderBottomWidth = 
                    btn.style.borderLeftWidth = btn.style.borderRightWidth = 0;
                
                bool isSelected = (node.FilterType == types[i]);
                btn.style.backgroundColor = isSelected ? new Color(0.4f, 0.86f, 0.7f) : new Color(0.2f, 0.2f, 0.22f);
                btn.style.color = isSelected ? Color.white : new Color(0.6f, 0.6f, 0.6f);
                
                filterButtons[i] = btn;
                filterRow.Add(btn);
            }
            _nodePopup.Add(filterRow);
            
            // Frequency slider
            var freqRow = CreatePopupRow("Freq", node.Frequency, 20f, 20000f, (v) => {
                node.Frequency = v;
                UpdateNodePosition(node);
                ApplyNodeToEQ(node);
                _spectrumCanvas?.MarkDirtyRepaint();
            }, out _popupFreqSlider, out _popupFreqValue);
            _nodePopup.Add(freqRow);
            
            // Gain slider
            var gainRow = CreatePopupRow("Gain", node.Gain, -24f, 24f, (v) => {
                node.Gain = v;
                UpdateNodePosition(node);
                ApplyNodeToEQ(node);
                _spectrumCanvas?.MarkDirtyRepaint();
            }, out _popupGainSlider, out _popupGainValue, "dB");
            _nodePopup.Add(gainRow);
            
            // Q slider (max 30 for sharper cuts)
            var qRow = CreatePopupRow("Q", node.Q, 0.1f, 30f, (v) => {
                node.Q = v;
                ApplyNodeToEQ(node);
                _spectrumCanvas?.MarkDirtyRepaint();
            }, out _popupQSlider, out _popupQValue);
            _nodePopup.Add(qRow);
            
            _root?.Add(_nodePopup);
            _nodePopup.BringToFront();
        }
        
        /// <summary>
        /// Updates popup slider and label values during node drag
        /// </summary>
        private void UpdatePopupValues(EQNodeElement node)
        {
            if (_nodePopup == null || _selectedNode != node) return;
            
            // Update sliders without triggering callbacks
            _popupFreqSlider?.SetValueWithoutNotify(node.Frequency);
            _popupGainSlider?.SetValueWithoutNotify(node.Gain);
            
            // Update value labels with format and color
            if (_popupFreqValue != null) 
                _popupFreqValue.text = FormatFrequency(node.Frequency);
            if (_popupGainValue != null)
            {
                _popupGainValue.text = FormatGain(node.Gain);
                _popupGainValue.style.color = GetGainColor(node.Gain);
            }
        }
        
        private VisualElement CreatePopupRow(string label, float value, float min, float max, Action<float> onChange, string suffix = "")
        {
            return CreatePopupRow(label, value, min, max, onChange, out _, out _, suffix);
        }
        
        private VisualElement CreatePopupRow(string label, float value, float min, float max, Action<float> onChange, out Slider outSlider, out Label outValueLabel, string suffix = "")
        {
            var row = new VisualElement();
            row.AddToClassList("popup-row");
            
            var lbl = new Label(label);
            lbl.AddToClassList("popup-label");
            row.Add(lbl);
            
            var slider = new Slider(min, max);
            slider.value = value;
            slider.AddToClassList("popup-slider");
            slider.style.flexGrow = 1;
            slider.style.minWidth = 70;
            slider.style.maxWidth = 100;
            row.Add(slider);
            
            // Format and color based on label type
            string displayVal;
            Color displayColor = NeutralColor;
            
            if (label == "Gain")
            {
                displayVal = FormatGain(value);
                displayColor = GetGainColor(value);
            }
            else if (label == "Freq")
            {
                displayVal = FormatFrequency(value);
            }
            else if (label == "Q")
            {
                displayVal = FormatQ(value);
            }
            else
            {
                displayVal = suffix != "" ? $"{value:F1}{suffix}" : $"{value:F1}";
            }
            
            var valLabel = new Label(displayVal);
            valLabel.AddToClassList("popup-value");
            valLabel.style.width = 60;
            valLabel.style.minWidth = 60;
            if (label == "Gain") valLabel.style.color = displayColor;
            row.Add(valLabel);
            
            string capturedLabel = label; // Closure 캡처
            slider.RegisterValueChangedCallback(evt => {
                onChange(evt.newValue);
                
                if (capturedLabel == "Gain")
                {
                    valLabel.text = FormatGain(evt.newValue);
                    valLabel.style.color = GetGainColor(evt.newValue);
                }
                else if (capturedLabel == "Freq")
                {
                    valLabel.text = FormatFrequency(evt.newValue);
                }
                else if (capturedLabel == "Q")
                {
                    valLabel.text = FormatQ(evt.newValue);
                }
                else
                {
                    valLabel.text = suffix != "" ? $"{evt.newValue:F1}{suffix}" : $"{evt.newValue:F1}";
                }
            });
            
            outSlider = slider;
            outValueLabel = valLabel;
            
            return row;
        }

        #endregion
    }
}
