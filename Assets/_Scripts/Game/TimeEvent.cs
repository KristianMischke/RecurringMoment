

public struct TimeEvent
{
    public enum EventType
    {
        NONE = -1,
        
        PLAYER_GRAB,
        PLAYER_DROP,
        EXPLODE,
        GUARD_SHOOT,
        
        COUNT
    }

    public EventType Type;
    public int SourceID;
    public int TargetID;
    public string OtherData;

    public TimeEvent(int sourceID, EventType type, int targetID, string otherData = null)
    {
        SourceID = sourceID;
        Type = type;
        TargetID = targetID;
        OtherData = otherData;
    }
}
