using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IndestructableObject : MonoBehaviour, ICustomObject
{
    public GameController gameController;
    public int ID { get; private set; }

    public void Init(GameController gameController, int id)
    {
        this.gameController = gameController;
        this.ID = id;
    }

    public void GameUpdate() { }
}
