using log4net;

namespace WvsBeta.Game
{
    public class MessageBox : IFieldObj
    {
        private static ILog _log = LogManager.GetLogger("MessageBox");

        public int SN { get; set; }
        public string Creator { get; set; }
        public Map Field { get; set; }
        public int ItemID { get; set; }
        public string Message { get; set; }
        public short X { get; set; }
        public short Y { get; set; }
        
        public long CreateTime { get; private set; }

        public MessageBox(Character owner, int itemID, string message, Map field)
        {
            Creator = owner.Name;
            ItemID = itemID;
            Message = message;
            Field = field;
            X = owner.Position.X;
            Y = owner.Position.Y;
            
            Field.MessageBoxes.Add(this);
            SN = Field.SetBalloon(owner.Position, Map.BalloonType.MessageBox);
            _log.Info($"Opened MessageBox at {X} {Y} on {Field.ID}, SN {SN}, by {Creator}, itemID {ItemID}, message: {Message}");

            CreateTime = MasterThread.CurrentTime;
        }

        public bool IsShownTo(IFieldObj Object) => true;

        public void Spawn()
        {
            MapPacket.SpawnMessageBox(this);
        }

        public void Remove()
        {
            _log.Info($"Removing MessageBox on {Field.ID}, SN {SN}, by {Creator}");
            MapPacket.DespawnMessageBox(this, 0);
            Field.MessageBoxes.Remove(this);
            Field.RemoveBalloon(SN);
        }
    }
}
