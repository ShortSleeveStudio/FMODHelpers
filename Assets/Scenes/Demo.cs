using System;
using System.IO;
using FMOD.Studio;
using FMODHelpers;
using Piper;
using UnityEngine;

// public class AudioTableNoLoadTester : IFMODStudioEventHandler
// {
//     int _subSoundIndex;
//     IntPtr _soundHandle;

//     public AudioTableNoLoadTester(string tableKey)
//     {
//         // Load Sound Info
//         SOUND_INFO soundInfo;
//         FMOD.RESULT keyResult = FMODUnity.RuntimeManager.StudioSystem.getSoundInfo(tableKey, out soundInfo);
//         if (keyResult != FMOD.RESULT.OK)
//         {
//             Debug.LogError($"Couldn't find audio in table with key: {tableKey}");
//             return;
//         }

//         // Load Sound
//         FMOD.Sound sound;
//         FMOD.MODE soundMode =
//             FMOD.MODE.LOOP_NORMAL | FMOD.MODE.CREATECOMPRESSEDSAMPLE | FMOD.MODE.NONBLOCKING;
//         FMOD.RESULT soundResult = RuntimeManager.CoreSystem.createSound(
//             soundInfo.name_or_data,
//             soundMode | soundInfo.mode,
//             ref soundInfo.exinfo,
//             out sound
//         );
//         if (soundResult != FMOD.RESULT.OK)
//         {
//             Debug.LogError($"Couldn't load audio in table with key: {tableKey}");
//             return;
//         }
//         _soundHandle = sound.handle;
//         _subSoundIndex = soundInfo.subsoundindex;
//     }

//     #region Programmer Instrument
//     void IFMODStudioEventHandler.OnCreateProgrammerSound(
//         bool isMainThread,
//         FMODUserData userData,
//         ref PROGRAMMER_SOUND_PROPERTIES programmerSoundProperties
//     )
//     {
//         programmerSoundProperties.sound = _soundHandle;
//         programmerSoundProperties.subsoundIndex = _subSoundIndex;
//     }
//     #endregion
// }

public class Demo : MonoBehaviour, IFMODStudioEventHandler
{
    #region Inspector
    [SerializeField]
    FMODManager fmod;

    [SerializeField]
    FMODEventRef programmerSound;

    [SerializeField]
    PiperManager piperManager;
    #endregion

    #region State
    SoundCreateResult[] soundTable;
    #endregion

    #region Unity Lifecycle
    void Awake()
    {
        soundTable = new SoundCreateResult[128];
    }
    #endregion

    #region Play File
    public async Awaitable PlayFromFile()
    {
        string path = $"{Application.streamingAssetsPath}/TestAudio.wav";
        SoundCreateResult result = await FMODShortcuts.CreateSoundFromFile(
            path,
            destroyCancellationToken
        );
        PlaySound(result);
    }
    #endregion

    #region Play Byte Array
    public async Awaitable PlayFromByteArray()
    {
        string path = $"{Application.streamingAssetsPath}/TestAudio.wav";
        byte[] byteArray = File.ReadAllBytes(path);
        SoundCreateResult result = await FMODShortcuts.CreateSoundFromByteArray(
            new() { Data = byteArray },
            destroyCancellationToken
        );
        PlaySound(result);
    }
    #endregion

    #region Play Audio Table
    public async Awaitable PlayFromAudioTable()
    {
        string key = $"TestAudio";
        SoundCreateResult result = await FMODShortcuts.CreateSoundFromAudioTable(
            key,
            destroyCancellationToken
        );
        PlaySound(result);
    }
    #endregion

    // #region Play Audio Table - No Preload
    // public void PlayFromAudioTableNoPreload()
    // {
    //     string key = $"TestAudio";
    //     AudioTableNoLoadTester test = new(key);
    //     EventInstance instance = fmod.GetEventInstance(
    //         programmerSound,
    //         EVENT_CALLBACK_TYPE.CREATE_PROGRAMMER_SOUND
    //     );
    //     instance.RegisterCallbackHandler(test);
    //     instance.Start();
    //     instance.Release();
    // }
    // #endregion

    #region TTS
    public async Awaitable PlayTTS(string output)
    {
        float[] samples = await piperManager.TextToSpeech(output);
        byte[] bytes = new byte[samples.Length * sizeof(float)];
        Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);
        SoundCreateResult result = await FMODShortcuts.CreateSoundFromByteArray(
            new()
            {
                Data = bytes,
                Frequency = 22050,
                NumberOfChannels = 1,
                Format = FMOD.SOUND_FORMAT.PCMFLOAT,
                IsRaw = true,
            },
            destroyCancellationToken
        );
        PlaySound(result);
    }
    #endregion

    #region Helpers
    void PlaySound(SoundCreateResult result)
    {
        EventInstance instance = fmod.GetEventInstance(
            programmerSound,
            EVENT_CALLBACK_TYPE.CREATE_PROGRAMMER_SOUND
        );
        soundTable[instance.GetUserData().ID] = result;
        instance.RegisterCallbackHandler(this);
        instance.Start();
        instance.Release();
    }
    #endregion

    #region Programmer Instrument
    void IFMODStudioEventHandler.OnCreateProgrammerSound(
        bool isMainThread,
        FMODUserData userData,
        ref PROGRAMMER_SOUND_PROPERTIES programmerSoundProperties
    )
    {
        SoundCreateResult result = soundTable[userData.ID];
        programmerSoundProperties.sound = result.Sound.handle;
        programmerSoundProperties.subsoundIndex = result.SubsoundIndex;
    }
    #endregion
}

#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(Demo))]
public class DemoEditor : UnityEditor.Editor
{
    string _textArea;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        if (!Application.isPlaying)
            return;
        if (GUILayout.Button("Play from File Path"))
        {
            _ = ((Demo)target).PlayFromFile();
        }
        if (GUILayout.Button("Play from Byte Array"))
        {
            _ = ((Demo)target).PlayFromByteArray();
        }
        if (GUILayout.Button("Play from Audio Table"))
        {
            _ = ((Demo)target).PlayFromAudioTable();
        }
        // if (GUILayout.Button("Play from Audio Table - No Preload"))
        // {
        //     ((Demo)target).PlayFromAudioTableNoPreload();
        // }
        UnityEditor.EditorGUILayout.LabelField("Text to Speech");
        _textArea = UnityEditor.EditorGUILayout.TextArea(_textArea);
        if (GUILayout.Button("Play TTS"))
        {
            _ = ((Demo)target).PlayTTS(_textArea);
        }
    }
}
#endif
