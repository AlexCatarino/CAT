using System;
using System.Collections.Generic;
using System.Linq;
using TicTacTec.TA.Library;

namespace CAT.Model
{
    [Serializable]
    class ICap001 : Strategy
    {
        # region Fields

        private int _limit;
        private bool _continue;
        private Candle _last;
        private List<Tick> _ticks;
        private List<Candle> _candles;
        
        #endregion
        public ICap001()
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
            base.WarmUp(ticks);
            Indicadors(true);
            this._ticks = new List<Tick>();
            _last = _candles.FirstOrDefault();
        }

        public override void Run(Tick tick)
        {
            currentTime = tick.Time;
            if (Filter(tick)) return;

            _ticks.Add(tick);
            _ticks.RemoveAll(t => t.Time < tick.Time.AddMinutes(-30));
            var isclosed = MakeCandle(tick);

            if (type != 0)
            {
                trade.ExitTime = tick.Time;
                trade.ExitValue = tick.Value;

                //TrailingStop();

                var eod = trade.ExitTime >= eodTime;
                var gain = type * trade.ExitValue >= trade.StopGain * type;
                var loss = type * trade.ExitValue <= trade.StopLoss * type;
                if (loss || gain || eod) type = 0;
                action = type == 0 ? -trade.Type : 0;

                Send(trade, action);

                if (action == 0) return;
                
                _continue = true;
                
                if (eod) eodTime = trade.ExitTime.Value;   // Is EOD?

                trade.ExitValue =
                    trade.ExitTime > DateTime.Today ? trade.ExitValue :   // Is EOD
                    loss ? trade.StopLoss - trade.Type * slippage :  // Try StopLoss Exit
                    gain ? trade.StopGain : trade.ExitValue;   // Try StopGain Exit

                trades.Add(trade);

                if (loss) 
                    _ticks.RemoveAll(t => t.Time < trade.EntryTime);
            }

            // 
            StartOrReset();
            
            if(type != 0 || currentTime >= eodTime) return;
            
            #region Test for band cross

            _last = Indicadors(isclosed);

            if (_last == null || _last.Indicators == null) return;

            var bands = _last.Indicators.Split(' ');
            
            var index = Array.FindLastIndex(new bool[] 
            { 
                             allow != "C" && decimal.Parse(bands[0]) <= tick.Value,
                             allow != "V" && decimal.Parse(bands[1]) >= tick.Value,
                _continue && allow != "C" && decimal.Parse(bands[0]) <= trades.Last().ExitValue,
                _continue && allow != "V" && decimal.Parse(bands[1]) >= trades.Last().ExitValue
            }, c => c == true);

            if (index < 0) { _continue = false; return; };
            
            #endregion
            
            type = index % 2 != 0 ? 1 : -1;
            var tick_Value = index > 1 ? trades.Last().ExitValue.Value : tick.Value;

            trade = new Trade(Guid.NewGuid(), setup.SetupId, type, tick.Time, setup.Capital.Value);
            trade.Symbol = tick.Symbol;
            trade.EntryValue = type > 0 ? _ticks.Max(t => t.Value) - _limit : _ticks.Min(t => t.Value) + _limit;
            
            if (type > 0) type = tick_Value <= trade.EntryValue ? type : 0;
            if (type < 0) type = tick_Value >= trade.EntryValue ? type : 0;
            if (type == 0) { _continue = false; return; }

            //if (Math.Abs(tick_Value - trade.EntryValue) >= setup.StaticLoss)
            //{

            //    if (type > 0) _ticks.RemoveRange(0, _ticks.FindLastIndex(t => t.Value - _limit >= tick_Value));
            //    if (type < 0) _ticks.RemoveRange(0, _ticks.FindLastIndex(t => t.Value + _limit <= tick_Value));
            //    trade.EntryValue = type > 0 ? _ticks.Max(t => t.Value) - _limit : _ticks.Min(t => t.Value) + _limit;
            //}

            trade.StopLoss = trade.EntryValue - (decimal)setup.StaticLoss * type;
            trade.StopGain = trade.EntryValue + (decimal)setup.StaticGain * type;
            trade.ExitValue = tick.Value;
            trade.ExitTime = currentTime;
            
            Send(trade, trade.Type);   
        }

        public override void StartOrReset()
        {
            if (trades.Count == 0) _continue = false;
            if (eodTime.Date == currentTime.Date) return;

            var h = eodTime > dbvsp ? 9 : currentTime.Hour > 10 ? eodTime.Hour - setup.DayTradeDuration : currentTime.Hour;
            this.eodTime = FixEODTime(currentTime.Date.AddHours(h + setup.DayTradeDuration));

            if (eodTime.Date == DateTime.Today)
                OnStatusChanged("Id: " + setup.SetupId + " Bell: " + eodTime.ToShortTimeString());          

            this.type = 0;
            this.allow = setup.Allow;   // Allow both long and sell again.
            this.slippage = 0;

            _continue = false;
            _ticks.RemoveAll(t => t.Time.Date < eodTime.Date);
            _limit = int.Parse(setup.Parameters.Split(' ')[4]);
            _last = _candles.FindLast(c => c.OpenTime.Date < eodTime.Date);
            _candles.RemoveAll(c => c.OpenTime.Date < eodTime.Date);
            if (_last == null && _candles.Count() > 0) _last = _candles[1];
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
                if (_candles.Count == 1) return candles.Last();

                var index = _candles.FindIndex(c => c.OpenTime.AddMinutes(setup.TimeFrame) > currentTime) - 1;
                
                if (index < 0) return null;
                if (index > 0) _candles.RemoveRange(0, index);

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

            var isOnline = currentTime > DateTime.Today;
            if (isOnline) OnStatusChanged("Último Candle: " + candles.Last().ToString());

            _candles = isOnline
                ? candles.GetRange(candles.Count() - 1, 1)
                : candles.FindAll(c => c.Indicators != null && c.OpenTime > setup.OfflineStartTime);

            return _candles.Last();
        }
        private void TrailingStop()
        {
            if (trade.Result < (decimal?)setup.StaticGain) return;

            var tsg = trade.Type * (trade.ExitValue - 50 * type);
            if (tsg > trade.Type * trade.StopLoss)
                trade.StopLoss = trade.Type > 0
                    ? Math.Max(trade.EntryValue, tsg.Value / trade.Type)
                    : Math.Min(trade.EntryValue, tsg.Value / trade.Type);
        }

        #endregion
    }
}
