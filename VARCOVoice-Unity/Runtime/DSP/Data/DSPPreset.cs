using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace VARCOVoice.DSP
{
    /// <summary>
    /// Serializable data for a single effect's configuration.
    /// </summary>
    [Serializable]
    public class EffectData
    {
        public string TypeName;
        public bool Enabled = true;
        public List<EffectParameter> Parameters = new List<EffectParameter>();
    }

    /// <summary>
    /// Serializable key-value pair for effect parameters.
    /// </summary>
    [Serializable]
    public class EffectParameter
    {
        public string Name;
        public string Value; // Stored as string, parsed at runtime
        public string TypeHint; // "float", "int", "bool", "enum"
    }

    /// <summary>
    /// ScriptableObject for storing DSP effect chain presets.
    /// Can be saved as .asset files for persistence.
    /// </summary>
    [CreateAssetMenu(fileName = "DSPPreset", menuName = "VARCO Voice/DSP Preset", order = 100)]
    public class DSPPreset : ScriptableObject
    {
        [Header("Preset Info")]
        public string PresetName = "New Preset";
        
        [TextArea(2, 4)]
        public string Description = "";
        
        [Header("Effect Chain")]
        public List<EffectData> Effects = new List<EffectData>();

        /// <summary>
        /// Captures the current state of a DSPChain into this preset.
        /// </summary>
        public void CaptureFromChain(DSPChain chain)
        {
            if (chain == null) return;
            
            Effects.Clear();
            foreach (var effect in chain.Effects)
            {
                if (effect == null) continue;
                
                var data = new EffectData
                {
                    TypeName = effect.GetType().AssemblyQualifiedName,
                    Enabled = effect.Enabled
                };

                // Serialize all public properties
                var props = effect.GetType().GetProperties(
                    System.Reflection.BindingFlags.Public | 
                    System.Reflection.BindingFlags.Instance);
                
                foreach (var prop in props)
                {
                    if (!prop.CanRead || !prop.CanWrite) continue;
                    if (prop.Name == "Name" || prop.Name == "Enabled") continue;
                    
                    var value = prop.GetValue(effect);
                    if (value == null) continue;
                    
                    string typeHint = "string";
                    string valueStr;
                    if (prop.PropertyType == typeof(float))
                    {
                        typeHint = "float";
                        valueStr = ((float)value).ToString(CultureInfo.InvariantCulture);
                    }
                    else if (prop.PropertyType == typeof(int))
                    {
                        typeHint = "int";
                        valueStr = value.ToString();
                    }
                    else if (prop.PropertyType == typeof(bool))
                    {
                        typeHint = "bool";
                        valueStr = value.ToString();
                    }
                    else if (prop.PropertyType.IsEnum)
                    {
                        typeHint = "enum:" + prop.PropertyType.AssemblyQualifiedName;
                        valueStr = value.ToString();
                    }
                    else
                    {
                        valueStr = value.ToString();
                    }
                    
                    data.Parameters.Add(new EffectParameter
                    {
                        Name = prop.Name,
                        Value = valueStr,
                        TypeHint = typeHint
                    });
                }

                // Serialize public fields
                var fields = effect.GetType().GetFields(
                    System.Reflection.BindingFlags.Public | 
                    System.Reflection.BindingFlags.Instance);

                foreach (var field in fields)
                {
                   if (field.Name == "Name" || field.Name == "Enabled") continue;

                   var value = field.GetValue(effect);
                   if (value == null) continue;

                   string typeHint = "string";
                   string valueStr;
                   if (field.FieldType == typeof(float))
                   {
                       typeHint = "float";
                       valueStr = ((float)value).ToString(CultureInfo.InvariantCulture);
                   }
                   else if (field.FieldType == typeof(int))
                   {
                       typeHint = "int";
                       valueStr = value.ToString();
                   }
                   else if (field.FieldType == typeof(bool))
                   {
                       typeHint = "bool";
                       valueStr = value.ToString();
                   }
                   else if (field.FieldType.IsEnum)
                   {
                       typeHint = "enum:" + field.FieldType.AssemblyQualifiedName;
                       valueStr = value.ToString();
                   }
                   else
                   {
                       valueStr = value.ToString();
                   }

                   data.Parameters.Add(new EffectParameter
                   {
                       Name = field.Name,
                       Value = valueStr,
                       TypeHint = typeHint
                   });
                }
                
                Effects.Add(data);
            }
        }

        /// <summary>
        /// Applies this preset to a DSPChain, replacing its current effects.
        /// </summary>
        public void ApplyToChain(DSPChain chain)
        {
            if (chain == null) return;

            var effects = new List<IDSPEffect>();

            foreach (var data in Effects)
            {
                if (string.IsNullOrEmpty(data.TypeName)) continue;
                
                var type = Type.GetType(data.TypeName);
                if (type == null)
                {
#if VARCO_DEBUG
                    Debug.LogWarning($"[DSPPreset] Unknown effect type: {data.TypeName}");
#endif
                    continue;
                }
                
                var effect = Activator.CreateInstance(type) as IDSPEffect;
                if (effect == null) continue;
                
                effect.Enabled = data.Enabled;
                
                // Restore parameters
                foreach (var param in data.Parameters)
                {
                    // Try Property
                    var prop = type.GetProperty(param.Name);
                    if (prop != null && prop.CanWrite)
                    {
                        try
                        {
                            object parsedValue = ParseValue(param.Value, param.TypeHint, prop.PropertyType);
                            if (parsedValue != null)
                            {
                                prop.SetValue(effect, parsedValue);
                            }
                        }
                        catch (Exception) { }
                        continue; // Found property, skip field check
                    }

                    // Try Field
                    var field = type.GetField(param.Name);
                    if (field != null)
                    {
                        try
                        {
                            object parsedValue = ParseValue(param.Value, param.TypeHint, field.FieldType);
                            if (parsedValue != null)
                            {
                                field.SetValue(effect, parsedValue);
                            }
                        }
                        catch (Exception) { }
                    }
                }

                effects.Add(effect);
            }

            chain.ApplyPresetEffects(effects);
        }

        private object ParseValue(string value, string typeHint, Type targetType)
        {
            if (typeHint == "float") return float.Parse(value, CultureInfo.InvariantCulture);
            if (typeHint == "int") return int.Parse(value, CultureInfo.InvariantCulture);
            if (typeHint == "bool") return bool.Parse(value);
            if (typeHint.StartsWith("enum:"))
            {
                return Enum.Parse(targetType, value);
            }
            return value;
        }
    }
}
