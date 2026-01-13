# VARCOVoice-Unity

<p align="center">
  <img src="https://img.shields.io/badge/Unity-2022.3%2B-blue?logo=unity" alt="Unity Version">
  <img src="https://img.shields.io/badge/License-MIT-green" alt="License">
  <img src="https://img.shields.io/badge/VARCO-Voice%20API-orange" alt="VARCO Voice">
</p>

Unity plugin for **VARCO Voice TTS API** with DSP effects and real-time lip sync support.

## ✨ Features

| Feature | Description |
|---------|-------------|
| 🎙️ **TTS Synthesis** | Text-to-Speech with 1,293+ voice speakers |
| 🔄 **Voice Conversion** | Convert any audio to different voice |
| 🎛️ **DSP Effects** | Pitch, Reverb, 3D Spatial, EQ, LowPass, Chorus |
| 👄 **Lip Sync** | Real-time audio analysis with Korean Viseme mapping |
| 🛠️ **Editor Tools** | Voice Picker, Preview Tool, Settings UI |

## 📦 Installation

### Via Git URL (Recommended)

1. Open **Window > Package Manager**
2. Click **+** > **Add package from git URL...**
3. Enter:
```
https://github.com/your-repo/VARCOVoice-Unity.git
```

### Via Local Folder

1. Clone this repository
2. Open **Window > Package Manager**
3. Click **+** > **Add package from disk...**
4. Select `package.json`

## 🚀 Quick Start

### 1. Set API Key

Go to **Edit > Project Settings > VARCO Voice** and enter your API key.

### 2. Basic TTS

```csharp
using VARCOVoice;
using Cysharp.Threading.Tasks;

public class TTSExample : MonoBehaviour
{
    async void Start()
    {
        var tts = VarcoTTS.Instance;
        
        // Synthesize and play
        AudioClip clip = await tts.SynthesizeAsync(
            "안녕하세요, 바르코 보이스입니다.",
            voice: "멀더"
        );
        
        GetComponent<AudioSource>().PlayOneShot(clip);
    }
}
```

### 3. With DSP Effects

```csharp
using VARCOVoice;
using VARCOVoice.DSP;

public class DSPExample : MonoBehaviour
{
    [SerializeField] VarcoAudioSource varcoAudio;
    
    void Start()
    {
        // Add effects
        varcoAudio.DSPChain.AddEffect(new PitchShiftEffect { Semitones = 2 });
        varcoAudio.DSPChain.AddEffect(new ReverbEffect { Preset = ReverbPreset.Hall });
        varcoAudio.DSPChain.AddEffect(new Spatial3DEffect());
    }
}
```

### 4. Lip Sync

```csharp
using VARCOVoice;
using VARCOVoice.LipSync;

public class LipSyncExample : MonoBehaviour
{
    [SerializeField] LipSyncPlayer lipSyncPlayer;
    [SerializeField] Animator characterAnimator;
    
    async void Start()
    {
        lipSyncPlayer.SetAnimator(characterAnimator);
        
        var clip = await VarcoTTS.Instance.SynthesizeAsync("안녕하세요!");
        lipSyncPlayer.PlayWithLipSync(clip);
    }
}
```

## 📖 Documentation

- [API Reference](Documentation~/api-reference.md)
- [Manual](Documentation~/manual.md)

## 📂 Samples

Import samples via **Package Manager > VARCO Voice TTS > Samples**:

| Sample | Description |
|--------|-------------|
| Basic TTS | Simple text-to-speech usage |
| Dialogue System | Game dialogue with TTS |
| 3D Spatial Audio | Positional audio example |
| Lip Sync | Character lip animation |
| DSP Effects | Audio effects demo |

## 🔧 Requirements

- Unity 2022.3 LTS or later
- [UniTask](https://github.com/Cysharp/UniTask) 2.5.0+
- [Newtonsoft.Json](https://docs.unity3d.com/Packages/com.unity.nuget.newtonsoft-json@3.2/manual/index.html) 3.2.1+

## 📄 License

MIT License - see [LICENSE](LICENSE) for details.

## 🔗 Links

- [VARCO Voice Console](https://voice.varco.ai)
- [API Documentation](https://voice.varco.ai/docs)

---

Made with ❤️ for Unity developers
