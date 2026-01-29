using System.Runtime.InteropServices;
using System.Threading;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;

namespace FMODHelpers
{
    /// <summary>
    /// Result from creating a sound for use with programmer sound events.
    /// Pass this to EventInstance.SetProgrammerSound() before starting the event.
    /// </summary>
    public struct SoundCreateResult
    {
        /// <summary>
        /// Subsound index within the sound, or -1 for single sounds.
        /// </summary>
        public int SubsoundIndex;

        /// <summary>
        /// The FMOD.Sound handle.
        /// </summary>
        public FMOD.Sound Sound;
    }

    /// <summary>
    /// Helper methods for creating FMOD sounds from various sources for use with programmer sound events.
    /// </summary>
    public static class FMODShortcuts
    {
        /// <summary>
        /// Creates an FMOD sound from a file path asynchronously.
        /// Use this for loading audio files at runtime (e.g., user-generated content, DLC audio).
        /// </summary>
        /// <param name="filePath">Full path to the audio file (e.g., WAV, MP3, OGG)</param>
        /// <param name="token">Cancellation token to cancel the load operation</param>
        /// <returns>SoundCreateResult for use with SetProgrammerSound(), or default if load failed</returns>
        public static async Awaitable<SoundCreateResult> CreateSoundFromFile(
            string filePath,
            CancellationToken token
        )
        {
            // Load sound
            FMOD.Sound sound;
            FMOD.CREATESOUNDEXINFO createSoundInfo =
                new() { cbsize = Marshal.SizeOf<FMOD.CREATESOUNDEXINFO>() };
            FMOD.RESULT soundResult = RuntimeManager.CoreSystem.createSound(
                filePath,
                FMOD.MODE.LOOP_NORMAL | FMOD.MODE.CREATECOMPRESSEDSAMPLE | FMOD.MODE.NONBLOCKING,
                ref createSoundInfo,
                out sound
            );
            if (soundResult != FMOD.RESULT.OK)
            {
                Debug.LogError($"Couldn't load sound from file: {filePath}");
                return default;
            }

            // Wait until loaded
            await WaitUntilLoaded(sound, token);

            // Return sound
            return new() { Sound = sound, SubsoundIndex = -1 };
        }

        /// <summary>
        /// Information needed to create a sound from raw audio data.
        /// </summary>
        public struct CreateSoundInfo
        {
            /// <summary>
            /// Raw audio data bytes.
            /// </summary>
            public byte[] Data;

            /// <summary>
            /// Number of audio channels (1 = mono, 2 = stereo).
            /// </summary>
            public int NumberOfChannels;

            /// <summary>
            /// Sample rate in Hz (e.g., 44100, 48000, 22050).
            /// </summary>
            public int Frequency;

            /// <summary>
            /// Audio format (e.g., PCMFLOAT, PCM16, PCM8).
            /// </summary>
            public FMOD.SOUND_FORMAT Format;

            /// <summary>
            /// If true, data is raw PCM. If false, data is encoded (WAV/MP3/etc).
            /// </summary>
            public bool IsRaw;
        }

        /// <summary>
        /// Creates an FMOD sound from a byte array asynchronously.
        /// Use this for procedurally generated audio, TTS, or audio decoded from custom formats.
        /// </summary>
        /// <param name="soundData">Audio data and format information</param>
        /// <param name="token">Cancellation token to cancel the load operation</param>
        /// <returns>SoundCreateResult for use with SetProgrammerSound(), or default if creation failed</returns>
        public static async Awaitable<SoundCreateResult> CreateSoundFromByteArray(
            CreateSoundInfo soundData,
            CancellationToken token
        )
        {
            // Load sound
            FMOD.Sound sound;
            FMOD.CREATESOUNDEXINFO createSoundInfo =
                new()
                {
                    cbsize = Marshal.SizeOf<FMOD.CREATESOUNDEXINFO>(),
                    length = (uint)soundData.Data.Length,
                    numchannels = soundData.NumberOfChannels,
                    defaultfrequency = soundData.Frequency,
                    format = soundData.Format,
                };
            FMOD.MODE mode = FMOD.MODE.NONBLOCKING | FMOD.MODE.OPENMEMORY;
            if (soundData.IsRaw)
            {
                mode |= FMOD.MODE.OPENRAW;
                mode |= FMOD.MODE.LOOP_OFF;
            }
            else
            {
                mode |= FMOD.MODE.LOOP_NORMAL;
            }
            FMOD.RESULT soundResult = RuntimeManager.CoreSystem.createSound(
                soundData.Data,
                mode,
                ref createSoundInfo,
                out sound
            );
            if (soundResult != FMOD.RESULT.OK)
            {
                Debug.LogError($"Couldn't create sound from byte array");
                return default;
            }

            // Wait until loaded
            await WaitUntilLoaded(sound, token);

            // Return sound
            return new() { Sound = sound, SubsoundIndex = -1 };
        }

        /// <summary>
        /// Creates an FMOD sound from an entry in an FMOD Audio Table asynchronously.
        /// Audio tables are defined in FMOD Studio and allow managing many audio files as a single bank.
        /// </summary>
        /// <param name="tableKey">Key of the audio entry in the FMOD Audio Table</param>
        /// <param name="token">Cancellation token to cancel the load operation</param>
        /// <returns>SoundCreateResult for use with SetProgrammerSound(), or default if not found</returns>
        public static async Awaitable<SoundCreateResult> CreateSoundFromAudioTable(
            string tableKey,
            CancellationToken token
        )
        {
            // Load Sound Info
            SOUND_INFO soundInfo;
            FMOD.RESULT keyResult = RuntimeManager.StudioSystem.getSoundInfo(
                tableKey,
                out soundInfo
            );
            if (keyResult != FMOD.RESULT.OK)
            {
                Debug.LogError($"Couldn't find audio in table with key: {tableKey}");
                return default;
            }

            // Load Sound
            FMOD.Sound sound;
            FMOD.MODE soundMode =
                FMOD.MODE.LOOP_NORMAL | FMOD.MODE.CREATECOMPRESSEDSAMPLE | FMOD.MODE.NONBLOCKING;
            FMOD.RESULT soundResult = RuntimeManager.CoreSystem.createSound(
                soundInfo.name_or_data,
                soundMode | soundInfo.mode,
                ref soundInfo.exinfo,
                out sound
            );
            if (soundResult != FMOD.RESULT.OK)
            {
                Debug.LogError($"Couldn't load audio in table with key: {tableKey}");
                return default;
            }

            // Wait until loaded
            await WaitUntilLoaded(sound, token);

            // Return sound
            return new()
            {
                Sound = sound,
                SubsoundIndex = soundInfo.subsoundindex,
            };
        }

        #region Private API
        static async Awaitable WaitUntilLoaded(FMOD.Sound sound, CancellationToken token)
        {
            FMOD.OPENSTATE openstate;
            uint percentbuffered;
            bool starving;
            bool diskbusy;
            sound.getOpenState(out openstate, out percentbuffered, out starving, out diskbusy);
            float start = Time.unscaledTime;
            bool warningTriggered = false;
            while (openstate != FMOD.OPENSTATE.READY)
            {
                if (openstate == FMOD.OPENSTATE.ERROR)
                {
                    Debug.LogError("Failed to load sound - file may be corrupted or invalid");
                    return;
                }
                await Awaitable.NextFrameAsync(token);
                sound.getOpenState(out openstate, out percentbuffered, out starving, out diskbusy);
                if (!warningTriggered && Time.unscaledTime - start > 2) // Warning if it takes longer than 2 seconds
                {
                    Debug.LogWarning($"Loading sound is taking a long time...");
                    warningTriggered = true;
                }
            }
        }
        #endregion
    }
}
