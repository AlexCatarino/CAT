using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TicTacTec.TA.Library;

namespace CAT.Model
{
    [Serializable]
    class ACap003 : Strategy
    {
        # region Fields

        private KeyValuePair<int, DateTime> _shift;
        private DateTime _shifttime;

        [NonSerialized]
        private List<Candle> _candles;
        private List<Candle> _eodcandles;
        
        #endregion

        #region Constructor

        public ACap003()
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
                //TrailingStop(tick);
                //TrailingGain(tick, last);

                var action = trade.Update(tick) ? 0 : -trade.Type;
                Send(trade, action);

                if (trade.IsTrading) return;

                type = 0;
                trades.Add(trade);
            }

            // Return while ref candle has not oppened
            if (type != 0 || last == null || eod) return;

            type = int.Parse(last.Indicators);

            if (tick.Value <= last.MaxValue) type = Math.Min(0, type);
            if (tick.Value >= last.MinValue) type = Math.Max(0, type);
            if (type == 0) return;

            trade = new Trade(type, tick, setup);
            trade.CloseTime = eodTime;

            var sl = trade.Type > 0 ? last.MinValue : last.MaxValue;

            trade.ChangeLoss(sl);
            trade.ChangeGain(last.MaxValue - last.MinValue);

            trade.IsTrading = true;
            trade.Update(tick);

            Send(trade, 1);

            _candles.Remove(last);
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
        }
        #endregion

        #region Specific functions
        private Candle Indicadors(bool closedcandle)
        {
            if (!closedcandle && _candles != null)
            {
                var last = _candles.FindLast(c => currentTime >= c.OpenTime.AddMinutes(setup.TimeFrame));
                if (last == null || (currentTime - last.OpenTime).TotalMinutes > setup.TimeFrame * 2) return null;

                _candles.RemoveRange(0, _candles.IndexOf(last));

                return last;
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

            var optInTimePeriod = 20;
            var optInNbDevUpDn = 2.0;
            var optInFastPeriod = 12;
            var optInSlowPeriod = 26;
            var optInSignalPeriod = 9;
            var optInMAType = AllMAType["SMA"];

            if (string.IsNullOrWhiteSpace(setup.Parameters))
            {
                setup.Parameters = optInTimePeriod + " " + optInNbDevUpDn + " " + optInFastPeriod + " " + optInSlowPeriod + " " + optInSignalPeriod; 
            }
            else
            {
                var par = setup.Parameters.Split(' ');
                if (par.Length > 0) int.TryParse(par[0], out optInTimePeriod);
                if (par.Length > 1) double.TryParse(par[1], out optInNbDevUpDn);
                if (par.Length > 2) int.TryParse(par[2], out optInFastPeriod);
                if (par.Length > 3) int.TryParse(par[3], out optInSlowPeriod);
                if (par.Length > 4) int.TryParse(par[4], out optInSignalPeriod);
            }

            var startIdx = 0;
            var outNBElement = 0;
            var endIdx = candles.Count;
            var outBegIdx = new int[]
            {
                Core.MacdLookback(optInFastPeriod, optInSlowPeriod, optInSignalPeriod),
                Core.BbandsLookback(optInTimePeriod, optInNbDevUpDn, optInNbDevUpDn, optInMAType)
            };
            
            if (outBegIdx.Max() >= endIdx) return null;
            
            var outMACD = new double[endIdx - outBegIdx[0]];
            var outMACDHis = new double[endIdx - outBegIdx[0]];
            var outMACDSignal = new double[endIdx - outBegIdx[0]];
            var outRealUpperBand = new double[endIdx - outBegIdx[1]];
            var outRealLowerBand = new double[endIdx - outBegIdx[1]];
            var outRealMiddleBand = new double[endIdx - outBegIdx[1]];

            // MACD -------------------------------------------------------------------------------
            Core.Macd(startIdx, endIdx - 1, candles.Select(c => (double)c.CloseValue).ToArray(),
                optInFastPeriod, optInSlowPeriod, optInSignalPeriod, out outBegIdx[0], out outNBElement,
                outMACD, outMACDSignal, outMACDHis);
            
            // Bollinger Bands --------------------------------------------------------------------
            Core.Bbands(startIdx, endIdx - 1, candles.Select(c => (double)c.CloseValue).ToArray(),
                optInTimePeriod, optInNbDevUpDn, optInNbDevUpDn, optInMAType, out outBegIdx[1], out outNBElement,
                outRealUpperBand, outRealMiddleBand, outRealLowerBand);

            var reflowertouch = new DateTime();
            var refuppertouch = new DateTime();
            var dicMACD = new Dictionary<DateTime, double>();

            for (var i = outBegIdx.Max() + 1; i < candles.Count; i++)
            {
                var prevcandle = candles[i - 1];
                var currcandle = candles[i - 0];
                var lasttouch = new DateTime();                
                var thistouch = currcandle.OpenTime;

                dicMACD.Add(thistouch, outMACD[i - outBegIdx[0]]);

                var prevlowerbandtouch = prevcandle.MinValue <= (decimal)outRealLowerBand[i - 1 - outBegIdx[1]];
                var currlowerbandtouch = currcandle.MinValue <= (decimal)outRealLowerBand[i - 0 - outBegIdx[1]];
                var prevupperbandtouch = prevcandle.MaxValue >= (decimal)outRealUpperBand[i - 1 - outBegIdx[1]];
                var currupperbandtouch = currcandle.MaxValue >= (decimal)outRealUpperBand[i - 0 - outBegIdx[1]];
                var currbothbandtouch = currlowerbandtouch && currupperbandtouch;

                if (prevlowerbandtouch && !currlowerbandtouch) reflowertouch = prevcandle.OpenTime;
                if (prevupperbandtouch && !currupperbandtouch) refuppertouch = prevcandle.OpenTime;

                // Clean double touch
                if (reflowertouch > refuppertouch) 
                    currupperbandtouch = false;
                else
                    currlowerbandtouch = false;

                if (currlowerbandtouch) lasttouch = reflowertouch;
                if (currupperbandtouch) lasttouch = refuppertouch;

                if (!dicMACD.ContainsKey(lasttouch)) continue;
                //if (thistouch < new DateTime(2015, 7, 15, 13, 0, 0)) continue;
                
                var type = Math.Sign(dicMACD[thistouch] - dicMACD[lasttouch]);
               
                if (currlowerbandtouch) type = Math.Max(type, 0);
                if (currupperbandtouch) type = Math.Min(type, 0);

                currcandle.Indicators = type.ToString() + ";" + 
                    thistouch + ":;" + dicMACD[thistouch] + ";" +
                    lasttouch + ":;" + dicMACD[lasttouch];
            }

            _candles = candles.FindAll(c => c.OpenTime >= setup.OfflineStartTime &&
                !string.IsNullOrWhiteSpace(c.Indicators) && c.Indicators.Substring(0, 1) != "0");

            #region Print Candles
            if (!true)
            {
                var file = new FileInfo(Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + @"\" + this.setup.Symbol + "candles.csv");
                if (file.Exists) file.Delete();
                _candles.ForEach(c => File.AppendAllText(file.FullName, c.ToString() + "\r\n"));
            }
            _candles.ForEach(c => c.Indicators = c.Indicators.Split(';')[0]);
            #endregion

            candles.RemoveRange(0, Math.Max(0, candles.Count - outBegIdx.Min()));

            var lastcandle = _candles.Count < 2 || _candles[_candles.Count - 2].Indicators == _candles.Last().Indicators
                ? null : _candles.Last();

            if (candles.Count > 0 && candles.Last().OpenTime > DateTime.Today) OnStatusChanged(candles.Last().ToString());
            
            return lastcandle;
        }
        private void TrailingStop(Tick tick)
        {
            if (!setup.DynamicLoss.HasValue || tick.Symbol != trade.Symbol) return;

            var tsl = trade.Type * (tick.Value - (decimal?)setup.DynamicLoss * trade.Type);

            if (tsl > trade.Type * trade.StopLoss || !trade.StopLoss.HasValue) trade.StopLoss = tsl * trade.Type;

            //if (!setup.DynamicLoss.HasValue || tick.Symbol != trade.Symbol) return;

            //var tsl = trade.Type*(tick.Value - trade.EntryValue*(decimal?) setup.DynamicLoss*trade.Type);
            //tsl = trade.Type*(tick.Value*(1 - (decimal?) setup.DynamicLoss*trade.Type));

            //if (tsl > trade.Type*trade.StopLoss || !trade.StopLoss.HasValue) trade.StopLoss = tsl*trade.Type;
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
            mySetup.Capital = 1m;
            mySetup.DayTradeDuration = setup.DayTradeDuration;
            mySetup.OfflineStartTime = setup.OfflineStartTime;
            mySetup.StaticLoss = null;
            mySetup.DynamicLoss = null;
            mySetup.StaticGain = null;

            mySetup.TimeFrame = Math.Max(2, Math.Min(60, Math.Ceiling(genes[0] * 62)));

            //var optInTimePeriod = 20;
            //var optInNbDevUpDn = 2.0;
            //var optInFastPeriod = 12;
            //var optInSlowPeriod = 26;
            //var optInSignalPeriod = 9;

            //var optInTimePeriod = Math.Max(2, Math.Min(30, Math.Ceiling(genes[1] * 32)));
            //var optInNbDevUpDn = genes[2] * 3;

            //mySetup.Parameters = optInTimePeriod + " " + optInNbDevUpDn;

            if (mySetup.Symbol.Substring(3) == "FUT")
            {
                mySetup.StaticLoss = Math.Max(1, Math.Min(20, Math.Ceiling(genes[1] * 22))) / 2;
                mySetup.StaticGain = Math.Max(1, Math.Min(20, Math.Ceiling(genes[2] * 22))) / 2;
                mySetup.DynamicLoss = Math.Max(1, Math.Min(20, Math.Ceiling(genes[3] * 22))) / 2;
            }
            else
            {
                mySetup.StaticLoss = Math.Max(1, Math.Min(20, Math.Ceiling(genes[1] * 22))) / 10;
                mySetup.StaticGain = Math.Max(1, Math.Min(20, Math.Ceiling(genes[2] * 22))) / 10;
                mySetup.DynamicLoss = Math.Max(1, Math.Min(20, Math.Ceiling(genes[3] * 22))) / 10;
            }
            return mySetup;
        }
        public override double[] Setup2Gene(Setup setup)
        {
            var lenght = GetNumberOfParameters();
            var myGenes = new double[lenght];

            myGenes[0] = Math.Max(0, Math.Min(1, setup.TimeFrame / 32));

            var strGenes = setup.Parameters.Split(' ');
            
            if (lenght <= 1) return myGenes;
            myGenes[1] = setup.StaticLoss.HasValue ? Math.Max(0, Math.Min(1, (double)setup.StaticLoss / 11)) : 1;
            
            if (lenght <= 2) return myGenes;
            myGenes[2] = setup.StaticGain.HasValue ? Math.Max(0, Math.Min(1, (double)setup.StaticGain / 11)) : 1;
            
            if (lenght <= 3) return myGenes;
            myGenes[3] = setup.DynamicLoss.HasValue ? Math.Max(0, Math.Min(1, (double)setup.DynamicLoss / 11)) : 1;
            return myGenes;
        }
        #endregion
    }
}