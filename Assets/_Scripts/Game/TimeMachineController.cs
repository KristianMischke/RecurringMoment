using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TimeMachineController : MonoBehaviour, ITimeTracker
{
    private HashSet<GameObject> triggeringObjects = new HashSet<GameObject>();
    private GameController gameController;

    // art related
    public SpriteRenderer renderer;

    public bool HistoryActivated = false;
    public bool HistoryOccupied = false;
    public int HistoryActivatedTimeStep = -1;

    public bool CurrentlyActivated = false;
    public bool CurrentlyOccupied = false;
    public int CurrentActivatedTimeStep = -1;

    public bool IsActivatedOrOccupied => HistoryActivated || CurrentlyActivated || HistoryOccupied || CurrentlyOccupied;
    public int ID { get; private set; }
    public bool FlagDestroy { get; set; }

    public bool Activate(int currentTimeStep, PlayerController player, out bool doTimeTravel)
    {
        doTimeTravel = false;

        if (CurrentlyOccupied || HistoryOccupied) // time machine is occupied, cannot use it
            return false;

        if (CurrentlyActivated || HistoryActivated) // time machine was active, so deactivate and do timetravel
        {
            CurrentlyActivated = false;
            doTimeTravel = true;
        }
        else
        {
            CurrentlyActivated = true;
            CurrentActivatedTimeStep = currentTimeStep;
        }

        return true;
    }

    public bool IsTouching(GameObject other)
    {
        return triggeringObjects.Contains(other);
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
        snapshotDictionary[nameof(CurrentlyActivated)] = CurrentlyActivated;
        snapshotDictionary[nameof(CurrentlyOccupied)] = CurrentlyOccupied;
        snapshotDictionary[nameof(CurrentActivatedTimeStep)] = CurrentActivatedTimeStep;
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

        CurrentlyOccupied &= HistoryActivated || HistoryOccupied;
    }
}
