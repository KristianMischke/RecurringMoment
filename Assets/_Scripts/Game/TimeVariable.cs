using System;
using System.Collections.Generic;
using UnityEngine;

public class TimeVariable<T> where T : IEquatable<T>
{
    public virtual T History { get; set; }
    public virtual T Current { get; set; }
    public virtual string CurrentName { get; protected set; }
    public virtual string HistoryName { get; protected set; }

    public TimeVariable(string name, T historyDefault = default, T currentDefault = default)
    {
        CurrentName = $"current_{name}";
        HistoryName = $"history_{name}";

        History = historyDefault;
        Current = currentDefault;
    }

    public void Copy(TimeVariable<T> other, bool copyName=false)
    {
        if (copyName)
        {
            CurrentName = other.CurrentName;
            HistoryName = other.HistoryName;
        }

        History = other.History;
        Current = other.Current;
    }
    
    public virtual void SaveSnapshot(TimeDict.TimeSlice snapshotDictionary, bool force=false)
    {
        // NOTE: behavior for how current/history are stored is ill-defined for templated type T
        snapshotDictionary.Set(CurrentName, Current, force);
        
        snapshotDictionary.Set(HistoryName, History, force);
    }
    
    public virtual void LoadSnapshot(TimeDict.TimeSlice snapshotDictionary)
    {
        History = snapshotDictionary.Get<T>(HistoryName);
    }
    
    public virtual void ForceLoadSnapshot(TimeDict.TimeSlice snapshotDictionary)
    {
        Current = snapshotDictionary.Get<T>(CurrentName);
        
        History = snapshotDictionary.Get<T>(HistoryName);
    }
}

public class TimeBool : TimeVariable<bool>
{
    public TimeBool(string name, bool historyDefault = false, bool currentDefault = false) : base(name, historyDefault, currentDefault) { }

    public bool AnyTrue => Current || History;
    
    public override void SaveSnapshot(TimeDict.TimeSlice snapshotDictionary, bool force=false)
    {
        snapshotDictionary.Set(CurrentName, Current, force);
        
        snapshotDictionary.Set(HistoryName, History || Current, force);
    }
}

public class TimeInt : TimeVariable<int>
{
    public TimeInt(string name, int historyDefault = -1, int currentDefault = -1) : base(name, historyDefault, currentDefault) { }
    
    public override void SaveSnapshot(TimeDict.TimeSlice snapshotDictionary, bool force=false)
    {
        snapshotDictionary.Set(CurrentName, Current, force);
        
        snapshotDictionary.Set(HistoryName, Current == -1 ? History : Current, force);
    }
}

public class TimeVector : TimeVariable<Vector2>
{
    private readonly Action<Vector2> _setter;
    private readonly Func<Vector2> _getter;

    private bool _canClearFuturePosition;
    
    private Vector2 _current;

    public TimeVector(string name, Action<Vector2> setter = null, Func<Vector2> getter = null, bool canClearFuturePosition = false) : base(name)
    {
        _setter = setter;
        _getter = getter;
        _canClearFuturePosition = canClearFuturePosition;
    }

    public override Vector2 Current
    {
        get
        {
            _current = _getter?.Invoke() ?? _current;
            return _current;
        }
        set
        {
            _current = value;
            _setter?.Invoke(value);
        }
    }

    public Vector2 Get
    {
        get
        {
            Vector2 temp = Current;
            return temp == Vector2.negativeInfinity ? History : temp;
        }
    }

    public void ClearCurrent() => Current = Vector2.negativeInfinity;
    
    public override void SaveSnapshot(TimeDict.TimeSlice snapshotDictionary, bool force=false)
    {
        bool clearPositionFuture = false;
        if (_canClearFuturePosition)
        {
            clearPositionFuture = Vector2.Distance(Current, History) >
                                  GameController.POSITION_CLEAR_FUTURE_THRESHOLD;
        }
        
        Vector2 temp = Current;
        snapshotDictionary.Set(CurrentName, temp, force, clearPositionFuture);
        
        snapshotDictionary.Set(HistoryName, temp == Vector2.negativeInfinity ? History : temp, force, clearPositionFuture);
    }
}