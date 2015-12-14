using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TicTacTec.TA.Library;

namespace CAT.Model
{
    [Serializable]
    class DCap002 : Strategy
    {
        # region Fields

        private KeyValuePair<int, DateTime> _shift;
        private DateTime _shifttime;

        [NonSerialized]
        private List<Candle> _candles;
        private List<Candle> _eodcandles;
        
        #endregion

        #region Constructor

        public DCap002()
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

            type = int.Parse(last.Indicators.Split(';')[0]);
           
            if (type > 0 && tick.Value <= last.MaxValue) type = 0;
            if (type < 0 && tick.Value >= last.MinValue) type = 0;
            if (type == 0) return;

            trade = new Trade(type, tick, setup);
            trade.CloseTime = eodTime;

            var sl = type > 0 ? last.MinValue : last.MaxValue;
            var sg = type < 0 ? last.MinValue : last.MaxValue;

            trade.ChangeLoss(sl);
            trade.ChangeGain(10 - Math.Abs(sg - trade.EntryValue));

            trade.IsTrading = true;

            trade.Update(tick);

            Send(trade, 1);
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

            _candle = new Candle(ticks[0]);
            _candle.Period = "15";
            candlecloseTime = candles[1].OpenTime.AddMinutes(15 - setup.TimeFrame);
            ticks.ForEach(t => MakeCandle(t, 15));
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
            var index = 0;

            if (!closedcandle && _candles != null)
            {
                var history = _candles.FindAll(c => currentTime.Date == c.OpenTime.Date &&
                    currentTime >= c.OpenTime.AddMinutes(setup.TimeFrame) &&
                    currentTime < c.OpenTime.AddMinutes(2 * setup.TimeFrame));
                if (history == null || history.Count == 0) return null;

                var _last = history.Last();

                _candles.RemoveRange(0, _candles.IndexOf(_last));

                return _candles[0];
            }

            var uppertimeframe = 15;
            var optInTimePeriod = 21;
            var par = !string.IsNullOrWhiteSpace(setup.Parameters) ? setup.Parameters.Split(' ') :
                new string[] { uppertimeframe.ToString() };

            if (par.Length > 0) int.TryParse(par[0], out uppertimeframe);
            if (par.Length > 1) int.TryParse(par[1], out optInTimePeriod);
            
            var candleslower = candles.FindAll(c => c.Period != uppertimeframe.ToString() &&
                c.OpenValue == c.CloseValue && c.OpenTime.TimeOfDay < new TimeSpan(17, 0, 0) &&
                (c.OpenTime.TimeOfDay < new TimeSpan(13, 00, 00) || c.OpenTime.TimeOfDay > new TimeSpan(14, 30, 00)));
           
            var startIdx = 0;
            var outNBElement = 0;
            var outBegIdx = Core.MovingAverageLookback(optInTimePeriod, Core.MAType.Ema);
            var candlesupper = candles.FindAll(c => c.Period == uppertimeframe.ToString());
            var endIdx = candlesupper.Count;
            if (endIdx < outBegIdx) return null;

            var outReal = new double[endIdx - outBegIdx];

            // Moving Averages ------------------------------------------------------------------------
            Core.MovingAverage(startIdx, endIdx - 1, candlesupper.Select(c => (double)c.MaxValue).ToArray(),
                optInTimePeriod, Core.MAType.Ema, out outBegIdx, out outNBElement, outReal);

            var opt = !true;
            var file = new FileInfo(@"C:\Users\Alexandre\Desktop\candles.csv");
            if (file.Exists) file.Delete();

            for (var i = outBegIdx; i < endIdx; i++)
            {
                var close = candlesupper[i].CloseValue;
                var movave = (decimal)outReal[i - outBegIdx];
                var sign = Math.Sign(close - movave);

                var odate = candlesupper[i].OpenTime.AddMinutes(uppertimeframe);
                var cdate = odate.AddMinutes(uppertimeframe);

                var subcandles = candleslower.FindAll(c => c.OpenTime >= odate && c.OpenTime < cdate);
                
                for (var j = 0; j < subcandles.Count; j++)
                {
                    subcandles[j].Indicators = sign.ToString();
                    if (opt) File.AppendAllText(file.FullName, subcandles[j].ToString() + "\r\n");
                }
                candleslower.RemoveAll(c => c.OpenTime < cdate);
            }

            _candles = candles.FindAll(c => c.OpenTime >= setup.OfflineStartTime && !string.IsNullOrWhiteSpace(c.Indicators));

            candles.RemoveAll(c => c.Period != uppertimeframe.ToString());
            if (candles.Count > 0 && candles.Last().OpenTime > DateTime.Today) OnStatusChanged(candles.Last().ToString());

            var lastcandle = _candles.Count < 2 || _candles[_candles.Count - 2].Indicators == _candles.Last().Indicators
                ? null : _candles.Last();
            
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
            mySetup.Capital = 1m;
            mySetup.DayTradeDuration = setup.DayTradeDuration;
            mySetup.OfflineStartTime = setup.OfflineStartTime;
            mySetup.StaticLoss = null;
            mySetup.DynamicLoss = null;
            mySetup.StaticGain = null;

            mySetup.TimeFrame = Math.Max(2, Math.Min(7, Math.Ceiling(2 + genes[0] * 7)));

            var optInTimePeriod = Math.Max(2, Math.Min(30, Math.Ceiling(1 + genes[1] * 30)));
            var maxrangeallowed = Math.Max(2, Math.Min(20, Math.Ceiling(1 + genes[2] * 20)));
            var breakoutcandlesize = Math.Max(.5, Math.Min(2, Math.Round(.4 + genes[3] * 2, 4)));

            mySetup.Parameters = optInTimePeriod + " " + maxrangeallowed + " " + breakoutcandlesize;
            
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