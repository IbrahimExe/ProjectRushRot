using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class AudioManager : MonoBehaviour
{
    [Header("Soundtracks")]
    [SerializeField] private AudioClip mainSoundtrack;

    [Header("Settings")]
    [SerializeField] [Range(0f, 1f)] private float volume = 1f;

    private AudioSource _audioSource;
    private bool _hasStarted = false;

    void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        _audioSource.clip = mainSoundtrack;
        _audioSource.loop = true;
        _audioSource.playOnAwake = false;
        _audioSource.volume = volume;
    }

    void Update()
    {
        // Begin playback the first time the game starts
        if (!_hasStarted && GameState.IsStarted)
        {
            _hasStarted = true;
            _audioSource.Play();
        }

        // Reset so the track can start again on a new run
        if (_hasStarted && !GameState.IsStarted)
        {
            _hasStarted = false;
            _audioSource.Stop();
        }
    }

    // Called by GameManager when the game is paused
    public void PauseMusic()
    {
        if (_audioSource.isPlaying)
            _audioSource.Pause();
    }

    // Called by GameManager after the resume countdown finishes
    public void ResumeMusic()
    {
        if (!_audioSource.isPlaying && _hasStarted)
            _audioSource.UnPause();
    }

    public void StopMusic()
    {
        if (_audioSource.isPlaying)
            _audioSource.Stop();
    }
}
