using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class BasicTimeTracker : MonoBehaviour, ITimeTracker
{
    protected GameController gameController;

    public int ID { get; protected set; }

    public TimeVector Position { get; protected set; }
    private TimeBool ItemForm { get; } = new TimeBool("ItemForm");

    public bool FlagDestroy { get; set; }

    public virtual bool ShouldPoolObject => _shouldPoolObject;
    [SerializeField] private bool _shouldPoolObject;
    

    private Collider2D _collider2d;
    public Collider2D Collider2D
    {
        get
        {
            if (_collider2d == null)
            {
                _collider2d = gameObject.GetComponentInChildren<Collider2D>();
            }
            return _collider2d;
        }
    }

    public virtual bool SetItemState(bool state)
    {
        ItemForm.Current = state;
        gameObject.SetActive(!ItemForm.AnyTrue);
        return true;
    }

    public virtual void OnPoolInstantiate() { }
    public virtual void OnPoolInit() { }
    public virtual void OnPoolRelease() { }

    public virtual void Init(GameController gameController, int id)
    {
        this.gameController = gameController;
        ID = id;
        
        Position = new TimeVector("Position", x => transform.position = x, () => transform.position);
    }

    public virtual void GameUpdate()
    {
        gameObject.SetActive(!ItemForm.AnyTrue);
    }

    public virtual void SaveSnapshot(TimeDict.TimeSlice snapshotDictionary, bool force=false)
    {
        if (FlagDestroy)
        {
            snapshotDictionary.Set(GameController.FLAG_DESTROY, true, force);
        }

        ItemForm.SaveSnapshot(snapshotDictionary, force);
        Position.SaveSnapshot(snapshotDictionary, force);
    }

    public virtual void LoadSnapshot(TimeDict.TimeSlice snapshotDictionary)
    {
        ItemForm.LoadSnapshot(snapshotDictionary);
        Position.LoadSnapshot(snapshotDictionary);

        gameObject.SetActive(!ItemForm.AnyTrue);
    }

    public virtual void ForceLoadSnapshot(TimeDict.TimeSlice snapshotDictionary)
    {
        ItemForm.ForceLoadSnapshot(snapshotDictionary);
        Position.ForceLoadSnapshot(snapshotDictionary);
        
        gameObject.SetActive(!ItemForm.AnyTrue);
    }
}
