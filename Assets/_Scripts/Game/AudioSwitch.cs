using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioSwitch : MonoBehaviour
{

    [SerializeField] private AudioClip musicToPlay;


    void Start()
    {
	if(musicToPlay != null)
	{
            AudioManager.GenericPlayMusic(musicToPlay);
	}
    }
}
