using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

interface ITimeTracker
{
    int ID { get; }
    void Init(GameController gameController, int id);

    void SaveSnapshot(Dictionary<string, object> snapshotDictionary);
    void LoadSnapshot(Dictionary<string, object> snapshotDictionary);
}
