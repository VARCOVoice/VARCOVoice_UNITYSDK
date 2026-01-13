<div align="center">

<img src="logo.png" height="80" alt="VARCO Voice">

## Official Unity SDK for VARCO Voice API.

[![Unity](https://img.shields.io/badge/UNITY-2021.3+-000000?style=for-the-badge&logo=unity&logoColor=white)](https://unity.com)
[![License](https://img.shields.io/badge/LICENSE-Apache%202.0-007ACC?style=for-the-badge&logo=apache&logoColor=white)](LICENSE)
[![Release](https://img.shields.io/github/v/release/VARCOVoice/VARCOVoice_UNITYSDK?style=for-the-badge&logo=github&logoColor=white&label=RELEASE)](https://github.com/VARCOVoice/VARCOVoice_UNITYSDK/releases)
[![Build](https://img.shields.io/github/actions/workflow/status/VARCOVoice/VARCOVoice_UNITYSDK/validate.yml?style=for-the-badge&logo=github-actions&logoColor=white&label=BUILD)](https://github.com/VARCOVoice/VARCOVoice_UNITYSDK/actions)

[![Homepage](https://img.shields.io/badge/HOMEPAGE-6E3CBC?style=for-the-badge&logo=google-chrome&logoColor=white)](https://voice.varco.ai/)
[![API Portal](https://img.shields.io/badge/API--PORTAL-FF8C00?style=for-the-badge&logo=api&logoColor=white)](https://api.varco.ai/ko)
[![YouTube](https://img.shields.io/badge/YOUTUBE-FF0000?style=for-the-badge&logo=youtube&logoColor=white)](https://www.youtube.com/@VARCOMediaAI)
[![Support](https://img.shields.io/badge/SUPPORT-28A745?style=for-the-badge&logo=github&logoColor=white)](../../issues)

### Bring your characters to life with TTS, LipSync, and 80+ DSP presets.

VARCO Voice API를 Unity에서 사용하기 위한 공식 SDK입니다. 음성 합성뿐만 아니라 실시간 립싱크, 80가지 이상의 오디오 효과 프리셋을 제공하여 빠르게 대사생성 및 처리가 가능합니다.

</div>

---

## Features / 주요 기능

|       Feature       | Description                       | 상세 설명                             |
| :-----------------: | :-------------------------------- | :------------------------------------ |
|    **TTS**    | High-quality AI voice synthesis   | 고품질 AI 음성 합성 (Standard / Lite) |
| **FX Studio** | 80+ DSP effects presets           | 80+ DSP 오디오 효과 프리셋 제공       |
|  **LipSync**  | Automatic viseme data generation  | 실시간 입 모양(Viseme) 데이터 생성    |
|  **Caching**  | Local cache to minimize API calls | API 호출 최적화를 위한 로컬 캐시      |

---

## Requirements / 요구 사항

- **Tested**: Unity 2021.3 LTS ~ Unity 6 / 테스트 완료: Unity 2021.3 LTS ~ Unity 6
- UniTask, Burst, Mathematics (auto-installed / 자동 설치)

> 💡 Lower versions may work but are untested. Please [open an issue](../../issues/new) if you encounter problems!
>
> 💡 더 낮은 버전에서도 작동할 수 있지만 테스트되지 않았습니다. 문제가 있으면 [이슈를 등록](../../issues/new)해주세요!

---

## Installation / 설치

**Package Manager > Add package from git URL:**

```
https://github.com/VARCOVoice/VARCOVoice_UNITYSDK.git?path=/VARCOVoice-Unity
```

---

## Quick Start / 빠른 시작

1. **Window > VARCO Voice > Main Window**
2. Click **Settings** (⚙️) > **API Settings** > Enter API Key (API 키 입력)
3. Enter text and click **Generate** (텍스트 입력 후 클릭)

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

## Scripting API / 스크립팅 API

### 1. Runtime Synthesis / 실시간 생성

Generate and play voices directly from your C# code.
코드로 음성을 실시간 생성하고 재생합니다.

```csharp
using VARCOVoice;

// Simple one-liner / 간단한 한 줄 호출
await VarcoTTS.Instance.SpeakAsync("Hello, this is VARCO Voice!");

// Full control / 고급 제어
var clip = await VarcoTTS.Instance.SynthesizeAsync("안녕하세요", voice: "멀더", speed: 1.2f);
audioSource.PlayOneShot(clip);
```

### 2. Playing Assets with LipSync / 데이터 기반 재생

Play pre-exported `.wav` and `.asset` (LipSync) files.
미리 내보낸 `.wav`와 `.asset` (립싱크 데이터) 파일을 재생합니다.

```csharp
using VARCOVoice;

public VarcoDialoguePlayer dialoguePlayer;

void Start() {
    // Play by ID defined in the inspector
    // 인스펙터에 정의된 ID로 재생
    dialoguePlayer.Play("Greeting_01");
}
```

---

## Roadmap / 로드맵

- [X] **v1.0.0**: High-quality Korean TTS & LipSync support (Stable)
- [ ] **Multi-language**: Full API integration for English, Japanese, Taiwanese
- [ ] **Native Localization**: Support for Korean language in Editor UI
- [ ] **Advanced FX**: Multi-track editing in FX Studio

*Roadmap is subject to change. / 로드맵은 변경될 수 있습니다.*

---

## License / 라이선스

Copyright © NCAI Corporation. All rights reserved.
Licensed under the [Apache License 2.0](LICENSE).
