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
    protected bool ItemForm = false;

    public bool FlagDestroy { get; set; }
    public bool prevFlagDestroy;

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
        
        ItemForm = state;
        return true;
    }

    public virtual void GetItemSpriteProperties(out Sprite sprite, out Color color)
    {
        var sr = GetComponentInChildren<SpriteRenderer>();
        sprite = sr.sprite;
        color = sr.color;
    }
    
    public virtual bool IsEquivalentItem(ITimeTracker other)
    {
        return other is BasicTimeTracker;
    }

    public virtual void ExecutePastEvent(TimeEvent timeEvent)
    {
    }

    public virtual void CopyTimeTrackerState(ITimeTracker other)
    {
        BasicTimeTracker otherTracker = other as BasicTimeTracker;
        if (otherTracker != null)
        {
            Position.Copy(otherTracker.Position);
            ItemForm = otherTracker.ItemForm;

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

        prevFlagDestroy = false;
        Position = new TimeVector("Position", x => transform.position = x, () => transform.position, canClearFuturePosition:true);
    }

    protected virtual void UpdateShow()
    {
        gameObject.SetActive(!ItemForm && !(FlagDestroy && prevFlagDestroy));
    }

    public virtual void GameUpdate()
    {
        UpdateShow();
    }

    public virtual void SaveSnapshot(TimeDict.TimeSlice snapshotDictionary, bool force=false)
    {
        prevFlagDestroy = FlagDestroy;
        snapshotDictionary.Set(GameController.FLAG_DESTROY, FlagDestroy, force, clearFuture:true);
        snapshotDictionary.Set(nameof(ItemForm), ItemForm, force, clearFuture:true);
        UpdateShow();
        
        Position.SaveSnapshot(snapshotDictionary, force);
    }

    public virtual void PreUpdateLoadSnapshot(TimeDict.TimeSlice snapshotDictionary)
    {
        //FlagDestroy = snapshotDictionary.Get<bool>(GameController.FLAG_DESTROY);
        prevFlagDestroy = gameController.GetSnapshotValue<bool>(this, gameController.TimeStep - 1, GameController.FLAG_DESTROY);

        UpdateShow();
    }

    public virtual void ForceRestoreSnapshot(TimeDict.TimeSlice snapshotDictionary)
    {
        FlagDestroy = snapshotDictionary.Get<bool>(GameController.FLAG_DESTROY);
        prevFlagDestroy = gameController.GetSnapshotValue<bool>(this, gameController.TimeStep - 1, GameController.FLAG_DESTROY);
        ItemForm = snapshotDictionary.Get<bool>(nameof(ItemForm));
        Position.ForceLoadSnapshot(snapshotDictionary);
        Position.Current = Position.History;

        UpdateShow();
    }
}
