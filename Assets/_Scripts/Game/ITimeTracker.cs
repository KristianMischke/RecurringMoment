using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
///     <para>Custom interface that all objects the Recurring Moment project needs to reference via code.</para>
///     <para>This is required if a <see cref="ITimeTracker"/> object needs a reference to an object not tracked in time</para>
///     <para><see cref="IndestructableObject"/> implement this interface directly because they don't need to record timely data</para>
/// </summary>
public interface ICustomObject
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
}

/// <summary>
///     Interface designed fop objects that track information in time (e.g. custom variables, or destroy state)
/// </summary>
public interface ITimeTracker : ICustomObject
{
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
    ///     Method to assign the item state of this object
    /// </summary>
    /// <param name="state">If true, will attempt to turn object into an item. If false will attempt to turn item
    ///                     into an object</param>
    /// <returns>True if operation was successful</returns>
    bool SetItemState(bool state); //TODO: rename to TrySetItemState?

    /// <summary>
    ///     Retrieves a sprite to show in the player's item slot
    /// </summary>
    /// <returns></returns>
    void GetItemSpriteProperties(out Sprite sprite, out Color color);
    
    /// <summary>
    ///     Determines if the other time tracker is equivalent to this one for the purposes of whether or not a player
    ///     can pick it up.
    /// </summary>
    /// <returns>True if equivalent</returns>
    bool IsEquivalentItem(ITimeTracker other);

    /// <summary>
    ///     Copy the state from another ITimeTracker object to this object
    /// </summary>
    /// <param name="other">the object to copy from</param>
    void CopyTimeTrackerState(ITimeTracker other);

    /// <summary>
    ///     Execute a past event as given by the <see cref="GameController"/>
    /// </summary>
    /// <param name="timeEvent">The event this object should execute</param>
    void ExecutePastEvent(TimeEvent timeEvent);
    
    /// <summary>
    ///     Method called by <see cref="GameController"/> to save a snapshot of this objects time-tracked variables
    ///     to the history.
    /// </summary>
    /// <param name="snapshotDictionary"><see cref="TimeDict.TimeSlice"/>dictionary for storing the values</param>
    /// <param name="force">Force all values to be written to the history, even if they haven't changed</param>
    void SaveSnapshot(TimeDict.TimeSlice snapshotDictionary, bool force=false);
    /// <summary>
    ///     Method called by <see cref="GameController"/> to load a snapshot of this objects time-tracked variables into
    ///     the current state. This method doesn't necessarily need to restore the current state (although sometimes it
    ///     needs to); it does however need to restore any history states.
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
