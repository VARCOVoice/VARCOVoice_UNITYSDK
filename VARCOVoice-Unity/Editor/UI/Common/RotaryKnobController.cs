using UnityEngine;
using UnityEngine.UIElements;

namespace VARCOVoice.Editor
{
    /// <summary>
    /// Rotary Knob Controller - handles mouse drag interaction for knob elements
    /// </summary>
    public class RotaryKnobController
    {
        private VisualElement _knobContainer;
        private VisualElement _indicator;
        private VisualElement _progressArc;
        private Slider _linkedSlider;
        private Label _valueLabel;
        
        private bool _isDragging;
        private Vector2 _dragStartPos;
        private float _dragStartValue;
        
        private float _minValue = 0.5f;
        private float _maxValue = 2.0f;
        private float _currentValue = 1.0f;
        private string _valueFormat = "{0:F1}x";
        
        public float Value => _currentValue;
        public event System.Action<float> OnValueChanged;
        
        public void Initialize(VisualElement knobContainer, VisualElement indicator, VisualElement progressArc, 
            Slider linkedSlider, Label valueLabel, float minValue, float maxValue, float initialValue, string valueFormat)
        {
            _knobContainer = knobContainer;
            _indicator = indicator;
            _progressArc = progressArc;
            _linkedSlider = linkedSlider;
            _valueLabel = valueLabel;
            
            _minValue = minValue;
            _maxValue = maxValue;
            _currentValue = initialValue;
            _valueFormat = valueFormat;
            
            if (_knobContainer == null) return;
            
            // Register mouse events
            _knobContainer.RegisterCallback<MouseDownEvent>(OnMouseDown);
            _knobContainer.RegisterCallback<MouseMoveEvent>(OnMouseMove);
            _knobContainer.RegisterCallback<MouseUpEvent>(OnMouseUp);
            _knobContainer.RegisterCallback<MouseLeaveEvent>(OnMouseLeave);
            _knobContainer.RegisterCallback<WheelEvent>(OnWheel);
            
            // Initial visual update
            UpdateVisuals();
        }
        
        private void OnMouseDown(MouseDownEvent evt)
        {
            if (evt.button != 0) return;
            
            _isDragging = true;
            _dragStartPos = evt.mousePosition;
            _dragStartValue = _currentValue;
            
            _knobContainer.CaptureMouse();
            evt.StopPropagation();
        }
        
        private void OnMouseMove(MouseMoveEvent evt)
        {
            if (!_isDragging) return;
            
            // Calculate drag delta (vertical drag for value change)
            float deltaY = _dragStartPos.y - evt.mousePosition.y;
            float sensitivity = 0.005f; // Reduced for smoother control
            
            // Map delta to value change
            float range = _maxValue - _minValue;
            float valueDelta = deltaY * sensitivity * range;
            
            _currentValue = Mathf.Clamp(_dragStartValue + valueDelta, _minValue, _maxValue);
            
            UpdateVisuals();
            SyncToSlider();
            OnValueChanged?.Invoke(_currentValue);
            
            evt.StopPropagation();
        }
        
        private void OnMouseUp(MouseUpEvent evt)
        {
            if (!_isDragging) return;
            
            _isDragging = false;
            _knobContainer.ReleaseMouse();
        }
        
        private void OnMouseLeave(MouseLeaveEvent evt)
        {
            // Don't stop dragging when mouse leaves - CaptureMouse handles this
            // Dragging continues until MouseUp is received
        }
        
        private void OnWheel(WheelEvent evt)
        {
            float delta = evt.delta.y > 0 ? -0.05f : 0.05f;
            float range = _maxValue - _minValue;
            
            _currentValue = Mathf.Clamp(_currentValue + delta * range, _minValue, _maxValue);
            
            UpdateVisuals();
            SyncToSlider();
            OnValueChanged?.Invoke(_currentValue);
            
            evt.StopPropagation();
        }
        
        private void UpdateVisuals()
        {
            // Update indicator rotation based on value
            // 7 o'clock (-150°) -> 12 o'clock (0°) -> 5 o'clock (+150°)
            if (_indicator != null)
            {
                float normalizedValue = (_currentValue - _minValue) / (_maxValue - _minValue);
                float angle = -150f + normalizedValue * 300f; // -150 to +150 degrees
                _indicator.style.rotate = new Rotate(angle);
            }
            
            // Update progress arc (rotate based on value)
            if (_progressArc != null)
            {
                float normalizedValue = (_currentValue - _minValue) / (_maxValue - _minValue);
                float angle = -45f + normalizedValue * 270f;
                _progressArc.style.rotate = new Rotate(angle);
            }
            
            // Update value label
            if (_valueLabel != null)
            {
                _valueLabel.text = string.Format(_valueFormat, _currentValue);
            }
        }
        
        private void SyncToSlider()
        {
            if (_linkedSlider != null)
            {
                _linkedSlider.SetValueWithoutNotify(_currentValue);
            }
        }
        
        public void SetValue(float value)
        {
            _currentValue = Mathf.Clamp(value, _minValue, _maxValue);
            UpdateVisuals();
            SyncToSlider();
        }
    }
}
