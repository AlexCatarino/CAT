using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TicTacTec.TA.Library;

namespace CAT.Model
{
    [Serializable]
    class ACap004 : Strategy
    {
        # region Fields

        private KeyValuePair<int, DateTime> _shift;
        private DateTime _shifttime;

        [NonSerialized]
        private List<Candle> _candles;
        private List<Candle> _eodcandles;
        
        #endregion

        #region Constructor

        public ACap004()
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

            var minvar = tick.GetMinVar();
            
            trade = new Trade(type, tick, setup);
            trade.CloseTime = eodTime;
            trade.EntryValue = (trade.Type < 0 ? last.MinValue : last.MaxValue) + minvar * trade.Type;

            trade.ChangeLoss(trade.Type > 0 ? last.MinValue : last.MaxValue);

            var range = setup.StaticGain.HasValue ? (decimal)setup.StaticGain.Value : 0m;
            trade.ChangeGain(range = Math.Max(range, (last.MaxValue - last.MinValue + 2 * minvar) * 1m));
            while (trade.CurrentMaxGain[0] == '-') trade.ChangeGain(range += minvar);

            trade.StopLoss = null;

            trade.CloseTime = eodTime;
            trade.IsTrading = true;
            trade.Update(tick);

            Send(trade, 1);

            _candles.Remove(last);
        }

        public override void StartOrReset()
        {
            if (eodTime.Date == currentTime.Date) return;

            if (eodTime.Year > 1) eodTime.AddHours(-setup.DayTradeDuration);
            var x = eodTime > dbvsp ? 9 : currentTime.Hour > 10 ? eodTime.Hour : currentTime.Hour;
            eodTime = FixEODTime(currentTime.Date.AddHours(x + setup.DayTradeDuration));          
            return;
        }

        public override void WarmUp(List<Tick> ticks)
        {
            base.WarmUp(ticks);
            var last = Indicadors(true);
        }
        #endregion

        #region Specific functions
        private Candle Indicadors(bool closedcandle)
        {
            if (!closedcandle && _candles != null)
            {
                var last = _candles.FindLast(c => currentTime >= c.OpenTime.AddMinutes(setup.TimeFrame));
                if (last == null) return null;
                if ((currentTime - last.OpenTime).TotalMinutes > setup.TimeFrame * 2) return null;

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
                { "KAMA", Core.MAType.Kama },
                { "MAMA", Core.MAType.Mama },
                { "TRIMA", Core.MAType.Trima },
            };
            #endregion

            var unit = new Tick(candles.First().Symbol, DateTime.Now, 0m).GetMinVar();
            var optInTimePeriod = 21;
            var maType = AllMAType["SMA"];

            if (string.IsNullOrWhiteSpace(setup.Parameters)) setup.Parameters = "21 SMA";

            var par = setup.Parameters.Split(' ');
            if (par.Length > 0) int.TryParse(par[0], out optInTimePeriod);
            if (par.Length > 1) maType = AllMAType[par[1].Trim().ToUpper()];

            setup.Parameters = (
                (par.Length > 0 ? optInTimePeriod.ToString() : "") + " " +
                (par.Length > 1 ? par[1].Trim().ToUpper() : "")).Trim();
            
            var startIdx = 0;
            var outNBElement = 0;
            var endIdx = candles.Count;
            var outBegIdx = Core.MovingAverageLookback(optInTimePeriod, maType);
            
            if (outBegIdx >= endIdx) return null;
            
            var outReal = new double[endIdx - outBegIdx];

            // Moving Average ---------------------------------------------------------------------------
            Core.MovingAverage(startIdx, endIdx - 1, candles.Select(c => (double)c.CloseValue).ToArray(),
                optInTimePeriod, maType, out outBegIdx, out outNBElement, outReal);

            for (var i = outBegIdx; i < candles.Count; i++)
            {
                var candle = candles[i];
                if (candle.CloseValue != candle.OpenValue) continue; // Doji
                candle.Indicators = Math.Sign(candle.CloseValue - (decimal)outReal[i - outBegIdx]) +
                    ";" + outReal[i - outBegIdx].ToString("0.0000");
            }

            _candles = candles.FindAll(c => c.OpenTime >= setup.OfflineStartTime &&
                !string.IsNullOrWhiteSpace(c.Indicators) && c.Indicators.Substring(0, 1) != "0" &&
                c.OpenTime.TimeOfDay < new TimeSpan(12, 30, 0));

            #region Print Candles
            if (!true)
            {
                var file = new FileInfo(Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + @"\" + this.setup.Symbol + "candles.csv");
                if (file.Exists) file.Delete();
                _candles.ForEach(c => File.AppendAllText(file.FullName, c.ToString() + "\r\n"));
            }
            _candles.ForEach(c => c.Indicators = c.Indicators.Split(';')[0]);
            #endregion

            candles.RemoveRange(0, Math.Max(0, candles.Count - outBegIdx));

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
            return 3;
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
            mySetup.StaticGain = null;

            mySetup.Parameters = setup.Parameters;

            mySetup.TimeFrame = Math.Ceiling(Math.Max(1, Math.Min(2, genes[0] * 2.1))) * 5;
            mySetup.DynamicLoss = Math.Ceiling(Math.Max(1, Math.Min(50, genes[1] * 50.5))) / 2;
            mySetup.StaticGain = Math.Ceiling(Math.Max(1, Math.Min(100, genes[2] * 101))) / 2;
            
            return mySetup;
        }
        public override double[] Setup2Gene(Setup setup)
        {
            var lenght = GetNumberOfParameters();
            var myGenes = new double[lenght];

            myGenes[0] = Math.Max(0, Math.Min(1, setup.TimeFrame / 10.5));
            if (lenght <= 1) return myGenes;

            myGenes[1] = setup.DynamicLoss.HasValue ? Math.Max(0, Math.Min(1, (double)(setup.DynamicLoss / 50.5))) : 0;
            if (lenght <= 2) return myGenes;

            myGenes[2] = setup.StaticGain.HasValue ? Math.Max(0, Math.Min(1, (double)(setup.StaticGain / 25.25))) : 0;
            return myGenes;
        }
        #endregion
    }
}