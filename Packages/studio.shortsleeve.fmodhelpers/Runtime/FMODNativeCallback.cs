using System;
using System.Runtime.InteropServices;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FMODHelpers
{
    public static class FMODNativeCallback
    {
        public static readonly FMOD.Studio.EVENT_CALLBACK Instance = new FMOD.Studio.EVENT_CALLBACK(FMODUnmanagedCallback);

        [AOT.MonoPInvokeCallback(typeof(FMOD.Studio.EVENT_CALLBACK))]
        static FMOD.RESULT FMODUnmanagedCallback(FMOD.Studio.EVENT_CALLBACK_TYPE type, IntPtr instancePtr, IntPtr parameterPtr)
        {
            // Get Instance
            FMOD.Studio.EventInstance instance = new FMOD.Studio.EventInstance(instancePtr);
            // Retrieve the user data
            IntPtr userDataPtr;
            FMOD.RESULT result = instance.getUserData(out userDataPtr);
            if (result != FMOD.RESULT.OK)
            {
                Debug.LogError("Failed to fetch user data for audio callback: " + result);
            }
            else if (userDataPtr != IntPtr.Zero)
            {
                // Grab Parameters
                GCHandle userDataHandle = GCHandle.FromIntPtr(userDataPtr);
                FMODUserData userData = (FMODUserData)userDataHandle.Target;

                // Update User Data Event
                FMODCallback callback;
                callback.UserData = userData;
                callback.CallbackData.EventType = type;
                callback.CallbackData.MarkerName = null;

                // Handle Default Actions
                switch (type)
                {
                    case FMOD.Studio.EVENT_CALLBACK_TYPE.CREATE_PROGRAMMER_SOUND:
                        FMOD.Studio.PROGRAMMER_SOUND_PROPERTIES programmerSoundProperties = (FMOD.Studio.PROGRAMMER_SOUND_PROPERTIES)
                            Marshal.PtrToStructure(parameterPtr, typeof(FMOD.Studio.PROGRAMMER_SOUND_PROPERTIES));
                        programmerSoundProperties.sound = userData.FmodSound.handle;
                        programmerSoundProperties.subsoundIndex = userData.FmodSoundInfo.subsoundindex;
                        Marshal.StructureToPtr(programmerSoundProperties, parameterPtr, false);
                        break;
                    case FMOD.Studio.EVENT_CALLBACK_TYPE.DESTROY_PROGRAMMER_SOUND:
                        FMOD.Studio.PROGRAMMER_SOUND_PROPERTIES parameter = (FMOD.Studio.PROGRAMMER_SOUND_PROPERTIES)
                            Marshal.PtrToStructure(parameterPtr, typeof(FMOD.Studio.PROGRAMMER_SOUND_PROPERTIES));
                        FMOD.Sound sound = new FMOD.Sound(parameter.sound);
                        sound.release();
                        break;
                    case FMOD.Studio.EVENT_CALLBACK_TYPE.TIMELINE_MARKER:
                        FMOD.Studio.TIMELINE_MARKER_PROPERTIES markerParameter = (FMOD.Studio.TIMELINE_MARKER_PROPERTIES)
                            Marshal.PtrToStructure(parameterPtr, typeof(FMOD.Studio.TIMELINE_MARKER_PROPERTIES));
                        callback.CallbackData.MarkerName = markerParameter.name;
                        HandleCallback(callback, userData.CallbackCancellationToken).Forget();
                        break;
                    case FMOD.Studio.EVENT_CALLBACK_TYPE.STOPPED:
                        HandleCallback(callback, userData.CallbackCancellationToken).Forget();
                        break;
                    case FMOD.Studio.EVENT_CALLBACK_TYPE.DESTROYED:
                        userDataHandle.Free();
                        HandleCallback(callback, userData.CallbackCancellationToken).Forget();
                        break;
                }
            }
            return FMOD.RESULT.OK;
        }

        static async UniTaskVoid HandleCallback(FMODCallback callback, CancellationToken cancellationToken)
        {
            await UniTask.SwitchToMainThread(cancellationToken);
            switch (callback.CallbackData.EventType)
            {
                case FMOD.Studio.EVENT_CALLBACK_TYPE.TIMELINE_MARKER:
                    callback.UserData.UserCallbackHandler?.Invoke(callback.CallbackData);
                    break;
                case FMOD.Studio.EVENT_CALLBACK_TYPE.STOPPED:
                    callback.UserData.UserCallbackHandler?.Invoke(callback.CallbackData);
                    break;
                case FMOD.Studio.EVENT_CALLBACK_TYPE.DESTROYED:
                    callback.UserData.UserCallbackHandler?.Invoke(callback.CallbackData);
                    callback.UserData.Release();
                    break;
            }
        }
    }
}
