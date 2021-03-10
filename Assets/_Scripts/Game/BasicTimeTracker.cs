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

    public Vector2 Position
    {
        get => transform.position;
        set => transform.position = value;
    }

    private bool _itemForm = false;
    public bool ItemForm
    {
        get => _itemForm;
        set
        {
            _itemForm = value;
            gameObject.SetActive(!_itemForm);
        }

    }
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
    
    void FixedUpdate()
    {
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
    }

    public void SaveSnapshot(Dictionary<string, object> snapshotDictionary)
    {
        if (FlagDestroy)
        {
            snapshotDictionary[GameController.FLAG_DESTROY] = true;
        }

        snapshotDictionary[nameof(ItemForm)] = ItemForm;
        snapshotDictionary[nameof(Position)] = Position;
    }

    public void LoadSnapshot(Dictionary<string, object> snapshotDictionary)
    {
        if (!ItemForm) //TODO: need better way to handle the variables that can be "broken" in the past... i.e. things that are not set in stone
        {
            ItemForm = (bool) snapshotDictionary[nameof(ItemForm)];
        }
        if (!touchedPlayer) //TODO: if object is bumped by something (not just the player) unexpected in the past, should ignore loading positions
        {
            Position = (Vector2) snapshotDictionary[nameof(Position)];
        }
    }

    public void ForceLoadSnapshot(Dictionary<string, object> snapshotDictionary) => LoadSnapshot(snapshotDictionary);
}
