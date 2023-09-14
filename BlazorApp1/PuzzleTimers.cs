using PandaHunt.Pages;

internal class PuzzleTimers
{

    public bool PuzzleAOpened => DateTime.Now.AddHours(2) > PuzzleATime;
    public bool PuzzleBOpened => DateTime.Now.AddHours(2) > PuzzleBTime;
    public bool PuzzleCOpened => DateTime.Now.AddHours(2) > PuzzleCTime;
    public bool PuzzleDOpened => DateTime.Now.AddHours(2) > PuzzleDTime;


    public DateTime PuzzleATime = new DateTime(2023, 9, 13).AddHours(12);
    public DateTime PuzzleBTime = new DateTime(2023, 9, 17).AddHours(12);
    public DateTime PuzzleCTime = new DateTime(2023, 9, 20).AddHours(12);
    public DateTime PuzzleDTime = new DateTime(2023, 9, 24).AddHours(12);

    //public DateTime PuzzleATime = new DateTime(2023, 8, 13).AddHours(12);
    //public DateTime PuzzleBTime = new DateTime(2023, 8, 17).AddHours(12);
    //public DateTime PuzzleCTime = new DateTime(2023, 8, 20).AddHours(12);
    //public DateTime PuzzleDTime = new DateTime(2023, 8, 24).AddHours(12);

}