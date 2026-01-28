using UnityEngine;
using UnityEngine.UIElements;
using VARCOVoice.Editor.UI;

namespace VARCOVoice.Editor.UI.Components
{
    /// <summary>
    /// Full 360° Stereo Field Visualizer for Spatial3D Effect
    /// Click and drag anywhere within the circle to position the sound source
    /// </summary>
    public class SpatialVisualizer : DSPVisualizerBase
    {
        // Position state: normalized coordinates (-1 to 1)
        private float _posX = 0f;  // -1 = left, 0 = center, 1 = right
        private float _posY = 0f;  // -1 = back, 0 = center, 1 = front
        
        public float Pan 
        { 
            get => _posX; 
            set { _posX = Mathf.Clamp(value, -1f, 1f); } 
        }
        
        public float Distance 
        { 
            get => Mathf.Sqrt(_posX * _posX + _posY * _posY); 
            set 
            { 
                // When setting distance, keep current angle but adjust radius
                float currentDist = Distance;
                if (currentDist > 0.001f)
                {
                    float scale = Mathf.Clamp01(value) / currentDist;
                    _posX *= scale;
                    _posY *= scale;
                }
                else
                {
                    // If at center, move to front
                    _posY = -Mathf.Clamp01(value);
                }
            }
        }
        
        public float Width { get; set; } = 1f;
        public float Spread { get; set; } = 180f;
        
        public System.Action<float, float> OnPanDistanceChanged { get; set; }
        
        private bool _isDragging;
        private float _circleRadius;
        private Vector2 _circleCenter;
        
        // Colors
        private readonly Color _bgColor = new Color(0.08f, 0.09f, 0.11f, 1f);
        private readonly Color _gridColor = new Color(1f, 1f, 1f, 0.08f);
        private readonly Color _axisColor = new Color(1f, 1f, 1f, 0.2f);
        private readonly Color _accentColor = new Color(0.2f, 0.85f, 0.7f);
        private readonly Color _leftColor = new Color(0.3f, 0.6f, 1f);
        private readonly Color _rightColor = new Color(1f, 0.5f, 0.3f);
        private readonly Color _spreadColor = new Color(0.5f, 0.8f, 1f, 0.2f);
        
        public SpatialVisualizer() : base()
        {
            style.minHeight = 120;
            style.minWidth = 120;
            
            RegisterCallback<MouseDownEvent>(OnMouseDown);
            RegisterCallback<MouseMoveEvent>(OnMouseMove);
            RegisterCallback<MouseUpEvent>(OnMouseUp);
        }
        
        protected override void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            var painter = ctx.painter2D;
            var rect = contentRect;
            float size = Mathf.Min(rect.width, rect.height);
            float centerX = rect.width * 0.5f;
            float centerY = rect.height * 0.5f;
            float radius = size * 0.45f;
            
            _circleCenter = new Vector2(centerX, centerY);
            _circleRadius = radius;
            
            if (radius < 20) return;
            
            // ===== BACKGROUND CIRCLE =====
            painter.fillColor = _bgColor;
            painter.BeginPath();
            painter.Arc(new Vector2(centerX, centerY), radius + 2, 0, 360);
            painter.Fill();
            
            // Border
            painter.strokeColor = new Color(1f, 1f, 1f, 0.15f);
            painter.lineWidth = 1f;
            painter.BeginPath();
            painter.Arc(new Vector2(centerX, centerY), radius, 0, 360);
            painter.Stroke();
            
            // ===== SPREAD VISUALIZATION =====
            float spreadAngle = Spread * 0.5f;
            if (spreadAngle > 0)
            {
                painter.fillColor = _spreadColor;
                painter.BeginPath();
                if (spreadAngle >= 180f)
                {
                    // Full circle for 360° spread
                    painter.Arc(new Vector2(centerX, centerY), radius * 0.9f, 0, 360);
                }
                else
                {
                    painter.MoveTo(new Vector2(centerX, centerY));
                    painter.Arc(new Vector2(centerX, centerY), radius * 0.9f, 270 - spreadAngle, 270 + spreadAngle);
                    painter.ClosePath();
                }
                painter.Fill();
            }
            
            // ===== DISTANCE RINGS =====
            for (int i = 1; i <= 4; i++)
            {
                float r = radius * i / 4f;
                painter.strokeColor = _gridColor;
                painter.lineWidth = 1f;
                painter.BeginPath();
                painter.Arc(new Vector2(centerX, centerY), r, 0, 360);
                painter.Stroke();
            }
            
            // ===== AXIS LINES =====
            painter.strokeColor = _axisColor;
            painter.lineWidth = 1f;
            
            painter.BeginPath();
            painter.MoveTo(new Vector2(centerX - radius, centerY));
            painter.LineTo(new Vector2(centerX + radius, centerY));
            painter.Stroke();
            
            painter.BeginPath();
            painter.MoveTo(new Vector2(centerX, centerY - radius));
            painter.LineTo(new Vector2(centerX, centerY + radius));
            painter.Stroke();
            
            // ===== L/R LABELS =====
            painter.fillColor = _leftColor;
            painter.BeginPath();
            painter.Arc(new Vector2(centerX - radius + 8, centerY), 5, 0, 360);
            painter.Fill();
            
            painter.fillColor = _rightColor;
            painter.BeginPath();
            painter.Arc(new Vector2(centerX + radius - 8, centerY), 5, 0, 360);
            painter.Fill();
            
            // ===== WIDTH VISUALIZATION =====
            float widthAngle = Width * 30f;
            float widthRadius = radius * 0.6f;
            
            painter.strokeColor = new Color(_leftColor.r, _leftColor.g, _leftColor.b, 0.5f);
            painter.lineWidth = 3f;
            painter.BeginPath();
            painter.Arc(new Vector2(centerX, centerY), widthRadius, 180 - widthAngle, 180 + widthAngle);
            painter.Stroke();
            
            painter.strokeColor = new Color(_rightColor.r, _rightColor.g, _rightColor.b, 0.5f);
            painter.BeginPath();
            painter.Arc(new Vector2(centerX, centerY), widthRadius, -widthAngle, widthAngle);
            painter.Stroke();
            
            // ===== POSITION INDICATOR (Full 360°) =====
            // Use internal _posX, _posY directly for rendering
            float posX = centerX + _posX * radius * 0.9f;
            float posY = centerY - _posY * radius * 0.9f;  // Y is inverted (up = positive Y in logic)
            
            // Position trail
            painter.strokeColor = new Color(_accentColor.r, _accentColor.g, _accentColor.b, 0.4f);
            painter.lineWidth = 2f;
            painter.BeginPath();
            painter.MoveTo(new Vector2(centerX, centerY));
            painter.LineTo(new Vector2(posX, posY));
            painter.Stroke();
            
            // Glow effect
            for (int i = 3; i >= 1; i--)
            {
                float alpha = 0.15f / i;
                painter.fillColor = new Color(_accentColor.r, _accentColor.g, _accentColor.b, alpha);
                painter.BeginPath();
                painter.Arc(new Vector2(posX, posY), 8 + i * 4, 0, 360);
                painter.Fill();
            }
            
            // Main position dot
            painter.fillColor = _accentColor;
            painter.BeginPath();
            painter.Arc(new Vector2(posX, posY), 10, 0, 360);
            painter.Fill();
            
            // Inner highlight
            painter.fillColor = new Color(1f, 1f, 1f, 0.6f);
            painter.BeginPath();
            painter.Arc(new Vector2(posX - 2, posY - 2), 3, 0, 360);
            painter.Fill();
            
            // ===== CENTER POINT (Listener) =====
            painter.fillColor = new Color(0.3f, 0.9f, 0.4f, 0.8f);
            painter.BeginPath();
            painter.Arc(new Vector2(centerX, centerY), 6, 0, 360);
            painter.Fill();
            
            painter.strokeColor = new Color(0.3f, 0.9f, 0.4f, 1f);
            painter.lineWidth = 2f;
            painter.BeginPath();
            painter.Arc(new Vector2(centerX, centerY), 6, 0, 360);
            painter.Stroke();
        }
        
        private bool IsWithinCircle(Vector2 localPos)
        {
            float dx = localPos.x - _circleCenter.x;
            float dy = localPos.y - _circleCenter.y;
            return (dx * dx + dy * dy) <= (_circleRadius * _circleRadius);
        }
        
        private void OnMouseDown(MouseDownEvent evt)
        {
            if (evt.button == 0 && IsWithinCircle(evt.localMousePosition))
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
            float centerX = _circleCenter.x;
            float centerY = _circleCenter.y;
            float radius = _circleRadius;
            
            if (radius < 10) return;
            
            // Calculate normalized position (-1 to 1)
            float dx = (localPos.x - centerX) / (radius * 0.9f);
            float dy = -(localPos.y - centerY) / (radius * 0.9f);  // Invert Y
            
            // Clamp to circle
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            if (dist > 1f)
            {
                dx /= dist;
                dy /= dist;
            }
            
            if (Mathf.Abs(dx - _posX) > 0.01f || Mathf.Abs(dy - _posY) > 0.01f)
            {
                _posX = dx;
                _posY = dy;
                MarkDirtyRepaint();
                
                // Report Pan (X) and Distance
                float newDist = Mathf.Sqrt(_posX * _posX + _posY * _posY);
                OnPanDistanceChanged?.Invoke(_posX, newDist);
            }
        }
    }
}
