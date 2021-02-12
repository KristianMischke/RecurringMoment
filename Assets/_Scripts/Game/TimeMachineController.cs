using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TimeMachineController : MonoBehaviour, ITimeTracker
{
    private HashSet<GameObject> triggeringObjects = new HashSet<GameObject>();
    private bool occupied = true;
    private GameController gameController;

    public bool IsActivated { get; private set; }
    public bool Occupied => occupied;
    public int ActivatedTimeStep { get; private set; }
    public int ID { get; private set; }

    public bool Activate(int currentTimeStep, PlayerController player, out bool doTimeTravel)
    {
        doTimeTravel = false;

        if (!occupied)
            return false;

        if (IsActivated)
        {
            IsActivated = false;
            doTimeTravel = true;
        }
        else
        {
            IsActivated = true;
            ActivatedTimeStep = currentTimeStep;
        }

        return true;
    }

    public bool IsTouching(GameObject other)
    {
        return triggeringObjects.Contains(other);
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
        snapshotDictionary[nameof(occupied)] = IsActivated; // NOTE: when loading it will be occupied when it is currently active
    }

    public void LoadSnapshot(Dictionary<string, object> snapshotDictionary)
    {
        occupied = (bool)snapshotDictionary[nameof(occupied)];
    }
}
