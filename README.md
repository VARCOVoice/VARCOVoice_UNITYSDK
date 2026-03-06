[![Status](https://img.shields.io/badge/Status-Preview-orange)](https://github.com/VARCOVoice/VARCOVoice_UNITYSDK) [![Unity](https://img.shields.io/badge/Unity-2022.3+-black?logo=unity)](https://unity.com) [![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](LICENSE) [![GitHub release](https://img.shields.io/github/v/release/VARCOVoice/VARCOVoice_UNITYSDK?include_prereleases)](https://github.com/VARCOVoice/VARCOVoice_UNITYSDK/releases) [![Build](https://github.com/VARCOVoice/VARCOVoice_UNITYSDK/actions/workflows/validate.yml/badge.svg)](https://github.com/VARCOVoice/VARCOVoice_UNITYSDK/actions/workflows/validate.yml)


# VARCO Voice Unity SDK

VARCO Voice API를 Unity 프로젝트에서 사용할 수 있도록 제공되는 Unity SDK (Preview) 입니다.

TTS 생성, DSP 처리, Lip Sync 데이터 생성, 배치 워크플로우 등을 Unity Editor 내부에서 사용할 수 있습니다.

---

# v0.0.2 업데이트

* FX Studio Export 개선

  * 현재 적용된 Effect Chain + Master EQ가 반영된 단일 WAV 파일 생성

* Unity Editor 안정성 개선

  * Editor rebuild 이후 발생하던

    * callback 중복 등록
    * theme subscription 중복
    * delayed UI action 중복 실행
      문제 수정

* Export 패널 Drag & Drop 버그 수정

  * callback 누적 문제 해결

* 패키지 버전 통합

  * SDK 및 Editor UI 표시 버전 → v0.0.2

---

# 주요 기능

## TTS 생성

* Standard Voice Model
* Lite Voice Model

Unity Editor에서 텍스트 입력 후 음성 생성 가능

---

## FX Studio

Unity 내부 DSP 처리 툴

지원 기능

* Effect Chain
* Master EQ
* Export baked audio

---

## Lip Sync 데이터 생성

Export된 음성에서 Viseme 데이터 생성

사용처

* 캐릭터 Lip Sync
* Dialogue 시스템

---

## Voice Tools

Voice 탐색 및 비교 도구 제공

* Voice Picker
* Voice Comparison

---

## Batch TTS Workflow

대량 음성 생성 자동화 지원

---

## Local Cache

동일한 TTS 요청 반복 시

* API 호출 최소화
* 캐시된 결과 재사용

---

# 요구 사항

## Unity

Unity 2022.3 LTS 이상

## UPM Dependency

* com.unity.nuget.newtonsoft-json
* com.unity.burst
* com.unity.mathematics
* com.unity.collections
* com.cysharp.unitask

---

# 설치

권장: Release Tag 고정 설치

[https://github.com/VARCOVoice/VARCOVoice_UNITYSDK.git?path=/VARCOVoice-Unity#v0.0.2](https://github.com/VARCOVoice/VARCOVoice_UNITYSDK.git?path=/VARCOVoice-Unity#v0.0.2)

최신 버전 사용

[https://github.com/VARCOVoice/VARCOVoice_UNITYSDK.git?path=/VARCOVoice-Unity](https://github.com/VARCOVoice/VARCOVoice_UNITYSDK.git?path=/VARCOVoice-Unity)

Unity Package Manager → Git URL로 설치

---

# Quick Start

1. Unity 메뉴에서 SDK 실행

Window → VARCO Voice → Main Window

2. API Key 설정

Settings → API Settings

3. TTS 생성

Text 입력 → Generate

4. DSP 처리 (선택)

Send to FX Studio

5. WAV Export

FX Studio → Export

현재 Effect Chain + Master EQ가 적용된 baked WAV 생성

6. Dialogue Player 연결

Export 패널에서

* Audio Clip
* Viseme Data

를 VarcoDialoguePlayer 슬롯에 할당

---

# Repository 구조

VARCOVoice-Unity
Unity SDK 패키지

.github
Issue Template 및 CI Workflow

CHANGELOG.md
Release 기록

CONTRIBUTING.md
기여 가이드

SECURITY.md
보안 정책

---

# Support

Issue

[https://github.com/VARCOVoice/VARCOVoice_UNITYSDK/issues](https://github.com/VARCOVoice/VARCOVoice_UNITYSDK/issues)

Security

[varcovoice.ncsoft@gmail.com](mailto:varcovoice.ncsoft@gmail.com)

---

# License

Apache License 2.0
