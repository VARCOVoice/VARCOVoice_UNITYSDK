# VARCO Voice Unity SDK

[![Unity](https://img.shields.io/badge/Unity-2022.3+-black?logo=unity)](https://unity.com)
[![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](LICENSE)

Official Unity package for the VARCO Voice API.

## Version

Current package version: `0.0.2`

## Highlights

- TTS generation inside the Unity Editor
- FX Studio workflow for DSP processing
- Lip Sync data generation for dialogue playback
- Batch generation and voice browsing tools
- Local caching for repeated requests

## Requirements

- Unity 2022.3 LTS or later
- Package dependencies are declared in [`package.json`](package.json)

## Install via Git URL

Pinned release tag:

```text
https://github.com/VARCOVoice/VARCOVoice_UNITYSDK.git?path=/VARCOVoice-Unity#v0.0.2
```

Latest `main` branch:

```text
https://github.com/VARCOVoice/VARCOVoice_UNITYSDK.git?path=/VARCOVoice-Unity
```

## Quick Start

1. Open `Window > VARCO Voice > Main Window`.
2. Configure your API key in `Project Settings > VARCO Voice`.
3. Generate a voice clip from the TTS panel.
4. Send the clip to FX Studio when you want DSP processing.
5. Click `Export` in FX Studio to save the final baked WAV.

## Related Files

- Root docs: [`../README.md`](../README.md)
- Release history: [`../CHANGELOG.md`](../CHANGELOG.md)
- Third-party notices: [`ThirdPartyNotices.md`](ThirdPartyNotices.md)

## License

Licensed under the [Apache License 2.0](LICENSE).
