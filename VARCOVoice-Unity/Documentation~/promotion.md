# VARCOVoice Unity SDK
## Enterprise-Grade Text-to-Speech Plugin for Unity

---

# 🎯 Executive Summary

**VARCOVoice Unity SDK**는 NC의 VARCO Voice TTS API를 Unity 게임 엔진에 통합하는 **오픈소스 플러그인**입니다.

| 핵심 지표 | 값 |
|-----------|-----|
| 지원 화자 수 | **1,293개** |
| 지원 언어 | 한국어, 영어, 일본어, 대만어 |
| DSP 이펙트 | 7종 |
| 라이선스 | MIT (상업적 사용 가능) |
| 타겟 Unity | 2022.3 LTS+ |

---

# 🏗️ System Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                        Unity Application                      │
├─────────────────────────────────────────────────────────────┤
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────┐  │
│  │   VarcoTTS  │  │  VarcoVC    │  │  VarcoAudioSource   │  │
│  │  (싱글톤)   │  │ (음성변환)  │  │  (DSP 통합)         │  │
│  └──────┬──────┘  └──────┬──────┘  └──────────┬──────────┘  │
│         │                │                     │             │
│  ┌──────▼────────────────▼─────────────────────▼──────────┐ │
│  │                  VarcoApiClient                         │ │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────────┐ │ │
│  │  │ Request     │  │ Audio       │  │ Connection      │ │ │
│  │  │ Queue       │  │ Cache       │  │ State Manager   │ │ │
│  │  └─────────────┘  └─────────────┘  └─────────────────┘ │ │
│  └─────────────────────────┬───────────────────────────────┘ │
├─────────────────────────────┼───────────────────────────────┤
│                             ▼                                │
│  ┌─────────────────────────────────────────────────────────┐ │
│  │                    DSP Chain                             │ │
│  │  Phase Vocoder │ Reverb │ 3D Spatial │ EQ │ Chorus     │ │
│  └─────────────────────────────────────────────────────────┘ │
│  ┌─────────────────────────────────────────────────────────┐ │
│  │                   Lip Sync Engine                        │ │
│  │  Formant Analysis │ Viseme Mapping │ BlendShape Control │ │
│  └─────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
                   ┌─────────────────────┐
                   │   OUTPUT   │
                   │   (UNity)        │
                   └─────────────────────┘
```

---

# 🔊 Core Feature 1: Audio DSP System

## Phase Vocoder Pitch Shifting

기존 피치 시프팅의 문제점:
- 단순 리샘플링 → 속도 변화 + 음질 저하
- 시간 영역 처리 → 아티팩트 발생

**해결책: Phase Vocoder 알고리즘**

```
┌──────────────────────────────────────────────────────────────┐
│                    Phase Vocoder Pipeline                     │
├──────────────────────────────────────────────────────────────┤
│                                                              │
│  Input → [Window] → [FFT] → [Analysis] → [Pitch Shift]      │
│                                              ↓               │
│  Output ← [Window] ← [IFFT] ← [Synthesis] ← [Resample]      │
│                                                              │
└──────────────────────────────────────────────────────────────┘
```

### 기술 상세

| 파라미터 | 기본값 | 설명 |
|----------|--------|------|
| Window Size | 2048 samples | FFT 해상도 |
| Overlap Factor | 4x | 부드러운 전환 |
| Pitch Range | ±12 semitones | 1옥타브 |

### 핵심 알고리즘

```csharp
// 1. Phase 분석
float phaseDiff = phase - lastPhase[k];
phaseDiff = WrapPhase(phaseDiff - k * expectedPhaseDiff);
float trueFreq = k * freqPerBin + phaseDiff / (2π) * sampleRate;

// 2. Frequency Domain에서 피치 시프트
int newBin = (int)(k * pitchRatio);
synthMag[newBin] += analysisMag[k];
synthFreq[newBin] = analysisFreq[k] * pitchRatio;

// 3. Phase 재합성
sumPhase[k] += (synthFreq[k] - k * freqPerBin) * hopSize / sampleRate * 2π;
```

---

## 7종 DSP 이펙트

| 이펙트 | 알고리즘 | 용도 |
|--------|----------|------|
| **Phase Vocoder** | FFT/IFFT | 고품질 피치 시프트 |
| **Simple Pitch** | 리샘플링 | 저사양 피치 시프트 |
| **Reverb** | Comb + Allpass Filter | 공간감 |
| **3D Spatial** | HRTF-like Panning | 위치 기반 오디오 |
| **Parametric EQ** | Biquad Filter | 음색 조절 |
| **Low Pass** | Butterworth | 필터링 |
| **Chorus** | Modulated Delay | 풍성한 소리 |

### Voice Effect Presets

```csharp
// 로봇 음성
ApplyRobotVoice() → PitchShift(-3) + Chorus + EQ(Treble+8dB)

// 라디오/워키토키
ApplyRadioVoice() → LowPass(3kHz) + EQ(Bass-15dB)

// 동굴 에코
ApplyCaveVoice() → Reverb(Cave) + PitchShift(-2)

// 수중
ApplyUnderwaterVoice() → LowPass(800Hz) + Reverb + Chorus

// 유령
ApplyGhostVoice() → PitchShift(-5) + Reverb(Church) + Chorus(Slow)
```

---

# 👄 Core Feature 2: Enhanced Lip Sync

## Formant-Based Phoneme Detection

기존 에너지 기반 방식의 한계:
- 소리 크기만 분석 → 모든 모음이 같아 보임
- 자음 구분 불가

**해결책: 포먼트(Formant) 분석**

### 한국어 모음 포먼트 데이터

| 모음 | F1 (Hz) | F2 (Hz) | Viseme |
|------|---------|---------|--------|
| ㅏ | 800 | 1200 | AA |
| ㅓ | 600 | 1000 | AA |
| ㅗ | 450 | 800 | OH |
| ㅜ | 350 | 800 | OO |
| ㅣ | 300 | 2300 | EE |
| ㅔ | 500 | 1900 | EE |

### 분석 파이프라인

```
┌─────────────────────────────────────────────────────────────┐
│                  Enhanced Lip Sync Pipeline                  │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  Audio Frame                                                │
│       ↓                                                     │
│  [Hamming Window] → [DFT] → [Spectrum]                     │
│                              ↓                              │
│                     [Find F1 Peak] (200-900 Hz)            │
│                     [Find F2 Peak] (800-2500 Hz)           │
│                              ↓                              │
│                     [Formant → Viseme Mapping]             │
│                              ↓                              │
│                     [Gaussian Scoring]                      │
│                              ↓                              │
│                     [Blend Shape Weights]                   │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### 15종 Viseme 지원

```
Silence, AA, EE, IH, OH, OO, CH, FF, TH, PP, KK, NN, RR, DD, SS
```

---

# 🔄 Core Feature 3: Enterprise Request Management

## Priority Queue with Rate Limiting

```
┌─────────────────────────────────────────────────────────────┐
│                   Request Queue Manager                      │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  [Incoming Request]                                         │
│         ↓                                                   │
│  ┌─────────────────────────────────────────┐               │
│  │         Priority Queue (Heap)           │               │
│  │  ┌─────────┐ ┌─────────┐ ┌─────────┐   │               │
│  │  │Critical │→│  High   │→│ Normal  │   │               │
│  │  └─────────┘ └─────────┘ └─────────┘   │               │
│  └─────────────────────────────────────────┘               │
│         ↓                                                   │
│  [Rate Limiter] ← 5 req/sec                                │
│         ↓                                                   │
│  [Concurrent Limit] ← max 3 simultaneous                   │
│         ↓                                                   │
│  [Execute] ─────────┬─────────────────────────             │
│                     │                                       │
│              Success?                                       │
│              ↙     ↘                                       │
│           Yes       No                                      │
│            ↓         ↓                                      │
│        [Done]   [Retry with Exponential Backoff]           │
│                  1s → 2s → 4s → 8s (max 60s)               │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

## Circuit Breaker Pattern

```
┌──────────────────────────────────────────────────────────┐
│                    Circuit Breaker                        │
├──────────────────────────────────────────────────────────┤
│                                                          │
│      ┌─────────┐      3 failures     ┌─────────┐        │
│      │ CLOSED  │ ─────────────────→  │  OPEN   │        │
│      │(정상동작)│                      │(차단중) │        │
│      └────┬────┘                      └────┬────┘        │
│           │                                │             │
│           │ success                        │ 30s timeout │
│           │                                ↓             │
│           │                          ┌─────────┐        │
│           └────────────────────────  │HALF-OPEN│        │
│                    success           │(1회시도)│        │
│                                      └─────────┘        │
│                                                          │
└──────────────────────────────────────────────────────────┘
```

---

# 💾 Core Feature 4: Intelligent Caching

## LRU Cache with Disk Persistence

```
┌─────────────────────────────────────────────────────────────┐
│                    Audio Cache Manager                       │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  Cache Key = SHA256(text + voice + speed + pitch + quality) │
│                                                             │
│  ┌─────────────────────────────────────────────────────┐   │
│  │              Memory Cache (LRU)                      │   │
│  │              Max: 50 MB                              │   │
│  │  ┌───┐ ┌───┐ ┌───┐ ┌───┐ ┌───┐                     │   │
│  │  │MRU│→│   │→│   │→│   │→│LRU│ → Evict             │   │
│  │  └───┘ └───┘ └───┘ └───┘ └───┘                     │   │
│  └─────────────────────────────────────────────────────┘   │
│                         ↓ Miss                              │
│  ┌─────────────────────────────────────────────────────┐   │
│  │              Disk Cache                              │   │
│  │              Max: 500 MB                             │   │
│  │              Expiry: 7 days                          │   │
│  │              Path: %TEMP%/VARCOVoice/AudioCache/     │   │
│  └─────────────────────────────────────────────────────┘   │
│                         ↓ Miss                              │
│                   [VARCO API Call]                          │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### Cache Statistics

```csharp
var stats = AudioCacheManager.Instance.GetStatistics();
// stats.HitRate → 0.85 (85% 캐시 히트!)
// stats.MemoryUsageFormatted → "23.5 MB"
```

---

# 🔐 Core Feature 5: Security

## API Key Protection

```
┌─────────────────────────────────────────────────────────────┐
│                  Secure Key Storage                          │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  [API Key] ─────────────────────────────────────→           │
│                                                             │
│  1. Generate Random Salt (16 bytes)                         │
│                                                             │
│  2. XOR Obfuscation:                                        │
│     encrypted[i] = key[i] ⊕ deviceId[i] ⊕ salt[i]          │
│                                                             │
│  3. Base64 Encode                                           │
│                                                             │
│  4. Store in PlayerPrefs                                    │
│     - VARCO_EK: encrypted key                               │
│     - VARCO_ES: salt                                        │
│                                                             │
│  ※ 기기별 고유 암호화 (다른 기기에서 복호화 불가)           │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

## Input Validation

| 파라미터 | 제한 | 검증 |
|----------|------|------|
| Text | ≤ 1200 bytes | ✅ |
| Speed | 0.5 ~ 1.5 | ✅ |
| Pitch | 0.5 ~ 1.5 | ✅ |
| Quality | 8 ~ 20 | ✅ |
| Voice Name | ≤ 100 chars | ✅ |

---

# 🧪 Quality Assurance

## Test Coverage

| 테스트 영역 | 케이스 수 |
|-------------|-----------|
| AudioCacheManager | 4 |
| VoiceFilter | 5 |
| VarcoVoice Model | 6 |
| Exceptions | 4 |
| RequestQueue | 2 |
| DSP Effects | 7 |
| Lip Sync | 6 |
| Language | 4 |
| Text Validation | 4 |
| **Total** | **40+** |

## Test Categories

```
[Test] GenerateKey_SameInputs_ReturnsSameKey
[Test] Matches_GenderFilter_FiltersCorrectly
[Test] ParseDescription_ValidDescription_SetsProperties
[Test] VarcoRateLimitException_HasRetryAfter
[Test] PhaseVocoderPitchShift_ZeroSemitones_NoChange
[Test] LipSyncData_GetVisemeAtTime_ReturnsCorrectViseme
```

---

# 🛠️ Editor Tools

## Voice Picker Window

```
┌─────────────────────────────────────────────────────────────┐
│  VARCO Voice Picker                              [Refresh]  │
├─────────────────────────────────────────────────────────────┤
│  Total: 1,293 voices | Filtered: 127                        │
├─────────────────────────────────────────────────────────────┤
│  Filters                                                    │
│  ┌─────────────────────────────────────────────────────┐   │
│  │ Search: [멀더_________]                              │   │
│  │ Gender: [Male ▼]  Age: [Young ▼]                    │   │
│  │ ☑ Emotion: [Happy ▼]                                │   │
│  │ [Clear Filters]                                      │   │
│  └─────────────────────────────────────────────────────┘   │
├─────────────────────────────────────────────────────────────┤
│  ┌──────────────────────────────────────────────┬──┬──┐    │
│  │ 멀더(중립)                                    │선택│▶│    │
│  │ 남성, 청년, 저음, 맑음, 냉정한                │    │  │    │
│  ├──────────────────────────────────────────────┼──┼──┤    │
│  │ 멀더(행복)                                    │선택│▶│    │
│  │ 남성, 청년, 저음, 맑음, 밝은                  │    │  │    │
│  └──────────────────────────────────────────────┴──┴──┘    │
│                    ◀ Prev | Page 1/3 | Next ▶              │
├─────────────────────────────────────────────────────────────┤
│  Preview                                                    │
│  Text: [안녕하세요. 바르코 보이스 테스트입니다.]            │
│  [Preview Selected Voice]  [Stop]                           │
│  Selected: 멀더(중립) | [Copy Voice Name to Clipboard]      │
└─────────────────────────────────────────────────────────────┘
```

---

# 📈 Performance Metrics

## Built-in Monitoring

```csharp
// 자동 성능 측정
using (PerformanceMonitor.Instance.StartTiming("TTS.Synthesize"))
{
    await client.SynthesizeAsync(text, voice);
}

// 결과 조회
var metrics = PerformanceMonitor.Instance.GetMetrics();
// TTS.Synthesize: 127 calls, avg 342.5ms, min 180ms, max 2100ms
```

## Connection Health

```csharp
ConnectionStateManager.Instance.OnStateChanged += (state) => {
    switch (state) {
        case ConnectionState.Healthy: // 정상
        case ConnectionState.RateLimited: // 잠시 대기
        case ConnectionState.NetworkUnavailable: // 네트워크 확인
        case ConnectionState.ApiUnreachable: // 서버 문제
    }
};
```

---

# 🎮 Use Cases

## 1. 게임 NPC 대화
```csharp
npc.Speak("용사여, 잘 왔다!", voice: "노수혜(중립)");
```

## 2. 인터랙티브 스토리
```csharp
await dialogueSystem.PlayLine(narrator, "어두운 숲 속...");
await dialogueSystem.PlayLine(hero, "누구냐!");
```

## 3. VR/AR 캐릭터
```csharp
// 3D 공간 음성 + 립싱크
avatar.SpeakWithLipSync("안녕하세요!", spatial3D: true);
```

## 4. 교육용 콘텐츠
```csharp
// 느린 속도 + 또렷한 발음
tutor.Speak(lesson, speed: 0.8f, pitch: 1.0f);
```

## 5. 음성 효과
```csharp
// 로봇 AI 캐릭터
robot.ApplyRobotVoice();
robot.Speak("저는 인공지능입니다.");
```

---

# 📦 Package Structure

```
VARCOVoice-Unity/           (MIT License)
├── package.json            UPM 패키지 정의
├── Runtime/                게임에서 사용
│   ├── Core/               API 통신, 모델
│   ├── Audio/              오디오 관리, 캐시
│   ├── DSP/                7종 이펙트
│   └── LipSync/            립싱크 시스템
├── Editor/                 에디터 도구
├── Tests/                  40+ 테스트
├── Samples~/               5개 예제
└── Documentation~/         API 문서
```

---

# 🚀 Getting Started

```csharp
// 1줄로 TTS 재생
await VarcoTTS.Instance.SpeakAsync("안녕하세요!");

// DSP 이펙트 적용
VarcoAudioSource audio = GetComponent<VarcoAudioSource>();
audio.ApplyRobotVoice();
await audio.SpeakAsync("로봇 음성입니다.");

// 립싱크 캐릭터
LipSyncPlayer player = character.GetComponent<LipSyncPlayer>();
player.PlayWithLipSync(audioClip);
```

---

# 📊 Summary

| 구분 | 스펙 |
|------|------|
| **화자** | 1,293개 (한/영/일/대만) |
| **DSP** | 7종 (Phase Vocoder 포함) |
| **립싱크** | 15 Viseme, 포먼트 분석 |
| **캐싱** | LRU + 디스크 (550MB) |
| **보안** | 암호화 저장, 입력 검증 |
| **안정성** | 서킷브레이커, 자동 재시도 |
| **테스트** | 40+ 유닛 테스트 |
| **라이선스** | MIT (상업적 무료) |

---

# 🤝 Powered by

- **VARCO Voice** - NC's AI TTS Technology
- **UniTask** - Unity Async/Await
- **Unity 2022.3+** - Game Engine
