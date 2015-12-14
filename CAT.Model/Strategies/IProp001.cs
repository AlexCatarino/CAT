using System;
using System.Collections.Generic;
using System.Linq;
using TicTacTec.TA.Library;

namespace CAT.Model
{
    [Serializable]
    class IProp001 : Strategy
    {
        # region Fields

        private Candle _last;
        private List<Tick> _ticks;
        private List<Candle> _candles;
        
        #endregion
        public IProp001()
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
            ticks.RemoveAll(t => t.Time < setup.OfflineStartTime);
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
            _ticks.RemoveAll(t => t.Time < tick.Time.AddMinutes(-15));
            var isclosed = MakeCandle(tick);

            if (type != 0)
            {
                trade.ExitTime = tick.Time;
                trade.ExitValue = tick.Value;

                TrailingGain();

                var eod = trade.ExitTime >= eodTime;
                var gain = type * trade.ExitValue >= trade.StopGain * type;
                var loss = type * trade.ExitValue <= trade.StopLoss * type;
                if (loss || gain || eod) type = 0;
                action = type == 0 ? -trade.Type : 0;

                Send(trade, action);

                if (action == 0) return;

                if (eod) eodTime = trade.ExitTime.Value;   // Is EOD?

                trade.ExitValue =
                    trade.ExitTime > DateTime.Today ? trade.ExitValue :   // Is EOD
                    loss ? trade.StopLoss - trade.Type * slippage :  // Try StopLoss Exit
                    gain ? trade.StopGain : trade.ExitValue;   // Try StopGain Exit

                trades.Add(trade);
            }

            // 
            StartOrReset();
            
            if(type != 0 || currentTime >= eodTime) return;
            
            _last = Indicadors(isclosed);

            if (_last == null || _last.Indicators == null) return;

            var ind = _last.Indicators.Split(' ');
            var gsv = decimal.Parse(ind[0]);
            var stretchloss = decimal.Parse(ind[1]);
            var stretchgain = stretchloss * (decimal)setup.StaticGain.Value;

            if (tradlng == null || tradsht == null)
            {
                tradlng = new Trade(Guid.NewGuid(), setup.SetupId, 1, currentTime, setup.Capital.Value);
                tradlng.Symbol = tick.Symbol;
                tradlng.EntryValue = Math.Floor((tick.Value - gsv) / 5) * 5;
                //tradlng.StopLoss = Math.Ceiling((tick.Value + stretchloss) / 5) * 5;
                //tradlng.StopGain = Math.Floor((tick.Value - stretchgain) / 5) * 5;

                tradsht = new Trade(Guid.NewGuid(), setup.SetupId, -1, currentTime, setup.Capital.Value);
                tradsht.Symbol = tick.Symbol;
                tradsht.EntryValue = Math.Ceiling((tick.Value + gsv) / 5) * 5;
                //tradsht.StopLoss = Math.Floor((tick.Value - stretchloss) / 5) * 5;
                //tradsht.StopGain = Math.Ceiling((tick.Value + stretchgain) / 5) * 5;
            }

            if ((allow == "A" || allow == "C") && tick.Value <= tradlng.EntryValue) type = 1;
            if ((allow == "A" || allow == "V") && tick.Value >= tradsht.EntryValue) type = -1;

            if (type == 0) return;
            trade = type > 0 ? tradlng : tradsht;
            trade.ExitTime = currentTime;
            trade.EntryTime = currentTime;
            trade.ExitValue = tick.Value;
            Send(trade, trade.Type);

            allow = allow == "A" ? type > 0 ? "V" : "C" : string.Empty;
            
            
            //Send(trade, trade.Type);
        }

        public override void StartOrReset()
        {
            if (eodTime.Date == currentTime.Date) return;

            var timeexit = int.Parse(setup.Parameters.Split(' ')[4]) - 1;
            var h = eodTime > dbvsp ? 9 : currentTime.Hour > 10 ? eodTime.Hour - setup.DayTradeDuration : currentTime.Hour;


            this.tradlng = null;
            this.tradsht = null; 
            this.type = 0;
            this.slippage = 25;
            this.allow = setup.Allow;   // Allow both long and sell again.
            this.eodTime = FixEODTime(currentTime.Date.AddHours(h + setup.DayTradeDuration)).AddDays(timeexit);

            if (currentTime.Date == DateTime.Today)
                OnStatusChanged("Id: " + setup.SetupId + " TimeExit: " + eodTime.ToShortTimeString());

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

                var index = _candles.FindIndex(c => c.OpenTime == currentTime.Date) - 1;

                if (index < 0) return null;
                _candles.RemoveRange(0, index);
                
                return _candles.FirstOrDefault();
            }

            var par = setup.Parameters.Split(' ');
            var gsvlen = int.Parse(par[0]);
            var gsvidx = int.Parse(par[1]);
            var atrlen = int.Parse(par[2]);
            var atridx = int.Parse(par[3]);

            #region Loop for GSV
            for (var i = gsvlen - 1; i < candles.Count(); i++)
            {
                var sum = 0.0m;
                var stretch = 0.0m;

                for (var j = gsvlen - 1; j >= 0; j--)
                {
                    var curcandle = candles[i - j];

                    if (curcandle.CloseValue == curcandle.OpenValue)
                        stretch = Math.Min(curcandle.OpenValue - curcandle.MinValue, curcandle.MaxValue - curcandle.OpenValue);

                    if (curcandle.CloseValue > curcandle.OpenValue)
                        stretch = curcandle.OpenValue - curcandle.MinValue;

                    if (curcandle.CloseValue < curcandle.OpenValue)
                        stretch = curcandle.MaxValue - curcandle.OpenValue;

                    sum += stretch;
                }
                candles[i].Indicators = (gsvidx * sum / gsvlen).ToString() + " " + stretch.ToString();
            }
            #endregion

            #region Loop for ATR
            for (var i = atrlen - 1; i < candles.Count(); i++)
            {
                var sum = 0m;
                var curcandle = candles[i];
                var prevclose = candles[i - 1].CloseValue;
                var max = (new decimal[3] { 
                    curcandle.MaxValue - curcandle.MinValue, 
                    Math.Abs(prevclose - curcandle.MaxValue), 
                    Math.Abs(prevclose - curcandle.MinValue),
                }).Max();

                if (i == atrlen - 1)
                {
                    for (var j = 1; j < atrlen; j++)
                    {
                        curcandle = candles[i - j];
                        if (i == j)
                            max += curcandle.MaxValue - curcandle.MinValue;
                        else
                        {
                            prevclose = candles[i - j - 1].CloseValue;
                            max += (new decimal[3] { 
                                curcandle.MaxValue - curcandle.MinValue, 
                                Math.Abs(prevclose - curcandle.MaxValue), 
                                Math.Abs(prevclose - curcandle.MinValue)
                            }).Max();
                        }
                    }
                    sum = max / atrlen;
                }
                else
                {
                    var prevvalue = decimal.Parse(candles[i - 1].Indicators.Split(' ')[2]) / atridx;
                    sum = (prevvalue * (atrlen - 1) + max) / atrlen;
                }

                if (candles[i].Indicators == null) candles[i].Indicators = "0 0";
                candles[i].Indicators = candles[i].Indicators + " " + (atridx * sum).ToString();
            }
            #endregion

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
