using UnityEngine;
using UnityEngine.UIElements;

namespace VARCOVoice.Editor
{
    public class WaveformRenderController
    {
        private VisualElement _waveformImage;
        private VisualElement _playhead;
        private int _textureWidth = 2048; // Increased from 512 for sharpness
        private int _textureHeight = 256; // Increased from 64
        private Color _waveformColor = new Color(0.0f, 0.8f, 1.0f, 1.0f); // Cyan/Blue
        private Color _backgroundColor = new Color(0.0f, 0.0f, 0.0f, 0.0f); // Transparent

        public void Initialize(VisualElement waveformImage, VisualElement playhead)
        {
            _waveformImage = waveformImage;
            _playhead = playhead;
        }

        public void RenderWaveform(AudioClip clip)
        {
            if (_waveformImage == null || clip == null) return;

            Texture2D texture = GenerateWaveformTexture(clip, _textureWidth, _textureHeight, _waveformColor, _backgroundColor);
            _waveformImage.style.backgroundImage = new StyleBackground(texture);
        }

        public void UpdatePlayhead(float progress) // 0.0 to 1.0
        {
            if (_playhead == null) return;
            
            // Clamp progress
            progress = Mathf.Clamp01(progress);
            
            // Set position (percentage)
            // Optimization: Use Translate instead of Left to avoid Layout Reflow
            _playhead.style.translate = new Translate(new Length(progress * 100, LengthUnit.Percent), 0);
            _playhead.style.left = 0; // Ensure base position is 0
        }

        private Texture2D GenerateWaveformTexture(AudioClip clip, int width, int height, Color waveColor, Color bgColor)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear; // Ensure smooth quality

            Color[] pixels = new Color[width * height];

            // Fill background
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = bgColor;

            // Ensure data is loaded
            if (clip.loadState != AudioDataLoadState.Loaded)
            {
                if (!clip.LoadAudioData())
                {
                    Debug.LogError("[Waveform] Failed to load audio data");
                    return texture;
                }
            }

            float[] samples = new float[clip.samples * clip.channels];
            clip.GetData(samples, 0);
            
            // Debug info
            // Debug.Log($"[Waveform] Samples: {samples.Length}, Channels: {clip.channels}");

            int packSize = (samples.Length / width) + 1;
            int halfHeight = height / 2;

            for (int x = 0; x < width; x++)
            {
                float max = 0;
                
                // Find max amplitude in this chunk
                int startSample = x * packSize;
                int endSample = Mathf.Min(startSample + packSize, samples.Length);
                
                for (int i = startSample; i < endSample; i++)
                {
                    float val = Mathf.Abs(samples[i]);
                    if (val > max) max = val;
                }
                
                // Boost visualization for visibility (linear gain)
                max = Mathf.Clamp01(max * 1.5f); 

                // Draw vertical line from center
                int barHeight = (int)(max * halfHeight);
                if (barHeight < 1 && max > 0.001f) barHeight = 1; // Minimum dot for non-silent

                for (int y = halfHeight - barHeight; y <= halfHeight + barHeight; y++)
                {
                    if (y >= 0 && y < height)
                        pixels[y * width + x] = waveColor;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }
    }
}
