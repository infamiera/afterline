namespace Afterline.Services;

public sealed class DailyLogRolloverEventArgs : EventArgs
{
    public DateTime PreviousDate { get; }
    public DateTime NewDate { get; }
    public string PreviousLogPath { get; }
    public string NewLogPath { get; }

    public DailyLogRolloverEventArgs(
        DateTime previousDate,
        DateTime newDate,
        string previousLogPath,
        string newLogPath)
    {
        PreviousDate = previousDate.Date;
        NewDate = newDate.Date;
        PreviousLogPath = previousLogPath;
        NewLogPath = newLogPath;
    }
}
