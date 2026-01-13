using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using VARCOVoice.DSP;
using Object = UnityEngine.Object;

namespace VARCOVoice.Editor
{
    public partial class DSPPanelController
    {
        private float GetFreqFromX(float normX)
        {
            float minLog = Mathf.Log10(MIN_FREQ);
            float maxLog = Mathf.Log10(MAX_FREQ);
            return Mathf.Pow(10, Mathf.Lerp(minLog, maxLog, normX));
        }

        
        private float GetXFromFreq(float freq)
        {
            float minLog = Mathf.Log10(MIN_FREQ);
            float maxLog = Mathf.Log10(MAX_FREQ);
            float logF = Mathf.Log10(Mathf.Clamp(freq, MIN_FREQ, MAX_FREQ));
            return (logF - minLog) / (maxLog - minLog);
        }

        
        private float GetDbFromY(float normY) => Mathf.Lerp(MAX_DB, MIN_DB, normY);

        private float GetYFromDb(float db) => Mathf.InverseLerp(MAX_DB, MIN_DB, db);

        private void SyncEQNodesToDSP()
        {
            if (_target == null) return;
            // Ensure reference is valid and currently in the chain
            var validEQ = _target.MasterEQ;
            if (_parametricEQ != validEQ) _parametricEQ = validEQ;

            if (_parametricEQ == null) return;

            // Clear all bands first (set gain to 0)
            for (int i = 0; i < 16; i++)
            {
                _parametricEQ.SetBand(i, 1000f, 0f, 1f, VARCOVoice.DSP.EQFilterType.Peak);
                _parametricEQ.SetBandEnabled(i, false);
            }
            
            // Map UI nodes to DSP bands
            for (int i = 0; i < _eqNodes.Count && i < 16; i++)
            {
                var node = _eqNodes[i];
                
                // Convert UI filter type to DSP filter type
                VARCOVoice.DSP.EQFilterType dspType = node.Type switch
                {
                    EQFilterType.Bell => VARCOVoice.DSP.EQFilterType.Peak,
                    EQFilterType.LowCut => VARCOVoice.DSP.EQFilterType.HighPass,
                    EQFilterType.HighCut => VARCOVoice.DSP.EQFilterType.LowPass,
                    _ => VARCOVoice.DSP.EQFilterType.Peak
                };
                
                _parametricEQ.SetBand(i, node.Frequency, node.Gain, node.Q, dspType);
                _parametricEQ.SetBandEnabled(i, true);
            }
        }

        
        private void HandleEQNodeInteraction(Rect rect)
        {
            Event e = Event.current;
            Vector2 mousePos = e.mousePosition;
            if (!rect.Contains(mousePos)) return;

            // Double-click ?’Create node
            if (e.type == EventType.MouseDown && e.button == 0 && e.clickCount == 2)
            {
                if (_eqNodes.Count >= MAX_EQ_NODES) return; // Limit check
                
                float normX = (mousePos.x - rect.x) / rect.width;
                float normY = (mousePos.y - rect.y) / rect.height;
                
                // Deselect all others FIRST
                foreach (var n in _eqNodes) n.IsSelected = false;
                
                var node = new EQBandNode {
                    Frequency = GetFreqFromX(normX),
                    Gain = GetDbFromY(normY),
                    IsSelected = true  // Now this stays true
                };
                
                _eqNodes.Add(node);
                _selectedEQNode = node;
                SyncEQNodesToDSP(); // New node -> DSP update
                e.Use();
                _visualizerContainer?.MarkDirtyRepaint();
                return;
            }

            // Click ?’Select / Start Drag
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                // Deselect all
                foreach (var n in _eqNodes) n.IsSelected = false;
                _selectedEQNode = null;
                
                // Check hit
                for (int i = _eqNodes.Count - 1; i >= 0; i--)
                {
                    if (Vector2.Distance(mousePos, _eqNodes[i].ScreenPos) < 12f)
                    {
                        _selectedEQNode = _eqNodes[i];
                        _selectedEQNode.IsSelected = true;
                        _selectedEQNode.IsDragging = true;
                        e.Use();
                        break;
                    }
                }
            }

            // Drag ?’Move node
            if (e.type == EventType.MouseDrag && _selectedEQNode?.IsDragging == true)
            {
                float normX = Mathf.Clamp01((mousePos.x - rect.x) / rect.width);
                float normY = Mathf.Clamp01((mousePos.y - rect.y) / rect.height);
                _selectedEQNode.Frequency = GetFreqFromX(normX);
                _selectedEQNode.Gain = GetDbFromY(normY);
                _selectedEQNode.ScreenPos = mousePos; // Fix: Prevent position mismatch flickering
                SyncEQNodesToDSP(); // Real-time DSP update
                e.Use();
            }

            // Mouse Up ¡æ End Drag
            if (e.type == EventType.MouseUp)
            {
                if (_selectedEQNode != null) _selectedEQNode.IsDragging = false;
            }

            // Scroll ¡æ Adjust Q (max 30 for sharper cuts)
            if (e.type == EventType.ScrollWheel && _selectedEQNode != null)
            {
                if (Vector2.Distance(mousePos, _selectedEQNode.ScreenPos) < 30f)
                {
                    _selectedEQNode.Q = Mathf.Clamp(_selectedEQNode.Q + e.delta.y * 0.5f, 0.1f, 30f);
                    SyncEQNodesToDSP(); // Q change -> DSP update
                    e.Use();
                    _visualizerContainer?.MarkDirtyRepaint();
                }
            }
            
            // Right-click ?’Delete
            if (e.type == EventType.MouseDown && e.button == 1)
            {
                for (int i = _eqNodes.Count - 1; i >= 0; i--)
                {
                    if (Vector2.Distance(mousePos, _eqNodes[i].ScreenPos) < 12f)
                    {
                        _eqNodes.RemoveAt(i);
                        _selectedEQNode = null;
                        SyncEQNodesToDSP(); // Node deleted -> DSP update
                        e.Use();
                        _visualizerContainer?.MarkDirtyRepaint();
                        break;
                    }
                }
            }
        }

        
        private void DrawEQNodesVisual(Rect rect)
        {
            Handles.BeginGUI();
            
            // 1. Draw EQ Response Curve (Combined frequency response of all nodes)
            if (_eqNodes.Count > 0)
            {
                DrawEQResponseCurve(rect);
            }
            
            // 2. Draw individual nodes with Q visualization
            foreach (var node in _eqNodes)
            {
                // Use pre-calculated screen position
                float x = node.ScreenPos.x;
                float y = node.ScreenPos.y;
                
                // Draw Q width indicator (bell curve outline for this node)
                DrawNodeQCurve(rect, node);

                // Minimal FabFilter-style node design
                Vector3 center = new Vector3(x, y, 0);
                
                // Base color - soft purple/blue like Pro-Q
                Color nodeColor = VarcoEditorStyles.Mint; // Accent blue
                
                if (node.IsSelected)
                {
                    // Selected: Bright solid circle with thin white border
                    Handles.color = nodeColor;
                    Handles.DrawSolidDisc(center, Vector3.forward, 6f);
                    Handles.color = new Color(1f, 1f, 1f, 0.9f);
                    Handles.DrawWireDisc(center, Vector3.forward, 6f);
                }
                else
                {
                    // Unselected: Semi-transparent filled circle (no border)
                    Handles.color = new Color(nodeColor.r, nodeColor.g, nodeColor.b, 0.5f);
                    Handles.DrawSolidDisc(center, Vector3.forward, 5f);
                }
            }
            
            Handles.EndGUI();
        }

        private Rect GetPopupRect(EQBandNode node, Rect analysisRect)
        {
            float popupWidth = 200f;  // Wider for filter type buttons
            float popupHeight = 135f; // Taller for filter type row
            float x = Mathf.Clamp(node.ScreenPos.x - popupWidth / 2f, analysisRect.x + 5, analysisRect.xMax - popupWidth - 5);
            float y = node.ScreenPos.y + 25f;
            
            if (y + popupHeight > analysisRect.yMax - 10)
                y = node.ScreenPos.y - popupHeight - 25f;
            
            return new Rect(x, y, popupWidth, popupHeight);
        }

        private void DrawNodePopup(EQBandNode node, Rect analysisRect)
        {
            Rect popupRect = GetPopupRect(node, analysisRect);
            
            // Background with rounded look (only on Repaint)
            if (Event.current.type == EventType.Repaint)
            {
                // Shadow
                EditorGUI.DrawRect(new Rect(popupRect.x + 3, popupRect.y + 3, popupRect.width, popupRect.height), 
                    new Color(0, 0, 0, 0.3f));
                
                // Main background
                EditorGUI.DrawRect(popupRect, new Color(0.1f, 0.1f, 0.12f, 0.98f));
                
                // Inner highlight (top edge)
                EditorGUI.DrawRect(new Rect(popupRect.x, popupRect.y, popupRect.width, 2), 
                    new Color(VarcoEditorStyles.Mint.r, VarcoEditorStyles.Mint.g, VarcoEditorStyles.Mint.b, 0.8f));
                
                // Subtle border
                DrawRectOutline(popupRect, new Color(0.3f, 0.3f, 0.35f));
            }
            
            float padding = 10f;
            float rowHeight = 22f;
            float labelWidth = 35f;
            float sliderWidth = popupRect.width - padding * 2 - labelWidth - 8;
            float currentY = popupRect.y + padding + 4;
            
            if (_popupLabelStyle == null)
            {
                _popupLabelStyle = new GUIStyle(EditorStyles.miniLabel) { 
                    normal = { textColor = new Color(0.7f, 0.7f, 0.7f) },
                    fontSize = 11,
                    fontStyle = FontStyle.Bold
                };
            }
            if (_popupValueStyle == null)
            {
                 _popupValueStyle = new GUIStyle(EditorStyles.miniLabel) { 
                    normal = { textColor = VarcoEditorStyles.Mint },
                    alignment = TextAnchor.MiddleRight,  // Right align to prevent overflow
                    fontSize = 11,
                    fontStyle = FontStyle.Bold,
                    clipping = TextClipping.Clip
                };
            }
            
            var labelStyle = _popupLabelStyle;
            var valueStyle = _popupValueStyle;
            
            // === Filter Type Buttons ===
            float btnWidth = (popupRect.width - padding * 2 - 8) / 3f;
            float btnX = popupRect.x + padding;
            
            string[] typeNames = { "Bell", "LCut", "HCut" };
            EQFilterType[] types = { EQFilterType.Bell, EQFilterType.LowCut, EQFilterType.HighCut };
            
            for (int i = 0; i < types.Length; i++)
            {
                bool isSelected = node.Type == types[i];
                Color btnColor = isSelected ? VarcoEditorStyles.Mint : new Color(0.2f, 0.2f, 0.22f);
                Color textColor = isSelected ? Color.white : new Color(0.6f, 0.6f, 0.6f);
                
                var btnRect = new Rect(btnX + i * (btnWidth + 2), currentY, btnWidth, 18f);
                
                if (Event.current.type == EventType.Repaint)
                {
                    EditorGUI.DrawRect(btnRect, btnColor);
                }
                
                var btnStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 9,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = textColor },
                    hover = { textColor = Color.white },
                    active = { textColor = Color.white }
                };
                btnStyle.normal.background = null;
                
                if (GUI.Button(btnRect, typeNames[i], btnStyle))
                {
                    node.Type = types[i];
                    SyncEQNodesToDSP();
                    _visualizerContainer?.MarkDirtyRepaint();
                }
            }
            currentY += 24f;
            
            // === Frequency slider (log scale) ===
            GUI.Label(new Rect(popupRect.x + padding, currentY, labelWidth, rowHeight), "Freq", labelStyle);
            float currentLogFreq = Mathf.Log10(node.Frequency);
            float targetLogFreq = GUI.HorizontalSlider(
                new Rect(popupRect.x + padding + labelWidth, currentY + 5, sliderWidth - 45, rowHeight - 10),
                currentLogFreq, Mathf.Log10(MIN_FREQ), Mathf.Log10(MAX_FREQ));
            
            if (Mathf.Abs(targetLogFreq - currentLogFreq) > 0.01f)
            {
                float smoothedLogFreq = Mathf.Lerp(currentLogFreq, targetLogFreq, 0.3f);
                node.Frequency = Mathf.Pow(10, smoothedLogFreq);
                SyncEQNodesToDSP();
                _visualizerContainer?.MarkDirtyRepaint();
            }
            
            // Format frequency nicely
            string freqText = node.Frequency >= 1000 ? $"{node.Frequency/1000f:F1}k" : $"{node.Frequency:F0}";
            GUI.Label(new Rect(popupRect.xMax - padding - 40, currentY, 40, rowHeight), freqText, valueStyle);
            currentY += rowHeight;
            
            // === Gain slider ===
            GUI.Label(new Rect(popupRect.x + padding, currentY, labelWidth, rowHeight), "Gain", labelStyle);
            float targetGain = GUI.HorizontalSlider(
                new Rect(popupRect.x + padding + labelWidth, currentY + 5, sliderWidth - 45, rowHeight - 10),
                node.Gain, MIN_DB, MAX_DB);
            if (Mathf.Abs(targetGain - node.Gain) > 0.1f)
            {
                node.Gain = Mathf.Lerp(node.Gain, targetGain, 0.3f);
                SyncEQNodesToDSP();
                _visualizerContainer?.MarkDirtyRepaint();
            }
            GUI.Label(new Rect(popupRect.xMax - padding - 40, currentY, 40, rowHeight), $"{node.Gain:F1}dB", valueStyle);
            currentY += rowHeight;
            
            // === Q slider (increased max to 30 for sharper cuts) ===
            GUI.Label(new Rect(popupRect.x + padding, currentY, labelWidth, rowHeight), "Q", labelStyle);
            float targetQ = GUI.HorizontalSlider(
                new Rect(popupRect.x + padding + labelWidth, currentY + 5, sliderWidth - 45, rowHeight - 10),
                node.Q, 0.1f, 30f);  // Max Q increased to 30
            if (Mathf.Abs(targetQ - node.Q) > 0.05f)
            {
                node.Q = Mathf.Lerp(node.Q, targetQ, 0.3f);
                SyncEQNodesToDSP();
                _visualizerContainer?.MarkDirtyRepaint();
            }
            GUI.Label(new Rect(popupRect.xMax - padding - 40, currentY, 40, rowHeight), $"{node.Q:F1}", valueStyle);
        }

        private void DrawEQResponseCurve(Rect rect)
        {
            const int pointCount = 256;
            Vector3[] curvePoints = new Vector3[pointCount];
            
            float minLog = Mathf.Log10(MIN_FREQ);
            float maxLog = Mathf.Log10(MAX_FREQ);
            float logRange = maxLog - minLog;
            
            for (int i = 0; i < pointCount; i++)
            {
                float t = (float)i / (pointCount - 1);
                float logFreq = minLog + t * logRange;
                float freq = Mathf.Pow(10, logFreq);
                
                // Calculate combined gain at this frequency from all nodes
                float totalGainDb = 0f;
                foreach (var node in _eqNodes)
                {
                    totalGainDb += CalculateNodeGainAtFreq(node, freq);
                }
                
                // Clamp to visible range
                totalGainDb = Mathf.Clamp(totalGainDb, MIN_DB, MAX_DB);
                
                float x = rect.x + t * rect.width;
                float y = rect.y + GetYFromDb(totalGainDb) * rect.height;
                
                curvePoints[i] = new Vector3(x, y, 0);
            }
            
            // Draw filled area under curve
            // First, draw semi-transparent fill
            Color fillColor = new Color(VarcoEditorStyles.Mint.r, VarcoEditorStyles.Mint.g, VarcoEditorStyles.Mint.b, 0.15f);
            
            // Draw curve line
            Handles.color = new Color(VarcoEditorStyles.Mint.r, VarcoEditorStyles.Mint.g, VarcoEditorStyles.Mint.b, 0.9f); // Accent blue color for EQ curve
            Handles.DrawAAPolyLine(3f, curvePoints);
        }
        private float CalculateNodeGainAtFreq(EQBandNode node, float freq)
        {
            if (node == null) return 0f;
            if (Mathf.Approximately(node.Gain, 0f) &&
                node.Type == EQFilterType.Bell)
            {
                return 0f;
            }

            VARCOVoice.DSP.EQFilterType dspType = node.Type switch
            {
                EQFilterType.Bell => VARCOVoice.DSP.EQFilterType.Peak,
                EQFilterType.LowCut => VARCOVoice.DSP.EQFilterType.HighPass,
                EQFilterType.HighCut => VARCOVoice.DSP.EQFilterType.LowPass,
                _ => VARCOVoice.DSP.EQFilterType.Peak
            };

            var band = new EQBandParams
            {
                Enabled = true,
                Type = dspType,
                Frequency = node.Frequency,
                Gain = node.Gain,
                Q = node.Q
            };

            int sampleRate = AudioSettings.outputSampleRate;
            EQLogic.UpdateCoefficients(band, sampleRate, out var coeffs);
            float mag = EQLogic.GetBiquadMagnitude(coeffs, freq, sampleRate);
            return 20f * Mathf.Log10(Mathf.Max(mag, 1e-9f));
        }

        private void DrawNodeQCurve(Rect rect, EQBandNode node)
        {
            if (Mathf.Approximately(node.Gain, 0f)) return;
            
            const int pointCount = 64;
            Vector3[] curvePoints = new Vector3[pointCount];
            
            // Determine frequency range to draw (about 3 octaves each side based on Q)
            float octaveSpread = 2f / node.Q; // Wider for low Q, narrower for high Q
            octaveSpread = Mathf.Clamp(octaveSpread, 0.2f, 3f);
            
            float minFreq = node.Frequency / Mathf.Pow(2f, octaveSpread);
            float maxFreq = node.Frequency * Mathf.Pow(2f, octaveSpread);
            
            minFreq = Mathf.Max(minFreq, MIN_FREQ);
            maxFreq = Mathf.Min(maxFreq, MAX_FREQ);
            
            float minLog = Mathf.Log10(minFreq);
            float maxLog = Mathf.Log10(maxFreq);
            
            for (int i = 0; i < pointCount; i++)
            {
                float t = (float)i / (pointCount - 1);
                float logFreq = Mathf.Lerp(minLog, maxLog, t);
                float freq = Mathf.Pow(10, logFreq);
                
                float gainDb = CalculateNodeGainAtFreq(node, freq);
                
                float x = rect.x + GetXFromFreq(freq) * rect.width;
                float y = rect.y + GetYFromDb(gainDb) * rect.height;
                
                curvePoints[i] = new Vector3(x, y, 0);
            }
            
            // Draw with node-specific color (semi-transparent)
            Color curveColor = node.IsSelected 
                ? new Color(VarcoEditorStyles.Mint.r, VarcoEditorStyles.Mint.g, VarcoEditorStyles.Mint.b, 0.4f) 
                : new Color(VarcoEditorStyles.Mint.r, VarcoEditorStyles.Mint.g, VarcoEditorStyles.Mint.b, 0.25f);
            Handles.color = curveColor;
            Handles.DrawAAPolyLine(2f, curvePoints);
        }
    }
}



