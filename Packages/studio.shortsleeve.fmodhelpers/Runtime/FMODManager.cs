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

        #region Static
        static Thread MainThread;
        public static bool IsMainThread => Thread.CurrentThread == MainThread;
        #endregion

        #region Inspector
        [SerializeField]
        int initialInstancePoolSize = 32;
        #endregion

        #region State
        Bus _masterBus;
        Func<bool> _haveBanksLoaded;
        List<FMODBankRef> _banksPendingUnload;
        Stack<FMODUserData> _inactiveUserData;
        HashSet<FMODUserData> _activeUserData;
        Action<FMODUserData> _releaseUserDataAction;
        List<EventInstanceData> _activeInstances; // maybe should be something other than a list
        ObjectPool<EventInstanceData> _eventInstanceDataPool;
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

        void OnDestroy()
        {
            Debug.Log($"DESTROYING {_inactiveUserData.Count + _activeInstances.Count} USER DATA");
            foreach (FMODUserData data in _inactiveUserData)
            {
                data.Handle.Free();
            }
            foreach (FMODUserData data in _activeUserData)
            {
                data.Handle.Free();
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
            EventInstance eventInstance = GetEventInstance(eventRef);
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
        public EventInstance GetEventInstance(
            FMODEventRef eventRef,
            EVENT_CALLBACK_TYPE callbacks = EVENT_CALLBACK_TYPE.DESTROYED
        )
        {
            EventInstance eventInstance = RuntimeManager.CreateInstance(eventRef.Guid);
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

        FMODUserData TryGetUserData(FMODEventRef eventRef)
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
            _activeUserData.Remove(userData);
            _inactiveUserData.Push(userData);
        }

        void AddNewInactiveInstance()
        {
            _inactiveUserData.Push(
                FMODUserData.Create(_releaseUserDataAction, destroyCancellationToken)
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
