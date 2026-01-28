# FMODHelpers

A powerful, performance-focused Unity package for working with FMOD Studio, providing automatic resource management, comprehensive callback support, and elegant programmer sound integration.

## Features

- **Automatic Resource Management** - Pooled event instances with automatic cleanup via FMOD callbacks
- **Zero-Allocation Callbacks** - Interface-based callbacks with C# 8.0 default implementations
- **Programmer Sound Support** - Clean API for dynamic audio from files, byte arrays, or audio tables
- **Bank Safety** - Intelligent bank unloading that waits for active sounds to finish
- **Complete Callback Coverage** - Access all 18 FMOD event callback types
- **Performance Optimized** - Dictionary-based lookups, object pooling, IL2CPP compatible

## Installation

### Via Unity Package Manager

1. Open **Window > Package Manager**
2. Click **+ > Add package from git URL**
3. Enter: `https://github.com/yourusername/FMODHelpers.git?path=/Packages/studio.shortsleeve.fmodhelpers`

### Via Git Submodule

```bash
cd Packages
git submodule add https://github.com/yourusername/FMODHelpers.git studio.shortsleeve.fmodhelpers
```

## Quick Start

### Basic Setup

1. Add an `FMODManager` component to a scene GameObject
2. Use FMOD's `EventReference` type for event fields (no ScriptableObjects needed!)

```csharp
using FMODHelpers;
using FMODUnity;
using UnityEngine;

public class AudioExample : MonoBehaviour
{
    [SerializeField] FMODManager fmodManager;
    [SerializeField] EventReference explosionSound;

    void OnCollisionEnter(Collision collision)
    {
        // Play a one-shot sound at a position
        fmodManager.PlayOneShot(explosionSound, transform.position);
    }
}
```

### Sustained Events with Parameters

```csharp
using FMOD.Studio;
using FMODHelpers;
using FMODUnity;
using UnityEngine;

public class MusicPlayer : MonoBehaviour
{
    [SerializeField] FMODManager fmodManager;
    [SerializeField] EventReference musicEvent;

    EventInstance musicInstance;

    void Start()
    {
        // Create a sustained event instance
        musicInstance = fmodManager.GetEventInstance(musicEvent);
        musicInstance.Start();
        musicInstance.Release(); // Enables automatic cleanup when stopped
    }

    void UpdateIntensity(float intensity)
    {
        // Set parameters on the fly
        musicInstance.SetParameter("Intensity", intensity);
    }

    void OnDestroy()
    {
        // Stop with fadeout, cleanup happens automatically
        musicInstance.Stop(immediate: false);
    }
}
```

### Event Callbacks

Implement `IFMODStudioEventHandler` to receive callbacks. Only override the callbacks you need (all methods have default empty implementations):

```csharp
using FMOD.Studio;
using FMODHelpers;
using FMODUnity;
using UnityEngine;

public class MusicBeatTracker : MonoBehaviour, IFMODStudioEventHandler
{
    [SerializeField] FMODManager fmodManager;
    [SerializeField] EventReference musicEvent;

    void Start()
    {
        // Subscribe to timeline beat callbacks
        EventInstance instance = fmodManager.GetEventInstance(musicEvent,
            EVENT_CALLBACK_TYPE.TIMELINE_BEAT);
        instance.RegisterCallbackHandler(this);
        instance.Start();
        instance.Release();
    }

    // Only override callbacks you need - others are no-op by default
    void IFMODStudioEventHandler.OnTimelineBeat(bool isMainThread,
        FMODUserData userData, ref TIMELINE_BEAT_PROPERTIES props)
    {
        Debug.Log($"Beat: {props.bar}:{props.beat} at tempo {props.tempo}");
        // Trigger visual effects, gameplay events, etc.
    }

    void IFMODStudioEventHandler.OnStopped(bool isMainThread, FMODUserData userData)
    {
        Debug.Log("Music stopped");
    }
}
```

### Programmer Sounds (v1.1.0+)

Load dynamic audio from files, byte arrays, or audio tables:

#### From File

```csharp
using FMODHelpers;
using FMODUnity;
using UnityEngine;

public class DynamicAudioPlayer : MonoBehaviour, IFMODStudioEventHandler
{
    [SerializeField] FMODManager fmodManager;
    [SerializeField] EventReference programmerSoundEvent;

    async Awaitable PlayAudioFile(string filePath)
    {
        // Load sound from file asynchronously
        SoundCreateResult sound = await FMODShortcuts.CreateSoundFromFile(
            filePath, destroyCancellationToken);

        // Create event instance with programmer sound callback
        EventInstance instance = fmodManager.GetEventInstance(programmerSoundEvent,
            EVENT_CALLBACK_TYPE.CREATE_PROGRAMMER_SOUND);

        // Attach the sound to the instance
        instance.SetProgrammerSound(sound);
        instance.RegisterCallbackHandler(this);
        instance.Start();
        instance.Release(); // Auto-cleanup when done
    }

    void IFMODStudioEventHandler.OnCreateProgrammerSound(bool isMainThread,
        FMODUserData userData, ref PROGRAMMER_SOUND_PROPERTIES props)
    {
        // Retrieve and provide the sound to FMOD
        SoundCreateResult? result = userData.ProgrammerSoundResult;
        if (result.HasValue)
        {
            props.sound = result.Value.Sound.handle;
            props.subsoundIndex = result.Value.SubsoundIndex;
        }
    }
}
```

#### From Audio Table

```csharp
async Awaitable PlayFromAudioTable(string audioKey)
{
    SoundCreateResult sound = await FMODShortcuts.CreateSoundFromAudioTable(
        audioKey, destroyCancellationToken);

    EventInstance instance = fmodManager.GetEventInstance(programmerSoundEvent,
        EVENT_CALLBACK_TYPE.CREATE_PROGRAMMER_SOUND);
    instance.SetProgrammerSound(sound);
    instance.RegisterCallbackHandler(this);
    instance.Start();
    instance.Release();
}
```

#### From Raw PCM Data (e.g., TTS)

```csharp
async Awaitable PlayGeneratedAudio(float[] samples, int sampleRate)
{
    // Convert float samples to bytes
    byte[] bytes = new byte[samples.Length * sizeof(float)];
    System.Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);

    // Create sound from raw PCM data
    SoundCreateResult sound = await FMODShortcuts.CreateSoundFromByteArray(
        new FMODShortcuts.CreateSoundInfo
        {
            Data = bytes,
            Frequency = sampleRate,
            NumberOfChannels = 1, // Mono
            Format = FMOD.SOUND_FORMAT.PCMFLOAT,
            IsRaw = true
        },
        destroyCancellationToken
    );

    EventInstance instance = fmodManager.GetEventInstance(programmerSoundEvent,
        EVENT_CALLBACK_TYPE.CREATE_PROGRAMMER_SOUND);
    instance.SetProgrammerSound(sound);
    instance.RegisterCallbackHandler(this);
    instance.Start();
    instance.Release();
}
```

### Bank Management

```csharp
using FMODHelpers;
using UnityEngine;

public class BankLoader : MonoBehaviour
{
    [SerializeField] FMODManager fmodManager;

    async Awaitable LoadMusicBank()
    {
        // Load bank with sample data
        fmodManager.LoadBankAsync("Music", loadSamples: true);

        // Wait for all banks to finish loading
        await Awaitable.WaitUntil(fmodManager.HaveBanksLoadedPredicate);

        Debug.Log("Music bank loaded!");
    }

    void UnloadMusicBank()
    {
        // Safe unload - will wait until all sounds from this bank finish
        fmodManager.UnloadBankAsync("bank:/Music");
    }
}
```

### Bus Volume Control

```csharp
using FMODHelpers;
using UnityEngine;

public class VolumeSettings : MonoBehaviour
{
    [SerializeField] FMODManager fmodManager;

    public void SetMasterVolume(float volume)
    {
        // Volume range: 0.0 to 1.0
        fmodManager.SetMasterVolume(volume);
    }

    public void SetMusicVolume(float volume)
    {
        var musicBus = fmodManager.GetBus("bus:/Music");
        musicBus.setVolume(volume);
    }
}
```

### Global Parameters

```csharp
using FMODHelpers;
using UnityEngine;

public class GameStateAudio : MonoBehaviour
{
    [SerializeField] FMODManager fmodManager;

    void OnEnterCombat()
    {
        // Set a global parameter that affects all events
        fmodManager.SetGlobalParameter("InCombat", 1f);
    }

    void OnExitCombat()
    {
        fmodManager.SetGlobalParameter("InCombat", 0f);
    }
}
```

## Architecture

### Resource Management

FMODHelpers uses sophisticated pooling to minimize garbage collection:

- **FMODUserData Pool**: Pre-allocated user data objects reused across event instances
- **EventInstanceData Pool**: Tracks active events per GUID for bank safety
- **GCHandle Management**: Prevents GC during native callbacks

### Automatic Cleanup

When you call `instance.Release()` after `Start()`, cleanup happens automatically via the DESTROYED callback:

1. Event finishes playing naturally
2. DESTROYED callback fires
3. FMODUserData is released back to pool
4. Play count is decremented
5. Banks can now be safely unloaded if needed

### Bank Safety

The library builds a complete GUID→Bank mapping at startup:

- Prevents unloading banks with active sounds
- Deferred unloading in `Update()` loop
- Supports events that span multiple banks

## API Reference

### FMODManager

| Method | Description |
|--------|-------------|
| `PlayOneShot(EventReference, Vector3)` | Plays a fire-and-forget sound at a position |
| `GetEventInstance(EventReference, EVENT_CALLBACK_TYPE)` | Creates a managed event instance |
| `LoadBankAsync(string, bool)` | Loads a bank asynchronously |
| `UnloadBankAsync(string)` | Unloads a bank when safe |
| `IsAnyBankLoading()` | Checks if banks are still loading |
| `SetGlobalParameter(string, float)` | Sets a global FMOD parameter |
| `SetMasterVolume(float)` | Sets master bus volume |
| `GetBus(string)` | Gets a bus by path |

### EventInstance Extensions

| Method | Description |
|--------|-------------|
| `Start()` | Starts playback |
| `Stop(bool immediate)` | Stops playback (with or without fadeout) |
| `Release()` | Marks for release (enables auto-cleanup) |
| `Pause(bool pause)` | Pauses/unpauses |
| `SetParameter(string, float)` | Sets a local parameter |
| `GetParameter(string)` | Gets a local parameter value |
| `SetProgrammerSound(SoundCreateResult)` | Attaches a sound for programmer events |
| `RegisterCallbackHandler(IFMODStudioEventHandler)` | Registers callback handler |
| `AttachToGameObject(GameObject)` | Makes event follow a GameObject |
| `GetPositionSeconds()` | Gets timeline position |

### FMODShortcuts

| Method | Description |
|--------|-------------|
| `CreateSoundFromFile(string, CancellationToken)` | Loads audio from file path |
| `CreateSoundFromByteArray(CreateSoundInfo, CancellationToken)` | Creates sound from raw data |
| `CreateSoundFromAudioTable(string, CancellationToken)` | Loads from FMOD audio table |

### IFMODStudioEventHandler Callbacks

All callbacks are optional (default empty implementations):

- `OnCreated` / `OnDestroyed`
- `OnStarting` / `OnStarted` / `OnRestarted` / `OnStopped` / `OnStartFailed`
- `OnCreateProgrammerSound` / `OnDestroyProgrammerSound`
- `OnPluginCreated` / `OnPluginDestroyed`
- `OnTimelineMarker` / `OnTimelineBeat` / `OnNestedTimelineBeat`
- `OnSoundPlayed` / `OnSoundStopped`
- `OnRealToVirtual` / `OnVirtualToReal`
- `OnStartEventCommand`

## Migration Guide

Upgrading from v0.x? See [MIGRATION.md](MIGRATION.md) for detailed upgrade instructions.

## Performance Characteristics

- **Zero allocations** in hot paths (callback dispatch, event playback)
- **O(1) lookups** for bank safety checks via Dictionary
- **Pre-allocated pools** prevent mid-game GC spikes
- **IL2CPP compatible** with proper `[AOT.MonoPInvokeCallback]` attributes

## Requirements

- Unity 6.0+ (uses `Awaitable` API)
- FMOD for Unity 2.02+
- C# 8.0+ (default interface implementations)

## Version History

### v1.1.0
- Added `SetProgrammerSound()` / `GetProgrammerSound()` API
- Complete callback coverage (all 18 FMOD callback types)
- Eliminated manual array management for programmer sounds

### v1.0.0
- Major refactor: removed ScriptableObject-based references
- Native FMOD `EventReference` integration
- Dictionary-based bank safety mechanism
- ~400 lines of code removed

## License

MIT License - See [LICENSE](LICENSE) for details

## Contributing

Contributions welcome! Please submit issues and pull requests on GitHub.

## Credits

Created by [Your Name] • [Your Website]
