using FMOD.Studio;

namespace FMODHelpers
{
    public interface IFMODStudioEventHandler
    {
        void OnCreated(bool isMainThread, FMODUserData userData) { }
        void OnDestroyed(bool isMainThread, FMODUserData userData) { }
        void OnStarting(bool isMainThread, FMODUserData userData) { }
        void OnStarted(bool isMainThread, FMODUserData userData) { }
        void OnRestarted(bool isMainThread, FMODUserData userData) { }
        void OnStopped(bool isMainThread, FMODUserData userData) { }
        void OnStartFailed(bool isMainThread, FMODUserData userData) { }
        void OnCreateProgrammerSound(
            bool isMainThread,
            FMODUserData userData,
            ref PROGRAMMER_SOUND_PROPERTIES programmerSoundProperties
        ) { }
        void OnDestroyProgrammerSound(
            bool isMainThread,
            FMODUserData userData,
            ref PROGRAMMER_SOUND_PROPERTIES programmerSoundProperties
        ) { }
        void OnPluginCreated(
            bool isMainThread,
            FMODUserData userData,
            ref PLUGIN_INSTANCE_PROPERTIES pluginInstanceProperties
        ) { }
        void OnPluginDestroyed(
            bool isMainThread,
            FMODUserData userData,
            ref PLUGIN_INSTANCE_PROPERTIES pluginInstanceProperties
        ) { }
        void OnTimelineMarker(
            bool isMainThread,
            FMODUserData userData,
            ref TIMELINE_MARKER_PROPERTIES markerProperties
        ) { }
        void OnTimelineBeat(
            bool isMainThread,
            FMODUserData userData,
            ref TIMELINE_BEAT_PROPERTIES beatProperties
        ) { }
        void OnSoundPlayed(bool isMainThread, FMODUserData userData, ref FMOD.Sound sound) { }
        void OnSoundStopped(bool isMainThread, FMODUserData userData, ref FMOD.Sound sound) { }
        void OnRealToVirtual(bool isMainThread, FMODUserData userData) { }
        void OnVirtualToReal(bool isMainThread, FMODUserData userData) { }
        void OnStartEventCommand(
            bool isMainThread,
            FMODUserData userData,
            ref EventInstance instance
        ) { }
        void OnNestedTimelineBeat(
            bool isMainThread,
            FMODUserData userData,
            ref TIMELINE_NESTED_BEAT_PROPERTIES beatProperties
        ) { }
    }
}
