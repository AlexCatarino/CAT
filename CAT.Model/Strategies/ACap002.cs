using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TicTacTec.TA.Library;

namespace CAT.Model
{
    [Serializable]
    class ACap002 : Strategy
    {
        # region Fields

        private KeyValuePair<int, DateTime> _shift;
        private DateTime _shifttime;

        [NonSerialized]
        private List<Candle> _candles;
        private List<Candle> _eodcandles;
        
        #endregion

        #region Constructor

        public ACap002()
            : base()
        {
            this.hour = 10;
            this.candles = new List<Candle>();
            this._candles = new List<Candle>();
            this._eodcandles = new List<Candle>();
        }

        #endregion

        #region Public override functions
                
        public override void Run(Tick tick)
        {
            if (Filter(tick)) return;

            currentTime = tick.Time;
            StartOrReset();

            var eod = currentTime >= eodTime;
            var last = Indicadors(MakeCandle(tick));

            if (type != 0)
            {
                TrailingStop(tick);
                TrailingGain(tick, last);

                var action = trade.Update(tick) ? 0 : -trade.Type;
                Send(trade, action);

                if (trade.IsTrading) return;

                type = 0;
                trades.Add(trade);
            }

            // Return while ref candle has not oppened
            if (type != 0 || last == null || eod) return;

            type = int.Parse(last.Indicators);

            trade = new Trade(type, tick, setup);
            trade.CloseTime = eodTime;
            trade.IsTrading = true;

            trade.Update(tick);

            Send(trade, 1);

            // Rule: Can only exit after 2 candles
            _candles.RemoveAll(c => c.OpenTime < currentTime);
        }

        public override void StartOrReset()
        {
            if (eodTime.Date == currentTime.Date) return;
            eodTime = GetBell(currentTime, 6 + 55.0 / 60);
            if (trade != null) trade.CloseTime = eodTime;
            return;
        }

        public override void WarmUp(List<Tick> ticks)
        {
            base.WarmUp(ticks);
            #region Get EOD data to calculate VolRank

            if (ticks.Count == 0) ticks.Add(new Tick(setup.Symbol, DateTime.Now, 0));
            var year = ticks.First().Time.AddYears(-1).Year;
            if (ticks.First().Time.Month == 1) year--;

            if (_eodcandles == null) 
                _eodcandles = new List<Candle>();
            else 
                _eodcandles.Clear();

            var files = Directory.GetFiles(DataDir, "COTAHIST" + "*xml").ToList();            
            files.Clear(); // Assim não vai ler nada

            foreach (var file in files)
            {                
                if (year > int.Parse(new FileInfo(file).Name.Substring(10, 4))) continue;

                try
                {
                    using (var tr = new StreamReader(file))
                    {
                        var allcandles = (List<Candle>)(new System.Xml.Serialization.XmlSerializer(typeof(List<Candle>))).Deserialize(tr);
                        _eodcandles.AddRange(allcandles.FindAll(c => Symbols.Contains(c.Symbol)));
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    throw;
                }
            }
            candles = candles.OrderBy(c => c.OpenTime).ToList();
            if (ticks.Count == 1) ticks.Clear();
            #endregion
            var last = Indicadors(true);
            //ticks.RemoveAll(t => t.Time >= new DateTime(2013, 1, 1));
        }
        #endregion

        #region Specific functions
        private Candle Indicadors(bool closedcandle)
        {
            var index = 0;

            if (!closedcandle && _candles != null)
            {
                index = _candles.FindIndex(c => currentTime < c.OpenTime.AddMinutes(setup.TimeFrame));
                if (index == -1) index = _candles.Count;
                if (index < 2) return null;

                _candles.RemoveRange(0, index - 2);

                var zero = _candles[0];
                var last = _candles[1];

                // For day-trade
                if (false && setup.DayTradeDuration > 0 && zero.OpenTime.Date != last.OpenTime.Date && zero.Indicators == last.Indicators)
                {
                    if (_shift.Key.ToString() != zero.Indicators)
                        _shift = new KeyValuePair<int, DateTime>(int.Parse(zero.Indicators), zero.OpenTime);

                    var type = _shift.Value == zero.OpenTime ? _shift.Key : -_shift.Key;
                    //type = -_shift.Key;

                    last.Indicators = type.ToString();

                    return last;
                }

                return zero.Indicators == last.Indicators ? null : last;
            }

            #region All MA Types
            var AllMAType = new Dictionary<string, Core.MAType> 
            { 
                { "T3", Core.MAType.T3 },
                { "SMA", Core.MAType.Sma },
                { "WMA", Core.MAType.Wma },
                { "EMA", Core.MAType.Ema },
                { "DEMA", Core.MAType.Dema },
                { "TEMA", Core.MAType.Tema },
                { "TRIMA", Core.MAType.Trima },
                { "KAMA", Core.MAType.Kama },
                { "MAMA", Core.MAType.Mama },
            };
            #endregion

            var par = setup.Parameters.Split(' ');
            var optInTimePeriod = int.Parse(par[0]);
            var key = par.Length > 1 ? par[1].ToUpper() : "SMA";
            if (!AllMAType.ContainsKey(key)) key = "SMA";
            var optInMAType = AllMAType[key];

            var startIdx = 0;
            var outNBElement = 0;
            var endIdx = candles.Count;
            var outBegIdx = Core.MovingAverageLookback(optInTimePeriod, optInMAType);
            if (outBegIdx >= endIdx) return null;

            var vecH = new double[endIdx - outBegIdx];
            var vecL = new double[endIdx - outBegIdx];
            
            // Moving Averages ------------------------------------------------------------------------
            Core.MovingAverage(startIdx, endIdx - 1, candles.Select(c => (double)c.MaxValue).ToArray(),
                optInTimePeriod, optInMAType, out outBegIdx, out outNBElement, vecH);

            Core.MovingAverage(startIdx, endIdx - 1, candles.Select(c => (double)c.MinValue).ToArray(),
                optInTimePeriod, optInMAType, out outBegIdx, out outNBElement, vecL);

            #region Vol Rank -------------------------------------------------------------------------------
            //var std = new List<decimal>(); std.Add(0);
            //var ret = new List<decimal>(); ret.Add(0);
            //endIdx = _eodcandles.Count;
            //for (var i = 1; i < endIdx; i++)
            //{
            //    ret.Add((_eodcandles[i - 1].CloseValue - _eodcandles[i].CloseValue) / _eodcandles[i].CloseValue);
            //    std.Add(0);
                
            //    if (i < 21) continue;

            //    var range = ret.GetRange(i - 21, 21);
            //    var avg = range.Average();
            //    std[i] = (decimal)Math.Sqrt(range.Sum(d => Math.Pow((double)(d - avg), 2)) / 21);

            //    if (i < 126) continue;

            //    range = ret.GetRange(i - 126, 126);
            //    var max = range.Max();
            //    _eodcandles[i].Indicators = (std[i] / max).ToString("P0");
            //}

            //_eodcandles.RemoveAll(c => string.IsNullOrWhiteSpace(c.Indicators));
            #endregion

            for (var i = outBegIdx; i < candles.Count; i++)
            {
                var outH = (decimal)vecH[i - outBegIdx];
                var outL = (decimal)vecL[i - outBegIdx];
                var close = candles[i].CloseValue;

                var sel = close > outH ? 1 : 0;
                var buy = close < outL ? -1 : 0;

                candles[i].Indicators = sel + buy == 0 ? candles[i - 1].Indicators : (sel + buy).ToString();

                //candles[i].Indicators += ";" + outL.ToString("#.000") + ";" + outH.ToString("#.000\r\n");
            }

            #region ShiftIndex para day-trade
            for (var i = 0; i < candles.Count && setup.DayTradeDuration > 0; i++)
            {
                if (string.IsNullOrWhiteSpace(candles[i].Indicators)) continue;
                if (candles[i].OpenTime.Date != candles[i + 1].OpenTime.Date)
                {
                    _shift = new KeyValuePair<int, DateTime>(int.Parse(candles[i].Indicators), candles[i].OpenTime);
                    _shifttime = candles[i].OpenTime;
                    break;
                }
            }
            #endregion

            _candles = candles.FindAll(c => c.OpenTime >= setup.OfflineStartTime &&
                !string.IsNullOrWhiteSpace(c.Indicators) && c.Indicators != "0");

            #region Print Candles
            if (true)
            {
                var file = new FileInfo(Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + @"\candles.csv");
                if (file.Exists) file.Delete();
                _candles.ForEach(c => File.AppendAllText(file.FullName, c.ToString() + "\r\n"));
            }
            #endregion

            candles.RemoveRange(0, Math.Max(0, candles.Count - optInTimePeriod));

            var lastcandle = _candles.Count < 2 || _candles[_candles.Count - 2].Indicators == _candles.Last().Indicators
                ? null : _candles.Last();

            if (candles.Count > 0 && candles.Last().OpenTime > DateTime.Today) OnStatusChanged(candles.Last().ToString());

            return lastcandle;
        }
        private void TrailingStop(Tick tick)
        {
            if (!setup.DynamicLoss.HasValue || tick.Symbol != trade.Symbol) return;

            var tsl = trade.Type*(tick.Value - trade.EntryValue*(decimal?) setup.DynamicLoss*trade.Type);
            tsl = trade.Type*(tick.Value*(1 - (decimal?) setup.DynamicLoss*trade.Type));

            if (tsl > trade.Type*trade.StopLoss || !trade.StopLoss.HasValue) trade.StopLoss = tsl*trade.Type;
        }
        private void TrailingGain(Tick tick, Candle last)
        {
            if (last == null || last.Indicators == type.ToString()) return;
            trade.StopGain = tick.Value;
        }
        #endregion

        #region Public override functions ONLINE TRADING
        public override DateTime GetStartTime()
        {
            var optInTimePeriod = .0;
            var str = setup.Parameters.Split(' ')[0];
            if (!double.TryParse(str, out optInTimePeriod))
                return setup.OfflineStartTime;

            var time = DateTime.Today.AddDays(-optInTimePeriod);
            if (time < setup.OfflineStartTime) time = setup.OfflineStartTime;

            return candles != null && candles.Count > 0 ?
                candles.Last().OpenTime.AddMinutes(setup.TimeFrame) : time;
        }
        #endregion

        #region Public override functions OPTIMIZATION

        public override int GetNumberOfParameters()
        {
            return 4;
        }

        public override Setup Gene2Setup(Setup setup, double[] genes)
        {
            var mySetup = new Setup(setup.SetupId, setup.Name);
            mySetup.Symbol = setup.Symbol;
            mySetup.Allow = setup.Allow;
            mySetup.Discount = setup.Discount;
            mySetup.Slippage = setup.Slippage;
            mySetup.Capital = setup.Capital;
            mySetup.DayTradeDuration = setup.DayTradeDuration;
            mySetup.OfflineStartTime = setup.OfflineStartTime;
            mySetup.StaticLoss = .1;

            mySetup.DynamicLoss = Math.Min(.1, Math.Round(genes[0]*.12, 4));
            mySetup.Parameters = Math.Ceiling(3 + genes[1]*100).ToString();
            mySetup.TimeFrame = Math.Max(5, Math.Min(60, Math.Ceiling(3 + genes[2]*60)));
            mySetup.StaticGain = Math.Min(.15, Math.Round(genes[3]*.17, 4));

            return mySetup;
        }
        public override double[] Setup2Gene(Setup setup)
        {
            var lenght = GetNumberOfParameters();
            var myGenes = new double[lenght];

            myGenes[0] = setup.DynamicLoss.HasValue ? Math.Max(0, Math.Min(1, (setup.DynamicLoss.Value / .05))) : 1;
            
            if (lenght <= 1) return myGenes;
            myGenes[1] = !string.IsNullOrWhiteSpace(setup.Parameters) ? Math.Max(0, Math.Min(1, (double.Parse(setup.Parameters) - 3) / 100)) : 1;
            
            if (lenght <= 2) return myGenes;
            myGenes[2] = Math.Max(0, Math.Min(1, ((setup.TimeFrame - 3) / 15)));
            
            if (lenght <= 3) return myGenes;
            myGenes[3] = setup.StaticGain.HasValue ? Math.Max(0, Math.Min(1, (setup.StaticGain.Value / .05))) : 1;
            return myGenes;
        }
        #endregion
    }
}