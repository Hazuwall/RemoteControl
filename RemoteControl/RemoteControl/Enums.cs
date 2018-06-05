namespace RemoteControl
{
    public enum Command
    {
        None = 0,
        Stop = 1,
        Backward = 2,
        Forward = 3,
        LeftTurn = 4,
        RightTurn = 5,
        Autopilot = 6,
        CheckConnection = 7,
        Authorize = 8,
        GetIP = 9,
        GetSSID = 10
    }
    public enum Response
    {
        Fail = 1,
        SSIDNotFound = 2,
        IncorrectPassword = 3,
        TimeOut = 4,
        Success = 5,
        State = 6
    }
}
