﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.PlayerLoop;


public class TimeDict
{
    public class TimeSlice
    {
        private TimeDict _timeDict;
        private int _timeStep;

        public TimeSlice(TimeDict source, int timeStep)
        {
            _timeDict = source;
            _timeStep = timeStep;
        }
        
        public T Get<T>(string key) => _timeDict.Get<T>(_timeStep, key);
        public void Set<T>(string key, T value, bool force=false, bool clearFuture=false) where T : IEquatable<T> => _timeDict.Set(_timeStep, key, value, force, clearFuture);
    }
    
    private Dictionary<string, IVariableTimeline> _dict = new Dictionary<string, IVariableTimeline>();

    public TimeDict()
    {
    }
    
    public TimeDict(TimeDict other)
    {
        foreach (var kvp in other._dict)
        {
            _dict[kvp.Key] = kvp.Value.Copy();
        }
    }

    public IEnumerable<string> Keys => _dict.Keys;
    
    public T Get<T>(int timeStep, string key, T defaultValue = default)
    {
        T result = defaultValue;
        if (_dict.TryGetValue(key, out var timeline))
        {
            object temp = timeline.GetRaw(timeStep);
            if (temp != null)
            {
                result = (T)temp;
            }
        }
        return result;
    }
    
    public void Set<T>(int timeStep, string key, T value, bool force=false, bool clearFuture=false) where T : IEquatable<T>
    {
        if (!_dict.TryGetValue(key, out var timeline))
        {
            timeline = _dict[key] = new VariableTimeline<T>();
        }

        timeline.SetRaw(timeStep, value, force, clearFuture);
    }

    // index a vertical slice of the time dictionary at a given timeStep
    public TimeSlice this[int timeStep] => new TimeSlice(this, timeStep);
}

public interface IVariableTimeline
{
    object GetRaw(int timeStep);
    void SetRaw(int timeStep, object value, bool force=false, bool clearFuture=false);
    IVariableTimeline Copy();
}

/// <summary>
///     This class stores the values of a variable in time
/// </summary>
public class VariableTimeline<T> : IVariableTimeline where T : IEquatable<T>
{
    private SortedList<int, T> valueHistory;
    private int maxTimeStep = -1;
    
    public VariableTimeline()
    {
        valueHistory = new SortedList<int, T>();
    }

    public VariableTimeline(VariableTimeline<T> other)
    {
        valueHistory = new SortedList<int, T>(other.valueHistory);
    }

    public T Get(int timeStep)
    {
        if (!valueHistory.TryGetValue(timeStep, out T result))
        {
            result = default;
            // timeStep key not found directly, so try to find prior value

            KeyValuePair<int, T>? priorRecord = null;
            foreach (var kvp in valueHistory)
            {
                if (kvp.Key < timeStep) // update priorRecord if Key is before target timeStep
                {
                    priorRecord = kvp;
                }
                else
                {
                    break; // passed target timeStep, so break loop
                }
            }

            if (priorRecord.HasValue)
            {
                result = priorRecord.Value.Value;
            }
        }

        return result;
    }

    public void Set(int timeStep, T value, bool force = false, bool clearFuture = false)
    {
        // only store deltas in the timeline (or force the value)
        if (force || !value.Equals(Get(timeStep)))
        {
            valueHistory[timeStep] = value;
            if (timeStep > maxTimeStep)
            {
                maxTimeStep = timeStep;
            }

            if (clearFuture) // if we need to clear all future values, remove them
            {
                for (int i = timeStep+1; i <= maxTimeStep; i++)
                {
                    valueHistory.Remove(i);
                }
            }
        }   
    }

    public T this[int timeStep]
    {
        get => Get(timeStep);
        set => Set(timeStep, value);
    }

    // implement interface for abstracting the templated type
    public object GetRaw(int timeStep) => Get(timeStep);
    public void SetRaw(int timeStep, object value, bool force=false, bool clearFuture = false) => Set(timeStep, (T)value, force, clearFuture);
    public IVariableTimeline Copy() => new VariableTimeline<T>(this);
}