using PandaHunt.Pages;

internal class PuzzleTimers
{

    public bool PuzzleAOpened => DateTime.Now > PuzzleATime;
    public bool PuzzleBOpened => DateTime.Now > PuzzleBTime;
    public bool PuzzleCOpened => DateTime.Now > PuzzleCTime;
    public bool PuzzleDOpened => DateTime.Now > PuzzleDTime;


    public DateTime PuzzleATime = new DateTime(2023, 9, 1).AddHours(16);
    public DateTime PuzzleBTime = new DateTime(2023, 8, 1).AddHours(16);
    public DateTime PuzzleCTime = new DateTime(2023, 9, 1).AddHours(16);
    public DateTime PuzzleDTime = new DateTime(2023, 9, 1).AddHours(16);

}