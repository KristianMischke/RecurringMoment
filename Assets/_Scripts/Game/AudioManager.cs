using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    
    [SerializeField] private AudioClip menuMusic;
    [SerializeField] private AudioClip stageMusic;
    [SerializeField] private AudioClip guardMusic;

    [SerializeField] private AudioSource source;    

    static private AudioManager instance;

    private void Awake()
    {
	if(instance == null)
	{
	    instance = this;
	    DontDestroyOnLoad(this.gameObject);
	}
	else
	{
	    Destroy(this);
	    return;
	}
    }

    private void Start()
    {
	PlayMenuMusic();
    }

    static public void PlayMenuMusic()
    {
	if(instance != null)
	{
	    if(instance.source != null)
	    {
		instance.source.Stop();
		instance.source.clip = instance.menuMusic;
		instance.source.Play();
	    }
	}
    }

    static public void PlayStageMusic()
    {
	if(instance != null)
	{
	    if(instance.source != null)
	    {
		instance.source.Stop();
		instance.source.clip = instance.stageMusic;
		instance.source.Play();
	    }
	}
    }

    static public void PlayGuardMusic()
    {
	if(instance != null)
	{
	    if(instance.source != null)
	    {
		instance.source.Stop();
		instance.source.clip = instance.guardMusic;
		instance.source.Play();
	    }
	}
    }

    static public void GenericPlayMusic(AudioClip clip)
    {
	if(instance != null)
	{
	    if(instance.source != null)
	    {
		instance.source.Stop();
		instance.source.clip = clip;
		instance.source.Play();
	    }
	}
    }
}
