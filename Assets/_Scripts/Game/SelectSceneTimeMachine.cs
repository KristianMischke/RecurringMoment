﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SelectSceneTimeMachine : MonoBehaviour
{
    private PlayerController touchingPlayer;

    public string MyScene;

    private bool _didCompleteScene;
    private bool _sceneIsUnlocked;
    private float _bestTime;
    
    // art related
    public SpriteRenderer renderer;
    public TMP_Text timeText;
	public TMP_Text levelShow;
	//public int currLevel = 0; 
	public string writeStuffHereToShow;

    void Start()
    {
        int numPlays = PlayerPrefs.GetInt($"{MyScene}", defaultValue:GameController.SCENE_LOCKED);
        _sceneIsUnlocked = numPlays != GameController.SCENE_LOCKED;
        _didCompleteScene = numPlays > 0;
        _bestTime = PlayerPrefs.GetFloat($"{MyScene}_time", defaultValue:float.PositiveInfinity);
        
		
		
		
		levelShow = GetComponentInChildren<Canvas>().GetComponentInChildren<BoxCollider2D>().GetComponentInChildren<TMP_Text>();
		
	
		levelShow.text = writeStuffHereToShow; 
		levelShow.rectTransform.parent.transform.parent.gameObject.SetActive(false); 
		
        if (!_sceneIsUnlocked) // occupied color when locked
        {
            renderer.color = new Color(0.8f, 0.8f, 1f);
            timeText.text = "LOCKED";
        }
        else if (!_didCompleteScene) // unlocked, not completed, so active color 
        {
            renderer.color = new Color(1f, 1f, 0.8f);
            timeText.text = "READY";
        }
        else
        {
            renderer.color = new Color(1f, 1f, 1f); // completed, so normal (TODO: we should rethink these in beta)
            TimeSpan timeSpan = TimeSpan.FromSeconds(_bestTime);
            timeText.text = timeSpan.ToString(@"mm\:ss\.ff");
        }
		
    }

    public void Update()
    {
        if (_sceneIsUnlocked && touchingPlayer != null && touchingPlayer.IsActivating)
        {
            SceneManager.LoadScene(MyScene);
        }
		if(touchingPlayer)
		{
			levelShow.rectTransform.parent.transform.parent.gameObject.SetActive(true); 

		}
		if(!touchingPlayer)
		{
			levelShow.rectTransform.parent.transform.parent.gameObject.SetActive(false); 
		}
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.tag == "Player")
        {
            touchingPlayer = collision.gameObject.GetComponent<PlayerController>();
			levelShow.rectTransform.parent.transform.parent.gameObject.SetActive(true); 
        }
    }
    void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.gameObject.tag == "Player")
        {
            touchingPlayer = null;
			levelShow.rectTransform.parent.transform.parent.gameObject.SetActive(true); 
 
        }
    }
}
