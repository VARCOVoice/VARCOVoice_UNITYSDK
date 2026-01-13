<div align="center">

# <img src="logo.png" height="40" alt="VARCO Voice" style="vertical-align:middle;">

**Unity SDK for VARCO Voice API (Preview)**

A preview SDK for evaluation and testing of TTS, LipSync, and DSP features in Unity.

VARCO Voice API를 Unity에서 평가 및 테스트하기 위한 프리뷰 SDK입니다.

[![Status](https://img.shields.io/badge/Status-Preview-orange)](https://github.com/VARCOVoice/VARCOVoice_UNITYSDK)
[![Unity](https://img.shields.io/badge/Unity-2022.3+-black?logo=unity)](https://unity.com)
[![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](LICENSE)
[![GitHub release](https://img.shields.io/github/v/release/VARCOVoice/VARCOVoice_UNITYSDK?include_prereleases)](https://github.com/VARCOVoice/VARCOVoice_UNITYSDK/releases)
[![Build](https://github.com/VARCOVoice/VARCOVoice_UNITYSDK/actions/workflows/validate.yml/badge.svg)](https://github.com/VARCOVoice/VARCOVoice_UNITYSDK/actions)

---

## Preview Disclaimer / 프리뷰 안내

**This SDK is provided as a preview for evaluation and testing purposes only.**

**본 SDK는 평가 및 테스트 목적으로만 제공되는 프리뷰 버전입니다.**

| EN | KR |
|---|---|
| **Intended use**: Evaluation, internal testing, selected partners only | **용도**: 평가, 내부 테스트, 선택된 파트너 전용 |
| **Stability**: May change without notice. Breaking changes possible. | **안정성**: 예고 없이 변경될 수 있으며 호환성이 깨질 수 있습니다. |
| **Support**: Best-effort only. No SLA/response guarantees. | **지원**: 최선의 노력으로 지원하나 SLA/응답 보장 없음 |
| **Production use**: Not for production use. | **프로덕션 사용**: 프로덕션 사용 금지 |
| **Security**: Never hard-code API keys. Use proxy/tokens. | **보안**: API 키 하드코딩 금지. 프록시/토큰 사용 |

---

## Features / 주요 기능

| Feature | Runtime | Editor | Description | 상세 설명 |
| :---: | :---: | :---: | :--- | :--- |
| **TTS** | ● | ● | AI voice synthesis | AI 음성 합성 (Standard / Lite) |
| **FX Studio** | ○ | ● | 80+ DSP presets (Radio, Hall, Phone, Character, etc.) | 80+ DSP 프리셋 (라디오, 홀, 전화, 캐릭터 등) |
| **LipSync** | ● | ● | Viseme generation & playback | 입 모양 생성 및 재생 |
| **Caching** | ● | ● | Local cache to minimize API calls | API 호출 최적화를 위한 로컬 캐시 |

**Legend:** ● Full support / 완전 지원 | ○ Preset application via API / API로 프리셋 적용 가능

</div>

---

## Requirements / 요구 사항

| EN | KR |
|---|---|
| Tested on Unity 2022.3 LTS – Unity 6 (Unity 2021.3 may work but is untested) | Unity 2022.3 LTS – Unity 6에서 테스트됨 (Unity 2021.3은 미검증) |
| UniTask, Burst, Mathematics (auto-installed) | UniTask, Burst, Mathematics (자동 설치) |

> **EN:** Other versions may work but are not guaranteed. Please [open an issue](../../issues/new) if you encounter problems.
>
> **KR:** 다른 버전에서의 동작은 보장하지 않습니다. 문제가 있으면 [이슈를 등록](../../issues/new)해주세요.

---

## Installation / 설치

**Package Manager > Add package from git URL:**

### Option 1: Fixed version (Strongly Recommended) / 특정 버전 고정 (강력 권장)

Pin to a specific preview tag or commit to avoid breaking changes:

특정 프리뷰 태그 또는 커밋 해시를 사용하여 호환성 문제를 방지하세요:

```
https://github.com/VARCOVoice/VARCOVoice_UNITYSDK.git?path=/VARCOVoice-Unity#v0.1.0-preview
```

Replace `v0.1.0-preview` with your desired [release tag](https://github.com/VARCOVoice/VARCOVoice_UNITYSDK/releases), or use a commit hash:

`v0.1.0-preview`를 원하는 [릴리스 태그](https://github.com/VARCOVoice/VARCOVoice_UNITYSDK/releases)로 교체하거나 커밋 해시를 사용하세요:

```
https://github.com/VARCOVoice/VARCOVoice_UNITYSDK.git?path=/VARCOVoice-Unity#abc1234
```

### Option 2: Latest from main branch (Not Recommended) / main 브랜치 최신 버전 (권장하지 않음)

```
https://github.com/VARCOVoice/VARCOVoice_UNITYSDK.git?path=/VARCOVoice-Unity
```

> **EN:** The `main` branch may contain breaking changes at any time. Version pinning is strongly recommended.
>
> **KR:** `main` 브랜치는 언제든지 호환성이 깨지는 변경사항을 포함할 수 있습니다. 버전 고정을 강력히 권장합니다.

---

## Quick Start / 빠른 시작

**Evaluation setup (3–5 minutes) / 평가 설정 (3–5분):**

1. **Window > VARCO Voice > Main Window**
2. **Settings** > **API Settings** > Enter API Key / API 키 입력
3. Enter text and click **Generate** / 텍스트 입력 후 **Generate** 클릭
4. Click **Play** to preview / **Play** 클릭하여 미리듣기
5. Click **Send to FX Studio** / **FX Studio로 보내기** 클릭
6. Click **Export LipSync Data** / **립싱크 데이터 내보내기** 클릭

> **Success check / 성공 확인:** You should hear the generated voice and see the waveform. / 생성된 음성이 들리고 파형이 보이면 성공입니다.

---

## Configuration / 설정

### Editor API Key / 에디터 API 키

**EN:** API keys are stored in `EditorPrefs` and are not included in project files.

**KR:** API 키는 `EditorPrefs`에 저장되며 프로젝트 파일에 포함되지 않습니다.

```
Edit > Project Settings > VARCO Voice
```

### Runtime Authentication / 런타임 인증

**DO NOT hard-code API keys in runtime builds.**

**런타임 빌드에 API 키를 하드코딩하지 마세요.**

| Level | EN | KR |
|---|---|---|
| **Recommended** | Server-side proxy or short-lived tokens | 서버 측 프록시 또는 단기 토큰 발급 |
| **Evaluation only** | Local API key in Editor builds (not for distribution) | 에디터 빌드에서만 로컬 API 키 사용 (배포 금지) |
| **Never** | Commit keys to repositories or embed in client builds | 리포지토리 커밋 또는 클라이언트 빌드 포함 |

**EN:** For any builds intended for distribution, implement server-side authentication and pass tokens to the SDK at runtime.

**KR:** 배포용 빌드에서는 반드시 서버 측 인증을 구현하고 런타임에 토큰을 SDK에 전달하세요.

---

## Scripting API / 스크립팅 API

### 1. Runtime Synthesis / 실시간 생성

**EN:** Generate and play voices directly from your C# code.

**KR:** 코드로 음성을 실시간 생성하고 재생합니다.

```csharp
using VARCOVoice;

// Simple one-liner / 간단한 한 줄 호출
await VarcoTTS.Instance.SpeakAsync("Hello, this is VARCO Voice!");

// Full control / 고급 제어
var clip = await VarcoTTS.Instance.SynthesizeAsync(
    text: "안녕하세요",
    voice: "멀더",
    speed: 1.2f,
    pitch: 0,
    emotion: "neutral"
);
audioSource.PlayOneShot(clip);
```

**Advanced Options / 고급 옵션:**

| Feature | EN | KR |
|---|---|---|
| **Cancellation** | Pass `CancellationToken` to cancel requests | 취소 토큰으로 요청 중단 가능 |
| **Error handling** | Catch `VarcoException` for API errors (401, 429, network failures) | API 에러 처리 (인증, 제한, 네트워크) |
| **Caching** | Automatically caches by `(text, voice, speed, pitch)` | 자동 캐시 (텍스트, 음성, 속도, 피치) |
| **Parameters** | `voice`, `speed` (0.5–2.0), `pitch` (-12–12), `emotion`, `sampleRate`, `format` | 매개변수 상세 |

### 2. Playing Assets with LipSync / 데이터 기반 재생

**EN:** Play pre-exported `.wav` and `.asset` (LipSync) files.

**KR:** 미리 내보낸 `.wav`와 `.asset` (립싱크 데이터) 파일을 재생합니다.

```csharp
using VARCOVoice;

public VarcoDialoguePlayer dialoguePlayer;

void Start() {
    // Play by ID defined in the inspector
    // 인스펙터에 정의된 ID로 재생
    dialoguePlayer.Play("Greeting_01");
}
```

**Cache Management / 캐시 관리:**

| Item | EN | KR |
|---|---|---|
| **Location** | `Application.persistentDataPath/VarcoCache` | 위치 |
| **Policy** | Key = hash(text, voice, params), no expiration | 캐시 키 구성, 만료 없음 |
| **Clear** | `VarcoTTS.Instance.ClearCache()` | 캐시 삭제 메서드 |

---

## Roadmap / 로드맵

| Feature | EN | KR |
|---|---|---|
| Multi-language support | English, Japanese, Taiwanese | 다국어 지원 (영어, 일본어, 대만어) |
| FX Studio multi-track editing | Multi-track editing | 다중 트랙 편집 |

**EN:** Roadmap items are subject to change, delay, or cancellation without notice. No delivery guarantees are provided.

**KR:** 로드맵 항목은 예고 없이 변경, 지연, 또는 취소될 수 있으며, 제공을 보장하지 않습니다.

---

## Support & Feedback / 지원 및 피드백

### Support Policy / 지원 정책

**EN:** This is a preview SDK with **best-effort support only**. We do not guarantee:

**KR:** 본 SDK는 프리뷰 버전으로 **최선의 노력으로만 지원**합니다. 다음을 보장하지 않습니다:

| Item | EN | KR |
|---|---|---|
| **Response** | Response times or SLA | 응답 시간 또는 SLA |
| **Updates** | Bug fixes or feature updates | 버그 수정 또는 기능 업데이트 |
| **Compatibility** | Backward compatibility across preview versions | 프리뷰 버전 간 하위 호환성 |

### Issue Reporting / 이슈 보고

**EN:** When reporting issues, please include the following information:

**KR:** 이슈 보고 시 다음 정보를 반드시 포함해주세요:

| Required Info | Example / 예시 |
|---|---|
| Unity version / Unity 버전 | e.g., 2022.3.12f1 |
| Platform / 플랫폼 | Windows/Mac/Linux/iOS/Android |
| SDK version / SDK 버전 | Tag or commit hash from `Packages/manifest.json` |
| Reproduction steps / 재현 단계 | Step-by-step instructions / 단계별 설명 |
| Console logs / 콘솔 로그 | Full logs and stack traces / 전체 로그 및 스택 트레이스 |
| Expected vs. actual / 예상 vs 실제 | What should happen vs. what happened / 예상 동작 vs 실제 동작 |

[Open an issue](../../issues/new) | [이슈 등록](../../issues/new)

---

## Version Support Policy / 버전 지원 정책

| Policy | EN | KR |
|---|---|---|
| **Unity versions** | Tested on Unity 2022.3 LTS – Unity 6. Other versions are not guaranteed. | Unity 2022.3 LTS – Unity 6에서 테스트됨. 다른 버전은 보장하지 않음. |
| **Preview versioning** | Preview releases use `v0.x.y-preview` tags. Breaking changes may occur between preview versions. | 프리뷰 릴리스는 `v0.x.y-preview` 태그를 사용하며, 프리뷰 버전 간 호환성이 깨질 수 있습니다. |

---

## License / 라이선스

Licensed under the [Apache License 2.0](LICENSE).

**Trademarks / 상표:** VARCO Voice™ is a trademark of NCAI Corporation. / VARCO Voice™는 NCAI Corporation의 상표입니다.
