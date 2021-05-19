using UnityEngine;

public class AudioManager : MonoBehaviour
{
    private static AudioManager instance;

    [SerializeField] private AudioClip menuMusic;
    [SerializeField] private AudioClip stageMusic;
    [SerializeField] private AudioClip guardMusic;

    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource ambientSource;

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
            if (instance.musicSource != null)
            {
                instance.musicSource.Stop();
                instance.musicSource.clip = instance.menuMusic;
                instance.musicSource.Play();
            }
        }
    }

    public static void PlayStageMusic()
    {
        if (instance != null)
        {
            if (instance.musicSource != null)
            {
                instance.musicSource.Stop();
                instance.musicSource.clip = instance.stageMusic;
                instance.musicSource.Play();
            }
        }
    }

    public static void PlayGuardMusic()
    {
        if (instance != null)
        {
            if (instance.musicSource != null)
            {
                instance.musicSource.Stop();
                instance.musicSource.clip = instance.guardMusic;
                instance.musicSource.Play();
            }
        }
    }

    public static void GenericPlayMusic(AudioClip clip)
    {
        if (instance != null)
        {
            if (instance.musicSource != null && instance.musicSource.clip != clip)
            {
                instance.musicSource.Stop();
                instance.musicSource.clip = clip;
                instance.musicSource.Play();
            }
        }
    }

    [SerializeField] private AudioClip[] _ambientSounds;
    private float _timer = 0, _ambientTrigger = 40;

    void Update()
    {
	if(instance._timer >= instance._ambientTrigger)
	{
	    instance._timer = 0;
	    instance._ambientTrigger = UnityEngine.Random.Range(60, 90);
	    instance.ambientSource.Stop();
	    instance.ambientSource.clip = _ambientSounds[UnityEngine.Random.Range(0, _ambientSounds.Length)];
	    instance.ambientSource.Play();
	}
	
	instance._timer += Time.deltaTime;
    }
}