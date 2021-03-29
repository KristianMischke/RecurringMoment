using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public interface ITimeTracker
{
    int ID { get; }
    TimePosition Position { get; }
    bool FlagDestroy { get; set; }
    void Init(GameController gameController, int id);

    bool SetItemState(bool state);
    
    void SaveSnapshot(TimeDict.TimeSlice snapshotDictionary);
    void LoadSnapshot(TimeDict.TimeSlice snapshotDictionary);
    void ForceLoadSnapshot(TimeDict.TimeSlice snapshotDictionary);
}
