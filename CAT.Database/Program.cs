using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using CAT.Model;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;

namespace CAT.Database
{
    class Program
    {
        private static string _dir;
        private static string _xmldir;
        private static List<Tick> _ticks { get; set; }
        private static CloudTableClient TableClient
        {
            get 
            {
                return CloudStorageAccount.Parse("DefaultEndpointsProtocol=https;" +
                    "AccountName=catarino;" +
                    "AccountKey=pQFN//7LQ5L0ki0AyUKcwTBUz9i9F07zoX5iFezs9Q4Fm4mm/WsF8MmKmOwZVmNdOpOGr7NZnJs5Jy4ZX6RmsQ==")
                    .CreateCloudTableClient();
            }
        }
        
        static void Main(string[] args)
        {
            _ticks = new List<Tick>();
            _dir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\CAT\Data\";
            _xmldir = @"C:\Users\Alexandre\Documents\Data\options\";

            var id = Guid.NewGuid().ToString();

            var table = TableClient.GetTableReference("Clients");

            //if (table.CreateIfNotExists()) Console.WriteLine("Clients table created!");

            char key;
            do
            {
                key = Console.ReadKey().KeyChar;

                if (key == '1')
                {
                    GetData("PETR");
                }
                
                if (key == '2' || key == '3') 
                {
                    Console.WriteLine();
                    var symbol = key == '2' ? "PETR" : "VALE";
                    GetXML(symbol);
                    //GetData(symbol);
                }
                if (key == '4')
                {
                    Console.WriteLine();
                    GetXML("USIM5");
                }

            } while (key != '0');
        }

        private static void GetData(string symbol)
        {
            var files = Directory.GetFiles(_dir, symbol + "*");

            foreach (var file in files)
            {
                var info = new FileInfo(file);
                if (!info.Exists)
                {
                    Console.WriteLine(file + " não existe.");
                    continue;
                }
                var buffer = new byte[info.Length];
                if (buffer.Length < 10000) continue;

                using (var asciifile = File.OpenRead(file))
                {
                    asciifile.Read(buffer, 0, buffer.Length);
                    var lines = System.Text.Encoding.UTF8.GetString(buffer).Split('\n').ToList();
                    lines.RemoveAll(l => l.Length == 0);
                    var last = lines.Last();
                    var eod = last.Contains(":") ? 3 : 2;

                    foreach (var line in lines)
                    {
                        var columns = line.Split(';');

                        try
                        {
                            var values = new List<decimal>();
                            for (var i = 0; i < 4; i++) values.Add(decimal.Parse(columns[eod + i]));
                            
                            values = values.Distinct().ToList();
                            if (values.Count == 1) values.Add(values.First());

                            var add = eod > 2 ? new List<double>() { 0, 59 } : new List<double>() { 10, 16 + 55 / 60 };
                            if (values.Count == 3) if (eod > 2) add.Add(30); else add.Add(13);
                            if (values.Count == 4) if (eod > 2) add.AddRange(new List<double>() { 20, 40 }); else add.AddRange(new List<double>() { 12, 14 });

                            if (values.Count == 4 && values.Last() > values.First())
                            {
                                var tmp = values[1];
                                values[1] = values[2];
                                values[2] = tmp;
                            }

                            var lineticks = new List<Tick>();
                            var time = DateTime.Parse(columns[1]);
                            if (eod == 3) time += TimeSpan.Parse(columns[2]);

                            for (var i = 0; i < values.Count; i++)
                            {
                                var tick = new Tick { Symbol = columns[0].Trim(), Value = values[i], Qnty = 0 };
                                tick.Time = eod == 2 ? time.AddHours(add[i]) : time.AddSeconds(add[i]);
                                lineticks.Add(tick);
                            }

                            _ticks.AddRange(lineticks);
                        }
                        catch (Exception e) 
                        {
                            Console.WriteLine(file);
                            Console.WriteLine(e.Message);
                        };
                    }
                }
            }
            _ticks = _ticks.OrderBy(t => t.Time).ToList();
        }

        private static void GetXML(string symbol)
        {
            var files = Directory.GetFiles(_xmldir, "NEG*.xml");
            
            foreach (var file in files)
            {
                var year = file.Substring(file.Length - 12, 4);
                //if (int.Parse(year) < 2015) continue;
                var tmp = Deserialize(file);
                Console.WriteLine(file.Replace(_xmldir, "Eliminated from ") + ": " + tmp.RemoveAll(t => t.Symbol.Substring(0, 4) != symbol));
                Serialize(symbol, file, Datafilter(tmp));
            }

        }

        private static List<Tick> Datafilter(List<Tick> ticks)
        {
            return ticks; // No Filter

            if (ticks.Count < 1) return ticks;
            if (ticks.First().Symbol == "USIM5") return ticks;
            
            var today = ticks.First().Time.Date;

            var exp = new List<DateTime>();
            
            while (today < ticks.Last().Time.Date.AddMonths(1))
            {
                if (today.DayOfWeek == DayOfWeek.Monday && today.Day > 14 && today.Day < 22) exp.Add(today);
                today = today.AddDays(1);
            }

            Console.WriteLine("Filtered " + ticks.RemoveAll(t =>
            {
                // Keep quotes for today's trades
                if (t.Time > DateTime.Today) return false;

                exp.RemoveAll(d => d <= t.Time);

                if (exp.Count < 1) 
                    return true;

                var tmp = exp.First();

                if (tmp.Month != (int)t.Symbol[4] - 64) return true;

                return false; // t.Time < tmp.AddDays(1 - tmp.Day);
            }) + " ticks");

            ticks.TrimExcess();
            return ticks;
        }

        private static void Serialize(string symbol, string file, List<Tick> ticks)
        {
            if (ticks.Count < 1) return;

            var newfile = file.Replace("NEG", symbol);
            using (var tw = new StreamWriter(newfile))
                (new XmlSerializer(typeof(List<Tick>))).Serialize(tw, ticks);

            Console.WriteLine(newfile.Replace(_xmldir, "") + " is saved. Ticks: " + ticks.Count);
        }

        private static List<Tick> Deserialize(string file)
        {
            using (var rw = new StreamReader(file))
                return (List<Tick>)(new System.Xml.Serialization.XmlSerializer(typeof(List<Tick>))).Deserialize(rw);
        }
    }
}