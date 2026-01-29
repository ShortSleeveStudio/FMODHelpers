using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using FMOD.Studio;
using UnityEngine;

namespace FMODHelpers
{
    /// <summary>
    /// Thread-safe container for callback data captured from FMOD thread.
    /// </summary>
    public struct PendingCallback
    {
        public EVENT_CALLBACK_TYPE Type;
        public FMODUserData UserData;

        // Timeline data (captured from native memory before it becomes invalid)
        public TimelineMarkerData MarkerData;
        public TimelineBeatData BeatData;
        public NestedTimelineBeatData NestedBeatData;

        // Sound/Instance handles (these remain valid across threads)
        public FMOD.Sound Sound;
        public EventInstance EventInstance;
    }

    public static class FMODNativeCallbackStudioEvent
    {
        public static readonly EVENT_CALLBACK StudioEventCallbackInstance =
            new EVENT_CALLBACK(StudioEventCallback);

        static readonly ConcurrentQueue<PendingCallback> _callbackQueue = new();

        #region Native Callback
        [AOT.MonoPInvokeCallback(typeof(EVENT_CALLBACK))]
        static FMOD.RESULT StudioEventCallback(
            EVENT_CALLBACK_TYPE type,
            IntPtr instancePtr,
            IntPtr parameterPtr
        )
        {
            // Resolve UserData
            EventInstance instance = new EventInstance(instancePtr);
            IntPtr userDataPtr;
            if (instance.getUserData(out userDataPtr) != FMOD.RESULT.OK || userDataPtr == IntPtr.Zero)
                return FMOD.RESULT.OK;

            GCHandle userDataHandle = GCHandle.FromIntPtr(userDataPtr);
            FMODUserData userData = (FMODUserData)userDataHandle.Target;

            // Short-circuit if shutting down
            if (userData == null || FMODManager.IsShuttingDown || userData.Cancellation.IsCancellationRequested)
                return FMOD.RESULT.OK;

            // Handle synchronous callbacks that must interact with FMOD immediately
            switch (type)
            {
                case EVENT_CALLBACK_TYPE.CREATE_PROGRAMMER_SOUND:
                    var createProps = Marshal.PtrToStructure<PROGRAMMER_SOUND_PROPERTIES>(parameterPtr);
                    try
                    {
                        userData.StudioEventCallbackHandler?.OnCreateProgrammerSound(userData, ref createProps);
                    }
                    catch { }
                    Marshal.StructureToPtr(createProps, parameterPtr, false);
                    return FMOD.RESULT.OK;

                case EVENT_CALLBACK_TYPE.DESTROY_PROGRAMMER_SOUND:
                    var destroyProps = Marshal.PtrToStructure<PROGRAMMER_SOUND_PROPERTIES>(parameterPtr);
                    try
                    {
                        userData.StudioEventCallbackHandler?.OnDestroyProgrammerSound(userData, ref destroyProps);
                    }
                    catch { }
                    new FMOD.Sound(destroyProps.sound).release();
                    return FMOD.RESULT.OK;
            }

            // Capture data and queue for main thread processing
            var pending = new PendingCallback { Type = type, UserData = userData };

            switch (type)
            {
                case EVENT_CALLBACK_TYPE.TIMELINE_MARKER:
                    var markerProps = Marshal.PtrToStructure<TIMELINE_MARKER_PROPERTIES>(parameterPtr);
                    pending.MarkerData = new TimelineMarkerData
                    {
                        Name = markerProps.name,
                        Position = markerProps.position
                    };
                    break;

                case EVENT_CALLBACK_TYPE.TIMELINE_BEAT:
                    var beatProps = Marshal.PtrToStructure<TIMELINE_BEAT_PROPERTIES>(parameterPtr);
                    pending.BeatData = new TimelineBeatData
                    {
                        Bar = beatProps.bar,
                        Beat = beatProps.beat,
                        Position = beatProps.position,
                        Tempo = beatProps.tempo,
                        TimeSignatureUpper = beatProps.timesignatureupper,
                        TimeSignatureLower = beatProps.timesignaturelower
                    };
                    break;

                case EVENT_CALLBACK_TYPE.NESTED_TIMELINE_BEAT:
                    var nestedProps = Marshal.PtrToStructure<TIMELINE_NESTED_BEAT_PROPERTIES>(parameterPtr);
                    pending.NestedBeatData = new NestedTimelineBeatData
                    {
                        Bar = nestedProps.properties.bar,
                        Beat = nestedProps.properties.beat,
                        Position = nestedProps.properties.position,
                        Tempo = nestedProps.properties.tempo,
                        TimeSignatureUpper = nestedProps.properties.timesignatureupper,
                        TimeSignatureLower = nestedProps.properties.timesignaturelower,
                        EventGuid = nestedProps.eventid
                    };
                    break;

                case EVENT_CALLBACK_TYPE.SOUND_PLAYED:
                case EVENT_CALLBACK_TYPE.SOUND_STOPPED:
                    pending.Sound = Marshal.PtrToStructure<FMOD.Sound>(parameterPtr);
                    break;

                case EVENT_CALLBACK_TYPE.START_EVENT_COMMAND:
                    pending.EventInstance = Marshal.PtrToStructure<EventInstance>(parameterPtr);
                    break;
            }

            _callbackQueue.Enqueue(pending);
            return FMOD.RESULT.OK;
        }
        #endregion

        #region Main Thread Processing
        /// <summary>
        /// Processes queued callbacks on the main thread. Called from FMODManager.Update().
        /// </summary>
        internal static void ProcessCallbacks()
        {
            while (_callbackQueue.TryDequeue(out var callback))
            {
                // Skip if userData is invalid or shutting down
                if (callback.UserData == null || callback.UserData.Cancellation.IsCancellationRequested)
                    continue;

                var handler = callback.UserData.StudioEventCallbackHandler;

                try
                {
                    switch (callback.Type)
                    {
                        case EVENT_CALLBACK_TYPE.CREATED:
                            handler?.OnCreated(callback.UserData);
                            break;
                        case EVENT_CALLBACK_TYPE.DESTROYED:
                            handler?.OnDestroyed(callback.UserData);
                            callback.UserData.Release();
                            break;
                        case EVENT_CALLBACK_TYPE.STARTING:
                            handler?.OnStarting(callback.UserData);
                            break;
                        case EVENT_CALLBACK_TYPE.STARTED:
                            handler?.OnStarted(callback.UserData);
                            break;
                        case EVENT_CALLBACK_TYPE.RESTARTED:
                            handler?.OnRestarted(callback.UserData);
                            break;
                        case EVENT_CALLBACK_TYPE.STOPPED:
                            handler?.OnStopped(callback.UserData);
                            break;
                        case EVENT_CALLBACK_TYPE.START_FAILED:
                            handler?.OnStartFailed(callback.UserData);
                            break;
                        case EVENT_CALLBACK_TYPE.TIMELINE_MARKER:
                            handler?.OnTimelineMarker(callback.UserData, callback.MarkerData);
                            break;
                        case EVENT_CALLBACK_TYPE.TIMELINE_BEAT:
                            handler?.OnTimelineBeat(callback.UserData, callback.BeatData);
                            break;
                        case EVENT_CALLBACK_TYPE.NESTED_TIMELINE_BEAT:
                            handler?.OnNestedTimelineBeat(callback.UserData, callback.NestedBeatData);
                            break;
                        case EVENT_CALLBACK_TYPE.SOUND_PLAYED:
                            handler?.OnSoundPlayed(callback.UserData, callback.Sound);
                            break;
                        case EVENT_CALLBACK_TYPE.SOUND_STOPPED:
                            handler?.OnSoundStopped(callback.UserData, callback.Sound);
                            break;
                        case EVENT_CALLBACK_TYPE.REAL_TO_VIRTUAL:
                            handler?.OnRealToVirtual(callback.UserData);
                            break;
                        case EVENT_CALLBACK_TYPE.VIRTUAL_TO_REAL:
                            handler?.OnVirtualToReal(callback.UserData);
                            break;
                        case EVENT_CALLBACK_TYPE.START_EVENT_COMMAND:
                            handler?.OnStartEventCommand(callback.UserData, callback.EventInstance);
                            break;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        /// <summary>
        /// Clears any remaining callbacks during shutdown.
        /// </summary>
        internal static void ClearQueue()
        {
            while (_callbackQueue.TryDequeue(out _)) { }
        }
        #endregion
    }
}
