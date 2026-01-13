using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using VARCOVoice.DSP;
using VARCOVoice.Editor.Services;
using Object = UnityEngine.Object;

namespace VARCOVoice.Editor
{
    public partial class DSPPanelController
    {

        private void DrawSpectrumLog(Rect rect)
        {
            // Spectrum Curve (Line Only)
            // Use FIXED point count for consistent Hz accuracy regardless of window size
            const int pointCount = 512;
            Vector3[] points = new Vector3[pointCount];

            float minLog = Mathf.Log10(20);
            float maxLog = Mathf.Log10(20000);
            float logRange = maxLog - minLog;

            var smoothSpectrum = AudioAnalysisService.SmoothSpectrum;
            if (smoothSpectrum == null || smoothSpectrum.Length == 0) return;

            Handles.BeginGUI();

            float nyquist = AudioSettings.outputSampleRate / 2f;
            int spectrumBins = smoothSpectrum.Length / 2; // Only use first half (positive frequencies)
            
            for (int i = 0; i < pointCount; i++)
            {
                float t = (float)i / (pointCount - 1);
                float logFreq = minLog + t * logRange;
                float freq = Mathf.Pow(10, logFreq);
                
                // Find bin - accurate mapping
                float binFloat = (freq / nyquist) * spectrumBins;
                int binIndex = Mathf.FloorToInt(binFloat);
                binIndex = Mathf.Clamp(binIndex, 0, spectrumBins - 1);
                
                float mag = smoothSpectrum[binIndex];
                // Linear interpolation between bins for smoother curve
                if (binIndex < spectrumBins - 1)
                {
                    float frac = binFloat - binIndex;
                    mag = Mathf.Lerp(mag, smoothSpectrum[binIndex + 1], frac);
                }
                
                // Convert to dB height
                float db = 20f * Mathf.Log10(mag + 0.00001f);
                float normH = (db + 60f) / 60f; 
                normH = Mathf.Clamp01(normH);
                normH = Mathf.Pow(normH, 0.8f);

                float x = rect.x + t * rect.width;
                float y = rect.y + rect.height - normH * rect.height;
                
                points[i] = new Vector3(x, y);
            }
            
            // Draw Line (Subtle)
            Handles.color = new Color(VarcoEditorStyles.Mint.r, VarcoEditorStyles.Mint.g, VarcoEditorStyles.Mint.b, 0.6f);
            Handles.DrawAAPolyLine(2f, points);

            Handles.EndGUI();
        }


        private void DrawWaveformOverlay(Rect rect)
        {
            Handles.BeginGUI();
            var waveform = AudioAnalysisService.WaveformData;
            if (waveform == null || waveform.Length == 0)
            {
                Handles.EndGUI();
                return;
            }
            int len = waveform.Length;
            Vector3[] points = new Vector3[len];
            
            for (int i = 0; i < len; i++)
            {
                float t = (float)i / len;
                float x = rect.x + t * rect.width;
                float sample = waveform[i];
                
                // Real Oscilloscope View
                // Center vertically, Amplitude scaled to occupy good amount of space
                float amplitude = 0.8f; 
                float y = rect.y + rect.height / 2f - sample * (rect.height * 0.5f * amplitude);
                
                // Clamp to prevent drawing outside area (though usually clipped by GPU, good for safety)
                y = Mathf.Clamp(y, rect.y, rect.y + rect.height);
                
                points[i] = new Vector3(x, y);
            }
            
            // Highlight: Bright White Line (The "Real Waveform")
            Handles.color = new Color(1f, 1f, 1f, 0.95f);
            Handles.DrawAAPolyLine(2.0f, points);
            Handles.EndGUI();
        }

        
        private void DrawRectOutline(Rect rect, Color color)
        {
            Handles.BeginGUI();
            Handles.color = color;
            Handles.DrawAAPolyLine(2f, new Vector3(rect.x, rect.y), new Vector3(rect.x + rect.width, rect.y), new Vector3(rect.x + rect.width, rect.y + rect.height), new Vector3(rect.x, rect.y + rect.height), new Vector3(rect.x, rect.y));
            Handles.EndGUI();
        }

        
        private void DrawVisualizationSection()
        {
            // Integrated View: Analyzer + Meters in one block
            DrawAnalyzer();
        }

        
        private void DrawAnalyzer()
        {
            // Fixed height, must match container height
            var rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(280), GUILayout.ExpandWidth(true));
            
            // Layout: Main Analysis Area | Meters (Right side)
            float meterWidth = 40f;
            Rect analysisRect = new Rect(rect.x, rect.y, rect.width - meterWidth - 4, rect.height);
            Rect meterRect = new Rect(rect.x + rect.width - meterWidth, rect.y, meterWidth, rect.height);

            // Mode Indicator
            string modeText = Application.isPlaying ? "PLAY MODE" : "EDITOR MODE";
            Color modeColor = Application.isPlaying ? new Color(1f, 0.4f, 0.4f, 0.8f) : new Color(0.4f, 0.8f, 1f, 0.5f);
            var modeStyle = new GUIStyle(EditorStyles.boldLabel) { 
                alignment = TextAnchor.UpperRight, 
                normal = { textColor = modeColor },
                fontSize = 10
            };
            GUI.Label(new Rect(analysisRect.xMax - 100, analysisRect.y + 4, 96, 20), modeText, modeStyle);
            
            // ?꿂RITICAL: Calculate node screen positions FIRST (before any interaction)
            // This is needed for hit detection in HandleEQNodeInteraction
            foreach (var node in _eqNodes)
            {
                float nx = analysisRect.x + GetXFromFreq(node.Frequency) * analysisRect.width;
                float ny = analysisRect.y + GetYFromDb(node.Gain) * analysisRect.height;
                node.ScreenPos = new Vector2(nx, ny);
            }
            
            // Calculate popup rect to check if mouse is inside (for event priority)
            Rect popupRect = Rect.zero;
            if (_selectedEQNode != null && _selectedEQNode.IsSelected)
            {
                popupRect = GetPopupRect(_selectedEQNode, analysisRect);
            }
            
            // Handle EQ node interaction (skip if mouse is in popup area - let popup handle it)
            bool mouseInPopup = popupRect.width > 0 && popupRect.Contains(Event.current.mousePosition);
            if (!mouseInPopup)
            {
                HandleEQNodeInteraction(analysisRect);
            }
            
            // Recalculate positions after interaction (node might have moved)
            // Skip recalculation for actively dragging node to prevent popup position jitter
            foreach (var node in _eqNodes)
            {
                if (node.IsDragging) continue; // Keep mouse position during drag
                
                float nx = analysisRect.x + GetXFromFreq(node.Frequency) * analysisRect.width;
                float ny = analysisRect.y + GetYFromDb(node.Gain) * analysisRect.height;
                node.ScreenPos = new Vector2(nx, ny);
            }
            
            if (Event.current.type == EventType.Repaint)
            {
                // 1. Background - gradient-like effect for depth
                EditorGUI.DrawRect(rect, new Color(0.08f, 0.09f, 0.11f)); // Darker base
                
                // Subtle vertical gradient overlay (top lighter)
                for (int i = 0; i < 5; i++)
                {
                    float t = (float)i / 5f;
                    float alpha = 0.03f * (1f - t);
                    EditorGUI.DrawRect(new Rect(rect.x, rect.y + t * rect.height * 0.3f, rect.width, rect.height * 0.06f), new Color(1, 1, 1, alpha));
                } 
                
                // 2. Grid & Guides (Analysis Area)
                DrawAnalyzerGrid(analysisRect);

                // 3. Spectrum
                var spectrum = AudioAnalysisService.SpectrumData;
                if (spectrum != null && spectrum.Length > 0)
                {
                    DrawSpectrumLog(analysisRect);
                }
                
                // 4. EQ Nodes (FabFilter Style) - visual only in Repaint
                DrawEQNodesVisual(analysisRect);
                
                // 5. Meters (Right Side)
                DrawIntegratedMeters(meterRect);

                // Border
                DrawRectOutline(rect, new Color(0.3f, 0.3f, 0.35f));
                
                // Separator line between analysis and meters
                EditorGUI.DrawRect(new Rect(meterRect.x - 2, meterRect.y + 10, 1, meterRect.height - 20), new Color(1,1,1,0.1f));
            }
            
            // 6. Draw popup LAST (on top of everything) - handles all events for sliders
            if (_selectedEQNode != null && _selectedEQNode.IsSelected && analysisRect.width > 10)
            {
                DrawNodePopup(_selectedEQNode, analysisRect);
            }
            
            // Force repaint only when actively dragging (not just selected - that causes lag)
            if (_selectedEQNode != null && _selectedEQNode.IsDragging)
            {
                _visualizerContainer?.MarkDirtyRepaint();
            }
        }


        private void DrawIntegratedMeters(Rect rect)
        {
            // Meter container - use more vertical space
            Rect meterContainer = new Rect(rect.x + 2, rect.y + 4, rect.width - 4, rect.height - 8);
            
            // Container background
            EditorGUI.DrawRect(meterContainer, new Color(0.06f, 0.06f, 0.08f, 0.95f));
            
            // Container border
            DrawRectOutline(meterContainer, new Color(0.2f, 0.2f, 0.25f));
            
            // Compact layout
            float innerPadding = 4f;
            float meterSpacing = 3f;
            float meterWidth = (meterContainer.width - innerPadding * 2 - meterSpacing) / 2f;
            float titleHeight = 14f;
            float labelHeight = 12f;
            float meterHeight = meterContainer.height - innerPadding * 2 - titleHeight - labelHeight - 4;
            
            float leftX = meterContainer.x + innerPadding;
            float rightX = leftX + meterWidth + meterSpacing;
            float meterTopY = meterContainer.y + innerPadding + titleHeight;
            
            // Title
            var titleStyle = new GUIStyle(EditorStyles.miniLabel) { 
                alignment = TextAnchor.MiddleCenter, 
                fontSize = 8,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.45f, 0.45f, 0.5f) }
            };
            GUI.Label(new Rect(meterContainer.x, meterContainer.y + 2, meterContainer.width, titleHeight), "LEVEL", titleStyle);
            
            // Draw meter bars with labels
            DrawSingleMeter(new Rect(leftX, meterTopY, meterWidth, meterHeight), AudioAnalysisService.SmoothLeftLevel, "L");
            DrawSingleMeter(new Rect(rightX, meterTopY, meterWidth, meterHeight), AudioAnalysisService.SmoothRightLevel, "R");
        }

        
        private void DrawSingleMeter(Rect rect, float level, string label)
        {
            // Background track with rounded feel
            EditorGUI.DrawRect(rect, new Color(0.05f, 0.05f, 0.07f));
            
            // Inner track (slightly inset)
            Rect innerRect = new Rect(rect.x + 1, rect.y + 1, rect.width - 2, rect.height - 2);
            EditorGUI.DrawRect(innerRect, new Color(0.03f, 0.03f, 0.04f));
            
            // Fill with gradient-like segments
            float normalizedLevel = Mathf.Clamp01(level * 5f);
            float fillHeight = normalizedLevel * innerRect.height;
            
            // Draw segmented meter
            int segments = 20;
            float segHeight = innerRect.height / segments;
            float segGap = 1f;
            
            for (int i = 0; i < segments; i++)
            {
                float segBottom = innerRect.y + innerRect.height - (i + 1) * segHeight;
                float segT = (float)(i + 1) / segments;
                
                if (segT <= normalizedLevel)
                {
                    // Color based on level
                    Color segColor;
                    if (segT < 0.6f)
                        segColor = VarcoEditorStyles.Mint;
                    else if (segT < 0.85f)
                        segColor = VarcoEditorStyles.Blue; // Light accent
                    else
                        segColor = VarcoEditorStyles.Error;
                    
                    EditorGUI.DrawRect(new Rect(innerRect.x, segBottom + segGap, innerRect.width, segHeight - segGap), segColor);
                }
            }
            
            // Peak Hold line
            float peakNormalized = Mathf.Clamp01(AudioAnalysisService.PeakLevel * 5f);
            float peakY = innerRect.y + innerRect.height * (1f - peakNormalized);
            peakY = Mathf.Clamp(peakY, innerRect.y, innerRect.yMax - 2);
            EditorGUI.DrawRect(new Rect(innerRect.x, peakY, innerRect.width, 2), Color.white);
            
            // Label at bottom - compact
            var labelStyle = new GUIStyle(EditorStyles.miniLabel) { 
                alignment = TextAnchor.UpperCenter, 
                fontSize = 9,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.55f, 0.55f, 0.6f) }
            };
            GUI.Label(new Rect(rect.x - 2, rect.yMax, rect.width + 4, 12), label, labelStyle);
        }

        
        private void DrawAnalyzerGrid(Rect rect)
        {
            Handles.BeginGUI();
            // Clip to rect logic (Manual clipping via logic)
            Color gridColor = new Color(1f, 1f, 1f, 0.08f);
            Handles.color = gridColor;

            // Logarithmic Frequency Lines (Approximate positions)
            float minLog = Mathf.Log10(20);
            float maxLog = Mathf.Log10(20000);
            float range = maxLog - minLog;

            float[] freqs = { 50, 100, 200, 500, 1000, 2000, 5000, 10000 };
            foreach (var f in freqs)
            {
                float logF = Mathf.Log10(f);
                float t = (logF - minLog) / range;
                if (t >= 0 && t <= 1)
                {
                    float x = rect.x + t * rect.width;
                    Handles.DrawAAPolyLine(1f, new Vector3(x, rect.y), new Vector3(x, rect.y + rect.height));
                    
                    // Label
                    GUI.Label(new Rect(x + 2, rect.y + rect.height - 18, 40, 15), FormatFreq(f), new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(1,1,1,0.4f) } });
                }
            }

            // dB Lines - avoid bottom edge for clipping
            float[] dBs = { -6, -12, -24, -48 }; // Removed -60 to avoid clipping
            foreach (var db in dBs)
            {
                float t = Mathf.Abs(db) / 60f;
                float y = rect.y + t * (rect.height - 20); // Leave space at bottom
                Handles.DrawAAPolyLine(1f, new Vector3(rect.x, y), new Vector3(rect.x + rect.width, y));
                
                var dbStyle = new GUIStyle(EditorStyles.miniLabel) { 
                    alignment = TextAnchor.MiddleRight, 
                    fontSize = 9,
                    normal = { textColor = new Color(1, 1, 1, 0.35f) } 
                };
                GUI.Label(new Rect(rect.x + rect.width - 32, y - 7, 28, 14), $"{db}", dbStyle);
            }
            
            Handles.EndGUI();
        }

        
        private string FormatFreq(float f)
        {
            if (f >= 1000) return (f / 1000f).ToString("0") + "k";
            return f.ToString("0");
        }
    }
}
