using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VARCOVoice.DSP;

namespace VARCOVoice.Editor
{
    public partial class EffectDetailController
    {
        private readonly Dictionary<string, (float min, float max)> _parameterRanges = new()
        {
            // Common
            { "Mix", (0f, 1f) },
            { "Wet", (0f, 1f) },
            { "Dry", (0f, 1f) },
            { "Feedback", (0f, 0.99f) },

            // Dynamics
            { "Threshold", (-60f, 0f) },
            { "Ratio", (1f, 20f) },
            { "Attack", (0.1f, 200f) },
            { "Release", (10f, 2000f) },
            { "Knee", (0f, 24f) },
            { "MakeupGain", (0f, 24f) },
            { "SidechainHPF", (20f, 500f) },
            { "Ceiling", (-12f, 0f) },
            { "Hold", (0f, 500f) },

            // Filter
            { "CutoffFrequency", (20f, 20000f) },
            { "Frequency", (20f, 20000f) },
            { "CenterFreq", (20f, 20000f) },
            { "FreqRange", (100f, 10000f) },
            { "Resonance", (0.1f, 10f) },
            { "Q", (0.1f, 10f) },
            { "FeedbackHPF", (20f, 20000f) },
            { "FeedbackLPF", (20f, 20000f) },

            // Delay/Time
            { "DelayTime", (0f, 2000f) },
            { "DelayMs", (0f, 2000f) },
            { "PreDelay", (0f, 200f) },
            { "PreDelayMs", (0f, 200f) },
            { "Lookahead", (0f, 15f) },

            // Modulation
            { "Rate", (0.01f, 10f) },
            { "Depth", (0f, 50f) },
            { "ModulationRate", (0f, 5f) },
            { "ModulationDepth", (0f, 1f) },
            { "StereoPhase", (0f, 180f) },

            // Gain/Level
            { "Gain", (-24f, 24f) },
            { "OutputGain", (-24f, 24f) },
            { "OutputLevel", (-12f, 12f) },
            { "Output", (-12f, 12f) },
            { "Drive", (0f, 24f) },
            { "InputDrive", (-12f, 24f) },
            { "InputGain", (-24f, 24f) },

            // Pitch
            { "Semitones", (-12f, 12f) },
            { "PitchShift", (-12f, 12f) },

            // Reverb (FDNReverb specific)
            { "RoomSize", (10f, 100f) },
            { "DecayTime", (0.1f, 10f) },
            { "Damping", (0f, 1f) },
            { "Diffusion", (0f, 1f) },
            { "EarlyLevel", (0f, 1f) },
            { "TailLevel", (0f, 1f) },
            { "StereoWidth", (0f, 1f) },
            { "LowDecayMultiplier", (0.5f, 2f) },
            { "Width", (0f, 1f) },

            // Phaser
            { "Stages", (2f, 12f) },
            { "CenterFrequency", (200f, 2000f) },

            // Chorus/Flanger
            { "Voices", (1f, 8f) },
            { "Spread", (0f, 1f) },

            // Spatial
            { "Azimuth", (-180f, 180f) },
            { "Elevation", (-90f, 90f) },
            { "Distance", (0f, 10f) },
        };

        private readonly Dictionary<string, (int min, int max)> _intParameterRanges = new()
        {
            { "Voices", (1, 8) },
            { "Taps", (1, 16) },
            { "TapCount", (1, 8) },
            { "FilterTaps", (256, 16384) },
            { "FFTSize", (256, 8192) },
            { "OverlapFactor", (2, 8) },
            { "SampleRateReduction", (1, 32) },
            { "BitDepth", (1, 16) },
        };

        private VisualElement CreateParameterRow(IDSPEffect effect, EffectParameter param)
        {
            var row = new VisualElement();
            row.AddToClassList("param-row");

            var label = new Label(FormatPropertyName(param.Name));
            label.AddToClassList("param-label");
            row.Add(label);

            if (param.ValueType == typeof(float))
            {
                float currentValue = (float)param.Getter();
                if (param.IsReadOnly)
                {
                    if (TryBuildMeterRow(param, out var meterRow)) return meterRow;
                    var readOnlyValueLabel = BuildReadOnlyValue(row, currentValue, param.Name);
                    ScheduleReadOnlyUpdate(param, readOnlyValueLabel, param.Name);
                    return row;
                }

                var (min, max) = GetRange(param);

                var slider = new Slider(min, max);
                slider.AddToClassList("param-slider");
                slider.SetValueWithoutNotify(Mathf.Clamp(currentValue, min, max));
                slider.RegisterCallback<PointerDownEvent>(_ => BeginEditSession());
                slider.RegisterCallback<PointerUpEvent>(_ => EndEditSession());
                slider.RegisterCallback<PointerCaptureOutEvent>(_ => EndEditSession());

                var sliderValueLabel = new Label(FormatValue(currentValue, param.Name, min, max));
                sliderValueLabel.AddToClassList("param-value");
                
                // Make value label clickable for inline editing
                sliderValueLabel.RegisterCallback<ClickEvent>(evt => {
                    ShowInlineEditor(sliderValueLabel, slider, param.Setter, min, max, param.Name);
                    evt.StopPropagation();
                });

                slider.RegisterValueChangedCallback(evt =>
                {
                    param.Setter?.Invoke(evt.newValue);
                    sliderValueLabel.text = FormatValue(evt.newValue, param.Name, min, max);
                    NotifyChange();
                });

                row.Add(slider);
                row.Add(sliderValueLabel);
            }
            else if (param.ValueType == typeof(int))
            {
                int currentValue = (int)param.Getter();
                if (param.IsReadOnly)
                {
                    var valueLabel = BuildReadOnlyValue(row, currentValue, param.Name);
                    ScheduleReadOnlyUpdate(param, valueLabel, param.Name);
                    return row;
                }

                if (TryGetIntRange(param.Name, out int min, out int max))
                {
                    var slider = new SliderInt(min, max);
                    slider.AddToClassList("param-slider");
                    slider.SetValueWithoutNotify(Mathf.Clamp(currentValue, min, max));
                    slider.RegisterCallback<PointerDownEvent>(_ => BeginEditSession());
                    slider.RegisterCallback<PointerUpEvent>(_ => EndEditSession());
                    slider.RegisterCallback<PointerCaptureOutEvent>(_ => EndEditSession());
                    slider.RegisterValueChangedCallback(evt =>
                    {
                        param.Setter?.Invoke(evt.newValue);
                        NotifyChange();
                    });
                    row.Add(slider);
                }
                else
                {
                    var field = new IntegerField();
                    field.SetValueWithoutNotify(currentValue);
                    field.style.flexGrow = 1;
                    field.style.marginLeft = 10;
                    field.RegisterCallback<PointerDownEvent>(_ => BeginEditSession());
                    field.RegisterCallback<PointerUpEvent>(_ => EndEditSession());
                    field.RegisterCallback<PointerCaptureOutEvent>(_ => EndEditSession());
                    field.RegisterValueChangedCallback(evt =>
                    {
                        param.Setter?.Invoke(evt.newValue);
                        NotifyChange();
                    });
                    row.Add(field);
                }
            }
            else if (param.ValueType == typeof(bool))
            {
                bool currentValue = (bool)param.Getter();
                if (param.IsReadOnly)
                {
                    var valueLabel = BuildReadOnlyValue(row, currentValue, param.Name);
                    ScheduleReadOnlyUpdate(param, valueLabel, param.Name);
                    return row;
                }

                var toggle = new Toggle();
                toggle.AddToClassList("param-toggle");
                toggle.SetValueWithoutNotify(currentValue);
                toggle.RegisterCallback<PointerDownEvent>(_ => BeginEditSession());
                toggle.RegisterCallback<PointerUpEvent>(_ => EndEditSession());
                toggle.RegisterCallback<PointerCaptureOutEvent>(_ => EndEditSession());

                toggle.RegisterValueChangedCallback(evt =>
                {
                    param.Setter?.Invoke(evt.newValue);
                    NotifyChange();
                });

                row.Add(toggle);
            }
            else if (param.ValueType.IsEnum)
            {
                var currentValue = (Enum)param.Getter();
                if (param.IsReadOnly)
                {
                    var valueLabel = BuildReadOnlyValue(row, currentValue, param.Name);
                    ScheduleReadOnlyUpdate(param, valueLabel, param.Name);
                    return row;
                }

                var dropdown = new EnumField(currentValue);
                dropdown.AddToClassList("param-dropdown");
                dropdown.RegisterCallback<PointerDownEvent>(_ => BeginEditSession());
                dropdown.RegisterCallback<PointerUpEvent>(_ => EndEditSession());
                dropdown.RegisterCallback<PointerCaptureOutEvent>(_ => EndEditSession());

                dropdown.RegisterValueChangedCallback(evt =>
                {
                    param.Setter?.Invoke(evt.newValue);
                    NotifyChange();
                });

                row.Add(dropdown);
            }
            else if (typeof(UnityEngine.Object).IsAssignableFrom(param.ValueType))
            {
                var currentValue = param.Getter() as UnityEngine.Object;
                if (param.IsReadOnly)
                {
                    var valueLabel = BuildReadOnlyValue(row, currentValue, param.Name);
                    ScheduleReadOnlyUpdate(param, valueLabel, param.Name);
                    return row;
                }

                var field = new ObjectField();
                field.objectType = param.ValueType;
                field.value = currentValue;
                field.style.flexGrow = 1;
                field.style.marginLeft = 10;
                field.RegisterCallback<PointerDownEvent>(_ => BeginEditSession());
                field.RegisterCallback<PointerUpEvent>(_ => EndEditSession());
                field.RegisterCallback<PointerCaptureOutEvent>(_ => EndEditSession());
                field.RegisterValueChangedCallback(evt =>
                {
                    param.Setter?.Invoke(evt.newValue);
                    NotifyChange();
                });
                row.Add(field);
            }

            return row;
        }

        private (float min, float max) GetRange(EffectParameter param)
        {
            if (param.Range != null)
                return (param.Range.min, param.Range.max);

            // Check direct match
            if (_parameterRanges.TryGetValue(param.Name, out var range))
                return range;

            // Check contains patterns
            if (param.Name.Contains("Frequency") || param.Name.Contains("Cutoff") || param.Name.Contains("Hz"))
                return (20f, 20000f);
            if (param.Name.Contains("Delay"))
                return (0f, 2000f);
            if (param.Name.Contains("Rate"))
                return (0.01f, 10f);
            if (param.Name.Contains("Depth"))
                return (0f, 50f);
            if (param.Name.Contains("Gain"))
                return (-24f, 24f);

            // Default
            return (0f, 1f);
        }

        private bool TryGetIntRange(string propName, out int min, out int max)
        {
            if (_intParameterRanges.TryGetValue(propName, out var range))
            {
                min = range.min;
                max = range.max;
                return true;
            }

            min = 0;
            max = 10;
            return false;
        }

        private string FormatPropertyName(string name)
        {
            // Abbreviations for long names
            var abbrevs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "GainReduction", "GR" },
                { "CurrentGainReduction", "GR" },
                { "Modulation", "Mod" },
                { "ModulationRate", "Mod Rate" },
                { "ModulationDepth", "Mod Depth" },
                { "FilterLow", "HPF" },
                { "FilterHigh", "LPF" },
                { "Frequency", "Freq" },
                { "CutoffFrequency", "Cutoff" },
                { "CenterFreq", "Center" },
                { "Threshold", "Thresh" },
                { "InputLevel", "Input" },
                { "OutputLevel", "Output" },
                { "CurrentInput", "Input" },
                { "CurrentOutput", "Output" },
                { "LowDecayFactor", "Low Decay" },
                { "StereoWidth", "Width" },
                { "PreDelay", "Pre Dly" },
                { "TailLevel", "Tail" },
                { "EarlyLevel", "Early" },
            };
            
            if (abbrevs.TryGetValue(name, out var abbrev))
                return abbrev;
            
            // Add spaces before capitals: "DelayTime" -> "Delay Time"
            var result = System.Text.RegularExpressions.Regex.Replace(name, "(\\B[A-Z])", " $1");
            return result;
        }

        private void ShowInlineEditor(Label valueLabel, Slider slider, Action<object> setter, float min, float max, string propName)
        {
            if (valueLabel == null || slider == null) return;
            
            var parent = valueLabel.parent;
            if (parent == null) return;
            
            // Hide label, create float field
            valueLabel.style.display = DisplayStyle.None;

            var floatField = new FloatField();
            floatField.value = slider.value;
            floatField.style.width = 55;
            floatField.style.minWidth = 55;
            floatField.style.height = 18;
            floatField.style.fontSize = 11;
            parent.Add(floatField);
            floatField.Focus();
            floatField.SelectAll();
            BeginEditSession();
            
            void CommitValue()
            {
                float newVal = Mathf.Clamp(floatField.value, min, max);
                slider.SetValueWithoutNotify(newVal);
                setter?.Invoke(newVal);
                valueLabel.text = FormatValue(newVal, propName, min, max);
                valueLabel.style.display = DisplayStyle.Flex;
                floatField.RemoveFromHierarchy();
                NotifyChange();
                EndEditSession();
            }
            
            floatField.RegisterCallback<KeyDownEvent>(evt => {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    CommitValue();
                    evt.StopPropagation();
                }
                else if (evt.keyCode == KeyCode.Escape)
                {
                    valueLabel.style.display = DisplayStyle.Flex;
                    floatField.RemoveFromHierarchy();
                    EndEditSession();
                    evt.StopPropagation();
                }
            });

            floatField.RegisterCallback<BlurEvent>(_ => CommitValue());
        }

        private string FormatValue(float value, string propName, float min = 0f, float max = 1f)
        {
            if (propName.Contains("DecayTime"))
                return $"{value:F2}s";
            if (propName.Contains("Frequency") || propName.Contains("Cutoff") ||
                propName.Contains("Hz") || propName.Contains("Filter") ||
                propName.Contains("HPF") || propName.Contains("LPF"))
            {
                if (value >= 1000) return $"{value/1000f:F1}kHz";
                return $"{value:F0}Hz";
            }
            if (propName.Contains("Delay") || propName.Contains("Time") || propName.Contains("Attack") || propName.Contains("Release") || propName.Contains("Lookahead"))
                return $"{value:F0}ms";
            if (propName.Contains("Gain") || propName.Contains("dB"))
                return $"{value:F1}dB";
            if (propName == "Mix" || propName == "Wet" || propName == "Dry")
                return $"{value*100:F0}%";
            if (propName.Contains("Level") && min >= 0f && max <= 1f)
                return $"{value*100:F0}%";
            if (propName.Contains("RoomSize"))
                return $"{value:F0}m";
            if (propName.Contains("Ratio"))
                return $"{value:F2}x";

            return $"{value:F2}";
        }

        private VisualElement CreateMeterRow(string label, Func<float> getValue, Func<string> getText, float min, float max)
        {
            var row = new VisualElement();
            row.AddToClassList("meter-row");

            var nameLabel = new Label(label);
            nameLabel.AddToClassList("meter-label");
            row.Add(nameLabel);

            var track = new VisualElement();
            track.AddToClassList("meter-track");
            var fill = new VisualElement();
            fill.AddToClassList("meter-fill");
            track.Add(fill);
            row.Add(track);

            var valueLabel = new Label();
            valueLabel.AddToClassList("meter-value");
            row.Add(valueLabel);

            void UpdateMeter()
            {
                float value = getValue();
                float normalized = Mathf.InverseLerp(min, max, value);
                fill.style.width = Length.Percent(Mathf.Clamp01(normalized) * 100f);
                valueLabel.text = getText();
            }

            UpdateMeter();
            var item = row.schedule.Execute(UpdateMeter).Every(100);
            _scheduledUpdates.Add(item);

            return row;
        }

        private bool TryBuildMeterRow(EffectParameter param, out VisualElement meterRow)
        {
            meterRow = null;
            if (!param.IsReadOnly || param.ValueType != typeof(float)) return false;

            string name = param.Name;
            if (name.IndexOf("GainReduction", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                meterRow = CreateMeterRow(
                    FormatPropertyName(name),
                    () => Mathf.Clamp(-(float)param.Getter(), 0f, 30f),
                    () => $"{(float)param.Getter():F1} dB",
                    0f,
                    30f);
                return true;
            }

            if (name.IndexOf("Input", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Output", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                meterRow = CreateMeterRow(
                    FormatPropertyName(name),
                    () => Mathf.Clamp((float)param.Getter(), -60f, 0f),
                    () => $"{(float)param.Getter():F1} dB",
                    -60f,
                    0f);
                return true;
            }

            return false;
        }

        private Label BuildReadOnlyValue(VisualElement row, object value, string propName)
        {
            var valueLabel = new Label(FormatReadOnlyValue(value, propName));
            valueLabel.AddToClassList("param-value");
            valueLabel.AddToClassList("param-readonly");
            row.Add(valueLabel);
            return valueLabel;
        }

        private void ScheduleReadOnlyUpdate(EffectParameter param, Label label, string propName)
        {
            void UpdateLabel()
            {
                label.text = FormatReadOnlyValue(param.Getter(), propName);
            }

            UpdateLabel();
            var item = label.schedule.Execute(UpdateLabel).Every(200);
            _scheduledUpdates.Add(item);
        }

        private string FormatReadOnlyValue(object value, string propName)
        {
            switch (value)
            {
                case float f:
                    return FormatValue(f, propName);
                case int i:
                    return i.ToString();
                case bool b:
                    return b ? "On" : "Off";
                case Enum e:
                    return e.ToString();
                case UnityEngine.Object obj:
                    return obj != null ? obj.name : "None";
                default:
                    return value?.ToString() ?? "-";
            }
        }

        private int GetColumnCount(List<EffectParameter> parameters, HashSet<string> excluded)
        {
            int count = 0;
            foreach (var param in parameters)
            {
                if (!excluded.Contains(param.Name)) count++;
            }

            // Keep columns minimal to avoid horizontal overflow
            // With Meter column, 2 param columns max works best
            if (count <= 4) return 1;
            return 2;
        }

        private VisualElement CreateColumnsContainer(string className, int columnCount, out List<VisualElement> columns)
        {
            var container = new VisualElement();
            container.AddToClassList(className);

            columns = new List<VisualElement>(Mathf.Max(1, columnCount));
            for (int i = 0; i < Mathf.Max(1, columnCount); i++)
            {
                var column = new VisualElement();
                column.AddToClassList("detail-column");
                container.Add(column);
                columns.Add(column);
            }

            return container;
        }

        private VisualElement CreateColumnsContainer(string className, out VisualElement left, out VisualElement right,
            string rightClass = null)
        {
            var container = CreateColumnsContainer(className, 2, out var columns);
            left = columns[0];
            right = columns[1];
            if (!string.IsNullOrEmpty(rightClass))
            {
                right.AddToClassList(rightClass);
            }
            return container;
        }

        private Label CreateSectionHeader(string title)
        {
            var header = new Label(title);
            header.AddToClassList("section-header");
            return header;
        }

        private void AddSectionHeader(VisualElement container, string title)
        {
            if (container == null) return;
            container.Add(CreateSectionHeader(title));
        }

        private string FormatTypeName(string typeName)
        {
            return typeName.Replace("Effect", "").Replace("FDN", "FDN ");
        }

        private List<EffectParameter> GetParameters(IDSPEffect effect)
        {
            var type = effect.GetType();
            var parameters = new List<EffectParameter>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (prop.GetIndexParameters().Length > 0) continue;
                if (!prop.CanRead) continue;
                if (prop.Name == "Name" || prop.Name == "Enabled") continue;
                if (!IsSupportedType(prop.PropertyType)) continue;

                var range = prop.GetCustomAttribute<RangeAttribute>();
                if (range == null)
                {
                    var backingField = type.GetField($"<{prop.Name}>k__BackingField",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                    range = backingField?.GetCustomAttribute<RangeAttribute>();
                }

                parameters.Add(new EffectParameter
                {
                    Name = prop.Name,
                    ValueType = prop.PropertyType,
                    Getter = () => prop.GetValue(effect),
                    Setter = prop.CanWrite ? value => prop.SetValue(effect, value) : null,
                    Range = range
                });
                seen.Add(prop.Name);
            }

            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (field.IsInitOnly) continue;
                if (field.Name == "Name" || field.Name == "Enabled") continue;
                if (!IsSupportedType(field.FieldType)) continue;
                if (seen.Contains(field.Name)) continue;

                parameters.Add(new EffectParameter
                {
                    Name = field.Name,
                    ValueType = field.FieldType,
                    Getter = () => field.GetValue(effect),
                    Setter = value => field.SetValue(effect, value),
                    Range = field.GetCustomAttribute<RangeAttribute>()
                });
                seen.Add(field.Name);
            }

            return parameters;
        }

        private bool IsSupportedType(Type type)
        {
            if (type == typeof(float) || type == typeof(int) || type == typeof(bool)) return true;
            if (type.IsEnum) return true;
            if (typeof(UnityEngine.Object).IsAssignableFrom(type)) return true;
            return false;
        }

        private sealed class EffectParameter
        {
            public string Name { get; set; }
            public Type ValueType { get; set; }
            public Func<object> Getter { get; set; }
            public Action<object> Setter { get; set; }
            public RangeAttribute Range { get; set; }
            public bool IsReadOnly => Setter == null;
        }
    }
}
