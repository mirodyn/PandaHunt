using System.Globalization;

namespace PandaHunt.Puzzle3Data
{
    public class PandaDay
    {

        public string BreakfastFood { get; set; }


        public List<PandaActivityTimeSpan> Activities { get; set; }

        public string BreakfastTime { get; set; }
        public DateTime BreakfastStart { get; set; }
        public string DinnerTime { get; internal set; }

        public PandaDay PreviousDay { get; set; }

        public PandaDay NextDay { get; set; }

        public PandaDay(string food, DateTime day)
        {
            Random rnd = new Random();
            BreakfastFood = food;
            Activities = new List<PandaActivityTimeSpan>();

            DateTime breakfastStart = day.AddHours(6).AddMinutes(rnd.Next(0, 7) * 5);
            BreakfastStart = breakfastStart;
            DateTime breakfastEnd = breakfastStart.AddMinutes(5).AddSeconds(59);

            BreakfastTime = breakfastStart.Hour.ToString().PadLeft(2, '0') + ":" + breakfastStart.Minute.ToString().PadLeft(2, '0');

            DateTime dinnerStart = day.AddHours(19).AddMinutes(rnd.Next(0, 7) * 5);
            DateTime dinnerEnd = dinnerStart.AddMinutes(5).AddSeconds(59);

            DinnerTime = dinnerStart.Hour.ToString().PadLeft(2,'0') + ":" + dinnerStart.Minute.ToString().PadLeft(2,'0');

            Activities.Add(new PandaActivityTimeSpan(day, breakfastStart.AddMinutes(-1).AddSeconds(59), PandaActivity.Night));
            Activities.Add(new PandaActivityTimeSpan(breakfastStart, breakfastEnd, PandaActivity.Breakfast));
            Activities.Add(new PandaActivityTimeSpan(breakfastEnd.AddMinutes(1).AddSeconds(-59), day.AddHours(6).AddMinutes(44).AddSeconds(59), PandaActivity.Toilet));

            Activities.Add(new PandaActivityTimeSpan(day.AddHours(6).AddMinutes(45), day.AddHours(6).AddMinutes(50).AddSeconds(59), PandaActivity.WakeUp));
            Activities.Add(new PandaActivityTimeSpan(day.AddHours(6).AddMinutes(51), day.AddHours(8).AddMinutes(44).AddSeconds(59), PandaActivity.Sleeping));

            Activities.Add(new PandaActivityTimeSpan(day.AddHours(8).AddMinutes(45), day.AddHours(8).AddMinutes(50).AddSeconds(59), PandaActivity.WakeUp));
            Activities.Add(new PandaActivityTimeSpan(day.AddHours(8).AddMinutes(51), day.AddHours(10).AddMinutes(44).AddSeconds(59), PandaActivity.Sleeping));

            Activities.Add(new PandaActivityTimeSpan(day.AddHours(10).AddMinutes(45), day.AddHours(10).AddMinutes(50).AddSeconds(59), PandaActivity.WakeUp));
            Activities.Add(new PandaActivityTimeSpan(day.AddHours(10).AddMinutes(51), day.AddHours(12).AddMinutes(44).AddSeconds(59), PandaActivity.Sleeping));

            Activities.Add(new PandaActivityTimeSpan(day.AddHours(12).AddMinutes(45), day.AddHours(12).AddMinutes(50).AddSeconds(59), PandaActivity.WakeUp));
            Activities.Add(new PandaActivityTimeSpan(day.AddHours(12).AddMinutes(51), day.AddHours(14).AddMinutes(44).AddSeconds(59), PandaActivity.Sleeping));

            Activities.Add(new PandaActivityTimeSpan(day.AddHours(14).AddMinutes(45), day.AddHours(14).AddMinutes(50).AddSeconds(59), PandaActivity.WakeUp));
            Activities.Add(new PandaActivityTimeSpan(day.AddHours(14).AddMinutes(51), day.AddHours(16).AddMinutes(44).AddSeconds(59), PandaActivity.Sleeping));

            Activities.Add(new PandaActivityTimeSpan(day.AddHours(16).AddMinutes(45), day.AddHours(16).AddMinutes(50).AddSeconds(59), PandaActivity.WakeUp));
            Activities.Add(new PandaActivityTimeSpan(day.AddHours(16).AddMinutes(51), day.AddHours(18).AddMinutes(44).AddSeconds(59), PandaActivity.Sleeping));

            Activities.Add(new PandaActivityTimeSpan(day.AddHours(18).AddMinutes(45), day.AddHours(18).AddMinutes(50).AddSeconds(59), PandaActivity.WakeUp));

            Activities.Add(new PandaActivityTimeSpan(day.AddHours(16).AddMinutes(56), dinnerStart.AddMinutes(-1).AddSeconds(59), PandaActivity.Playing));
            Activities.Add(new PandaActivityTimeSpan(dinnerStart, dinnerEnd, PandaActivity.Dinner));

            Activities.Add(new PandaActivityTimeSpan(dinnerEnd.AddSeconds(-59), day.AddHours(23).AddMinutes(59).AddSeconds(59), PandaActivity.Night));


            Activities.OrderBy(x => x.Start);

        }

        public void LinkDays(PandaDay nextDay)
        {
            NextDay = nextDay;
            NextDay.PreviousDay = this;
        }

        public override string ToString()
        {
            string output = "";
            output += "-----------------------------------------\n";
            output += "breakfast: " + BreakfastFood + "\n"; 
            foreach (PandaActivityTimeSpan a in Activities)
            {
                output += a.ToString() + "\n"; 
            }
            output += "-----------------------------------------\n";
            return output;
        }


        public string GetActivityHtml()
        {
            return GetActivityHtml(DateTime.Now.AddHours(2));
        }

        public string GetActivityHtml(DateTime currentTime)
        {
            DateTime adjustedTime = currentTime.AddSeconds(currentTime.Second * -1);
            foreach(PandaActivityTimeSpan a in Activities)
            {

                if (adjustedTime >= a.Start && adjustedTime <= a.End) return a.GetHtml(this,adjustedTime);
            }
            return "<h1>Chybička se vloudila ... Zkus to později.</h1>";
        }

        public bool CheckAnswer(string answer,DateTime time)
        {
            if (time >= BreakfastStart)
            {
                return answer.ToLower().Trim() == BreakfastFood.ToLower().Trim();
            }
            else
            {
                return answer.ToLower().Trim() == PreviousDay.BreakfastFood.ToLower().Trim(); 
            }
        }

    }


    public enum PandaActivity
    {
        Breakfast,
        Toilet,
        WakeUp,
        Sleeping,
        Playing,
        Dinner,
        Night
    }


}
