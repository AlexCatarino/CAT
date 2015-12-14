namespace CAT.Model
{
    using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading.Tasks;
using System.Xml.Serialization;

    [Serializable]
    public class Strategy : IDisposable
    {
        #region Constructor / Destructor
        public Strategy()
        {
            this.hour = 9;
            this.slippage = 0.01m;
            this.trades = new List<Trade>();
            this.TodayTrades = new ConcurrentDictionary<string, Trade>();
        }
        #endregion

        #region Global variables
        public DateTime dbvsp
        {
            get
            {
                // After this date, Bovespa ALWAYS opens at 9 am.
                return new DateTime(2012, 12, 2);
            }
        }

        public string DataDir
        {
            get
            {
                return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\CAT\Data\";
            }
        }

        public Tick tick;
        public List<Tick> ticks;
        public List<Candle> candles;
        public Candle _candle;
        
        public int hour;
        public int type;
        public int action;
        public bool IsOnline;
        public Trade trade;
        public Trade tradlng;
        public Trade tradsht;
        public Dictionary<string, Trade> tradeDic { get; set; }
        public Setup setup { get; set; }
        public List<Trade> trades { get; set; }
        
        public DateTime eodTime;
        public DateTime currentTime;
        public DateTime candlecloseTime;
        
        public string allow;
        public decimal slippage;

        public event Action<string> StatusChanged;
        public event Action<Trade, int> TradeArrived;
        public List<string> Symbols;
        public ConcurrentDictionary<string, Trade> TodayTrades;
        #endregion

        #region Global functions

        public void OnStatusChanged(string status)
        {
            if (StatusChanged != null) StatusChanged(status);
        }
        protected void Send(Trade trade, int act)
        {
            if (TradeArrived != null) TradeArrived(trade, act);
        }
        public void Dispose()
        {
            TradeArrived = null;
            StatusChanged = null;

            using (var output = File.OpenWrite(String.Format("{0:ID_000}.dat", this.setup.SetupId)))
                (new BinaryFormatter()).Serialize(output, this);
        }
        public Setup Statistics(bool opt)
        {
            return Statistics(opt, 1m);
        }
        public Setup Statistics(bool opt, decimal share)
        {
            if (trades.Count == 0) return null;

            var groupTrades = trades.GroupBy(t => t.ExitTime.Value.Date).ToDictionary(x => x.Key, y => y.ToList());
            var countr = groupTrades.ToDictionary(x => x.Key, y => y.Value.Count);
            var cumsum = new Dictionary<DateTime, decimal>();
            if (trade == null) trade = trades.LastOrDefault();

            if (trade.AssetClass != "BMF.FUT")
            {
                trades.ForEach(t =>
                    {
                        t.GetNetResult(t.ExitValue);
                        t.Cost += t.BkrFixComm / countr[t.ExitTime.Value.Date];
                        t.NetResult = t.NetResult * share - 100 * t.BkrFixComm / t.EntryValue / t.Qnty / countr[t.ExitTime.Value.Date];
                    });
            }
            else
            {
                trades.ForEach(t =>
                    {
                        t.GetNetResult(t.ExitValue);
                        t.NetResult = t.Capital * t.Result * t.Unit - t.Cost;
                    });
            }
            
            trades[0].CumResult = trades[0].NetResult;
            for (var i = 1; i < trades.Count; i++) trades[i].CumResult = trades[i - 1].CumResult + trades[i].NetResult;
            
            var dailyr = trades.GroupBy(t => t.ExitTime.Value.Date).ToDictionary(x => x.Key, y => y.Sum(z => z.NetResult.Value));
            
            #region Calculate DrawnDown
            var max = 0.0m;
            var mdd = 0.0m;
            var esum = 0.0m;
            
            foreach (var pair in dailyr)
            {
                esum += pair.Value;
                max = Math.Max(max, esum);
                mdd = Math.Max(mdd, (max - esum));
                cumsum.Add(pair.Key, esum);
            }
            #endregion
            
            var months = (decimal)(trades.Last().ExitTime.Value - trades.First().EntryTime).TotalDays / 30.4368499m;

            this.action = 0;
            this.setup.MaxDrawndown = mdd;
            this.setup.TotalNetProfit = esum;
            this.setup.DailyNetProfit = esum / months;
            this.setup.TradesCount = trades.Count();
            this.setup.TotalCosts = trades.Sum(t => t.Cost) / months;
            this.setup.SharpeRatio = GetSharpeRation(dailyr.Values);
            this.setup.SharpeRatio = this.setup.SharpeRatio * GetRSquared(cumsum);
            this.setup.PositiveTrades = 100  * trades.Count(t => t.NetResult > 0) / trades.Count;
            this.setup.Description =
                trades.First().EntryTime.ToShortDateString() + " " +
                trades.Last().ExitTime.Value.ToShortDateString() + "\t";

            var nonnulltrades = trades.FindAll(t => t.NetResult.HasValue);
            this.setup.WinLossRatio = this.setup.PositiveTrades == 0 || this.setup.PositiveTrades == 100 ? 0 : -
                nonnulltrades.Where(t => t.NetResult.Value > 0).Average(t => t.NetResult.Value) /
                nonnulltrades.Where(t => t.NetResult.Value < 0).Average(t => t.NetResult.Value);

            var resultsDic = new Dictionary<string, string>();
            
            var firstofthismonth = trade.ExitTime.Value.AddDays(1 - trade.ExitTime.Value.Day);
            var lastofthismonth = firstofthismonth.AddMonths(1).AddDays(-1);
            resultsDic.Add("Mês", Return(firstofthismonth, lastofthismonth));

            var firstofthisyear = new DateTime(trade.ExitTime.Value.Year, 1, 1);
            resultsDic.Add("Ano", Return(firstofthisyear, lastofthismonth));

            var startlast06months = trade.ExitTime.Value.AddMonths(-6);
            resultsDic.Add("6 meses", Return(startlast06months, lastofthismonth));

            var startlast12months = trade.ExitTime.Value.AddMonths(-12);
            resultsDic.Add("12 meses", Return(startlast12months, lastofthismonth));
            
            var startlast24months = trade.ExitTime.Value.AddMonths(-24);
            resultsDic.Add("24 meses", Return(startlast24months, lastofthismonth));

            for (var i = 1; i <= 12; i++)
                resultsDic.Add(firstofthismonth.AddMonths(-i).ToString(@"MMM/yy"),
                    Return(firstofthismonth.AddMonths(-i), lastofthismonth.AddMonths(-i)));

            this.setup.Description += string.Join(";", resultsDic.Keys) + "\r\n;" + string.Join(";", resultsDic.Values);

            if (opt)
            {
                if (esum > mdd) OnStatusChanged(setup.ToString());           
                
                var _file = String.Format(DataDir.Replace(@"\Data\", @"\Backtest\") + "{0:ID_000}_{1:yyMMddHHmm}.csv", setup.SetupId, DateTime.Now);
                if (File.Exists(_file)) File.Delete(_file);

                File.AppendAllText(_file, this.setup.ToString() + "\r\n", System.Text.Encoding.UTF8);
                foreach (var pair in countr) File.AppendAllText(_file, "\r\n" +
                    pair.Key.ToShortDateString() + ";" + pair.Value + ";" + dailyr[pair.Key] + ";" + cumsum[pair.Key]);

                _file = _file.Replace(".csv", "_trades.csv");
                if (File.Exists(_file)) File.Delete(_file);
                for (var i = 0; i < trades.Count; i++) 
                    File.AppendAllText(_file, trades[i].ToString() + "\r\n");
            }

            this.trades.RemoveAll(t => t.EntryTime.Date != trade.EntryTime.Date);

            return this.setup;
        }

        private string Return(DateTime current, DateTime last)
        {
            var sectrades = trades.FindAll(t => t.EntryTime.Date >= current.Date && t.ExitTime.Value.Date <= last.Date);
            if (sectrades.Count == 0) return "0.00";
            return sectrades.Sum(s => s.NetResult).Value.ToString("0.00");
        }

        public virtual void TradableAssets()
        {
            this.Symbols = new List<string>();
            this.Symbols.Add(setup.Symbol);
        }
        public virtual void TradableAssets(bool isonlive)
        {
            this.TradableAssets();
        }
        public void CheckMrktHourChange(double o, double n)
        {
            if (n == o) return;
            eodTime = eodTime.AddHours(n - o);
            OnStatusChanged("Id: " + setup.SetupId + " Novo Bell: " + eodTime.ToShortTimeString());
        }
        public bool MakeCandle(Tick tick)
        {
            return MakeCandle(tick, setup.TimeFrame);
        }
        public bool MakeCandle(Tick tick, double timeframe)
        {
            if (tick.Time < candlecloseTime) 
            {
                if (_candle.OpenTime <= tick.Time && tick.Time < eodTime) _candle.Update(tick);
                return false;
            }

            while (tick.Time >= candlecloseTime)
            {
                candlecloseTime = timeframe == 0
                    ? candlecloseTime.Date.AddDays(1)
                    : candlecloseTime.AddMinutes(timeframe);

                if (candlecloseTime.Date == tick.Time.Date) continue;

                hour = tick.Time.Hour > 10 ? hour : tick.Time.Hour;
                if (timeframe > 0) candlecloseTime = tick.Time.Date.AddHours(hour);

                eodTime = tick.Time.Date.AddDays(1).AddMilliseconds(-1);
            }

            
            candles.Add(_candle);

            _candle = new Candle(tick);
            _candle.Period = timeframe.ToString();
            _candle.OpenTime = candlecloseTime.AddMinutes(-timeframe);
            return true;
        }
        public DateTime GetBell(DateTime open, double duration)
        {
            if (eodTime.Year > 1) eodTime.AddHours(-duration);
            var hour = eodTime > dbvsp ? 10 : open.Hour > 11 ? eodTime.Hour : open.Hour;
            var minutes = duration > 6 && open.Date >= new DateTime(2012, 12, 3) && open.Date <= new DateTime(2013, 7, 5) ? 30 : 0;           
            return open.Date.AddHours(hour + duration).AddMinutes(minutes);
        }
        public DateTime FixEODTime(DateTime input)
        {
            var plus30 = input.Date >= new DateTime(2012, 12, 3) && input.Date <= new DateTime(2013, 7, 5);
            return !plus30 ? input : input.AddMinutes(30);
        }
        
        #endregion

        #region Virtual functions

        /// <summary>
        /// AQUI!!!
        /// </summary>
        /// <returns></returns>
        public virtual DateTime GetStartTime()
        {
            return TimeZoneInfo.ConvertTime(DateTime.Now,
                    TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time"));
        }
        public virtual IEnumerable<Tick> GetData(DateTime starttime)
        {
            DateTime time = starttime;

            var ticks = Datafilter(GetDataXML(starttime));
            foreach (var tick in ticks)
            {
                time = tick.Time;
                yield return tick;
            }
            
            // Read 1sec zip files
            starttime = time;
            ticks = Datafilter(GetDataFromZip("xxx", starttime).OrderBy(t => t.Time));
            foreach (var tick in ticks)
            {
                time = tick.Time;
                yield return tick;
            }
            
            // Read 1min zip files
            starttime = time;
            ticks = Datafilter(GetDataFromZip("xxx", starttime).OrderBy(t => t.Time));
            foreach (var tick in ticks)
            {
                time = tick.Time;
                yield return tick;
            }
            
            // Read 1min csv files
            starttime = time;
            ticks = Datafilter(GetDataFromCsv("min", starttime).OrderBy(t => t.Time));
            foreach (var tick in ticks) yield return tick;
        }

        private IEnumerable<Tick> GetDataFromZip(string resolution, DateTime starttime)
        {
            var zipfile = String.Format("{0}{1}_1{2}.zip", DataDir, this.setup.Symbol, resolution);

            if (File.Exists(zipfile))
            {
                using (var zip2open = new FileStream(zipfile, FileMode.Open, FileAccess.Read))
                {
                    using (var archive = new ZipArchive(zip2open, ZipArchiveMode.Read))
                    {
                        foreach (var entry in archive.Entries)
                        {
                            using (var file = new StreamReader(entry.Open()))
                            {
                                while (!file.EndOfStream)
                                {
                                    foreach (var tick in Bar2Tick(file.ReadLine(), false, starttime))
                                    {
                                        yield return tick;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private IEnumerable<Tick> GetDataFromCsv(string resolution, DateTime starttime)
        {
            var csvfile = String.Format("{0}{1}_1{2}.csv", DataDir, this.setup.Symbol, resolution);

            if (File.Exists(csvfile))
            {
                foreach (var line in File.ReadLines(csvfile))
                {
                    foreach (var tick in Bar2Tick(line, true, starttime))
                    {
                        yield return tick;
                    }
                }
            }
        }
        
        private IEnumerable<Tick> Bar2Tick(string line, bool isMin, DateTime starttime)
        {
            var ticks = new List<Tick>();
            
            var col = line.Split(';');
            var time = DateTime.Parse(col[1] + " " + col[2]);
            if (time < starttime) return ticks;

            if (col[4] == col[5])
            {
                ticks.Add(new Tick(col[0].Trim(), time, decimal.Parse(col[4])));
                return ticks;
            }

            var values = col.ToList().GetRange(3, 4).Select(c => decimal.Parse(c)).ToList();
            if (values[3] > values[0])
            {
                var tmp = values[1];
                values[1] = values[2];
                values[2] = tmp;
            }

            ticks.Add(new Tick(col[0].Trim(), time, values[0]));
            ticks.Add(new Tick(col[0].Trim(), time.AddMilliseconds((isMin ? 59000 : 99)), values[3]));

            values.RemoveAll(v => v == values.First());
            values.RemoveAll(v => v == values.Last());
            if (values.Count == 0) return ticks;

            if (values.Count == 1)
            {
                ticks.Add(new Tick(col[0].Trim(), time.AddMilliseconds((isMin ? 30000 : 50)), values[0]));
                return ticks;
            }

            ticks.Add(new Tick(col[0].Trim(), time.AddMilliseconds((isMin ? 20000 : 33)), values[0]));
            ticks.Add(new Tick(col[0].Trim(), time.AddMilliseconds((isMin ? 40000 : 66)), values[1]));

            return ticks;
        }
        
        public virtual IEnumerable<Tick> GetDataXML(DateTime starttime)
        {
            if (string.IsNullOrWhiteSpace(this.setup.Symbol)) this.setup.Symbol = "NEG";
            var files = Directory.EnumerateFiles(DataDir, this.setup.Symbol.Replace("_C", "") + "*xml");

            foreach (var file in files)
            {
                var date = file.Substring(file.Length - 12, 8);
                var startDate = DateTime.ParseExact(date, "yyyyMMdd", CultureInfo.InvariantCulture);
                if (startDate < starttime.AddDays(1 - starttime.Day)) continue;

                using (var tr = new StreamReader(file))
                {
                    foreach (var tick in (List<Tick>)(new XmlSerializer(typeof(List<Tick>))).Deserialize(tr))
                    {
                        yield return tick;
                    }
                }
            }

        }
        
        private async Task<List<Tick>> GetDataCSV(DateTime lasttime)
        {
            var ticks = new List<Tick>();
            var files = Directory.GetFiles(DataDir, this.setup.Symbol + "*_1min.csv");

            foreach (var file in files)
            {
                var info = new FileInfo(file);
                if (!info.Exists)
                {
                    OnStatusChanged(file + " não existe.");
                    continue;
                }
                var buffer = new byte[info.Length];
                if (buffer.Length < 10000) continue;

                using (var asciifile = File.OpenRead(file))
                {
                    await asciifile.ReadAsync(buffer, 0, buffer.Length);
                    var lines = System.Text.Encoding.UTF8.GetString(buffer).Split('\n').ToList();
                    lines.RemoveAll(l => l.Length == 0);
                    var last = lines.Last();
                    var eod = last.Contains(":") ? 3 : 2;

                    foreach (var line in lines)
                    {
                        var columns = line.Split(';');

                        try
                        {
                            var time = DateTime.Parse(columns[1]);
                            if (eod == 3) time += TimeSpan.Parse(columns[2]);

                            if (time < lasttime) continue;

                            var originals = new decimal[4];
                            for (var i = 0; i < 4; i++) originals[i] = decimal.Parse(columns[eod + i]);
                            if (originals[3] > originals[0])
                            {
                                var tmp = originals[1];
                                originals[1] = originals[2];
                                originals[2] = tmp;
                            }

                            var values = originals.Distinct().ToList();
                            if (originals[3] == originals[0]) values.Add(originals[3]);

                            var add = new double[values.Count];
                            add[0] = eod > 2 ? 0 : 10;
                            add[add.Length - 1] = eod > 2 ? 59 : 16 + 5 / 6;
                            if (add.Length > 2)
                            {
                                add[1] = eod > 2 ? 30 : 13;
                                
                                if (add.Length > 3)
                                {
                                    add[1] = eod > 2 ? 20 : 12;
                                    add[2] = eod > 2 ? 40 : 14;
                                }
                            }
                            
                            var lineticks = new List<Tick>();

                            for (var i = 0; i < values.Count; i++)
                            {
                                var tick = new Tick { Symbol = columns[0].Trim(), Value = values[i], Qnty = 0 };
                                tick.Time = eod == 2 ? time.AddHours(add[i]) : time.AddSeconds(add[i]);
                                lineticks.Add(tick);
                            }
                            
                            ticks.AddRange(lineticks);
                        }
                        catch (Exception e) 
                        {
                            Console.WriteLine(e.Message);
                        };
                    }
                }
            }
            return ticks.OrderBy(t => t.Time).ToList();
        }

        public virtual int GetNumberOfParameters()
        {
            return 4;
        }
        public virtual Setup Gene2Setup(Setup setup, double[] genes)
        {
            return setup;
        }
        public virtual double[] Setup2Gene(Setup setup)
        {
            return new double[GetNumberOfParameters()];
        }
        public virtual IEnumerable<Tick> Datafilter(IEnumerable<Tick> database)
        {
            foreach (var tick in database) yield return tick;
        }
        public virtual void WarmUp(List<Tick> database)
        {
            if (candles == null) candles = new List<Candle>();
            if (candles.Count == 0 || _candle == null)
            {
                _candle = new Candle(database[0]);
                _candle.Period = setup.TimeFrame.ToString();
            }
            if (candles.Count >= 2 && setup.TimeFrame != (candles[1].OpenTime - candles[0].OpenTime).TotalMinutes)
                candles.Clear();

            if (database.Count > 0)
            {
                eodTime = database[0].Time.Date.AddDays(1).AddMilliseconds(-1);
                eodTime = eodTime.Date.AddHours(16).AddMinutes(55);
            }
            if (candles.Count == 0)
                candlecloseTime = setup.TimeFrame == 0 ? eodTime
                    : GetBell(eodTime, 0).AddMinutes(setup.TimeFrame);

            //database.GroupBy(o => o.Time.Date).ToList().ForEach(d =>
            //    {
            //        var hour = d.First().Time.Hour;
            //        database.GroupBy(p=> 
            //            {
            //                var totalminutes = p.Time.TimeOfDay.TotalMinutes;
            //                return totalminutes - (totalminutes % setup.TimeFrame);
            //            }).ToList().ForEach(c=>
            //            {
            //                var x = d.Key.AddMinutes(c.Key);
            //                var y = x;
            //            });
            //    });
                

            database.ForEach(t => MakeCandle(t));
        }
        public virtual void Run(Tick tick)
        { 
        
        }
        public virtual void StartOrReset() 
        {
            
        }
        public virtual bool Filter(Tick tick) 
        {
            if (Symbols == null || Symbols.Count == 0) return false;
            return !Symbols.Contains(tick.Symbol);
        }
        public virtual void ReadDailyData()
        {
        }
        public void TryClose()
        {
            if (!trade.ExitValue.HasValue)
            {
                OnStatusChanged("Esse trade não está em curso.");
                return;
            }

            type = 0;
            action = 1;
            trade.Obs = "Forced exit";
            trades.Add(trade);
            Send(trade, -1);
        }
        public void Break(string timestr)
        {
            if (currentTime > DateTime.ParseExact(timestr, "yyMMdd HHmmss", CultureInfo.InvariantCulture))
                Console.WriteLine("Break!");
        }

        #endregion

        private decimal GetSharpeRation(IEnumerable<decimal> returns)
        {
            var count = returns.Count();
            if (count < 2) return 0;
           
            //Compute the Average
            var avg = returns.Average();

            // Compute the Standard Deviation
            var std = (decimal)Math.Sqrt(returns.Sum(d => Math.Pow((double)(d - avg), 2)) / count);

            //Perform the Sum of (value-avg)^2
            return std == 0 ? 0 : avg / std;
        }

        private decimal GetRSquared(Dictionary<DateTime,decimal> pair)
        {
            var n = pair.Count;
            decimal RSquared = 1m;
            
            if (n < 2) return RSquared;

            try
            {
                var itime = pair.Keys.First();
                var sumX = pair.Sum(c => (c.Key - itime).TotalDays);
                var sumY = (double)pair.Values.Sum();
                var sumXX = pair.Sum(c => Math.Pow((c.Key - itime).TotalDays, 2));
                var sumYY = pair.Sum(c => Math.Pow((double)c.Value, 2));
                var sumXY = pair.Select(c => (double)c.Value * (c.Key - itime).TotalDays).Sum();
                
                var R = (n * sumXY - sumX * sumY) / 
                    (Math.Pow((n * sumXX - Math.Pow(sumX, 2)), 0.5) * Math.Pow((n * sumYY - Math.Pow(sumY, 2)), 0.5));

                RSquared = (decimal)(R * R); 
            }
            catch (Exception ex) { throw (ex); }

            return RSquared;
        }
    }
}

