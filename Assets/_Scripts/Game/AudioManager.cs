using UnityEngine;

public class AudioManager : MonoBehaviour
{
    private static AudioManager instance;

    [SerializeField] private AudioClip menuMusic;
    [SerializeField] private AudioClip stageMusic;
    [SerializeField] private AudioClip guardMusic;

    [SerializeField] private AudioSource source;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            PlayMenuMusic();
        }
        else
        {
            Destroy(this);
        }
    }

    public static void PlayMenuMusic()
    {
        if (instance != null)
        {
            if (instance.source != null)
            {
                instance.source.Stop();
                instance.source.clip = instance.menuMusic;
                instance.source.Play();
            }
        }
    }

    public static void PlayStageMusic()
    {
        if (instance != null)
        {
            if (instance.source != null)
            {
                instance.source.Stop();
                instance.source.clip = instance.stageMusic;
                instance.source.Play();
            }
        }
    }

    public static void PlayGuardMusic()
    {
        if (instance != null)
        {
            if (instance.source != null)
            {
                instance.source.Stop();
                instance.source.clip = instance.guardMusic;
                instance.source.Play();
            }
        }
    }

    public static void GenericPlayMusic(AudioClip clip)
    {
        if (instance != null)
        {
            if (instance.source != null && instance.source.clip != clip)
            {
                instance.source.Stop();
                instance.source.clip = clip;
                instance.source.Play();
            }
        }
    }
}