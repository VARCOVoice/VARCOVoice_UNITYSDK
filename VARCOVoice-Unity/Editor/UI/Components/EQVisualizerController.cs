using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using VARCOVoice.DSP;

namespace VARCOVoice.Editor
{
    /// <summary>
    /// UI Toolkit-based EQ Visualizer Controller.
    /// Handles real-time spectrum rendering and interactive EQ nodes using Painter2D.
    /// </summary>
    public partial class EQVisualizerController
    {
        private VisualElement _root;
        private VisualElement _spectrumArea;
        private ParametricEQ16 _lastKnownEQ;
        
        // ... rest of class
        private VisualElement _spectrumCanvas;
        private VisualElement _nodesContainer;
        private VisualElement _meterLeftFill;
        private VisualElement _meterRightFill;
        private VisualElement _meterLeftPeak;
        private VisualElement _meterRightPeak;
        private Label _meterLeftValue;
        private Label _meterRightValue;

        
        // Popup element references for real-time updates (Bug 3 fix)
        private Slider _popupFreqSlider;
        private Slider _popupGainSlider;
        private Slider _popupQSlider;
        private Label _popupTitle;
        private Label _popupFreqValue;
        private Label _popupGainValue;
        private Label _popupQValue;
        
        private Func<float[]> _spectrumProvider;
        
        // Spectrum data (updated externally)
        private float[] _spectrumData = new float[4096];
        private float[] _smoothSpectrum = new float[4096];
        private float[] _smoothPreEQSpectrum;  // Pre-EQ spectrum for overlay
        private float _leftLevel = 0f;
        private float _rightLevel = 0f;
        private float _peakLevel = 0f;
        
        // EQ Nodes
        private List<EQNodeElement> _eqNodes = new List<EQNodeElement>();
        private EQNodeElement _selectedNode;
        private VisualElement _nodePopup;
        
        // Constants
        private const float MIN_FREQ = 20f;
        private const float MAX_FREQ = 20000f;
        private const float MIN_DB = -24f;
        private const float MAX_DB = 24f;
        
        // Grid
        private VisualElement _freqLabelsContainer;
        private VisualElement _dbLabels; // Reference for dynamic offset
        private readonly float[] _gridFreqs = { 50f, 100f, 200f, 500f, 1000f, 2000f, 5000f, 10000f };
        
        // Gain 색상 (옵션 B: 오렌지/파랑)
        private static readonly Color BoostColor = new Color(1f, 0.6f, 0.2f);     // 🟠 오렌지
        private static readonly Color CutColor = new Color(0.4f, 0.6f, 1f);       // 🔵 파랑
        private static readonly Color NeutralColor = new Color(0.6f, 0.6f, 0.6f); // 회색
        
        public event Action OnEQChanged;
        private bool _layoutRestoreDone = false;

        public void Initialize(VisualElement root, ParametricEQ16 targetEQ, Func<float[]> spectrumProvider = null)
        {
            _root = root;
            _targetEQ = targetEQ;
            _spectrumProvider = spectrumProvider;
            
            // Clear existing nodes first (clean slate on re-init)
            ClearAllNodes();
            _layoutRestoreDone = false;  // Reset for re-initialization
            
            QueryElements();
            SetupSpectrumCanvas();
            SetupDefaultNodes();
            CreateGridLabels();
            
            // Restore EQ nodes after layout is complete (deferred)
            // This prevents nodes from appearing at (0,0) when canvas has no size yet
            if (_spectrumCanvas != null)
            {
                _spectrumCanvas.RegisterCallback<GeometryChangedEvent>(OnCanvasLayoutComplete);
            }
        }
        
        private void OnCanvasLayoutComplete(GeometryChangedEvent evt)
        {
            // Only do this once
            if (_layoutRestoreDone) return;
            
            var rect = _spectrumCanvas.contentRect;
            if (rect.width >= 10 && rect.height >= 10)
            {
                _layoutRestoreDone = true;
                RestoreNodesFromDSP();
            }
        }
        
        /// <summary>
        /// Clears all EQ nodes without affecting DSP state.
        /// </summary>
        public void ClearAllNodes()
        {
            if (_eqNodes != null)
            {
                foreach (var node in _eqNodes.ToArray())
                {
                    node?.RemoveFromHierarchy();
                }
                _eqNodes.Clear();
            }
            _selectedNode = null;
            _nodePopup?.RemoveFromHierarchy();
            _nodePopup = null;
            MarkCurveDirty();
        }
        
        /// <summary>
        /// Restores EQ nodes from ParametricEQ16 state.
        /// Called on Initialize to restore nodes after editor restart.
        /// </summary>
        private void RestoreNodesFromDSP()
        {
            var eq = GetParametricEQ();
            if (eq == null) return;
            
            // Ensure EQ is enabled
            eq.Enabled = true;
            
            // Clear any existing nodes first
            ClearAllNodes();
            
            // Restore nodes from enabled bands
            for (int i = 0; i < 16; i++)
            {
                var band = eq.Bands[i];
                
                // Restore all enabled bands
                if (band.Enabled)
                {
                    // Logic: Hide Peak filters with 0 gain (reduces clutter)
                    bool zeroGain = Mathf.Abs(band.Gain) < 0.05f;
                    if (zeroGain && band.Type == EQFilterType.Peak) continue;

                    var node = AddEQNode(band.Frequency, band.Gain, band.Q, band.Type, i);
                    if (node != null)
                    {
                        UpdateNodePosition(node);
                        // Explicitly re-apply to DSP to ensure EQ is active
                        ApplyNodeToEQ(node);
                    }
                }
            }
            
            // Force coefficient recalculation
            eq.UpdateCoefficients(AudioSettings.outputSampleRate);
            
            MarkCurveDirty();
            _spectrumCanvas?.MarkDirtyRepaint();
        }
        
        private void QueryElements()
        {
            _spectrumCanvas = _root.Q<VisualElement>("spectrum-canvas");
            _nodesContainer = _root.Q<VisualElement>("eq-nodes-container");
            _meterLeftFill = _root.Q<VisualElement>("meter-left-fill");
            _meterRightFill = _root.Q<VisualElement>("meter-right-fill");
            _meterLeftPeak = _root.Q<VisualElement>("meter-left-peak");
            _meterRightPeak = _root.Q<VisualElement>("meter-right-peak");
            _meterLeftValue = _root.Q<Label>("meter-left-value");
            _meterRightValue = _root.Q<Label>("meter-right-value");
            _freqLabelsContainer = _root.Q<VisualElement>("freq-labels");
            _dbLabels = _root.Q<VisualElement>("db-labels");

            
            // Clear button
            var clearBtn = _root.Q<Button>("eq-clear-btn");
            if (clearBtn != null)
            {
                clearBtn.clicked += OnClearEQClicked;
            }
        }
        
        /// <summary>
        /// Clears all EQ bands and nodes, resetting EQ to flat.
        /// </summary>
        private void OnClearEQClicked()
        {
            var eq = GetParametricEQ();
            if (eq != null)
            {
                // Reset all bands in DSP
                eq.Reset();
                eq.Enabled = true;
                eq.UpdateCoefficients(AudioSettings.outputSampleRate);
            }
            
            // Clear UI nodes
            ClearAllNodes();
            
            _spectrumCanvas?.MarkDirtyRepaint();
            OnEQChanged?.Invoke();
            

        }

        private void SetupSpectrumCanvas()
        {
            if (_spectrumCanvas == null) return;
            
            // Register for custom drawing
            _spectrumCanvas.generateVisualContent += OnGenerateVisualContent;
            
            // Click to add node
            _spectrumCanvas.RegisterCallback<PointerDownEvent>(OnCanvasPointerDown);
            
            // Handle resize for grid labels and nodes
            _spectrumCanvas.RegisterCallback<GeometryChangedEvent>(evt => {
                UpdateGridLabelPositions();
                UpdateAllNodePositions();
            });
        }

        private void SetupDefaultNodes()
        {
            // Don't create default nodes - let user add them
            // This prevents too many nodes on initialization
        }

        /// <summary>
        /// Main rendering callback - draws spectrum and grid using Painter2D
        /// </summary>
        private void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            var rect = _spectrumCanvas.contentRect;
            if (rect.width < 10 || rect.height < 10) return;
            
            var painter = ctx.painter2D;
            
            // 1. Draw Grid
            DrawGrid(painter, rect);
            
            // 2. Draw Q Rings (behind curve)
            DrawQRings(painter, rect);
            
            // 3. Draw EQ Response Curve
            DrawEQCurve(painter, rect);
            
            // 4. Draw Spectrum
            DrawSpectrum(painter, rect);
        }
        
        /// <summary>
        /// Draws individual Q curves for each node to visualize their bell shape
        /// </summary>
        private void DrawQRings(Painter2D painter, Rect rect)
        {
            float centerY = rect.height / 2f;
            
            foreach (var node in _eqNodes)
            {
                // Skip nodes with zero gain (no visible curve)
                if (Mathf.Abs(node.Gain) < 0.5f) continue;
                
                // Draw individual bell curve for this node
                bool isSelected = node == _selectedNode;
                float alpha = isSelected ? 0.4f : 0.2f;
                
                // Different color for selected node
                if (isSelected)
                    painter.strokeColor = new Color(1f, 0.85f, 0.4f, alpha);
                else
                    painter.strokeColor = new Color(0.4f, 0.86f, 0.7f, alpha);
                
                painter.lineWidth = isSelected ? 2f : 1.5f;
                painter.lineJoin = LineJoin.Round;
                
                painter.BeginPath();
                
                int pointCount = 200;  // High resolution for narrow Q curves
                bool first = true;
                
                for (int i = 0; i < pointCount; i++)
                {
                    float t = (float)i / (pointCount - 1);
                    float x = t * rect.width;
                    float freq = XToFreq(x, rect.width);
                    
                    // Use proper filter response calculation
                    float gain = CalculateFilterGainAtFreq(node, freq);
                    float y = DbToY(gain, rect.height);
                    
                    if (first)
                    {
                        painter.MoveTo(new Vector2(x, y));
                        first = false;
                    }
                    else
                    {
                        painter.LineTo(new Vector2(x, y));
                    }
                }
                
                painter.Stroke();
                
                // Draw filled area for selected node
                if (isSelected)
                {
                    painter.fillColor = new Color(1f, 0.85f, 0.4f, 0.08f);
                    painter.BeginPath();
                    painter.MoveTo(new Vector2(0, centerY));
                    
                    for (int i = 0; i < pointCount; i++)
                    {
                        float t = (float)i / (pointCount - 1);
                        float x = t * rect.width;
                        float freq = XToFreq(x, rect.width);
                        float gain = CalculateFilterGainAtFreq(node, freq);
                        float y = DbToY(gain, rect.height);
                        painter.LineTo(new Vector2(x, y));
                    }
                    
                    painter.LineTo(new Vector2(rect.width, centerY));
                    painter.ClosePath();
                    painter.Fill();
                }
            }
        }

        private void CreateGridLabels()
        {
            if (_freqLabelsContainer == null) return;
            _freqLabelsContainer.Clear();
            
            foreach (float freq in _gridFreqs)
            {
                var label = new Label(FormatGridLabel(freq));
                label.AddToClassList("freq-label");
                label.userData = freq; // Store frequency for updates
                _freqLabelsContainer.Add(label);
            }
            
            // Re-add "Hz" suffix if needed, or rely on UXML structure if it was separate.
            // UXML structure had suffix inside freq-labels. We cleared it.
            // Let's add it back as a static label at the end.
            var suffix = new Label("Hz");
            suffix.AddToClassList("freq-label");
            suffix.AddToClassList("suffix");
            suffix.style.right = 0;
            suffix.style.left = StyleKeyword.Auto;
            suffix.style.translate = new Translate(0, 0, 0); // No center align
            _freqLabelsContainer.Add(suffix);
        }

        private void UpdateGridLabelPositions()
        {
            if (_freqLabelsContainer == null || _spectrumCanvas == null) return;
            var rect = _spectrumCanvas.contentRect;
            if (rect.width < 10) return;
            
            // Calculate offset dynamically from db-labels width
            float offset = 30f; // Fallback
            if (_dbLabels != null)
            {
                float w = _dbLabels.resolvedStyle.width;
                if (!float.IsNaN(w) && w > 0) offset = w;
            }

            foreach (var child in _freqLabelsContainer.Children())
            {
                if (child is Label label && label.userData is float freq)
                {
                    float x = FreqToX(freq, rect.width);
                    label.style.left = x + offset;
                }
            }
        }
        
        private string FormatGridLabel(float freq)
        {
            if (freq >= 1000f) return $"{freq/1000f:0}k";
            return $"{freq:0}";
        }

        private void DrawGrid(Painter2D painter, Rect rect)
        {
            // Vertical Grid Lines
            painter.strokeColor = new Color(1f, 1f, 1f, 0.08f);
            painter.lineWidth = 1.0f;
            painter.BeginPath();
            
            foreach (float freq in _gridFreqs)
            {
                float x = FreqToX(freq, rect.width);
                painter.MoveTo(new Vector2(x, 0));
                painter.LineTo(new Vector2(x, rect.height));
            }
            painter.Stroke();
            
            // Center line (0dB) - visible, full width
            painter.strokeColor = new Color(1f, 1f, 1f, 0.2f);
            painter.lineWidth = 1.0f;
            float centerY = rect.height / 2f;
            painter.BeginPath();
            painter.MoveTo(new Vector2(0, centerY));
            painter.LineTo(new Vector2(rect.width, centerY));
            painter.Stroke();
        }

        private void DrawSpectrum(Painter2D painter, Rect rect)
        {
            if (_smoothSpectrum == null || _smoothSpectrum.Length == 0) return;
            
            int pointCount = 512;
            float minLog = Mathf.Log10(MIN_FREQ);
            float maxLog = Mathf.Log10(MAX_FREQ);
            float logRange = maxLog - minLog;
            float nyquist = AudioSettings.outputSampleRate / 2f;
            int spectrumBins = _smoothSpectrum.Length / 2;
            
            // 1. Draw Pre-EQ Spectrum (Input - Grey, behind)
            if (_smoothPreEQSpectrum != null && _smoothPreEQSpectrum.Length > 0)
            {
                painter.strokeColor = new Color(0.5f, 0.5f, 0.5f, 0.35f); // Grey
                painter.lineWidth = 1.5f;
                painter.lineJoin = LineJoin.Round;
                
                painter.BeginPath();
                bool first = true;
                int preEQBins = _smoothPreEQSpectrum.Length / 2;
                
                for (int i = 0; i < pointCount; i++)
                {
                    float t = (float)i / (pointCount - 1);
                    float logFreq = minLog + t * logRange;
                    float freq = Mathf.Pow(10, logFreq);
                    
                    float binFloat = (freq / nyquist) * preEQBins;
                    int binIndex = Mathf.Clamp(Mathf.FloorToInt(binFloat), 0, preEQBins - 1);
                    float mag = _smoothPreEQSpectrum[binIndex];
                    
                    if (binIndex < preEQBins - 1)
                    {
                        float frac = binFloat - binIndex;
                        mag = Mathf.Lerp(mag, _smoothPreEQSpectrum[binIndex + 1], frac);
                    }
                    
                    float db = 20f * Mathf.Log10(mag + 0.00001f);
                    float normH = (db + 60f) / 60f;
                    normH = Mathf.Clamp01(normH);
                    normH = Mathf.Pow(normH, 0.8f);
                    
                    float x = t * rect.width;
                    float y = rect.height - normH * rect.height;
                    
                    if (first)
                    {
                        painter.MoveTo(new Vector2(x, y));
                        first = false;
                    }
                    else
                    {
                        painter.LineTo(new Vector2(x, y));
                    }
                }
                painter.Stroke();
            }
            
            // 2. Draw Post-EQ Spectrum (Output - Mint, on top)
            painter.strokeColor = new Color(0.4f, 0.86f, 0.7f, 0.6f); // Mint
            painter.lineWidth = 2f;
            painter.lineJoin = LineJoin.Round;
            
            painter.BeginPath();
            bool firstPost = true;
            for (int i = 0; i < pointCount; i++)
            {
                float t = (float)i / (pointCount - 1);
                float logFreq = minLog + t * logRange;
                float freq = Mathf.Pow(10, logFreq);
                
                float binFloat = (freq / nyquist) * spectrumBins;
                int binIndex = Mathf.Clamp(Mathf.FloorToInt(binFloat), 0, spectrumBins - 1);
                float mag = _smoothSpectrum[binIndex];
                
                if (binIndex < spectrumBins - 1)
                {
                    float frac = binFloat - binIndex;
                    mag = Mathf.Lerp(mag, _smoothSpectrum[binIndex + 1], frac);
                }
                
                float db = 20f * Mathf.Log10(mag + 0.00001f);
                float normH = (db + 60f) / 60f;
                normH = Mathf.Clamp01(normH);
                normH = Mathf.Pow(normH, 0.8f);
                
                float x = t * rect.width;
                float y = rect.height - normH * rect.height;
                
                if (firstPost)
                {
                    painter.MoveTo(new Vector2(x, y));
                    firstPost = false;
                }
                else
                {
                    painter.LineTo(new Vector2(x, y));
                }
            }
            
            painter.Stroke();
        }

        // Cache for expensive EQ curve calculation
        private List<Vector2> _cachedCurvePoints = new List<Vector2>();
        private bool _isCurveDirty = true;
        private float _lastCurveWidth = -1f;

        private void MarkCurveDirty()
        {
            _isCurveDirty = true;
        }

        private void DrawEQCurve(Painter2D painter, Rect rect)
        {
            // FabFilter-style EQ Curve with filled gradient
            
            // If no nodes, draw center line only
            if (_eqNodes.Count == 0)
            {
                // Draw 0dB center line
                painter.strokeColor = new Color(1f, 1f, 1f, 0.3f);
                painter.lineWidth = 1.5f;
                float centerY = rect.height / 2f;
                painter.BeginPath();
                painter.MoveTo(new Vector2(0, centerY));
                painter.LineTo(new Vector2(rect.width, centerY));
                painter.Stroke();
                return;
            }
            
            // Re-calculate points only if needed
            if (_isCurveDirty || Math.Abs(_lastCurveWidth - rect.width) > 1f)
            {
                _cachedCurvePoints.Clear();
                _lastCurveWidth = rect.width;
                _isCurveDirty = false;
                
                int pointCount = Mathf.Clamp((int)rect.width, 256, 1280);

                for (int i = 0; i < pointCount; i++)
                {
                    float t = (float)i / (pointCount - 1);
                    float x = t * rect.width;
                    float freq = XToFreq(x, rect.width);
                    
                    // Calculate combined gain from all nodes at this frequency
                    float totalGain = 0f;
                    foreach (var node in _eqNodes)
                    {
                        totalGain += CalculateFilterGainAtFreq(node, freq);
                    }
                    
                    totalGain = Mathf.Clamp(totalGain, MIN_DB, MAX_DB);
                    float y = DbToY(totalGain, rect.height);
                    _cachedCurvePoints.Add(new Vector2(x, y));
                }
            }
            
            // Draw filled area (gradient effect)
            float centerY2 = rect.height / 2f;
            painter.fillColor = new Color(1f, 0.85f, 0.4f, 0.15f);
            painter.BeginPath();
            painter.MoveTo(new Vector2(0, centerY2));
            foreach (var pt in _cachedCurvePoints)
            {
                painter.LineTo(pt);
            }
            painter.LineTo(new Vector2(rect.width, centerY2));
            painter.ClosePath();
            painter.Fill();
            
            // Draw curve line
            painter.strokeColor = new Color(1f, 0.85f, 0.4f, 0.9f);
            painter.lineWidth = 2.5f;
            painter.lineJoin = LineJoin.Round;
            painter.lineCap = LineCap.Round;
            
            painter.BeginPath();
            if (_cachedCurvePoints.Count > 0)
            {
                painter.MoveTo(_cachedCurvePoints[0]);
                for (int i = 1; i < _cachedCurvePoints.Count; i++)
                {
                    painter.LineTo(_cachedCurvePoints[i]);
                }
            }
            painter.Stroke();
        }    


        #region EQ Nodes

        public EQNodeElement AddEQNode(float freq, float gain, float q, EQFilterType type, int bandIndex)
        {
            if (_nodesContainer == null) return null;

            type = SanitizeFilterType(type);
            var node = new EQNodeElement(freq, gain, q, type);
            node.BandIndex = bandIndex;
            node.OnDragUpdate += OnNodeDragUpdate;
            node.OnSelected += OnNodeSelected;
            node.OnRightClick += OnNodeRightClick;
            
            _nodesContainer.Add(node);
            _eqNodes.Add(node);
            
            UpdateNodePosition(node);
            return node;
        }

        private void OnNodeRightClick(EQNodeElement node)
        {
            RemoveEQNode(node);
        }
        
        public void RemoveEQNode(EQNodeElement node)
        {
             var eq = GetParametricEQ();
             if (eq != null && node.BandIndex >= 0 && node.BandIndex < 16)
             {
                 eq.ClearBand(node.BandIndex);
                 eq.UpdateCoefficients(AudioSettings.outputSampleRate);
             }
             
             // UI Cleanup
             node.RemoveFromHierarchy();
             _eqNodes.Remove(node);
             if (_selectedNode == node)
             {
                 _selectedNode = null;
                 _nodePopup?.RemoveFromHierarchy();
             }
             
             MarkCurveDirty();
             OnEQChanged?.Invoke();
             _spectrumCanvas?.MarkDirtyRepaint();
        }

        private void OnNodeDragUpdate(EQNodeElement node, Vector2 position)
        {
            var rect = _spectrumCanvas.contentRect;
            if (rect.width < 10) return;
            
            // Clamp position within canvas bounds
            position.x = Mathf.Clamp(position.x, 0, rect.width);
            position.y = Mathf.Clamp(position.y, 0, rect.height);
            
            // Convert position to frequency and gain
            float freq = XToFreq(position.x, rect.width);
            float gain = YToDb(position.y, rect.height);
            
            // Clamp to valid ranges (20Hz - 20kHz, -30dB to +30dB)
            freq = Mathf.Clamp(freq, MIN_FREQ, MAX_FREQ);
            gain = Mathf.Clamp(gain, MIN_DB, MAX_DB);
            
            node.Frequency = freq;
            node.Gain = gain;
            
            // Update node visual position (clamped)
            UpdateNodePosition(node);
            
            ApplyNodeToEQ(node);
            _spectrumCanvas.MarkDirtyRepaint();
            
            // Update popup position and values if visible
            UpdatePopupPosition(node);
            UpdatePopupValues(node);
        }

        // Logic moved to EQVisualizerController.Popup.cs

        private void OnNodeSelected(EQNodeElement node)
        {
            // Deselect previous
            if (_selectedNode != null && _selectedNode != node)
            {
                _selectedNode.RemoveFromClassList("selected");
            }
            
            _selectedNode = node;
            node.AddToClassList("selected");
            
            ShowNodePopup(node);
        }

        private void UpdateNodePosition(EQNodeElement node)
        {
            var rect = _spectrumCanvas.contentRect;
            if (rect.width < 10) return;
            
            float x = FreqToX(node.Frequency, rect.width);
            float y = DbToY(node.Gain, rect.height);
            
            node.style.left = x;
            node.style.top = y;
        }

        private void UpdateAllNodePositions()
        {
            foreach (var node in _eqNodes)
            {
                UpdateNodePosition(node);
            }
        }

        private void ApplyNodeToEQ(EQNodeElement node)
        {
            var eq = GetParametricEQ();
            if (eq == null) return;
            eq.Enabled = true;

            if (node.BandIndex >= 0 && node.BandIndex < 16)
            {
                var sanitizedType = SanitizeFilterType(node.FilterType);
                if (sanitizedType != node.FilterType)
                {
                    node.FilterType = sanitizedType;
                }
                eq.SetBand(node.BandIndex, node.Frequency, node.Gain, node.Q, sanitizedType);
                
                // Force coefficient recalculation for immediate effect
                eq.UpdateCoefficients(AudioSettings.outputSampleRate);
            }

            MarkCurveDirty();
            OnEQChanged?.Invoke();
        }

        // Logic moved to EQVisualizerController.Popup.cs
        
        // === 포맷 헬퍼 메서드 ===
        
        private string FormatFrequency(float freq)
        {
            if (freq >= 10000f)
                return $"{freq/1000f:F1} kHz";
            else if (freq >= 1000f)
                return $"{freq/1000f:F2} kHz";
            else if (freq >= 100f)
                return $"{freq:F0} Hz";
            else
                return $"{freq:F1} Hz";
        }
        
        private string FormatGain(float gain)
        {
            if (gain > 0.1f)
                return $"+{gain:F1} dB";
            else if (gain < -0.1f)
                return $"{gain:F1} dB";
            else
                return "0.0 dB";
        }
        
        private string FormatQ(float q)
        {
            if (q >= 10f)
                return $"{q:F1}";
            else if (q >= 1f)
                return $"{q:F2}";
            else
                return $"{q:F3}";
        }
        
        private Color GetGainColor(float gain)
        {
            if (gain > 0.1f)
                return BoostColor;   // 🟠 오렌지
            else if (gain < -0.1f)
                return CutColor;     // 🔵 파랑
            else
                return NeutralColor; // 회색
        }
        
        private string FormatFreq(float f)
        {
            if (f >= 1000) return $"{f/1000f:F1}kHz";
            return $"{f:F0}Hz";
        }

        #endregion

        #region Level Meters

        public void UpdateMeters(float left, float right, float peak)
        {
            _leftLevel = left;
            _rightLevel = right;
            _peakLevel = peak;
            
            // Convert dB to normalized (assuming -60dB to 0dB range)
            float leftDb = left;
            float rightDb = right;
            float leftNorm = Mathf.Clamp01((leftDb + 60f) / 60f);
            float rightNorm = Mathf.Clamp01((rightDb + 60f) / 60f);
            float peakNorm = Mathf.Clamp01((peak + 60f) / 60f);
            
            // Update meter fills
            if (_meterLeftFill != null)
                _meterLeftFill.style.height = Length.Percent(leftNorm * 100f);
            if (_meterRightFill != null)
                _meterRightFill.style.height = Length.Percent(rightNorm * 100f);
            
            // Color based on individual levels (L/R separate colors)
            Color GetMeterColor(float norm)
            {
                if (norm < 0.5f) return new Color(0.3f, 0.85f, 0.65f);      // Green/Mint
                if (norm < 0.75f) return new Color(0.4f, 0.86f, 0.7f);      // Light mint
                if (norm < 0.9f) return new Color(1f, 0.8f, 0.2f);          // Yellow/Orange
                return new Color(0.95f, 0.3f, 0.3f);                         // Red (clipping)
            }
            
            if (_meterLeftFill != null)
                _meterLeftFill.style.backgroundColor = GetMeterColor(leftNorm);
            if (_meterRightFill != null)
                _meterRightFill.style.backgroundColor = GetMeterColor(rightNorm);
            
            // Peak indicators
            if (_meterLeftPeak != null)
                _meterLeftPeak.style.bottom = Length.Percent(Mathf.Max(leftNorm, peakNorm) * 100f);
            if (_meterRightPeak != null)
                _meterRightPeak.style.bottom = Length.Percent(Mathf.Max(rightNorm, peakNorm) * 100f);
                
            // Text readouts (dB values)
            if (_meterLeftValue != null) 
                _meterLeftValue.text = leftDb > -60 ? $"{leftDb:F0}" : "-∞";
            if (_meterRightValue != null) 
                _meterRightValue.text = rightDb > -60 ? $"{rightDb:F0}" : "-∞";
        }

        #endregion

        #region Public API

        public void UpdateSpectrum(float[] spectrumData, float[] smoothSpectrum, float[] smoothPreEQSpectrum = null)
        {
            _spectrumData = spectrumData;
            _smoothSpectrum = smoothSpectrum;
            _smoothPreEQSpectrum = smoothPreEQSpectrum;
            
            _spectrumCanvas?.MarkDirtyRepaint();
        }

        public void UpdateStatus(int effectCount)
        {
            // _statusLabel removed by design
        }

        public void Refresh()
        {
            SyncFromEQ();
            UpdateAllNodePositions();
            _spectrumCanvas?.MarkDirtyRepaint();
        }
        
        public void OnUpdate()
        {
            try
            {
                if (_spectrumProvider != null)
                {
                    var spectrum = _spectrumProvider();
                    if (spectrum != null)
                    {
                        UpdateSpectrum(spectrum, spectrum, _smoothPreEQSpectrum);
                    }
                }
                // Skip sync if any node is being dragged to prevent destroying active drag
                if (_eqNodes != null)
                {
                    foreach (var node in _eqNodes)
                    {
                        if (node.IsDragging) return;
                    }
                }
                
                // If popup is visible, only sync node values (not structure)
                // This prevents popup from disappearing while editing
                if (_nodePopup != null && _nodePopup.parent != null)
                {
                    SyncNodeValueOnly();
                    return;
                }
                
                // Full sync from EQ: structure + values
                SyncFromEQ();
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[EQVisualizer] OnUpdate error: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Sync only node values without structural changes (for use when popup is visible)
        /// </summary>
        private void SyncNodeValueOnly()
        {
            var eq = GetParametricEQ();
            if (eq == null || _eqNodes == null) return;
            
            foreach (var node in _eqNodes)
            {
                if (node.BandIndex < 0 || node.BandIndex >= 16) continue;
                if (node == _selectedNode) continue; // Don't update the selected node
                
                var band = eq.Bands[node.BandIndex];
                node.Frequency = band.Frequency;
                node.Gain = band.Gain;
                node.Q = band.Q;
                UpdateNodePosition(node);
            }
            
            MarkCurveDirty();
            _spectrumCanvas?.MarkDirtyRepaint();
        }
        
        /// <summary>
        /// Force immediate refresh of the visualizer (for Clear button)
        /// </summary>
        public void ForceRefresh()
        {
            SyncFromEQ();
            _spectrumCanvas?.MarkDirtyRepaint();
        }
        
        private void SyncNodesToDSP()
        {
             var eq = GetParametricEQ();
             if (eq == null || _eqNodes == null) return;
             
             bool changed = false;
             
             foreach (var node in _eqNodes)
             {
                 // Skip if dragging (avoids fighting)
                 if (node.IsDragging) continue;
                 
                 if (node.BandIndex >= 0 && node.BandIndex < 16)
                 {
                     var band = eq.Bands[node.BandIndex];
                     var sanitizedType = SanitizeFilterType(band.Type);
                     if (sanitizedType != band.Type)
                     {
                         eq.SetBand(node.BandIndex, band.Frequency, band.Gain, band.Q, sanitizedType);
                         band.Type = sanitizedType;
                     }

                     // Check for differences (Epsilon)
                     if (!Mathf.Approximately(node.Frequency, band.Frequency) ||
                         !Mathf.Approximately(node.Gain, band.Gain) ||
                         !Mathf.Approximately(node.Q, band.Q) ||
                         node.FilterType != sanitizedType)
                     {
                         node.Frequency = band.Frequency;
                         node.Gain = band.Gain;
                         node.Q = band.Q;
                         node.FilterType = sanitizedType;
                         
                         UpdateNodePosition(node);
                         
                         // If this is the selected node, update popup too
                         if (_selectedNode == node)
                         {
                             UpdatePopupValues(node);
                         }
                         
                         changed = true;
                     }
                     
                     // Also handle enabled state? If disabled externally, maybe hide?
                     // Currently SyncFromEQ hides disabled bands. 
                     // If a band is disabled while valid, we might want to remove it?
                     // Let's keep it simple: assume structure (enabled/disabled) changes less often 
                     // or requires a full Refresh/Rebuild if bands are added/removed.
                     // But if a band is disabled in Inspector, we should probably remove the node.
                     // IMPORTANT: Removing collection items while iterating is bad.
                     // For now, just sync values. Structure changes (Enabled) are harder to sync cheaply.
                 }
             }
             
             if (changed)
             {
                 _spectrumCanvas?.MarkDirtyRepaint();
             }
        }
        
        private void SyncFromEQ()
        {
            var eq = GetParametricEQ();
            _lastKnownEQ = eq;
            
            if (eq == null) return;

            if (_eqNodes == null) _eqNodes = new List<EQNodeElement>();

            // 1. Identify active bands from DSP
            var activeBands = new List<(int Index, EQBandParams Band)>();
            for (int i = 0; i < 16; i++)
            {
                var band = eq.Bands[i];
                var sanitizedType = SanitizeFilterType(band.Type);
                if (sanitizedType != band.Type)
                {
                    band.Type = sanitizedType;
                    if (band.Enabled)
                    {
                        eq.SetBand(i, band.Frequency, band.Gain, band.Q, sanitizedType);
                    }
                    else
                    {
                        eq.Bands[i] = band;
                    }
                }
                bool isActive = band.Enabled;

                if (isActive)
                {
                    bool zeroGain = Mathf.Abs(band.Gain) < 0.05f;
                    if (zeroGain)
                    {
                        if (band.Type == EQFilterType.Peak)
                        {
                            isActive = false;
                        }
                    }
                }
                
                if (isActive)
                {
                    activeBands.Add((i, band));
                }
            }

            // 2. Identify nodes to remove
            var nodesToRemove = new List<EQNodeElement>();
            foreach (var node in _eqNodes)
            {
                if (!activeBands.Exists(x => x.Index == node.BandIndex))
                {
                    nodesToRemove.Add(node);
                }
            }

            foreach (var node in nodesToRemove)
            {
                node.RemoveFromHierarchy();
                _eqNodes.Remove(node);
                if (_selectedNode == node)
                {
                    _selectedNode = null;
                    _nodePopup?.RemoveFromHierarchy();
                }
            }

            // 3. Update Existing or Add New
            bool uiChanged = nodesToRemove.Count > 0;

            foreach (var (index, band) in activeBands)
            {
                var existingNode = _eqNodes.Find(n => n.BandIndex == index);
                
                if (existingNode != null)
                {
                    if (!Mathf.Approximately(existingNode.Frequency, band.Frequency) ||
                        !Mathf.Approximately(existingNode.Gain, band.Gain) ||
                        !Mathf.Approximately(existingNode.Q, band.Q) ||
                        existingNode.FilterType != band.Type)
                    {
                        existingNode.Frequency = band.Frequency;
                        existingNode.Gain = band.Gain;
                        existingNode.Q = band.Q;
                        existingNode.FilterType = band.Type;
                        
                        UpdateNodePosition(existingNode);
                        
                        if (_selectedNode == existingNode)
                        {
                            UpdatePopupValues(existingNode);
                        }
                        uiChanged = true;
                    }
                }
                else
                {
                    AddEQNode(band.Frequency, band.Gain, band.Q, band.Type, index);
                    uiChanged = true;
                }
            }
            
            if (uiChanged)
            {
                MarkCurveDirty();
                _spectrumCanvas?.MarkDirtyRepaint();
            }
        }

        /// <summary>
        /// Calculate filter gain at a specific frequency based on biquad response.
        /// This matches the audio path used by ParametricEQ16.
        /// </summary>
        private float CalculateFilterGainAtFreq(EQNodeElement node, float freq)
        {
            var filterType = SanitizeFilterType(node.FilterType);
            // Early exit: Peak filter with 0 gain = pass-through (no effect)
            if (filterType == EQFilterType.Peak && Mathf.Approximately(node.Gain, 0f))
                return 0f;

            float sampleRate = AudioSettings.outputSampleRate;
            if (sampleRate <= 0f) sampleRate = 48000f;

            var band = new EQBandParams
            {
                Enabled = true,
                Type = filterType,
                Frequency = node.Frequency,
                Gain = node.Gain,
                Q = node.Q
            };

            EQLogic.UpdateCoefficients(band, (int)sampleRate, out var coeffs);

            float mag = EQLogic.GetBiquadMagnitude(coeffs, freq, (int)sampleRate);
            return 20f * Mathf.Log10(Mathf.Max(mag, 1e-9f));
        }

        private float FreqToX(float freq, float width)
        {
            float minLog = Mathf.Log10(MIN_FREQ);
            float maxLog = Mathf.Log10(MAX_FREQ);
            float fLog = Mathf.Log10(Mathf.Clamp(freq, MIN_FREQ, MAX_FREQ));
            return ((fLog - minLog) / (maxLog - minLog)) * width;
        }

        private float XToFreq(float x, float width)
        {
            float minLog = Mathf.Log10(MIN_FREQ);
            float maxLog = Mathf.Log10(MAX_FREQ);
            float t = Mathf.Clamp01(x / width);
            return Mathf.Pow(10, minLog + t * (maxLog - minLog));
        }

        private float DbToY(float db, float height)
        {
            // 0 dB = center, +30 dB = top, -30 dB = bottom
            float normalized = (db - MIN_DB) / (MAX_DB - MIN_DB);
            return height * (1f - normalized);
        }

        private float YToDb(float y, float height)
        {
            float normalized = 1f - (y / height);
            return MIN_DB + normalized * (MAX_DB - MIN_DB);
        }

        private static EQFilterType SanitizeFilterType(EQFilterType type)
        {
            return type == EQFilterType.LowShelf || type == EQFilterType.HighShelf
                ? EQFilterType.Peak
                : type;
        }

        private ParametricEQ16 _targetEQ;

        public void SetTargetEQ(ParametricEQ16 eq)
        {
            _targetEQ = eq;
            RestoreNodesFromDSP();
        }

        private ParametricEQ16 GetParametricEQ()
        {
            return _targetEQ;
        }

        private void OnCanvasPointerDown(PointerDownEvent evt)
        {
            // Double-click to add new node (limit to 16 max) - CHECK FIRST to avoid single-click logic
            if (evt.clickCount == 2 && _eqNodes.Count < 16)
            {
                var localPos = evt.localPosition;
                var rect = _spectrumCanvas.contentRect;
                
                float freq = XToFreq(localPos.x, rect.width);
                float gain = YToDb(localPos.y, rect.height);
                
                // Find free band index
                int freeIndex = -1;
                for (int i = 0; i < 16; i++)
                {
                    bool taken = false;
                    foreach (var n in _eqNodes)
                    {
                        if (n.BandIndex == i) { taken = true; break; }
                    }
                    if (!taken)
                    {
                        freeIndex = i;
                        break;
                    }
                }
                
                if (freeIndex != -1)
                {
                    // Deselect previous node
                    if (_selectedNode != null)
                    {
                        _selectedNode.RemoveFromClassList("selected");
                    }
                    _nodePopup?.RemoveFromHierarchy();
                    
                    var node = AddEQNode(freq, gain, 1.0f, EQFilterType.Peak, freeIndex);
                    // Ensure the band is enabled in DSP
                    var eq = GetParametricEQ();
                    if (eq != null) 
                    {
                        eq.Bands[freeIndex].Enabled = true; // Directly set enabled before applying
                        ApplyNodeToEQ(node);
                    }
                    
                    // Select the new node and show popup
                    _selectedNode = node;
                    node.AddToClassList("selected");
                    ShowNodePopup(node);
                    
                    // Force immediate repaint of EQ curve
                    _spectrumCanvas?.MarkDirtyRepaint();
                }
                
                evt.StopPropagation();
                return;  // Don't process single-click logic
            }
            
            // Single click on empty space - deselect popup (only if not part of double-click)
            if (evt.clickCount == 1)
            {
                // Use delayed action to check if this becomes a double-click
                // For now, simple approach: just deselect
                _nodePopup?.RemoveFromHierarchy();
                _nodePopup = null;
                if (_selectedNode != null)
                {
                    _selectedNode.RemoveFromClassList("selected");
                    _selectedNode = null;
                }
            }
        }

        #endregion
    }

    /// <summary>
    /// Draggable EQ Node Element
    /// </summary>
    public class EQNodeElement : VisualElement
    {
        public float Frequency { get; set; }
        public float Gain { get; set; }
        public float Q { get; set; }
        public EQFilterType FilterType { get; set; }
        public int BandIndex { get; set; } = -1; // Added for mapping to DSP band
        
        public event Action<EQNodeElement, Vector2> OnDragUpdate;
        public event Action<EQNodeElement> OnSelected;
        public event Action<EQNodeElement> OnRightClick;
        
        public bool IsDragging => _isDragging;
        
        private bool _isDragging;
        private Vector2 _dragOffset;

        public EQNodeElement(float freq, float gain, float q, EQFilterType type)
        {
            Frequency = freq;
            Gain = gain;
            Q = q;
            FilterType = type;
            
            AddToClassList("eq-node");
            AddToClassList(type.ToString().ToLower());
            
            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button == 0) // Left click dragging
            {
                _isDragging = true;
                _dragOffset = evt.localPosition;
                this.CapturePointer(evt.pointerId);
                AddToClassList("dragging");
                OnSelected?.Invoke(this);
                evt.StopPropagation();
            }
            else if (evt.button == 1) // Right click delete
            {
                OnRightClick?.Invoke(this);
                evt.StopPropagation();
            }
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (_isDragging && this.HasPointerCapture(evt.pointerId))
            {
                var parentPos = parent.WorldToLocal(evt.position);
                OnDragUpdate?.Invoke(this, parentPos);
                
                // CRITICAL FIX: Do NOT set style.left/top here.
                // Let the controller update the position (clamped).
            }
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (_isDragging)
            {
                _isDragging = false;
                this.ReleasePointer(evt.pointerId);
                RemoveFromClassList("dragging");
            }
        }

        private void OnPointerCaptureOut(PointerCaptureOutEvent evt)
        {
            _isDragging = false;
            RemoveFromClassList("dragging");
        }
    }
}





