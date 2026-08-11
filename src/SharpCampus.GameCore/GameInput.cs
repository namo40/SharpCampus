namespace SharpCampus.GameCore;

public enum GameInput : byte
{
    MoveLeft = 0,
    MoveRight = 1,
    RotateCw = 2,
    RotateCcw = 3,
    SoftDropOn = 4,
    SoftDropOff = 5,
    HardDrop = 6,
    Hold = 7,
}
