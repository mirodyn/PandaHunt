using System.Globalization;

namespace PandaHunt.Puzzle3Data
{
    public class PandaDaysProvider
    {


        public Dictionary<string, PandaDay> AvailableDays { get; set; }



        public string GetCurrentActivityHtml()
        {
            return GetCurrentActivityHtml(DateTime.Now.AddHours(2));
        }

        public string GetCurrentActivityHtml(DateTime time)
        {
                string dateStr = time.ToString("dd-MM-yyyy");
                if (AvailableDays.ContainsKey(dateStr))
                {
                    return AvailableDays[time.ToString("dd-MM-yyyy")].GetActivityHtml(time);
                }
                else
                {
                    return "<h1>Jdeš pozdě, panda už asi umřela :( ... </h1>";
                }
        }

        public string CurrentDayDescription
        {
            get
            {

                if (AvailableDays.ContainsKey(DateTime.Today.ToString("dd-MM-yyyy")))
                {
                    return "<div>" + AvailableDays[DateTime.Today.ToString("dd-MM-yyyy")].ToString() + "</div>";
                }
                else
                {
                    return "nějaká chyba asi...";
                }
            }
        }


        public PandaDaysProvider()
        {
            AvailableDays = new Dictionary<string, PandaDay>();

            DateTime sourceTime = DateTime.ParseExact("01-09-2023", "dd-MM-yyyy", CultureInfo.InvariantCulture);
            string prevDayKey = "none";
            for(int i = 0; i <= 100; i++)
            {
                string dateStr = sourceTime.AddDays(i).ToString("dd-MM-yyyy");
                AvailableDays.Add(dateStr, new PandaDay(GetFood(i), sourceTime.AddDays(i)));
                if (prevDayKey != "none") AvailableDays[prevDayKey].LinkDays(AvailableDays[dateStr]);
                prevDayKey = dateStr;
            }

        }
        private string GetFood(int i)
        {
            string[] foods = new string[]
            {
                "Cornflaky s cibulí",
                "Mangové pyré",
                "Vanilková zmrzlina",
                "Jahodové knedlíky",
                "Polévka gazpacho",
                "Trenčiansky párok s fazulou",
                "Smažené kohoutí hříbky",
                "Hovězí jazyk v želé",
                "Vepřová kůže na plackách",
                "Kapr v čokoládové omáčce",
                "Houskový knedlík se švestkovou omáčkou",
                "Kyselé zelí s borůvkovým pyré",
                "Jelení maso s brusinkovou omáčkou",
                "Telecí mozky na másle",
                "Mangové pyré",
                "Cornflaky s cibulí",
                "Polévka gazpacho",
                "Jahodové knedlíky",
                "Vanilková zmrzlina",
                "Hrnčířská polévka s houbami",
                "Mangové pyré",
                "Vanilková zmrzlina",
                "Jahodové knedlíky",
                "Polévka gazpacho",
                "Trenčiansky párok s fazulou",
                "Knedlíky plněné špenátem a ořechy",
                "Paprikový guláš s tvarohovými knedlíky",
                "Rybí hlava v pivním těstu",
                "Jahodová omáčka s masem",
                "Cornflaky s cibulí",
                "Mangové pyré",
                "Vanilková zmrzlina",
                "Jahodové knedlíky",
                "Polévka gazpacho",
                "Kachna s jablky a rozinkami",
                "Telecí játra s cibulí",
                "Vepřová krkovice s borůvkovou omáčkou",
                "Zvěřinový guláš",
                "Smažené houby v cibulovém těstíčku",
                "Rybí oči v pivním těstu",
                "Knedlíky plněné bramborovým pyré",
                "Pražská šunka s ostrým hořčicovým dipem",
                "Hovězí koleno na pivo",
                "Tvarohový salát s červenou řepou",
                "Krůtí srdce na pomerančové omáčce",
                "Vepřové jazyčky na hřebíčkové omáčce",
                "Telecí brzlík s křenovou omáčkou",
                "Knedlíky plněné švestkami",
                "Smažená pstruhová hlava",
                "Husí paštika s brusinkami",
                "Telecí líčka v pivo",
                "Jablečné knedlíky s ořechovou náplní",
                "Pórková polévka s kaštanovým krémem",
                "Vepřové nožky na pivní omáčce",
                "Kohoutí krky s hřebíčky",
                "Telecí maso s chřestem a houbami",
                "Svíčková na rybízové omáčce",
                "Knedlíky plněné tvarohem a šunkou",
                "Rybí oči na paprikovém džusu",
                "Houskový knedlík s čokoládovou náplní",
                "Hovězí játra s borůvkami",
                "Vepřová žebra na třezalkové omáčce",
                "Kachní prsa s meruňkovou omáčkou",
                "Telecí hřbety v bílém víně",
                "Knedlíky plněné broskvemi",
                "Lososová polévka s pomerančovou smetanou",
                "Rybí ocásky na zázvorovém džusu",
                "Telecí maso s rajčatovým krémem",
                "Houskový knedlík se švestkovou náplní",
                "Pražský šunkový dort s brusinkovým krémem",
                "Telecí játra s pomerančovou omáčkou",
                "Vepřový bok s bramborovým krémem",
                "Svíčková na třezalkovém džusu",
                "Knedlíky plněné hruškami",
                "Lososový steak s borůvkovou omáčkou",
                "Rybí ocásky na hroznovém džusu",
                "Hovězí líčka s broskvovou omáčkou",
                "Telecí maso s švestkovým krémem",
                "Houskový knedlík s meruňkovou náplní",
                "Pražský šunkový dort s hřebíčkovým krémem",
                "Kachní prsa s třezalkovou omáčkou",
                "Vepřová plec s rajčatovým krémem",
                "Knedlíky plněné malinami",
                "Kapr v rybízové omáčce",
                "Smažené holubí srdce",
                "Houskový knedlík s brusinkovou náplní",
                "Svíčková na švestkovém džusu",
                "Rybí ocásky na malinovém džusu",
                "Telecí maso s meruňkovým krémem",
                "Hovězí kotlety s borůvkovou omáčkou",
                "Pražský šunkový dort s hroznovým krémem",
                "Vepřová plec s třezalkovou omáčkou",
                "Knedlíky plněné hrozny",
                "Kapr v ostružinové omáčce",
                "Smažené holubí játra",
                "Houskový knedlík s malinovou náplní",
                "Svíčková na meruňkovém džusu",
                "Rybí ocásky na švestkovém džusu",
                "Telecí maso s hřebíčkovým krémem",
                "Hovězí kotlety s meruňkovou omáčkou"

            };
            if (i > foods.Length - 1) return "Hovno";
            return foods[i];
        }

        public  bool CheckAnswer(string answer,DateTime time)
        {
            if (string.IsNullOrEmpty(answer)) return false;
            string dateStr = time.ToString("dd-MM-yyyy");
            if (AvailableDays.ContainsKey(dateStr)) return AvailableDays[time.ToString("dd-MM-yyyy")].CheckAnswer(answer,time);

            return false;

        }
    }
}
