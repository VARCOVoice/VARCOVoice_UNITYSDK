using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace VARCOVoice.Editor
{
    [CustomEditor(typeof(VarcoDialoguePlayer))]
    public class VarcoDialoguePlayerEditor : UnityEditor.Editor
    {
        private bool[] _slotFoldouts;
        
        public override void OnInspectorGUI()
        {
            var source = (VarcoDialoguePlayer)target;
            int slotCount = source.dialogueSlots != null ? source.dialogueSlots.Count : 0;

            EditorGUILayout.LabelField("VARCO Audio Source", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Dialogue Slots: {slotCount}");
            EditorGUILayout.Space();

            if (GUILayout.Button("Open in Export Panel"))
            {
                var window = EditorWindow.GetWindow<VarcoVoiceMainWindow>();
                window.Focus();
                window.OpenExportFor(source);
            }

            EditorGUILayout.Space();
            
            // Initialize foldouts array
            if (_slotFoldouts == null || _slotFoldouts.Length != slotCount)
                _slotFoldouts = new bool[slotCount];
            
            // Draw slots with custom UI for manual mapping
            serializedObject.Update();
            
            var slotsProperty = serializedObject.FindProperty("dialogueSlots");
            
            for (int i = 0; i < slotCount; i++)
            {
                var slot = source.dialogueSlots[i];
                var slotProp = slotsProperty.GetArrayElementAtIndex(i);
                
                string slotName = string.IsNullOrEmpty(slot.id) ? $"Slot {i}" : slot.id;
                _slotFoldouts[i] = EditorGUILayout.Foldout(_slotFoldouts[i], slotName, true);
                
                if (_slotFoldouts[i])
                {
                    EditorGUI.indentLevel++;
                    
                    // Draw default properties
                    EditorGUILayout.PropertyField(slotProp.FindPropertyRelative("id"));
                    EditorGUILayout.PropertyField(slotProp.FindPropertyRelative("clip"));
                    EditorGUILayout.PropertyField(slotProp.FindPropertyRelative("visemeData"));
                    EditorGUILayout.PropertyField(slotProp.FindPropertyRelative("triggerType"));
                    EditorGUILayout.PropertyField(slotProp.FindPropertyRelative("triggerRadius"));
                    EditorGUILayout.PropertyField(slotProp.FindPropertyRelative("lipsyncTarget"));
                    EditorGUILayout.PropertyField(slotProp.FindPropertyRelative("enableLipsync"));
                    EditorGUILayout.PropertyField(slotProp.FindPropertyRelative("lipsyncIntensity"));
                    
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Manual Blend Shape Mapping", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(slotProp.FindPropertyRelative("useManualMapping"), 
                        new GUIContent("Use Manual Mapping"));
                    
                    if (slot.useManualMapping)
                    {
                        DrawBlendShapeMappingUI(slot, slotProp);
                    }
                    
                    EditorGUI.indentLevel--;
                    EditorGUILayout.Space();
                }
            }
            
            serializedObject.ApplyModifiedProperties();
        }
        
        private void DrawBlendShapeMappingUI(VarcoDialoguePlayer.DialogueSlot slot, SerializedProperty slotProp)
        {
            // Get blend shape names from target
            var blendShapeNames = GetBlendShapeNames(slot.lipsyncTarget);
            
            if (blendShapeNames.Count == 0)
            {
                EditorGUILayout.HelpBox("Set a Lipsync Target with blend shapes first.", MessageType.Info);
                return;
            }
            
            EditorGUI.indentLevel++;
            
            // Add empty option at the beginning
            var options = new List<string> { "(None)" };
            options.AddRange(blendShapeNames);
            var optionsArray = options.ToArray();
            
            // Draw dropdown for each vowel
            DrawMappingDropdown("A (아)", slot.mappingA, slotProp.FindPropertyRelative("mappingA"), optionsArray);
            DrawMappingDropdown("I (이)", slot.mappingI, slotProp.FindPropertyRelative("mappingI"), optionsArray);
            DrawMappingDropdown("U (우)", slot.mappingU, slotProp.FindPropertyRelative("mappingU"), optionsArray);
            DrawMappingDropdown("E (에)", slot.mappingE, slotProp.FindPropertyRelative("mappingE"), optionsArray);
            DrawMappingDropdown("O (오)", slot.mappingO, slotProp.FindPropertyRelative("mappingO"), optionsArray);
            
            EditorGUI.indentLevel--;
        }
        
        private void DrawMappingDropdown(string label, VarcoDialoguePlayer.VisemeMapping mapping, 
            SerializedProperty mappingProp, string[] options)
        {
            // Find current selection index
            int currentIndex = 0;
            if (!string.IsNullOrEmpty(mapping.blendShapeName))
            {
                for (int i = 1; i < options.Length; i++)
                {
                    if (options[i] == mapping.blendShapeName)
                    {
                        currentIndex = i;
                        break;
                    }
                }
            }
            
            EditorGUI.BeginChangeCheck();
            int newIndex = EditorGUILayout.Popup(label, currentIndex, options);
            if (EditorGUI.EndChangeCheck())
            {
                var nameProp = mappingProp.FindPropertyRelative("blendShapeName");
                nameProp.stringValue = newIndex == 0 ? "" : options[newIndex];
            }
        }
        
        private List<string> GetBlendShapeNames(SkinnedMeshRenderer target)
        {
            var names = new List<string>();
            
            if (target == null || target.sharedMesh == null)
                return names;
            
            var mesh = target.sharedMesh;
            int count = mesh.blendShapeCount;
            
            for (int i = 0; i < count; i++)
            {
                names.Add(mesh.GetBlendShapeName(i));
            }
            
            return names;
        }
    }
}
