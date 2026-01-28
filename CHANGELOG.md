# Changelog / 변경 이력

All notable changes to this project will be documented in this file.

이 프로젝트의 모든 주요 변경 사항이 이 파일에 기록됩니다.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## [1.0.0] - 2026-01-13

### Added / 추가

| Feature | 기능 | Description / 설명 |
|---------|------|-------------------|
| TTS Generation | TTS 생성 | High-quality AI voice synthesis (Standard/Lite) / 고품질 AI 음성 합성 |
| FX Studio | FX 스튜디오 | 80+ DSP effects (EQ, Reverb, Compressor, etc.) / 80개 이상의 DSP 효과 |
| Voice Picker | 보이스 선택기 | Browse and preview voices with favorites / 즐겨찾기로 음성 미리보기 |
| LipSync | 립싱크 | Automatic viseme data generation / 자동 비짐 데이터 생성 |
| Audio Caching | 오디오 캐싱 | Local cache to minimize API calls / 로컬 캐시로 API 호출 최소화 |
| Voice Comparison | 음성 비교 | A/B testing for voices / 음성 A/B 테스트 |
| Batch TTS | 일괄 TTS | Generate multiple audio files / 다중 오디오 파일 생성 |
| Export Panel | 내보내기 패널 | Export audio with LipSync data / 립싱크 데이터와 함께 오디오 내보내기 |

### Technical / 기술

- Unity 2022.3 LTS support / Unity 2022.3 LTS 지원
- Burst-optimized DSP processing / Burst 최적화 DSP 처리
- UniTask-based async operations / UniTask 기반 비동기 처리
- EditorPrefs-based secure API key storage / EditorPrefs 기반 보안 API 키 저장

---

## [Unreleased] / 예정

### Planned / 계획

- [ ] Multi-language support (EN, JP, TW) / 다국어 지원
- [ ] FX Studio multi-track editing / FX 스튜디오 다중 트랙 편집
- [ ] Voice cloning / custom profiles / 음성 복제 / 커스텀 프로필

*Roadmap is subject to change. / 로드맵은 변경될 수 있습니다.*
