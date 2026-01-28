# FMODHelpers Library - Code Review

## Executive Summary

The FMODHelpers library is well-architected with good separation of concerns, proper pooling strategies, and solid FMOD integration patterns. However, there is **one critical bug** and several opportunities for improvement in DRY principles, consistency, and best practices.

---

## 1. Critical Bug 🔴

### FMODNativeCallbackStudioEvent.cs:235 - Wrong Callback Invoked

**Severity:** HIGH

```csharp
case FMOD.Studio.EVENT_CALLBACK_TYPE.SOUND_STOPPED:
    FMOD.Sound soundStopped = (FMOD.Sound)
        Marshal.PtrToStructure(parameterPtr, typeof(FMOD.Sound));
    try
    {
        userData?.StudioEventCallbackHandler.OnSoundPlayed( // BUG: Should be OnSoundStopped
            FMODManager.IsMainThread,
            userData,
            ref soundStopped
        );
    }
```

**Impact:** The SOUND_STOPPED event incorrectly calls `OnSoundPlayed()` instead of `OnSoundStopped()`. This prevents users from properly handling sound stop events.

**Fix:** Change `OnSoundPlayed` to `OnSoundStopped` on line 235.

---

## 2. DRY Violations

### 2.1 Duplicate GUID Extraction Code

**Location:**
- `FMODParameterDrawer.cs:118-136`
- `FMODParameterizedEventDrawer.cs:77-95`

**Issue:** Identical `ExtractGuidFromEventReference()` method exists in both property drawers.

**Recommendation:** Create a shared `FMODEditorUtilities` class:

```csharp
namespace FMODHelpers.Editor
{
    public static class FMODEditorUtilities
    {
        public static FMOD.GUID ExtractGuidFromEventReference(SerializedProperty eventRefProperty)
        {
            // ... shared implementation
        }

        public static bool IsGuidNull(FMOD.GUID guid)
        {
            return guid.Data1 == 0 && guid.Data2 == 0 && guid.Data3 == 0 && guid.Data4 == 0;
        }
    }
}
```

---

## 3. Architecture Review

### Strengths ✅

1. **Excellent Pooling Strategy**
   - FMODUserData pooling prevents GC pressure
   - EventInstanceData pooling using Unity's ObjectPool
   - Proper GCHandle lifecycle management

2. **Smart Bank Safety Mechanism**
   - Dictionary-based bank lookup prevents unloading banks with active sounds
   - Deferred bank unloading in Update() is elegant

3. **Callback Architecture**
   - Interface with C# 8.0 default implementations allows opt-in behavior
   - Try-catch around user callbacks prevents crashes
   - Proper main thread detection

4. **Extension Method Pattern**
   - FMODEventInstanceExtensions provides clean, discoverable API
   - Consistent error handling and logging

5. **New Programmer Sound API** (v1.1.0)
   - Storing SoundCreateResult in FMODUserData eliminates manual array management
   - Type-safe and elegant

### Areas for Improvement

#### 3.1 FMODManager._activeInstances Performance

**Current:** `List<EventInstanceData>` with linear search (O(n))
```csharp
// Line 247-259: Linear search for every event play
for (int i = 0; i < _activeInstances.Count; i++)
{
    if (eventInstanceData.EventRef.Guid.Equals(eventRef.Guid))
    {
        eventInstanceData.PlayCount += 1;
        return;
    }
}
```

**Issue:** The comment on line 38 acknowledges this: `"maybe should be something other than a list"`

**Recommendation:** Use `Dictionary<FMOD.GUID, EventInstanceData>` for O(1) lookups:
```csharp
Dictionary<FMOD.GUID, EventInstanceData> _activeInstances;
```

#### 3.2 FMODShortcuts.WaitUntilLoaded Missing Error Handling

**Current:** Only checks for `FMOD.OPENSTATE.READY`
```csharp
while (openstate != FMOD.OPENSTATE.READY)
{
    await Awaitable.NextFrameAsync(token);
    sound.getOpenState(out openstate, ...);
}
```

**Issue:** Doesn't handle `FMOD.OPENSTATE.ERROR` - could hang indefinitely on corrupted files.

**Recommendation:**
```csharp
while (openstate != FMOD.OPENSTATE.READY)
{
    if (openstate == FMOD.OPENSTATE.ERROR)
    {
        Debug.LogError("Failed to load sound - file may be corrupted");
        return;
    }
    await Awaitable.NextFrameAsync(token);
    sound.getOpenState(out openstate, ...);
}
```

---

## 4. Best Practices Issues

### 4.1 Empty Catch Blocks

**Location:** Throughout `FMODNativeCallbackStudioEvent.cs`

**Issue:** All user callback invocations have empty catch blocks:
```csharp
try
{
    userData?.StudioEventCallbackHandler.OnCreated(
        FMODManager.IsMainThread,
        userData
    );
}
catch { } // Empty - exceptions are silently swallowed
```

**Recommendation:** Log exceptions in debug/editor mode:
```csharp
catch (Exception ex)
{
    #if UNITY_EDITOR || DEVELOPMENT_BUILD
    Debug.LogError($"Exception in FMOD callback handler: {ex}");
    #endif
}
```

### 4.2 FMODParameterLocal Factory Method

**Current:** Static factory method instead of constructor
```csharp
public static FMODParameterLocal Create(string name)
{
    FMODParameterLocal parameter = new();
    parameter.name = name;
    parameter.value = 0f;
    parameter.skipSeek = false;
    return parameter;
}
```

**Issue:** Inconsistent with typical C# patterns; name field has `[ReadOnly]` attribute but is set via factory.

**Recommendation:** Use a constructor:
```csharp
public FMODParameterLocal(string name)
{
    this.name = name;
    this.value = 0f;
    this.skipSeek = false;
}
```

### 4.3 Debug.Log in Production Code

**Location:** `FMODManager.OnDestroy():97`
```csharp
Debug.Log($"DESTROYING {_inactiveUserData.Count + _activeInstances.Count} USER DATA");
```

**Issue:** Debug.Log in production code creates log spam.

**Recommendation:** Use conditional compilation or remove:
```csharp
#if UNITY_EDITOR || DEVELOPMENT_BUILD
Debug.Log($"DESTROYING {_inactiveUserData.Count + _activeInstances.Count} USER DATA");
#endif
```

### 4.4 Missing Null Checks

**Location:** `FMODManager.BuildCompleteBankLookup():324`
```csharp
if (Settings.Instance.MasterBanks != null) // Settings.Instance itself not checked
```

**Recommendation:**
```csharp
if (Settings.Instance != null && Settings.Instance.MasterBanks != null)
```

---

## 5. Consistency Issues

### 5.1 Inconsistent Object Initialization

**Examples:**
- `EventRef = new()` (line 70, FMODUserData.cs)
- `return new EventInstance()` (line 206, FMODManager.cs)
- `FMODParameterLocal parameter = new()` (line 17, FMODParameterLocal.cs)

**Recommendation:** Choose one style and apply consistently. Modern C# prefers `new()` where type is inferred.

### 5.2 Empty Regions

**Location:** `Demo.cs:25-26`
```csharp
#region Unity Lifecycle
#endregion
```

**Issue:** Empty regions add no value and clutter code.

**Recommendation:** Remove empty regions.

### 5.3 Inconsistent Error Message Formatting

**Examples:**
- With result: `Debug.LogError($"Failed to set {bus} volume: {result}");` (line 165)
- Without result: `Debug.LogError("Can't pause a destroyed sound instance");` (line 22)

**Recommendation:** Standardize on including context when available.

---

## 6. Potential Correctness Issues

### 6.1 FMODParameterLocal Equality Implementation

**Issue:** Equality/hashing only considers `name` field, ignoring `value` and `skipSeek`:
```csharp
public bool Equals(FMODParameterLocal other) =>
    GetComparerString().Equals(other.GetComparerString());

private string GetComparerString()
{
    if (string.IsNullOrEmpty(name))
        return string.Empty;
    else
        return name; // Only name - not value or skipSeek
}
```

**Impact:** If used in `HashSet<FMODParameterLocal>` or as dictionary keys, could have unexpected behavior.

**Recommendation:** Either:
1. Include all fields in equality comparison, or
2. Document that equality is name-based only

### 6.2 GetComparerString Simplification

**Current:**
```csharp
private string GetComparerString()
{
    if (string.IsNullOrEmpty(name))
        return string.Empty;
    else
        return name;
}
```

**Simplified:**
```csharp
private string GetComparerString() => name ?? string.Empty;
```

### 6.3 Async Void Pattern

**Location:** `FMODNativeCallbackStudioEvent.ReleaseUserData():299`
```csharp
static async void ReleaseUserData(FMODUserData data)
```

**Issue:** `async void` methods can't be awaited and exceptions escape to unobserved task exception handlers.

**Context:** This is acceptable here since it's a fire-and-forget callback, but worth noting for maintainability.

---

## 7. Positive Highlights

### What This Library Does Exceptionally Well:

1. **Zero-Allocation Callback Dispatch**
   - Interface with default implementations means no delegates/allocations
   - Perfect for audio code that runs frequently

2. **Proper IL2CPP Support**
   - `[AOT.MonoPInvokeCallback]` attribute present
   - GCHandle usage for native-managed interop

3. **Cancellation Token Support**
   - Modern async patterns with proper cancellation
   - `destroyCancellationToken` integration

4. **Smart Resource Management**
   - Pooling prevents GC spikes during gameplay
   - Automatic cleanup via DESTROYED callback

5. **Bank Safety**
   - Prevents unloading banks with active events
   - Dictionary-based lookup is efficient

6. **Extension Method API**
   - Clean, discoverable, and idiomatic C#
   - Proper error messages guide users

---

## 8. Recommendations Summary

### High Priority
1. ✅ **Fix critical bug**: Change `OnSoundPlayed` to `OnSoundStopped` in SOUND_STOPPED handler
2. ✅ Add error state handling in `WaitUntilLoaded()`
3. ✅ Replace `_activeInstances` List with Dictionary for O(1) lookups

### Medium Priority
4. ✅ Extract duplicate GUID helper methods to shared utility class
5. ✅ Add exception logging in catch blocks (editor/development builds only)
6. ✅ Add null check for `Settings.Instance`
7. ✅ Remove or guard production Debug.Log statements

### Low Priority
8. ✅ Remove empty regions
9. ✅ Standardize object initialization syntax
10. ✅ Document or fix FMODParameterLocal equality semantics
11. ✅ Simplify GetComparerString()
12. ✅ Consider constructor instead of factory method for FMODParameterLocal

---

## 9. Overall Assessment

**Rating: 8/10**

This is a well-engineered library with thoughtful architecture and good performance characteristics. The v1.1.0 refactor successfully eliminated ScriptableObject overhead and improved the programmer sound API.

The critical bug is easy to fix, and most other issues are minor consistency or best-practice improvements. The core architecture is solid and production-ready.

**Primary Strengths:**
- Performance-conscious design
- Clean separation of concerns
- Proper Unity/FMOD integration patterns

**Primary Weakness:**
- One critical callback bug
- Minor DRY violations in editor code
- Could benefit from better error handling in edge cases
