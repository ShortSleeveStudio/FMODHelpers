using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace FMODHelpers
{
    public static class FMODNativeCallbackStudioEvent
    {
        public static readonly FMOD.Studio.EVENT_CALLBACK StudioEventCallbackInstance =
            new FMOD.Studio.EVENT_CALLBACK(StudioEventCallback);

        #region Unmanaged Callbacks
        [AOT.MonoPInvokeCallback(typeof(FMOD.Studio.EVENT_CALLBACK))]
        static FMOD.RESULT StudioEventCallback(
            FMOD.Studio.EVENT_CALLBACK_TYPE type,
            IntPtr instancePtr,
            IntPtr parameterPtr
        )
        {
            // Get Instance
            FMOD.Studio.EventInstance instance = new FMOD.Studio.EventInstance(instancePtr);

            // Retrieve the user data
            IntPtr userDataPtr;
            FMOD.RESULT result = instance.getUserData(out userDataPtr);
            if (result != FMOD.RESULT.OK)
            {
                Debug.LogError("Failed to fetch user data for audio callback: " + result);
                instance.Destroy(immediate: true);
                return FMOD.RESULT.OK;
            }
            if (userDataPtr == IntPtr.Zero)
            {
                return FMOD.RESULT.OK;
            }
            GCHandle userDataHandle = GCHandle.FromIntPtr(userDataPtr);
            FMODUserData userData = (FMODUserData)userDataHandle.Target;

            // Handle Default Actions
            switch (type)
            {
                case FMOD.Studio.EVENT_CALLBACK_TYPE.CREATED:
                    try
                    {
                        userData?.StudioEventCallbackHandler.OnCreated(
                            FMODManager.IsMainThread,
                            userData
                        );
                    }
                    catch { }
                    break;
                case FMOD.Studio.EVENT_CALLBACK_TYPE.DESTROYED:
                    try
                    {
                        userData?.StudioEventCallbackHandler.OnDestroyed(
                            FMODManager.IsMainThread,
                            userData
                        );
                    }
                    catch { }
                    ReleaseUserData(userData);
                    break;
                case FMOD.Studio.EVENT_CALLBACK_TYPE.STARTING:
                    try
                    {
                        userData?.StudioEventCallbackHandler.OnStarting(
                            FMODManager.IsMainThread,
                            userData
                        );
                    }
                    catch { }
                    break;
                case FMOD.Studio.EVENT_CALLBACK_TYPE.STARTED:
                    try
                    {
                        userData?.StudioEventCallbackHandler.OnStarted(
                            FMODManager.IsMainThread,
                            userData
                        );
                    }
                    catch { }
                    break;
                case FMOD.Studio.EVENT_CALLBACK_TYPE.RESTARTED:
                    try
                    {
                        userData?.StudioEventCallbackHandler.OnRestarted(
                            FMODManager.IsMainThread,
                            userData
                        );
                    }
                    catch { }
                    break;
                case FMOD.Studio.EVENT_CALLBACK_TYPE.STOPPED:
                    try
                    {
                        userData?.StudioEventCallbackHandler.OnStopped(
                            FMODManager.IsMainThread,
                            userData
                        );
                    }
                    catch { }
                    break;
                case FMOD.Studio.EVENT_CALLBACK_TYPE.START_FAILED:
                    try
                    {
                        userData?.StudioEventCallbackHandler.OnStartFailed(
                            FMODManager.IsMainThread,
                            userData
                        );
                    }
                    catch { }
                    break;
                case FMOD.Studio.EVENT_CALLBACK_TYPE.CREATE_PROGRAMMER_SOUND:
                    FMOD.Studio.PROGRAMMER_SOUND_PROPERTIES createProperties =
                        (FMOD.Studio.PROGRAMMER_SOUND_PROPERTIES)
                            Marshal.PtrToStructure(
                                parameterPtr,
                                typeof(FMOD.Studio.PROGRAMMER_SOUND_PROPERTIES)
                            );
                    try
                    {
                        userData?.StudioEventCallbackHandler.OnCreateProgrammerSound(
                            FMODManager.IsMainThread,
                            userData,
                            ref createProperties
                        );
                    }
                    catch { }
                    Marshal.StructureToPtr(createProperties, parameterPtr, false);
                    break;
                case FMOD.Studio.EVENT_CALLBACK_TYPE.DESTROY_PROGRAMMER_SOUND:
                    FMOD.Studio.PROGRAMMER_SOUND_PROPERTIES destroyProperties =
                        (FMOD.Studio.PROGRAMMER_SOUND_PROPERTIES)
                            Marshal.PtrToStructure(
                                parameterPtr,
                                typeof(FMOD.Studio.PROGRAMMER_SOUND_PROPERTIES)
                            );
                    try
                    {
                        userData?.StudioEventCallbackHandler.OnDestroyProgrammerSound(
                            FMODManager.IsMainThread,
                            userData,
                            ref destroyProperties
                        );
                    }
                    catch { }
                    new FMOD.Sound(destroyProperties.sound).release();
                    break;
                case FMOD.Studio.EVENT_CALLBACK_TYPE.PLUGIN_CREATED:
                    FMOD.Studio.PLUGIN_INSTANCE_PROPERTIES pluginCreateProperties =
                        (FMOD.Studio.PLUGIN_INSTANCE_PROPERTIES)
                            Marshal.PtrToStructure(
                                parameterPtr,
                                typeof(FMOD.Studio.PLUGIN_INSTANCE_PROPERTIES)
                            );
                    try
                    {
                        userData?.StudioEventCallbackHandler.OnPluginCreated(
                            FMODManager.IsMainThread,
                            userData,
                            ref pluginCreateProperties
                        );
                    }
                    catch { }
                    break;
                case FMOD.Studio.EVENT_CALLBACK_TYPE.PLUGIN_DESTROYED:
                    FMOD.Studio.PLUGIN_INSTANCE_PROPERTIES pluginDestroyedProperties =
                        (FMOD.Studio.PLUGIN_INSTANCE_PROPERTIES)
                            Marshal.PtrToStructure(
                                parameterPtr,
                                typeof(FMOD.Studio.PLUGIN_INSTANCE_PROPERTIES)
                            );
                    try
                    {
                        userData?.StudioEventCallbackHandler.OnPluginDestroyed(
                            FMODManager.IsMainThread,
                            userData,
                            ref pluginDestroyedProperties
                        );
                    }
                    catch { }
                    break;
                case FMOD.Studio.EVENT_CALLBACK_TYPE.TIMELINE_MARKER:
                    FMOD.Studio.TIMELINE_MARKER_PROPERTIES markerParameter =
                        (FMOD.Studio.TIMELINE_MARKER_PROPERTIES)
                            Marshal.PtrToStructure(
                                parameterPtr,
                                typeof(FMOD.Studio.TIMELINE_MARKER_PROPERTIES)
                            );
                    try
                    {
                        userData?.StudioEventCallbackHandler.OnTimelineMarker(
                            FMODManager.IsMainThread,
                            userData,
                            ref markerParameter
                        );
                    }
                    catch { }
                    break;
                case FMOD.Studio.EVENT_CALLBACK_TYPE.TIMELINE_BEAT:
                    FMOD.Studio.TIMELINE_BEAT_PROPERTIES beatProperties =
                        (FMOD.Studio.TIMELINE_BEAT_PROPERTIES)
                            Marshal.PtrToStructure(
                                parameterPtr,
                                typeof(FMOD.Studio.TIMELINE_BEAT_PROPERTIES)
                            );
                    try
                    {
                        userData?.StudioEventCallbackHandler.OnTimelineBeat(
                            FMODManager.IsMainThread,
                            userData,
                            ref beatProperties
                        );
                    }
                    catch { }
                    break;
                case FMOD.Studio.EVENT_CALLBACK_TYPE.SOUND_PLAYED:
                    FMOD.Sound soundPlayed = (FMOD.Sound)
                        Marshal.PtrToStructure(parameterPtr, typeof(FMOD.Sound));
                    try
                    {
                        userData?.StudioEventCallbackHandler.OnSoundPlayed(
                            FMODManager.IsMainThread,
                            userData,
                            ref soundPlayed
                        );
                    }
                    catch { }
                    break;
                case FMOD.Studio.EVENT_CALLBACK_TYPE.SOUND_STOPPED:
                    FMOD.Sound soundStopped = (FMOD.Sound)
                        Marshal.PtrToStructure(parameterPtr, typeof(FMOD.Sound));
                    try
                    {
                        userData?.StudioEventCallbackHandler.OnSoundPlayed(
                            FMODManager.IsMainThread,
                            userData,
                            ref soundStopped
                        );
                    }
                    catch { }
                    break;
                case FMOD.Studio.EVENT_CALLBACK_TYPE.REAL_TO_VIRTUAL:
                    try
                    {
                        userData?.StudioEventCallbackHandler.OnRealToVirtual(
                            FMODManager.IsMainThread,
                            userData
                        );
                    }
                    catch { }
                    break;
                case FMOD.Studio.EVENT_CALLBACK_TYPE.VIRTUAL_TO_REAL:
                    try
                    {
                        userData?.StudioEventCallbackHandler.OnVirtualToReal(
                            FMODManager.IsMainThread,
                            userData
                        );
                    }
                    catch { }
                    break;
                case FMOD.Studio.EVENT_CALLBACK_TYPE.START_EVENT_COMMAND:
                    FMOD.Studio.EventInstance startEvent = (FMOD.Studio.EventInstance)
                        Marshal.PtrToStructure(parameterPtr, typeof(FMOD.Studio.EventInstance));
                    try
                    {
                        userData?.StudioEventCallbackHandler.OnStartEventCommand(
                            FMODManager.IsMainThread,
                            userData,
                            ref startEvent
                        );
                    }
                    catch { }
                    break;
                case FMOD.Studio.EVENT_CALLBACK_TYPE.NESTED_TIMELINE_BEAT:
                    FMOD.Studio.TIMELINE_NESTED_BEAT_PROPERTIES nestedProperties =
                        (FMOD.Studio.TIMELINE_NESTED_BEAT_PROPERTIES)
                            Marshal.PtrToStructure(
                                parameterPtr,
                                typeof(FMOD.Studio.TIMELINE_NESTED_BEAT_PROPERTIES)
                            );
                    try
                    {
                        userData?.StudioEventCallbackHandler.OnNestedTimelineBeat(
                            FMODManager.IsMainThread,
                            userData,
                            ref nestedProperties
                        );
                    }
                    catch { }
                    break;
            }
            return FMOD.RESULT.OK;
        }
        #endregion

        #region Callbacks
        static async void ReleaseUserData(FMODUserData data)
        {
            await Awaitable.MainThreadAsync();
            data.Cancellation.ThrowIfCancellationRequested();
            data.Release();
        }
        #endregion
    }
}
