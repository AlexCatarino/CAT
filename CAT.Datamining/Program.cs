using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CAT.Model;

namespace CAT.Datamining
{
    class Program
    {
        static string _file;
        static private List<Trade> Trades;
        static private List<Tick> MyTicks;
        static private List<Tick> Selected;
        static private Dictionary<DateTime, double> DayReturn;

        static private int _nStocksTrade = 30;
        static private int _stdLookback = 90;

        static void Main(string[] args)
        {
            Run();
            char key;
            do { key = Console.ReadKey().KeyChar; } while (key != '0');
        }

        static void Run()
        {
            var market = new List<Quote>();
            var files = File.ReadAllLines(@"C:\Users\alex\Documents\IBOV\IBrA.csv");
            for (var i = 0; i < files.Length; i++)
                files[i] = @"C:\Users\alex\Documents\IBOV\RawDataDaily\" + files[i].Split(';')[0] + ".csv";
            
            foreach (var file in files)
            {
                if (!File.Exists(file))
                {
                    Console.WriteLine(file + " doesn't exist.");
                    continue;
                }
                var data = File.ReadAllLines(file);
                var quotes = new List<Quote>();

                foreach (var d in data)
                {
                    var tmp = d.Split(';');
                    quotes.Add(new Quote(tmp[0], tmp[1], tmp[2], tmp[3], tmp[4], tmp[5], tmp[6], tmp[7]));
                }

                for (var i = 2; i < quotes.Count; i++)
                {
                    quotes[i].UltUltRet = (quotes[i - 1].PreUlt - quotes[i - 2].PreUlt) / quotes[i - 2].PreUlt;
                    quotes[i].MinAbeRet = (quotes[i].PreAbe - quotes[i - 1].PreMin) / quotes[i - 1].PreMin;
                    quotes[i].MaxAbeRet = (quotes[i].PreAbe - quotes[i - 1].PreMax) / quotes[i - 1].PreMax;
                }

                for (var i = _stdLookback + 2; i < quotes.Count; i++)
                    quotes[i].UltUltStd = quotes.GetRange(i - 2 - _stdLookback, _stdLookback)
                        .Select(q => q.UltUltRet).StdDev();

                market.AddRange(quotes);
            }
            //Math.Abs(m.UltUltRet) >= m.UltUltStd ||
            Console.WriteLine("Eliminated: " + market.RemoveAll(m =>  m.Time.Year < 2013));
            market = market.OrderBy(m => m.Time).ToList();

            var output = "";
            var history = new List<double[]>();
            var currentdate = market.First().Time;

            do
            {
                var profit = TradeThem(market.FindAll(q => q.Time == currentdate));
                if (profit != null)
                {
                    history.Add(profit);
                    output += currentdate.ToShortDateString() + ";" +
                        profit[0] + ";" + profit[1] + ";" + profit[2] + "\n";
                } 
                currentdate = currentdate.AddDays(1);

            } while (currentdate <= market.Last().Time);

            var result =
                "Total profit: " + history.Sum(p => p[2]) + " in " + history.Count +
                " days.\nAverage profit: " + history.Average(p => p[2]) +
                "\nW/L(%): " + 100.0 * history.Count(p => p[2] > 0) / history.Count;

            Console.WriteLine(result);
            File.Delete(@"C:\Users\alex\Desktop\result.csv");
            File.AppendAllText(@"C:\Users\alex\Desktop\result.csv", output + result);
        }
        static double[] TradeThem(List<Quote> market)
        {
            if (market.Count <= _nStocksTrade) return null;
            var delete = new List<string>();
            var alltrades = new ConcurrentDictionary<string, Quote>();
            var lngtrades = market.OrderBy(m => m.MinAbeRet).ToList().GetRange(0, _nStocksTrade);
            var shrtrades = market.OrderByDescending(m => m.MaxAbeRet).ToList().GetRange(0, _nStocksTrade);

            foreach (var candle in lngtrades) alltrades.TryAdd(candle.Symbol, candle);
            foreach (var candle in shrtrades)
            {
                if (!alltrades.TryAdd(candle.Symbol, candle))
                    delete.Add(candle.Symbol);
            }
            foreach(var item in delete)
            {
                var dummy = new Quote();
                alltrades.TryRemove(item, out dummy);
                lngtrades.RemoveAll(c => c.Symbol == item);
                shrtrades.RemoveAll(c => c.Symbol == item);
            }
            
            var result = new double[3]
            {
                lngtrades.Sum(m => m.UltAbeRet),
                -shrtrades.Sum(m => m.UltAbeRet),
                0
            };
            result[2] = result[0] + result[1];

            //if (lngtrades.Count < 20)
                Console.WriteLine(lngtrades.First().Time + " " + lngtrades.Count + " " + result[2]);
                if (lngtrades.First().Time.Date == new DateTime(2014, 2, 7))
                {
                    foreach (var candle in lngtrades) File.AppendAllText(@"C:\Users\alex\Desktop\resultgapdvlp.csv", candle.ToString() + Environment.NewLine);
                    File.AppendAllText(@"C:\Users\alex\Desktop\resultgapdvlp.csv", Environment.NewLine);
                    foreach (var candle in shrtrades) File.AppendAllText(@"C:\Users\alex\Desktop\resultgapdvlp.csv", candle.ToString() + Environment.NewLine);
                }

            return result;
        }

    //    static async void Run()
    //    {
    //        DayReturn = new Dictionary<DateTime, double>();
    //        MyTicks = await (new SystemManager()).GetData("INDFUT");
    //        Selected = new List<Tick>();

    //        Trades = new List<Trade>();
    //        var count = 0;
    //        var date = MyTicks.First().Time.Date;
    //        date = new DateTime(2007, 2, 9);
            
    //        _file = @"C:\Users\alex\Desktop\result.csv";
            
    //        //File.Delete(_file);

    //        //do
    //        //{
    //        //    var daytrade = MyTicks.FindAll(t => t.Time.Date == date);
              
    //        //    if (daytrade.Count > 0)
    //        //    {
    //        //        //Console.WriteLine(date.ToShortDateString() + " > " + daytrade.Count);
    //        //        count++;
    //        //        PreProcess(daytrade);
    //        //    }
                
    //        //    date = date.AddDays(1);

    //        //} while (date <= DateTime.Today);

    //        Process(null);



    //        return;

    //        foreach (var tick in Selected)
    //            File.AppendAllText(_file, tick.Time + ";" + tick.Value + Environment.NewLine);


    //        //foreach (var trade in Trades) File.AppendAllText(file, trade.ToString() + Environment.NewLine);

    //        var cnt = Selected.Count();
    //        Console.WriteLine(cnt + " trades em " + count + " dias. Pode fechar.");
    //    }

    //    static void PreProcess(List<Tick> allticks)
    //    { 
    //        var date = allticks.First().Time.Date;
    //        var resu = allticks.Last().Value - allticks.First().Value;
    //        var retn = 0 <= resu && resu <= 25 ? 0 : resu / allticks.First().Value;
    //        var strg = retn > 0 ? " W " : " L ";
    //        strg = date.ToShortDateString() + strg + retn;

    //        if (retn == 0) Console.WriteLine(strg);
    //        File.AppendAllText(_file, date.ToShortDateString() + ";" + retn + Environment.NewLine);

    //        DayReturn.Add(date, retn);
    //    }

    //    static void Process(List<Tick> allticks)
    //    {
    //        var alllines = File.ReadAllLines(_file);

    //        foreach(var line in alllines)
    //        {
    //            var data = line.Split(';');
    //            DayReturn.Add(DateTime.Parse(data[0]), double.Parse(data[1]));
    //        }

    //        var Consective = new Dictionary<DateTime, int>();
    //        var dow = new DayOfWeek[] { DayOfWeek.Sunday, DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday };
    //        var all = new Dictionary<DateTime, double>[dow.Count()];
    //        var win = new Dictionary<DateTime, double>[dow.Count()];
    //        var los = new Dictionary<DateTime, double>[dow.Count()];
    //        var daweek = new double[dow.Count()];
    //        var stddev = new double[dow.Count()];
    //        var aveall = new double[dow.Count()]; 
    //        var avewin = new double[dow.Count()]; 
    //        var avelos = new double[dow.Count()];

    //        var keys = DayReturn.Keys.ToArray();
    //        var values = DayReturn.Values.ToArray();
    //        for (var i = 1; i < DayReturn.Count; i++)
    //        {
    //            var t0 = values[i];
                
                
    //            var c0 = values[i] > 0;
    //            var c1 = values[i - 1] > 0;
    //            if ((c0 && c1) || (!c0 && !c1)) continue;

    //            var c = 0;
    //            var k = i - 1;
    //            do { c++; k--; } while (k >= 0 && ((c0 && values[k] <= 0) || (!c0 && values[k] > 0)));

    //            if (c0) c *= -1;
    //            Consective.Add(keys[i - 1], c);
    //        }

    //        var cfile = @"C:\Users\alex\Desktop\rConsecutive.csv";
    //        File.Delete(cfile);

    //        var hcon = new double[20];
    //        for (var j = -10; j < hcon.Count() - 10; j++)
    //        {
    //            hcon[j + 10] = Consective.Values.Count(r => r == j);
    //            File.AppendAllText(cfile, j + ";" + hcon[j + 10] + Environment.NewLine);
    //        }

    //        for (int i = 0; i < dow.Count(); i++)
    //        {
    //            all[i] = i == 0 ? DayReturn : 
    //                DayReturn.Where(kvp => kvp.Key.DayOfWeek == dow[i]).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    //            win[i] = all[i].Where(d => d.Value > 0).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    //            los[i] = all[i].Where(d => d.Value <= 0).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                
    //            daweek[i] = 100.0 * win[i].Count / all[i].Count;
    //            stddev[i] = all[i].Values.StdDev();
    //            aveall[i] = all[i].Values.Sum() / all[i].Count; 
    //            avewin[i] = win[i].Values.Sum() / win[i].Count;
    //            avelos[i] = los[i].Values.Sum() / los[i].Count;

    //            var str = "E(A) - sigma; E(L); E(W); E(A) + sigma" + Environment.NewLine +
    //                Math.Round(aveall[i] - stddev[i], 5) + ";" + Math.Round(avelos[i], 5) + ";" +
    //                Math.Round(avewin[i], 5) + ";" + Math.Round(aveall[i] + stddev[i], 5) + Environment.NewLine;

    //            Console.WriteLine(str);

    //            if (false)
    //            {
    //                var file = @"C:\Users\alex\Desktop\result" + i + ".csv";
    //                File.Delete(file);
    //                File.AppendAllText(file, str);

    //                var hall = new double[33];
    //                for (var j = 0; j < hall.Count(); j++)
    //                {
    //                    var bound = new double[] { -0.0825 + j * 0.005, -0.0825 + (j + 1) * 0.005 };
    //                    hall[j] = all[i].Values.Count(r => bound[0] <= r && r < bound[1]);
    //                    File.AppendAllText(file, (bound[0] + bound[1]) / 2 + ";" + hall[j] + Environment.NewLine);
    //                }
    //            }
    //        }

    //        return;

    //        var percentage = 0.005;
            
    //        var last = allticks.Last();
    //        var open = last.Time.Date.AddMinutes(570).Add(
    //            TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time").GetUtcOffset(last.Time) -
    //            TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time").GetUtcOffset(last.Time));

    //        var price = allticks.Find(t => t.Time >= open);
    //        var past = allticks.FindAll(t => t.Time <= open);
    //        var futu = allticks.FindAll(t => t.Time >= open);
    //        var diff = 5 * Math.Floor(price.Value * percentage / 5);
    //        if (past.Count < 1) return;
    //        var extremes = new double[] { past.Max(v => v.Value), past.Min(v => v.Value) };

    //        var ticks = new Tick[]
    //        {
    //            futu.Find(t => t.Value >= price.Value + diff),
    //            futu.Find(t => t.Value <= price.Value - diff),
    //        }.Where(t => t != null).OrderBy(t => t.Time).ToList();
            
    //        var count = ticks.Count();
    //        if (count == 0) return;

    //        var result = Math.Sign(ticks[0].Value - price.Value) * (last.Value - ticks[0].Value);

    //        Console.WriteLine("Price: " + price + " Result: " + result + " " + count);
    //        File.AppendAllText(_file, price.Time.Date + ";" + result + ";" + count + Environment.NewLine);
            

    //        return;
            
    //        //var breakUP = new Tick();
    //        //var breakDN = new Tick();

    //        //var types = new bool[] { breakDN == null, breakUP == null};
    //        //if(!types.Contains(true)) return;
    //        //var dirUP = false;
    //        //var dirDN = false;
    //        //if (dirUP && dirDN) return;

    //        //var sign = price.Value - allticks.First().Value > 0;
    //        //var strsign =
    //        //    dirUP && sign ? "UP" :
    //        //    dirDN && !sign ? "DN" : "XX";

    //        //var result =
    //        //    dirUP && !dirDN ? allticks.Last().Value - breakUP.Value :
    //        //    dirDN && !dirUP ? breakDN.Value - allticks.Last().Value : 0;

    //        //if (result == 0 && !dirUP && !dirDN)
    //        //{
    //        //    var ddir = breakUP.Time < breakDN.Time;

    //        //    result = ddir
    //        //        ? allticks.Last().Value - breakUP.Value
    //        //        : breakDN.Value - allticks.Last().Value;
    //        //}

    //        //Console.WriteLine("Price: " + price + " Result: " + result + " " + strsign);
    //        //File.AppendAllText(_file, price.Time.Date + ";" + result + Environment.NewLine);
    //        ////if (!dirDN) Console.WriteLine("UP: " + breakUP);
    //        //if (!dirUP) Console.WriteLine("DN: " + breakDN);
    //    }
    }

    public static class Extensions
    {
        public static double StdDev(this IEnumerable<double> values)
        {
            var count = values.Count();
            if (count < 1) return 0;
            
            //Compute the Average
            var avg = values.Average();
            
            //Perform the Sum of (value-avg)^2
            return Math.Sqrt(values.Sum(d => (d - avg) * (d - avg)) / count);
        }

    //    public static double InvStdDev(this IEnumerable<double> values)
    //    {
            
    //        var count = values.Count();
    //        if (count < 1) return 0;

    //        var prange = 1.0;

    //        //Compute the Average
    //        var avg = values.Average();

    //        var dx = Math.Max(values.Max(), Math.Abs(values.Min()));

    //        do
    //        {
    //            prange = (double)values.Count(d => avg - dx <= d && d <= avg + dx) / count;
    //            dx /= 1.00001;
                
    //        } while (prange > 0.5);
            
    //        return dx;
    //    }
    }

    public class Quote
    {
        public string Symbol { get; set; }
        public DateTime Time { get; set; }
        public double PreAbe { get; set; }
        public double PreMax { get; set; }
        public double PreMin { get; set; }
        public double PreUlt { get; set; }
        public double QuaTot { get; set; }
        public Int64 VolTot { get; set; }
        public double UltAbeRet { get; set; }

        public double UltUltRet { get; set; }
        public double MinAbeRet { get; set; }
        public double MaxAbeRet { get; set; }
        public double UltUltStd { get; set; }

        public Quote()
        { }

        public Quote(string syn, string time, string abe, string max, string min, string ult, string qua, string vol)
        {
            this.Symbol = syn;
            this.Time = DateTime.Parse(time);
            this.PreAbe = double.Parse(abe);
            this.PreMax = double.Parse(max);
            this.PreMin = double.Parse(min);
            this.PreUlt = double.Parse(ult);
            this.QuaTot = double.Parse(qua);
            this.VolTot = Int64.Parse(vol);
            this.UltAbeRet = (this.PreUlt - this.PreAbe) / this.PreAbe;
        }

        public override string ToString()
        {
            var volinfo = this.QuaTot + ";" + this.VolTot + ";";
            var gapinfo = this.MinAbeRet.ToString("#0.000") + ";" + this.MaxAbeRet.ToString("#0.000") + ";";
            return this.Symbol + ";" + this.Time.Date.ToShortDateString().Replace("-", "/") + ";" +
                this.PreAbe.ToString("#.00") + ";" + this.PreMax.ToString("#.00") + ";" + this.PreMin.ToString("#.00") + ";" + this.PreUlt.ToString("#.00") + ";" +
                gapinfo + this.UltAbeRet.ToString("#0.000");
        }

    }
}