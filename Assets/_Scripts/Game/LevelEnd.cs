using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[ExecuteInEditMode]
public class LevelEnd : InvisibleObject
{
    public string TransitionToLevel;
    public bool inferTransition;

    public static List<string> levels = null;
    public static List<string> levelTitles = null;
    
    private void Awake()
    {
        if (inferTransition)
        {
            if (levels == null)
            {
                TextAsset levelOrderText = Resources.Load<TextAsset>("LevelOrder");
                List<string> levelPairs = new List<string>(levelOrderText.text.Split(new char[] {'\n', '\r'}, StringSplitOptions.RemoveEmptyEntries));
                levels = new List<string>();
                levelTitles = new List<string>();
                foreach (string pair in levelPairs)
                {
                    string[] split = pair.Split(';');
                    levels.Add(split[0]);
                    levelTitles.Add(split[1]);
                }
            }

            int index = levels.IndexOf(SceneManager.GetActiveScene().name);
            if (index >= 0 && index < levels.Count)
            {
                TransitionToLevel = levels[index + 1];
            }
            else
            {
                Debug.LogError($"Could not find level after {SceneManager.GetActiveScene().name}, got index: {index}");
            }
        }
    }
}
