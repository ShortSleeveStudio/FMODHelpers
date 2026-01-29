using FMOD.Studio;

namespace FMODHelpers
{
    /// <summary>
    /// Captured timeline marker data for main-thread processing.
    /// </summary>
    public struct TimelineMarkerData
    {
        public string Name;
        public int Position;
    }

    /// <summary>
    /// Captured timeline beat data for main-thread processing.
    /// </summary>
    public struct TimelineBeatData
    {
        public int Bar;
        public int Beat;
        public int Position;
        public float Tempo;
        public int TimeSignatureUpper;
        public int TimeSignatureLower;
    }

    /// <summary>
    /// Captured nested timeline beat data for main-thread processing.
    /// </summary>
    public struct NestedTimelineBeatData
    {
        public int Bar;
        public int Beat;
        public int Position;
        public float Tempo;
        public int TimeSignatureUpper;
        public int TimeSignatureLower;
        public FMOD.GUID EventGuid;
    }

    /// <summary>
    /// Interface for receiving FMOD Studio event callbacks.
    /// All methods are called on the main thread unless otherwise noted.
    /// CREATE_PROGRAMMER_SOUND and DESTROY_PROGRAMMER_SOUND are called synchronously
    /// from the FMOD thread and must not block.
    /// </summary>
    public interface IFMODStudioEventHandler
    {
        void OnCreated(FMODUserData userData) { }
        void OnDestroyed(FMODUserData userData) { }
        void OnStarting(FMODUserData userData) { }
        void OnStarted(FMODUserData userData) { }
        void OnRestarted(FMODUserData userData) { }
        void OnStopped(FMODUserData userData) { }
        void OnStartFailed(FMODUserData userData) { }

        /// <summary>
        /// Called synchronously from FMOD thread. Must set programmerSoundProperties.sound.
        /// Do not block or do heavy work here.
        /// </summary>
        void OnCreateProgrammerSound(
            FMODUserData userData,
            ref PROGRAMMER_SOUND_PROPERTIES programmerSoundProperties
        ) { }

        /// <summary>
        /// Called synchronously from FMOD thread. Sound is released automatically after this.
        /// Do not block or do heavy work here.
        /// </summary>
        void OnDestroyProgrammerSound(
            FMODUserData userData,
            ref PROGRAMMER_SOUND_PROPERTIES programmerSoundProperties
        ) { }

        void OnTimelineMarker(FMODUserData userData, TimelineMarkerData marker) { }
        void OnTimelineBeat(FMODUserData userData, TimelineBeatData beat) { }
        void OnSoundPlayed(FMODUserData userData, FMOD.Sound sound) { }
        void OnSoundStopped(FMODUserData userData, FMOD.Sound sound) { }
        void OnRealToVirtual(FMODUserData userData) { }
        void OnVirtualToReal(FMODUserData userData) { }
        void OnStartEventCommand(FMODUserData userData, EventInstance instance) { }
        void OnNestedTimelineBeat(FMODUserData userData, NestedTimelineBeatData beat) { }
    }
}
