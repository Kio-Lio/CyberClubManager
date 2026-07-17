using UnityEngine;
using UnityEngine.Audio;

public sealed class GameSettingsResources : ScriptableObject
{
    [SerializeField] private AudioMixer mainAudioMixer;

    public AudioMixer MainAudioMixer => mainAudioMixer;
}
