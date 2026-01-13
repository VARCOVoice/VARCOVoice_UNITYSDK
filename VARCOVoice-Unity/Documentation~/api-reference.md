# VARCO Voice Unity - API Reference

## Core Components

### VarcoTTS

Main TTS component. Use as singleton or component.

```csharp
// Singleton access
VarcoTTS.Instance.SpeakAsync("Hello world");

// Component access
[SerializeField] private VarcoTTS tts;
await tts.SynthesizeAsync("Hello", voice: "멀더");
```

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `ApiClient` | `VarcoApiClient` | Underlying API client |
| `IsPlaying` | `bool` | Whether audio is playing |
| `CurrentClip` | `AudioClip` | Currently loaded clip |

#### Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `SpeakAsync(text)` | `UniTask` | Synthesize and play |
| `SpeakAsync(text, voice)` | `UniTask` | Synthesize with voice |
| `SynthesizeAsync(...)` | `UniTask<AudioClip>` | Synthesize to clip |
| `Play(clip)` | `void` | Play audio clip |
| `Stop()` | `void` | Stop playback |

---

### VarcoAudioSource

Enhanced AudioSource with DSP chain.

```csharp
[SerializeField] private VarcoAudioSource audio;

// Speak with effects
audio.ApplyRobotVoice();
await audio.SpeakAsync("I am a robot");
```

#### DSP Effect Shortcuts

| Method | Description |
|--------|-------------|
| `AddPitchShift(semitones)` | Add pitch shift |
| `AddReverb(preset)` | Add reverb effect |
| `AddEQ()` | Add equalizer |
| `AddLowPass(cutoff)` | Add low-pass filter |
| `AddChorus()` | Add chorus effect |
| `ClearEffects()` | Remove all effects |

#### Voice Presets

| Method | Effect |
|--------|--------|
| `ApplyRobotVoice()` | Robot/mechanical |
| `ApplyRadioVoice()` | Walkie-talkie |
| `ApplyCaveVoice()` | Echo/cave |
| `ApplyUnderwaterVoice()` | Muffled underwater |
| `ApplyGhostVoice()` | Spooky ghost |

---

### VarcoApiClient

Low-level API client.

```csharp
var client = new VarcoApiClient();

// Get voices
var voices = await client.GetVoicesAsync();

// Synthesize
var clip = await client.SynthesizeAsync(
    text: "Hello",
    voice: "멀더",
    speed: 1.0f,
    pitch: 1.0f
);

// Voice conversion
var converted = await client.ConvertVoiceAsync(
    audioData: wavBytes,
    speakerName: "노수혜(중립)"
);
```

---

## DSP Effects

### PitchShiftEffect

```csharp
var pitch = dspChain.AddEffect<PitchShiftEffect>();
pitch.Semitones = 3;  // +3 semitones higher
```

### ReverbEffect

```csharp
var reverb = dspChain.AddEffect<ReverbEffect>();
reverb.Preset = ReverbPreset.Hall;
reverb.Mix = 0.5f;
```

**Presets:** `Off`, `Room`, `Hall`, `Cave`, `Arena`, `Bathroom`, `Church`, `Underwater`

### Spatial3DEffect

```csharp
var spatial = dspChain.AddEffect<Spatial3DEffect>();
spatial.MaxDistance = 50f;
spatial.RolloffMode = AudioRolloffMode.Logarithmic;
```

### EQEffect

```csharp
var eq = dspChain.AddEffect<EQEffect>();
eq.Bass = 5f;    // +5 dB
eq.Treble = -3f; // -3 dB
```

---

## Lip Sync

### LipSyncPlayer

```csharp
[SerializeField] private LipSyncPlayer player;
[SerializeField] private SkinnedMeshRenderer face;

player.SetTarget(face);
player.PlayWithLipSync(audioClip);
```

### LipSyncProfile

ScriptableObject for viseme-to-blendshape mapping.

Create via: **Assets > Create > VARCO Voice > Lip Sync Profile**

---

## Events

### VarcoTTS Events

```csharp
tts.OnSynthesisComplete += (clip) => { };
tts.OnPlaybackComplete += () => { };
tts.OnError += (ex) => { };
```

### VarcoAudioSource Events

```csharp
audio.OnPlayStarted += () => { };
audio.OnPlayCompleted += () => { };
audio.OnPlayProgress += (progress) => { };
```

---

## Exceptions

| Exception | When |
|-----------|------|
| `VarcoAuthException` | Invalid API key |
| `VarcoBadRequestException` | Invalid parameters |
| `VarcoRateLimitException` | Rate limit exceeded |
| `VarcoTextTooLongException` | Text > 1200 bytes |
| `VarcoVoiceNotFoundException` | Voice not found |
| `VarcoNetworkException` | Network error |

```csharp
try
{
    await tts.SpeakAsync("Hello");
}
catch (VarcoRateLimitException ex)
{
    await UniTask.Delay(ex.RetryAfterSeconds * 1000);
}
```

---

## Configuration

### VarcoConfig

ScriptableObject for API settings.

```csharp
// Access
VarcoConfig.Instance.ApiKey;
VarcoConfig.Instance.DefaultVoice;
VarcoConfig.Instance.QualityLevel;
```

### Editor Settings

**Edit > Project Settings > VARCO Voice**

- API Key configuration
- Default voice/language
- Quality settings
- Cache settings
