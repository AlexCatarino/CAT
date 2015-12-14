using System;
using System.Collections.Generic;
using System.Linq;
using TicTacTec.TA.Library;

namespace CAT.Model
{
    [Serializable]
    class IBand : Strategy
    {
        public IBand() : base()
        {
            this.slippage = 10;
            this.ticks = new List<Tick>();
            this.candles = new List<Candle>();
            this._candles = new List<Candle>();
        }

        #region Override functions
        public override void WarmUp(List<Tick> ticks)
        {
            base.WarmUp(ticks);
            Indicadors(true);
            _candles = new List<Candle>(candles);
            _candles.RemoveAll(c => c.Indicators[0] == '0');
        }
        public override void Run(Tick tick)
        {
            currentTime = tick.Time;
            var eod = currentTime >= eodTime;
            if (Filter(tick)) return;

            #region While trading:
            if (type != 0)
            {
                trade.ExitTime = tick.Time;
                trade.ExitValue = tick.Value;

                // Try StopLoss Exit
                if (type != 0 && type * tick.Value < trade.StopLoss * type)
                {
                    trade.ExitValue = trade.StopLoss;
                    type = 0;
                }

                // Try StopGain Exit
                if (type != 0 && type * tick.Value >= trade.StopGain * type)
                {
                    trade.ExitValue = trade.StopGain;
                    type = 0;
                }

                // Is EOD?
                if (type != 0 && tick.Time >= eodTime) type = 0;

                if (type == 0)
                {
                    cantrade = false;
                    trade.Cost = 12;
                    trades.Add(trade);
                }
            }
            #endregion
            // 
            StartOrReset();
            Indicadors(MakeCandle(tick));

            if (type == 0 && eodTime > tick.Time && cantrade)
            {
                if (_candles.Count == 0 || _candles[0].OpenTime.AddMinutes(setup.TimeFrame) > tick.Time) return;
                type = int.Parse(_candles[0].Indicators.Split(';')[0]);
                
                trade = new Trade
                {
                    Qnty = 1,
                    Type = type,
                    Symbol = setup.Symbol,
                    EntryTime = tick.Time,
                    EntryValue = tick.Value + slippage * type,
                    StopLoss = tick.Value - (decimal)setup.StaticLoss * type,
                    StopGain = tick.Value + (decimal)setup.StaticGain * type,
                };    
            }
        }
        public override void StartOrReset()
        {
            if (eodTime.Date != tick.Time.Date)
            {
                eodTime = eodTime.AddHours(-setup.DayTradeDuration);
                var h = eodTime > dbvsp ? 9 : tick.Time.Hour > 10
                    ? eodTime.Hour : tick.Time.Hour;
                eodTime = tick.Time.Date.AddHours(h + setup.DayTradeDuration);
                candlecloseTime = eodTime.AddHours(-setup.DayTradeDuration);
                if (tick.Time.Date == DateTime.Today) this.slippage = 5;
                _candles.RemoveAll(c => eodTime.Date > c.OpenTime);
                cantrade = true;
            }
        }
        #endregion

        # region Specific variables
        private bool cantrade;
        private List<Candle> _candles;
        #endregion

        #region Specific functions
        private void Indicadors(bool closedcandle)
        {
            if (!closedcandle) return;
            
            _candles.Clear();
            var startIdx = 0;
            var outNBElement = 0;
            
            var par = setup.Parameters.Split(' ');
            var optInMAType = (par[0] == "Sma") ? Core.MAType.Sma : Core.MAType.Ema;
            var optInTimePeriod = int.Parse(par[1]);
            var optInNbDevUp = double.Parse(par[2]);
            var optInNbDevDn = double.Parse(par[3]);
            var optInFastPeriod = int.Parse(par[4]);
            var optInSlowPeriod = int.Parse(par[5]);
            var optInSignalPeriod = int.Parse(par[6]);

            var outBegIdx = new int[] 
            {
                Core.BbandsLookback(optInTimePeriod, optInNbDevUp, optInNbDevDn, optInMAType),
                Core.MacdLookback(optInFastPeriod, optInSlowPeriod, optInSignalPeriod) + 1,
            };
            var inReal = (from c in candles select (double)c.CloseValue).ToArray();
            var endIdx = inReal.Length;
            if (endIdx < outBegIdx.Max()) return;

            // Bollinger Bands -------------------------------------------
            var outRealUpperBand = new double[endIdx - outBegIdx[0]];
            var outReAllowerBand = new double[endIdx - outBegIdx[0]];
            var outRealMiddleBand = new double[endIdx - outBegIdx[0]];

            Core.Bbands(startIdx, endIdx - 1, inReal, optInTimePeriod, optInNbDevUp, optInNbDevDn, optInMAType,
                out outBegIdx[0], out outNBElement, outRealUpperBand, outRealMiddleBand, outReAllowerBand);

            // MACD Histo ------------------------------------------------
            var outMACDHist = new double[endIdx - outBegIdx[1] + 1];
            var outMACD = new double[endIdx - outBegIdx[1] + 1];
            var outMACDSignal = new double[endIdx - outBegIdx[1] + 1];

            if (outBegIdx[1] > 0)
                Core.MacdExt(startIdx, endIdx - 1, inReal, optInFastPeriod, optInMAType, optInSlowPeriod, optInMAType, optInSignalPeriod, optInMAType,
                    out outBegIdx[1], out outNBElement, outMACD, outMACDSignal, outMACDHist);

            // Return the result
            for (var i = 0; i < inReal.Length; i++)
            {
                if (i <= outBegIdx.Max())
                {
                    candles[i].Indicators = "0";
                    continue;
                }

                var value = new double[]
                {
                    outRealUpperBand[i - outBegIdx[0]],
                    outReAllowerBand[i - outBegIdx[0]],
                    outMACDHist[i - outBegIdx[1] - 1],
                    outMACDHist[i - outBegIdx[1]]
                };

                var type = 0;
                if (value[2] < value[3] && candles[i].MinValue <= (decimal)value[1] && setup.Allow != "S") 
                    type = 1;
                if (value[2] > value[3] && candles[i].MaxValue >= (decimal)value[0] && setup.Allow != "L")
                    type = -1;
                if (i < inReal.Length - 1 && candles[i + 1].OpenTime.Date != candles[i].OpenTime.Date)
                    type = 0;

                candles[i].Indicators = (type + ";" +
                    value[0].ToString() + ";" + value[1].ToString() + ";" +
                    value[2].ToString() + ";" + value[3].ToString());
            }
            if (candles.Last().Indicators[0] != '0') _candles.Add(candles.Last());
        }
        #endregion
    }
}
