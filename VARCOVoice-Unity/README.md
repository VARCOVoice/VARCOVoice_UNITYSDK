# VARCO Voice Unity SDK

[![Unity](https://img.shields.io/badge/Unity-2022.3+-black?logo=unity)](https://unity.com)
[![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](LICENSE)

Official Unity SDK for VARCO Voice API.

VARCO Voice API를 Unity에서 사용하기 위한 공식 SDK입니다.

---

## Features / 주요 기능

| Feature | Description |
|---------|-------------|
| **TTS** | High-quality AI voice synthesis (Standard / Lite) |
| **FX Studio** | 80+ DSP effects including EQ, Reverb, Compressor |
| **LipSync** | Automatic viseme data generation |
| **Caching** | Local cache to minimize API calls |

| 기능 | 설명 |
|-----|------|
| **TTS** | 고품질 AI 음성 합성 (Standard / Lite) |
| **FX Studio** | EQ, Reverb, Compressor 등 80+ DSP 효과 |
| **LipSync** | Viseme 데이터 자동 생성 |
| **Caching** | 로컬 캐시를 통한 API 호출 최소화 |

---

## Requirements / 요구 사항

- Unity 2022.3 LTS or later / Unity 2022.3 LTS 이상
- UniTask, Burst, Mathematics (auto-installed / 자동 설치)

---

## Installation / 설치

**Package Manager > Add package from git URL:**

```
https://github.com/VARCOVoice/VARCOVoice_UNITYSDK.git?path=/VARCOVoice-Unity
```

---

## Quick Start / 빠른 시작

1. **Window > VARCO Voice > Main Window**
2. Click **Settings** (⚙️) > **API Settings** > Enter API Key  
   **Settings** (⚙️) 클릭 > **API Settings** > API 키 입력
3. Enter text and click **Generate**  
   텍스트 입력 후 **Generate** 클릭

> After generation, click **Send to FX Studio** to apply DSP effects.  
> 생성 완료 후 **FX Studio로 보내기** 버튼으로 DSP 효과를 적용할 수 있습니다.

---

## Configuration / 설정

API keys are stored in `EditorPrefs` and are not included in project files.

API 키는 `EditorPrefs`에 저장되며 프로젝트 파일에 포함되지 않습니다.

```
Edit > Project Settings > VARCO Voice
```

---

## Roadmap / 로드맵

- [ ] Multi-language support (English, Japanese, Taiwanese) / 다국어 지원
- [ ] FX Studio multi-track editing / 다중 트랙 편집

*Roadmap is subject to change. / 로드맵은 변경될 수 있습니다.*

---

## License / 라이선스

Copyright © NCAI Corporation. All rights reserved.

Licensed under the [Apache License 2.0](LICENSE).
