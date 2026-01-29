using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;
using UnityEngine.Pool;

namespace FMODHelpers
{
    public class FMODManager : MonoBehaviour
    {
        #region Constants
        public const float MinBusVolume = 0.00001f;
        public const string BusMaster = "bus:/";
        #endregion

        #region Static
        static Thread MainThread;
        public static bool IsMainThread => Thread.CurrentThread == MainThread;
        public static bool IsShuttingDown { get; private set; }
        #endregion

        #region Inspector
        [SerializeField]
        int initialInstancePoolSize = 32;
        #endregion

        #region State
        Bus _masterBus;
        Func<bool> _haveBanksLoaded;
        List<string> _banksPendingUnload;
        Stack<FMODUserData> _inactiveUserData;
        HashSet<FMODUserData> _activeUserData;
        Action<FMODUserData> _releaseUserDataAction;
        Dictionary<FMOD.GUID, EventInstanceData> _activeInstances;
        ObjectPool<EventInstanceData> _eventInstanceDataPool;
        Dictionary<FMOD.GUID, HashSet<string>> _eventGuidToBankPaths;
        #endregion

        #region Unity Lifecycle
        void Awake()
        {
            // Set Main Thread
            MainThread = Thread.CurrentThread;

            // Cached Predicates
            _releaseUserDataAction = ReleaseUserData;
            _haveBanksLoaded = () => !IsAnyBankLoading();

            // Instance Collections
            _activeInstances = new(initialInstancePoolSize);
            _inactiveUserData = new(initialInstancePoolSize);
            _activeUserData = new(initialInstancePoolSize);
            for (int i = 0; i < initialInstancePoolSize; i++)
                AddNewInactiveInstance();

            // Bank Collection
            _banksPendingUnload = new();

            // Event Instance Pool
            _eventInstanceDataPool = new(
                () => new EventInstanceData(),
                defaultCapacity: initialInstancePoolSize
            );

            // Build complete bank lookup
            BuildCompleteBankLookup();
        }

        void Update()
        {
            // Process queued FMOD callbacks on main thread
            FMODNativeCallbackStudioEvent.ProcessCallbacks();

            // Unload any banks that need unloading
            if (_banksPendingUnload.Count > 0)
            {
                // But first make sure no sounds are playing from those banks
                for (int i = _banksPendingUnload.Count - 1; i >= 0; i--)
                {
                    // Grab bank path
                    string bankPath = _banksPendingUnload[i];

                    // Don't unload banks that have sounds playing
                    if (_activeInstances.Count > 0 && AnySoundsPlayingFromBank(bankPath))
                        continue;

                    // Unload
                    RuntimeManager.UnloadBank(bankPath);
                    _banksPendingUnload.RemoveAt(i);
                }
            }
        }

        void OnDestroy()
        {
            // Phase 1: Signal shutdown - callbacks will short-circuit
            IsShuttingDown = true;

            // Phase 2: Stop all active instances and unregister callbacks
            foreach (FMODUserData data in _activeUserData)
            {
                if (data.CurrentInstance.isValid())
                {
                    data.CurrentInstance.setCallback(null, 0);
                    data.CurrentInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
                    data.CurrentInstance.release();
                }
            }

            // Phase 3: Flush FMOD's command buffer - safe because callbacks just enqueue and return
            RuntimeManager.StudioSystem.flushCommands();

            // Phase 4: Clear any remaining queued callbacks and free handles
            FMODNativeCallbackStudioEvent.ClearQueue();

            foreach (FMODUserData data in _activeUserData)
            {
                data.Handle.Free();
            }
            foreach (FMODUserData data in _inactiveUserData)
            {
                data.Handle.Free();
            }
        }
        #endregion

        #region API
        /// <summary>
        /// Loads an FMOD bank asynchronously. Bank will not unload while events from it are playing.
        /// </summary>
        /// <param name="bankName">Name of the bank (without "bank:/" prefix), e.g. "Music"</param>
        /// <param name="loadSamples">If true, loads sample data immediately. If false, loads metadata only.</param>
        public void LoadBankAsync(string bankName, bool loadSamples)
        {
            try
            {
                RuntimeManager.LoadBank(bankName, loadSamples);
            }
            catch (BankLoadException e)
            {
                Debug.LogException(e);
            }
        }

        /// <summary>
        /// Unloads an FMOD bank when it's safe to do so (no active instances from that bank).
        /// Bank unloading is deferred until all sounds from the bank have finished.
        /// </summary>
        /// <param name="bankName">Name of the bank (without "bank:/" prefix), e.g. "Music"</param>
        public void UnloadBankAsync(string bankName)
        {
            string bankPath = bankName.StartsWith("bank:/") ? bankName : $"bank:/{bankName}";
            _banksPendingUnload.Add(bankPath);
        }

        /// <summary>
        /// Checks if any FMOD banks are currently loading sample data.
        /// </summary>
        /// <returns>True if any banks are still loading</returns>
        public bool IsAnyBankLoading() => RuntimeManager.AnySampleDataLoading();

        /// <summary>
        /// Gets a predicate function that returns true when all banks have finished loading.
        /// Useful for async await patterns.
        /// </summary>
        public Func<bool> HaveBanksLoadedPredicate => _haveBanksLoaded;

        bool AnySoundsPlayingFromBank(string bankPath)
        {
            foreach (KeyValuePair<FMOD.GUID, EventInstanceData> kvp in _activeInstances)
            {
                // Direct HashSet lookup - no array allocation
                if (_eventGuidToBankPaths.TryGetValue(kvp.Key, out HashSet<string> paths))
                {
                    if (paths.Contains(bankPath))
                        return true;
                }
            }
            return false;
        }
        #endregion

        #region Public Bus API
        /// <summary>
        /// Gets an FMOD bus by path.
        /// </summary>
        /// <param name="busPath">Bus path, e.g. "bus:/Music" or "bus:/SFX/UI"</param>
        /// <returns>FMOD Bus instance</returns>
        public Bus GetBus(string busPath) => RuntimeManager.GetBus(busPath);

        /// <summary>
        /// Sets the master bus volume (all audio).
        /// </summary>
        /// <param name="volume">Volume level (0.0 to 1.0)</param>
        public void SetMasterVolume(float volume) => SetBusVolume(MasterBus, volume);

        /// <summary>
        /// Gets the current master bus volume.
        /// </summary>
        /// <returns>Current volume level (0.0 to 1.0)</returns>
        public float GetMasterVolume() => GetBusVolume(MasterBus);

        Bus MasterBus
        {
            get
            {
                if (!_masterBus.isValid())
                    _masterBus = RuntimeManager.GetBus(BusMaster);
                return _masterBus;
            }
        }

        void SetBusVolume(Bus bus, float volume)
        {
            // If you set it to 0, it mutes all buses
            FMOD.RESULT result = bus.setVolume(Mathf.Clamp(volume, MinBusVolume, 1f));
            if (result != FMOD.RESULT.OK)
                Debug.LogError($"Failed to set {bus} volume: {result}");
        }

        float GetBusVolume(Bus bus)
        {
            float volume;
            FMOD.RESULT result = bus.getVolume(out volume);
            if (result != FMOD.RESULT.OK)
            {
                Debug.LogError($"Failed to fetch {bus} volume: {result}");
                return 0f;
            }
            return volume;
        }
        #endregion

        #region Playback API
        /// <summary>
        /// Plays a one-shot (fire-and-forget) event at the origin (0,0,0).
        /// Automatically cleans up when finished.
        /// </summary>
        /// <param name="eventRef">FMOD event reference to play</param>
        public void PlayOneShot(EventReference eventRef) => PlayOneShot(eventRef, Vector3.zero);

        /// <summary>
        /// Plays a one-shot (fire-and-forget) event at a world position.
        /// Automatically cleans up when finished.
        /// </summary>
        /// <param name="eventRef">FMOD event reference to play</param>
        /// <param name="atPosition">World position where the sound should play</param>
        public void PlayOneShot(EventReference eventRef, Vector3 atPosition)
        {
            EventInstance eventInstance = GetEventInstance(eventRef);
            eventInstance.set3DAttributes(RuntimeUtils.To3DAttributes(atPosition));
            eventInstance.start();
            eventInstance.release();
        }

        /// <summary>
        /// Creates an FMOD event instance with automatic cleanup via DESTROYED callback.
        /// Call Release() after Start() to enable automatic cleanup when the sound finishes.
        /// </summary>
        /// <param name="eventRef">FMOD event reference to instantiate</param>
        /// <param name="callbacks">Additional callbacks to subscribe to (DESTROYED, CREATE_PROGRAMMER_SOUND, and DESTROY_PROGRAMMER_SOUND are always included)</param>
        /// <returns>A valid EventInstance, or an invalid instance if creation failed</returns>
        /// <example>
        /// EventInstance instance = fmodManager.GetEventInstance(myEvent);
        /// instance.Start();
        /// instance.Release(); // Enables automatic cleanup via DESTROYED callback
        /// </example>
        public EventInstance GetEventInstance(
            EventReference eventRef,
            EVENT_CALLBACK_TYPE callbacks = EVENT_CALLBACK_TYPE.DESTROYED
        )
        {
            if (eventRef.IsNull)
            {
                Debug.LogError("Cannot create instance from null EventReference");
                return new();
            }

            EventInstance eventInstance = RuntimeManager.CreateInstance(eventRef.Guid);
            if (!eventInstance.isValid())
            {
                Debug.LogError($"Failed to create EventInstance for GUID {eventRef.Guid}. " +
                              "Event may not exist or bank may not be loaded.");
                return eventInstance;
            }

            FMODUserData userData = TryGetUserData(eventRef);
            userData.CurrentInstance = eventInstance;
            eventInstance.setUserData(GCHandle.ToIntPtr(userData.Handle));
            eventInstance.setCallback(
                FMODNativeCallbackStudioEvent.StudioEventCallbackInstance,
                // There's cleanup we have to do for these guys so we always listen
                EVENT_CALLBACK_TYPE.CREATE_PROGRAMMER_SOUND
                    | EVENT_CALLBACK_TYPE.DESTROY_PROGRAMMER_SOUND
                    | EVENT_CALLBACK_TYPE.DESTROYED
                    | callbacks
            );
            return eventInstance;
        }

        FMODUserData TryGetUserData(EventReference eventRef)
        {
            // Make sure we have FMODUserData
            if (_inactiveUserData.Count == 0)
                AddNewInactiveInstance();

            // Get and/or Increment Count
            EventInstancePlayCountIncrement(eventRef);

            // Return FMODUserData
            FMODUserData userData = _inactiveUserData.Pop();
            _activeUserData.Add(userData);
            userData.EventRef = eventRef;
            return userData;
        }

        void EventInstancePlayCountIncrement(EventReference eventRef)
        {
            // Try to increment existing
            if (_activeInstances.TryGetValue(eventRef.Guid, out EventInstanceData eventInstanceData))
            {
                eventInstanceData.PlayCount += 1;
                return;
            }

            // Create new entry
            eventInstanceData = _eventInstanceDataPool.Get();
            eventInstanceData.EventRef = eventRef;
            eventInstanceData.PlayCount = 1;
            _activeInstances[eventRef.Guid] = eventInstanceData;
        }

        void EventInstancePlayCountDecrement(EventReference eventRef)
        {
            // Try to decrement
            if (_activeInstances.TryGetValue(eventRef.Guid, out EventInstanceData eventInstanceData))
            {
                eventInstanceData.PlayCount -= 1;
                if (eventInstanceData.PlayCount == 0)
                {
                    _activeInstances.Remove(eventRef.Guid);
                    _eventInstanceDataPool.Release(eventInstanceData);
                }
                return;
            }

            // Error: tried to decrement non-existent instance
            #if UNITY_EDITOR
            throw new Exception($"Tried to decrement play count on event not playing: {eventRef.Path}");
            #else
            throw new Exception($"Tried to decrement play count on event not playing: {eventRef.Guid}");
            #endif
        }
        #endregion

        #region Public Parameter API
        /// <summary>
        /// Sets a global FMOD parameter value.
        /// Global parameters affect all events that reference them.
        /// </summary>
        /// <param name="name">Parameter name as defined in FMOD Studio</param>
        /// <param name="value">Parameter value to set</param>
        public void SetGlobalParameter(string name, float value) => SetParameter(name, value);

        void SetParameter(string name, float value) =>
            RuntimeManager.StudioSystem.setParameterByName(name, value);
        #endregion

        #region Private API
        void ReleaseUserData(FMODUserData userData)
        {
            EventInstancePlayCountDecrement(userData.EventRef);
            userData.Clear();
            _activeUserData.Remove(userData);
            _inactiveUserData.Push(userData);
        }

        void AddNewInactiveInstance()
        {
            _inactiveUserData.Push(
                FMODUserData.Create(_releaseUserDataAction, destroyCancellationToken)
            );
        }

        void BuildCompleteBankLookup()
        {
            _eventGuidToBankPaths = new();

            // Collect all bank names from Settings
            HashSet<string> allBankNames = new();

            if (Settings.Instance != null && Settings.Instance.MasterBanks != null)
            {
                foreach (string masterBank in Settings.Instance.MasterBanks)
                {
                    allBankNames.Add(masterBank);
                    allBankNames.Add(masterBank + ".strings"); // Master banks have .strings
                }
            }

            if (Settings.Instance != null && Settings.Instance.Banks != null)
            {
                foreach (string bank in Settings.Instance.Banks)
                    allBankNames.Add(bank);
            }

            // Track banks we temporarily load (to unload them after)
            List<string> temporaryBanks = ListPool<string>.Get();

            try
            {
                foreach (string bankName in allBankNames)
                {
                    // Check if bank is already loaded
                    bool alreadyLoaded = RuntimeManager.HasBankLoaded(bankName);

                    if (!alreadyLoaded)
                    {
                        // Load metadata only (no sample data)
                        RuntimeManager.LoadBank(bankName, loadSamples: false);
                        temporaryBanks.Add(bankName);
                    }

                    // Get bank handle and enumerate its events
                    string bankPath = "bank:/" + bankName;
                    FMOD.Studio.Bank bank;
                    if (RuntimeManager.StudioSystem.getBank(bankPath, out bank) != FMOD.RESULT.OK)
                        continue;

                    FMOD.Studio.EventDescription[] events;
                    if (bank.getEventList(out events) != FMOD.RESULT.OK)
                        continue;

                    foreach (FMOD.Studio.EventDescription eventDesc in events)
                    {
                        FMOD.GUID guid;
                        if (eventDesc.getID(out guid) != FMOD.RESULT.OK)
                            continue;

                        // Reuse existing HashSet if present (for multi-bank events)
                        if (!_eventGuidToBankPaths.TryGetValue(guid, out HashSet<string> bankSet))
                        {
                            bankSet = new();
                            _eventGuidToBankPaths[guid] = bankSet;
                        }

                        bankSet.Add(bankPath);
                    }
                }

                // Unload banks we temporarily loaded
                foreach (string bankName in temporaryBanks)
                {
                    RuntimeManager.UnloadBank(bankName);
                }
            }
            finally
            {
                ListPool<string>.Release(temporaryBanks);
            }
        }
        #endregion

        #region Helper Constructs
        class EventInstanceData
        {
            public int PlayCount;
            public EventReference EventRef;
        }
        #endregion
    }
}
