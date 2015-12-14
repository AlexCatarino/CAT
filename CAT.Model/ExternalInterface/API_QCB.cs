using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using CAT.Model.ExternalInterface.JSONMessages;

namespace CAT.Model
{
    /// <summary>
    /// This class is the client that connects to Quantsis Connection Box
    /// </summary>
    public class API_QCB : IExternalComm, IDisposable
    {
        #region Fields

        /// <summary>
        /// esign Constant - electronic signature 
        /// </summary>
        private const string esign = "";

        /// <summary>
        /// PORT Constant - Quantsis Connection Box opened port to this kind of app
        /// </summary>
        private const int PORT = 4760;

        /// <summary>
        /// Client Socket
        /// </summary>
        private TcpClient socket;

        // Network Stream, Reader and Writer
        private NetworkStream stream;
        //private StreamReader streamReader;
        private StreamWriter streamWriter;

        /// <summary>
        /// CancellationTokenSource to cancel the running task
        /// </summary>
        private CancellationTokenSource cts;

        /// <summary>
        /// List of monitored securities
        /// </summary>
        private List<Security> securities;

        #endregion

        #region Constructor / Destructor

        /// <summary>
        /// Constructor
        /// </summary>
        public API_QCB()
        {
            this.cts = new CancellationTokenSource();
            this.securities = new List<Security>();
        }

        ~API_QCB()
        {
            this.disconnect();
        }

        public void Dispose()
        {
            cts.Dispose();      
        }
        #endregion

        #region Event Handler

        public event Action<Tick> TickArrived;
        public event Action<string> InfoChanged;
        public event Action<string> FeedChanged;

        #endregion

        #region Interface Functions : Subscribe, Start and SendTrade

        /// <summary>
        /// Creates the socket Thread and starts the connection process
        /// </summary>
        public void Start()
        {
            Task.Factory.StartNew(run, cts.Token);           
        }

        /// <summary>
        /// Subscribe symbol in DDE server 
        /// </summary>
        /// <param name="symbol">Symbol to subscribe</param>
        public void Subscribe(string symbol)
        {
            // TERMO FRACIONARIO FUTUROS
            var security = new List<Security>(1);
            security.Add(new Security(symbol, "ACOES", "BOVESPA"));

            #region If OPTION, define series
            if (symbol.Contains("#"))
            {
                var reff = DateTime.Now;
                reff = new DateTime(reff.Year, reff.Month, 15);
                if (reff.Day > DateTime.Now.Day) reff = reff.AddMonths(-1);
                while (reff.DayOfWeek != DayOfWeek.Monday) reff = reff.AddDays(1);
                var num = reff <= DateTime.Today ? 65 : 64;
                security[0].symbol = security[0].symbol.Replace('#', (char)(num + reff.Month));
                security[0].market = "OPCOES";
            }
            #endregion

            #region If INDFUT, define INDXYY
            if (symbol.Contains("FUT"))
            {
                var reff = DateTime.Now;
                reff = new DateTime(reff.Year, reff.Month, 12);
                if (reff.Day > DateTime.Now.Day) reff = reff.AddMonths(-1);
                if (reff.Month % 2 != 0) reff = reff.AddMonths(-1);
                while (reff.DayOfWeek != DayOfWeek.Wednesday) reff = reff.AddDays(1);
                if (reff.Month == 12) reff = reff.AddYears(1);

                security[0].symbol = security[0].symbol
                    .Replace("FUT", new Dictionary<int, char>
                    {   { 2, 'J' }, { 6, 'Q' }, { 10, 'Z' }, 
                        { 4, 'M' }, { 8, 'V' }, { 12, 'G' }
                    }[reff.Month] + reff.ToString("yy"));
                security[0].market = "INDICES";
                security[0].exchange = "BMF";
            }
            #endregion

            if (securities.Contains(security[0]))
            {
                NotifyQCB("QCB. O ativo " + symbol + " já foi adicionado");
                return;
            }

            securities.Add(security[0]);

            this.send(new QuoteRequest(security).Serialize());
        }

        /// <summary>
        /// Send the trade to OMS 
        /// </summary>
        /// <param name="setup">Setup</param>
        /// <param name="trade">Trade to send</param>
        /// <param name="command">How to process the trade</param>
        public void SendTrade(Setup setup, Trade trade, int command)
        {
            if (setup.How2Trade * command == 0) return;

            var side = command > 0
                ? trade.Type > 0 ? NewOrderRequest.BUY : NewOrderRequest.SELL
                : trade.Type > 0 ? NewOrderRequest.SELL : NewOrderRequest.BUY;

            var market = securities.Find(s => s.symbol == trade.Symbol).market;
            var price = command == -2 ? trade.StopGain : setup.How2Trade == 6 ? 0
                : trade.ExitValue + setup.Slippage * trade.Type;

            switch (command)
            {
                case 6:
                    {
                        var cmd = new NewOrderRequest(trade.Symbol, side, trade.Qnty.ToString(), price.ToString(), market, esign);
                        NotifyOMS(cmd.ToString());
                        //this.send(cmd.Serialize());
                        break;
                    }
                case 1:
                    {
                        var cmd = new NewStopOrderRequest(trade.Symbol, side, trade.Qnty.ToString(), price.ToString(), price.ToString(), market, esign);
                        NotifyOMS(cmd.ToString());
                        //this.send(cmd.Serialize());
                        break;
                    }
            }

            ///

        }

        #endregion

        #region Private Functions

        /// <summary>
        /// The socket thread code
        /// </summary>
        private async void run()
        {
            try
            {
                // Creates the socket
                socket = new TcpClient();
                // connects to Quantsis Conneciton Box
                await socket.ConnectAsync("localhost", PORT);
                
                // annouce its connected
                NotifyOMS("OK");
                NotifyQCB("QCB");

                if (!socket.Connected) disconnect();

                // Gets Network Stream 
                stream = socket.GetStream();
                streamWriter = new StreamWriter(stream);
                streamWriter.AutoFlush = true;

                using (var streamReader = new StreamReader(stream))
                {
                    while (!cts.IsCancellationRequested)
                    {
                        // reads incoming message
                        var responseData = await streamReader.ReadLineAsync();

                        // calls the method to process the incoming message
                        processMessage(responseData);
                    }
                }
            }
            catch (Exception e)
            {
                // if any exception is thrown, writes it in output
                if (InfoChanged != null) InfoChanged(e.Message + " - " + e.StackTrace);
            }
            finally
            {
                socket.Close();   // Closes the socket
                if (InfoChanged != null) InfoChanged("OFF CMA indisponível.");
                if (FeedChanged != null) FeedChanged("OFF Conexão ao QCB indisponível");
            }
        }

        /// <summary>
        /// Disconnects from Quantsis Connection Box
        /// </summary>
        public void disconnect()
        {
            this.cts.Cancel();
        }

        /// <summary>
        /// Process the incoming messages 
        /// </summary>
        /// <param name="message">Incoming message</param>
        private void processMessage(string message)
        {
            // Deserialize the incoming command to check wich message has just arrived
            // The action property brings the command name
            switch (Command.Deserialize(message).action)
            {
                case KeepAlive.CLASSNAME:
                    {   // If it is a KeepAlive message, just answer Quantsis Conneciton Box another KeepAlive message
                        
                        //CheckAsyncTaskInProgress
                        this.send(new KeepAlive().Serialize());
                        break;
                    }
                case NewOrderResponse.CLASSNAME:
                    {   // If it is an NewOrderResponse message and it contains error, show the error message
                        var cmd = NewOrderResponse.Deserialize(message);
                        if (!cmd.success) NotifyOMS(cmd.ToString());
                        break;
                    }
                case NewStopOrderResponse.CLASSNAME:
                    {   // If it is an NewStopOrderResponse message and it contains error, show the error message
                        var cmd = NewStopOrderResponse.Deserialize(message);
                        if (!cmd.success) NotifyOMS(cmd.ToString());
                        break;
                    }
                case NewStopGainLossOrderResponse.CLASSNAME:
                    {   // If it is an NewStopGainLossOrderResponse message and it contains error, show the error message
                        var cmd = NewStopGainLossOrderResponse.Deserialize(message);
                        if (!cmd.success) NotifyOMS(cmd.ToString());
                        break;
                    }
                case CancelOrderResponse.CLASSNAME:
                    {   // If it is an CancelOrderResponse message and it contains error, show the error message
                        var cmd = CancelOrderResponse.Deserialize(message);
                        if (!cmd.success) NotifyOMS(cmd.ToString());
                        break;
                    }
                case ExecutionReport.CLASSNAME:
                    {   // If it is an ExecutionReport message, check if it was successful and updates the order list or show error message
                        var cmd = ExecutionReport.Deserialize(message);
                        if (cmd.success)
                            NotifyOMS(cmd.ToString());
                        else
                            NotifyOMS(cmd.ToString());

                        break;
                    }
                case OrderListResponse.CLASSNAME:
                    {   // If it is an OrderListResponse, check if it was successful. In negative case show error message.
                        var cmd = OrderListResponse.Deserialize(message);
                        if (!cmd.success) NotifyOMS(cmd.ToString());
                        break;
                    }
                case PositionResponse.CLASSNAME:
                    {   // If it is a Position Response, check if it was successful and updates the position list. In case of failure, show error messasge
                        var cmd = PositionResponse.Deserialize(message);
                        if (cmd.success)
                            NotifyOMS(cmd.ToString());
                        else
                            NotifyOMS(cmd.ToString());

                        break;
                    }
                case QuoteResponse.CLASSNAME:
                    {   // If it is a Quote Response, check if it was successful and logs the message
                        var cmd = QuoteResponse.Deserialize(message);
                        if (cmd.success)
                            OnQuoteResponse(cmd);
                        else
                            NotifyQCB(cmd.ToString());
                        break;
                    }
                case QuoteUpdate.CLASSNAME:
                    {   // If it is a Quote Update, check if it was successful and logs the message
                        var cmd = QuoteUpdate.Deserialize(message);
                        if (cmd.success)
                            OnQuoteUpdate(cmd);
                        else
                            NotifyQCB(cmd.ToString());
                        break;
                    }
                case QuoteUnsubscribeResponse.CLASSNAME:
                    {   // If it is a Quote Unsubscribe Response, check if it was successful
                        var cmd = QuoteUnsubscribeResponse.Deserialize(message);
                        if (cmd.success)
                            NotifyOMS(cmd.ToString());
                        else
                            NotifyOMS(cmd.ToString());
                        break;
                    }

                case CandleResponse.CLASSNAME:
                    {   // If it is a Quote Update, check if it was successful and logs the message
                        var cmd = CandleResponse.Deserialize(message);
                        if (cmd.success)
                        {
                            if (cmd.candles != null)
                            {
                                if (cmd.candles.Count <= 0)
                                {
                                    NotifyOMS("Nenhum foi possivel retornar nenhum candle para " + cmd.security + " - Timeframe: " + cmd.timeframe);
                                }
                                else
                                {
                                    NotifyOMS("Dados Historicos Intraday para: " + cmd.security + " - Qtd de Candles: " + cmd.candles.Count + " - Timeframe: " + cmd.timeframe);
                                    //foreach (Candle candle in cmd.candles)
                                    //    this.mainForm.log(candle.ToString());

                                }
                            }
                        }
                        else
                            NotifyOMS(cmd.ToString());

                        break;
                    }

                // Candle Update 
                case CandleUpdate.CLASSNAME:
                    {   // If it is a Candle Update, check if it was successful and logs the message
                        var cmd = CandleUpdate.Deserialize(message);

                        if (cmd.success)
                        {
                            if (cmd.candle != null)
                            {
                                switch (cmd.timeframe)
                                {
                                    case CandleUpdate.TIMEFRAME_INTRADAY_1_MIN:
                                        NotifyOMS("Candle Intraday Update: " + cmd.security + " - Last: " + cmd.candle.close + " - Time: " + cmd.candle.date);
                                        break;

                                    case CandleUpdate.TIMEFRAME_DAILY:
                                        NotifyOMS("Candle Daily Update: " + cmd.security + " - Last: " + cmd.candle.close + " - Time: " + cmd.candle.date);
                                        break;
                                }
                            }
                        }
                        //this.mainForm.updatePositions(cmd);
                        else
                            NotifyOMS(cmd.ToString());

                        break;
                    }

                // QCB has disconnected (shut down)
                case Disconnect.CLASSNAME:
                    this.disconnect();
                    break;
            }
        }

        /// <summary>
        /// Sends the command to Quantsis Conneciton Box
        /// </summary>
        /// <param name="message"></param>
        private void send(string message)
        {
            try
            {
                if ((this.socket != null) && (this.stream != null) && (this.socket.Connected))
                    ThreadPool.QueueUserWorkItem(o => streamWriter.WriteLine(message));
            }
            catch (Exception e)
            {
                NotifyOMS(e.Message + Environment.NewLine + e.StackTrace);
            }
        }

        #region Broadcast received messages

        /// <summary>
        /// Events
        /// </summary>
        private void OnQuoteResponse(QuoteResponse cmd)
        {
            ThreadPool.QueueUserWorkItem(o =>
            {
                try
                {
                    if (TickArrived == null) return;
                    TickArrived(new Tick(cmd.quote.symbol, DateTime.Now, (decimal)cmd.quote.last));
                }
                catch (Exception e)
                {
                    if (InfoChanged != null) InfoChanged(e.Message + Environment.NewLine + e.StackTrace);
                }
            });
        }

        /// <summary>
        /// Events
        /// </summary>
        private void OnQuoteUpdate(QuoteUpdate cmd)
        {
            ThreadPool.QueueUserWorkItem(o =>
            {
                try
                {
                    if (TickArrived == null || cmd.quote.last <= 0) return;
                    TickArrived(new Tick(cmd.quote.symbol, DateTime.Now, (decimal)cmd.quote.last));
                }
                catch (Exception e)
                {
                    if (InfoChanged != null) InfoChanged(e.Message + Environment.NewLine + e.StackTrace);
                }
            });
        }

        /// <summary>
        /// Events
        /// </summary>
        private void NotifyOMS(string value)
        {
            ThreadPool.QueueUserWorkItem(o =>
            {
                if (InfoChanged != null) InfoChanged(value);
            });
        }

        /// <summary>
        /// Events
        /// </summary>
        private void NotifyQCB(string value)
        {
            ThreadPool.QueueUserWorkItem(o =>
            {
                if (FeedChanged != null) FeedChanged(value);
            });
        }

        #endregion
        
        #endregion
    }
}