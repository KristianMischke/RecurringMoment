using System;
using System.Collections;
using System.Collections.Generic;


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

        public object this[string key]
        {
            get => _timeDict.GetValue<object>(_timeStep, key);
            set => _timeDict.SetValue(_timeStep, key, value);
        }

        public T GetValue<T>(string key) => _timeDict.GetValue<T>(_timeStep, key);
    }
    
    private Dictionary<string, VariableTimeline<object>> _dict = new Dictionary<string, VariableTimeline<object>>();

    public TimeDict()
    {
    }
    
    public TimeDict(TimeDict other)
    {
        foreach (var kvp in other._dict)
        {
            _dict[kvp.Key] = new VariableTimeline<object>(kvp.Value);
        }
    }

    public IEnumerable<string> Keys => _dict.Keys;
    
    public T GetValue<T>(int timeStep, string key, T defaultValue = default)
    {
        T result = defaultValue;
        if (_dict.TryGetValue(key, out var timeline))
        {
            object temp = timeline[timeStep];
            if (temp != null)
            {
                result = (T)temp;
            }
        }
        return result;
    }
    
    public void SetValue(int timeStep, string key, object value)
    {
        if (!_dict.TryGetValue(key, out var timeline))
        {
            timeline = _dict[key] = new VariableTimeline<object>();
        }

        timeline[timeStep] = value;
    }

    // index a vertical slice of the time dictionary at a given timeStep
    public TimeSlice this[int timeStep] => new TimeSlice(this, timeStep);
}

/// <summary>
///     This class stores the values of a variable in time
/// </summary>
public class VariableTimeline<T> //where T : IEquatable<T>
{
    private SortedList<int, T> valueHistory;
    
    public VariableTimeline()
    {
        valueHistory = new SortedList<int, T>();
    }

    public VariableTimeline(VariableTimeline<T> other)
    {
        valueHistory = new SortedList<int, T>(other.valueHistory);
    }

    public T GetValue(int timeStep)
    {
        if (!valueHistory.TryGetValue(timeStep, out T result))
        {
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

    public void SetValue(int timeStep, T value)
    {
        // only store deltas in the timeline
        if (!value.Equals(GetValue(timeStep)))
        {
            valueHistory[timeStep] = value;
        }   
    }

    public T this[int timeStep]
    {
        get => GetValue(timeStep);
        set => SetValue(timeStep, value);
    }
}