﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public interface ITimeTracker
{
    /// <summary>
    ///     The game object of this <see cref="ITimeTracker"/>
    /// </summary>
    GameObject gameObject { get; }
    
    /// <summary>
    ///     The unique ID representing this <see cref="ITimeTracker"/>
    /// </summary>
    int ID { get; }
    /// <summary>
    ///     The Vector of this gameObject, to be tracked in time
    /// </summary>
    TimeVector Position { get; }
    /// <summary>
    ///     Flag that, when assigned to true, will properly destroy this gameObject in time 
    /// </summary>
    bool FlagDestroy { get; set; }
    /// <summary>
    ///     Bool that, when assigned to true, will put destroyed objects in a pool to be reclaimed later 
    /// </summary>
    bool ShouldPoolObject { get; }
    
    /// <summary>
    ///     Method called in <see cref="GameController"/> when this item is instantiated from the pool
    /// </summary>
    void OnPoolInstantiate();
    /// <summary>
    ///     Method called in <see cref="GameController"/> when this item is acquired from the pool
    /// </summary>
    void OnPoolInit();
    /// <summary>
    ///     Method called in <see cref="GameController"/> when this item is released to the pool
    /// </summary>
    void OnPoolRelease();
    
    /// <summary>
    ///     Method called in <see cref="GameController"/> to initialize this object with an ID and pass it the
    ///     gameController reference
    /// </summary>
    /// <param name="gameController"></param>
    /// <param name="id"></param>
    void Init(GameController gameController, int id);
    /// <summary>
    ///     Method called in <see cref="GameController"/> during every TimeStep.
    ///     Use this method instead of Unity's Update() or FixedUpdate() functions if you are modifying TimeVariables
    ///     Use Update() or FixedUpdate() for visual information or ReadOnly access of TimeVariables
    /// </summary>
    void GameUpdate();

    /// <summary>
    ///     Method to assign the item state of this object
    /// </summary>
    /// <param name="state">If true, will attempt to turn object into an item. If false will attempt to turn item
    ///                     into an object</param>
    /// <returns>True if operation was successful</returns>
    bool SetItemState(bool state); //TODO: rename to TrySetItemState?
    
    /// <summary>
    ///     Method called by <see cref="GameController"/> to save a snapshot of this objects time-tracked variables
    ///     to the history.
    /// </summary>
    /// <param name="snapshotDictionary"><see cref="TimeDict.TimeSlice"/>dictionary for storing the values</param>
    /// <param name="force">Force all values to be written to the history, even if they haven't changed</param>
    void SaveSnapshot(TimeDict.TimeSlice snapshotDictionary, bool force=false);
    /// <summary>
    ///     Method called by <see cref="GameController"/> to load a snapshot of this objects time-tracked variables into
    ///     the current state.
    /// </summary>
    /// <param name="snapshotDictionary"><see cref="TimeDict.TimeSlice"/>dictionary for storing the values</param>
    void LoadSnapshot(TimeDict.TimeSlice snapshotDictionary);
    /// <summary>
    ///     Same as <see cref="LoadSnapshot"/>, except forces current values to be updated as well (typically used in
    ///     <see cref="GameController"/> when resetting the state after time travelling or loading from a spawn point.
    /// </summary>
    /// <param name="snapshotDictionary"><see cref="TimeDict.TimeSlice"/>dictionary for storing the values</param>
    void ForceLoadSnapshot(TimeDict.TimeSlice snapshotDictionary);
}
