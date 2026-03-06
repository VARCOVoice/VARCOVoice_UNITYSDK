using System;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VARCOVoice.LipSync;
using Object = UnityEngine.Object;

namespace VARCOVoice.Editor
{
    public class ExportPanelController
    {
        private VisualElement _root;
        private ScrollView _objectList;
        private VisualElement _detailPanel;
        private ScrollView _libraryList;
        private Button _refreshButton;
        private Button _createButton;

        private VarcoDialoguePlayer _selectedSource;
        private VisualElement _selectedItem;
        private AudioClip _currentClip;

        private const string LIBRARY_FOLDER = "Assets/VARCOExports";

        // Sort options
        private enum LibrarySortMode { Name, Duration, Recent }
        private LibrarySortMode _currentSortMode = LibrarySortMode.Name;

        public void Initialize(VisualElement root, AudioClip currentClip)
        {
            _root = root;
            _currentClip = currentClip;

            _objectList = _root.Q<ScrollView>("object-list");
            _detailPanel = _root.Q<VisualElement>("detail-panel");
            _libraryList = _root.Q<ScrollView>("library-list");
            _refreshButton = _root.Q<Button>("export-refresh");
            _createButton = _root.Q<Button>("create-object");

            if (_refreshButton != null) _refreshButton.clicked += RefreshAll;
            if (_createButton != null) _createButton.clicked += CreateNewGameObject;

            // Setup sort dropdown
            var sortContainer = _root.Q<VisualElement>("library-sort-container");
            if (sortContainer != null)
            {
                var sortDropdown = new EnumField(_currentSortMode);
                sortDropdown.style.width = 80;
                sortDropdown.RegisterValueChangedCallback(evt =>
                {
                    _currentSortMode = (LibrarySortMode)evt.newValue;
                    RefreshLibrary();
                });
                sortContainer.Add(sortDropdown);
            }

            EditorApplication.hierarchyChanged -= RefreshObjectList;
            EditorApplication.hierarchyChanged += RefreshObjectList;

            RefreshAll();
        }

        public void RefreshAll()
        {
            RefreshObjectList();
            RefreshLibrary();
        }

        public void SetCurrentClip(AudioClip clip)
        {
            _currentClip = clip;
            if (_selectedSource != null)
            {
                ShowDetailPanel(_selectedSource);
            }
        }

        public void RefreshObjectList()
        {
            if (_objectList == null) return;

            _objectList.Clear();
            _selectedItem = null;

            var sources = Object.FindObjectsByType<VarcoDialoguePlayer>(FindObjectsSortMode.None);
            foreach (var source in sources.OrderBy(s => s.gameObject.name))
            {
                _objectList.Add(CreateObjectItem(source));
            }

            if (sources.Length == 0)
            {
                var empty = new Label("No VarcoDialoguePlayer found in the scene.");
                empty.AddToClassList("placeholder-text");
                _objectList.Add(empty);
            }

            if (_selectedSource != null)
            {
                if (sources.Contains(_selectedSource))
                {
                    SelectSource(_selectedSource);
                }
                else
                {
                    _selectedSource = null;
                    ShowDetailPanel(null);
                }
            }
            else if (sources.Length == 0)
            {
                ShowDetailPanel(null);
            }
        }

        private VisualElement CreateObjectItem(VarcoDialoguePlayer source)
        {
            var item = new VisualElement();
            item.AddToClassList("object-item");
            item.userData = source;

            var nameLabel = new Label(source.gameObject.name);
            nameLabel.AddToClassList("object-name");
            item.Add(nameLabel);

            int total = source.dialogueSlots != null ? source.dialogueSlots.Count : 0;
            int filled = 0;
            if (source.dialogueSlots != null)
            {
                filled = source.dialogueSlots.Count(s => s != null && s.clip != null);
            }

            var metaText = total == 0 ? "No slots" : $"{filled}/{total} slots";
            var metaLabel = new Label(metaText);
            metaLabel.AddToClassList("object-meta");
            item.Add(metaLabel);

            item.RegisterCallback<ClickEvent>(_ => SelectObject(item, source));

            return item;
        }

        private void SelectObject(VisualElement item, VarcoDialoguePlayer source)
        {
            _selectedSource = source;

            if (_selectedItem != null)
            {
                _selectedItem.RemoveFromClassList("object-item--selected");
            }

            _selectedItem = item;
            _selectedItem.AddToClassList("object-item--selected");

            Selection.activeGameObject = source.gameObject;
            ShowDetailPanel(source);
        }

        public void SelectSource(VarcoDialoguePlayer source)
        {
            if (source == null || _objectList == null) return;

            foreach (var child in _objectList.Children())
            {
                if (ReferenceEquals(child.userData, source))
                {
                    SelectObject(child, source);
                    return;
                }
            }
        }

        private void ShowDetailPanel(VarcoDialoguePlayer source)
        {
            if (_detailPanel == null) return;

            _detailPanel.Clear();

            if (source == null)
            {
                var placeholder = new Label("Select an object to configure dialogue slots.");
                placeholder.AddToClassList("placeholder-text");
                _detailPanel.Add(placeholder);
                return;
            }

            var header = new VisualElement();
            header.AddToClassList("detail-header");

            var title = new Label(source.gameObject.name);
            title.AddToClassList("detail-title");
            header.Add(title);

            var clipLabel = new Label(GetCurrentClipLabel());
            clipLabel.AddToClassList("detail-clip");
            header.Add(clipLabel);

            _detailPanel.Add(header);

            var scrollView = new ScrollView();
            scrollView.AddToClassList("detail-scroll");

            if (source.dialogueSlots == null)
            {
                Undo.RecordObject(source, "Initialize Dialogue Slots");
                source.dialogueSlots = new System.Collections.Generic.List<VarcoDialoguePlayer.DialogueSlot>();
                EditorUtility.SetDirty(source);
            }

            for (int i = 0; i < source.dialogueSlots.Count; i++)
            {
                var slot = source.dialogueSlots[i];
                scrollView.Add(CreateSlotUI(source, slot, i));
            }

            _detailPanel.Add(scrollView);

            var actions = new VisualElement();
            actions.AddToClassList("slot-actions");

            var addBtn = new Button(() => AddNewSlot(source));
            addBtn.text = "+ Add Slot";
            addBtn.AddToClassList("btn-secondary");
            actions.Add(addBtn);

            _detailPanel.Add(actions);

            // Add drop handling to detail panel (Drop to create new slot)
            _detailPanel.RegisterCallback<DragUpdatedEvent>(evt =>
            {
                if (DragAndDrop.objectReferences.Any(o => o is AudioClip))
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Link;
                    evt.StopPropagation();
                }
            });

            _detailPanel.RegisterCallback<DragPerformEvent>(evt =>
            {
                var clips = DragAndDrop.objectReferences.Where(o => o is AudioClip).Cast<AudioClip>().ToList();
                if (clips.Count > 0)
                {
                    DragAndDrop.AcceptDrag();
                    foreach (var clip in clips)
                    {
                        var newSlot = new VarcoDialoguePlayer.DialogueSlot { clip = clip, id = clip.name };
                        source.dialogueSlots.Add(newSlot);
                        GenerateVisemeData(source, newSlot);
                    }
                    EditorUtility.SetDirty(source);
                    RefreshObjectList();
                    ShowDetailPanel(source);
                    evt.StopPropagation();
                }
            });
        }

        private string GetCurrentClipLabel()
        {
            return _currentClip != null ? $"Current Clip: {_currentClip.name}" : "Current Clip: None";
        }

        private VisualElement CreateSlotUI(VarcoDialoguePlayer source, VarcoDialoguePlayer.DialogueSlot slot, int index)
        {
            if (slot == null)
            {
                slot = new VarcoDialoguePlayer.DialogueSlot();
                source.dialogueSlots[index] = slot;
                EditorUtility.SetDirty(source);
            }

            var container = new VisualElement();
            container.AddToClassList("slot-container");

            // === HEADER ===
            var header = new VisualElement();
            header.AddToClassList("slot-header");

            var title = new Label($"SLOT {index + 1}");
            title.AddToClassList("slot-title");
            header.Add(title);

            var headerActions = new VisualElement();
            headerActions.style.flexDirection = FlexDirection.Row;
            headerActions.style.alignItems = Align.Center;

            var deleteBtn = new Button(() =>
            {
                Undo.RecordObject(source, "Delete Dialogue Slot");
                source.dialogueSlots.RemoveAt(index);
                EditorUtility.SetDirty(source);
                RefreshObjectList();
                ShowDetailPanel(source);
            });
            deleteBtn.text = "X"; // Minimal close icon style
            deleteBtn.AddToClassList("btn-danger");
            deleteBtn.tooltip = "Remove Slot";
            // Override danger btn to be smaller/icon-like
            deleteBtn.style.paddingLeft = 0; deleteBtn.style.paddingRight = 0;
            deleteBtn.style.paddingTop = 0; deleteBtn.style.paddingBottom = 0;
            deleteBtn.style.width = 24; deleteBtn.style.height = 24;
            deleteBtn.style.minWidth = 24; deleteBtn.style.minHeight = 24;
            deleteBtn.style.justifyContent = Justify.Center;
            deleteBtn.style.alignItems = Align.Center;

            headerActions.Add(deleteBtn);
            header.Add(headerActions);
            container.Add(header);

            // === CONTENT BODY ===
            var content = new VisualElement();
            content.AddToClassList("slot-content");

            // Row1: ID [TextField] + Clip [ObjectField]
            var row1 = new VisualElement();
            row1.style.flexDirection = FlexDirection.Row;
            row1.style.alignItems = Align.Center;
            row1.style.flexWrap = Wrap.NoWrap; // Force single line
            row1.style.marginBottom = 6;

            var idLabel = new Label("ID");
            idLabel.style.width = 20; // Minimal label width
            idLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
            row1.Add(idLabel);

            var idField = new TextField();
            idField.isDelayed = true;
            idField.value = slot.id;
            idField.style.flexGrow = 1; // Grow 1
            idField.style.flexShrink = 1;
            idField.style.minWidth = 50;
            idField.style.marginRight = 8; // Reduced margin
            idField.RegisterValueChangedCallback(evt =>
            {
                Undo.RecordObject(source, "Edit Slot ID");
                slot.id = evt.newValue;
                EditorUtility.SetDirty(source);
            });
            row1.Add(idField);

            var clipLabel = new Label("Clip");
            clipLabel.style.width = 30; // Minimal label width
            clipLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
            row1.Add(clipLabel);

            var clipField = new ObjectField();
            clipField.objectType = typeof(AudioClip);
            clipField.value = slot.clip;
            clipField.style.flexGrow = 2; // Clip gets more space (Grow 2)
            clipField.style.flexShrink = 1;
            clipField.style.minWidth = 80;
            clipField.RegisterValueChangedCallback(evt =>
            {
                Undo.RecordObject(source, "Assign Slot Clip");
                slot.clip = evt.newValue as AudioClip;
                if (slot.clip != null) GenerateVisemeData(source, slot);
                EditorUtility.SetDirty(source);
                RefreshObjectList();
                ShowDetailPanel(source);
            });
            row1.Add(clipField);

            content.Add(row1);

            // Row2: Trigger [Dropdown] + Viseme N frames + 우측끝 [Generate]
            var row2 = new VisualElement();
            row2.style.flexDirection = FlexDirection.Row;
            row2.style.alignItems = Align.Center;
            row2.style.flexWrap = Wrap.Wrap;
            row2.style.marginBottom = 6;

            var triggerLabel = new Label("Trigger");
            triggerLabel.style.width = 70;
            triggerLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
            row2.Add(triggerLabel);

            var triggerField = new EnumField(slot.triggerType);
            triggerField.style.width = 100;
            triggerField.style.marginRight = 8;
            row2.Add(triggerField);

            var radiusField = new FloatField();
            radiusField.value = slot.triggerRadius;
            radiusField.style.width = 50;
            radiusField.style.marginRight = 12;
            radiusField.style.display = slot.triggerType == VarcoDialoguePlayer.DialogueSlot.TriggerType.OnTrigger
                ? DisplayStyle.Flex : DisplayStyle.None;
            radiusField.RegisterValueChangedCallback(evt =>
            {
                Undo.RecordObject(source, "Edit Trigger Radius");
                slot.triggerRadius = evt.newValue;
                EditorUtility.SetDirty(source);
            });
            row2.Add(radiusField);

            triggerField.RegisterValueChangedCallback(evt =>
            {
                if (source == null) return;
                Undo.RecordObject(source, "Change Trigger Type");
                slot.triggerType = (VarcoDialoguePlayer.DialogueSlot.TriggerType)evt.newValue;
                EditorUtility.SetDirty(source);
                radiusField.style.display = slot.triggerType == VarcoDialoguePlayer.DialogueSlot.TriggerType.OnTrigger
                    ? DisplayStyle.Flex : DisplayStyle.None;
            });

            var visemeLabel = new Label("Viseme");
            visemeLabel.style.width = 45;
            visemeLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
            row2.Add(visemeLabel);

            var visemeStatus = new Label(slot.visemeData != null 
                ? $"{slot.visemeData.Keyframes?.Count ?? 0} frames" 
                : "Not generated");
            visemeStatus.style.flexGrow = 1;
            visemeStatus.style.flexShrink = 1;
            visemeStatus.style.minWidth = 80;
            visemeStatus.style.marginLeft = 4;
            visemeStatus.style.color = slot.visemeData != null ? new Color(0.4f, 0.9f, 0.4f) : new Color(0.5f, 0.5f, 0.5f);
            row2.Add(visemeStatus);

            var genBtn = new Button(() =>
            {
                if (slot.clip != null) { GenerateVisemeData(source, slot); ShowDetailPanel(source); }
            });
            genBtn.text = "Generate";
            genBtn.style.height = 20;
            genBtn.SetEnabled(slot.clip != null);
            row2.Add(genBtn);

            content.Add(row2);

            // Row3: Lipsync [ObjectField] + 우측끝 Enable [Toggle]
            var row3 = new VisualElement();
            row3.style.flexDirection = FlexDirection.Row;
            row3.style.alignItems = Align.Center;
            row3.style.flexWrap = Wrap.Wrap; // Important for overflow
            row3.style.marginBottom = 6;

            var lipsyncLabel = new Label("Lipsync");
            lipsyncLabel.style.width = 70;
            lipsyncLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
            row3.Add(lipsyncLabel);

            var lipsyncField = new ObjectField();
            lipsyncField.objectType = typeof(SkinnedMeshRenderer);
            lipsyncField.value = slot.lipsyncTarget;
            lipsyncField.style.flexGrow = 1;
            lipsyncField.style.flexShrink = 1;
            lipsyncField.style.minWidth = 100;
            lipsyncField.RegisterValueChangedCallback(evt =>
            {
                Undo.RecordObject(source, "Assign Lipsync Target");
                slot.lipsyncTarget = evt.newValue as SkinnedMeshRenderer;
                EditorUtility.SetDirty(source);
            });
            row3.Add(lipsyncField);

            var enableLabel = new Label("Enable");
            enableLabel.style.width = 45;
            enableLabel.style.marginLeft = 12;
            enableLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
            row3.Add(enableLabel);

            var lipsyncToggle = new Toggle();
            lipsyncToggle.value = slot.enableLipsync;
            lipsyncToggle.RegisterValueChangedCallback(evt =>
            {
                Undo.RecordObject(source, "Toggle Lipsync");
                slot.enableLipsync = evt.newValue;
                EditorUtility.SetDirty(source);
            });
            row3.Add(lipsyncToggle);

            content.Add(row3);
            
            // Add content to main container
            container.Add(content);

            // === ACTION BAR ===
            var actions = new VisualElement();
            actions.AddToClassList("slot-action-bar");

            var playBtn = new Button(() => TestPlay(slot));
            playBtn.text = "▶";
            playBtn.tooltip = "Test Play";
            playBtn.AddToClassList("slot-run-btn");
            playBtn.SetEnabled(slot.clip != null);
            actions.Add(playBtn);

            var stopBtn = new Button(AudioUtilWrapper.StopAllClips);
            stopBtn.text = "■";
            stopBtn.tooltip = "Stop";
            stopBtn.AddToClassList("slot-stop-btn");
            actions.Add(stopBtn);

            container.RegisterCallback<DragUpdatedEvent>(evt =>
            {
                if (DragAndDrop.objectReferences.Any(o => o is AudioClip))
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    evt.StopPropagation();
                }
            });

            container.RegisterCallback<DragPerformEvent>(evt =>
            {
                var clip = DragAndDrop.objectReferences.FirstOrDefault(o => o is AudioClip) as AudioClip;
                if (clip != null)
                {
                    DragAndDrop.AcceptDrag();
                    AssignClipToSlot(source, slot, clip);
                    evt.StopPropagation();
                }
            });

            content.Add(actions);
            
            // Add content to main container
            container.Add(content);
            
            return container;
        }

        private void AssignClipToSlot(VarcoDialoguePlayer source, VarcoDialoguePlayer.DialogueSlot slot, AudioClip clip)
        {
            Undo.RecordObject(source, "Assign Slot Clip");
            slot.clip = clip;
            if (slot.clip != null)
            {
                GenerateVisemeData(source, slot);
            }
            EditorUtility.SetDirty(source);
            RefreshObjectList();
            ShowDetailPanel(source);
        }

        private VisualElement CreateSection(string title)
        {
            var section = new VisualElement();
            section.style.marginBottom = 8;
            
            var label = new Label(title);
            label.style.fontSize = 9;
            label.style.color = new Color(0.5f, 0.5f, 0.5f);
            label.style.marginBottom = 4;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            section.Add(label);
            
            return section;
        }

        private VisualElement CreateFormRow(string labelText, VisualElement field)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 6;

            var label = new Label(labelText);
            label.style.width = 80;
            label.style.color = new Color(0.6f, 0.6f, 0.6f);
            row.Add(label);

            field.style.flexGrow = 1;
            row.Add(field);

            return row;
        }

        private void AssignCurrentClip(VarcoDialoguePlayer source, VarcoDialoguePlayer.DialogueSlot slot)
        {
            if (_currentClip == null || source == null || slot == null) return;

            Undo.RecordObject(source, "Assign Current Clip");
            slot.clip = _currentClip;
            
            // Auto-generate Viseme Data
            GenerateVisemeData(source, slot);
            
            EditorUtility.SetDirty(source);

            RefreshObjectList();
            ShowDetailPanel(source);
        }
        
        /// <summary>
        /// Generates Viseme Data from AudioClip using LipSyncAnalyzer
        /// </summary>
        private void GenerateVisemeData(VarcoDialoguePlayer source, VarcoDialoguePlayer.DialogueSlot slot)
        {
            if (slot.clip == null) return;
            
            try
            {
                // Use EnhancedLipSyncAnalyzer for better quality (Formant-based analysis)
                var analyzer = new EnhancedLipSyncAnalyzer();
                slot.visemeData = analyzer.AnalyzeEnhanced(slot.clip, 60f); // Higher frame rate for smoother animation
                
                EditorUtility.SetDirty(source);
                Debug.Log($"[VARCO] Generated ENHANCED Viseme Data: {slot.visemeData.Keyframes.Count} keyframes from '{slot.clip.name}'");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[VARCO] Failed to generate Enhanced Viseme Data: {ex.Message}");
            }
        }

        private void AddNewSlot(VarcoDialoguePlayer source)
        {
            if (source == null) return;

            Undo.RecordObject(source, "Add Dialogue Slot");
            source.dialogueSlots.Add(new VarcoDialoguePlayer.DialogueSlot());
            EditorUtility.SetDirty(source);

            RefreshObjectList();
            ShowDetailPanel(source);
        }

        private void TestPlay(VarcoDialoguePlayer.DialogueSlot slot)
        {
            if (slot == null || slot.clip == null) return;
            AudioUtilWrapper.PlayClip(slot.clip, 0, false);
        }

        private void CreateNewGameObject()
        {
            var go = new GameObject("Varco Player");
            var audioSource = go.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;

            var varcoSource = go.AddComponent<VarcoDialoguePlayer>();

            Undo.RegisterCreatedObjectUndo(go, "Create Varco Player");
            Selection.activeGameObject = go;

            RefreshAll();
            SelectSource(varcoSource);
        }

        #region Library

        private void RefreshLibrary()
        {
            if (_libraryList == null) return;

            _libraryList.Clear();

            // Ensure folder exists
            if (!AssetDatabase.IsValidFolder(LIBRARY_FOLDER))
            {
                var empty = new Label("No exports yet.\nUse 'Export to Library' from TTS or DSP tab.");
                empty.AddToClassList("placeholder-text");
                empty.style.whiteSpace = WhiteSpace.Normal;
                _libraryList.Add(empty);
                return;
            }

            var guids = AssetDatabase.FindAssets("t:AudioClip", new[] { LIBRARY_FOLDER });
            if (guids.Length == 0)
            {
                var empty = new Label("No exports yet.\nUse 'Export to Library' from TTS or DSP tab.");
                empty.AddToClassList("placeholder-text");
                empty.style.whiteSpace = WhiteSpace.Normal;
                _libraryList.Add(empty);
                return;
            }

            // Load clips
            var clips = new System.Collections.Generic.List<(AudioClip clip, string path)>();
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip != null)
                {
                    clips.Add((clip, path));
                }
            }

            // Sort based on mode
            switch (_currentSortMode)
            {
                case LibrarySortMode.Name:
                    clips.Sort((a, b) => string.Compare(a.clip.name, b.clip.name, System.StringComparison.OrdinalIgnoreCase));
                    break;
                case LibrarySortMode.Duration:
                    clips.Sort((a, b) => a.clip.length.CompareTo(b.clip.length));
                    break;
                case LibrarySortMode.Recent:
                    clips.Sort((a, b) => System.IO.File.GetLastWriteTime(b.path).CompareTo(System.IO.File.GetLastWriteTime(a.path)));
                    break;
            }

            foreach (var (clip, _) in clips)
            {
                _libraryList.Add(CreateLibraryItem(clip));
            }
        }

        private VisualElement CreateLibraryItem(AudioClip clip)
        {
            var item = new VisualElement();
            item.AddToClassList("library-item");
            item.userData = clip;

            // Use simple dash instead of emoji (emoji may not render properly)
            var icon = new Label("-");
            icon.AddToClassList("library-item-icon");
            icon.style.unityFontStyleAndWeight = FontStyle.Bold;
            item.Add(icon);

            var name = new Label(clip.name);
            name.AddToClassList("library-item-name");
            item.Add(name);

            var duration = new Label($"{clip.length:F1}s");
            duration.AddToClassList("library-item-duration");
            item.Add(duration);

            // === Actions ===
            var actions = new VisualElement();
            actions.AddToClassList("library-item-actions");
            
            var playBtn = new Button(() => {
                AudioUtilWrapper.StopAllClips();
                AudioUtilWrapper.PlayClip(clip, 0, false);
            });
            playBtn.AddToClassList("library-btn");
            playBtn.AddToClassList("library-btn--play");
            playBtn.text = "▶";
            actions.Add(playBtn);
            
            var deleteBtn = new Button(() => DeleteLibraryFile(clip));
            deleteBtn.AddToClassList("library-btn");
            deleteBtn.AddToClassList("library-btn--delete");
            deleteBtn.text = "X";
            actions.Add(deleteBtn);
            
            item.Add(actions);

            // Make draggable
            item.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button == 0)
                {
                    DragAndDrop.PrepareStartDrag();
                    DragAndDrop.objectReferences = new Object[] { clip };
                    DragAndDrop.StartDrag(clip.name);
                    evt.StopPropagation();
                }
            });

            // Play on double-click
            item.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.clickCount == 2)
                {
                    AudioUtilWrapper.StopAllClips();
                    AudioUtilWrapper.PlayClip(clip, 0, false);
                }
            });

            return item;
        }

        private void DeleteLibraryFile(AudioClip clip)
        {
            if (clip == null) return;
            string path = AssetDatabase.GetAssetPath(clip);
            
            if (EditorUtility.DisplayDialog("Delete File", 
                $"Are you sure you want to delete '{clip.name}'?\nThis cannot be undone.", 
                "Delete", "Cancel"))
            {
                AssetDatabase.DeleteAsset(path);
                RefreshLibrary();
            }
        }

        public static void ExportClipToLibrary(AudioClip clip, string clipName, DSP.DSPChain chain = null)
        {
            if (clip == null) return;

            // Ensure folder exists
            EnsureLibraryFolder();

            // If chain provided, bake DSP effects into clip
            AudioClip exportClip = chain != null ? BakeClipWithDSP(clip, chain) : clip;

            string path = $"{LIBRARY_FOLDER}/{clipName}.wav";
            
            // Check for duplicates
            int counter = 1;
            while (File.Exists(path))
            {
                path = $"{LIBRARY_FOLDER}/{clipName}_{counter}.wav";
                counter++;
            }

            // Save WAV using direct implementation
            SaveClipToWav(exportClip, path);
            AssetDatabase.ImportAsset(path);
            AssetDatabase.Refresh();
            
            Debug.Log($"[Export] Saved to: {path}");
        }

        public static void ExportClipToPath(AudioClip clip, string outputPath, DSP.DSPChain chain = null)
        {
            if (clip == null || string.IsNullOrEmpty(outputPath)) return;

            AudioClip exportClip = chain != null ? BakeClipWithDSP(clip, chain) : clip;
            SaveClipToWav(exportClip, outputPath);
            AssetDatabase.Refresh();

            Debug.Log($"[Export] Saved to: {outputPath}");
        }

        /// <summary>
        /// Ensures the library folder exists, creating it if necessary.
        /// </summary>
        public static void EnsureLibraryFolder()
        {
            if (!AssetDatabase.IsValidFolder(LIBRARY_FOLDER))
            {
                // Create parent folders if needed
                string[] parts = LIBRARY_FOLDER.Split('/');
                string current = parts[0]; // "Assets"
                for (int i = 1; i < parts.Length; i++)
                {
                    string next = $"{current}/{parts[i]}";
                    if (!AssetDatabase.IsValidFolder(next))
                    {
                        AssetDatabase.CreateFolder(current, parts[i]);
                    }
                    current = next;
                }
            }
        }

        /// <summary>
        /// Creates a new AudioClip with all DSP effects baked in, including effect tails (reverb, delay).
        /// </summary>
        public static AudioClip BakeClipWithDSP(AudioClip source, DSP.DSPChain chain)
        {
            if (source == null || chain == null) return source;

            int sampleRate = source.frequency;
            int channels = source.channels;
            int sourceSamples = source.samples * channels;

            // Get source data
            float[] sourceData = new float[sourceSamples];
            source.GetData(sourceData, 0);

            // Allocate extra buffer for tails (up to 10 seconds max)
            int maxTailSamples = sampleRate * channels * 10;
            float[] workBuffer = new float[sourceSamples + maxTailSamples];
            Array.Copy(sourceData, workBuffer, sourceSamples);
            
            // Fill tail section with silence (effects will produce their own tail)
            Array.Clear(workBuffer, sourceSamples, maxTailSamples);            // Process through DSP chain effects (offline)
            var effects = chain.Effects;
            foreach (var effect in effects)
            {
                if (effect == null || !effect.Enabled) continue;

                try
                {
                    effect.Process(workBuffer, channels, sampleRate);
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[BakeClipWithDSP] Effect '{effect.Name}' error: {ex.Message}");
                }
            }

            var masterEQ = chain.MasterEQ;
            if (masterEQ != null && masterEQ.Enabled)
            {
                try
                {
                    masterEQ.Process(workBuffer, channels, sampleRate);
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[BakeClipWithDSP] Master EQ error: {ex.Message}");
                }
            }

            // Find where the tail ends (signal falls below threshold)
            const float SILENCE_THRESHOLD = 0.0001f; // -80dB
            int tailEnd = sourceSamples;
            int silenceCount = 0;
            int silenceRequired = sampleRate / 10; // 100ms of silence required to confirm end
            
            for (int i = sourceSamples; i < workBuffer.Length; i++)
            {
                float abs = workBuffer[i] > 0 ? workBuffer[i] : -workBuffer[i];
                if (abs < SILENCE_THRESHOLD)
                {
                    silenceCount++;
                    if (silenceCount >= silenceRequired)
                    {
                        tailEnd = i - silenceRequired + 1;
                        break;
                    }
                }
                else
                {
                    silenceCount = 0;
                    tailEnd = i + 1;
                }
            }

            // Create final output with exact length
            int finalSampleCount = tailEnd / channels;
            float[] finalData = new float[tailEnd];
            Array.Copy(workBuffer, finalData, tailEnd);

            AudioClip bakedClip = AudioClip.Create(
                $"{source.name}_dsp",
                finalSampleCount,
                channels,
                sampleRate,
                false
            );
            bakedClip.SetData(finalData, 0);

            Debug.Log($"[Export] Original: {source.samples / (float)sampleRate:F2}s, Baked: {finalSampleCount / (float)sampleRate:F2}s (tail: {(finalSampleCount - source.samples) / (float)sampleRate:F2}s)");

            return bakedClip;
        }

        private static void SaveClipToWav(AudioClip clip, string filepath)
        {
            if (clip == null) return;

            var samples = new float[clip.samples * clip.channels];
            clip.GetData(samples, 0);

            Directory.CreateDirectory(Path.GetDirectoryName(filepath));

            using (var fileStream = new FileStream(filepath, FileMode.Create))
            using (var writer = new BinaryWriter(fileStream))
            {
                int sampleRate = clip.frequency;
                int channels = clip.channels;
                short bitsPerSample = 16;
                int byteRate = sampleRate * channels * (bitsPerSample / 8);
                short blockAlign = (short)(channels * (bitsPerSample / 8));
                int dataSize = samples.Length * 2;

                // RIFF header
                writer.Write(System.Text.Encoding.UTF8.GetBytes("RIFF"));
                writer.Write(36 + dataSize);
                writer.Write(System.Text.Encoding.UTF8.GetBytes("WAVE"));

                // fmt chunk
                writer.Write(System.Text.Encoding.UTF8.GetBytes("fmt "));
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)channels);
                writer.Write(sampleRate);
                writer.Write(byteRate);
                writer.Write(blockAlign);
                writer.Write(bitsPerSample);

                // data chunk
                writer.Write(System.Text.Encoding.UTF8.GetBytes("data"));
                writer.Write(dataSize);

                // Convert float to Int16
                for (int i = 0; i < samples.Length; i++)
                {
                    float sample = Mathf.Clamp(samples[i], -1f, 1f);
                    writer.Write((short)(sample * 32767f));
                }
            }
        }

        #endregion

        public void Cleanup()
        {
            EditorApplication.hierarchyChanged -= RefreshObjectList;
        }
    }
}

