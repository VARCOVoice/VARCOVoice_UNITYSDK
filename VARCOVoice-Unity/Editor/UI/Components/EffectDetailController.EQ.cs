using System.Collections.Generic;
using UnityEditor;
using UnityEngine.UIElements;
using VARCOVoice.DSP;
using VARCOVoice.Editor.Services;

namespace VARCOVoice.Editor
{
    public partial class EffectDetailController
    {
        private EQVisualizerController _eqVisualizer;
        internal void BuildParametricEQUI(ParametricEQ16 eq, HashSet<string> excluded)
        {
            _eqVisualizer = null;

            // Direct instantiation to use full width of ContentContainer
            // Bypassing BuildStandard3ZoneUI constraints
            var assetPath = "Packages/com.varco.voice/Editor/UI/Components/EQVisualizerPanel.uxml";
            var eqAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(assetPath);
            
            if (eqAsset != null)
            {
                var eqContainer = eqAsset.Instantiate();
                var stylePath = assetPath.Replace(".uxml", ".uss");
                var eqStyle = AssetDatabase.LoadAssetAtPath<StyleSheet>(stylePath);
                if (eqStyle != null) eqContainer.styleSheets.Add(eqStyle);
                
                // Hide Level Meters as requested
                var meters = eqContainer.Q("meters-panel");
                if (meters != null) meters.style.display = DisplayStyle.None;

                eqContainer.style.flexGrow = 1;
                eqContainer.style.height = 350;
                eqContainer.style.minHeight = 350;
                
                // Add directly to main content container for full width
                ContentContainer.Add(eqContainer);
                
                _eqVisualizer = new EQVisualizerController();
                _eqVisualizer.Initialize(eqContainer, eq, () => AudioAnalysisService.SmoothSpectrum);

                // Schedule update on the container
                var item = eqContainer.schedule.Execute(UpdateEQVisualization).Every(30);
                TrackScheduledItem(item);
            }
            else
            {
                ContentContainer.Add(new Label("EQ Visualizer UXML not found"));
            }
            
            excluded.Add("OutputGain");
            excluded.Add("Gain");
            excluded.Add("Mix");
        }
        
        private EffectParameter GetParameterByName(IDSPEffect effect, string name)
        {
            var paramsList = GetParameters(effect);
            return paramsList.Find(p => p.Name == name);
        }
        
        private void UpdateEQVisualization()
        {
            _eqVisualizer?.OnUpdate();
        }
    }
}
