using System;
using System.Collections.Generic;
using System.Linq;
using TicTacTec.TA.Library;

namespace CAT.Model
{
    [Serializable]
    class ICap002 : Strategy
    {
        # region Fields

        private int _limit;
        private bool _continue;
        private Candle _last;
        private List<Tick> _ticks;
        private List<Candle> _candles;
        
        #endregion
        public ICap002()
            : base()
        {
            this.ticks = new List<Tick>();
            this._ticks = new List<Tick>();
            this.candles = new List<Candle>();
            this._candles = new List<Candle>();
        }

        #region Override functions
        public override void WarmUp(List<Tick> ticks)
        {
            //base.WarmUp(ticks);
            //Indicadors(true);
            //this._ticks = new List<Tick>();
            //_last = _candles.FirstOrDefault();
        }

        public override IEnumerable<Tick> Datafilter(IEnumerable<Tick> ticks)
        {
            return ticks.Where(t => t.Time > DateTime.Today || t.Time >= this.setup.OfflineStartTime);
        }

        public override void Run(Tick tick)
        {
            currentTime = tick.Time;
            if (Filter(tick)) return;

            _ticks.Add(tick);
            _ticks.RemoveAll(t => t.Time < tick.Time.AddMinutes(-30));
            
            if (type != 0)
            {
                trade.ExitTime = tick.Time;
                trade.ExitValue = tick.Value;

                //TrailingGain();

                var eod = trade.ExitTime >= eodTime;
                var gain = type * trade.ExitValue >= trade.StopGain * type;
                var loss = type * trade.ExitValue <= trade.StopLoss * type;
                if (loss || gain || eod) type = 0;
                action = type == 0 ? -trade.Type : 0;

                Send(trade, action);

                if (action == 0) return;

                if (eod) eodTime = trade.ExitTime.Value;   // Is EOD?

                if (loss) _ticks.Clear();

                _continue = true;

                trade.ExitValue =
                    trade.ExitTime > DateTime.Today ? trade.ExitValue :   // Is EOD
                    loss ? trade.StopLoss - trade.Type * slippage :  // Try StopLoss Exit
                    gain ? trade.StopGain : trade.ExitValue;   // Try StopGain Exit

                trades.Add(trade);
            }

            // 
            StartOrReset();
            
            if(type != 0 || currentTime >= eodTime) return;
            
            var change= (decimal)setup.DynamicLoss.Value; // %

            var imax = _ticks.FindLastIndex(t => t.Value * (1 - change) >= tick.Value );
            var imin = _ticks.FindLastIndex(t => t.Value * (1 + change) <= tick.Value );
            type = imax < 0 && imin < 0 ? 0 : imin < imax ? 1 : -1;
            
            if (type == 0) return;

            trade = new Trade(Guid.NewGuid(), setup.SetupId, type, tick.Time, setup.Capital.Value);
            trade.Symbol = tick.Symbol;
            trade.EntryValue = type > 0
                ? 5 * Math.Ceiling(_ticks[imax].Value * (1 - change) / 5)
                : 5 * Math.Floor(_ticks[imin].Value * (1 + change) / 5);
            trade.StopLoss = type > 0
                ? 5 * Math.Ceiling(trade.EntryValue * (1 - (decimal)setup.StaticLoss.Value) / 5)
                : 5 * Math.Floor(trade.EntryValue * (1 + (decimal)setup.StaticLoss.Value) / 5);
            trade.StopGain = type > 0
                ? 5 * Math.Floor(trade.EntryValue * (1 + (decimal)setup.StaticGain.Value) / 5)
                : 5 * Math.Ceiling(trade.EntryValue * (1 - (decimal)setup.StaticGain.Value) / 5);
            
            trade.ExitTime = tick.Time;
            trade.ExitValue = trade.EntryValue;
            
            Send(trade, trade.Type);
        }

        public override void StartOrReset()
        {
            if (eodTime.Date == currentTime.Date) return;

            var h = eodTime > dbvsp ? 9 : currentTime.Hour > 10 ? eodTime.Hour - setup.DayTradeDuration : currentTime.Hour;
            this.eodTime = FixEODTime(currentTime.Date.AddHours(h + setup.DayTradeDuration));

            if (eodTime.Date == DateTime.Today)
                OnStatusChanged("Id: " + setup.SetupId + " Bell: " + eodTime.ToShortTimeString());          

            this.type = 0;
            this.allow = setup.Allow;   // Allow both long and sell again.
            this.slippage = 25;

            _continue = false;
            _ticks.RemoveAll(t => t.Time.Date < eodTime.Date);
            _limit = int.Parse(setup.Parameters.Split(' ')[4]);
            _last = _candles.FindLast(c => c.OpenTime.Date < eodTime.Date);
            _candles.RemoveAll(c => c.OpenTime.Date < eodTime.Date);
            if (_last == null) _last = _candles.FirstOrDefault();
        }

        public override bool Filter(Tick tick)
        {
            return !tick.Symbol.Contains("IND");
        }

        #endregion

        #region Specific functions

        private Candle Indicadors(bool closedcandle)
        {
            if (!closedcandle && candles.Last().Indicators != null)
            {
                if (_candles.Count == 0) return candles.Last();
                if (_last.OpenTime.AddMinutes(2 * setup.TimeFrame) > currentTime) return _last;
                _candles.RemoveAll(c => c.OpenTime.AddMinutes(setup.TimeFrame) < currentTime);
                return _candles.FirstOrDefault();
            }

            var par = setup.Parameters.Split(' ');
            var optInMAType = (par[0] == "Sma") ? Core.MAType.Sma : Core.MAType.Ema;
            var optInTimePeriod = int.Parse(par[1]);
            var optInNbDevUp = double.Parse(par[2]);
            var optInNbDevDn = double.Parse(par[3]);

            var startIdx = 0;
            var outNBElement = 0;
            var outBegIdx = Core.BbandsLookback(optInTimePeriod, optInNbDevUp, optInNbDevDn, optInMAType);
            var inReal = (from c in candles select (double)c.CloseValue).ToArray();
            var endIdx = inReal.Length;
            if (endIdx < outBegIdx) return null;

            // Bollinger Bands -------------------------------------------
            var outRealUpperBand = new double[endIdx - outBegIdx];
            var outRealLowerBand = new double[endIdx - outBegIdx];
            var outRealMiddleBand = new double[endIdx - outBegIdx];

            Core.Bbands(startIdx, endIdx - 1, inReal, optInTimePeriod, optInNbDevUp, optInNbDevDn, optInMAType,
                out outBegIdx, out outNBElement, outRealUpperBand, outRealMiddleBand, outRealLowerBand);

            // Return the result
            for (var i = outBegIdx; i < inReal.Length; i++)
            {
                if (candles[i].Indicators != null) continue;
                
                candles[i].Indicators = (
                    5 * Math.Ceiling(outRealUpperBand[i - outBegIdx] / 5) + " " +
                    5 * Math.Floor(outRealLowerBand[i - outBegIdx] / 5));
            }
            
            _candles = candles.FindAll(c =>
            {
                if (c.Indicators == null) return false;
                return c.OpenTime > setup.OfflineStartTime;
            });

            if (_candles.Last().OpenTime > DateTime.Today) OnStatusChanged("Último Candle:" + _candles.Last().ToString());

            return _candles.Last();
        }
        private void TrailingGain()
        {
            if (trade.Result >= 0) return;

            var tsg = trade.Type * (trade.ExitValue + (decimal?)setup.StaticGain * type);
            if (tsg < trade.Type * trade.StopGain)
                trade.StopGain = trade.Type > 0
                    ? Math.Max(trade.EntryValue, tsg.Value / trade.Type)
                    : Math.Min(trade.EntryValue, tsg.Value / trade.Type);
        }

        #endregion
    }
}
