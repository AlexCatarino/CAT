using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;

namespace CAT.Model
{
    [Serializable]
    class ICap003 : Strategy
    {
        # region Fields
        private double _max;
        private double _min;
        private bool _pregain;
        private DateTime _open;
        private DateTime _clse;
        private Candle _candle;
        #endregion

        #region Constructor / Destructor

        public ICap003()
            : base()
        {
            _candle = new Candle();
        }
        #endregion

        #region Public override functions

        public override void WarmUp(List<Tick> ticks)
        {
            // Empty, because ICandle does not need it
        }

        public override IEnumerable<Tick> Datafilter(IEnumerable<Tick> ticks)
        {
            return ticks.Where(t => t.Time > DateTime.Today || t.Time >= this.setup.OfflineStartTime);
        }

        public override void Run(Tick tick)
        {
            currentTime = tick.Time;
            if (Filter(tick)) return;

            if (type != 0)
            {
                trade.ExitTime = tick.Time;
                trade.ExitValue = tick.Value;

                TrailingStop();

                var eod = currentTime >= eodTime;
                var loss = type * tick.Value <= trade.StopLoss * type;
                var gain = type * tick.Value >= trade.StopGain * type;
                if (loss || gain || eod) type = 0;
                action = type == 0 ? -trade.Type : 0;

                #region Review Later
                //if (action == 0 && trade.Result >= setup.StaticGain - 100)
                //{
                //    var change = false;
                //    if (!_pregain)
                //    {
                //        change = true;
                //        _pregain = true;
                //    }
                //    action = change ? -2 : 0;
                //}
                #endregion
                Send(trade, action);

                if ((action == 0 || action == -2) && !gain) return;

                if (eod || gain) eodTime = trade.ExitTime.Value;

                trade.ExitValue =
                    gain ? trade.StopGain :   // Try StopLoss Exit
                    loss ? trade.StopLoss - slippage * trade.Type :   // Try StopGain Exit
                    trade.ExitValue;          // Is EOD

                trades.Add(trade);

                if (loss)
                {
                    type = -trade.Type;
                    trade = new Trade(Guid.NewGuid(), setup.SetupId, type, currentTime, 1);
                    trade.Symbol = tick.Symbol;
                    trade.EntryValue = tick.Value;
                    trade.StopLoss = trade.EntryValue - (decimal)setup.StaticLoss * type;
                    trade.StopGain = trade.EntryValue + (decimal)setup.StaticGain * type;
                    trade.ExitValue = tick.Value;
                    trade.ExitTime = currentTime;
                    allow = allow == "A" ? type > 0 ? "V" : "C" : string.Empty;
                }
            }

            // 
            StartOrReset();

            // Return while ref candle has not oppened
            if (type != 0 || currentTime < _open || currentTime >= eodTime) return;

            // Get ref candle extremes
            if (currentTime < _clse)
            {
                if (_candle.OpenValue == 0)
                {
                    _candle.OpenValue = tick.Value;
                    _candle.CloseValue = tick.Value;
                    _candle.MaxValue = tick.Value;
                    _candle.MinValue = tick.Value;
                }
                _candle.CloseValue = tick.Value;
                _candle.MaxValue = Math.Max(_candle.MaxValue, tick.Value);
                _candle.MinValue = Math.Min(_candle.MinValue, tick.Value);
                action = 0; return;
            }

            if (_candle.OpenValue == 0) return; // Return if no candle was added

            type = Math.Sign(_candle.OpenValue - _candle.CloseValue);

            if ((type < 0 && allow == "C") || (type > 0 && allow == "V")) type = 0;
            if (type == 0) return;

            trade = new Trade(Guid.NewGuid(), setup.SetupId, type, currentTime, 1);
            trade.Symbol = tick.Symbol;
            trade.EntryValue = tick.Value;
            trade.StopLoss = trade.EntryValue - (decimal?)setup.StaticLoss * type;
            trade.StopGain = trade.EntryValue + (decimal?)setup.StaticGain * type;
            trade.ExitValue = tick.Value;
            trade.ExitTime = currentTime;
            allow = allow == "A" ? type > 0 ? "V" : "C" : string.Empty;

            
            //var otick = tick;

            //if (canLong && string.IsNullOrEmpty(tradlng.Symbol))
            //{
            //    tradlng.Symbol = otick.Symbol;
            //    tradlng.EntryValue = otick.Value - setup.StaticGain.Value;
            //    tradlng.StopLoss = tradlng.EntryValue - setup.StaticLoss;
            //    tradlng.StopGain = otick.Value;
            //    Send(tradlng, 0);
            //}

            //if (canShor && string.IsNullOrEmpty(tradsht.Symbol))
            //{
            //    tradsht.Symbol = otick.Symbol;
            //    tradsht.EntryValue = otick.Value + setup.StaticGain.Value;
            //    tradsht.StopLoss = tradsht.EntryValue + setup.StaticLoss;
            //    tradsht.StopGain = otick.Value;
            //    Send(tradsht, 0);
            //}

            //if (canLong && tick.Value <= tradlng.EntryValue) type = 1;
            //if (canShor && tick.Value >= tradsht.EntryValue) type = -1;

            //if (type == 0) return;

            //trade = type > 0 ? tradlng : tradsht;
            //trade.ExitValue = tick.Value;
            //trade.ExitTime = currentTime;
            //trade.EntryTime = currentTime;
            //Send(trade, 1);

            //allow = allow == "A" ? type > 0 ? "V" : "C" : string.Empty;
        }

        public override void StartOrReset()
        {
            if (_open.Date == currentTime.Date) return;

            if (eodTime.Year > 1) eodTime.AddHours(-setup.DayTradeDuration);
            type = eodTime > dbvsp ? 9 : currentTime.Hour > 10 ? Math.Max(9, eodTime.Hour) : currentTime.Hour;
            eodTime = currentTime.Date.AddHours(type + setup.DayTradeDuration);

            var h = int.Parse(setup.Parameters) - 1;

            _open = h >= 0
                ? eodTime.AddHours(-setup.DayTradeDuration).AddMinutes(setup.TimeFrame * h)
                : eodTime.Date.AddMinutes(570).Add(
                    TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time").GetUtcOffset(eodTime) -
                    TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time").GetUtcOffset(eodTime));

            _clse = _open.AddMinutes(setup.TimeFrame);

            this.tradlng = new Trade(Guid.NewGuid(), setup.SetupId, 1, _clse, 1);
            this.tradsht = new Trade(Guid.NewGuid(), setup.SetupId, -1, _clse, 1);
            this.slippage = setup.Symbol.Contains("IND") ? 10 : 0;

            eodTime = FixEODTime(eodTime);

            if (eodTime.Date == DateTime.Today)
                OnStatusChanged("Id: " + setup.SetupId + " Bell: " + eodTime.ToShortTimeString() +
                    " Abe: " + _open.ToShortTimeString() +
                    " Fech: " + _clse.ToShortTimeString());

            allow = setup.Allow;   // Allow both long and sell again.
            _candle = new Candle { OpenTime = _open };
            _pregain = false;
            _max = 0;
            _min = 0;
            type = 0;
        }

        #endregion

        #region Private functions

        private void TrailingStop()
        {

            if (trade.NetResult <= 0 || !setup.DynamicLoss.HasValue) return;
            var tsl = type * (trade.ExitValue - (decimal?)setup.DynamicLoss * type);
            if (tsl > type * trade.StopLoss) trade.StopLoss = tsl / type;
        }

        #endregion
    }
}
