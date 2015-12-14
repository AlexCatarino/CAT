using System;
using System.Collections.Generic;
using System.Linq;
using TicTacTec.TA.Library;

namespace CAT.Model
{
    [Serializable]
    class ACap001 : Strategy
    {
        # region Fields

        private List<Candle> _candles;
        
        #endregion

        #region Constructor / Destructor
        
        public ACap001() : base()
        {
            this.hour = 10;
            this.ticks = new List<Tick>();
            this.candles = new List<Candle>();
            this._candles = new List<Candle>();
        }
        #endregion

        #region Public override functions

        public override void WarmUp(List<Tick> ticks)
        {
            base.WarmUp(ticks);
            Indicadors(true);
        }

        public override void Run(Tick tick)
        {
            currentTime = tick.Time;
            if (Filter(tick)) return;

            if (type != 0)
            {
                trade.ExitTime = tick.Time;
                trade.ExitValue = tick.Value;

                var loss = type * tick.Value <= trade.StopLoss * type;
                var gain = type * tick.Value >= trade.StopGain * type;
                var eod = currentTime >= eodTime;
                action = gain || loss || eod ? -1 : 0;
                Send(trade, action);

                if (action == 0) return;

                // Is EOD?
                if (eod || gain) eodTime = tick.Time;

                // Try StopLoss Exit
                if (loss) trade.ExitValue = trade.StopLoss;

                // Try StopGain Exit
                if (gain) trade.ExitValue = trade.StopGain;

                type = 0;
                action = 1;
                trades.Add(trade);
            }

            // 
            StartOrReset();
            var last = Indicadors(MakeCandle(tick));

            // Return while ref candle has not oppened
            if (type != 0 || last == null || currentTime >= eodTime) return;

            type = int.Parse(last.Indicators.Split(' ')[0]);

            trade = new Trade(Guid.NewGuid(), setup.SetupId, type, tick.Time, setup.Capital.Value);
            trade.Symbol = tick.Symbol;
            trade.EntryValue = type > 0 ? Math.Round(last.MaxValue + slippage, 2) : Math.Round(last.MinValue - slippage, 2);
            trade.StopLoss = type > 0 ? Math.Round(last.MinValue - slippage, 2) : Math.Round(last.MaxValue + slippage, 2);
            trade.StopGain = trade.EntryValue + Math.Round(trade.EntryValue - trade.StopLoss.Value, 2);

            action = 0;
            Send(trade, 0);
            
            if (type * tick.Value < trade.EntryValue * type) type = 0;

            if (type == 0) return;
            trade.ExitTime = tick.Time;
            trade.EntryTime = tick.Time;
            trade.ExitValue = tick.Value;
            Send(trade, 1);

            _candles.RemoveAll(c => c.OpenTime < tick.Time);
        }

        public override void StartOrReset()
        {
            if (eodTime.Date == tick.Time.Date) return;

            var h = eodTime > dbvsp ? 10 : tick.Time.Hour > 11 ? eodTime.Hour : tick.Time.Hour;
            eodTime = tick.Time.Date.AddHours(h + setup.DayTradeDuration);

            type = 0;
            allow = setup.Allow;   // Allow both long and sell again.
            if (tick.Time.Date == DateTime.Today)
            {
                OnStatusChanged("Id: " + setup.SetupId +
                    " Bell: " + eodTime.ToShortTimeString());
            }
            else
            {
                if (tick.Time.Date >= new DateTime(2012, 12, 3) && tick.Time.Date <= new DateTime(2013, 7, 5))
                {
                    eodTime = eodTime.AddMinutes(30);
                }
            }
        }

        #endregion

        #region Specific functions

        private Candle Indicadors(bool closedcandle)
        {
            if (!closedcandle)
            {
                var last = _candles.FindLast(c => tick.Time >= c.OpenTime.AddMinutes(setup.TimeFrame));
                if (last == null || last.OpenTime.Date != tick.Time.Date) return null;

                //if (tick.Time > last.OpenTime.AddMinutes(2 * setup.TimeFrame))
                //{
                //    var indicator = last.Indicators;
                //    last = candles.FindLast(c => tick.Time >= c.OpenTime.AddMinutes(setup.TimeFrame));
                //    last.Indicators = indicator;
                //}
                
                return last;
            }

            var par = setup.Parameters.Split(' ');
            var optInMAType = (par[0] == "Sma") ? Core.MAType.Sma : Core.MAType.Ema;
            var optInTimePeriod = int.Parse(par[1]);
            var optInNbDevUp = double.Parse(par[2]);
            var optInNbDevDn = double.Parse(par[3]);
            var WRoptInTimePeriod = int.Parse(par[4]);
            var WRUpperValue = int.Parse(par[5]);
            var WRLowerValue = int.Parse(par[6]);

            var startIdx = 0;
            var outNBElement = 0;
            var inReal = (from c in candles select (double)c.CloseValue).ToArray();
            var inHigh = (from c in candles select (double)c.MaxValue).ToArray();
            var inLow = (from c in candles select (double)c.MinValue).ToArray();
            var endIdx = inReal.Length;
            
            // Bollinger Bands -------------------------------------------
            var outBegIdx = Core.BbandsLookback(optInTimePeriod, optInNbDevUp, optInNbDevDn, optInMAType);
            if (endIdx < outBegIdx) return null;

            var outRealUpperBand = new double[endIdx - outBegIdx];
            var outRealLowerBand = new double[endIdx - outBegIdx];
            var outRealMiddleBand = new double[endIdx - outBegIdx];

            Core.Bbands(startIdx, endIdx - 1, inReal, optInTimePeriod, optInNbDevUp, optInNbDevDn, optInMAType,
                out outBegIdx, out outNBElement, outRealUpperBand, outRealMiddleBand, outRealLowerBand);

            // Williams %R -------------------------------------------
            var WRoutBegIdx = Core.WillRLookback(WRoptInTimePeriod);
            if (endIdx < outBegIdx) return null;

            var outReal = new double[endIdx - WRoutBegIdx];
            Core.WillR(startIdx, endIdx - 1, inHigh, inLow, inReal, WRoptInTimePeriod, out WRoutBegIdx, out outNBElement, outReal);

            // Return the result
            for (var i = outBegIdx; i < inReal.Length; i++)
            {
                var bsig = outReal[i - WRoutBegIdx - 1] <= WRLowerValue && outRealLowerBand[i - outBegIdx] >= (double)candles[i].MinValue;
                var ssig = outReal[i - WRoutBegIdx - 1] >= WRUpperValue && outRealUpperBand[i - outBegIdx] <= (double)candles[i].MaxValue;
                
                var type = bsig ? 1 : ssig ? -1 : 0;

                candles[i].Indicators = (type + " " +
                    outRealMiddleBand[i - outBegIdx].ToString("#.00") + " " +
                    outRealUpperBand[i - outBegIdx].ToString("#.00") + " " +
                    outRealLowerBand[i - outBegIdx].ToString("#.00") + " " +
                    outReal[i - WRoutBegIdx - 1].ToString("#.00"));
            }

            _candles = candles.FindAll(c =>
                {
                    if (c.Indicators == null) return false;
                    if (c.OpenTime < setup.OfflineStartTime) return false;
                    return c.Indicators.Split(' ')[0] != "0";
                });

            return _candles.Last();
        }

        #endregion
    }
}
