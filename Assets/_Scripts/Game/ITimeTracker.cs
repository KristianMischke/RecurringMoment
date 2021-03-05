using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public interface ITimeTracker
{
    int ID { get; }
    bool ItemForm { get; set; }
    UnityEngine.Vector2 Position { get; set; }
    bool FlagDestroy { get; set; }
    void Init(GameController gameController, int id);

    void SaveSnapshot(Dictionary<string, object> snapshotDictionary);
    void LoadSnapshot(Dictionary<string, object> snapshotDictionary);
}
