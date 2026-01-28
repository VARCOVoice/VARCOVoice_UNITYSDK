using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using VARCOVoice.Editor;

namespace VARCOVoice.Editor
{
    public partial class TTSPanelController
    {
        #region Audio Playback

        private void PlayAudio(AudioClip clip)
        {
            if (clip == null) return;

            _currentClip = clip;

            EnsureAudioSource();

            _previewSource.clip = clip;
            _isPaused = false;
            ClearLoopPoints();
            _previewSource.Play();
            _isPlaying = true;
            
            // Render Waveform
            if (_waveformRenderer != null) _waveformRenderer.RenderWaveform(clip);
            
            UpdatePlaybackUI();
        }
        
        private void StopAudio()
        {
            if (_previewSource != null && _previewSource.isPlaying)
            {
                _previewSource.Stop();
            }
            _isPlaying = false;
            _isPaused = false;
            
            // Reset playhead visually
             if (_waveformRenderer != null) _waveformRenderer.UpdatePlayhead(0);
            UpdatePlayButtonLabel();
        }
        
        private void UpdatePlaybackUI()
        {
            // Safety Check for destroyed object (Unity overload)
            if (!_previewSource) return;

            try
            {
                if (_previewSource.clip == null)
                {
                    if (_playbackTimeLabel != null) _playbackTimeLabel.text = "00:00.0 / 00:00.0";
                    if (_scrubSlider != null && !_isScrubbing)
                        _scrubSlider.SetValueWithoutNotify(0f);
                    UpdatePlayButtonLabel();
                    return;
                }

                if (_previewSource.clip != null)
                {
                     // Use samples for precise sync
                     // Accessing .timeSamples on a destroyed object could throw
                     float currentTime = (float)_previewSource.timeSamples / _previewSource.clip.frequency;
                     float totalTime = _previewSource.clip.length;
                     
                     // Fallback if frequency is weird (unlikely)
                     if (totalTime <= 0) totalTime = 1f;
                     
                     // Update Labels
                     if (_playbackTimeLabel != null)
                     {
                        _playbackTimeLabel.text = $"{FormatTime(currentTime)} / {FormatTime(totalTime)}";
                     }
                     
                     // Update Playhead
                     if (_waveformRenderer != null)
                     {
                        float progress = totalTime > 0 ? currentTime / totalTime : 0;
                        _waveformRenderer.UpdatePlayhead(progress);
                     }
                     
                     if (_scrubSlider != null && !_isScrubbing)
                     {
                        float normalized = totalTime > 0 ? currentTime / totalTime : 0f;
                        _scrubSlider.SetValueWithoutNotify(Mathf.Clamp01(normalized));
                     }

                     HandleLooping(currentTime, totalTime);
                     _isPlaying = _previewSource.isPlaying;
                 }
                 else if (_isPlaying) // Detect stop
                 {
                    _isPlaying = false;
                    // Reset state when stopped
                    if (_playbackTimeLabel != null) _playbackTimeLabel.text = "00:00.0 / 00:00.0";
                    if (_waveformRenderer != null) _waveformRenderer.UpdatePlayhead(0);
                    if (_scrubSlider != null && !_isScrubbing)
                        _scrubSlider.SetValueWithoutNotify(0f);
                 }
             }
            catch
            {
                // Silently ignore errors during reload/cleanup
                _isPlaying = false;
            }

            UpdatePlayButtonLabel();
        }

        private void TogglePlayPause()
        {
            if (_previewSource == null || _previewSource.clip == null)
            {
                UpdateStatus("No audio to play", StatusType.Warning);
                return;
            }

            if (_previewSource.isPlaying)
            {
                _previewSource.Pause();
                _isPaused = true;
            }
            else
            {
                if (_previewSource.time >= _previewSource.clip.length)
                    _previewSource.time = 0f;

                if (_isPaused)
                    _previewSource.UnPause();
                else
                    _previewSource.Play();

                _isPaused = false;
            }

            UpdatePlayButtonLabel();
        }

        private void StopPlayback()
        {
            if (_previewSource == null) return;
            _previewSource.Stop();
            _previewSource.time = 0f;
            _isPaused = false;
            _isPlaying = false;
            if (_waveformRenderer != null) _waveformRenderer.UpdatePlayhead(0f);
            if (_scrubSlider != null) _scrubSlider.SetValueWithoutNotify(0f);
            UpdatePlayButtonLabel();
        }

        private void SetScrubPosition(float normalized)
        {
            if (_previewSource == null || _previewSource.clip == null) return;
            float total = _previewSource.clip.length;
            float target = Mathf.Clamp01(normalized) * total;
            _previewSource.time = Mathf.Clamp(target, 0f, total);
            UpdatePlaybackUI();
        }

        private void SetLoopPointA()
        {
            if (_previewSource == null || _previewSource.clip == null) return;
            _loopASeconds = _previewSource.time;
            _hasLoopA = true;
            _setLoopABtn?.EnableInClassList("playback-marker--active", true);
        }

        private void SetLoopPointB()
        {
            if (_previewSource == null || _previewSource.clip == null) return;
            _loopBSeconds = _previewSource.time;
            _hasLoopB = true;
            _setLoopBBtn?.EnableInClassList("playback-marker--active", true);
        }

        private void ClearLoopPoints()
        {
            _hasLoopA = false;
            _hasLoopB = false;
            _loopASeconds = 0f;
            _loopBSeconds = 0f;
            _setLoopABtn?.EnableInClassList("playback-marker--active", false);
            _setLoopBBtn?.EnableInClassList("playback-marker--active", false);
        }

        private void HandleLooping(float currentTime, float totalTime)
        {
            if (_previewSource == null || !_previewSource.isPlaying) return;
            if (totalTime <= 0f) return;

            if (_hasLoopA && _hasLoopB && _loopBSeconds > _loopASeconds)
            {
                if (currentTime >= _loopBSeconds || currentTime < _loopASeconds)
                    _previewSource.time = Mathf.Clamp(_loopASeconds, 0f, totalTime);
            }
            else if (currentTime >= totalTime)
            {
                _previewSource.time = 0f;
            }
        }

        private void UpdatePlayButtonLabel()
        {
            if (_playPauseBtn == null) return;
            string target = _previewSource != null && _previewSource.isPlaying ? "||" : "▶";
            if (_playPauseBtn.text != target)
                _playPauseBtn.text = target;
        }

        private string FormatTime(float time)
        {
            System.TimeSpan t = System.TimeSpan.FromSeconds(time);
            return string.Format("{0:D2}:{1:D2}.{2:D1}", t.Minutes, t.Seconds, t.Milliseconds / 100);
        }
        
        #endregion
    }
}
