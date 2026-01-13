using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VARCOVoice.DSP;

namespace VARCOVoice.Editor
{
    /// <summary>
    /// Main controller for the Effect Stack Panel (Master-Detail layout).
    /// Coordinates EffectPills, EffectDetailPanel, and Preset management.
    /// </summary>
    public class EffectStackPanelController
    {
        private VisualElement _root;
        private DSPChain _chain;
        
        // UI References
        private VisualElement _pillList;
        private VisualElement _detailContainer;
        private Button _savePresetBtn;
        private DropdownField _loadPresetDropdown;
        private Button _clearBtn;
        private Button _addFxBtn;

        private readonly Dictionary<string, DSPChainPresetLibrary.ChainPreset> _builtInPresets = new();
        private const string PresetPlaceholder = "Load Preset...";
        private readonly Stack<DSPPreset> _undoStack = new();
        private readonly Stack<DSPPreset> _redoStack = new();
        private bool _isRestoringUndo;
        private bool _editSessionActive;

        // Controllers
        private List<EffectPillController> _pillControllers = new List<EffectPillController>();
        private EffectDetailController _detailController;

        // Templates
        private VisualTreeAsset _pillTemplate;
        private VisualTreeAsset _detailPanelTemplate;

        // State
        private IDSPEffect _selectedEffect;
        private string _currentPresetName = null;  // Track current loaded preset
        private EffectPillController _draggingPill;
        private int _draggingIndex = -1;
        private int _draggingPointerId = -1;
        
        // Events
        public event Action OnChainModified;

        public void Initialize(VisualElement root, DSPChain chain)
        {
            _root = root;
            _chain = chain;
            
            LoadTemplates();
            QueryElements();
            SetupCallbacks();
            RebuildPillList();
            RefreshPresetDropdown();
        }

        private void LoadTemplates()
        {
            // Package paths for UI Toolkit assets
            const string PKG = "Packages/com.varco.voice/Editor/UI/Components/";
            
            _pillTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(PKG + "EffectPill.uxml");
            _detailPanelTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(PKG + "EffectDetailPanel.uxml");
            
            // Also load stylesheets
            var pillStyle = AssetDatabase.LoadAssetAtPath<StyleSheet>(PKG + "EffectPill.uss");
            var detailStyle = AssetDatabase.LoadAssetAtPath<StyleSheet>(PKG + "EffectDetailPanel.uss");
            
            if (pillStyle != null) _root.styleSheets.Add(pillStyle);
            if (detailStyle != null) _root.styleSheets.Add(detailStyle);
        }

        private void QueryElements()
        {
            _pillList = _root.Q<VisualElement>("pill-list");
            _detailContainer = _root.Q<VisualElement>("detail-panel-container");
            _savePresetBtn = _root.Q<Button>("save-preset-btn");
            _loadPresetDropdown = _root.Q<DropdownField>("load-preset-dropdown");
            _clearBtn = _root.Q<Button>("clear-btn");
            _addFxBtn = _root.Q<Button>("add-fx-btn");
            
            // Setup detail panel
            if (_detailPanelTemplate != null && _detailContainer != null)
            {
                var detailPanel = _detailPanelTemplate.Instantiate();
                detailPanel.style.flexGrow = 1;
                detailPanel.style.flexShrink = 0;
                detailPanel.style.width = Length.Percent(100);
                detailPanel.style.height = Length.Percent(100);
                _detailContainer.Add(detailPanel);
                
                _detailController = new EffectDetailController();
                _detailController.Initialize(detailPanel, _chain);
                _detailController.OnEffectChanged += () =>
                {
                    RefreshPillStates();
                    OnChainModified?.Invoke();
                };
                _detailController.OnEditSessionBegin += BeginEditSession;
                _detailController.OnEditSessionEnd += EndEditSession;
            }
        }

        private void SetupCallbacks()
        {
            // Add FX Button
            _addFxBtn?.RegisterCallback<ClickEvent>(evt => ShowAddEffectMenu());

            // Clear Button
            _clearBtn?.RegisterCallback<ClickEvent>(evt =>
            {
                if (_chain == null) return;
                PushUndoSnapshot();
                _chain.ClearEffects();
                _selectedEffect = null;
                _currentPresetName = null;  // Reset preset name
                RebuildPillList();
                _detailController?.Clear();
                RefreshPresetDropdown();  // Reset dropdown to placeholder
                EditorUtility.SetDirty(_chain);
                OnChainModified?.Invoke();
            });
            
            // Save Preset
            _savePresetBtn?.RegisterCallback<ClickEvent>(evt => ShowSavePresetDialog());
            
            // Load Preset Dropdown
            _loadPresetDropdown?.RegisterValueChangedCallback(evt =>
            {
                if (string.IsNullOrEmpty(evt.newValue) || evt.newValue == PresetPlaceholder) return;

                bool loaded = false;
                if (_builtInPresets.TryGetValue(evt.newValue, out var builtIn) && _chain != null)
                {
                    PushUndoSnapshot();
                    DSPChainPresetLibrary.ApplyPreset(_chain, builtIn);
                    RebuildPillList();
                    _detailController?.Clear();
                    EditorUtility.SetDirty(_chain);
                    OnChainModified?.Invoke();
                    loaded = true;
                }
                else
                {
                    var presets = DSPPresetManager.GetAllPresets();
                    var selected = presets.FirstOrDefault(p => p.PresetName == evt.newValue);
                    if (selected != null && _chain != null)
                    {
                        PushUndoSnapshot();
                        DSPPresetManager.LoadPreset(selected, _chain);
                        RebuildPillList();
                        _detailController?.Clear();
                        OnChainModified?.Invoke();
                        loaded = true;
                    }
                }

                // Keep showing selected preset name
                if (loaded)
                {
                    _currentPresetName = evt.newValue;
                    
                    // Feature: Auto-select EQ if present in the preset
                    // This allows the user to see the EQ curve immediately
                    var eqEffect = _chain.Effects.LastOrDefault(e => e is ParametricEQ16);
                    if (eqEffect != null)
                    {
                        SelectEffect(eqEffect);
                    }
                }
            });
        }

        public void RebuildPillList()
        {
            if (_pillList == null || _chain == null) return;
            
            // Clear existing
            foreach (var ctrl in _pillControllers)
                ctrl.Destroy();
            _pillControllers.Clear();
            _pillList.Clear();
            
            // Get effects
            var effects = _chain.Effects.ToList();
            
            // Create pills
            foreach (var effect in effects)
            {
                if (_pillTemplate == null) continue;
                
                var pillElement = _pillTemplate.Instantiate();
                var controller = new EffectPillController();
                controller.Initialize(pillElement, effect, _chain);

                controller.OnSelected += SelectEffect;
                controller.OnRemoved += RemoveEffect;
                RegisterDragHandlers(controller);

                _pillControllers.Add(controller);
                _pillList.Add(pillElement);
            }
            
            // Update selection
            RefreshPillStates();
        }

        private void SelectEffect(IDSPEffect effect)
        {
            _selectedEffect = effect;
            _detailController?.DisplayEffect(effect);
            RefreshPillStates();
        }

        private void RemoveEffect(IDSPEffect effect)
        {
            if (_chain == null || effect == null) return;

            PushUndoSnapshot();
            _chain.RemoveEffect(effect);
            
            if (_selectedEffect == effect)
            {
                _selectedEffect = null;
                _detailController?.Clear();
            }
            
            RebuildPillList();
            EditorUtility.SetDirty(_chain);
            OnChainModified?.Invoke();
        }

        private void RefreshPillStates()
        {
            foreach (var ctrl in _pillControllers)
            {
                ctrl.SetSelected(ctrl.Effect == _selectedEffect);
                ctrl.UpdateStatus();
            }
        }

        private void RegisterDragHandlers(EffectPillController controller)
        {
            if (controller?.DragHandle == null || controller.Root == null) return;

            controller.DragHandle.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0) return;
                BeginDrag(controller, evt);
            });
            controller.Root.RegisterCallback<PointerMoveEvent>(UpdateDrag);
            controller.Root.RegisterCallback<PointerUpEvent>(EndDrag);
        }

        private void BeginDrag(EffectPillController controller, PointerDownEvent evt)
        {
            if (_pillList == null || controller == null) return;

            _draggingPill = controller;
            _draggingIndex = _pillControllers.IndexOf(controller);
            _draggingPointerId = evt.pointerId;
            controller.Root.AddToClassList("dragging");
            controller.Root.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void UpdateDrag(PointerMoveEvent evt)
        {
            if (_draggingPill == null || _draggingPointerId < 0) return;
            if (!_draggingPill.Root.HasPointerCapture(_draggingPointerId)) return;
        }

        private void EndDrag(PointerUpEvent evt)
        {
            if (_draggingPill == null) return;
            if (_draggingPill.Root.HasPointerCapture(_draggingPointerId))
                _draggingPill.Root.ReleasePointer(_draggingPointerId);

            _draggingPill.Root.RemoveFromClassList("dragging");

            int targetIndex = GetDropIndex(evt.position);
            if (targetIndex >= 0 && targetIndex != _draggingIndex)
            {
                ReorderEffects(_draggingIndex, targetIndex);
            }

            _draggingPill = null;
            _draggingIndex = -1;
            _draggingPointerId = -1;
            evt.StopPropagation();
        }

        private int GetDropIndex(Vector2 worldPosition)
        {
            if (_pillControllers.Count == 0) return -1;

            for (int i = 0; i < _pillControllers.Count; i++)
            {
                var bound = _pillControllers[i].Root.worldBound;
                if (worldPosition.y < bound.center.y)
                    return i;
            }

            return _pillControllers.Count - 1;
        }

        private void ReorderEffects(int fromIndex, int toIndex)
        {
            if (_chain == null) return;
            if (fromIndex < 0 || toIndex < 0) return;

            PushUndoSnapshot();
            var full = _chain.Effects.ToList();
            var nonEq = full.Where(e => !IsEqEffect(e)).ToList();

            if (fromIndex >= nonEq.Count || toIndex >= nonEq.Count) return;

            var moving = nonEq[fromIndex];
            nonEq.RemoveAt(fromIndex);
            nonEq.Insert(toIndex, moving);

            var reordered = new List<IDSPEffect>(full.Count);
            int nonEqIndex = 0;
            for (int i = 0; i < full.Count; i++)
            {
                var effect = full[i];
                if (IsEqEffect(effect))
                {
                    reordered.Add(effect);
                }
                else
                {
                    reordered.Add(nonEq[nonEqIndex]);
                    nonEqIndex++;
                }
            }

            _chain.SetEffects(reordered);
            EditorUtility.SetDirty(_chain);

            var selected = _selectedEffect;
            RebuildPillList();
            if (selected != null && _chain.Effects.Contains(selected))
                SelectEffect(selected);
            else
                _detailController?.Clear();

            OnChainModified?.Invoke();
        }

        private static bool IsEqEffect(IDSPEffect effect)
        {
            return effect is ParametricEQ16 || effect is EQEffect;
        }

        private void ShowAddEffectMenu()
        {
            if (_chain == null) return;

            var menu = new GenericMenu();

            menu.AddItem(new GUIContent("Filter/Parametric EQ"), false, () => AddEffect(new ParametricEQ16()));
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Dynamics"), false, () => AddEffect(new UnifiedDynamics()));
            menu.AddItem(new GUIContent("Delay"), false, () => AddEffect(new UnifiedDelay()));
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Distortion/Distortion"), false, () => AddEffect(new DistortionEffect()));
            menu.AddItem(new GUIContent("Distortion/Saturation"), false, () => AddEffect(new SaturationEffect()));
            menu.AddItem(new GUIContent("Distortion/Tape"), false, () => AddEffect(new TapeEmulation()));
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Modulation/Chorus"), false, () => AddEffect(new ChorusEffect()));
            menu.AddItem(new GUIContent("Modulation/Phaser"), false, () => AddEffect(new PhaserEffect()));
            menu.AddItem(new GUIContent("Modulation/Flanger"), false, () => AddEffect(new FlangerEffect()));
            menu.AddItem(new GUIContent("Modulation/Tremolo"), false, () => AddEffect(new TremoloEffect()));
            menu.AddItem(new GUIContent("Modulation/Ring Mod"), false, () => AddEffect(new RingModulatorEffect()));
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Reverb"), false, () => AddEffect(new FDNReverb()));
            menu.AddItem(new GUIContent("Pitch Shift"), false, () => AddEffect(new WSOLAPitchShift()));
            menu.AddItem(new GUIContent("Analog/Tube"), false, () => AddEffect(new TubeEmulation()));
            menu.AddItem(new GUIContent("Spatial/3D Spatial"), false, () => AddEffect(new Spatial3DEffect()));

            menu.ShowAsContext();
        }

        private void AddEffect(IDSPEffect effect)
        {
            if (_chain == null || effect == null) return;

            PushUndoSnapshot();
            _chain.AddEffect(effect);
            RebuildPillList();
            SelectEffect(effect); // Auto-select new effect
            EditorUtility.SetDirty(_chain);
            OnChainModified?.Invoke();
        }

        public void Undo()
        {
            if (_chain == null || _undoStack.Count == 0) return;

            var current = CaptureSnapshot();
            var target = _undoStack.Pop();
            _redoStack.Push(current);

            _isRestoringUndo = true;
            target.ApplyToChain(_chain);
            _isRestoringUndo = false;
            _editSessionActive = false;

            _selectedEffect = null;
            RebuildPillList();
            _detailController?.Clear();
            RefreshPresetDropdown();
            EditorUtility.SetDirty(_chain);
            OnChainModified?.Invoke();
        }

        public void Redo()
        {
            if (_chain == null || _redoStack.Count == 0) return;

            var current = CaptureSnapshot();
            var target = _redoStack.Pop();
            _undoStack.Push(current);

            _isRestoringUndo = true;
            target.ApplyToChain(_chain);
            _isRestoringUndo = false;
            _editSessionActive = false;

            _selectedEffect = null;
            RebuildPillList();
            _detailController?.Clear();
            RefreshPresetDropdown();
            EditorUtility.SetDirty(_chain);
            OnChainModified?.Invoke();
        }

        private void BeginEditSession()
        {
            if (_editSessionActive || _isRestoringUndo) return;
            PushUndoSnapshot();
            _editSessionActive = true;
        }

        private void EndEditSession()
        {
            _editSessionActive = false;
        }

        private void PushUndoSnapshot()
        {
            if (_chain == null || _isRestoringUndo) return;
            _undoStack.Push(CaptureSnapshot());
            _redoStack.Clear();
        }

        private DSPPreset CaptureSnapshot()
        {
            var preset = ScriptableObject.CreateInstance<DSPPreset>();
            preset.CaptureFromChain(_chain);
            return preset;
        }

        private void ShowSavePresetDialog()
        {
            if (_chain == null) return;
            
            // Simple input dialog
            var name = EditorInputDialog.Show("Save Preset", "Enter preset name:", "My Preset");
            if (!string.IsNullOrEmpty(name))
            {
                DSPPresetManager.SavePreset(_chain, name);
                RefreshPresetDropdown();
            }
        }

        private void RefreshPresetDropdown()
        {
            if (_loadPresetDropdown == null) return;

            _builtInPresets.Clear();
            var names = new List<string> { PresetPlaceholder };

            foreach (var preset in DSPChainPresetLibrary.GetPresets())
            {
                var displayName = $"[Built-in] {preset.DisplayName}";
                _builtInPresets[displayName] = preset;
                names.Add(displayName);
            }

            names.AddRange(DSPPresetManager.GetPresetNames());

            _loadPresetDropdown.choices = names;
            _loadPresetDropdown.SetValueWithoutNotify(PresetPlaceholder);
        }

        public void Refresh()
        {
            RebuildPillList();
            RefreshPresetDropdown();
            
            if (_selectedEffect != null && _chain?.Effects.Contains(_selectedEffect) == true)
                _detailController?.DisplayEffect(_selectedEffect);
            else
                _detailController?.Clear();
        }
    }

    /// <summary>
    /// Simple input dialog utility.
    /// </summary>
    public static class EditorInputDialog
    {
        public static string Show(string title, string message, string defaultValue)
        {
            // For now, use a simple prompt. In production, create a proper EditorWindow.
            return EditorUtility.SaveFilePanel(title, "", defaultValue, "") 
                   is { } path && !string.IsNullOrEmpty(path) 
                ? System.IO.Path.GetFileNameWithoutExtension(path) 
                : null;
        }
    }
}
