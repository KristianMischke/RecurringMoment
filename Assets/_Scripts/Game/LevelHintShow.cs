using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelHintShow : MonoBehaviour
{
    public int NumTries;
    void Start()
    {
        // destroy if less than acceptable number of tries to show hint
        if (PlayerPrefs.GetInt($"{SceneManager.GetActiveScene().name}_starts", defaultValue: 0) < NumTries)
        {
            Destroy(gameObject);
        }
    }
}