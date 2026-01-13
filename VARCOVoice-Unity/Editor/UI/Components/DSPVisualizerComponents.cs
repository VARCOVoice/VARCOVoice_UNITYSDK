using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace VARCOVoice.Editor.UI
{
    /// <summary>
    /// Base class for DSP visualizers using Painter2D
    /// </summary>
    public abstract class DSPVisualizerBase : VisualElement
    {
        protected DSPVisualizerBase()
        {
            generateVisualContent += OnGenerateVisualContent;
            style.flexGrow = 1;
            style.overflow = Overflow.Hidden; // Keep drawing within bounds
        }
         
        protected abstract void OnGenerateVisualContent(MeshGenerationContext ctx);
        
        public new void MarkDirtyRepaint()
        {
            base.MarkDirtyRepaint();
        }
    }

    /// <summary>
    /// Visualizes delay echoes fading into distance
    /// </summary>
    public class DelayVisualizer : DSPVisualizerBase
    {
        private float _timeMs = 500f;
        private float _feedback = 50f;
        private readonly Label[] _timeLabels = new Label[5];
        
        public float TimeMs 
        { 
            get => _timeMs; 
            set { _timeMs = value; UpdateLabels(); MarkDirtyRepaint(); } 
        }
        
        public float Feedback 
        { 
            get => _feedback; 
            set { _feedback = value; MarkDirtyRepaint(); } 
        }

        public DelayVisualizer()
        {
            // Create 5 time labels
            for (int i = 0; i < 5; i++)
            {
                var label = new Label();
                label.style.position = Position.Absolute;
                label.style.fontSize = 9;
                label.style.color = new Color(0.6f, 0.7f, 0.7f, 0.8f);
                label.style.unityTextAlign = TextAnchor.MiddleCenter;
                label.style.bottom = 2;
                _timeLabels[i] = label;
                Add(label);
            }
            
            RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        }
        
        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            UpdateLabelPositions();
        }
        
        private void UpdateLabels()
        {
            for (int i = 0; i < 5; i++)
            {
                float echoTimeMs = _timeMs * (i + 1);
                string text;
                if (echoTimeMs >= 1000f)
                    text = $"{echoTimeMs / 1000f:F1}s";
                else
                    text = $"{echoTimeMs:F0}ms";
                _timeLabels[i].text = text;
            }
            UpdateLabelPositions();
        }
        
        private void UpdateLabelPositions()
        {
            float w = resolvedStyle.width;
            if (float.IsNaN(w) || w < 10) return;
            
            // Leave 10% margin at start, fit 5 echoes in remaining 85%
            float margin = w * 0.08f;
            float usableWidth = w * 0.84f;
            float spacing = usableWidth / 5f;
            
            for (int i = 0; i < 5; i++)
            {
                float x = margin + spacing * (i + 0.5f);
                _timeLabels[i].style.left = x - 20;
                _timeLabels[i].style.width = 40;
            }
        }

        protected override void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            var painter = ctx.painter2D;
            var rect = contentRect;
            float w = rect.width;
            float h = rect.height;

            if (w < 10 || h < 10) return;

            // Draw baseline
            painter.strokeColor = new Color(1f, 1f, 1f, 0.2f);
            painter.lineWidth = 1f;
            painter.BeginPath();
            painter.MoveTo(new Vector2(0, h/2));
            painter.LineTo(new Vector2(w, h/2));
            painter.Stroke();

            // 5 echoes visible with margins
            float margin = w * 0.08f;
            float usableWidth = w * 0.84f;
            float spacing = usableWidth / 5f;
            
            float x = margin + spacing * 0.5f;
            float amp = _feedback / 100f;
            
            painter.lineWidth = 5f;
            painter.lineCap = LineCap.Round;

            for (int i = 0; i < 5 && amp > 0.02f; i++)
            {
                float barHeight = (h * 0.5f) * amp;
                float alpha = Mathf.Max(0.3f, amp);
                
                painter.strokeColor = new Color(0.2f, 0.8f, 0.7f, alpha);
                painter.BeginPath();
                painter.MoveTo(new Vector2(x, h/2 - barHeight/2));
                painter.LineTo(new Vector2(x, h/2 + barHeight/2));
                painter.Stroke();

                x += spacing;
                amp *= (_feedback / 100f);
            }
        }
    }

    /// <summary>
    /// Visualizes LFO waveform (Sine, Triangle, etc) scrolling
    /// </summary>
    public class LFOVisualizer : DSPVisualizerBase
    {
        public float Rate { get; set; } = 1f;
        public float Depth { get; set; } = 50f;
        
        // We'll just draw a static wave representing the shape/density
        // Animation could be added via schedule.Execute
        
        private float _phase = 0f;
        
        public LFOVisualizer()
        {
            // Animate
            schedule.Execute(() => {
                _phase += 0.05f * Rate; // Speed depends on rate
                MarkDirtyRepaint();
            }).Every(16); // ~60fps
        }

        protected override void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            var painter = ctx.painter2D;
            var rect = contentRect;
            float w = rect.width;
            float h = rect.height;
            float midY = h / 2f;

            painter.strokeColor = new Color(0.8f, 0.4f, 0.8f, 0.8f);
            painter.lineWidth = 2f;
            painter.BeginPath();

            float depthScale = (Depth / 100f) * (h * 0.4f);
            
            // Draw Sine wave
            bool first = true;
            int points = 100;
            
            // Frequency of wave visual increases with Rate
            float freq = Mathf.Clamp(Rate, 0.1f, 10f); 

            for(int i=0; i<points; i++)
            {
                float t = i / (float)(points - 1);
                float x = t * w;
                
                // Scrolling phase
                float angle = (t * freq * 2f * Mathf.PI) - (_phase * 5f);
                float y = midY + Mathf.Sin(angle) * depthScale;

                if(first) { painter.MoveTo(new Vector2(x, y)); first = false; }
                else painter.LineTo(new Vector2(x, y));
            }
            painter.Stroke();
        }
    }

    /// <summary>
    /// Visualizes Reverb decay curve
    /// </summary>
    public class ReverbVisualizer : DSPVisualizerBase
    {
        public float DecayTime { get; set; } = 2f;
        
        protected override void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            var painter = ctx.painter2D;
            var rect = contentRect;
            float w = rect.width;
            float h = rect.height;

            // Draw exponential decay curve
            painter.strokeColor = new Color(0.3f, 0.6f, 1f, 0.8f);
            painter.fillColor = new Color(0.3f, 0.6f, 1f, 0.2f);
            painter.lineWidth = 2f;

            painter.BeginPath();
            painter.MoveTo(new Vector2(0, h)); // Start bottom left

            int points = 50;
            for(int i=0; i<points; i++)
            {
                float t = i / (float)(points - 1);
                float x = t * w;
                
                // x represents time. w represents maybe 5 seconds?
                float visibleTime = 5f;
                float currentTime = t * visibleTime;
                
                // Simple exponential decay: exp(-t / decay)
                float env = Mathf.Exp(-currentTime * 3f / Mathf.Max(0.1f, DecayTime));
                float y = h - (env * h * 0.9f); // 0.9 to leave some headroom

                painter.LineTo(new Vector2(x, y));
            }
            
            painter.LineTo(new Vector2(w, h));
            painter.ClosePath();
            painter.Fill();
            
            // Stroke the top edge
            painter.BeginPath();
            bool first = true;
            for(int i=0; i<points; i++)
            {
                float t = i / (float)(points - 1);
                float x = t * w;
                float visibleTime = 5f;
                float currentTime = t * visibleTime;
                float env = Mathf.Exp(-currentTime * 3f / Mathf.Max(0.1f, DecayTime));
                float y = h - (env * h * 0.9f);

                if (first) { painter.MoveTo(new Vector2(x, y)); first = false; }
                else painter.LineTo(new Vector2(x, y));
            }
            painter.Stroke();
        }
    }

    /// <summary>
    /// Visualizes Pitch Shift amount
    /// </summary>
    public class PitchVisualizer : DSPVisualizerBase
    {
        public float PitchSemitones { get; set; } = 0f;

        protected override void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            var painter = ctx.painter2D;
            var rect = contentRect;
            float w = rect.width;
            float h = rect.height;
            float midY = h / 2f;
            float midX = w / 2f;

            // Draw Center line
            painter.strokeColor = new Color(1f, 1f, 1f, 0.3f);
            painter.lineWidth = 1f;
            painter.BeginPath();
            painter.MoveTo(new Vector2(0, midY));
            painter.LineTo(new Vector2(w, midY));
            painter.Stroke();

            // Draw Bar
            float maxPitch = 12f;
            float normalizedPitch = Mathf.Clamp(PitchSemitones, -maxPitch, maxPitch) / maxPitch; // -1 to 1
            
            float barHeight = normalizedPitch * (h * 0.45f); // Height from center
            
            // Color based on pitch direction (Gold/Orange for Up, Blue/Cyan for Down)
            Color barColor = (PitchSemitones >= 0) ? new Color(1f, 0.8f, 0.2f) : new Color(0.2f, 0.7f, 1f);
            
            painter.fillColor = barColor; // Transparent fill
            
            float barWidth = w * 0.3f;
            float barLeft = midX - barWidth / 2f;
            
            // Rect(x, y, w, h)
            // If pitch > 0, rect starts at midY - barHeight, height is barHeight
            // If pitch < 0, rect starts at midY, height is -barHeight (abs)
            
            float rectY = (PitchSemitones >= 0) ? midY - barHeight : midY;
            float rectH = Mathf.Abs(barHeight);
            
            painter.BeginPath();
            painter.MoveTo(new Vector2(barLeft, rectY));
            painter.LineTo(new Vector2(barLeft + barWidth, rectY));
            painter.LineTo(new Vector2(barLeft + barWidth, rectY + rectH));
            painter.LineTo(new Vector2(barLeft, rectY + rectH));
            painter.ClosePath();
            painter.Fill();

            // Text value (optional, simpler to just have bar)
        }
    }

    /// <summary>
    /// Visualizes Tube Saturation curve
    /// </summary>
    public class TubeVisualizer : DSPVisualizerBase
    {
        public float Drive { get; set; } = 0f;
        public float Bias { get; set; } = 0f;

        protected override void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            var painter = ctx.painter2D;
            var rect = contentRect;
            float w = rect.width;
            float h = rect.height;
            float midX = w / 2f;
            float midY = h / 2f;

            // Draw Grid (Axes)
            painter.strokeColor = new Color(1f, 1f, 1f, 0.1f);
            painter.lineWidth = 1f;
            painter.BeginPath();
            painter.MoveTo(new Vector2(midX, 0)); painter.LineTo(new Vector2(midX, h));
            painter.MoveTo(new Vector2(0, midY)); painter.LineTo(new Vector2(w, midY));
            painter.Stroke();

            // Draw Transfer Curve
            painter.strokeColor = new Color(1f, 0.4f, 0.2f, 0.9f);
            painter.lineWidth = 2.5f;
            painter.BeginPath();

            // Simulate saturation curve: y = tanh(x * drive + bias)
            // Drive in dB -> linear gain
            // range -12 to 24 dB. 0dB = 1.
            float gain = Mathf.Pow(10f, Drive / 20f);
            // Limit gain for visual sanity
            gain = Mathf.Clamp(gain, 0.1f, 5f);
            
            bool first = true;
            int points = 60;
            for(int i=0; i<points; i++)
            {
                float t = i / (float)(points - 1);
                // Input range -1.5 to 1.5
                float input = (t - 0.5f) * 3f; 
                
                // Simple soft clip model considering bias
                float biased = input + Bias * 0.5f;
                float output = (float)Math.Tanh(biased * gain);
                
                // Map input to X, output to Y
                float plotX = midX + input * (w/3f);
                float plotY = midY - output * (h/3f); // Invert Y
                
                if(first) { painter.MoveTo(new Vector2(plotX, plotY)); first = false; }
                else painter.LineTo(new Vector2(plotX, plotY));
            }
            painter.Stroke();
        }
    }

    /// <summary>
    /// Visualizes Distortion transfer curve based on type
    /// </summary>
    public class DistortionVisualizer : DSPVisualizerBase
    {
        public VARCOVoice.DSP.DistortionType Type { get; set; } = VARCOVoice.DSP.DistortionType.SoftClip;
        public float Drive { get; set; } = 50f;

        protected override void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            var painter = ctx.painter2D;
            var rect = contentRect;
            float w = rect.width;
            float h = rect.height;
            float midX = w / 2f;
            float midY = h / 2f;

            // Draw Grid
            painter.strokeColor = new Color(1f, 1f, 1f, 0.1f);
            painter.lineWidth = 1f;
            painter.BeginPath();
            painter.MoveTo(new Vector2(midX, 0)); painter.LineTo(new Vector2(midX, h));
            painter.MoveTo(new Vector2(0, midY)); painter.LineTo(new Vector2(w, midY));
            painter.Stroke();

            // Draw Transfer Curve
            painter.strokeColor = new Color(1f, 0.3f, 0.3f, 0.9f);
            painter.lineWidth = 2.5f;
            painter.BeginPath();

            float driveAmount = 1f + Drive * 0.05f;
            bool first = true;
            int points = 60;
            for(int i=0; i<points; i++)
            {
                float t = i / (float)(points - 1);
                float input = (t - 0.5f) * 3f;
                float driven = input * driveAmount;
                float output;

                switch (Type)
                {
                    case VARCOVoice.DSP.DistortionType.SoftClip:
                        output = (float)Math.Tanh(driven);
                        break;
                    case VARCOVoice.DSP.DistortionType.HardClip:
                        output = Mathf.Clamp(driven, -1f, 1f);
                        break;
                    case VARCOVoice.DSP.DistortionType.Tube:
                        output = driven >= 0 ? 1f - Mathf.Exp(-driven) : -1f + Mathf.Exp(driven);
                        break;
                    case VARCOVoice.DSP.DistortionType.Fuzz:
                        output = Mathf.Clamp(driven * Mathf.Abs(driven) + driven * 0.5f, -1f, 1f);
                        break;
                    case VARCOVoice.DSP.DistortionType.Bitcrusher:
                        float levels = 16f; // 4-bit for demo
                        output = Mathf.Round(driven * levels) / levels;
                        output = Mathf.Clamp(output, -1f, 1f);
                        break;
                    default:
                        output = driven;
                        break;
                }

                float plotX = midX + input * (w/3f);
                float plotY = midY - output * (h/3f);
                
                if(first) { painter.MoveTo(new Vector2(plotX, plotY)); first = false; }
                else painter.LineTo(new Vector2(plotX, plotY));
            }
            painter.Stroke();
        }
    }

    /// <summary>
    /// Visualizes Saturation with harmonic character
    /// </summary>
    public class SaturationVisualizer : DSPVisualizerBase
    {
        public float Amount { get; set; } = 30f;
        public float Character { get; set; } = 0.5f;

        protected override void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            var painter = ctx.painter2D;
            var rect = contentRect;
            float w = rect.width;
            float h = rect.height;
            float midX = w / 2f;
            float midY = h / 2f;

            // Draw Grid
            painter.strokeColor = new Color(1f, 1f, 1f, 0.1f);
            painter.lineWidth = 1f;
            painter.BeginPath();
            painter.MoveTo(new Vector2(midX, 0)); painter.LineTo(new Vector2(midX, h));
            painter.MoveTo(new Vector2(0, midY)); painter.LineTo(new Vector2(w, midY));
            painter.Stroke();

            // Draw Transfer Curve - blend even/odd harmonics
            painter.strokeColor = new Color(0.9f, 0.6f, 0.2f, 0.9f);
            painter.lineWidth = 2.5f;
            painter.BeginPath();

            float drive = 1f + Amount * 0.03f;
            bool first = true;
            int points = 60;
            for(int i=0; i<points; i++)
            {
                float t = i / (float)(points - 1);
                float input = (t - 0.5f) * 3f;
                float driven = input * drive;

                float even = driven / (1f + Mathf.Abs(driven));
                float odd = (float)Math.Tanh(driven);
                float output = even * (1f - Character) + odd * Character;

                float plotX = midX + input * (w/3f);
                float plotY = midY - output * (h/3f);
                
                if(first) { painter.MoveTo(new Vector2(plotX, plotY)); first = false; }
                else painter.LineTo(new Vector2(plotX, plotY));
            }
            painter.Stroke();
        }
    }

    /// <summary>
    /// Visualizes Tape wow/flutter modulation
    /// </summary>
    public class TapeVisualizer : DSPVisualizerBase
    {
        public float Saturation { get; set; } = 0.5f;
        public float Wow { get; set; } = 0.1f;
        public float Flutter { get; set; } = 0.1f;
        private float _phase = 0f;

        public TapeVisualizer()
        {
            schedule.Execute(() => {
                _phase += 0.03f;
                MarkDirtyRepaint();
            }).Every(16);
        }

        protected override void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            var painter = ctx.painter2D;
            var rect = contentRect;
            float w = rect.width;
            float h = rect.height;
            float midY = h / 2f;

            // Draw tape-like wobbling line
            painter.strokeColor = new Color(0.7f, 0.5f, 0.3f, 0.9f);
            painter.lineWidth = 3f;
            painter.BeginPath();

            bool first = true;
            int points = 80;
            for(int i=0; i<points; i++)
            {
                float t = i / (float)(points - 1);
                float x = t * w;

                float wowMod = Mathf.Sin(_phase + t * 2f) * Wow * 20f;
                float flutterMod = Mathf.Sin(_phase * 5f + t * 15f) * Flutter * 8f;
                float y = midY + wowMod + flutterMod;

                if(first) { painter.MoveTo(new Vector2(x, y)); first = false; }
                else painter.LineTo(new Vector2(x, y));
            }
            painter.Stroke();

            // Saturation indicator bar
            painter.fillColor = new Color(0.5f, 0.3f, 0.2f, 0.5f);
            float satHeight = Saturation * (h * 0.3f);
            painter.BeginPath();
            painter.MoveTo(new Vector2(w - 15, h));
            painter.LineTo(new Vector2(w - 5, h));
            painter.LineTo(new Vector2(w - 5, h - satHeight));
            painter.LineTo(new Vector2(w - 15, h - satHeight));
            painter.ClosePath();
            painter.Fill();
        }
    }

    /// <summary>
    /// Visualizes Ring Modulator carrier wave
    /// </summary>
    public class RingModVisualizer : DSPVisualizerBase
    {
        public float Frequency { get; set; } = 440f;
        public VARCOVoice.DSP.LFOWaveform Waveform { get; set; } = VARCOVoice.DSP.LFOWaveform.Sine;
        private float _phase = 0f;

        public RingModVisualizer()
        {
            schedule.Execute(() => {
                _phase += 0.1f * (Frequency / 100f);
                MarkDirtyRepaint();
            }).Every(16);
        }

        protected override void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            var painter = ctx.painter2D;
            var rect = contentRect;
            float w = rect.width;
            float h = rect.height;
            float midY = h / 2f;

            // Draw carrier wave
            painter.strokeColor = new Color(0.2f, 0.8f, 0.5f, 0.9f);
            painter.lineWidth = 2f;
            painter.BeginPath();

            bool first = true;
            int points = 100;
            float freq = Mathf.Clamp(Frequency / 50f, 1f, 20f);

            for(int i=0; i<points; i++)
            {
                float t = i / (float)(points - 1);
                float x = t * w;
                float angle = (t * freq * 2f * Mathf.PI) - _phase;
                float value;

                switch (Waveform)
                {
                    case VARCOVoice.DSP.LFOWaveform.Sine:
                        value = Mathf.Sin(angle);
                        break;
                    case VARCOVoice.DSP.LFOWaveform.Triangle:
                        float tNorm = (angle / (Mathf.PI * 2f)) % 1f;
                        value = tNorm < 0.5f ? 4f * tNorm - 1f : 3f - 4f * tNorm;
                        break;
                    case VARCOVoice.DSP.LFOWaveform.Square:
                        value = (angle % (Mathf.PI * 2f)) < Mathf.PI ? 1f : -1f;
                        break;
                    case VARCOVoice.DSP.LFOWaveform.Sawtooth:
                        value = 1f - 2f * ((angle / (Mathf.PI * 2f)) % 1f);
                        break;
                    default:
                        value = Mathf.Sin(angle);
                        break;
                }

                float y = midY - value * (h * 0.35f);
                if(first) { painter.MoveTo(new Vector2(x, y)); first = false; }
                else painter.LineTo(new Vector2(x, y));
            }
            painter.Stroke();

            // Frequency label
            painter.strokeColor = new Color(1f, 1f, 1f, 0.5f);
            painter.lineWidth = 1f;
            painter.BeginPath();
            painter.MoveTo(new Vector2(0, midY));
            painter.LineTo(new Vector2(w, midY));
            painter.Stroke();
        }
    }
}
