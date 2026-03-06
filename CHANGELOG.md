# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

## [0.0.2] - 2026-03-06

### Fixed

- FX Studio `Export` now saves a single baked WAV file with the current DSP chain and Master EQ applied.
- Fixed editor window lifecycle issues that could duplicate callbacks and theme subscriptions after UI rebuilds.
- Fixed Export panel drag-and-drop callback accumulation.
- Fixed duplicate callback registration in TTS and DSP panel workflows.

### Changed

- Unified displayed SDK version strings to `v0.0.2`.
- Updated package metadata to require Unity 2022.3 LTS or later.
- Refreshed repository and package README files for the current preview release.

## [0.0.1] - 2026-01-13

### Added

- TTS generation workflow with Standard and Lite models.
- FX Studio with DSP effects including EQ, reverb, and dynamics processing.
- Voice Picker, Voice Comparison, and Batch TTS editor tools.
- Lip Sync viseme data generation workflow.
- Local caching for generated audio.
