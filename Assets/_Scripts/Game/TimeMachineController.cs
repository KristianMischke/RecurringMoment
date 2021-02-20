﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class TimeMachineController : MonoBehaviour, ITimeTracker
{
    public const int TIME_MACHINE_COUNTDOWN = 500;

    private HashSet<GameObject> triggeringObjects = new HashSet<GameObject>();
    private GameController gameController;

    // art related
    public SpriteRenderer renderer;
    public TMP_Text timeText;

    public bool HistoryActivated = false;
    public bool HistoryOccupied = false;
    public int HistoryActivatedTimeStep = -1;
    public int HistoryCountdown = -1;

    public bool CurrentlyActivated = false;
    public bool CurrentlyOccupied = false;
    public int CurrentActivatedTimeStep = -1;
    public int CurrentCountdown = -1;

    public bool IsActivatedOrOccupied => HistoryActivated || CurrentlyActivated || HistoryOccupied || CurrentlyOccupied;
    public int ID { get; private set; }
    public bool FlagDestroy { get; set; }

    public bool Activate(out int timeTravelDestStep)
    {
        timeTravelDestStep = -1;

        if (CurrentlyOccupied || HistoryOccupied || CurrentCountdown >= 0 || HistoryCountdown >= 0) // time machine is occupied, cannot use it
            return false;

        if (CurrentlyActivated || HistoryActivated) // time machine is active, so deactivate and do timetravel
        {
            timeTravelDestStep = CurrentlyActivated ? CurrentActivatedTimeStep : HistoryActivatedTimeStep;
            CurrentActivatedTimeStep = -1;
            CurrentlyActivated = false;
        }
        else
        {
            CurrentCountdown = TIME_MACHINE_COUNTDOWN;
        }

        return true;
    }

    public void DoTimeTravel(bool isTravellingMachine)
    {
        CurrentlyActivated = false;
        CurrentActivatedTimeStep = -1;

        if (!isTravellingMachine)
        {
            CurrentlyOccupied = false;
        }
    }

    public void BackToPresent()
    {
        CurrentlyActivated = HistoryActivated;
        CurrentlyOccupied = HistoryOccupied;
        CurrentActivatedTimeStep = HistoryActivatedTimeStep;
        CurrentCountdown = HistoryCountdown;

        HistoryActivated = false;
        HistoryOccupied = false;
        HistoryActivatedTimeStep = -1;
        HistoryCountdown = -1;
    }

    public bool IsTouching(GameObject other)
    {
        return triggeringObjects.Contains(other);
    }


    public void GameUpdate()
    {
        if (CurrentCountdown > 0)
        {
            CurrentCountdown--;
        }

        if (CurrentCountdown == 0)
        {
            CurrentCountdown = -1;

            CurrentlyActivated = true;
            CurrentActivatedTimeStep = gameController.TimeStep;
        }
    }

    public void Start()
    {
        renderer = GetComponentInChildren<SpriteRenderer>();
    }
    public void Update()
    {
        if (CurrentlyOccupied || HistoryOccupied)
        {
            renderer.color = new Color(0.8f, 0.8f, 1f);
        }
        else if (CurrentlyActivated || HistoryActivated)
        {
            renderer.color = new Color(1f, 1f, 0.8f);
        }
        else
        {
            renderer.color = new Color(1f, 1f, 1f);
        }

        int displayStartStep = CurrentActivatedTimeStep == -1 ? HistoryActivatedTimeStep : CurrentActivatedTimeStep;
        int displayCountdown = CurrentCountdown == -1 ? HistoryCountdown : CurrentCountdown;
        if (displayCountdown >= 0)
        {
            timeText.text = displayCountdown.ToString();
        }
        else if (displayStartStep >= 0)
        {
            timeText.text = (gameController.TimeStep - displayStartStep).ToString();
        }
        else
        {
            timeText.text = "";
        }
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.tag == "Player")
        {
            triggeringObjects.Add(collision.gameObject);
        }
    }
    void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.gameObject.tag == "Player")
        {
            triggeringObjects.Remove(collision.gameObject);
        }
    }

    public void Init(GameController gameController, int id)
    {
        this.gameController = gameController;
        ID = id;
    }

    public void SaveSnapshot(Dictionary<string, object> snapshotDictionary)
    {
        snapshotDictionary[nameof(CurrentlyActivated)] = CurrentlyActivated || HistoryActivated;
        snapshotDictionary[nameof(CurrentlyOccupied)] = CurrentlyOccupied || HistoryOccupied;
        snapshotDictionary[nameof(CurrentActivatedTimeStep)] = CurrentActivatedTimeStep == -1 ? HistoryActivatedTimeStep : CurrentActivatedTimeStep;
        snapshotDictionary[nameof(CurrentCountdown)] = CurrentActivatedTimeStep == -1 ? CurrentCountdown : HistoryCountdown;

        if (FlagDestroy)
        {
            snapshotDictionary[GameController.FLAG_DESTROY] = true;
        }
    }

    public void LoadSnapshot(Dictionary<string, object> snapshotDictionary)
    {
        HistoryActivated = (bool)snapshotDictionary[nameof(CurrentlyActivated)];
        HistoryOccupied = (bool)snapshotDictionary[nameof(CurrentlyOccupied)];
        HistoryActivatedTimeStep = (int)snapshotDictionary[nameof(CurrentActivatedTimeStep)];
        HistoryCountdown = (int)snapshotDictionary[nameof(CurrentCountdown)];

        CurrentlyOccupied &= HistoryActivated;
    }
}
