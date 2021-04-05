# Time Travel Readme
This is to serve as a guide for the devs of the team to reference when implementing time travel code.

## `ICustomObject` interface
All objects that need to be tracked by the game need to implement this interface.
It stores a basic `int ID` and has methods for `Init()` and `GameUpdate()` which are called by `GameController`.
Objects that implement this directly should not have any variables stored in time
(which means these objects are expected to be stationary for the duration of the level).

- `IndestructableObject` implements this interface directly

## `ITimeTracker` interface
This is an extension of `ICustomObject` that adds in some other necessary features to track variables in time.
View `ITimeTracker.cs` which contains documentation and comments.
The basics are: it adds some methods to handle object destruction and pooling;
and methods to save/load/forceload a snapshot of time variables

Currently the following classes implement this interface:
- `BasicTimeTracker`
- `ExplodeBox` (technically derivative of `BasicTimeTracker`)
- `PlayerController`
- `TimeMachineController`

When you implement this interface, keep these in mind:
- Use the `GameUpdate()` to do logic that modifies time tracked variables
  - You can still use Unity's `Update()` for updating visuals; but you probably should use `GameUpdate()` in favor of `FixedUpdate()`
- Don't forget to save an load time variables in `SaveSnapshot()` and `ForceLoadSnapshot()` and `LoadSnapshot()`
## `BasicTimeTracker`
A `MonoBehavoir` implementation of `ITimeTracker` that adds boilerplate code for position/destruction tracking.

NOTE: I'll (Kristian) probably make other time tracker classes inherit from `BasicTimeTracker` instead of implementing `ITimeTracker` directly...
just to simplify making new classes (i.e. so you don't need to copy/paste the `Init(), ID, Position` functionalities)

## `GameController` class
This class manages all of the game state.

### Game Loop
- `LoadSnapshot()` is called on all `ITimeTracker`s
  - Objects that need to be spawned from the pool, will be spawned
  - `force=true` if we are resuming from a time travel rewind, so that the `Current` states of all objects are correct
- Physics simulation is called
- If we returned to the present, then copy TimeMachine history values to current values
- If player touching level end, then transition to that level
- `GameUpdate()` is called on all `ITimeTracker`s
- Check if player is activating a time machine or performing a time travel
- `SaveSnapshot()` is called on all `ITimeTrackers`s
  - Objects with `FlagDestroy` marked `true` are pooled or disabled depending on `bool ShouldPoolObject
- `ValidateTimeAnomalies()`
- Increment `TimeStep`
- `DoTimeTravel()` if applicable

Probably the most important thing to note is that `LoadSnapshot()` happens first, `GameUpdate()` in the middle, and `SaveSnapshot()` last.
This is most important to keep in mind when adding new time variables.
This also means that any modifications to time tracked variables should be done between the Load and Save!!! (so probably in `GameUpdate()`)
This also means the proper way to 'destroy' objects is by settings `FlagDestroy = true` and GameController will handle the rest. 


# TODO:
write about `TimeDict`, `TimeDict.TimeSlice` and `ITimeVariable` + helpful implementations like `TimeBool`