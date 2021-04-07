﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class BasicTimeTracker : MonoBehaviour, ITimeTracker
{
    protected GameController gameController;

    public int ID { get; protected set; }

    public TimeVector Position { get; protected set; }
    protected TimeBool ItemForm { get; } = new TimeBool("ItemForm");

    public bool FlagDestroy { get; set; }

    public virtual bool ShouldPoolObject => _shouldPoolObject;
    [SerializeField] protected bool _shouldPoolObject;
    [SerializeField] protected bool _isItemable; // can the player hold this as an item?    

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
        if (!_isItemable) return false;
        
        ItemForm.Current = state;
        ItemForm.History = state;
        gameObject.SetActive(!ItemForm.AnyTrue && !FlagDestroy);
        return true;
    }
    
    public virtual void CopyTimeTrackerState(ITimeTracker other)
    {
        BasicTimeTracker otherTracker = other as BasicTimeTracker;
        if (otherTracker != null)
        {
            Position.Copy(otherTracker.Position);
            ItemForm.Copy(otherTracker.ItemForm);

            _shouldPoolObject = otherTracker._shouldPoolObject;
            _isItemable = otherTracker._isItemable;
        }
        else
        {
            gameController.LogError($"Cannot copy state from {other.GetType()} to {nameof(BasicTimeTracker)}");
        }
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
        gameObject.SetActive(!ItemForm.AnyTrue && !FlagDestroy);
    }

    public virtual void SaveSnapshot(TimeDict.TimeSlice snapshotDictionary, bool force=false)
    {
        snapshotDictionary.Set(GameController.FLAG_DESTROY, FlagDestroy, force);
        ItemForm.SaveSnapshot(snapshotDictionary, force);
        Position.SaveSnapshot(snapshotDictionary, force);
    }

    public virtual void LoadSnapshot(TimeDict.TimeSlice snapshotDictionary)
    {
        FlagDestroy = snapshotDictionary.Get<bool>(GameController.FLAG_DESTROY);
        ItemForm.LoadSnapshot(snapshotDictionary);
        Position.LoadSnapshot(snapshotDictionary);
        Position.Current = Position.History;

        gameObject.SetActive(!ItemForm.AnyTrue && !FlagDestroy);
    }

    public virtual void ForceLoadSnapshot(TimeDict.TimeSlice snapshotDictionary)
    {
        FlagDestroy = snapshotDictionary.Get<bool>(GameController.FLAG_DESTROY);
        ItemForm.ForceLoadSnapshot(snapshotDictionary);
        Position.ForceLoadSnapshot(snapshotDictionary);
        Position.Current = Position.History;
        
        gameObject.SetActive(!ItemForm.AnyTrue && !FlagDestroy);
    }
}
