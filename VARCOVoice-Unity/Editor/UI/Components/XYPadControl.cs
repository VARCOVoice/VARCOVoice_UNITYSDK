using UnityEngine;
using UnityEngine.UIElements;
using VARCOVoice.Editor.UI;

namespace VARCOVoice.Editor.UI.Components
{
    /// <summary>
    /// XY Pad control following DSPVisualizerBase pattern
    /// Clean, minimal design matching the UI Toolkit style
    /// </summary>
    public class XYPadControl : DSPVisualizerBase
    {
        public string XLabel { get; set; } = "X";
        public string YLabel { get; set; } = "Y";
        
        public float XValue
        {
            get => _xValue;
            set { _xValue = Mathf.Clamp01(value); MarkDirtyRepaint(); }
        }
        
        public float YValue
        {
            get => _yValue;
            set { _yValue = Mathf.Clamp01(value); MarkDirtyRepaint(); }
        }
        
        public System.Action<float, float> OnValueChanged { get; set; }
        
        private float _xValue = 0.5f;
        private float _yValue = 0.5f;
        private bool _isDragging;
        
        // Match existing UI colors
        private readonly Color _lineColor = new Color(1f, 1f, 1f, 0.15f);
        private readonly Color _accentColor = new Color(0.2f, 0.8f, 0.7f); // Same as other visualizers
        
        public XYPadControl() : base()
        {
            // No extra styling - DSPVisualizerBase handles flexGrow and overflow
            RegisterCallback<MouseDownEvent>(OnMouseDown);
            RegisterCallback<MouseMoveEvent>(OnMouseMove);
            RegisterCallback<MouseUpEvent>(OnMouseUp);
        }
        
        protected override void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            var painter = ctx.painter2D;
            var rect = contentRect;
            float w = rect.width;
            float h = rect.height;
            
            if (w < 10 || h < 10) return;
            
            // Grid lines (5 vertical, 5 horizontal)
            painter.strokeColor = _lineColor;
            painter.lineWidth = 1f;
            
            for (int i = 1; i < 5; i++)
            {
                float x = w * i / 5f;
                painter.BeginPath();
                painter.MoveTo(new Vector2(x, 0));
                painter.LineTo(new Vector2(x, h));
                painter.Stroke();
            }
            
            for (int i = 1; i < 5; i++)
            {
                float y = h * i / 5f;
                painter.BeginPath();
                painter.MoveTo(new Vector2(0, y));
                painter.LineTo(new Vector2(w, y));
                painter.Stroke();
            }
            
            // Center crosshair (slightly brighter)
            painter.strokeColor = new Color(1f, 1f, 1f, 0.25f);
            painter.lineWidth = 1f;
            
            painter.BeginPath();
            painter.MoveTo(new Vector2(w * 0.5f, 0));
            painter.LineTo(new Vector2(w * 0.5f, h));
            painter.Stroke();
            
            painter.BeginPath();
            painter.MoveTo(new Vector2(0, h * 0.5f));
            painter.LineTo(new Vector2(w, h * 0.5f));
            painter.Stroke();
            
            // Current position
            float dotX = _xValue * w;
            float dotY = (1f - _yValue) * h;
            
            // Position crosshairs (accent color, faded)
            painter.strokeColor = new Color(_accentColor.r, _accentColor.g, _accentColor.b, 0.4f);
            painter.lineWidth = 1f;
            
            painter.BeginPath();
            painter.MoveTo(new Vector2(dotX, 0));
            painter.LineTo(new Vector2(dotX, h));
            painter.Stroke();
            
            painter.BeginPath();
            painter.MoveTo(new Vector2(0, dotY));
            painter.LineTo(new Vector2(w, dotY));
            painter.Stroke();
            
            // Dot glow
            for (int i = 3; i >= 1; i--)
            {
                float alpha = 0.15f / i;
                painter.fillColor = new Color(_accentColor.r, _accentColor.g, _accentColor.b, alpha);
                painter.BeginPath();
                painter.Arc(new Vector2(dotX, dotY), 6 + i * 3, 0, 360);
                painter.Fill();
            }
            
            // Dot ring
            painter.strokeColor = _accentColor;
            painter.lineWidth = 2f;
            painter.BeginPath();
            painter.Arc(new Vector2(dotX, dotY), 6, 0, 360);
            painter.Stroke();
            
            // Dot fill
            painter.fillColor = new Color(_accentColor.r, _accentColor.g, _accentColor.b, 0.8f);
            painter.BeginPath();
            painter.Arc(new Vector2(dotX, dotY), 4, 0, 360);
            painter.Fill();
        }
        
        private void OnMouseDown(MouseDownEvent evt)
        {
            if (evt.button == 0)
            {
                _isDragging = true;
                UpdateFromMouse(evt.localMousePosition);
                evt.StopPropagation();
                this.CaptureMouse();
            }
        }
        
        private void OnMouseMove(MouseMoveEvent evt)
        {
            if (_isDragging)
            {
                UpdateFromMouse(evt.localMousePosition);
                evt.StopPropagation();
            }
        }
        
        private void OnMouseUp(MouseUpEvent evt)
        {
            if (_isDragging && evt.button == 0)
            {
                _isDragging = false;
                this.ReleaseMouse();
                evt.StopPropagation();
            }
        }
        
        private void UpdateFromMouse(Vector2 localPos)
        {
            var rect = contentRect;
            float newX = Mathf.Clamp01(localPos.x / rect.width);
            float newY = Mathf.Clamp01(1f - localPos.y / rect.height);
            
            if (Mathf.Abs(newX - _xValue) > 0.001f || Mathf.Abs(newY - _yValue) > 0.001f)
            {
                _xValue = newX;
                _yValue = newY;
                MarkDirtyRepaint();
                OnValueChanged?.Invoke(_xValue, _yValue);
            }
        }
    }
}
