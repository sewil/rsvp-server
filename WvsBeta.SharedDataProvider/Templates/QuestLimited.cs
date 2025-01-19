using WzTools.Objects;

public class QuestLimited
{
    public int QuestID { get; set; }
    public string QuestState { get; set; }
    public short MaxCount { get; set; }


    public QuestLimited(WzProperty prop)
    {
        QuestID = prop.GetInt32("id") ?? 0;
        QuestState = prop.GetString("state");
        MaxCount = prop.GetInt16("count") ?? 0;
    }
}