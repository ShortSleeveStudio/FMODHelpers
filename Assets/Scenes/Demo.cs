using System;
using System.IO;
using FMOD.Studio;
using FMODHelpers;
using FMODUnity;
using Piper;
using UnityEngine;

public class Demo : MonoBehaviour, IFMODStudioEventHandler
{
    #region Inspector
    [SerializeField]
    FMODManager fmod;

    [SerializeField]
    EventReference programmerSound;

    [SerializeField]
    EventReference test;

    [SerializeField]
    PiperManager piperManager;
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

    #region Byte Array
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

    #region Audio Table
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
        instance.SetProgrammerSound(result);
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
        SoundCreateResult? result = userData.ProgrammerSoundResult;
        if (result.HasValue)
        {
            programmerSoundProperties.sound = result.Value.Sound.handle;
            programmerSoundProperties.subsoundIndex = result.Value.SubsoundIndex;
        }
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
        UnityEditor.EditorGUILayout.LabelField("Text to Speech");
        _textArea = UnityEditor.EditorGUILayout.TextArea(_textArea);
        if (GUILayout.Button("Play TTS"))
        {
            _ = ((Demo)target).PlayTTS(_textArea);
        }
    }
}
#endif
