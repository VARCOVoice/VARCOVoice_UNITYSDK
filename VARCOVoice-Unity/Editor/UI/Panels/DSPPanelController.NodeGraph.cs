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

        private void DrawNodeGraphUI()
        {
            // Canvas for node graph
            Rect chainRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(280));
            if (chainRect.width < 10) return; // Layout not ready
            
            float nodeWidth = 100f;
            float nodeHeight = 65f;
            float gridSize = 20f;
            float portSize = 10f;
            
            _lastCanvasRect = chainRect;
            Vector2 canvasOrigin = chainRect.position;
            
            // Background
            EditorGUI.DrawRect(chainRect, VarcoEditorStyles.BackgroundDark);
            
            // Grid dots (local coordinates)
            if (Event.current.type == EventType.Repaint)
            {
                Handles.BeginGUI();
                // Draw background grid (Dotted pattern)
                Handles.color = new Color(1, 1, 1, 0.08f);
                gridSize = 25f;
                for (float x = 0; x < chainRect.width; x += gridSize)
                {
                    for (float y = 0; y < chainRect.height; y += gridSize)
                    {
                        Handles.DrawSolidRectangleWithOutline(new Rect(chainRect.x + x - 1, chainRect.y + y - 1, 2, 2), new Color(1, 1, 1, 0.15f), Color.clear);
                    }
                }
                
                // Connections (converted to world for Handles)
                foreach (var conn in _connections)
                {
                    Vector2 fromWorld = canvasOrigin + GetNodeOutputPortPos(conn.FromEffect, nodeWidth, nodeHeight);
                    Vector2 toWorld = canvasOrigin + GetNodeInputPortPos(conn.ToEffect, nodeWidth, nodeHeight);
                    DrawBezierWireInternal(fromWorld, toWorld, true);
                }
                
                if (_isCreatingWire)
                {
                    Vector2 startLocal = _wireFromOutput ? 
                        GetNodeOutputPortPos(_wireStartEffect, nodeWidth, nodeHeight) :
                        GetNodeInputPortPos(_wireStartEffect, nodeWidth, nodeHeight);
                    DrawBezierWireInternal(canvasOrigin + startLocal, _wireDragEndPos, true);
                }
                Handles.EndGUI();
            }
            
            // Get effects (filter out EQ)
            var allEffects = _target?.Effects ?? new List<IDSPEffect>();
            var effects = allEffects.Where(eff => !(eff is ParametricEQ16)).ToList();
            
            // Fixed Input/Output positions (Stored as LOCAL)
            float localCenterY = (chainRect.height - nodeHeight) / 2;
            _inputNodePos = new Vector2(30, localCenterY);
            _outputNodePos = new Vector2(chainRect.width - nodeWidth - 30, localCenterY);
            
            // Initialize/Clamp LOCAL positions
            for (int i = 0; i < effects.Count; i++)
            {
                Vector2 localPos;
                if (!_effectPositions.ContainsKey(effects[i]))
                {
                    // New effect: use relative spawn or calculate default
                    if (_nextSpawnPos != Vector2.zero)
                        localPos = _nextSpawnPos - canvasOrigin;
                    else
                        localPos = new Vector2(150 + i * 120, localCenterY);
                    
                    _nextSpawnPos = Vector2.zero;
                }
                else
                {
                    localPos = _effectPositions[effects[i]];
                }
                
                // ALWAYS clamp to LOCAL canvas bounds
                localPos.x = Mathf.Clamp(localPos.x, 10, chainRect.width - nodeWidth - 10);
                localPos.y = Mathf.Clamp(localPos.y, 10, chainRect.height - nodeHeight - 10);
                _effectPositions[effects[i]] = localPos;
            }
            
            // Cleanup old effects
            var toRemove = _effectPositions.Keys.Where(eff => !effects.Contains(eff)).ToList();
            foreach (var eff in toRemove) _effectPositions.Remove(eff);
            
            // Auto-create linear connections if empty
            if (_connections.Count == 0)
            {
                if (effects.Count > 0)
                {
                    _connections.Add(new NodeConnection { FromEffect = null, ToEffect = effects[0] });
                    for (int i = 0; i < effects.Count - 1; i++)
                        _connections.Add(new NodeConnection { FromEffect = effects[i], ToEffect = effects[i + 1] });
                    _connections.Add(new NodeConnection { FromEffect = effects[effects.Count - 1], ToEffect = null });
                }
                else
                {
                    _connections.Add(new NodeConnection { FromEffect = null, ToEffect = null });
                }
            }
            
            // INTERACTION HANDLING
            Event e = Event.current;
            Vector2 mousePos = e.mousePosition;
            Vector2 localMousePos = mousePos - canvasOrigin;
            bool inCanvas = chainRect.Contains(mousePos);
            
            // Update wire drag position
            if (_isCreatingWire && (e.type == EventType.MouseMove || e.type == EventType.MouseDrag))
            {
                _wireDragEndPos = mousePos;
                _visualizerContainer?.MarkDirtyRepaint();
            }
            
            // Mouse Down
            if (e.type == EventType.MouseDown && inCanvas && !_showNodeAddMenu)
            {
                if (e.button == 1) // Right-click
                {
                    if (_isCreatingWire) _isCreatingWire = false;
                    else
                    {
                        _showNodeAddMenu = true;
                        _nodeAddMenuPos = mousePos;
                        _nextSpawnPos = mousePos; 
                    }
                    e.Use();
                    _visualizerContainer?.MarkDirtyRepaint();
                }
                else if (e.button == 0) // Left-click
                {
                    bool handled = false;
                    if (_isCreatingWire)
                    {
                        handled = TryCompleteWireConnection(effects, mousePos, localMousePos, nodeWidth, nodeHeight, portSize);
                        if (!handled) _isCreatingWire = false;
                        e.Use();
                        _visualizerContainer?.MarkDirtyRepaint();
                    }
                    else
                    {
                        if (TryStartWireFromPort(effects, mousePos, localMousePos, nodeWidth, nodeHeight, portSize))
                        {
                            handled = true;
                            e.Use();
                            _visualizerContainer?.MarkDirtyRepaint();
                        }
                        
                        if (!handled)
                        {
                            foreach (var effect in effects)
                            {
                                if (_effectPositions.TryGetValue(effect, out Vector2 lPos))
                                {
                                    if (new Rect(lPos.x, lPos.y, nodeWidth, nodeHeight).Contains(localMousePos))
                                    {
                                        _selectedEffect = effect;
                                        _draggingEffect = effect;
                                        _dragOffset = localMousePos - lPos;
                                        e.Use();
                                        _visualizerContainer?.MarkDirtyRepaint();
                                        handled = true;
                                        break;
                                    }
                                }
                            }
                        }
                        
                        if (!handled) TryDeleteWireAtPoint(mousePos, localMousePos, nodeWidth, nodeHeight);
                    }
                }
            }
            
            // Mouse Drag
            if (e.type == EventType.MouseDrag && _draggingEffect != null)
            {
                if (_effectPositions.ContainsKey(_draggingEffect))
                {
                    Vector2 newLocalPos = localMousePos - _dragOffset;
                    newLocalPos.x = Mathf.Clamp(newLocalPos.x, 5, chainRect.width - nodeWidth - 5);
                    newLocalPos.y = Mathf.Clamp(newLocalPos.y, 5, chainRect.height - nodeHeight - 5);
                    _effectPositions[_draggingEffect] = newLocalPos;
                    e.Use();
                    _visualizerContainer?.MarkDirtyRepaint();
                }
            }
            
            // Mouse Up
            if (e.type == EventType.MouseUp && e.button == 0) _draggingEffect = null;
            
            // DRAWING
            if (Event.current.type == EventType.Repaint)
            {
                // DRAWING ORDER: 
                // 1. GRID (Repaint loop, Handles block)
                // 2. WIRES (Repaint loop, Handles block)
                // 3. NODES (Repaint loop)
                // 4. POPUPS (Last)

                // 2. Wires Draw Pass
                Handles.BeginGUI();
                foreach (var conn in _connections)
                {
                    Vector2 fromWorld = canvasOrigin + GetNodeOutputPortPos(conn.FromEffect, nodeWidth, nodeHeight);
                    Vector2 toWorld = canvasOrigin + GetNodeInputPortPos(conn.ToEffect, nodeWidth, nodeHeight);
                    DrawBezierWireInternal(fromWorld, toWorld, true);
                }
                
                if (_isCreatingWire)
                {
                    Vector2 startLocal = _wireFromOutput ? 
                        GetNodeOutputPortPos(_wireStartEffect, nodeWidth, nodeHeight) :
                        GetNodeInputPortPos(_wireStartEffect, nodeWidth, nodeHeight);
                    DrawBezierWireInternal(canvasOrigin + startLocal, _wireDragEndPos, true);
                }
                Handles.EndGUI();
                
                // 3. Nodes Draw Pass
                Vector2 inNodeWorld = canvasOrigin + _inputNodePos;
                DrawGraphNodeNew(new Rect(inNodeWorld.x, inNodeWorld.y, nodeWidth, nodeHeight),
                    "Input", "?렎", VarcoEditorStyles.Mint, true, null, false);
                
                foreach (var effect in effects)
                {
                    Vector2 lPos = _effectPositions.GetValueOrDefault(effect, Vector2.zero);
                    Vector2 worldPos = canvasOrigin + lPos;
                    bool isSelected = effect == _selectedEffect;
                    bool isConnected = IsEffectConnected(effect);
                    Color headerColor = GetEffectHeaderColor(effect);
                    if (!isConnected) headerColor.a = 0.5f;

                    DrawGraphNodeNew(new Rect(worldPos.x, worldPos.y, nodeWidth, nodeHeight),
                        effect.Name, GetEffectIcon(effect), headerColor, isConnected, effect, isSelected);
                }
                
                Vector2 outNodeWorld = canvasOrigin + _outputNodePos;
                DrawGraphNodeNew(new Rect(outNodeWorld.x, outNodeWorld.y, nodeWidth, nodeHeight),
                    "Output", "?뵄", VarcoEditorStyles.Purple, true, null, false);

                // 4. Node Ports Pass (Top Layer)
                Handles.BeginGUI();
                DrawNodePortCircular(new Rect(inNodeWorld.x, inNodeWorld.y, nodeWidth, nodeHeight), true, Color.white, false, true); // hideInput = true
                foreach (var effect in effects)
                {
                    Vector2 worldPos = canvasOrigin + _effectPositions.GetValueOrDefault(effect, Vector2.zero);
                    bool isConnected = IsEffectConnected(effect);
                    DrawNodePortCircular(new Rect(worldPos.x, worldPos.y, nodeWidth, nodeHeight), isConnected, Color.white, false, false);
                }
                DrawNodePortCircular(new Rect(outNodeWorld.x, outNodeWorld.y, nodeWidth, nodeHeight), true, Color.white, true, false); // hideOutput = true
                Handles.EndGUI();
            }
            
            // Popups (always outside Repaint block if they use IMGUI GUI calls, but here they are custom methods)
            if (_selectedEffect != null) DrawEffectPropertiesPopup(_selectedEffect, chainRect);
            if (_showInputPopup) DrawInputPopup(chainRect);
            if (_showOutputPopup) DrawOutputPopup(chainRect);
            if (_showNodeAddMenu) DrawNodeAddMenu(chainRect);
        }

        
        private Vector2 GetNodeOutputPortPos(IDSPEffect effect, float nodeWidth, float nodeHeight)
        {
            if (effect == null) // Input node
                return _inputNodePos + new Vector2(nodeWidth, nodeHeight / 2);
            if (_effectPositions.TryGetValue(effect, out Vector2 pos))
                return pos + new Vector2(nodeWidth, nodeHeight / 2);
            return Vector2.zero;
        }

        
        private Vector2 GetNodeInputPortPos(IDSPEffect effect, float nodeWidth, float nodeHeight)
        {
            if (effect == null) // Output node
                return _outputNodePos + new Vector2(0, nodeHeight / 2);
            if (_effectPositions.TryGetValue(effect, out Vector2 pos))
                return pos + new Vector2(0, nodeHeight / 2);
            return Vector2.zero;
        }

        
        private bool TryStartWireFromPort(List<IDSPEffect> effects, Vector2 mousePos, Vector2 localMousePos, float nodeWidth, float nodeHeight, float portSize)
        {
            float hitSize = portSize * 2.5f; // Larger hit area
            
            // Input node output port
            Rect inputOutPort = new Rect(_inputNodePos.x + nodeWidth - hitSize/2, _inputNodePos.y + nodeHeight/2 - hitSize/2, hitSize, hitSize);
            if (inputOutPort.Contains(localMousePos))
            {
                _isCreatingWire = true;
                _wireStartEffect = null;
                _wireFromOutput = true;
                _wireDragEndPos = mousePos;
                return true;
            }
            
            // Effect ports
            foreach (var effect in effects)
            {
                if (_effectPositions.TryGetValue(effect, out Vector2 pos))
                {
                    Rect outPort = new Rect(pos.x + nodeWidth - hitSize/2, pos.y + nodeHeight/2 - hitSize/2, hitSize, hitSize);
                    Rect inPort = new Rect(pos.x - hitSize/2, pos.y + nodeHeight/2 - hitSize/2, hitSize, hitSize);
                    
                    if (outPort.Contains(localMousePos))
                    {
                        _isCreatingWire = true;
                        _wireStartEffect = effect;
                        _wireFromOutput = true;
                        _wireDragEndPos = mousePos;
                        return true;
                    }
                    if (inPort.Contains(localMousePos))
                    {
                        _isCreatingWire = true;
                        _wireStartEffect = effect;
                        _wireFromOutput = false;
                        _wireDragEndPos = mousePos;
                        return true;
                    }
                }
            }
            
            // Output node input port
            Rect outputInPort = new Rect(_outputNodePos.x - hitSize/2, _outputNodePos.y + nodeHeight/2 - hitSize/2, hitSize, hitSize);
            if (outputInPort.Contains(localMousePos))
            {
                _isCreatingWire = true;
                _wireStartEffect = null;
                _wireFromOutput = false;
                _wireDragEndPos = mousePos;
                return true;
            }
            
            return false;
        }

        
        private bool TryCompleteWireConnection(List<IDSPEffect> effects, Vector2 mousePos, Vector2 localMousePos, float nodeWidth, float nodeHeight, float portSize)
        {
            float hitSize = portSize * 2.5f;
            if (_wireFromOutput)
            {
                // Looking for input ports
                // Output node
                Rect outputInPort = new Rect(_outputNodePos.x - hitSize/2, _outputNodePos.y + nodeHeight/2 - hitSize/2, hitSize, hitSize);
                if (outputInPort.Contains(localMousePos))
                {
                    CreateConnection(_wireStartEffect, null);
                    _isCreatingWire = false;
                    return true;
                }
                
                // Effect input ports
                foreach (var effect in effects)
                {
                    if (effect == _wireStartEffect) continue;
                    if (WillCreateCycle(_wireStartEffect, effect)) continue;
                    
                    if (_effectPositions.TryGetValue(effect, out Vector2 pos))
                    {
                        Rect inPort = new Rect(pos.x - hitSize/2, pos.y + nodeHeight/2 - hitSize/2, hitSize, hitSize);
                        if (inPort.Contains(localMousePos))
                        {
                            CreateConnection(_wireStartEffect, effect);
                            _isCreatingWire = false;
                            return true;
                        }
                    }
                }
            }
            else
            {
                // Looking for output ports
                // Input node
                Rect inputOutPort = new Rect(_inputNodePos.x + nodeWidth - hitSize/2, _inputNodePos.y + nodeHeight/2 - hitSize/2, hitSize, hitSize);
                if (inputOutPort.Contains(localMousePos))
                {
                    CreateConnection(null, _wireStartEffect);
                    _isCreatingWire = false;
                    return true;
                }
                
                // Effect output ports
                foreach (var effect in effects)
                {
                    if (effect == _wireStartEffect) continue;
                    if (WillCreateCycle(effect, _wireStartEffect)) continue;
                    
                    if (_effectPositions.TryGetValue(effect, out Vector2 pos))
                    {
                        Rect outPort = new Rect(pos.x + nodeWidth - hitSize/2, pos.y + nodeHeight/2 - hitSize/2, hitSize, hitSize);
                        if (outPort.Contains(localMousePos))
                        {
                            CreateConnection(effect, _wireStartEffect);
                            _isCreatingWire = false;
                            return true;
                        }
                    }
                }
            }
            return false;
        }


        private void TryDeleteWireAtPoint(Vector2 mousePos, Vector2 localMousePos, float nodeWidth, float nodeHeight)
        {
            float threshold = 8f;
            NodeConnection toDelete = null;
            
            foreach (var conn in _connections)
            {
                Vector2 start = GetNodeOutputPortPos(conn.FromEffect, nodeWidth, nodeHeight);
                Vector2 end = GetNodeInputPortPos(conn.ToEffect, nodeWidth, nodeHeight);
                
                // Simple distance check to bezier midpoint (in local space)
                Vector2 mid = (start + end) / 2f;
                if (Vector2.Distance(localMousePos, mid) < threshold * 3)
                {
                    toDelete = conn;
                    break;
                }
            }
            
            if (toDelete != null)
            {
                _connections.Remove(toDelete);
                SyncRuntimeChain();
                _visualizerContainer?.MarkDirtyRepaint();
            }
        }

        
        private void CreateConnection(IDSPEffect from, IDSPEffect to)
        {
            // Remove existing connection TO target (input ports only have one connection)
            _connections.RemoveAll(c => c.ToEffect == to);
            _connections.Add(new NodeConnection { FromEffect = from, ToEffect = to });
            SyncRuntimeChain();
            _visualizerContainer?.MarkDirtyRepaint();
        }

        
        private bool IsEffectConnected(IDSPEffect effect)
        {
            if (effect == null) return true; // Input/Output virtual nodes
            
            // Iterative BFS to prevent recursion crash
            var visited = new HashSet<IDSPEffect>();
            var queue = new Queue<IDSPEffect>();
            
            foreach (var conn in _connections)
            {
                if (conn.FromEffect == null)
                {
                    if (conn.ToEffect == effect) return true;
                    if (conn.ToEffect != null && visited.Add(conn.ToEffect))
                        queue.Enqueue(conn.ToEffect);
                }
            }
            
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var conn in _connections)
                {
                    if (conn.FromEffect == current)
                    {
                        if (conn.ToEffect == effect) return true;
                        if (conn.ToEffect != null && visited.Add(conn.ToEffect))
                            queue.Enqueue(conn.ToEffect);
                    }
                }
            }
            return false;
        }


        private bool WillCreateCycle(IDSPEffect start, IDSPEffect target)
        {
            if (start == target) return true;
            if (target == null) return false; // Output node
            
            var visited = new HashSet<IDSPEffect>();
            var queue = new Queue<IDSPEffect>();
            queue.Enqueue(target);
            visited.Add(target);
            
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var conn in _connections)
                {
                    if (conn.FromEffect == current)
                    {
                        if (conn.ToEffect == start) return true;
                        if (conn.ToEffect != null && visited.Add(conn.ToEffect))
                            queue.Enqueue(conn.ToEffect);
                    }
                }
            }
            return false;
        }

        

        
        private void DrawGraphNodeNew(Rect rect, string name, string icon, Color headerColor, bool enabled, IDSPEffect effect, bool isSelected)
        {
            float headerHeight = 22f;
            
            // Drop Shadow
            EditorGUI.DrawRect(new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height), new Color(0, 0, 0, 0.3f));
            
            // Background
            Color bgColor = new Color(0.12f, 0.12f, 0.14f, 0.95f);
            if (isSelected) bgColor = new Color(0.18f, 0.18f, 0.22f, 0.95f);
            EditorGUI.DrawRect(rect, bgColor);
            
            // Header
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, headerHeight), headerColor);
            
            // Header Content
            var titleStyle = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleLeft };
            titleStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(rect.x + 6, rect.y + 2, rect.width - 12, headerHeight), name, titleStyle);
            
            // Main Body Content (Icon)
            var iconStyle = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleCenter, fontSize = 24 };
            GUI.Label(new Rect(rect.x, rect.y + headerHeight, rect.width, rect.height - headerHeight), icon, iconStyle);
            
            // Border
            Color borderColor = isSelected ? Color.white : new Color(0.3f, 0.3f, 0.35f);
            DrawRectOutline(rect, borderColor);

            // Status LED
            if (effect != null)
            {
                Color ledColor = enabled ? VarcoEditorStyles.Success : VarcoEditorStyles.Error;
                EditorGUI.DrawRect(new Rect(rect.xMax - 12, rect.y + 28, 6, 6), ledColor);
            }
        }


        private void DrawNodePortCircular(Rect rect, bool enabled, Color borderColor, bool isInputOnly = false, bool isOutputOnly = false)
        {
            float portSize = 10f;
            float portY = rect.y + rect.height / 2;
            Color portColor = VarcoEditorStyles.Mint;
            if (!enabled) portColor = Color.gray;

            // Input port (left) - shown for effect nodes (isInputOnly=false, isOutputOnly=false) and Output node (isInputOnly=true)
            if (!isOutputOnly)
            {
                Handles.color = portColor;
                Handles.DrawSolidDisc(new Vector3(rect.x, portY, 0), Vector3.forward, portSize * 0.4f);
                Handles.color = borderColor;
                Handles.DrawWireDisc(new Vector3(rect.x, portY, 0), Vector3.forward, portSize * 0.4f);
            }
            // Output port (right) - shown for effect nodes (isInputOnly=false, isOutputOnly=false) and Input node (isOutputOnly=true)
            if (!isInputOnly)
            {
                Handles.color = portColor;
                Handles.DrawSolidDisc(new Vector3(rect.xMax, portY, 0), Vector3.forward, portSize * 0.4f);
                Handles.color = borderColor;
                Handles.DrawWireDisc(new Vector3(rect.xMax, portY, 0), Vector3.forward, portSize * 0.4f);
            }
        }

        
        private Color GetEffectHeaderColor(IDSPEffect effect)
        {
            if (effect == null) return VarcoEditorStyles.Mint;
            var typeName = effect.GetType().Name.ToLower();
            if (typeName.Contains("delay")) return VarcoEditorStyles.Blue;
            if (typeName.Contains("compressor") || typeName.Contains("limiter")) return VarcoEditorStyles.Mint;
            if (typeName.Contains("distortion") || typeName.Contains("saturation") || typeName.Contains("tube")) return VarcoEditorStyles.Warning;
            if (typeName.Contains("pitch") || typeName.Contains("vocoder")) return VarcoEditorStyles.Purple;
            return VarcoEditorStyles.Purple;
        }

        
        private void DrawInputPopup(Rect canvasRect)
        {
            float popupWidth = 180f;
            float popupHeight = 100f;
            Vector2 worldPos = canvasRect.position + _inputNodePos;
            Rect popupRect = new Rect(worldPos.x, worldPos.y + 75f, popupWidth, popupHeight);
            
            // Background & Shadow
            EditorGUI.DrawRect(new Rect(popupRect.x + 2, popupRect.y + 2, popupWidth, popupHeight), new Color(0, 0, 0, 0.3f));
            EditorGUI.DrawRect(popupRect, new Color(0.12f, 0.12f, 0.15f, 0.98f));
            EditorGUI.DrawRect(new Rect(popupRect.x, popupRect.y, popupWidth, 2), VarcoEditorStyles.Mint);
            DrawRectOutline(popupRect, new Color(0.3f, 0.3f, 0.35f));
            
            float y = popupRect.y + 10;
            GUI.Label(new Rect(popupRect.x + 10, y, popupWidth - 20, 18), "Input Source", 
                new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = Color.white } });
            y += 24;
            
            GUI.Label(new Rect(popupRect.x + 10, y, popupWidth - 20, 16), "Speaker:", EditorStyles.miniLabel);
            y += 18;
            GUI.Label(new Rect(popupRect.x + 10, y, popupWidth - 20, 18), _inputSpeakerName, 
                new GUIStyle(EditorStyles.label) { normal = { textColor = VarcoEditorStyles.Mint }, fontStyle = FontStyle.Bold });
        }


        private void DrawOutputPopup(Rect canvasRect)
        {
            float popupWidth = 180f;
            float popupHeight = 110f;
            Vector2 worldPos = canvasRect.position + _outputNodePos;
            Rect popupRect = new Rect(worldPos.x - popupWidth + 100f, worldPos.y + 75f, popupWidth, popupHeight);
            
            // Background & Shadow
            EditorGUI.DrawRect(new Rect(popupRect.x + 2, popupRect.y + 2, popupWidth, popupHeight), new Color(0, 0, 0, 0.3f));
            EditorGUI.DrawRect(popupRect, new Color(0.12f, 0.12f, 0.15f, 0.98f));
            EditorGUI.DrawRect(new Rect(popupRect.x, popupRect.y, popupWidth, 2), VarcoEditorStyles.Purple);
            DrawRectOutline(popupRect, new Color(0.3f, 0.3f, 0.35f));
            
            float y = popupRect.y + 10;
            GUI.Label(new Rect(popupRect.x + 10, y, popupWidth - 20, 18), "Output Options", 
                new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = Color.white } });
            y += 24;
            
            var btnStyle = new GUIStyle(EditorStyles.miniButton) { fixedHeight = 24, fontSize = 11 };
            
            if (GUI.Button(new Rect(popupRect.x + 10, y, popupWidth - 20, 24), "Go to Export", btnStyle))
            {
                OnRequestTabChange?.Invoke(2); // TAB_EXPORT is 2
                _showOutputPopup = false;
            }
            y += 28;
            
            if (GUI.Button(new Rect(popupRect.x + 10, y, popupWidth - 20, 24), "Quick Export", btnStyle))
            {
                OnQuickExport?.Invoke();
                _showOutputPopup = false;
            }
        }


        private void DrawEffectPropertiesPopup(IDSPEffect effect, Rect canvasRect)
        {
            if (!_effectPositions.TryGetValue(effect, out Vector2 localNodePos)) return;
            Vector2 worldNodePos = canvasRect.position + localNodePos;
            
            // Dynamically calculate popup size based on parameter count
            var type = effect.GetType();
            var properties = type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite && p.Name != "Name" && p.Name != "Enabled")
                .ToList();
            
            float popupWidth = 240f;
            float headerHeight = 70f; // Title + Enable + spacing
            float rowHeight = 22f;
            float removeButtonHeight = 26f;
            float maxContentHeight = 300f; // Max height before scrolling
            float contentHeight = properties.Count * rowHeight;
            float actualContentHeight = Mathf.Min(contentHeight, maxContentHeight);
            float popupHeight = headerHeight + actualContentHeight + removeButtonHeight + 16f;
            
            float popupX = Mathf.Clamp(worldNodePos.x, canvasRect.x + 5, canvasRect.xMax - popupWidth - 5);
            float popupY = worldNodePos.y + 75f;
            
            if (popupY + popupHeight > canvasRect.yMax - 5)
                popupY = worldNodePos.y - popupHeight - 10;
            
            Rect popupRect = new Rect(popupX, popupY, popupWidth, popupHeight);
            
            // Background
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(new Rect(popupRect.x + 2, popupRect.y + 2, popupWidth, popupHeight), new Color(0, 0, 0, 0.3f));
                EditorGUI.DrawRect(popupRect, new Color(0.12f, 0.12f, 0.15f, 0.98f));
                EditorGUI.DrawRect(new Rect(popupRect.x, popupRect.y, popupWidth, 2), VarcoEditorStyles.Mint);
                DrawRectOutline(popupRect, new Color(0.3f, 0.3f, 0.35f));
            }
            
            float y = popupRect.y + 8;
            
            // Title
            GUI.Label(new Rect(popupRect.x + 8, y, popupWidth - 16, 16), effect.Name, 
                new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = Color.white }, fontSize = 11 });
            y += 22;
            
            // Enable toggle
            bool newEnabled = GUI.Toggle(new Rect(popupRect.x + 8, y, popupWidth - 16, 18), effect.Enabled, "Enabled");
            if (newEnabled != effect.Enabled)
            {
                effect.Enabled = newEnabled;
                SyncRuntimeChain();
                EditorUtility.SetDirty(_target);
            }
            y += 26;
            
            // Scrollable content area
            Rect contentRect = new Rect(popupRect.x + 4, y, popupWidth - 8, actualContentHeight);
            Rect viewRect = new Rect(0, 0, popupWidth - 24, contentHeight);
            
            // Only create scroll if needed
            if (contentHeight > maxContentHeight)
            {
                // Initialize scroll position if not exists
                if (!_effectScrollPositions.ContainsKey(effect))
                    _effectScrollPositions[effect] = Vector2.zero;
                
                _effectScrollPositions[effect] = GUI.BeginScrollView(contentRect, _effectScrollPositions[effect], viewRect);
            }
            else
            {
                GUI.BeginGroup(contentRect);
            }
            
            float paramY = 0;
            
            // Draw all properties dynamically with proper range detection
            foreach (var prop in properties)
            {
                try
                {
                    var value = prop.GetValue(effect);
                    
                    if (value is float floatVal)
                    {
                        // Auto-detect range from [Range] attribute
                        var rangeAttr = prop.GetCustomAttributes(typeof(RangeAttribute), true)
                            .FirstOrDefault() as RangeAttribute;
                        
                        float min = rangeAttr?.min ?? 0f;
                        float max = rangeAttr?.max ?? 1f;
                        
                        // Smart defaults if no Range attribute
                        if (rangeAttr == null)
                        {
                            var propNameLower = prop.Name.ToLower();
                            if (propNameLower.Contains("semitone") || propNameLower.Contains("pitch"))
                            {
                                min = -12f; max = 12f;
                            }
                            else if (propNameLower.Contains("gain") || propNameLower.Contains("level"))
                            {
                                min = -24f; max = 24f;
                            }
                            else if (propNameLower.Contains("freq"))
                            {
                                min = 20f; max = 20000f;
                            }
                            else if (propNameLower.Contains("time") || propNameLower.Contains("delay"))
                            {
                                min = 0f; max = 5f;
                            }
                        }
                        
                        // Clamp current value to valid range
                        floatVal = Mathf.Clamp(floatVal, min, max);
                        
                        GUI.Label(new Rect(8, paramY, 70, 18), prop.Name, EditorStyles.miniLabel);
                        
                        float newVal = GUI.HorizontalSlider(
                            new Rect(82, paramY + 3, popupWidth - 155, 14), 
                            floatVal, min, max);
                        
                        // Display value with appropriate precision
                        string valueText = (max - min) > 100 ? $"{newVal:F0}" : $"{newVal:F2}";
                        GUI.Label(new Rect(popupWidth - 73, paramY, 55, 18), valueText, 
                            new GUIStyle(EditorStyles.miniLabel) { 
                                normal = { textColor = VarcoEditorStyles.Mint },
                                alignment = TextAnchor.MiddleLeft,
                                fontSize = 11
                            });
                        
                        if (Mathf.Abs(newVal - floatVal) > 0.001f)
                        {
                            prop.SetValue(effect, newVal);
                            EditorUtility.SetDirty(_target);
                        }
                        paramY += rowHeight;
                    }
                    else if (value is int intVal)
                    {
                        var rangeAttr = prop.GetCustomAttributes(typeof(RangeAttribute), true)
                            .FirstOrDefault() as RangeAttribute;
                        
                        int min = (int)(rangeAttr?.min ?? 0);
                        int max = (int)(rangeAttr?.max ?? 100);
                        
                        GUI.Label(new Rect(8, paramY, 70, 18), prop.Name, EditorStyles.miniLabel);
                        
                        int newVal = (int)GUI.HorizontalSlider(
                            new Rect(82, paramY + 3, popupWidth - 155, 14), 
                            intVal, min, max);
                        
                        GUI.Label(new Rect(popupWidth - 73, paramY, 55, 18), newVal.ToString(), 
                            new GUIStyle(EditorStyles.miniLabel) { 
                                normal = { textColor = VarcoEditorStyles.Mint },
                                alignment = TextAnchor.MiddleLeft,
                                fontSize = 11
                            });
                        
                        if (newVal != intVal)
                        {
                            prop.SetValue(effect, newVal);
                            EditorUtility.SetDirty(_target);
                        }
                        paramY += rowHeight;
                    }
                    else if (value is bool boolVal)
                    {
                        bool newBool = GUI.Toggle(new Rect(8, paramY, popupWidth - 24, 18), boolVal, prop.Name);
                        if (newBool != boolVal)
                        {
                            prop.SetValue(effect, newBool);
                            EditorUtility.SetDirty(_target);
                        }
                        paramY += rowHeight;
                    }
                    else if (prop.PropertyType.IsEnum)
                    {
                        GUI.Label(new Rect(8, paramY, 70, 18), prop.Name, EditorStyles.miniLabel);
                        Enum currentEnum = (Enum)value;
                        if (GUI.Button(new Rect(82, paramY, popupWidth - 98, 18), currentEnum.ToString(), EditorStyles.miniButton))
                        {
                            GenericMenu menu = new GenericMenu();
                            foreach (var enumValue in Enum.GetValues(prop.PropertyType))
                            {
                                var ev = enumValue;
                                menu.AddItem(new GUIContent(ev.ToString()), ev.Equals(currentEnum), () => {
                                    prop.SetValue(effect, ev);
                                    EditorUtility.SetDirty(_target);
                                    _visualizerContainer?.MarkDirtyRepaint();
                                });
                            }
                            menu.ShowAsContext();
                        }
                        paramY += rowHeight;
                    }
                }
                catch (System.Exception ex)
                {
                    // Log but don't crash on parameter error
                    Debug.LogWarning($"[DSP] Error rendering parameter {prop.Name}: {ex.Message}");
                }
            }
            
            if (contentHeight > maxContentHeight)
                GUI.EndScrollView();
            else
                GUI.EndGroup();
            
            // Remove button
            y = popupRect.yMax - removeButtonHeight - 8;
            if (GUI.Button(new Rect(popupRect.x + 8, y, popupWidth - 16, removeButtonHeight), "Remove Effect"))
            {
                _target.RemoveEffect(effect);
                _effectPositions.Remove(effect);
                _connections.RemoveAll(c => c.FromEffect == effect || c.ToEffect == effect);
                _selectedEffect = null;
                if (_effectScrollPositions.ContainsKey(effect))
                    _effectScrollPositions.Remove(effect);
                EditorUtility.SetDirty(_target);
                _visualizerContainer?.MarkDirtyRepaint();
            }
        }

        
        private void DrawBezierWireInternal(Vector2 start, Vector2 end, bool enabled)
        {
            Color wireColor = enabled ? new Color(VarcoEditorStyles.Mint.r, VarcoEditorStyles.Mint.g, VarcoEditorStyles.Mint.b, 1.0f) : new Color(0.4f, 0.4f, 0.4f, 0.5f);
            Color shadowColor = new Color(0, 0, 0, 0.4f);
            
            float tangentOffset = Mathf.Abs(end.x - start.x) * 0.5f;
            // Clamp tangent offset for more realistic curving
            tangentOffset = Mathf.Max(tangentOffset, 30f);
            
            Vector3 startTangent = new Vector3(start.x + tangentOffset, start.y, 0);
            Vector3 endTangent = new Vector3(end.x - tangentOffset, end.y, 0);
            
            // Draw Shadow
            Handles.DrawBezier(
                new Vector3(start.x + 1, start.y + 1, 0),
                new Vector3(end.x + 1, end.y + 1, 0),
                startTangent + new Vector3(1, 1, 0),
                endTangent + new Vector3(1, 1, 0),
                shadowColor,
                null,
                5.5f
            );

            // Draw Main Wire
            Handles.DrawBezier(
                new Vector3(start.x, start.y, 0),
                new Vector3(end.x, end.y, 0),
                startTangent,
                endTangent,
                wireColor,
                null,
                enabled ? 4f : 3.5f
            );
        }


        private void DrawBezierWire(Vector2 start, Vector2 end, bool enabled)
        {
            Handles.BeginGUI();
            DrawBezierWireInternal(start, end, enabled);
            Handles.EndGUI();
        }

        
        private void DrawNodeAddMenu(Rect canvasRect)
        {
            float menuWidth = 140f;
            float menuHeight = 160f;
            Rect menuRect = new Rect(_nodeAddMenuPos.x, _nodeAddMenuPos.y, menuWidth, menuHeight);
            
            // Keep menu inside canvas
            if (menuRect.xMax > canvasRect.xMax) menuRect.x = canvasRect.xMax - menuWidth - 5;
            if (menuRect.yMax > canvasRect.yMax) menuRect.y = canvasRect.yMax - menuHeight - 5;
            
            // Background
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(new Rect(menuRect.x + 2, menuRect.y + 2, menuWidth, menuHeight), new Color(0, 0, 0, 0.4f));
                EditorGUI.DrawRect(menuRect, new Color(0.12f, 0.12f, 0.15f, 0.98f));
                DrawRectOutline(menuRect, new Color(0.3f, 0.3f, 0.4f));
            }
            
            // Title
            GUI.Label(new Rect(menuRect.x + 8, menuRect.y + 6, menuWidth - 16, 18), "Add Effect", 
                new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = Color.white }, fontSize = 11 });
            
            float buttonY = menuRect.y + 28;
            float buttonHeight = 22f;
            
            string[] effects = { "Dynamics", "Delay", "Reverb", "Pitch Shift", "Tube Saturation" };
            foreach (var effectName in effects)
            {
                Rect buttonRect = new Rect(menuRect.x + 6, buttonY, menuWidth - 12, buttonHeight);
                
                if (GUI.Button(buttonRect, effectName))
                {
                    AddEffectByName(effectName);
                    _showNodeAddMenu = false;
                    Event.current.Use();
                    _visualizerContainer?.MarkDirtyRepaint();
                    return;
                }
                buttonY += buttonHeight + 2;
            }
            
            // Click outside menu to close
            if (Event.current.type == EventType.MouseDown && !menuRect.Contains(Event.current.mousePosition))
            {
                _showNodeAddMenu = false;
                Event.current.Use();
                _visualizerContainer?.MarkDirtyRepaint();
            }
        }

        
        private void AddEffectByName(string name)
        {
            if (_target == null) return;
            
            IDSPEffect effect = name switch
            {
                "Dynamics" => new UnifiedDynamics(),
                "Delay" => new UnifiedDelay(),
                "Reverb" => new FDNReverb(),
                "Pitch Shift" => new WSOLAPitchShift(),
                "Tube Saturation" => new TubeEmulation(),
                _ => null
            };
            
            if (effect != null)
            {
                _target.AddEffect(effect);
                
                if (_isSimplifiedView)
                {
                    RebuildLinearConnections();
                }
                else
                {
                    // In node graph, just spawn the node
                    _effectPositions[effect] = _nextSpawnPos - _lastCanvasRect.position;
                    // Don't clear connections, let user connect it
                    SyncRuntimeChain();
                }
                
                EditorUtility.SetDirty(_target);
                _visualizerContainer?.MarkDirtyRepaint();
            }
        }

        
        private string GetEffectIcon(IDSPEffect effect)
        {
            var typeName = effect.GetType().Name.ToLower();
            if (typeName.Contains("dynamics")) return "COMP";
            if (typeName.Contains("pingpong")) return "PP";
            if (typeName.Contains("multitap")) return "MT";
            if (typeName.Contains("modulated")) return "MOD";
            if (typeName.Contains("delay")) return "DLY";
            if (typeName.Contains("compressor") || typeName.Contains("limiter")) return "COMP";
            if (typeName.Contains("distortion") || typeName.Contains("saturation") || typeName.Contains("tube")) return "SAT";
            if (typeName.Contains("phaser") || typeName.Contains("flanger")) return "PH";
            if (typeName.Contains("reverb")) return "REV";
            if (typeName.Contains("pitch") || typeName.Contains("vocoder")) return "PITCH";
            if (typeName.Contains("eq") || typeName.Contains("filter")) return "EQ";
            return "FX";
        }
    }
}
