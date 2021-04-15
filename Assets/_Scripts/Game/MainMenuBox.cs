using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MainMenuBox : MonoBehaviour
{
    public string ShowAtLevel;
    void Start()
    {
        gameObject.SetActive(PlayerPrefs.GetInt(ShowAtLevel, defaultValue: GameController.SCENE_LOCKED) > 0);
    }
}
