# FMODHelpers v1.0.0 - Post-Release Improvements

## v1.0.0 Refactor Summary (COMPLETED ✅)

The v1.0.0 refactor successfully removed ScriptableObject-based references (FMODEventRef, FMODBankRef) in favor of FMOD's native `EventReference` struct. Key accomplishments:

- ✅ Runtime bank lookup via dictionary built at startup
- ✅ All public APIs updated to use EventReference
- ✅ Bank safety mechanism preserved
- ✅ Editor property drawers updated with GUID extraction
- ✅ Obsolete files deleted (~400 lines removed)
- ✅ Demo scene updated
- ✅ Version bumped to 1.0.0 + migration guide created

**Architecture now:**
```
EventReference (FMOD struct) → FMODManager.GetEventInstance() →
Automatic cleanup via DESTROYED callback → Bank safety checks
```

---

## Post-v1.0.0 Improvements (TODO)

Based on architecture review, two key improvements remain:

### 1. Improve Programmer Sound API
**Problem:** Current approach requires manual `soundTable` array management
**Solution:** Store `SoundCreateResult` directly in `FMODUserData`

### 2. Complete Callback Coverage
**Problem:** `IFMODStudioEventHandler` only exposes programmer sound callbacks
**Solution:** Expand interface to cover all FMOD EVENT_CALLBACK_TYPE values

---

## Implementation: Improved Programmer Sound API

### Changes Required

**File:** `Runtime/FMODUserData.cs`

Add field for programmer sounds:
```csharp
#region Public Properties
public int ID => _ID;
public object CustomStateObject { get; set; }
public IntPtr CustomStatePointer { get; set; }
public SoundCreateResult? ProgrammerSoundResult { get; set; } // NEW
#endregion
```

Update Clear():
```csharp
public void Clear()
{
    CurrentInstance.setUserData(IntPtr.Zero);
    CurrentInstance.clearHandle();
    EventRef = new();
    StudioEventCallbackHandler = null;
    CustomStateObject = null;
    CustomStatePointer = IntPtr.Zero;
    ProgrammerSoundResult = null; // NEW
}
```

**File:** `Runtime/FMODEventInstanceExtensions.cs`

Add extension methods:
```csharp
public static void SetProgrammerSound(this EventInstance eventInstance, SoundCreateResult result)
{
    FMODUserData userData = eventInstance.GetUserData();
    if (userData == null)
    {
        Debug.LogError("Cannot set programmer sound on instance without FMODUserData. " +
                      "Did you create this instance via FMODManager.GetEventInstance()?");
        return;
    }
    userData.ProgrammerSoundResult = result;
}

public static SoundCreateResult? GetProgrammerSound(this EventInstance eventInstance)
{
    FMODUserData userData = eventInstance.GetUserData();
    return userData?.ProgrammerSoundResult;
}
```

**File:** `Assets/Scenes/Demo.cs`

Update to use new API:
```csharp
#region State
// REMOVE: SoundCreateResult[] soundTable;
#endregion

#region Unity Lifecycle
void Awake()
{
    // REMOVE: soundTable = new SoundCreateResult[128];
}
#endregion

#region Helpers
void PlaySound(SoundCreateResult result)
{
    EventInstance instance = fmod.GetEventInstance(
        programmerSound,
        EVENT_CALLBACK_TYPE.CREATE_PROGRAMMER_SOUND
    );
    instance.SetProgrammerSound(result); // NEW - replaces soundTable[userData.ID] = result
    instance.RegisterCallbackHandler(this);
    instance.Start();
    instance.Release();
}
#endregion

#region Programmer Instrument
void IFMODStudioEventHandler.OnCreateProgrammerSound(
    bool isMainThread,
    FMODUserData userData,
    ref PROGRAMMER_SOUND_PROPERTIES programmerSoundProperties
)
{
    SoundCreateResult? result = userData.ProgrammerSoundResult; // NEW
    if (result.HasValue)
    {
        programmerSoundProperties.sound = result.Value.Sound.handle;
        programmerSoundProperties.subsoundIndex = result.Value.SubsoundIndex;
    }
}
#endregion
```

### Testing
- Verify programmer sounds play correctly with new API
- Confirm no more manual array indexing needed
- Check that multiple programmer sounds can be active simultaneously

---

## Implementation: Complete Callback Coverage

### Changes Required

**File:** `Runtime/IFMODStudioEventHandler.cs`

Expand interface with all FMOD callbacks (using C# 8.0 default implementations):
```csharp
using FMOD.Studio;

namespace FMODHelpers
{
    public interface IFMODStudioEventHandler
    {
        // Lifecycle callbacks
        void OnCreated(FMODUserData userData) { }
        void OnDestroyed(FMODUserData userData) { }
        void OnStarting(FMODUserData userData) { }
        void OnStarted(FMODUserData userData) { }
        void OnRestarted(FMODUserData userData) { }
        void OnStopped(FMODUserData userData) { }
        void OnStartFailed(FMODUserData userData) { }

        // Programmer sound callbacks (EXISTING)
        void OnCreateProgrammerSound(bool isMainThread, FMODUserData userData,
            ref PROGRAMMER_SOUND_PROPERTIES props) { }
        void OnDestroyProgrammerSound(bool isMainThread, FMODUserData userData,
            ref PROGRAMMER_SOUND_PROPERTIES props) { }

        // Plugin callbacks
        void OnPluginCreated(FMODUserData userData) { }
        void OnPluginDestroyed(FMODUserData userData) { }

        // Timeline callbacks
        void OnTimelineMarker(FMODUserData userData, TIMELINE_MARKER_PROPERTIES props) { }
        void OnTimelineBeat(FMODUserData userData, TIMELINE_BEAT_PROPERTIES props) { }

        // Sound lifecycle
        void OnSoundPlayed(FMODUserData userData) { }
        void OnSoundStopped(FMODUserData userData) { }

        // Virtualization
        void OnRealToVirtual(FMODUserData userData) { }
        void OnVirtualToReal(FMODUserData userData) { }

        // Command callbacks
        void OnStartEventCommand(FMODUserData userData) { }
        void OnNestedTimelineBeat(FMODUserData userData, TIMELINE_NESTED_BEAT_PROPERTIES props) { }
    }
}
```

**File:** `Runtime/FMODNativeCallbackStudioEvent.cs`

Update dispatcher to invoke all callbacks:
```csharp
[AOT.MonoPInvokeCallback(typeof(EVENT_CALLBACK))]
public static FMOD.RESULT StudioEventCallbackInstance(
    EVENT_CALLBACK_TYPE type,
    IntPtr instancePtr,
    IntPtr parameterPtr
)
{
    EventInstance instance = new EventInstance(instancePtr);
    FMODUserData userData = instance.GetUserData();

    if (userData == null)
        return FMOD.RESULT.OK;

    IFMODStudioEventHandler handler = userData.StudioEventCallbackHandler;
    bool isMainThread = FMODManager.IsMainThread;

    switch (type)
    {
        case EVENT_CALLBACK_TYPE.CREATED:
            handler?.OnCreated(userData);
            break;

        case EVENT_CALLBACK_TYPE.DESTROYED:
            handler?.OnDestroyed(userData);
            if (isMainThread)
                userData.Release();
            else
                InvokeOnMainThread(userData.Release);
            break;

        case EVENT_CALLBACK_TYPE.STARTING:
            handler?.OnStarting(userData);
            break;

        case EVENT_CALLBACK_TYPE.STARTED:
            handler?.OnStarted(userData);
            break;

        case EVENT_CALLBACK_TYPE.RESTARTED:
            handler?.OnRestarted(userData);
            break;

        case EVENT_CALLBACK_TYPE.STOPPED:
            handler?.OnStopped(userData);
            break;

        case EVENT_CALLBACK_TYPE.START_FAILED:
            handler?.OnStartFailed(userData);
            break;

        case EVENT_CALLBACK_TYPE.CREATE_PROGRAMMER_SOUND:
            PROGRAMMER_SOUND_PROPERTIES programmerSoundProps =
                (PROGRAMMER_SOUND_PROPERTIES)Marshal.PtrToStructure(
                    parameterPtr, typeof(PROGRAMMER_SOUND_PROPERTIES));
            handler?.OnCreateProgrammerSound(isMainThread, userData, ref programmerSoundProps);
            Marshal.StructureToPtr(programmerSoundProps, parameterPtr, false);
            break;

        case EVENT_CALLBACK_TYPE.DESTROY_PROGRAMMER_SOUND:
            PROGRAMMER_SOUND_PROPERTIES destroyProps =
                (PROGRAMMER_SOUND_PROPERTIES)Marshal.PtrToStructure(
                    parameterPtr, typeof(PROGRAMMER_SOUND_PROPERTIES));
            handler?.OnDestroyProgrammerSound(isMainThread, userData, ref destroyProps);
            DestroySound(destroyProps.sound);
            break;

        case EVENT_CALLBACK_TYPE.PLUGIN_CREATED:
            handler?.OnPluginCreated(userData);
            break;

        case EVENT_CALLBACK_TYPE.PLUGIN_DESTROYED:
            handler?.OnPluginDestroyed(userData);
            break;

        case EVENT_CALLBACK_TYPE.TIMELINE_MARKER:
            TIMELINE_MARKER_PROPERTIES markerProps =
                (TIMELINE_MARKER_PROPERTIES)Marshal.PtrToStructure(
                    parameterPtr, typeof(TIMELINE_MARKER_PROPERTIES));
            handler?.OnTimelineMarker(userData, markerProps);
            break;

        case EVENT_CALLBACK_TYPE.TIMELINE_BEAT:
            TIMELINE_BEAT_PROPERTIES beatProps =
                (TIMELINE_BEAT_PROPERTIES)Marshal.PtrToStructure(
                    parameterPtr, typeof(TIMELINE_BEAT_PROPERTIES));
            handler?.OnTimelineBeat(userData, beatProps);
            break;

        case EVENT_CALLBACK_TYPE.SOUND_PLAYED:
            handler?.OnSoundPlayed(userData);
            break;

        case EVENT_CALLBACK_TYPE.SOUND_STOPPED:
            handler?.OnSoundStopped(userData);
            break;

        case EVENT_CALLBACK_TYPE.REAL_TO_VIRTUAL:
            handler?.OnRealToVirtual(userData);
            break;

        case EVENT_CALLBACK_TYPE.VIRTUAL_TO_REAL:
            handler?.OnVirtualToReal(userData);
            break;

        case EVENT_CALLBACK_TYPE.START_EVENT_COMMAND:
            handler?.OnStartEventCommand(userData);
            break;

        case EVENT_CALLBACK_TYPE.NESTED_TIMELINE_BEAT:
            TIMELINE_NESTED_BEAT_PROPERTIES nestedBeatProps =
                (TIMELINE_NESTED_BEAT_PROPERTIES)Marshal.PtrToStructure(
                    parameterPtr, typeof(TIMELINE_NESTED_BEAT_PROPERTIES));
            handler?.OnNestedTimelineBeat(userData, nestedBeatProps);
            break;
    }

    return FMOD.RESULT.OK;
}
```

### Testing
- Verify each callback type fires correctly
- Test with handlers that override only specific callbacks
- Confirm default implementations (empty methods) cause no issues
- Check that exceptions in user callbacks don't crash FMOD

---

## Benefits of These Improvements

**Programmer Sound API:**
- Cleaner: No manual array management
- Safer: Type-safe storage in FMODUserData
- Flexible: `CustomStateObject` remains available for user data

**Complete Callback Coverage:**
- Comprehensive: All FMOD callbacks available
- Discoverable: IntelliSense shows all options
- Zero-overhead: Default implementations allocate nothing
- Opt-in: Users only override callbacks they need

---

## Version Planning

- **v1.0.0** (current): Core refactor completed
- **v1.1.0** (next): Programmer sound + callback improvements
- **Future**: Consider delegate helpers for quick debugging (with allocation warnings)

---

## Success Criteria for v1.1.0

- ✅ No more `soundTable` pattern in demos
- ✅ All FMOD callback types accessible via interface
- ✅ Zero allocations for callback dispatch
- ✅ Backward compatible (additive changes only)
- ✅ Documentation updated with new patterns
