<div align="center">

# Unity SDK for VARCO Voice API (Preview)

VARCO Voice API를 Unity에서 평가 및 테스트하기 위한 프리뷰 SDK입니다.

[![Status](https://img.shields.io/badge/Status-Preview-orange)](https://github.com/VARCOVoice/VARCOVoice_UNITYSDK)
[![Unity](https://img.shields.io/badge/Unity-2022.3+-black?logo=unity)](https://unity.com)
[![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](LICENSE)
[![GitHub release](https://img.shields.io/github/v/release/VARCOVoice/VARCOVoice_UNITYSDK?include_prereleases)](https://github.com/VARCOVoice/VARCOVoice_UNITYSDK/releases)
[![Build](https://github.com/VARCOVoice/VARCOVoice_UNITYSDK/actions/workflows/validate.yml/badge.svg)](https://github.com/VARCOVoice/VARCOVoice_UNITYSDK/actions)

</div>

---

## 프리뷰 안내

**본 SDK는 프리뷰 버전입니다.**

- **안정성**: 예고 없이 변경될 수 있으며 호환성이 깨질 수 있습니다.
- **지원**: 최선의 노력으로 지원하나 SLA/응답 보장 없음
- **프로덕션 사용**: 프로덕션 사용 시 오류가 나면 책임 없음
- **보안**: API 키 하드코딩 금지. 프록시/토큰 사용

---

## 주요 기능

|       Feature       | Runtime | Editor | 상세 설명                                    |
| :-----------------: | :-----: | :----: | :------------------------------------------- |
|    **TTS**    |   ○   |   ●   | AI 음성 합성 (Standard / Lite)               |
| **FX Studio** |   ○   |   ●   | 80+ DSP 프리셋 (라디오, 홀, 전화, 캐릭터 등) |
|  **LipSync**  |   ●   |   ●   | 입 모양 생성 및 재생                         |
|  **Caching**  |   ○   |   ●   | API 호출 최적화를 위한 로컬 캐시             |

**범례:** ● 완전 지원 | ○ API로 프리셋 적용 가능

---

## 요구 사항

- **테스트 완료**: Unity 2022.3 LTS – Unity 6 (Unity 2021.3은 미검증)
- **자동 설치**: Burst, Mathematics
- **의존성**: Unitask 설치 필수

> 다른 버전에서의 동작은 보장하지 않습니다. 문제가 있으면 [이슈를 등록](../../issues/new)해주세요.

---

## 설치

**Package Manager > Add package from git URL:**

### 옵션 1: 특정 버전 고정 (강력 권장)

특정 프리뷰 태그 또는 커밋 해시를 사용하여 호환성 문제를 방지하세요:

```
https://github.com/VARCOVoice/VARCOVoice_UNITYSDK.git?path=/VARCOVoice-Unity#v1.0.0
```

`v0.1.0-preview`를 원하는 [릴리스 태그](https://github.com/VARCOVoice/VARCOVoice_UNITYSDK/releases)로 교체하거나 커밋 해시를 사용하세요:

```
https://github.com/VARCOVoice/VARCOVoice_UNITYSDK.git?path=/VARCOVoice-Unity#abc1234
```

### 옵션 2: main 브랜치 최신 버전 (권장하지 않음)

```
https://github.com/VARCOVoice/VARCOVoice_UNITYSDK.git?path=/VARCOVoice-Unity
```

> **경고**: `main` 브랜치는 언제든지 호환성이 깨지는 변경사항을 포함할 수 있습니다. 버전 고정을 강력히 권장합니다.

---

## 빠른 시작

**평가 설정 (3–5분):**

1. **Window > VARCO Voice > Main Window**
2. **Settings** > **API Settings** > API 키 입력
3. 텍스트 입력 후 **Generate** 클릭
4. **Play** 클릭하여 미리듣기
5. **FX Studio로 보내기** 클릭
6. **립싱크 데이터 내보내기** 클릭

> **성공 확인:** 생성된 음성이 들리고 파형이 보이면 성공입니다.

---

## 설정

### 에디터 API 키

API 키는 `EditorPrefs`에 저장되며 프로젝트 파일에 포함되지 않습니다.

```
Edit > Project Settings > VARCO Voice
```

### 런타임 인증

**런타임 빌드에 API 키를 하드코딩하지 마세요.**

- **권장**: 서버 측 프록시 구현 또는 단기 토큰 발급
- **평가 전용**: 에디터 빌드에서만 로컬 API 키 사용 (배포 금지)
- **절대 금지**: 리포지토리 커밋 또는 클라이언트 빌드 포함

배포용 빌드에서는 반드시 서버 측 인증을 구현하고 런타임에 토큰을 SDK에 전달하세요.

---

## 스크립팅 API

### 1. 실시간 생성

코드로 음성을 실시간 생성하고 재생합니다.

```csharp
using VARCOVoice;

// 간단한 한 줄 호출
await VarcoTTS.Instance.SpeakAsync("Hello, this is VARCO Voice!");

// 고급 제어
var clip = await VarcoTTS.Instance.SynthesizeAsync(
    text: "안녕하세요",
    voice: "멀더",
    speed: 1.2f,
    pitch: 0,
    emotion: "neutral"
);
audioSource.PlayOneShot(clip);
```

**고급 옵션:**

- **취소**: 취소 토큰(`CancellationToken`)으로 요청 중단 가능
- **에러 처리**: `VarcoException`으로 API 에러 처리 (인증, 제한, 네트워크)
- **캐싱**: 자동 캐시 (텍스트, 음성, 속도, 피치)
- **매개변수**: `voice`, `speed` (0.5–2.0), `pitch` (-12–12), `emotion`, `sampleRate`, `format`

### 2. 데이터 기반 재생 (VarcoDialoguePlayer)

미리 내보낸 `.wav`와 `.asset` (립싱크 데이터) 파일을 재생합니다.

#### 트리거 모드

| 모드 | 설명 |
|------|------|
| **Manual** | 스크립트에서 `Play()` 호출 시 재생 |
| **OnAwake** | `Awake()` 시 자동 재생 |
| **OnTrigger** | `OnTriggerEnter` 시 자동 재생 (Collider 필요) |

#### Manual 모드 사용법

```csharp
using VARCOVoice;

public class DialogueController : MonoBehaviour
{
    [SerializeField] private VarcoDialoguePlayer player;

    // ID로 재생 (권장)
    public void PlayGreeting() => player.Play("greeting_01");

    // 인덱스로 재생
    public void PlayFirst() => player.Play(0);

    // 정지
    public void StopDialogue() => player.Stop();
}
```

#### UnityEvent 연결 (버튼 클릭 등)

1. Button의 `OnClick()` 이벤트에 `VarcoDialoguePlayer` 오브젝트 드래그
2. 드롭다운에서 `VarcoDialoguePlayer > Play(string)` 선택
3. 슬롯 ID 입력 (예: `greeting_01`)

**캐시 관리:**

- **위치**: `Application.persistentDataPath/VarcoCache`
- **정책**: 캐시 키 = hash(text, voice, params), 만료 없음
- **삭제**: `VarcoTTS.Instance.ClearCache()`

---

## 로드맵

- [ ] 다국어 지원 (영어, 일본어, 대만어)
- [ ] FX Studio 다중 트랙 편집

**참고**: 로드맵 항목은 예고 없이 변경, 지연, 또는 취소될 수 있으며, 제공을 보장하지 않습니다.

---

## 지원 및 피드백

### 지원 정책

본 SDK는 프리뷰 버전으로 **최선의 노력으로만 지원**합니다. 다음을 보장하지 않습니다:

- 응답 시간 또는 SLA
- 버그 수정 또는 기능 업데이트
- 프리뷰 버전 간 하위 호환성

### 이슈 보고

이슈 보고 시 다음 정보를 반드시 포함해주세요:

- **Unity 버전** (예: 2022.3.12f1)
- **플랫폼** (Windows/Mac/Linux/iOS/Android)
- **SDK 버전** (`Packages/manifest.json`에서 태그 또는 커밋 해시)
- **재현 단계** (단계별 설명)
- **콘솔 로그** (전체 로그 및 스택 트레이스)
- **예상 vs 실제** (예상 동작 vs 실제 동작)

[이슈 등록](../../issues/new)

---

## 버전 지원 정책

- **Unity 버전**: Unity 2022.3 LTS – Unity 6에서 테스트됨. 다른 버전은 보장하지 않음.
- **프리뷰 버저닝**: 프리뷰 릴리스는 `v0.x.y-preview` 태그를 사용하며, 프리뷰 버전 간 호환성이 깨질 수 있습니다.

---

## 라이선스

Licensed under the [Apache License 2.0](LICENSE).
