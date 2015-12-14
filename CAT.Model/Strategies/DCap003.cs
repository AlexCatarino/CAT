using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TicTacTec.TA.Library;

namespace CAT.Model
{
    [Serializable]
    class DCap003 : Strategy
    {
        # region Fields

        private KeyValuePair<int, DateTime> _shift;
        private DateTime _shifttime;

        [NonSerialized]
        private List<Candle> _candles;
        private List<Candle> _eodcandles;
        
        #endregion

        #region Constructor

        public DCap003()
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
            base.WarmUp(ticks.Where(t => t.Time.Hour < 17).ToList());
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

            var optInTimePeriod = 9;
            var optInMAType = AllMAType["EMA"];
            var par = !string.IsNullOrWhiteSpace(setup.Parameters)
                ? setup.Parameters.Split(' ')
                : new string[] { optInTimePeriod.ToString(), "EMA" };
            
            if (par.Length > 0) int.TryParse(par[0], out optInTimePeriod);
            if (par.Length > 1 && AllMAType.ContainsKey(par[1].ToUpper()))
                optInMAType = AllMAType[par[1].ToUpper()];
            
            var startIdx = 0;
            var outNBElement = 0;
            var outBegIdx = Core.MovingAverageLookback(optInTimePeriod, optInMAType);
            var endIdx = candles.Count;
            if (endIdx < outBegIdx) return null;

            var outReal = new double[endIdx - outBegIdx];
            
            // Moving Averages ------------------------------------------------------------------------
            Core.MovingAverage(startIdx, endIdx - 1, candles.Select(c => (double)c.CloseValue).ToArray(),
                optInTimePeriod, optInMAType, out outBegIdx, out outNBElement, outReal);

            var prev = 0;

            for (var i = outBegIdx; i < endIdx; i++)
            {
                var mave = (decimal)outReal[i - outBegIdx];
                var curr = Math.Sign(candles[i].CloseValue - mave);

                if (curr != prev) candles[i].Indicators = string.Format("{0};{1:0.00}", curr, mave);
                
                prev = curr;
            }

            _candles = candles.FindAll(c => !string.IsNullOrWhiteSpace(c.Indicators));

            #region Print Candles
            var file = string.Format(@"{0}\ID_{1}_{2}_candle.csv", Environment.GetFolderPath(Environment.SpecialFolder.Desktop), setup.SetupId, setup.Symbol);
            if (File.Exists(file)) File.Delete(file);
            if (true) candles.ForEach(c => File.AppendAllText(file, c.ToString() + "\r\n"));
            #endregion

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
            if (last == null || last.Indicators.Split(';')[0] == type.ToString()) return;
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