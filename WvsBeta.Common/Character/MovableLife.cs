using System;

namespace WvsBeta.Common
{
    public enum MoveActionType
    {
        None = 0,
        Walk = 1,
        Move = 1,
        Stand = 2,
        Jump = 3,
        Alert = 4,
        Prone = 5,
        Fly = 6,
        Ladder = 7,
        Rope = 8,
        Dead = 9,
        Sit = 10,
        Stand0 = 11,
        Hungry = 12,
        Rest0 = 13,
        Rest1 = 14,
        Hang = 15,
        Chase = 16,
    }
    public class MovableLife
    {
        public byte MoveAction { get; set; }
        public short Foothold { get; set; }
        public Pos Position { get; set; }
        public Pos Wobble { get; set; }
        public byte Jumps { get; set; }
        public long LastMove { get; set; }

        public long MovePathTimeSumLastCheck { get; set; }
        public long MovePathTimeSum { get; set; }
        public long MovePathTimeHackCountLastReset { get; set; }
        public int MovePathTimeHackCount { get; set; }

        public MoveActionType MoveActionType => (MoveActionType)(MoveAction >> 1);

        public void SetMoveActionType(MoveActionType mat, bool? left = null)
        {
            left ??= IsFacingLeft();
            MoveAction = (byte)((byte)mat << 1 | (left.Value ? 1 : 0));
        }

        public MovableLife()
        {
            MoveAction = 0;
            Foothold = 0;
            Position = new Pos();
            Wobble = new Pos(0, 0);
        }
        public MovableLife(MovableLife baseML)
        {
            MoveAction = baseML.MoveAction;
            Foothold = baseML.Foothold;
            Position = new Pos(baseML.Position);
            Wobble = new Pos(baseML.Wobble);
        }

        public MovableLife(short pFH, Pos pPosition, byte pMoveAction)
        {
            MoveAction = pMoveAction;
            Position = new Pos(pPosition);
            Foothold = pFH;
            Wobble = new Pos(0, 0);
            MovePathTimeHackCountLastReset =
                MovePathTimeSumLastCheck =
                    LastMove = MasterThread.CurrentTime;
        }

        public bool IsFacingRight() => (int)MoveAction % 2 == 0;
        public bool IsFacingLeft() => (byte)MoveAction % 2 == 1;
    }

}
