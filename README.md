# VARCO Voice Unity SDK

[![Status](https://img.shields.io/badge/Status-Preview-orange)](https://github.com/VARCOVoice/VARCOVoice_UNITYSDK)
[![Unity](https://img.shields.io/badge/Unity-2022.3+-black?logo=unity)](https://unity.com)
[![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](LICENSE)
[![GitHub release](https://img.shields.io/github/v/release/VARCOVoice/VARCOVoice_UNITYSDK?include_prereleases)](https://github.com/VARCOVoice/VARCOVoice_UNITYSDK/releases)
[![Build](https://github.com/VARCOVoice/VARCOVoice_UNITYSDK/actions/workflows/validate.yml/badge.svg)](https://github.com/VARCOVoice/VARCOVoice_UNITYSDK/actions/workflows/validate.yml)

Preview Unity SDK for the VARCO Voice API.

## What's New in v0.0.2

- FX Studio `Export` now writes a single baked WAV file with the active effect chain and Master EQ applied.
- Fixed editor window lifecycle issues that could duplicate callbacks, theme subscriptions, and delayed UI actions after rebuilds.
- Fixed Export panel drag-and-drop callback accumulation.
- Unified package and editor-facing version strings to `v0.0.2`.

## Features

- TTS generation with Standard and Lite voice models
- FX Studio with a large DSP effect library
- Lip Sync data generation from exported clips
- Voice Picker and Voice Comparison tools
- Batch TTS generation workflow
- Local caching for repeated synthesis requests

## Requirements

- Unity 2022.3 LTS or later
- Dependencies installed through UPM:
  - `com.unity.nuget.newtonsoft-json`
  - `com.unity.burst`
  - `com.unity.mathematics`
  - `com.unity.collections`
  - `com.cysharp.unitask`

## Installation

Recommended: pin the package to the `v0.0.2` tag.

```text
https://github.com/VARCOVoice/VARCOVoice_UNITYSDK.git?path=/VARCOVoice-Unity#v0.0.2
```

Latest `main` branch:

```text
https://github.com/VARCOVoice/VARCOVoice_UNITYSDK.git?path=/VARCOVoice-Unity
```

## Quick Start

1. Open `Window > VARCO Voice > Main Window`.
2. Open `Settings > API Settings` and enter your API key.
3. Enter text and click `Generate`.
4. Send the result to FX Studio if you want DSP processing.
5. In FX Studio, click `Export` to save a baked WAV with the current chain and Master EQ applied.
6. Use the Export panel to assign clips and viseme data to `VarcoDialoguePlayer` slots.

## Repository Layout

- [`VARCOVoice-Unity`](VARCOVoice-Unity): Unity package contents
- [`.github`](.github): issue templates and CI workflow
- [`CHANGELOG.md`](CHANGELOG.md): release history
- [`CONTRIBUTING.md`](CONTRIBUTING.md): contribution guide
- [`SECURITY.md`](SECURITY.md): security reporting policy

## Support

- General issues: https://github.com/VARCOVoice/VARCOVoice_UNITYSDK/issues
- Security reports: `varcovoice.ncsoft@gmail.com`

## License

Licensed under the [Apache License 2.0](LICENSE).
