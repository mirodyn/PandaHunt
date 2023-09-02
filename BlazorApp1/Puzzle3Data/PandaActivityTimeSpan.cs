using Microsoft.AspNetCore.Components;
using System.Diagnostics.Eventing.Reader;
using System.Reflection;

namespace PandaHunt.Puzzle3Data
{
    public class PandaActivityTimeSpan
    {

        public DateTime Start { get; set; }
        public DateTime End { get; set; }

        public PandaActivity Activity { get; set; }



        public PandaActivityTimeSpan(DateTime start, DateTime end, PandaActivity activity)
        {
            Start = start;
            End = end;
            Activity = activity;
        }

        public override string ToString()
        {
            return "From " + Start.ToString() + " to " + End.ToString() + " -> " + Activity.ToString() ;
        }





        private string wakeUpSvg = "";
        private string sleepingSvg = "";
        private string breafastSvg = "";
        private string playingSvg = "";
        private string toiletSvg = "";
        private string dinnerSvg = "";


        public string GetHtml(PandaDay day, DateTime time)
        {
            if (time == null) time = DateTime.Now.AddHours(2);
            string html = "###img###<div class=\"panda-text\">###txt###</div>";
            switch (Activity) 
            {
                case PandaActivity.WakeUp:
                    html = html.Replace("###img###", ReadSvg("wakeup.svg"));
                    html = html.Replace("###txt###", "Už se těším na večeři. Dneska bude v " + day.DinnerTime);
                    return html;
                case PandaActivity.Sleeping:
                    html = html.Replace("###img###", ReadSvg("sleeping.svg"));
                    switch ((int)time.Subtract(Start).TotalMinutes) 
                    {
                        case 0:
                            html = html.Replace("###txt###", "Ještě neodpočívám ani minutu.");
                            break;
                        case 1:
                            html = html.Replace("###txt###", "Odpočívám minutu a pár vteřin.");
                            break;
                        case 2:
                        case 3:
                        case 4:
                            html = html.Replace("###txt###", "Odpočívám " + ((int)time.Subtract(Start).TotalMinutes).ToString() + " minuty a pár vteřin.");
                            break;
                        default:
                            html = html.Replace("###txt###", "Odpočívám " + ((int)time.Subtract(Start).TotalMinutes).ToString() + " minut a pár vteřin.");
                            break;

                    }

                    return html;
                case PandaActivity.Night:
                    html = html.Replace("###img###", ReadSvg("sleeping.svg"));
                    html = html.Replace("###txt###", "Neruš, spím až do rána...");
                    return html;
                case PandaActivity.Toilet:
                    html = html.Replace("###img###", ReadSvg("toilet.svg"));
                    html = html.Replace("###txt###", "Ale no tak, trochu soukromí...");
                    return html;
                case PandaActivity.Playing:
                    html = html.Replace("###img###", ReadSvg("playing.svg"));
                    html = html.Replace("###txt###", "Nemůžu, pařím!");
                    return html;
                case PandaActivity.Dinner:
                    html = html.Replace("###img###", ReadSvg("dinner.svg"));
                    html = html.Replace("###txt###", "Večeřím. Zase bambus. Ale už se nemůžu dočkat snídaně. Mňam. <br/> Bude v " + day.NextDay?.BreakfastTime);
                    return html;
                case PandaActivity.Breakfast:
                    html = html.Replace("###img###", ReadSvg("breakfast.svg"));
                    html = html.Replace("###txt###", "ÁÁÁ SNÍDANĚ! <br/><br/><br/> <span id=\"breakfast-text\">" + day.BreakfastFood + " </span><br/>Včera bylo: " + day.PreviousDay.BreakfastFood + "</br>Zítra bude: " + day.NextDay.BreakfastFood);
                    return html;
            }
            return "??????";
        }

        private string ReadSvg(string filename)
        {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = "PandaHunt.Puzzle3Data." + filename;

        using (Stream stream = assembly.GetManifestResourceStream(resourceName))
        using (StreamReader reader = new StreamReader(stream))
        {
            return reader.ReadToEnd();
        }
}
    }
}
