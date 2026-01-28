using System;
using System.Runtime.InteropServices;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;

namespace FMODHelpers
{
    public static class FMODEventInstanceExtensions
    {
        /// <summary>
        /// Marks an event instance for release. If the instance was created via FMODManager.GetEventInstance(),
        /// it will be automatically cleaned up when the DESTROYED callback fires.
        /// This is the recommended way to release event instances.
        /// </summary>
        /// <param name="eventInstance">The event instance to release</param>
        public static void Release(this EventInstance eventInstance)
        {
            FMOD.RESULT result = eventInstance.release();
            if (result != FMOD.RESULT.OK)
                Debug.LogError($"Couldn't release event: {result}");
        }

        /// <summary>
        /// Pauses or unpauses an event instance.
        /// </summary>
        /// <param name="eventInstance">The event instance to pause/unpause</param>
        /// <param name="pause">True to pause, false to unpause</param>
        public static void Pause(this EventInstance eventInstance, bool pause)
        {
            if (!eventInstance.isValid())
            {
                Debug.LogError("Can't pause a destroyed sound instance");
                return;
            }
            FMOD.RESULT result = eventInstance.setPaused(pause);
            if (result != FMOD.RESULT.OK)
            {
                Debug.LogError($"Failed to pause event: {result}");
            }
        }

        /// <summary>
        /// Starts playback of an event instance.
        /// </summary>
        /// <param name="eventInstance">The event instance to start</param>
        public static void Start(this EventInstance eventInstance)
        {
            FMOD.RESULT result = eventInstance.start();
            if (result != FMOD.RESULT.OK)
                Debug.LogError($"Couldn't start event: {result}");
        }

        /// <summary>
        /// Stops playback of an event instance.
        /// </summary>
        /// <param name="eventInstance">The event instance to stop</param>
        /// <param name="immediate">If true, stops immediately. If false, allows fadeout to complete.</param>
        public static void Stop(this EventInstance eventInstance, bool immediate)
        {
            if (!eventInstance.isValid())
                return;
            FMOD.RESULT result = eventInstance.stop(
                immediate ? FMOD.Studio.STOP_MODE.IMMEDIATE : FMOD.Studio.STOP_MODE.ALLOWFADEOUT
            );
            if (result != FMOD.RESULT.OK)
                Debug.LogError($"Couldn't stop event: {result}");
        }

        /// <summary>
        /// Sets a local parameter value on an event instance.
        /// </summary>
        /// <param name="eventInstance">The event instance to modify</param>
        /// <param name="parameter">Parameter name as defined in FMOD Studio</param>
        /// <param name="value">Parameter value to set</param>
        /// <param name="skipSeek">If true, sets instantly without seeking. If false, seeks the timeline to the new parameter value.</param>
        public static void SetParameter(
            this EventInstance eventInstance,
            string parameter,
            float value,
            bool skipSeek = false
        )
        {
            if (!eventInstance.isValid())
            {
                Debug.LogError("Can't set parameter on destroyed sound instance");
                return;
            }
            FMOD.RESULT result = eventInstance.setParameterByName(parameter, value, skipSeek);
            if (result != FMOD.RESULT.OK)
            {
                Debug.LogError(
                    $"Failed to set parameter {parameter} with value {value}: {result}."
                );
            }
        }

        /// <summary>
        /// Gets the current value of a local parameter on an event instance.
        /// </summary>
        /// <param name="eventInstance">The event instance to query</param>
        /// <param name="parameter">Parameter name as defined in FMOD Studio</param>
        /// <returns>Current parameter value, or 0 if query failed</returns>
        public static float GetParameter(this EventInstance eventInstance, string parameter)
        {
            if (!eventInstance.isValid())
            {
                Debug.LogError("Can't get parameter on destroyed sound instance");
                return 0f;
            }
            float value;
            FMOD.RESULT result = eventInstance.getParameterByName(parameter, out value);
            if (result != FMOD.RESULT.OK)
            {
                Debug.LogError($"Failed to get parameter {parameter}: {result}.");
            }
            return value;
        }

        /// <summary>
        /// Gets the FMODUserData associated with this event instance.
        /// Only works for instances created via FMODManager.GetEventInstance().
        /// </summary>
        /// <param name="eventInstance">The event instance to query</param>
        /// <returns>Associated FMODUserData, or null if not found</returns>
        public static FMODUserData GetUserData(this EventInstance eventInstance)
        {
            IntPtr userDataPtr;
            FMOD.RESULT result = eventInstance.getUserData(out userDataPtr);
            if (result != FMOD.RESULT.OK)
            {
                Debug.LogError($"Failed to load user data from FMOD event instance: {result}");
                return null;
            }
            GCHandle userDataHandle = GCHandle.FromIntPtr(userDataPtr);
            return (FMODUserData)userDataHandle.Target;
        }

        /// <summary>
        /// Registers a callback handler for this event instance.
        /// The handler will receive all subscribed callbacks until the DESTROYED callback fires.
        /// </summary>
        /// <param name="eventInstance">The event instance to register callbacks for</param>
        /// <param name="handler">Object implementing IFMODStudioEventHandler to receive callbacks</param>
        public static void RegisterCallbackHandler(
            this EventInstance eventInstance,
            IFMODStudioEventHandler handler
        )
        {
            FMODUserData userData = GetUserData(eventInstance);
            if (userData == null)
                return;
            userData.StudioEventCallbackHandler = handler;
        }

        /// <summary>
        /// Gets the EventReference that was used to create this instance.
        /// Only works for instances created via FMODManager.GetEventInstance().
        /// </summary>
        /// <param name="eventInstance">The event instance to query</param>
        /// <returns>The original EventReference, or default if not found</returns>
        public static EventReference GetEventRef(this EventInstance eventInstance)
        {
            FMODUserData userData = eventInstance.GetUserData();
            if (userData == null)
                return new();
            return userData.EventRef;
        }

        /// <summary>
        /// Stores a SoundCreateResult for use in programmer sound callbacks.
        /// Must be called BEFORE starting an instance that has CREATE_PROGRAMMER_SOUND callback.
        /// The stored sound will be automatically provided to the OnCreateProgrammerSound callback.
        /// </summary>
        /// <param name="eventInstance">The event instance to attach the sound to</param>
        /// <param name="result">The sound data from FMODShortcuts.CreateSound* methods</param>
        public static void SetProgrammerSound(
            this EventInstance eventInstance,
            SoundCreateResult result
        )
        {
            FMODUserData userData = eventInstance.GetUserData();
            if (userData == null)
            {
                Debug.LogError(
                    "Cannot set programmer sound on instance without FMODUserData. "
                        + "Did you create this instance via FMODManager.GetEventInstance()?"
                );
                return;
            }
            userData.ProgrammerSoundResult = result;
        }

        /// <summary>
        /// Gets the SoundCreateResult previously set via SetProgrammerSound().
        /// </summary>
        /// <param name="eventInstance">The event instance to query</param>
        /// <returns>The stored SoundCreateResult, or null if none was set</returns>
        public static SoundCreateResult? GetProgrammerSound(this EventInstance eventInstance)
        {
            FMODUserData userData = eventInstance.GetUserData();
            return userData?.ProgrammerSoundResult;
        }

        /// <summary>
        /// Attaches an event instance to a GameObject, making it follow the GameObject's position.
        /// </summary>
        /// <param name="eventInstance">The event instance to attach</param>
        /// <param name="gameObject">The GameObject to attach to</param>
        public static void AttachToGameObject(
            this EventInstance eventInstance,
            GameObject gameObject
        )
        {
            if (!eventInstance.isValid())
            {
                Debug.LogError("Can't attach object to a destroyed sound instance");
                return;
            }
            RuntimeManager.AttachInstanceToGameObject(
                eventInstance,
                gameObject.transform,
                gameObject.GetComponent<Rigidbody2D>()
            );
        }

        /// <summary>
        /// Detaches an event instance from its GameObject, stopping position tracking.
        /// </summary>
        /// <param name="eventInstance">The event instance to detach</param>
        public static void DetachFromGameObject(this EventInstance eventInstance)
        {
            if (!eventInstance.isValid())
            {
                Debug.LogError("Can't detach a destroyed sound instance");
                return;
            }
            RuntimeManager.DetachInstanceFromGameObject(eventInstance);
        }

        /// <summary>
        /// Gets the current playback position in seconds.
        /// </summary>
        /// <param name="eventInstance">The event instance to query</param>
        /// <returns>Current timeline position in seconds, or 0 if query failed</returns>
        public static float GetPositionSeconds(this EventInstance eventInstance)
        {
            if (!eventInstance.isValid())
            {
                Debug.LogError("Can't get position for a destroyed sound instance");
                return 0f;
            }
            int timeMs;
            FMOD.RESULT result = eventInstance.getTimelinePosition(out timeMs);
            if (result != FMOD.RESULT.OK)
            {
                // This happens sometimes because of threading, the handle isn't invalidated quickly enough
                return 0f;
            }
            return timeMs / 1000f;
        }
    }
}
