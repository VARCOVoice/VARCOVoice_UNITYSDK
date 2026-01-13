using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using System.IO;

namespace VARCOVoice.Editor
{
    /// <summary>
    /// Clean popup window for viewing licenses and third-party notices
    /// </summary>
    public class LicenseViewerWindow : EditorWindow
    {
        private static readonly Vector2 WindowSize = new Vector2(550, 600);
        
        [MenuItem("Window/VARCO Voice/Licenses & Credits", false, 200)]
        public static void ShowWindow()
        {
            var window = GetWindow<LicenseViewerWindow>();
            window.titleContent = new GUIContent("Licenses & Credits");
            window.minSize = WindowSize;
            window.maxSize = new Vector2(800, 800);
            window.Show();
        }
        
        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.backgroundColor = new Color(0.1f, 0.11f, 0.14f);
            root.style.paddingTop = 16;
            root.style.paddingBottom = 16;
            root.style.paddingLeft = 20;
            root.style.paddingRight = 20;
            
            // Header
            var header = new Label("VARCO Voice - Licenses & Credits");
            header.style.fontSize = 18;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.color = Color.white;
            header.style.marginBottom = 16;
            root.Add(header);
            
            // Scroll container
            var scrollView = new ScrollView(ScrollViewMode.Vertical);
            scrollView.style.flexGrow = 1;
            root.Add(scrollView);
            
            // VARCO Voice License
            AddSection(scrollView, "VARCO Voice SDK", 
                "Copyright (c) 2024 NC AI\nAll Rights Reserved\n\nThis SDK is provided under NC AI's proprietary license.\nFor commercial use, please contact NC AI.", 
                "#006EFF");
            
            // Third Party Libraries
            AddSection(scrollView, "Newtonsoft.Json (MIT License)", 
                "Copyright (c) 2007 James Newton-King\n\nPermission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files, to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software.\n\nhttps://github.com/JamesNK/Newtonsoft.Json", 
                "#00D68F");
            
            AddSection(scrollView, "UniTask (MIT License)", 
                "Copyright (c) 2019 Cysharp, Inc.\n\nPermission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files, to deal in the Software without restriction.\n\nhttps://github.com/Cysharp/UniTask", 
                "#00D68F");
            
            // Fonts
            AddSection(scrollView, "Google Sans Flex (SIL OFL 1.1)", 
                "Copyright (c) Google Inc.\n\nThis Font Software is licensed under the SIL Open Font License, Version 1.1. This license allows the licensed fonts to be used, studied, modified and redistributed freely.", 
                "#FFD700");
            
            AddSection(scrollView, "Inter (SIL OFL 1.1)", 
                "Copyright (c) 2016-2020 The Inter Project Authors\n\nThis Font Software is licensed under the SIL Open Font License, Version 1.1.", 
                "#FFD700");
            
            // Unity Packages
            AddSection(scrollView, "Unity Packages (Unity Companion License)", 
                "The following packages are dependencies:\n• com.unity.burst (Unity Burst Compiler)\n• com.unity.mathematics (Unity Mathematics)\n• com.unity.collections (Unity Collections)\n\nhttps://unity.com/legal/licenses/unity-companion-license", 
                "#888888");
            
            // Footer buttons
            var buttonRow = new VisualElement();
            buttonRow.style.flexDirection = FlexDirection.Row;
            buttonRow.style.marginTop = 16;
            buttonRow.style.justifyContent = Justify.FlexEnd;
            
            var openFullBtn = new Button(() => OpenFullLicenseFile()) { text = "Open Full License File" };
            openFullBtn.style.height = 30;
            openFullBtn.style.paddingLeft = 16;
            openFullBtn.style.paddingRight = 16;
            openFullBtn.style.backgroundColor = new Color(0.17f, 0.18f, 0.21f);
            openFullBtn.style.borderTopWidth = 1;
            openFullBtn.style.borderBottomWidth = 1;
            openFullBtn.style.borderLeftWidth = 1;
            openFullBtn.style.borderRightWidth = 1;
            openFullBtn.style.borderTopColor = new Color(1, 1, 1, 0.15f);
            openFullBtn.style.borderBottomColor = new Color(1, 1, 1, 0.15f);
            openFullBtn.style.borderLeftColor = new Color(1, 1, 1, 0.15f);
            openFullBtn.style.borderRightColor = new Color(1, 1, 1, 0.15f);
            openFullBtn.style.borderTopLeftRadius = 4;
            openFullBtn.style.borderTopRightRadius = 4;
            openFullBtn.style.borderBottomLeftRadius = 4;
            openFullBtn.style.borderBottomRightRadius = 4;
            openFullBtn.style.color = new Color(1, 1, 1, 0.8f);
            openFullBtn.style.marginRight = 8;
            buttonRow.Add(openFullBtn);
            
            var closeBtn = new Button(() => Close()) { text = "Close" };
            closeBtn.style.height = 30;
            closeBtn.style.paddingLeft = 24;
            closeBtn.style.paddingRight = 24;
            closeBtn.style.backgroundColor = new Color(0, 0.43f, 1);
            closeBtn.style.borderTopWidth = 0;
            closeBtn.style.borderBottomWidth = 0;
            closeBtn.style.borderLeftWidth = 0;
            closeBtn.style.borderRightWidth = 0;
            closeBtn.style.borderTopLeftRadius = 4;
            closeBtn.style.borderTopRightRadius = 4;
            closeBtn.style.borderBottomLeftRadius = 4;
            closeBtn.style.borderBottomRightRadius = 4;
            closeBtn.style.color = Color.white;
            closeBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            buttonRow.Add(closeBtn);
            
            root.Add(buttonRow);
        }
        
        private void AddSection(ScrollView parent, string title, string content, string accentColor)
        {
            var section = new VisualElement();
            section.style.backgroundColor = new Color(0.13f, 0.15f, 0.18f);
            section.style.borderTopLeftRadius = 6;
            section.style.borderTopRightRadius = 6;
            section.style.borderBottomLeftRadius = 6;
            section.style.borderBottomRightRadius = 6;
            section.style.marginBottom = 12;
            section.style.paddingTop = 12;
            section.style.paddingBottom = 12;
            section.style.paddingLeft = 14;
            section.style.paddingRight = 14;
            section.style.borderLeftWidth = 3;
            
            if (ColorUtility.TryParseHtmlString(accentColor, out Color color))
            {
                section.style.borderLeftColor = color;
            }
            
            var titleLabel = new Label(title);
            titleLabel.style.fontSize = 12;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = Color.white;
            titleLabel.style.marginBottom = 6;
            section.Add(titleLabel);
            
            var contentLabel = new Label(content);
            contentLabel.style.fontSize = 11;
            contentLabel.style.color = new Color(1, 1, 1, 0.7f);
            contentLabel.style.whiteSpace = WhiteSpace.Normal;
            section.Add(contentLabel);
            
            parent.Add(section);
        }
        
        private void OpenFullLicenseFile()
        {
            var paths = new[]
            {
                "Packages/com.varco.voice/ThirdPartyNotices.md",
                "Assets/VARCOVoice/ThirdPartyNotices.md"
            };
            
            foreach (var path in paths)
            {
                var fullPath = Path.GetFullPath(path);
                if (File.Exists(fullPath))
                {
                    EditorUtility.RevealInFinder(fullPath);
                    return;
                }
            }
            
            // Try finding via GUID
            var guids = AssetDatabase.FindAssets("ThirdPartyNotices t:TextAsset");
            if (guids.Length > 0)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                EditorUtility.RevealInFinder(Path.GetFullPath(assetPath));
            }
            else
            {
                EditorUtility.DisplayDialog("Not Found", "ThirdPartyNotices.md file not found.", "OK");
            }
        }
    }
}
