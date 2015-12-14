namespace CAT.WPF.ViewModels
{
    using CAT.Model;
    using CAT.WPF.Helpers;
    using CAT.WPF.Model;
    using Optimera.GA;
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Collections.Specialized;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Media;
    using System.Net.NetworkInformation;
    using System.Threading.Tasks;
    using System.Windows.Data;
    using System.Windows.Input;

    public class MainWindowViewModel : NotificationObject
    {
        #region Properties
        public UserInfo User { get; set; }
        
        public SystemManager SysManager { get; set; }
        private static string TimeTag 
        {
            get
            {
                return "[ " + TimeZoneInfo.ConvertTime(DateTime.Now,
                    TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time"))
                    .ToString("HH:mm:ss.fffffff", CultureInfo.CurrentCulture) + " ]: ";
            }
        }

        #region Version

        public static string Version
        {
            get
            {
                return "Versão: " + System.Reflection.Assembly.GetEntryAssembly().GetName().Version.ToString();
            }
        }

        #endregion

        #region Roaming
        private static string Roaming 
        {
            get
            {
                return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\CAT";
            }
        }
        #endregion

        #region OMS Color and Status

        private string _omscolor = "Red";
        public string OmsColor 
        { 
            get { return _omscolor; } 
        }

        private string _omsstatus = "Offline ";
        public string OmsStatus
        {
            get { return _omsstatus; }
            set
            {
                if (value != null && _omsstatus != value )
                {
                    var cma = value.Contains("OFF") || value.Contains("OK");
                    var idx = value.Contains("OFF") || value.Contains("Id");
                    _omscolor = idx ? "Red" : "Green";
                    RaisePropertyChanged(() => OmsColor);

                    if (cma) _omsstatus = value.Replace("OFF", TimeTag).Replace("OK", TimeTag);
                    if (!cma) Log = TimeTag + value + "\r\n" + Log;
                    RaisePropertyChanged(() => OmsStatus);
                }
            }
        }

        #endregion

        #region Datafeed Color and Status

        private string _feedcolor = "Red";
        public string FeedColor
        {
            get { return _feedcolor; }
        }

        private string _feedstatus = "Offline ";
        public string FeedStatus
        {
            get { return _feedstatus; }
            set
            {
                if(value != null && _feedstatus != value)
                {
                    _feedcolor = value.Contains("OFF") ? "Red" : "Green";
                    RaisePropertyChanged(() => FeedColor);

                    _feedstatus = value.Contains("OFF") ? value.Replace("OFF", TimeTag) : "Conectado ao " + value;
                    RaisePropertyChanged(() => FeedStatus);

                    if (value.Contains("DDE")) Log = TimeTag + _feedstatus + "\r\n" + Log;
                }
            }
        }

        #endregion

        #region Last Symbol

        private string _lastsymbol = "Fazendo login e carregando setups... ";
        public string LastSymbol
        {
            get { return _lastsymbol; }
            set
            {
                if (_lastsymbol != value)
                {
                    _lastsymbol = value;
                    RaisePropertyChanged(() => LastSymbol); 
                }
            }
        }

        #endregion

        #region Log

        private string _log = string.Empty;
        public string Log
        {
            get { return _log; }
            set
            {
                if (_log != value)
                {
                    _log = value;
                    RaisePropertyChanged(() => Log); 
                }
            }
        }

        #endregion
        
        #region TradingButton

        public string TradingButton
        {
            get { return SelectedSetup == null || !SelectedSetup.Online ? "Lance Robô" : "Pare Robô"; }
        }

        #endregion

        #region SelectedSetup

        private Setup _selectedsetup;
        public Setup SelectedSetup
        {
            get { return _selectedsetup; }
            set
            {
                if (_selectedsetup != value)
                {
                    _selectedsetup = value;
                    RaisePropertyChanged(() => SelectedSetup);
                    RaisePropertyChanged(() => TradingButton);

                    BasketCollection.Clear();

                    try
                    {
                        using (var db = new DatabaseContext())
                        {
                            var basket = db.Clients.Where(i => i.Setup == SelectedSetup.SetupId).ToList();
                            foreach (var item in basket) BasketCollection.Add(item);
                        }
                    }
                    catch (Exception) { }
                }
            }
        }

        #endregion

        #region SetupsCollection

        private ObservableCollection<Setup> _setupsCollection;
        public ObservableCollection<Setup> SetupsCollection
        {
            get { return _setupsCollection; }
            set
            {
                if (_setupsCollection != value)
                {
                    _setupsCollection = value;
                    RaisePropertyChanged(() => SetupsCollection);
                }
            }
        }

        #endregion

        #region SelectedBasketItem

        private Client _selectedbasketitem;
        public Client SelectedBasketItem
        {
            get { return _selectedbasketitem; }
            set
            {
                _selectedbasketitem = value;
                    
                if (_selectedbasketitem != null)
                {
                    RaisePropertyChanged(() => this.SelectedBasketItem); 
                }
            }
        }

        #endregion

        #region BasketCollection

        private ObservableCollection<Client> _basketCollection;
        public ObservableCollection<Client> BasketCollection
        {
            get { return _basketCollection; }
            set
            {
                if (_basketCollection != value)
                {
                    _basketCollection = value;
                    RaisePropertyChanged(() => this.ClientCount);            
                    RaisePropertyChanged(() => this.ClientCapital); 
                    RaisePropertyChanged(() => this.BasketCollection);
                }
            }
        }

        #endregion

        #region ClientCount e ClientCapital

        public int ClientCount { get; set; }
        public string ClientCapital { get; set; }
        
        #endregion

        #region SelectedTrade

        private Trade _selectedtrade;
        public Trade SelectedTrade
        {
            get { return _selectedtrade; }
            set
            {
                if (_selectedtrade != value)
                {
                    _selectedtrade = value;
                    RaisePropertyChanged(() => SelectedTrade); 
                }
            }
        }

        #endregion

        #region TradesCollection

        private ObservableCollection<Trade> _tradescollection;
        public ObservableCollection<Trade> TradesCollection
        {
            get { return _tradescollection; }
            set
            {
                if (_tradescollection != value)
                {
                    _tradescollection = value;
                    RaisePropertyChanged(() => this.TradesCollection);             
                }
            }
        }

        public int TradesCount { get; set; }

        #endregion

        #region CurrentPL

        public string CurrentPL {get; set;}

        #endregion

        #endregion

        #region Commands
        public ICommand SaveCommand { get { return new DelegateCommand(OnSave, CanWork); } }
        public ICommand SwitchCommand { get { return new DelegateCommand(Switch, CanOnline); } }
        public ICommand BacktestCommand { get { return new DelegateCommand(OnBacktest, CanWork); } }
        public ICommand OptimizationCommand { get { return new DelegateCommand(OnOptimization, CanWork); } }
        public ICommand DeleteCommand { get { return new DelegateCommand(OnDelete, () => SelectedBasketItem != null); } }
        public ICommand CloseTradeCommand { get { return new DelegateCommand(OnClosePosition, () => false); } }
        public ICommand ClearMemoryCommand { get { return new DelegateCommand(OnClearMemory, () => true); } }
        public ICommand OpenLogDirectoryCommand { get { return new DelegateCommand(OnOpenLogDirectory, () => true); } }
        public ICommand OpenResDirectoryCommand { get { return new DelegateCommand(OnOpenResDirectory, () => true); } }
        public ICommand SaveLogCommand { get { return new DelegateCommand(OnSaveLog, () => true); } }
        public ICommand PingCommand { get { return new DelegateCommand(OnPing, () => true); } }
        public ICommand PrintPortfolioCommand { get { return new DelegateCommand(OnPrintPorfolio, () => true); } }

        #region Command Handlers

        private bool CanOnline()
        {
            return CanWork() && (
                Process.GetProcessesByName("TCA_DDESVR").Length > 0 ||
                Process.GetProcessesByName("ProfitChart").Length > 0 ||
                Process.GetProcessesByName("ConnectionBox").Length > 0);
        }
        private bool CanWork()
        {
            return SelectedSetup != null && !SelectedSetup.Online 
                && User != null && DateTime.Today < User.Expiration;
        }
        private void Switch()
        { 
            SysManager.PreOnlineTrading();

            if (SelectedSetup.Online)
                ExitOnlineMode();
            else
                EnterOnlineMode();
            
            RaisePropertyChanged(() => TradingButton);
        }
        private void EnterOnlineMode()
        {
            SelectedSetup.Online = true;
            SelectedSetup.Basket = BasketCollection.Where(i => i.Capital > 0).ToList();
            
            SysManager.CreateTasks(SelectedSetup);

            UpdateSetupAtDatabase(SelectedSetup);
            OnSave(); 
        }
        private void ExitOnlineMode()
        { 
            Debug.WriteLine("Haha");
            SelectedSetup.Online = false;
      
        }
        private async void OnBacktest()
        {
            SelectedSetup.Online = true;
            await Task.Factory.StartNew(async () =>
            {
                try
                {
                    await SysManager.OfflineTrading(SelectedSetup);
                }
                catch (Exception e)
                {
                    Log = TimeTag + e.Message + "\r\n" + Log;
                }
                finally 
                {
                    SelectedSetup.Online = false;
                    UpdateSetupAtDatabase(SelectedSetup);
                    OnSave();
                }
            });
        }
        private async void OnOptimization()
        {
            if (!User.IsDeveloper()) return;

            SelectedSetup.Online = true;
            SysManager.OptimizationSetup = SelectedSetup;
            await SysManager.OfflineTrading(SelectedSetup);

            try
            {
                await Task.Factory.StartNew(()=>SysManager.GetData(SelectedSetup)).ContinueWith(async _ =>
                    {
                        try
                        {
                            var ga = new GA(SysManager, UpdateProgress, 2, 0.8, 0.1, 100, 10, 0.0001);
                            ga.InputGenes = SysManager.GetGenes();
                            await ga.Go();

                            double[] bestGenes; double bestFitness;
                            ga.GetBest(out bestGenes, out bestFitness);

                            var file = DateTime.Now.ToString("yyMMddHHmm") + ".csv";
                            File.WriteAllText(Roaming + @"\Otimização\" + file, Log);
                            Log = TimeTag + "Resultado da otimização no ficheiro " + file + ". Pressione F4 para abrir sua localização.\r\n\r\n" +
                                TimeTag + "Final/Best: " + bestFitness + "\r\n" +
                                SysManager.SetGenes(bestGenes).ToString() + "\r\n" + Log;

                            SysManager.OptimizationSetup.Online = false;
                            SelectedSetup.Online = false;
                        }
                        catch (Exception ex)
                        {
                            SelectedSetup.Online = false;
                            Log = TimeTag + ex.Message + "\r\n" + Log;
                            var file = DateTime.Now.ToString("yyMMddHHmm") + ".csv";
                            File.WriteAllText(Roaming + @"\Otimização\" + file, Log);
                        }
                        
                    });
            }
            catch (Exception e)
            {
                SelectedSetup.Online = false;
                Log = TimeTag + e.Message + "\r\n" + Log;
                var file = DateTime.Now.ToString("yyMMddHHmm") + ".csv";
                File.WriteAllText(Roaming + @"\Otimização\" + file, Log);
            }
            
        }
        private void OnDelete()
        {
            try
            {
                using (var db = new DatabaseContext())
                {
                    db.Clients.Remove(db.Clients.First(c => c.Id == SelectedBasketItem.Id));
                    db.SaveChanges();
                }
            }
            catch (Exception e)
            {
                Log = TimeTag + "Falha ao apagar cliente " + SelectedBasketItem.ToString() + ".\t" + e.Message + "\r\n" + Log;
            }

            this.BasketCollection.Remove(SelectedBasketItem);
        }
        private void OnSave()
        {
            try
            {
                using (var db = new DatabaseContext())
                {
                    var OldCollection = BasketCollection.Where(i => i.Setup == SelectedSetup.SetupId).ToList();
                    foreach (var item in OldCollection)
                    {
                        var fromDB = db.Clients.FirstOrDefault(c => c.Id == item.Id);
                        if (fromDB == null)
                        {
                            item.Setup = SelectedSetup.SetupId;
                            db.Clients.Add(item);
                        }
                        else
                        {
                            fromDB.ClientID = item.ClientID;
                            fromDB.Capital = item.Capital;
                            fromDB.IsMini = item.IsMini;
                        }
                    }

                    // Add new itens
                    foreach (var item in BasketCollection.Where(i => i.Setup == 0))
                    {
                        item.Setup = SelectedSetup.SetupId;
                        db.Clients.Add(item);
                    }

                    db.SaveChanges();
                }
            }
            catch (Exception e)
            {
                Log = TimeTag + "Falha ao salvar clientes na base de dados. Tente novamente.\r\n" + e.Message + "\r\n" + Log;
            }
            
        }
        private void OnSaveLog()
        {
            var file = "Log" + DateTime.Now.ToString("yyMMddHHmm") + ".txt";
            File.WriteAllText(Environment.CurrentDirectory + @"\log\" + file, Log);
            Log = TimeTag + "Ficheiro " + file + " criado. Pressione F5 para abrir sua localização.\r\n" + Log;
        }
        private void OnClosePosition()
        {
            SysManager.EditTrade(SelectedTrade);
        }
        private void OnPrintPorfolio()
        {
            if(SysManager != null) SysManager.PrintPortfolio();
        }
        private void OnClearMemory()
        {
            SysManager.DicTicks = new System.Collections.Concurrent.ConcurrentDictionary<int, List<Tick>>();
        }
        private void OnOpenLogDirectory()
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = Environment.CurrentDirectory + @"\log",
                UseShellExecute = true
            });
        }
        private void OnOpenResDirectory()
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = Roaming,
                UseShellExecute = true
            });
        }
        private async void OnPing()
        {
            try
            {
                using (var ping = new Ping())
                {

                    var imax = 10;
                    var roundtriptimes = new List<long>();
                    var hostNameOrAddress = "200.198.181.100";

                    for (var i = 0; i < imax; i++)
                    {
                        var reply = await ping.SendPingAsync(hostNameOrAddress);
                        if (reply.Status == IPStatus.Success)
                            roundtriptimes.Add(reply.RoundtripTime);
                    }

                    var output = TimeTag + "Pinging " + hostNameOrAddress + " com 32 bytes de data. Perda de pacotes: ";
                    output += ((decimal)(imax - roundtriptimes.Count()) / imax).ToString("P0");
                    if (roundtriptimes.Count() > 0)
                    {
                        output += "\r\nTempo aproximado da ida e volta em milisegundos: ";
                        output += "Min = " + roundtriptimes.Min() + "ms, ";
                        output += "Max = " + roundtriptimes.Max() + "ms, ";
                        output += "Med = " + Math.Ceiling(roundtriptimes.Average()) + "ms.\r\n";
                    }
                    output += "Pressione F3 para fazer ping novamente.\r\n";
                    output += "Pressione F4 para abrir pasta com resultados des simulações.\r\n";
                    output += "Pressione F5 para abrir pasta com logs.\r\n\r\n";

                    Log = output + Log;
                }
            }
            catch (Exception e) { Log = TimeTag + e.Message + Log; }
        }

        private void CheckRights()
        {
            LastSymbol = "Fazendo login e carregando setups... ";
            SetupsCollection.Clear();

            var isMember = User.IsMember();

            Log = isMember == -2 ? "Sem conexão à base de dados online. Utilizador não pode ser verificado.\r\n" + Log
                : isMember == -1 ? "Utilizador não está cadastrado. Contate admistrador.\r\n" + Log
                : isMember == 0 ? "Sua licença do utilizador expirou! Contate o administrador.\r\n" + Log
                : isMember == 2 ? "Selecione uma estratégia e pressione F9 para otimizar.\r\n" + Log : Log;

            LastSymbol = "Offline ";
            
            if (isMember < 1) return;

            try
            {
                using (var db = new DatabaseContext())
                {
                    var setupIDs = User.Setups.Split(' ');
                    foreach (var strId in setupIDs)
                    {
                        var id = Int16.Parse(strId, CultureInfo.CurrentCulture);
                        var setups = db.Setups.Where(s => s.SetupId == id).ToList();
                        if (setups == null || setups.Count == 0) continue;

                        foreach (var setup in setups)
                        {
                            if (!SetupsCollection.Contains(setup)) SetupsCollection.Add(setup);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log = TimeTag + e.Message + "\r\n" + Log;
            }
        }
        public void UpdateSetupAtDatabase(Setup update)
        {
            try
            {
                using (var db = new DatabaseContext())
                {
                    var setup = db.Setups.FirstOrDefault(s => s.SetupId == update.SetupId);
                    
                    setup.How2Trade = update.How2Trade;
                    setup.Name = update.Name;
                    setup.TimeFrame = update.TimeFrame;
                    setup.Allow = update.Allow;
                    setup.Capital = update.Capital;
                    setup.DayTradeDuration = update.DayTradeDuration;
                    setup.Slippage = update.Slippage;
                    setup.Discount = update.Discount;
                    setup.OfflineStartTime = update.OfflineStartTime;
                    
                    setup.Symbol = update.Symbol;
                    setup.StaticGain = update.StaticGain;
                    setup.StaticLoss = update.StaticLoss;
                    setup.DynamicLoss = update.DynamicLoss;
                    setup.Parameters = update.Parameters;

                    db.SaveChanges();
                }
            }
            catch (Exception e)
            {
                Log = TimeTag + e.Message + "\r\n" + Log;
            }
        }

        #endregion

        #endregion

        #region Constructor
        public MainWindowViewModel()
        {
            OnPing();

            User = new UserInfo();

            Directory.CreateDirectory(Roaming);
            Directory.CreateDirectory(Roaming + @"\Data");
            Directory.CreateDirectory(Roaming + @"\Backtest");
            Directory.CreateDirectory(Roaming + @"\Otimização");
            Directory.CreateDirectory(Environment.CurrentDirectory + @"\log");

            CurrentPL = "Nenhum trade sendo acompanhado.";
            
            SetupsCollection = new ObservableCollection<Setup>();
            BasketCollection = new ObservableCollection<Client>();
            BasketCollection.CollectionChanged += BasketChanged;
            TradesCollection = new ObservableCollection<Trade>();
            TradesCollection.CollectionChanged += TradesChanged;

            BindingOperations.EnableCollectionSynchronization(Log, new object());
            BindingOperations.EnableCollectionSynchronization(TradesCollection, new object());
            BindingOperations.EnableCollectionSynchronization(SetupsCollection, new object());

            SysManager = new SystemManager();
            SysManager.TickArrived += (tick) => this.LastSymbol = tick.ToShortString();
            SysManager.InfoChanged += (status) => this.OmsStatus = status;
            SysManager.CommChanged += (status) => this.FeedStatus = status;
            SysManager.TradeArrived += OnTradeArrived;
            SysManager.PrintSavedTrades();

            FillSetupDatabase();
            CheckRights();
        }

        ~MainWindowViewModel()
        {
            SysManager.Dispose();
            File.AppendAllText(Environment.CurrentDirectory + @"\log\CATLOG_" + DateTime.Today.ToString("yyyyMMdd", CultureInfo.CurrentCulture) + ".txt", Log);
        }

        #endregion

        #region Private functions
        
        async private void FillSetupDatabase()
        {
            await Task.Factory.StartNew(() =>
            {
                try
                {
                    using (var db = new DatabaseContext())
                    {

                        if (!db.Setups.Any(s => s.SetupId == 1)) db.Setups.Add(new Setup(1, "SProp001"));
                        if (!db.Setups.Any(s => s.SetupId == 1)) db.Setups.Add(new Setup(7, "SProp002"));
                        if (!db.Setups.Any(s => s.SetupId == 2)) db.Setups.Add(new Setup(2, "OCandle"));
                        if (!db.Setups.Any(s => s.SetupId == 3)) db.Setups.Add(new Setup(3, "OCandle"));
                        if (!db.Setups.Any(s => s.SetupId == 4)) db.Setups.Add(new Setup(4, "OCandle"));
                        if (!db.Setups.Any(s => s.SetupId == 5)) db.Setups.Add(new Setup(5, "OCandle"));
                        if (!db.Setups.Any(s => s.SetupId == 6)) db.Setups.Add(new Setup(6, "ACap002"));
                        if (!db.Setups.Any(s => s.SetupId == 12)) db.Setups.Add(new Setup(12, "OCandle"));
                        if (!db.Setups.Any(s => s.SetupId == 13)) db.Setups.Add(new Setup(13, "OCandle"));
                        if (!db.Setups.Any(s => s.SetupId == 14)) db.Setups.Add(new Setup(14, "OCandle"));
                        if (!db.Setups.Any(s => s.SetupId == 15)) db.Setups.Add(new Setup(15, "OCandle"));
                        if (!db.Setups.Any(s => s.SetupId == 16)) db.Setups.Add(new Setup(16, "OCandle"));
                        if (!db.Setups.Any(s => s.SetupId == 17)) db.Setups.Add(new Setup(17, "OCandle"));
                        if (!db.Setups.Any(s => s.SetupId == 99)) db.Setups.Add(new Setup(99, "AIMA"));

                        foreach (var setup in db.Setups) setup.Online = false;

                        db.SaveChanges();
                    }
                }
                catch (Exception e)
                {
                    Log = TimeTag + e.Message + "\r\n" + Log;
                }

            });
        }

        void OnTradeArrived(Trade trade, int act)
        {
            if (trade == null || trade.Id == null) return;
            PlayBeep(trade, act);
            
            var exist = this.TradesCollection.FirstOrDefault(t => t.Id == trade.Id);
            if (exist != null) { exist = trade; return; }

            this.TradesCollection.Add(trade);
        }
        
        async void PlayBeep(Trade trade, int act)
        {
            if (act == 0) return;

            await Task.Factory.StartNew(() =>
            {
                Log = TimeTag + trade.ToString().Replace(";", " ") + "\r\n" + Log;
                var resource = this.GetType().Assembly.GetManifestResourceStream("CAT.WPF.Resources.alarmclock.wav");
                using (var player = new SoundPlayer(resource)) player.Play();
            });
        }
        
        void UpdateProgress(String[] s)
        {
            Log = "\r\nTimestamp: " + s[0] +
                "\r\nGenerations complete: " + s[1] +
                "\r\nModel runs complete: " + s[2] +
                "\r\nBest fitness so far: " + s[3] +
                "\r\nBest genes so far: " + s[4] + "\r\n\r\n" + Log;
        }
        
        private string GetClientCapital()
        {
            var isIndex = SelectedSetup != null && SelectedSetup.Symbol != null && !_selectedsetup.Symbol.Contains("FUT");
            var minicon = BasketCollection.Where(x => x.IsMini).Sum(x => x.Capital);
            var fullcon = BasketCollection.Where(x => !x.IsMini).Sum(x => x.Capital);

            return isIndex
                ? (minicon + fullcon).ToString("R$ #.###,00")
                : fullcon + " contratos e " + minicon + " mini-contratos";
        }
        private void BasketChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            ClientCount = BasketCollection.Count;
            RaisePropertyChanged(() => this.ClientCount);

            if (e.Action == NotifyCollectionChangedAction.Remove)
            {
                foreach (var item in e.OldItems)
                {
                    //Removed items
                    (item as INotifyPropertyChanged).PropertyChanged -= (s, o) =>
                    {
                        ClientCapital = GetClientCapital();
                        RaisePropertyChanged(() => this.ClientCapital);
                    };

                    ClientCapital = GetClientCapital();
                    RaisePropertyChanged(() => this.ClientCapital);
                }
            }
            else if (e.Action == NotifyCollectionChangedAction.Add)
            {
                foreach (var item in e.NewItems)
                {
                    //Added items
                    (item as INotifyPropertyChanged).PropertyChanged += (s, o) =>
                        {
                            ClientCapital = GetClientCapital();
                            RaisePropertyChanged(() => this.ClientCapital);
                        };
                }
            }

        }        
        private string GetCurrentPL()
        {
            if (TradesCollection == null || TradesCollection.Count == 0) return "Nenhum trade sendo acompanhado.";

            var hasValue = TradesCollection.Where(x => x.Result.HasValue).ToList();
            if (hasValue.Count == 0) return "Nenhum trade sendo acompanhado.";

            var tradesIndex = hasValue.FindAll(x => x.AssetClass == "BMF.FUT");
            var tradesStock = hasValue.FindAll(x => x.AssetClass == "BVSP.VIS");
            var tradesOption = hasValue.FindAll(x => x.AssetClass == "BVSP.OPC");

            var capIndex = tradesIndex.Count == 0 ? 0 : tradesIndex.Sum(t => t.Capital);
            var capStock = tradesStock.Count == 0 ? 0 : tradesStock.Sum(t => t.Capital);
            var capOption = tradesOption.Count == 0 ? 0 : tradesOption.Sum(t => t.Capital);
            var capital = capIndex + capStock + capOption;
            if (capital == 0) return "Capital ZERO!";

            var resIndex = tradesIndex.Count == 0 ? 0 : tradesIndex.Sum(t => t.Qnty * t.Result - t.Cost);
            var resStock = tradesStock.Count == 0 ? 0 : tradesStock.Sum(t => t.Qnty * t.Result - t.Cost);
            var resOption = tradesOption.Count == 0 ? 0 : tradesOption.Sum(t => t.Qnty * t.Result - t.Cost);
            var result = resIndex + resStock + resOption;
            return (result.Value / capital).ToString("P2");
        }
        private void TradesChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            TradesCount = TradesCollection == null ? 0 : TradesCollection.Count;
            RaisePropertyChanged(() => this.TradesCount);

            if (e.Action == NotifyCollectionChangedAction.Remove)
            {
                foreach (var item in e.OldItems)
                {
                    //Removed items
                    (item as INotifyPropertyChanged).PropertyChanged -= (s, o) =>
                        {
                            CurrentPL = GetCurrentPL();
                            RaisePropertyChanged(() => this.CurrentPL);
                        };
                }
            }
            else if (e.Action == NotifyCollectionChangedAction.Add)
            {
                foreach (var item in e.NewItems)
                {
                    //Added items
                    
                    CurrentPL = GetCurrentPL();
                    RaisePropertyChanged(() => this.CurrentPL);

                    (item as INotifyPropertyChanged).PropertyChanged += (s, o) =>
                        {
                            CurrentPL = GetCurrentPL();
                            RaisePropertyChanged(() => this.CurrentPL);
                        };
                }
            }
        }
        #endregion
    }
}