using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public interface ITimeTracker
{
    GameObject gameObject { get; }
    int ID { get; }
    TimeVector Position { get; }
    bool FlagDestroy { get; set; }
    void Init(GameController gameController, int id);

    bool SetItemState(bool state);
    
    void SaveSnapshot(TimeDict.TimeSlice snapshotDictionary, bool force=false);
    void LoadSnapshot(TimeDict.TimeSlice snapshotDictionary);
    void ForceLoadSnapshot(TimeDict.TimeSlice snapshotDictionary);
}
