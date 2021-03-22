using System.Collections.Generic;

public class TimeVariable<T>
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

    public virtual void SaveSnapshot(Dictionary<string, object> snapshotDictionary)
    {
        // NOTE: behavior for how current/history are stored is ill-defined for templated type T
        snapshotDictionary[CurrentName] = Current;
        
        snapshotDictionary[HistoryName] = History;
    }
    
    public virtual void LoadSnapshot(Dictionary<string, object> snapshotDictionary)
    {
        History = (T)snapshotDictionary[HistoryName];
    }
    
    public virtual void ForceLoadSnapshot(Dictionary<string, object> snapshotDictionary)
    {
        Current = (T)snapshotDictionary[CurrentName];
        
        History = (T)snapshotDictionary[HistoryName];
    }
}

public class TimeBool : TimeVariable<bool>
{
    public TimeBool(string name, bool historyDefault = false, bool currentDefault = false) : base(name, historyDefault, currentDefault) { }

    public bool AnyTrue => Current || History;
    
    public override void SaveSnapshot(Dictionary<string, object> snapshotDictionary)
    {
        snapshotDictionary[CurrentName] = Current;
        
        snapshotDictionary[HistoryName] = History || Current;
    }
}

public class TimeInt : TimeVariable<int>
{
    public TimeInt(string name, int historyDefault = -1, int currentDefault = -1) : base(name, historyDefault, currentDefault) { }
    
    public override void SaveSnapshot(Dictionary<string, object> snapshotDictionary)
    {
        snapshotDictionary[CurrentName] = Current;
        
        snapshotDictionary[HistoryName] = Current == -1 ? History : Current;
    }
}