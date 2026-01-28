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

        private void DrawEffectChainSection()
        {
            VarcoEditorStyles.DrawSectionHeader("EFFECT STACK");
            DrawVibeGalleryUI();
            DrawLinearEffectStackUI();
        }


        private void DrawVibeGalleryUI()
        {
            EditorGUILayout.BeginHorizontal(VarcoEditorStyles.CardStyle);
            GUILayout.Label("Presets", EditorStyles.miniLabel, GUILayout.Width(60));
            GUI.enabled = _target != null;
            if (GUILayout.Button("Robot", EditorStyles.miniButton, GUILayout.Height(22)))
                ApplyPreset(() => _target?.ApplyRobotVoice());
            if (GUILayout.Button("Radio", EditorStyles.miniButton, GUILayout.Height(22)))
                ApplyPreset(() => _target?.ApplyRadioVoice());
            if (GUILayout.Button("Cave", EditorStyles.miniButton, GUILayout.Height(22)))
                ApplyPreset(() => _target?.ApplyCaveVoice());
            if (GUILayout.Button("Underwater", EditorStyles.miniButton, GUILayout.Height(22)))
                ApplyPreset(() => _target?.ApplyUnderwaterVoice());
            if (GUILayout.Button("Ghost", EditorStyles.miniButton, GUILayout.Height(22)))
                ApplyPreset(() => _target?.ApplyGhostVoice());
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Clear", EditorStyles.miniButton, GUILayout.Width(60), GUILayout.Height(22)))
            {
                _target?.ClearEffects();
                _selectedEffectIndex = -1;
                RebuildLinearConnections();
                SyncRuntimeChain();
                EditorUtility.SetDirty(_target);
                _visualizerContainer?.MarkDirtyRepaint();
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(6);
        }

        private void ApplyPreset(System.Action presetAction)
        {
            if (_target == null || presetAction == null) return;
            presetAction();
            StripLegacyEqEffects();
            RebuildLinearConnections();
            SyncRuntimeChain();
            EditorUtility.SetDirty(_target);
            _visualizerContainer?.MarkDirtyRepaint();
        }


        private void DrawVibeCard(string name, string icon, string description, System.Action onSelect)
        {
            Rect rect = GUILayoutUtility.GetRect(135, 125);
            
            // Background
            EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.15f, 0.6f));
            DrawRectOutline(rect, new Color(0.3f, 0.3f, 0.35f, 0.5f));
            
            // Hover effect
            if (rect.Contains(Event.current.mousePosition))
            {
                EditorGUI.DrawRect(rect, new Color(1, 1, 1, 0.05f));
            }
            
            // Icon
            var iconStyle = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleCenter, fontSize = 28 };
            GUI.Label(new Rect(rect.x, rect.y + 10, rect.width, 40), icon, iconStyle);
            
            // Name
            var nameStyle = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter };
            nameStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(rect.x, rect.y + 50, rect.width, 20), name, nameStyle);
            
            // Description
            var descStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
            descStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
            GUI.Label(new Rect(rect.x + 5, rect.y + 68, rect.width - 10, 30), description, descStyle);
            
            // Select Button
            if (GUI.Button(new Rect(rect.x + 15, rect.y + 98, rect.width - 30, 20), "APPLY", EditorStyles.miniButton))
            {
                onSelect?.Invoke();
                RebuildLinearConnections();
                SyncRuntimeChain();
                EditorUtility.SetDirty(_target);
                _visualizerContainer?.MarkDirtyRepaint();
            }
        }


        private void RebuildLinearConnections()
        {
            if (_target == null) return;
            var effects = _target.Effects.Where(eff => !(eff is ParametricEQ16)).ToList();
            _connections.Clear();
            
            if (effects.Count == 0)
            {
                _connections.Add(new NodeConnection { FromEffect = null, ToEffect = null });
            }
            else
            {
                _connections.Add(new NodeConnection { FromEffect = null, ToEffect = effects[0] });
                for (int i = 0; i < effects.Count - 1; i++)
                    _connections.Add(new NodeConnection { FromEffect = effects[i], ToEffect = effects[i + 1] });
                _connections.Add(new NodeConnection { FromEffect = effects[effects.Count - 1], ToEffect = null });
            }
            
            // Reset positions for a clean linear flow
            for (int i = 0; i < effects.Count; i++)
            {
                _effectPositions[effects[i]] = new Vector2(150 + i * 140, 100);
            }
            SyncRuntimeChain();
        }


        private void SyncRuntimeChain()
        {
            if (_target == null) return;

            // Unified Sync Logic: Always Rebuild from Connections to ensure consistency
            // In Simplified View, RebuildLinearConnections() ensures connections form a linear line.
            
            // Advanced Node Graph: Topological Sort
            var sorted = new List<IDSPEffect>();
            var visited = new HashSet<IDSPEffect>();
            var stack = new Stack<IDSPEffect>();
            
            // Find path from Input to Output
            void Visit(IDSPEffect current)
            {
                if (current == null || visited.Contains(current)) return;
                visited.Add(current);
                
                foreach (var conn in _connections)
                {
                    if (conn.FromEffect == current && conn.ToEffect != null)
                    {
                        Visit(conn.ToEffect);
                    }
                }
                sorted.Add(current);
            }

            // Start from Input (null)
            foreach(var conn in _connections)
            {
                if (conn.FromEffect == null && conn.ToEffect != null)
                {
                    Visit(conn.ToEffect);
                }
            }

            sorted.Reverse(); // Topological order
            
            // Preserve EQ effects as they are special and usually not in the graph
            // We fetch them from the existing target to ensure we don't lose them
            var eqEffects = _target.Effects.Where(eff => eff is ParametricEQ16).ToList();
            
            var finalChain = new List<IDSPEffect>();
            
            // Strategy: EQ First, then Graph Chain
            finalChain.AddRange(eqEffects); 
            
            // Add unique sorted effects
            foreach(var eff in sorted)
            {
                if (!finalChain.Contains(eff)) finalChain.Add(eff);
            }
            
            // Safety: If there were effects in TARGET that are NOT in graph (orphaned),
            // and we are in Simplified View, they might be effectively removed.
            // This is correct behavior: What you see is what you process.
            
            _target.SetEffects(finalChain);
        }


        private void ReorderEffect(int fromIndex, int toIndex)
        {
            if (_target == null) return;
            var effects = _target.Effects.ToList();
            if (fromIndex < 0 || fromIndex >= effects.Count || toIndex < 0 || toIndex >= effects.Count) return;
            
            var item = effects[fromIndex];
            effects.RemoveAt(fromIndex);
            effects.Insert(toIndex, item);
            
            _target.SetEffects(effects);
            RebuildLinearConnections(); // Rebuild connections to match new order
            SyncRuntimeChain();
            EditorUtility.SetDirty(_target);
        }


        private void DrawLinearEffectStackUI()
        {
            StripLegacyEqEffects();
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Mixer Line", EditorStyles.miniLabel, GUILayout.Width(70));
            GUI.enabled = _target != null;
            if (GUILayout.Button("Add FX", EditorStyles.miniButton, GUILayout.Width(70)))
                ShowSimplifiedAddMenu();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Clear", EditorStyles.miniButton, GUILayout.Width(60)))
            {
                _target?.ClearEffects();
                _selectedEffectIndex = -1;
                RebuildLinearConnections();
                SyncRuntimeChain();
                EditorUtility.SetDirty(_target);
                _visualizerContainer?.MarkDirtyRepaint();
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(6);
            _effectStackScroll = EditorGUILayout.BeginScrollView(_effectStackScroll, 
                GUILayout.Height(1120), 
                GUILayout.MinWidth(600),
                GUILayout.MaxWidth(1200));
            var allEffects = _target?.Effects ?? new List<IDSPEffect>();
            var filteredEffects = allEffects.Where(eff => !(eff is ParametricEQ16) && !(eff is EQEffect)).ToList();
            if (filteredEffects.Count == 0)
            {
                EditorGUILayout.HelpBox("No effects in chain. Use Add FX to start.", MessageType.Info);
            }
            for (int i = 0; i < filteredEffects.Count; i++)
            {
                DrawEffectModuleUI(filteredEffects[i], i, filteredEffects.Count);
            }
            EditorGUILayout.EndScrollView();
        }

        private void ShowSimplifiedAddMenu()
        {
            if (_target == null) return;
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Dynamics"), false, () => AddEffectInstance(new UnifiedDynamics()));
            menu.AddItem(new GUIContent("Delay"), false, () => AddEffectInstance(new UnifiedDelay()));
            menu.AddItem(new GUIContent("Reverb/Freeverb"), false, () => AddEffectInstance(new FDNReverb()));
            menu.AddItem(new GUIContent("Pitch/Pitch Shifter"), false, () => AddEffectInstance(new PitchShift()));
            menu.AddItem(new GUIContent("Saturation/Tube"), false, () => AddEffectInstance(new TubeEmulation()));
            menu.ShowAsContext();
        }

        private void AddEffectInstance(IDSPEffect effect)
        {
            if (_target == null || effect == null) return;
            _target.AddEffect(effect);
            RebuildLinearConnections();
            SyncRuntimeChain();
            EditorUtility.SetDirty(_target);
            _visualizerContainer?.MarkDirtyRepaint();
        }


        private void StripLegacyEqEffects()
        {
            if (_target == null) return;

            bool removed = false;
            foreach (var effect in _target.Effects.ToList())
            {
                if (effect is EQEffect)
                {
                    _target.RemoveEffect(effect);
                    removed = true;
                }
            }

            if (removed)
            {
                RebuildLinearConnections();
                SyncRuntimeChain();
                EditorUtility.SetDirty(_target);
                _visualizerContainer?.MarkDirtyRepaint();
            }
        }


        private void DrawEffectModuleUI(IDSPEffect effect, int index, int listCount)
        {
            const float rowHeight = 44f; // Increased from 36 to 44 for better visibility
            Rect rowRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(rowHeight), GUILayout.ExpandWidth(true));
            
            // Background
            EditorGUI.DrawRect(rowRect, new Color(0.12f, 0.12f, 0.15f, 0.6f));
            DrawRectOutline(rowRect, new Color(0.3f, 0.3f, 0.35f, 0.4f));
            
            // Vertical line on left
            float lineX = rowRect.x + 12f;
            EditorGUI.DrawRect(new Rect(lineX, rowRect.y, 2f, rowRect.height), new Color(VarcoEditorStyles.Mint.r, VarcoEditorStyles.Mint.g, VarcoEditorStyles.Mint.b, 0.35f));
            
            // Connection dot
            Handles.BeginGUI();
            Handles.color = VarcoEditorStyles.Mint;
            Handles.DrawSolidDisc(new Vector3(lineX + 1f, rowRect.center.y, 0), Vector3.forward, 4f);
            Handles.EndGUI();
            
            // Content area
            float contentX = rowRect.x + 26f;
            
            // Effect name
            GUI.Label(new Rect(contentX, rowRect.y + 6f, 160f, 18f), effect.Name, EditorStyles.boldLabel);
            
            // Effect type
            GUI.Label(new Rect(contentX + 160f, rowRect.y + 8f, 120f, 16f),
                effect.GetType().Name.Replace("Effect", "").Replace("FDN", ""), EditorStyles.miniLabel);
            
            // Button layout from right to left
            float right = rowRect.xMax - 6f;
            
            // X button (inline, prominent)
            Rect xButtonRect = new Rect(right - 28f, rowRect.y + (rowRect.height - 32f) / 2f, 26f, 32f);
            right -= 32f;
            
            // Original remove button (keeping for backwards compatibility)
            Rect removeRect = new Rect(right - 24f, rowRect.y + 11f, 22f, 18f);
            right -= 28f;
            
            // ON/OFF toggle
            Rect toggleRect = new Rect(right - 38f, rowRect.y + 11f, 36f, 18f);
            right -= 42f;
            
            // Down button
            Rect downRect = new Rect(right - 24f, rowRect.y + 11f, 22f, 18f);
            right -= 28f;
            
            // Up button
            Rect upRect = new Rect(right - 24f, rowRect.y + 11f, 22f, 18f);
            
            // Draw buttons
            if (index > 0 && GUI.Button(upRect, "↑", EditorStyles.miniButton))
            {
                ReorderEffect(index, index - 1);
                return;
            }
            if (index < listCount - 1 && GUI.Button(downRect, "↓", EditorStyles.miniButton))
            {
                ReorderEffect(index, index + 1);
                return;
            }
            
            // ON/OFF toggle
            bool newEnabled = GUI.Toggle(toggleRect, effect.Enabled, effect.Enabled ? "ON" : "OFF", EditorStyles.miniButton);
            if (newEnabled != effect.Enabled)
            {
                effect.Enabled = newEnabled;
                EditorUtility.SetDirty(_target);
            }
            
            // Prominent X button with red color
            GUI.backgroundColor = new Color(1f, 0.3f, 0.3f);
            if (GUI.Button(xButtonRect, "✕", new GUIStyle(EditorStyles.miniButton) { fontSize = 16, fontStyle = FontStyle.Bold }))
            {
                _target?.RemoveEffect(effect);
                SyncRuntimeChain();
                EditorUtility.SetDirty(_target);
                return;
            }
            GUI.backgroundColor = Color.white;
            
            DrawSimpleEffectProperties(effect);
            GUILayout.Space(4);
        }


        private bool TryGetFloatRange(string name, out float min, out float max)
        {
            min = 0f;
            max = 1f;

            switch (name)
            {
                case "Mix":
                case "Wet":
                case "Dry":
                    min = 0f; max = 1f; return true;
                case "CutoffFrequency":
                case "Frequency":
                    min = 20f; max = 20000f; return true;
                case "Resonance":
                case "Q":
                    min = 0.1f; max = 10f; return true;
                case "DelayTime":
                case "DelayMs":
                case "PreDelay":
                case "PreDelayMs":
                    min = 0f; max = 2000f; return true;
                case "Rate":
                    min = 0.01f; max = 10f; return true;
                case "Depth":
                    min = 0f; max = 50f; return true;
                case "Gain":
                case "OutputGain":
                case "Drive":
                    min = -24f; max = 24f; return true;
                case "Bass":
                case "LowMid":
                case "Mid":
                case "HighMid":
                case "Treble":
                    min = -12f; max = 12f; return true;
                case "Semitones":
                    min = -12f; max = 12f; return true;
                case "DecayTime":
                    min = 0.1f; max = 10f; return true;
            }

            if (name.IndexOf("Frequency", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Cutoff", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Hz", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                min = 20f; max = 20000f; return true;
            }

            if (name.IndexOf("Resonance", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Quality", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                min = 0.1f; max = 10f; return true;
            }

            if (name.IndexOf("Delay", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                min = 0f; max = 2000f; return true;
            }

            if (name.IndexOf("Rate", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                min = 0.01f; max = 10f; return true;
            }

            if (name.IndexOf("Depth", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                min = 0f; max = 50f; return true;
            }

            if (name.IndexOf("Gain", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                min = -24f; max = 24f; return true;
            }

            return false;
        }


        private bool TryGetIntRange(string name, out int min, out int max)
        {
            min = 0;
            max = 10;

            switch (name)
            {
                case "Voices":
                    min = 1; max = 8; return true;
                case "Taps":
                    min = 1; max = 16; return true;
            }

            return false;
        }


        private void DrawSimpleEffectProperties(IDSPEffect effect)
        {
            var props = effect.GetType().GetProperties()
                .Where(p => p.PropertyType == typeof(float) || p.PropertyType == typeof(int) || p.PropertyType == typeof(bool))
                .Where(p => p.Name != "Enabled" && p.Name != "Name" && p.DeclaringType != typeof(Object))
                .Take(2).ToList();
            if (props.Count > 0)
            {
                GUILayout.Space(4);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(28);
                foreach (var prop in props)
                {
                    if (prop.PropertyType == typeof(float))
                    {
                        float val = (float)prop.GetValue(effect);
                        float nextVal = val;
                        if (TryGetFloatRange(prop.Name, out float min, out float max) && val >= min && val <= max)
                        {
                            nextVal = EditorGUILayout.Slider(prop.Name, val, min, max, GUILayout.Width(200));
                        }
                        else
                        {
                            nextVal = EditorGUILayout.FloatField(prop.Name, val, GUILayout.Width(200));
                        }

                        if (Math.Abs(nextVal - val) > 0.001f && !float.IsNaN(nextVal) && !float.IsInfinity(nextVal))
                        {
                            prop.SetValue(effect, nextVal);
                            EditorUtility.SetDirty(_target);
                        }
                    }
                    else if (prop.PropertyType == typeof(bool))
                    {
                        bool val = (bool)prop.GetValue(effect);
                        bool nextVal = EditorGUILayout.Toggle(prop.Name, val, GUILayout.Width(120));
                        if (nextVal != val)
                        {
                            prop.SetValue(effect, nextVal);
                            EditorUtility.SetDirty(_target);
                        }
                    }
                    else if (prop.PropertyType == typeof(int))
                    {
                        int val = (int)prop.GetValue(effect);
                        int nextVal = val;
                        if (TryGetIntRange(prop.Name, out int minInt, out int maxInt) && val >= minInt && val <= maxInt)
                        {
                            nextVal = EditorGUILayout.IntSlider(prop.Name, val, minInt, maxInt, GUILayout.Width(200));
                        }
                        else
                        {
                            nextVal = EditorGUILayout.IntField(prop.Name, val, GUILayout.Width(200));
                        }

                        if (nextVal != val)
                        {
                            prop.SetValue(effect, nextVal);
                            EditorUtility.SetDirty(_target);
                        }
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        
        private void DrawEffectDetailsSection()
        {
            if (_selectedEffectIndex < 0 || _target == null || _selectedEffectIndex >= _target.Effects.Count) return;
            
            var effect = _target.Effects[_selectedEffectIndex];
            VarcoEditorStyles.DrawSectionHeader($"Effect Properties: {effect.Name}");
            
            EditorGUILayout.BeginVertical(VarcoEditorStyles.CardStyle);
            EditorGUILayout.LabelField("Type:", effect.GetType().Name);
            bool newEnabled = EditorGUILayout.Toggle("Enabled:", effect.Enabled);
            if (newEnabled != effect.Enabled) { effect.Enabled = newEnabled; EditorUtility.SetDirty(_target); }
            
            EditorGUILayout.Space(5);
            DrawEffectProperties(effect);
            EditorGUILayout.Space(5);
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Reset")) { effect.Reset(); EditorUtility.SetDirty(_target); }
            if (GUILayout.Button("Remove")) { _target.RemoveEffectAt(_selectedEffectIndex); _selectedEffectIndex = -1; EditorUtility.SetDirty(_target); }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
        }


        private void DrawEffectProperties(IDSPEffect effect) {
            var type = effect.GetType();
            var properties = type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            foreach (var prop in properties) {
                if (!prop.CanRead || !prop.CanWrite || prop.Name == "Name" || prop.Name == "Enabled") continue;
                var value = prop.GetValue(effect);
                
                if (prop.PropertyType == typeof(float)) {
                    float val = (float)value;
                    // Check for Range attribute logic if needed, but for now simple float field
                    float newVal = EditorGUILayout.FloatField(prop.Name, val);
                    if (!Mathf.Approximately(newVal, val)) { prop.SetValue(effect, newVal); EditorUtility.SetDirty(_target); }
                } else if (prop.PropertyType == typeof(int)) {
                    int newVal = EditorGUILayout.IntField(prop.Name, (int)value);
                    if (newVal != (int)value) { prop.SetValue(effect, newVal); EditorUtility.SetDirty(_target); }
                } else if (prop.PropertyType == typeof(bool)) {
                    bool newVal = EditorGUILayout.Toggle(prop.Name, (bool)value);
                    if (newVal != (bool)value) { prop.SetValue(effect, newVal); EditorUtility.SetDirty(_target); }
                } else if (prop.PropertyType.IsEnum) {
                    Enum newVal = EditorGUILayout.EnumPopup(prop.Name, (Enum)value);
                    if (!newVal.Equals(value)) { prop.SetValue(effect, newVal); EditorUtility.SetDirty(_target); }
                }
            }
        }

        
        private void DrawPresetsSection()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Quick Presets:", GUILayout.Width(90));
            GUI.enabled = _target != null;
            
            if (VarcoEditorStyles.DrawPillButton("?쨼 Robot", false)) { _target?.ApplyRobotVoice(); EditorUtility.SetDirty(_target); }
            if (VarcoEditorStyles.DrawPillButton("?벢 Radio", false)) { _target?.ApplyRadioVoice(); EditorUtility.SetDirty(_target); }
            if (VarcoEditorStyles.DrawPillButton("?쫯 Cave", false)) { _target?.ApplyCaveVoice(); EditorUtility.SetDirty(_target); }
            if (VarcoEditorStyles.DrawPillButton("?뙄 Underwater", false)) { _target?.ApplyUnderwaterVoice(); EditorUtility.SetDirty(_target); }
            if (VarcoEditorStyles.DrawPillButton("?뫛 Ghost", false)) { _target?.ApplyGhostVoice(); EditorUtility.SetDirty(_target); }
            
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Clear All", GUILayout.Width(70))) { _target?.ClearEffects(); _selectedEffectIndex = -1; EditorUtility.SetDirty(_target); }
            
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
        }
    }
}
