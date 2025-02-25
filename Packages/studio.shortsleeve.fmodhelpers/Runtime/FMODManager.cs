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
        public const float MaxVolumeDBs = 10f;
        public const float MinVolumeDBs = -80f;
        public const string BusMaster = "bus:/";
        #endregion

        #region Inspector
        [SerializeField]
        FMODEventRef dialogueEvent;

        [SerializeField]
        int initialInstancePoolSize = 32;
        #endregion

        #region State
        Bus _masterBus;
        Func<bool> _haveBanksLoaded;
        List<FMODBankRef> _banksPendingUnload;
        Stack<FMODUserData> _inactiveInstances;
        Action<FMODUserData> _releaseUserDataAction;
        List<EventInstanceData> _activeInstances; // maybe should be something other than a list
        ObjectPool<EventInstanceData> _eventInstanceDataPool;
        #endregion

        #region Unity Lifecycle
        void Awake()
        {
            // Cached Predicates
            _releaseUserDataAction = ReleaseUserData;
            _haveBanksLoaded = () => !IsAnyBankLoading();

            // Instance Collections
            _activeInstances = new(initialInstancePoolSize);
            _inactiveInstances = new(initialInstancePoolSize);
            for (int i = 0; i < initialInstancePoolSize; i++)
                AddNewInactiveInstance();

            // Bank Collection
            _banksPendingUnload = new();

            // Event Instance Pool
            _eventInstanceDataPool = new(
                () => new EventInstanceData(),
                defaultCapacity: initialInstancePoolSize
            );
        }

        void Update()
        {
            // Unload any banks that need unloading
            if (_banksPendingUnload.Count > 0)
            {
                // But first make sure no sounds are playing from those banks
                for (int i = _banksPendingUnload.Count - 1; i >= 0; i--)
                {
                    // Grab bank path
                    FMODBankRef bankRef = _banksPendingUnload[i];

                    // Don't unload banks that have sounds playing
                    if (_activeInstances.Count > 0 && AnySoundsPlayingFromBank(bankRef))
                        continue;

                    // Unload
                    RuntimeManager.UnloadBank(bankRef.StudioPath);
                    _banksPendingUnload.RemoveAt(i);
                }
            }
        }
        #endregion

        #region API
        public void LoadBankAsync(FMODBankRef bankRef, bool loadSamples)
        {
            try
            {
                RuntimeManager.LoadBank(bankRef.Name, loadSamples);
            }
            catch (BankLoadException e)
            {
                Debug.LogException(e);
            }
        }

        public void UnloadBankAsync(FMODBankRef bankRef) => _banksPendingUnload.Add(bankRef);

        public bool IsAnyBankLoading() => RuntimeManager.AnySampleDataLoading();

        public Func<bool> HaveBanksLoadedPredicate => _haveBanksLoaded;

        bool AnySoundsPlayingFromBank(FMODBankRef bankRef)
        {
            for (int i = 0; i < _activeInstances.Count; i++)
            {
                EventInstanceData eventInstanceData = _activeInstances[i];
                if (Array.IndexOf(eventInstanceData.EventRef.Banks, bankRef) != -1)
                    return true;
            }
            return false;
        }
        #endregion

        #region Public Bus API
        public Bus GetBus(string busPath) => RuntimeManager.GetBus(busPath);

        public void SetMasterVolume(float volume) => SetBusVolume(MasterBus, volume);

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
        public void PlayOneShot(FMODEventRef eventRef) => PlayOneShot(eventRef, Vector3.zero);

        public void PlayOneShot(FMODEventRef eventRef, Vector3 atPosition)
        {
            EventInstance eventInstance = CreateEventInstance(eventRef);
            eventInstance.set3DAttributes(RuntimeUtils.To3DAttributes(atPosition));
            eventInstance.start();
            eventInstance.release();
        }

        /// <summary>
        /// Create a new event instance.
        /// WARNING: Be sure to release this when you're done.
        /// </summary>
        /// <param name="eventRef">Event to instantiate</param>
        /// <returns></returns>
        public EventInstance GetEventInstance(FMODEventRef eventRef) =>
            CreateEventInstance(eventRef);

        /// <summary>
        /// Create a new instance of a programmer sound.
        /// WARNING: Be sure to release this when you're done.
        /// </summary>
        /// <param name="dialogueTableKey">Key to look up the programmer sound</param>
        /// <param name="token">Cancellation token</param>
        /// <returns></returns>
        public async Awaitable<EventInstance> GetDialogueEventInstance(
            string dialogueTableKey,
            CancellationToken token
        )
        {
            // Return Value
            EventInstance eventInstance;
            eventInstance.handle = IntPtr.Zero;

            // Load Sound Path
            SOUND_INFO dialogueSoundInfo;
            FMOD.RESULT keyResult = RuntimeManager.StudioSystem.getSoundInfo(
                dialogueTableKey,
                out dialogueSoundInfo
            );
            if (keyResult != FMOD.RESULT.OK)
            {
                Debug.LogError($"Couldn't find dialogue with key: {dialogueTableKey}");
                await Awaitable.NextFrameAsync(token);
                return eventInstance;
            }

            // Load Sound
            FMOD.Sound dialogueSound;
            FMOD.MODE soundMode =
                FMOD.MODE.LOOP_NORMAL | FMOD.MODE.CREATECOMPRESSEDSAMPLE | FMOD.MODE.NONBLOCKING;

            FMOD.RESULT soundResult = RuntimeManager.CoreSystem.createSound(
                dialogueSoundInfo.name_or_data,
                soundMode | dialogueSoundInfo.mode,
                ref dialogueSoundInfo.exinfo,
                out dialogueSound
            );
            if (soundResult != FMOD.RESULT.OK)
            {
                Debug.LogError("Couldn't load sound: " + dialogueTableKey);
                return eventInstance;
            }

            // Wait to Load
            FMOD.OPENSTATE openstate;
            uint percentbuffered;
            bool starving;
            bool diskbusy;
            dialogueSound.getOpenState(
                out openstate,
                out percentbuffered,
                out starving,
                out diskbusy
            );
            float start = Time.unscaledTime;
            bool warningTriggered = false;
            while (openstate != FMOD.OPENSTATE.READY)
            {
                await Awaitable.NextFrameAsync(token);
                dialogueSound.getOpenState(
                    out openstate,
                    out percentbuffered,
                    out starving,
                    out diskbusy
                );
                if (!warningTriggered && Time.unscaledTime - start > 2) // Warning if it takes longer than 2 seconds
                {
                    Debug.LogWarning($"Loading {dialogueTableKey} is taking a long time...");
                    warningTriggered = true;
                }
            }

            // Create Instance
            eventInstance = CreateEventInstance(dialogueEvent);

            // Store Loaded Sound Data
            FMODUserData userData = eventInstance.GetUserData();
            userData.FmodSound = dialogueSound;
            userData.FmodSoundInfo = dialogueSoundInfo;

            // Return the instance
            return eventInstance;
        }

        EventInstance CreateEventInstance(FMODEventRef eventRef)
        {
            EventInstance eventInstance = RuntimeManager.CreateInstance(eventRef.Guid);
            FMODUserData userData = TryGetUserData(eventRef);
            eventInstance.setUserData(GCHandle.ToIntPtr(GCHandle.Alloc(userData)));
            eventInstance.setCallback(
                FMODNativeCallback.Instance,
                EVENT_CALLBACK_TYPE.STOPPED
                    | EVENT_CALLBACK_TYPE.CREATE_PROGRAMMER_SOUND
                    | EVENT_CALLBACK_TYPE.DESTROY_PROGRAMMER_SOUND
                    | EVENT_CALLBACK_TYPE.DESTROYED
                    | EVENT_CALLBACK_TYPE.TIMELINE_MARKER
            );
            return eventInstance;
        }

        FMODUserData TryGetUserData(FMODEventRef eventRef)
        {
            // Make sure we have FMODUserData
            if (_inactiveInstances.Count == 0)
                AddNewInactiveInstance();

            // Get and/or Increment Count
            EventInstancePlayCountIncrement(eventRef);

            // Return FMODUserData
            FMODUserData userData = _inactiveInstances.Pop();
            userData.EventRef = eventRef;
            return userData;
        }

        void EventInstancePlayCountIncrement(FMODEventRef eventRef)
        {
            // Try to increment
            EventInstanceData eventInstanceData;
            for (int i = 0; i < _activeInstances.Count; i++)
            {
                eventInstanceData = _activeInstances[i];
                if (eventInstanceData.EventRef == eventRef)
                {
                    eventInstanceData.PlayCount += 1;
                    return;
                }
            }

            // Failed to increment, create new
            eventInstanceData = _eventInstanceDataPool.Get();
            eventInstanceData.EventRef = eventRef;
            eventInstanceData.PlayCount = 1;
            _activeInstances.Add(eventInstanceData);
        }

        void EventInstancePlayCountDecrement(FMODEventRef eventRef)
        {
            // Try to decrement
            EventInstanceData eventInstanceData;
            for (int i = 0; i < _activeInstances.Count; i++)
            {
                eventInstanceData = _activeInstances[i];
                if (eventInstanceData.EventRef == eventRef)
                {
                    eventInstanceData.PlayCount -= 1;
                    if (eventInstanceData.PlayCount == 0)
                    {
                        _activeInstances.RemoveAt(i);
                        _eventInstanceDataPool.Release(eventInstanceData);
                    }
                    return;
                }
            }
            throw new Exception(
                $"Tried to decrement play count on an event that's not playing: {eventRef.Path}"
            );
        }
        #endregion

        #region Public Parameter API
        public void SetGlobalParameter(string name, float value) => SetParameter(name, value);

        void SetParameter(string name, float value) =>
            RuntimeManager.StudioSystem.setParameterByName(name, value);
        #endregion

        #region Private API
        void ReleaseUserData(FMODUserData userData)
        {
            EventInstancePlayCountDecrement(userData.EventRef);
            userData.Clear();
            _inactiveInstances.Push(userData);
        }

        void AddNewInactiveInstance()
        {
            _inactiveInstances.Push(
                new FMODUserData(_releaseUserDataAction, destroyCancellationToken)
            );
        }
        #endregion

        #region Helper Constructs
        class EventInstanceData
        {
            public int PlayCount;
            public FMODEventRef EventRef;
        }
        #endregion
    }
}
