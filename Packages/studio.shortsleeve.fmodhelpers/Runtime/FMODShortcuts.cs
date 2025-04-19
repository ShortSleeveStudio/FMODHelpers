using System.Runtime.InteropServices;
using System.Threading;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;

namespace FMODHelpers
{
    public struct SoundCreateResult
    {
        public int SubsoundIndex;
        public FMOD.Sound Sound;
    }

    public static class FMODShortcuts
    {
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

            // Wait to until loaded
            await WaitUntilLoaded(sound, token);

            // Return sound
            return new SoundCreateResult() { Sound = sound, SubsoundIndex = -1 };
        }

        public struct CreateSoundInfo
        {
            public byte[] Data;
            public int NumberOfChannels;
            public int Frequency;
            public FMOD.SOUND_FORMAT Format;
            public bool IsRaw;
        }

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

            // Wait to until loaded
            await WaitUntilLoaded(sound, token);

            // Return sound
            return new SoundCreateResult() { Sound = sound, SubsoundIndex = -1 };
        }

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

            // Wait to until loaded
            await WaitUntilLoaded(sound, token);

            // Return sound
            return new SoundCreateResult()
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
                await Awaitable.NextFrameAsync(token);
                sound.getOpenState(out openstate, out percentbuffered, out starving, out diskbusy);
                if (!warningTriggered && Time.unscaledTime - start > 2) // Warning if it takes longer than 2 seconds
                {
                    Debug.LogWarning($"Loading sound from byte array is taking a long time...");
                    warningTriggered = true;
                }
            }
        }
        #endregion
    }
}
