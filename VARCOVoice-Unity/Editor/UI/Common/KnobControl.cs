using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace VARCOVoice.Editor
{
    /// <summary>
    /// Pro-style circular knob control for DSP parameters.
    /// Drag up/down to adjust value.
    /// </summary>
    public class KnobControl : VisualElement
    {
#pragma warning disable CS0618 // Suppress deprecated warning for Unity 2022 LTS compatibility
        public new class UxmlFactory : UxmlFactory<KnobControl, UxmlTraits> { }

        public new class UxmlTraits : VisualElement.UxmlTraits
        {
            UxmlStringAttributeDescription m_Label = new() { name = "label", defaultValue = "Param" };
            UxmlFloatAttributeDescription m_Value = new() { name = "value", defaultValue = 0f };
            UxmlFloatAttributeDescription m_Min = new() { name = "min", defaultValue = 0f };
            UxmlFloatAttributeDescription m_Max = new() { name = "max", defaultValue = 1f };
            UxmlStringAttributeDescription m_Unit = new() { name = "unit", defaultValue = "" };

            public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext cc)
            {
                base.Init(ve, bag, cc);
                var knob = ve as KnobControl;
                knob.label = m_Label.GetValueFromBag(bag, cc);
                knob.minValue = m_Min.GetValueFromBag(bag, cc);
                knob.maxValue = m_Max.GetValueFromBag(bag, cc);
                knob.value = m_Value.GetValueFromBag(bag, cc);
                knob.unit = m_Unit.GetValueFromBag(bag, cc);
            }
        }
#pragma warning restore CS0618

        private VisualElement _knobRing;
        private VisualElement _knobIndicator;
        private Label _valueLabel;
        private Label _labelText;
        private int _size;

        private float _value;
        private float _minValue = 0f;
        private float _maxValue = 1f;
        private string _label = "Param";
        private string _unit = "";
        private bool _isDragging;
        private float _dragStartY;
        private float _dragStartValue;
        private int _dragPointerId = -1;

        public event Action<float> onValueChanged;

        public float value
        {
            get => _value;
            set
            {
                _value = Mathf.Clamp(value, _minValue, _maxValue);
                UpdateVisuals();
            }
        }
        
        /// <summary>
        /// Sets value without triggering onValueChanged callback.
        /// Used for bidirectional sync (e.g., XY pad updating knobs).
        /// </summary>
        public void SetValueWithoutNotify(float newValue)
        {
            _value = Mathf.Clamp(newValue, _minValue, _maxValue);
            UpdateVisuals();
        }

        public float minValue
        {
            get => _minValue;
            set { _minValue = value; UpdateVisuals(); }
        }

        public float maxValue
        {
            get => _maxValue;
            set { _maxValue = value; UpdateVisuals(); }
        }

        public string label
        {
            get => _label;
            set { _label = value; if (_labelText != null) _labelText.text = value; }
        }

        public string unit
        {
            get => _unit;
            set { _unit = value; UpdateVisuals(); }
        }

        public KnobControl() : this(50) { }

        public KnobControl(int size)
        {
            _size = size;
            AddToClassList("knob-control");
            style.width = size + 20;
            style.alignItems = Align.Center;

            // Label above
            _labelText = new Label(_label);
            _labelText.AddToClassList("knob-label");
            Add(_labelText);

            // Knob ring container
            _knobRing = new VisualElement();
            _knobRing.AddToClassList("knob-ring");
            _knobRing.style.width = size;
            _knobRing.style.height = size;
            Add(_knobRing);

            // Indicator dot
            _knobIndicator = new VisualElement();
            _knobIndicator.AddToClassList("knob-indicator");
            _knobRing.Add(_knobIndicator);

            // Value label below
            _valueLabel = new Label();
            _valueLabel.AddToClassList("knob-value");
            Add(_valueLabel);

            // Drag handling
            _knobRing.RegisterCallback<PointerDownEvent>(OnPointerDown);
            _knobRing.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            _knobRing.RegisterCallback<PointerUpEvent>(OnPointerUp);
            _knobRing.RegisterCallback<PointerCaptureOutEvent>(_ => EndDrag());
            
            // Update visuals after layout
            RegisterCallback<GeometryChangedEvent>(_ => UpdateVisuals());

            UpdateVisuals();
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0) return;
            _isDragging = true;
            _dragStartY = evt.position.y;
            _dragStartValue = _value;
            _dragPointerId = evt.pointerId;
            _knobRing.CapturePointer(evt.pointerId);
            _knobRing.AddToClassList("knob-ring--dragging");
            evt.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!_isDragging || !_knobRing.HasPointerCapture(_dragPointerId)) return;

            float deltaY = _dragStartY - evt.position.y;
            float range = _maxValue - _minValue;
            float sensitivity = range / 100f; // 100px for full range

            float newValue = _dragStartValue + deltaY * sensitivity;
            newValue = Mathf.Clamp(newValue, _minValue, _maxValue);

            if (Math.Abs(newValue - _value) > 0.0001f)
            {
                _value = newValue;
                UpdateVisuals();
                onValueChanged?.Invoke(_value);
            }
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            EndDrag();
        }

        private void EndDrag()
        {
            if (!_isDragging) return;
            _isDragging = false;
            if (_knobRing.HasPointerCapture(_dragPointerId))
                _knobRing.ReleasePointer(_dragPointerId);
            _knobRing.RemoveFromClassList("knob-ring--dragging");
            _dragPointerId = -1;
        }

        private void UpdateVisuals()
        {
            // Update value label
            string formatted = FormatValue(_value);
            if (_valueLabel != null)
                _valueLabel.text = formatted + _unit;

            // Update indicator rotation (270° range: -135° to +135°)
            float normalized = Mathf.InverseLerp(_minValue, _maxValue, _value);
            float angle = Mathf.Lerp(-135f, 135f, normalized);
            if (_knobIndicator != null && _size > 0)
            {
                // Position indicator on the edge of the ring using stored size
                float halfSize = _size / 2f;
                float radius = halfSize - 6f; // 6px inset from edge
                float rad = (angle - 90f) * Mathf.Deg2Rad;
                float x = radius * Mathf.Cos(rad);
                float y = radius * Mathf.Sin(rad);
                _knobIndicator.style.left = halfSize + x - 4f; // 4 = half of indicator size (8px)
                _knobIndicator.style.top = halfSize + y - 4f;
            }
        }

        private string FormatValue(float val)
        {
            float range = _maxValue - _minValue;
            if (range >= 100) return val.ToString("F0");
            if (range >= 10) return val.ToString("F1");
            return val.ToString("F2");
        }
    }
}
