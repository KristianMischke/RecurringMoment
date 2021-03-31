using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class BasicTimeTracker : MonoBehaviour, ITimeTracker
{
    private GameController gameController;

    private bool touchedPlayer = false;
    public int ID { get; private set; }

    public TimePosition Position { get; private set; }
    private TimeBool ItemForm { get; } = new TimeBool("ItemForm");

    public bool FlagDestroy { get; set; }

    private Collider2D _collider2d;
    public Collider2D Collider2D
    {
        get
        {
            if (_collider2d == null)
            {
                _collider2d = GetComponentInChildren<Collider2D>();
            }
            return _collider2d;
        }
    }

    public bool SetItemState(bool state)
    {
        ItemForm.Current = state;
        gameObject.SetActive(!ItemForm.AnyTrue);
        return true;
    }

    void FixedUpdate()
    {
        gameObject.SetActive(!ItemForm.AnyTrue);
        
        if (Collider2D.IsTouching(gameController.player.CapsuleCollider))
        {
            touchedPlayer = true;
        }
        if (gameController.IsPresent) //TODO: prolly not the best place for this... should make .WhenPresent() method to reset certain variables
        {
            touchedPlayer = false;
        }
    }

    public void Init(GameController gameController, int id)
    {
        this.gameController = gameController;
        ID = id;
        
        Position = new TimePosition("Position", x => transform.position = x, () => transform.position);
    }

    public void SaveSnapshot(TimeDict.TimeSlice snapshotDictionary)
    {
        if (FlagDestroy)
        {
            snapshotDictionary.Set(GameController.FLAG_DESTROY, true);
        }

        ItemForm.SaveSnapshot(snapshotDictionary);
        Position.SaveSnapshot(snapshotDictionary);
    }

    public void LoadSnapshot(TimeDict.TimeSlice snapshotDictionary)
    {
        ItemForm.LoadSnapshot(snapshotDictionary);
        Position.LoadSnapshot(snapshotDictionary);

        gameObject.SetActive(!ItemForm.AnyTrue);
    }

    public void ForceLoadSnapshot(TimeDict.TimeSlice snapshotDictionary)
    {
        ItemForm.ForceLoadSnapshot(snapshotDictionary);
        Position.ForceLoadSnapshot(snapshotDictionary);
        
        gameObject.SetActive(!ItemForm.AnyTrue);
    }
}
