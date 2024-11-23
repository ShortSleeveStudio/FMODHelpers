using System;
using System.Runtime.InteropServices;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;

namespace FMODHelpers
{
    public static class FMODEventInstanceExtensions
    {
        public static void Release(this EventInstance eventInstance)
        {
            FMOD.RESULT result = eventInstance.release();
            if (result != FMOD.RESULT.OK)
                Debug.LogError($"Couldn't release event: {result}");
        }

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

        public static void Start(this EventInstance eventInstance)
        {
            FMOD.RESULT result = eventInstance.start();
            if (result != FMOD.RESULT.OK)
                Debug.LogError($"Couldn't start event: {result}");
        }

        public static void Stop(this EventInstance eventInstance, bool immediate)
        {
            if (!eventInstance.isValid())
                return;
            FMOD.RESULT result = eventInstance.stop(immediate ? FMOD.Studio.STOP_MODE.IMMEDIATE : FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            if (result != FMOD.RESULT.OK)
                Debug.LogError($"Couldn't stop event: {result}");
        }

        public static void SetParameter(this EventInstance eventInstance, string parameter, float value, bool skipSeek = false)
        {
            if (!eventInstance.isValid())
            {
                Debug.LogError("Can't set parameter on destroyed sound instance");
                return;
            }
            FMOD.RESULT result = eventInstance.setParameterByName(parameter, value, skipSeek);
            if (result != FMOD.RESULT.OK)
            {
                Debug.LogError($"Failed to set parameter {parameter} with value {value}: {result}.");
            }
        }

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

        public static void OnStateChange(this EventInstance eventInstance, Action<FMODCallbackData> callback)
        {
            FMODUserData userData = eventInstance.GetUserData();
            userData.UserCallbackHandler = callback;
        }

        public static FMODEventRef GetEventRef(this EventInstance eventInstance)
        {
            FMODUserData userData = eventInstance.GetUserData();
            return userData.EventRef;
        }

        public static void AttachToGameObject(this EventInstance eventInstance, GameObject gameObject)
        {
            if (!eventInstance.isValid())
            {
                Debug.LogError("Can't attach object to a destroyed sound instance");
                return;
            }
            RuntimeManager.AttachInstanceToGameObject(eventInstance, gameObject.transform, gameObject.GetComponent<Rigidbody2D>());
        }

        public static void DetachFromGameObject(this EventInstance eventInstance)
        {
            if (!eventInstance.isValid())
            {
                Debug.LogError("Can't detach a destroyed sound instance");
                return;
            }
            RuntimeManager.DetachInstanceFromGameObject(eventInstance);
        }

        // Pass by ref so we can clear the handle
        public static void Destroy(this ref EventInstance eventInstance, bool immediate)
        {
            if (!eventInstance.isValid())
                return;
            FMOD.RESULT result = eventInstance.release();
            if (result != FMOD.RESULT.OK)
            {
                Debug.LogError($"Failed to release event: {result}");
                return;
            }
            eventInstance.Stop(immediate);
            eventInstance.clearHandle();
        }

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
