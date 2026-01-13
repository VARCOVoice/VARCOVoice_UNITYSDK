using VARCOVoice.Editor.Services;

namespace VARCOVoice.Editor
{
    public partial class DSPPanelController
    {
        private void UpdateAudioAnalysis(float deltaTime)
        {
            if (_target == null) return;
            AudioAnalysisService.Update(_target, deltaTime, true);
        }

        private void DecayLevels()
        {
            if (_target == null) return;
            AudioAnalysisService.Update(_target, _analysisDeltaTime, false);
        }

        private bool HasVisualizerActivity()
        {
            return AudioAnalysisService.HasActivity();
        }
    }
}
