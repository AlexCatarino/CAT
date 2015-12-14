using System;
using System.Collections.Generic;
using System.Linq;
using TicTacTec.TA.Library;

namespace CAT.Model
{
    [Serializable]
    class Empty : Strategy
    {
        # region Fields

        #endregion

        #region Constructor / Destructor
        
        public Empty() : base()
        {
            this.hour = 10;
        }
        #endregion

        #region Public override functions

        public override void WarmUp(List<Tick> ticks)
        {
            // Empty, because OCandle does not need it
        }

        public override void Run(Tick tick)
        {
            currentTime = tick.Time;
            var eod = currentTime >= eodTime;
            if (Filter(tick)) return;

            if (type != 0)
            {
                trade.ExitTime = tick.Time;
                trade.ExitValue = tick.Value;

                var loss = type * tick.Value <= trade.StopLoss * type;
                var gain = type * tick.Value >= trade.StopGain * type;
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


        }

        public override void StartOrReset()
        {
            
        }

        #endregion

        #region Specific functions

        private void Indicadors(bool closedcandle)
        {
            if (!closedcandle) return;
        }

        #endregion
    }
}


