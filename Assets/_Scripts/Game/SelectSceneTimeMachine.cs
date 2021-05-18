using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

[ExecuteInEditMode]
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

	private static readonly int MainTex = Shader.PropertyToID("_MainTex");
	private static readonly int MainColor = Shader.PropertyToID("_MainColor");
	
    void Start()
    {
        int numPlays = PlayerPrefs.GetInt($"{MyScene}", defaultValue:GameController.SCENE_LOCKED);
        _sceneIsUnlocked = numPlays != GameController.SCENE_LOCKED;
        _didCompleteScene = numPlays > 0;
        _bestTime = PlayerPrefs.GetFloat($"{MyScene}_time", defaultValue:float.PositiveInfinity);
        
		levelShow.text = writeStuffHereToShow; 
		levelShow.rectTransform.parent.transform.parent.gameObject.SetActive(false);

		Color indicatorColor;
        if (!_sceneIsUnlocked) // occupied color when locked
        {
	        indicatorColor = new Color(1f, 0f, 0f);
            timeText.text = "LOCKED";
        }
        else if (!_didCompleteScene) // unlocked, not completed, so active color 
        {
	        indicatorColor = Color.yellow;
            timeText.text = "READY";
        }
        else
        {
	        indicatorColor = new Color(0f, 1f, 0f);
            TimeSpan timeSpan = TimeSpan.FromSeconds(_bestTime);
            timeText.text = timeSpan.ToString(@"mm\:ss\.ff");
        }
        
        MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
        propertyBlock.SetTexture(MainTex, renderer.sprite.texture);
        
        timeText.color = indicatorColor;
        propertyBlock.SetColor(MainColor, indicatorColor);
        renderer.SetPropertyBlock(propertyBlock);
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
