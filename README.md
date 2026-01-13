<div align="center">

# <img src="logo.png" height="40" alt="VARCO Voice" style="vertical-align:middle;">

**Unity SDK for VARCO Voice API (Preview)**

A preview SDK for evaluation and testing of TTS, LipSync, and DSP features in Unity.

[![Status](https://img.shields.io/badge/Status-Preview-orange)](https://github.com/VARCOVoice/VARCOVoice_UNITYSDK)
[![Unity](https://img.shields.io/badge/Unity-2022.3+-black?logo=unity)](https://unity.com)
[![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](LICENSE)
[![GitHub release](https://img.shields.io/github/v/release/VARCOVoice/VARCOVoice_UNITYSDK?include_prereleases)](https://github.com/VARCOVoice/VARCOVoice_UNITYSDK/releases)
[![Build](https://github.com/VARCOVoice/VARCOVoice_UNITYSDK/actions/workflows/validate.yml/badge.svg)](https://github.com/VARCOVoice/VARCOVoice_UNITYSDK/actions)

VARCO Voice API를 Unity에서 평가 및 테스트하기 위한 프리뷰 SDK입니다.

---

## Preview Disclaimer / 프리뷰 안내

**This SDK is provided as a preview for evaluation and testing purposes only.**

**본 SDK는 평가 및 테스트 목적으로만 제공되는 프리뷰 버전입니다.**

- **Intended use**: Evaluation, internal testing, selected partners only
- **용도**: 평가, 내부 테스트, 선택된 파트너 전용
- **Stability**: API and SDK structure may change without notice. Breaking changes are possible.
- **안정성**: API 및 SDK 구조는 예고 없이 변경될 수 있으며, 호환성이 깨질 수 있습니다.
- **Support**: Best-effort support only. No SLA or response time guarantees.
- **지원**: 최선의 노력으로 지원하나, SLA 및 응답 시간을 보장하지 않습니다.
- **Production use**: Not for production use.
- **프로덕션 사용**: 프로덕션 사용 금지.
- **Security**: Never hard-code API keys in builds. Use server-side proxy or short-lived tokens.
- **보안**: API 키를 빌드에 하드코딩하지 마세요. 서버 프록시 또는 단기 토큰을 사용하세요.

---

## Features / 주요 기능

| Feature | Runtime | Editor | Description | 상세 설명 |
| :---: | :---: | :---: | :--- | :--- |
| **TTS** | ● | ● | AI voice synthesis | AI 음성 합성 (Standard / Lite) |
| **FX Studio** | △ | ● | 80+ DSP presets (Radio, Hall, Phone, Character, etc.) | 80+ DSP 프리셋 (라디오, 홀, 전화, 캐릭터 등) |
| **LipSync** | ● | ● | Viseme generation & playback | 입 모양 생성 및 재생 |
| **Caching** | ● | ● | Local cache to minimize API calls | API 호출 최적화를 위한 로컬 캐시 |

**Legend:** ● Full support / 완전 지원 | △ Limited runtime support (varies by version) / 런타임 지원 제한 (버전에 따라 다름)

</div>

---

## Requirements / 요구 사항

- **Tested on Unity 2022.3 LTS – Unity 6 (6.0 / 6000.x). Unity 2021.3 may work but is untested.**
- **테스트 완료: Unity 2022.3 LTS – Unity 6 (6.0 / 6000.x). Unity 2021.3은 미검증.**
- UniTask, Burst, Mathematics (auto-installed / 자동 설치)

> Other versions may work but are not guaranteed. Please [open an issue](../../issues/new) if you encounter problems.
>
> 다른 버전에서의 동작은 보장하지 않습니다. 문제가 있으면 [이슈를 등록](../../issues/new)해주세요.

---

## Installation / 설치

**Package Manager > Add package from git URL:**

### Option 1: Fixed version (Strongly Recommended)

Pin to a specific preview tag or commit to avoid breaking changes:

```
https://github.com/VARCOVoice/VARCOVoice_UNITYSDK.git?path=/VARCOVoice-Unity#v0.1.0-preview
```

Replace `v0.1.0-preview` with your desired [release tag](https://github.com/VARCOVoice/VARCOVoice_UNITYSDK/releases), or use a commit hash for maximum stability:

```
https://github.com/VARCOVoice/VARCOVoice_UNITYSDK.git?path=/VARCOVoice-Unity#abc1234
```

**특정 버전 고정 (강력 권장):**
특정 프리뷰 태그 또는 커밋 해시를 사용하여 호환성 문제를 방지하세요. [릴리스 태그](https://github.com/VARCOVoice/VARCOVoice_UNITYSDK/releases)를 확인하세요.

### Option 2: Latest from main branch (Not Recommended)

```
https://github.com/VARCOVoice/VARCOVoice_UNITYSDK.git?path=/VARCOVoice-Unity
```

**Warning**: The `main` branch may contain breaking changes at any time. Version pinning is strongly recommended.

**경고**: `main` 브랜치는 언제든지 호환성이 깨지는 변경사항을 포함할 수 있습니다. 버전 고정을 강력히 권장합니다.

### Troubleshooting / 문제 해결

If you encounter UPM cache issues or package conflicts:
UPM 캐시 문제 또는 패키지 충돌이 발생하는 경우:

1. Remove the package from Package Manager / Package Manager에서 패키지 제거
2. Close Unity and delete `Library/PackageCache` / Unity를 닫고 `Library/PackageCache` 삭제
3. Reopen Unity and re-add the package / Unity를 다시 열고 패키지 재추가

---

## Quick Start / 빠른 시작

**Evaluation setup (3–5 minutes):**

1. **Window > VARCO Voice > Main Window**
2. Click **Settings** > **API Settings** > Enter API Key (API 키 입력)
3. Enter text and click **Generate** → AudioClip created (오디오 생성 확인)
4. Click **Play** to preview → Audio plays in Editor (에디터에서 재생 확인)
5. Click **Send to FX Studio** → Apply effects (Radio, Hall, etc.) (효과 적용 확인)
6. Click **Export LipSync Data** → `.asset` file created (립싱크 데이터 생성 확인)

> **Success check**: You should hear the generated voice and see the waveform.
>
> **성공 확인**: 생성된 음성이 들리고 파형이 보이면 성공입니다.

---

## Configuration / 설정

### Editor API Key / 에디터 API 키

API keys are stored in `EditorPrefs` and are not included in project files.
API 키는 `EditorPrefs`에 저장되며 프로젝트 파일에 포함되지 않습니다.

```
Edit > Project Settings > VARCO Voice
```

### Runtime Authentication / 런타임 인증

**Security Requirements / 보안 요구사항:**

**DO NOT hard-code API keys in runtime builds.**
**런타임 빌드에 API 키를 하드코딩하지 마세요.**

- **Recommended**: Implement server-side proxy or issue short-lived tokens
- **권장**: 서버 측 프록시 구현 또는 단기 토큰 발급
- **Evaluation only**: Local API key in Editor builds only (not for distribution)
- **평가 전용**: 에디터 빌드에서만 로컬 API 키 사용 (배포 금지)
- **Never**: Commit keys to repositories or embed in client builds
- **절대 금지**: 리포지토리 커밋 또는 클라이언트 빌드 포함

For any builds intended for distribution, implement server-side authentication and pass tokens to the SDK at runtime.

배포용 빌드에서는 반드시 서버 측 인증을 구현하고 런타임에 토큰을 SDK에 전달하세요.

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

**Note**: The following features and parameters may vary depending on your SDK version and API plan. Behavior may change between preview versions.

**참고**: 아래 기능 및 매개변수는 SDK 버전 및 API 플랜에 따라 다를 수 있으며, 프리뷰 버전 간 동작이 변경될 수 있습니다.

- **Cancellation**: If supported, pass `CancellationToken` to cancel in-flight requests
- **취소**: 지원되는 경우 `CancellationToken`으로 진행 중인 요청 중단 가능
- **Error handling**: Exact exception types and status codes may vary. Check SDK documentation for your version.
- **에러 처리**: 정확한 예외 타입과 상태 코드는 버전에 따라 다를 수 있음. 버전별 SDK 문서를 확인하세요.
- **Caching**: Caching behavior (cache key composition, eviction policy) may change between preview versions.
- **캐싱**: 캐싱 동작 (캐시 키 구성, 삭제 정책)은 프리뷰 버전 간 변경될 수 있습니다.
- **Parameters**: Available parameters (`voice`, `speed`, `pitch`, `emotion`, `sampleRate`, `format`) and their valid ranges depend on API plan and SDK version.
- **매개변수**: 사용 가능한 매개변수 및 유효 범위는 API 플랜과 SDK 버전에 따라 다릅니다.

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

**Cache Management / 캐시 관리:**

**Note**: Cache behavior is subject to change in preview versions.

**참고**: 캐시 동작은 프리뷰 버전에서 변경될 수 있습니다.

- **Location**: Typically `Application.persistentDataPath/VarcoCache` (may vary)
- **위치**: 일반적으로 `Application.persistentDataPath/VarcoCache` (변경 가능)
- **Policy**: Cache keys and retention policies may change between versions
- **정책**: 캐시 키 및 보관 정책은 버전 간 변경될 수 있음
- **Cleanup**: Monitor disk usage and clear cache periodically. Use SDK-provided cleanup methods if available.
- **정리**: 디스크 사용량을 모니터링하고 주기적으로 캐시를 정리하세요. SDK 제공 정리 메서드가 있다면 사용하세요.
- **Manual clear**: If supported, use `VarcoTTS.Instance.ClearCache()` or delete cache directory manually
- **수동 삭제**: 지원되는 경우 `VarcoTTS.Instance.ClearCache()` 사용 또는 캐시 디렉터리 수동 삭제

---

## Roadmap / 로드맵

- [ ] Multi-language support (English, Japanese, Taiwanese) / 다국어 지원
- [ ] FX Studio multi-track editing / 다중 트랙 편집

**Note**: Roadmap items are subject to change, delay, or cancellation without notice. No delivery guarantees are provided.

**참고**: 로드맵 항목은 예고 없이 변경, 지연, 또는 취소될 수 있으며, 제공을 보장하지 않습니다.

---

## Support & Feedback / 지원 및 피드백

### Support Policy / 지원 정책

This is a preview SDK with **best-effort support only**. We do not guarantee:
본 SDK는 프리뷰 버전으로 **최선의 노력으로만 지원**합니다. 다음을 보장하지 않습니다:

- Response times or SLA / 응답 시간 또는 SLA
- Bug fixes or feature updates / 버그 수정 또는 기능 업데이트
- Backward compatibility across preview versions / 프리뷰 버전 간 하위 호환성

### Issue Reporting / 이슈 보고

When reporting issues, please include the following information:
이슈 보고 시 다음 정보를 반드시 포함해주세요:

- **Unity version** (e.g., 2022.3.12f1) / Unity 버전
- **Platform** (Windows/Mac/Linux/iOS/Android)
- **SDK version** (tag or commit hash from `Packages/manifest.json`) / SDK 버전 (태그 또는 커밋 해시)
- **Reproduction steps** / 재현 단계
- **Console logs and stack traces** / 콘솔 로그 및 스택 트레이스
- **Expected vs. actual behavior** / 예상 동작 vs 실제 동작

[Open an issue](../../issues/new) | [이슈 등록](../../issues/new)

---

## Version Support Policy / 버전 지원 정책

- **Unity**: Tested on Unity 2022.3 LTS – Unity 6 (6.0 / 6000.x). Other versions are not guaranteed.
- **Unity**: Unity 2022.3 LTS – Unity 6 (6.0 / 6000.x)에서 테스트됨. 다른 버전은 보장하지 않음.
- **Preview Versioning**: Preview releases use `v0.x.y-preview` tags. Breaking changes may occur between preview versions.
- **프리뷰 버저닝**: 프리뷰 릴리스는 `v0.x.y-preview` 태그를 사용하며, 프리뷰 버전 간 호환성이 깨질 수 있습니다.

---

## License / 라이선스

Licensed under the [Apache License 2.0](LICENSE).

**Trademarks**: VARCO Voice™ is a trademark of NCAI Corporation.

**상표**: VARCO Voice™는 NCAI Corporation의 상표입니다.
