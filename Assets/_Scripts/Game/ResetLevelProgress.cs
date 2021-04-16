using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ResetLevelProgress : MonoBehaviour
{
    public List<ActivatableBehaviour> requiredActivatables = new List<ActivatableBehaviour>();

    public List<SelectSceneTimeMachine> SelectSceneTimeMachines = new List<SelectSceneTimeMachine>();
    public string MenuScene;
    void Start()
    {
        SelectSceneTimeMachines.AddRange(FindObjectsOfType<SelectSceneTimeMachine>());
    }
    
    void Update()
    {
        if (AllActivated())
        {
            foreach (var selectTimeMachine in SelectSceneTimeMachines)
            {
                PlayerPrefs.DeleteKey(selectTimeMachine.MyScene);
                PlayerPrefs.DeleteKey($"{selectTimeMachine.MyScene}_time");
            }

            SceneManager.LoadScene(MenuScene);
        }
    }

    private bool AllActivated()
    {
        bool valid = true;
        foreach (ActivatableBehaviour activatable in requiredActivatables)
        {
            valid &= activatable.IsActivated;
        }

        return valid;
    }
}
