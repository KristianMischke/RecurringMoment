using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RetryPopup : MonoBehaviour
{

    public TMP_Text titleText;
    public TMP_Text messageText;
    public Button retryLevelButton;
    public Button respawnButton;

    private Action retryLevelAction;
    private Action respawnAction;

    public void Init(string title, string message, Action retryLevelAction, Action respawnAction)
    {
        titleText.text = title;
        messageText.text = message;
        this.retryLevelAction = retryLevelAction;
        this.respawnAction = respawnAction;
    }

    void Start()
    {
        retryLevelButton.onClick.AddListener(OnRetryLevelPressed);
        respawnButton.onClick.AddListener(OnRespawnPressed);   
    }

    private void OnRetryLevelPressed()
    {
        retryLevelAction?.Invoke();
        Destroy(gameObject); //TODO: destroying and recreating not the best idea long term... pool popup? make generic popup class?
    }
    
    private void OnRespawnPressed()
    {
        respawnAction?.Invoke();
        Destroy(gameObject); //TODO: destroying and recreating not the best idea long term... pool popup? make generic popup class?
    }
}
