using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameController : MonoBehaviour
{
    public List<TimeMachineController> timeMachines = new List<TimeMachineController>();
    public PlayerController player;

    public List<PlayerController> pastPlayers;

    void Start()
    {
        //TODO: assert player not null
        player.SetGameController(this);
    }

    void Update()
    {
        if (player.IsActivating)
        {
            foreach (var timeMachine in timeMachines)
            {
                if (timeMachine.IsTouching(player.gameObject))
                {
                    //TODO: time travel
                }
            }
        }
    }
}
