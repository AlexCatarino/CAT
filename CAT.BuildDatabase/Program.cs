using CAT.Model;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Serialization;
using System.Configuration;
using System.Globalization;

namespace CAT.BuildDatabase
{
    class Program
    {
        private static int[] _ids;
        private static string[] _iBrA;
        private static string _connectionString;
        private static string _root = @"C:\Users\Alexandre\Documents\IBOV\";
        private static FileInfo fileprov = new FileInfo(_root + @"\Stock\PROVENTOS_1998_A_2014.xml");
        private static FileInfo filecota = new FileInfo(_root + @"\Stock\COTAHIST_A2014.xml");
        
        private static CloudTableClient TableClient
        {
            get
            {
                return CloudStorageAccount.Parse(
                    "DefaultEndpointsProtocol=https;" +
                    "AccountName=catarino;" +
                    "AccountKey=pQFN//7LQ5L0ki0AyUKcwTBUz9i9F07zoX5iFezs9Q4Fm4mm/WsF8MmKmOwZVmNdOpOGr7NZnJs5Jy4ZX6RmsQ==")
                    .CreateCloudTableClient();
            }
        }
        
        static void Main(string[] args)
        {
            char key;
            var cutoff = new DateTime(2011, 10, 1);

            do
            {
                key = Console.ReadKey().KeyChar;

                switch (key)
                { 
                    case '1':
                        GetID();
                        break;
                    case '2':
                        new int[] { 4820, 18821 }.ToList()
                            .ForEach(i => DelEvents(i));
                        GetEvents();
                        break;
                    case '3':
                        cutoff = new DateTime(2015, 5, 1);
                        ReadCOTAHIST();
                        break;
                    case '4':
                        AdjustedPrice();
                        COTAHIST2ASCII();                        
                        break;
                    case '5':
                        GetLastFromCOTAHIST();
                        //COTAHIST2CSV();
                        break;
                    case '6':
                        new string[] 
                        { 
                            //"ITUB4", "GGBR4", "CYRE3", "KROT3", "HGTX3", "MRFG3",  
                            "BBAS3"//,"JBSS3",  "POMO4", "USIM5", "ALLL3",
                        }.ToList().ForEach(s => AdjustedPrice(s, new DateTime(2014, 5, 31)));
                        break;
                    case '7':
                        ReadNEG(true, cutoff);
                        break;
                    case '8':
                        ReadNEG(false, cutoff);
                        break;
                    case '9':
                        cutoff = new DateTime(2012, 1, 1);
                        BuildPropTrading(cutoff);
                        break;
                    default:
                        break;
                }

            } while (key != '0');

        }

        static Dictionary<string,string> GetCodeAndName(string indexname,bool canprint)
        {
            var tickers = new Dictionary<string, string>();
            var url = "http://www.bmfbovespa.com.br/indices/ResumoCarteiraQuadrimestre.aspx?Indice=" + indexname;
            
            using (var client = new System.Net.WebClient())
            {
                var index = 0;
                var page1 = string.Empty;
                var lstfile = _root + @"\Stock\Ibovespa.dat";

                try
                {
                    page1 = client.DownloadString(url);

                    if (canprint)
                    {
                        File.Delete(lstfile);
                        File.AppendAllText(lstfile, "Ticker\tName\tClass\tMarket Cap\tWeight\r\n");
                    }

                    if ((index = page1.IndexOf("<tbody>")) >= 0)
                        page1 = page1.Substring(0, page1.IndexOf("</tbody>")).Substring(index);

                    index = 0;

                    while ((index = page1.IndexOf("<tr", index + 1)) > 0)
                    {
                        var idx = 0;
                        var pass = 0;
                        var code = string.Empty;
                        var output = string.Empty;
                        var line = page1.Substring(index, page1.IndexOf("/tr", index) - index);


                        while ((idx = line.IndexOf("\">", idx) + 2) >= 2)
                        {
                            pass++;
                            var field = line.Substring(idx, line.IndexOf("<", idx) - idx);
                            if (pass == 3) output = field.Trim();
                            if (pass == 5) output += "\t" + field.Trim();
                            if (pass == 7) output += "\t" + field.Replace("      ", " ").Trim();
                            if (pass == 9) output += "\t" + field.Replace(".", "").Trim();
                            if (pass < 11 || field.Length == 0) continue;
                            var w = double.Parse(field)/100;
                            output += "\t" + w.ToString().Replace(",",".");
                            var data = output.Split('\t');

                            if (string.IsNullOrWhiteSpace(data[1])) continue;
                            if (canprint) File.AppendAllText(lstfile, output + "\r\n");

                            tickers.Add(data[0], data[1]);
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);

                    if (File.Exists(lstfile))
                    {
                        Console.WriteLine("Read saved file instead...");
                        var lines = File.ReadAllLines(lstfile);
                        for (var i = 1; i < lines.Length; i++)
                        {
                            var data = lines[i].Split('\t');
                            tickers.Add(data[0].Trim(), data[1].Trim());
                        }
                    }

                    return tickers;
                }
            }
            return tickers;
        }

        private static async void Send2Azure()
        {
            var table = TableClient.GetTableReference("Daily");
            if (table.CreateIfNotExists()) Console.WriteLine("Daily table created!");
            foreach (var cloudTable in TableClient.ListTables()) Console.WriteLine(cloudTable.Name);

            var candles = table.ExecuteQuery(new TableQuery<AzureCandle>()).ToList();
                //.Where(TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.Equal, "19022014")))
                //.ToList();

            if (!filecota.Exists) return;

            var inbatch = new List<AzureCandle>();
            var batchOperation = new TableBatchOperation();
            var upcandles = new List<AzureCandle>();
            //DeserializeCandle(xmlfile);
            
            foreach (var upcandle in upcandles)
            {
                if (inbatch.Count > 99 || (inbatch.Count != 0 && upcandle.PartitionKey != inbatch.Last().PartitionKey))
                {
                    var resultb = await table.ExecuteBatchAsync(batchOperation);
                    batchOperation.Clear();
                    inbatch.Clear();
                }
                
                inbatch.Add(upcandle);
                batchOperation.InsertOrReplace(upcandle);
            }
            if (batchOperation.Count > 0) await table.ExecuteBatchAsync(batchOperation);

            Console.WriteLine("All read!");
        }
        private static async void GetEvents()
        {
            var timer = DateTime.Now;
            Console.WriteLine("\t" + timer);
            var corpevents = new List<CorporateEvents>();

            var idstxt = "Ids.txt";
            var errtxt = "Err.txt";
            var ids = new List<int>();
            var idx = new List<int>();
            if (File.Exists(idstxt)) File.ReadAllLines(idstxt).ToList().ForEach(i => { if (!ids.Contains(int.Parse(i))) ids.Add(int.Parse(i)); });
            Deserialize(fileprov.FullName, out corpevents);
            if (corpevents.Count > 0) corpevents.ForEach(i => { if (!idx.Contains(i.ID)) idx.Add(i.ID); });
            ids = ids.Except(idx).ToList();

            foreach(var id in ids)
            {
                var thisIdEvents = await GetEventForID(id, new DateTime(1998, 1, 1));
                if (thisIdEvents == null) continue;

                if (thisIdEvents.Count > 0) foreach (var e in thisIdEvents) if (!corpevents.Contains(e)) corpevents.Add(e);
                Console.WriteLine(Math.Ceiling((DateTime.Now - timer).TotalMilliseconds / id));
            }

            Serialize(fileprov.FullName, corpevents.OrderBy(e => e.Date).ThenBy(e => e.Symbols).ToList());
            Console.WriteLine((DateTime.Now - timer).TotalMinutes.ToString("Done in #.000 minutes!"));
        }

        private static void DelEvents(int id)
        {
            var timer = DateTime.Now;
            Console.WriteLine("\t" + timer);
            var corpevents = new List<CorporateEvents>();
            Deserialize(fileprov.FullName, out corpevents);
            corpevents.RemoveAll(c => c.ID == id);
            Serialize(fileprov.FullName, corpevents);
        }

        static private async void GetID()
        {
            var idstxt = "Ids.txt";
            var errtxt = "Err.txt";         // AMBEV
            var ids = new List<int>(); ids.Add(18112);
            var alphabet = new List<string>();
            _iBrA = GetCodeAndName("Ibovespa", false).Values.ToArray();
            _iBrA.ToList().ForEach(s => { if (!alphabet.Contains(s.Substring(0, 1))) alphabet.Add(s.Substring(0, 1)); });

            if (File.Exists(errtxt)) alphabet = File.ReadAllText(errtxt).Split(';').ToList();
            if (File.Exists(idstxt)) File.ReadAllLines(idstxt).ToList().ForEach(i => { if (!ids.Contains(int.Parse(i))) ids.Add(int.Parse(i)); });
            if (File.Exists(errtxt)) File.Delete(errtxt);
            if (File.Exists(idstxt)) File.Delete(idstxt);

            foreach (var letter in alphabet)
            {
                var names = _iBrA.Where(s => s.Substring(0, 1) == letter).ToList();
                var url = "http://www.bmfbovespa.com.br/cias-listadas/empresas-listadas/BuscaEmpresaListada.aspx?Letra=" + letter;

                try
                {
                    using (var client = new HttpClient())
                    using (var response = await client.GetAsync(url))
                    using (var content = response.Content)
                    {
                        int id;
                        var page0 = await content.ReadAsStringAsync();
                        
                        foreach (var name in names)
                        {
                            if ((id = page0.IndexOf(">" + name + "<")) < 0) continue;

                            var field = page0.Substring(id - 6, 5);

                            if (!int.TryParse(field, out id))
                                if (!int.TryParse(field.Substring(1), out id))
                                    id = int.Parse(field.Substring(2));

                            ids.Add(id);
                            Console.WriteLine(ids.Count + "\t" + id + "\t" + name);
                        }
                    }
                }
                catch (Exception e) 
                {
                    File.AppendAllText(errtxt, letter + ";");
                    Console.WriteLine(e.Message); 
                }
            }
            _ids = ids.OrderBy(i => i).ToArray();
            ids.OrderBy(i => i).ToList().ForEach(i => File.AppendAllText(idstxt, i + "\r\n"));
        }

        static private async Task<List<CorporateEvents>> GetEventForID(int id, DateTime cutoff)
        {
            var symbol = string.Empty;
            var thisIdEvents = new List<CorporateEvents>();
            var url0 = "http://www.bmfbovespa.com.br/pt-br/mercados/acoes/empresas/ExecutaAcaoConsultaInfoEmp.asp?CodCVM=" + id;
            var url1 = "http://www.bmfbovespa.com.br/Cias-Listadas/Empresas-Listadas/ResumoProventosDinheiro.aspx?codigoCvm=" + id;
            var url2 = "http://www.bmfbovespa.com.br/Cias-Listadas/Empresas-Listadas/ResumoEventosCorporativos.aspx?codigoCvm=" + id;
                
            try
            {
                var index = 0;
                var sector = string.Empty;
            
                #region ID to symbol
                using (var client = new HttpClient())
                using (var response = await client.GetAsync(url0))
                using (var content = response.Content)
                {
                    var page0 = await content.ReadAsStringAsync();

                    index = page0.IndexOf("Papel=");
                    if (index < 0) return null;

                    symbol = id != 18112 ? page0.Substring(index + 6, 250) : "ABEV3&";
                    index = symbol.IndexOf("&");
                    if (index < 5) return null;

                    symbol = symbol.Substring(0, index);

                    #region Classificação Setorial
                    index = page0.IndexOf("Classifica&ccedil;&atilde;o Setorial:") + 100;
                    sector = page0.Substring(index);
                    sector = sector.Substring(sector.IndexOf("Dado") + 8);
                    sector = sector.Substring(0, sector.IndexOf("\r\n")).Trim().Replace("&ccedil;", "ç").Replace("&ecirc;", "ê")
                        .Replace("&aacute;", "á").Replace("&oacute;", "ó").Replace("&eacute;", "é").Replace("&iacute;", "í")
                        .Replace("&atilde;", "ã").Replace("&otilde;", "õ").Replace("&uacute;", "ú");
                    #endregion
                }
                #endregion
                
                #region Proventos em Dinheiro
                using (var client = new HttpClient())
                using (var response = await client.GetAsync(url1))
                using (var content = response.Content)
                {
                    var page1 = await content.ReadAsStringAsync();

                    if ((index = page1.IndexOf("<tbody>")) >= 0) 
                        page1 = page1.Substring(0, page1.IndexOf("</tbody>")).Substring(index);
                    
                    index = 0;

                    while ((index = page1.IndexOf("<tr", index + 1)) > 0)
                    {
                        var corpEvent = new CorporateEvents(id, symbol, sector);
                        var line = page1.Substring(index, page1.IndexOf("/tr", index) - index);

                        var pass = 0;
                        decimal prix = 1m;
                        decimal prov = 0m;
                        DateTime date;
                        var dates = new List<DateTime>();

                        var idx = line.IndexOf("td>") + 3;
                        var sclass = id != 18112 ? line.Substring(idx, line.IndexOf("<", idx) - idx) : "ON";

                        while ((idx = line.IndexOf("\">", idx) + 2) >= 2)
                        {
                            pass++;
                            var field = line.Substring(idx, line.IndexOf("<", idx) - idx);
                            if (pass == 2) prov = decimal.Parse(field);
                            if ((pass == 1 || pass == 4 || pass == 5) && DateTime.TryParse(field, out date)) dates.Add(date);
                            if (pass != 6 || dates.Last() < cutoff || (prix = decimal.Parse(field)) == 0) continue;

                            corpEvent.Date = dates.Last();

                            if (sclass == "ON") corpEvent.ProvCashON = prov / prix;
                            else if (sclass == "PN") corpEvent.ProvCashPN = prov / prix;
                            else if (sclass == "PNA") corpEvent.ProvCashPNA = prov / prix;
                            else if (sclass == "PNB") corpEvent.ProvCashPNB = prov / prix;
                            else if (sclass == "UNT") corpEvent.ProvCashUNT = prov / prix;

                            #region Update date pay
                            var pastEvent = thisIdEvents.Count == 0 ? null : thisIdEvents.Find(e => e.Date == corpEvent.Date);
                            if (pastEvent == null) { thisIdEvents.Add(corpEvent); continue; }
                            
                            if (corpEvent.ProvCashON.HasValue)
                            {
                                if (pastEvent.ProvCashON.HasValue)
                                    pastEvent.ProvCashON += corpEvent.ProvCashON;
                                else
                                    pastEvent.ProvCashON = corpEvent.ProvCashON;
                            }
                            if (corpEvent.ProvCashPN.HasValue)
                            {
                                if (pastEvent.ProvCashPN.HasValue)
                                    pastEvent.ProvCashPN += corpEvent.ProvCashPN;
                                else
                                    pastEvent.ProvCashPN = corpEvent.ProvCashPN;
                            }
                            if (corpEvent.ProvCashPNA.HasValue)
                            {
                                if (pastEvent.ProvCashPNA.HasValue)
                                    pastEvent.ProvCashPNA += corpEvent.ProvCashPNA;
                                else
                                    pastEvent.ProvCashPNA = corpEvent.ProvCashPNA;
                            }
                            if (corpEvent.ProvCashPNB.HasValue)
                            {
                                if (pastEvent.ProvCashPNB.HasValue)
                                    pastEvent.ProvCashPNB += corpEvent.ProvCashPNB;
                                else
                                    pastEvent.ProvCashPNB = corpEvent.ProvCashPNB;
                            }
                            if (corpEvent.ProvCashUNT.HasValue)
                            {
                                if (pastEvent.ProvCashUNT.HasValue)
                                    pastEvent.ProvCashUNT += corpEvent.ProvCashUNT;
                                else
                                    pastEvent.ProvCashUNT += corpEvent.ProvCashUNT;
                            }
                            #endregion
                        }
                    }
                }
                #endregion
                
                #region Proventos em Ações
                #region Special case for "Nova AMBEV"
                if (id == 18112)
                {
                    thisIdEvents.Add(new CorporateEvents(id, symbol, sector, "20/10/2000", "50/1", ""));
                    thisIdEvents.Add(new CorporateEvents(id, symbol, sector, "01/08/2007", "100/1", ""));
                    thisIdEvents.Add(new CorporateEvents(id, symbol, sector, "17/12/2010", "50/1", ""));
                    thisIdEvents.Add(new CorporateEvents(id, symbol, sector, "08/11/2013", "50/1", ""));

                    Console.Write("18112 (ABEV): "); return thisIdEvents;
                }
                #endregion

                using (var client = new HttpClient())
                using (var response = await client.GetAsync(url2))
                using (var content = response.Content)
                {
                    var page2 = await content.ReadAsStringAsync();
                    if (page2.Contains("Proventos em Ações"))
                    {
                        if ((index = page2.IndexOf("<tbody>")) >= 0)
                            page2 = page2.Substring(0, page2.IndexOf("</tbody>")).Substring(index);

                        index = 0;

                        while ((index = page2.IndexOf("<tr", index + 1)) > 0)
                        {
                            var idx = 0;
                            var pass = 0;
                            var date = new DateTime();
                            var line = page2.Substring(index, page2.IndexOf("/tr", index) - index);
                            if (line.Contains("Cisão")) continue;

                            while ((idx = line.IndexOf("\">", idx) + 2) >= 2)
                            {
                                pass++;
                                var field = line.Substring(idx, line.IndexOf("<", idx) - idx);
                                if (pass == 3) DateTime.TryParse(field.Trim(), out date);
                                if (pass != 5) continue;

                                var corpEvent = new CorporateEvents(id, symbol, sector, date.ToShortDateString(), field, "");

                                // Special Case: Banco Santander
                                if (id == 20532 && corpEvent.Date.Year == 2014) continue;

                                // Special Case: Banco Bradesco
                                if (id == 906 && corpEvent.Date == new DateTime(2009, 6, 8)) continue;
                                if (id == 906 && corpEvent.Date == new DateTime(2004, 3, 19)) corpEvent.ProvShare = .1m;

                                // Special Case: Banco do Brasil
                                if (id == 1023 && corpEvent.Date == new DateTime(2004, 1, 23)) continue;

                                // Special Case: Cia Energetica de MG
                                if (id == 2453 && corpEvent.Date == new DateTime(2007, 6, 1)) corpEvent.ProvShare = 2m;

                                // Special Case: Cia Energetica de SP
                                if (id == 2577 && corpEvent.Date == new DateTime(2007, 8, 31)) continue;

                                // Special Case: Braskem S.A.
                                if (id == 4820 && corpEvent.Date == new DateTime(2005, 5, 13)) continue;

                                // Special Case: Cia Paranaense de Energia (COPEL)
                                if (id == 14311 && corpEvent.Date == new DateTime(2007, 8, 3)) continue;
                    
                                // Special Case: BRADESPAR
                                if (id == 18724 && corpEvent.Date == new DateTime(2004, 7, 2)) corpEvent.ProvShare = .02m;

                                // Save
                                if (corpEvent.Date >= cutoff) thisIdEvents.Add(corpEvent);
                            }
                        }
                    }
                }
                #endregion
            }
            catch (Exception e)
            {
                var output = id.ToString("00000 ") + e.Message + "\r\n";
                File.AppendAllText("Err.txt", output);
                Console.Write(output);
                return null;
            }

            // Special Case: CPFL Energia S.A.
            if (id == 18660)
            {
                var corpEvents = thisIdEvents.FindAll(e => e.Date == new DateTime(2011, 6, 28));
                thisIdEvents.Remove(corpEvents.Last()); corpEvents.First().ProvShare = 2m;
            }

            Console.Write(id.ToString("00000 (") + symbol.Substring(0, 4) + "): ");
            return thisIdEvents;
        }

        private static async void GetDaily()
        {
            var result = "";
            var timer = DateTime.Now;
            var client = new HttpClient() { MaxResponseContentBufferSize = 1000000 };
            var url = "http://www.bmfbovespa.com.br/Pregao-Online/ExecutaAcaoAjax.asp?CodigoPapel=";
            
            for (var i = 0; i < _iBrA.Length; i++)
            {
                result += await client.GetStringAsync(url + _iBrA[i]);
            }

            File.WriteAllText(@"C:\Users\alex\Documents\IBOV\IBrA.txt", result);
            Console.WriteLine(DateTime.Now - timer);
        }

        private static async void ReadCOTAHIST()
        {
            Console.WriteLine("\t" + DateTime.Now);
            _iBrA = GetCodeAndName("Ibovespa", true).Keys.ToArray();
            var bvspfiles = new DirectoryInfo(_root + @"\Stock\").GetFiles("COTAHIST_A*zip");
            
            foreach (var bvspfile in bvspfiles)
            {
                string filename = bvspfile.FullName;
                if (int.Parse(bvspfile.Name.Substring(10, 4)) < 1998) continue;
                
                await ReadCOTAHISTFile(filename);
            }
        }

        private static async Task ReadCOTAHISTFile(string filename)
        {
            int type;
            var candles = new List<Candle>();

            using (var zip2open = new FileStream(filename, FileMode.Open, FileAccess.Read))
            {
                using (var archive = new ZipArchive(zip2open, ZipArchiveMode.Read))
                {
                    foreach (var entry in archive.Entries)
                    {
                        using (var file = new StreamReader(entry.Open()))
                        {
                            while (!file.EndOfStream)
                            {
                                var line = await file.ReadLineAsync();

                                if (!int.TryParse(line.Substring(16, 3), out type) || type > 11) continue;

                                line = line
                                    .Replace("BMEF", "BVMF")
                                    .Replace("TLPP", "VIVT")
                                    .Replace("VNET", "CIEL")
                                    .Replace("VCPA", "FIBR")
                                    .Replace("PRGA", "BRFS")
                                    .Replace("AMBV4", "ABEV3")
                                    .Replace("DURA4", "DTEX3");

                                var candle = new Candle(line);
                                if (candle.OpenTime < new DateTime(1998, 3, 16)) continue;
                                candles.Add(candle);
                            }
                        }

                        Console.WriteLine("Read " + entry.Name + " " + DateTime.Now + " " + candles.Count);

                        var imin = candles.First().OpenTime.Month;

                        for (var i = imin; i < 13; i++)
                        {
                            var thismonth = candles.FindAll(c => c.OpenTime.Month == i).ToList();
                            candles.RemoveAll(c => c.OpenTime.Month == i);
                            var symbols = new List<string>();
                            thismonth.ForEach(t => { var s = t.Symbol; if (!symbols.Contains(s)) symbols.Add(s); });

                            var dictionary = new Dictionary<string, decimal>();

                            foreach (var symbol in symbols)
                            {
                                var trades = thismonth
                                    .FindAll(t => t.Symbol == symbol && t.MinValue >= 3)
                                    .Sum(t => t.Trades);
                                dictionary.Add(symbol, trades);
                            }

                            var ordered = (from pair in dictionary orderby pair.Value descending select pair).ToList();
                            if (ordered.Count > 100) ordered.RemoveRange(100, ordered.Count - 100);
                            symbols.Clear();
                            _iBrA.ToList().ForEach(symbol => { var s = symbol.Substring(0, 4); if (!symbols.Contains(s)) symbols.Add(s); });
                            ordered.ForEach(kvp => { var s = kvp.Key.Substring(0, 4); if (!symbols.Contains(s)) symbols.Add(s); });

                            var newcandles = thismonth.FindAll(t => symbols.Contains(t.Symbol.Substring(0, 4))).ToList();
                            Console.WriteLine(i.ToString("\tMonth: 00") + " " + thismonth.Count + " > " + newcandles.Count);

                            candles.AddRange(newcandles);
                        }
                        Console.WriteLine("Read*" + entry.Name + " " + DateTime.Now + " " + candles.Count);                        
                    }
                }
            }
            Serialize(filename.ToUpper().Replace(".ZIP", ".xml"), candles.OrderBy(t => t.OpenTime).ThenBy(a => a.Symbol).ToList());
        }

        private static async void COTAHIST2ASCII()
        { 
            Console.WriteLine("\t" + DateTime.Now);
            var index = GetCodeAndName("Ibovespa", true);
            var culture = CultureInfo.CreateSpecificCulture("en-US");
            

            #region Prices
            var alldata = new List<Candle>();
            var bvspfiles = new DirectoryInfo(_root + @"\Stock\").GetFiles("COTAHIST_A2*xml").ToList();
            bvspfiles.RemoveAll(f => int.Parse(f.Name.Substring(10, 4)) < 2004);
            foreach (var bvspfile in bvspfiles)
            {
                var data = new List<Candle>();
                Deserialize(bvspfile.FullName, out data);
                if (index.Count > 0) data.RemoveAll(c => !index.Keys.Contains(c.Symbol));
                if (data.Count > 0) alldata.AddRange(data);
            }
            if (index.Count == 0) alldata.ForEach(c => { if (!index.ContainsKey(c.Symbol)) index.Add(c.Symbol, c.Symbol); });
            #endregion

            #region Corp Events
            var allevents = new List<CorporateEvents>();
            Deserialize(fileprov.FullName, out allevents);
            #endregion

            var file = bvspfiles.First().DirectoryName + @"\firstline.dat";
            File.Delete(file);

            foreach (var kvp in index)
            {
                var candles = alldata.FindAll(c => c.Symbol == kvp.Key);
                if (candles.Count == 0) continue;

                var header = kvp.Value + " (" + kvp.Key + ")\r\n";
                Console.Write(header);

                #region Print Events
                var events = allevents.FindAll(c => c.Symbols.Split(',').Contains(kvp.Key) &&
                    (c.ProvCashON.HasValue || c.ProvCashPN.HasValue || c.ProvCashPNA.HasValue || c.ProvCashPNB.HasValue || c.ProvCashUNT.HasValue));

                events.Clear();

                if (events.Count > 0)
                {
                    var divfile = bvspfiles.First().DirectoryName + @"\" + kvp.Key + "_div.dat";
                    if (File.Exists(divfile)) File.Delete(divfile);

                    File.AppendAllText(divfile, header + "DATE\tDIVIDENDS\r\n");
                    foreach (var cevent in events)
                    {
                        allevents.Remove(cevent);

                        var provCash = new Dictionary<string, decimal?>
                        {
                            {"3", cevent.ProvCashON},
                            {"4", cevent.ProvCashPN},
                            {"5", cevent.ProvCashPNA},
                            {"6", cevent.ProvCashPNB},
                            {"11", cevent.ProvCashUNT},
                        }[kvp.Key.Substring(4)];

                        if(!provCash.HasValue) continue;

                        File.AppendAllText(divfile, cevent.Date.ToString("ddMMMyy\t", culture) +
                            provCash.Value.ToString("G", culture) + "\r\n");
                    }
                }
                #endregion

                var datfile = bvspfiles.First().DirectoryName + @"\" + kvp.Key + ".dat";
                if (File.Exists(datfile)) File.Delete(datfile);

                File.AppendAllText(datfile, header + "Industry: " + candles.First().Indicators + "\r\n");
                File.AppendAllText(datfile, "DATE\tOPEN\tHIGH\tLOW\tCLOSE\tADJCLOSE\tVOLUME\r\n");
                foreach (var candle in candles)
                {
                    alldata.Remove(candle);

                    var output = candle.OpenTime.ToString("ddMMMyy\t", culture) +
                        candle.OpenValue.ToString("0.00\t", culture) +
                        candle.MaxValue.ToString("0.00\t", culture) +
                        candle.MinValue.ToString("0.00\t", culture) +
                        candle.CloseValue.ToString("0.00\t", culture) +
                        candle.AdjClValue.ToString("0.000000\t", culture) +
                        candle.Volume.ToString("F6", culture) + "\r\n";

                    File.AppendAllText(datfile, output);
                    if (candle == candles.First())
                    {
                        Console.Write(output);
                        File.AppendAllText(file, candle.Symbol + "\t" + output);
                    }
                }
            }
        }
  
        private static void COTAHIST2CSV()
        {
            Console.WriteLine("\t" + DateTime.Now);
            var cedir = new DirectoryInfo(@"C:\Users\alex\Documents\IBOV\CorporativeEvents\");
            var candles = new List<Candle>();
            //DeserializeCandle(@"C:\Users\alex\Documents\IBOV\Stock\COTAHIST_1998_A_2014.xml");
            
            while (candles.Count > 0)
            {
                var symbol = candles.First().Symbol;
                var subcandles = candles.FindAll(c => c.Symbol == symbol);
                Console.WriteLine(candles.RemoveAll(c => c.Symbol == symbol) + " candles de " + symbol + " removidos");

                var cedata = cedir.GetFiles(symbol.Substring(0, 4) + "*");
            }        
        }

        private static async void ReadNEG(bool isStock, DateTime cutoff)
        {
            _iBrA = new string[] { "PETR", "VALE" };
            var assetType = isStock ? "Stock" : "Options";
            if (isStock) _iBrA = new string[] { "PETR4", "VALE5", "BBAS3", "MRFG3", "USIM5" };

            var bvspfiles = new DirectoryInfo(@"C:\Users\Alexandre\Documents\IBOV\" + assetType + @"\").GetFiles("NEG_*.zip");
            foreach (var bvspfile in bvspfiles)
            {
                var fileDateTime = bvspfile.Name.Substring(bvspfile.Name.Length - 12, 8);
                var fileStartDate = DateTime.ParseExact(fileDateTime, "yyyyMMdd", CultureInfo.InvariantCulture);
                if (fileStartDate < cutoff) continue;
                var dic = new Dictionary<DateTime, string[]>();
                    
                #region isStock
                if (isStock)
                {
                    var dates = new List<DateTime>();
                    var candles = new List<Candle>();
                    var dir = bvspfile.DirectoryName + @"\COTAHIST_A";

                    if (fileStartDate.Month == 1)
                    {
                        Deserialize(dir + (fileStartDate.Year - 1).ToString() + ".xml", out candles);
                        candles.RemoveAll(c => c.OpenTime.Date != candles.Last().OpenTime.Date);
                    }
                    List<Candle> thiscandles;
                    Deserialize(dir + (fileStartDate.Year - 0).ToString() + ".xml", out thiscandles);
                    candles.AddRange(thiscandles);

                    candles.ForEach(c => { var date = c.OpenTime.Date; if (!dates.Contains(date)) dates.Add(date); });

                    for (var i = 1; i < dates.Count; i++)
                    {
                        var symbols = new List<string>();    
                        thiscandles = candles.FindAll(c => c.OpenTime.Date == dates[i - 1]);
                        thiscandles.ForEach(c => { var s = c.Symbol.Substring(0, 4); if (!symbols.Contains(s)) symbols.Add(s); });
                        dic.Add(dates[i], symbols.ToArray());
                    }
                }
                #endregion

                using (var zip2open = new FileStream(bvspfile.FullName, FileMode.Open, FileAccess.Read))
                {
                    using (var archive = new ZipArchive(zip2open, ZipArchiveMode.Read))
                    {
                        var ticks = new List<Tick>();
                        var starttime = DateTime.Now;

                        Console.WriteLine("Start reading " + bvspfile.Name + " at " + starttime);

                        foreach (var entry in archive.Entries)
                        {
                            using (var file = new StreamReader(entry.Open()))
                            {
                                while (!file.EndOfStream)
                                {
                                    var line = await file.ReadLineAsync();
                                    var data = line.Split(';');
                                    if (data.Length < 5) continue;

                                    if (!_iBrA.Contains(data[1].Substring(0, (isStock ? 5 : 4)))) continue;

                                    var tick = new Tick(line);
                                    if (ticks.Count == 0) { ticks.Add(tick); continue; }

                                    var last = ticks.Last();

                                    if (tick.Symbol == last.Symbol && tick.Value == last.Value && tick.Time.Minute == last.Time.Minute)
                                        last.Qnty += tick.Qnty;
                                    else
                                        ticks.Add(tick);
                                }
                            }
                            Console.WriteLine("Read " + entry.Name + (DateTime.Now - starttime).TotalMinutes.ToString(" after #.00 minutes."));
                        }
                        Serialize(bvspfile.FullName, ticks.OrderBy(t => t.Time).ToList());
                    }
                }
            }
            Console.WriteLine("Done!");
        }

        private static async void GetLastFromCOTAHIST()
        {
            var starttime = DateTime.Now;
            var dicquotes = new ConcurrentDictionary<string,DateTime>();
            var tmp = "DefaultEndpointsProtocol=https;AccountName=giminidatabase;AccountKey=8Kt6cGFM4lYsSUvm1i8uBbazEVLV+t1F92log2T8AnHA/bdKxPvcFUG/f9PbsqZWaEn+YZmfpV/pV+O/sRrHeA==";
            var tableClient = CloudStorageAccount.Parse(tmp).CreateCloudTableClient();
            var table = tableClient.GetTableReference("Daily");
            var quotes = table.ExecuteQuery(new TableQuery<AzureCandle>()
                .Where(TableQuery.GenerateFilterConditionForDate("Time", QueryComparisons.GreaterThanOrEqual, new DateTime(2014, 2, 1))))
                .ToList().OrderBy(q => q.OpenTime);

            if (quotes.Any()) foreach (var quote in quotes) dicquotes.AddOrUpdate(quote.PartitionKey, quote.OpenTime, (k, v) => v = quote.OpenTime);
            foreach (var q in dicquotes)
                Console.WriteLine(q.Key + " " + q.Value);

            using (var zip2open = new FileStream(@"C:\Users\alex\Documents\IBOV\Stock\COTAHIST_D07022014.zip", FileMode.Open, FileAccess.Read))
            {
                using (var archive = new ZipArchive(zip2open, ZipArchiveMode.Read))
                {
                    foreach (var entry in archive.Entries)
                    {
                        using (var file = new StreamReader(entry.Open()))
                        {
                            while (!file.EndOfStream)
                            {
                                var line = await file.ReadLineAsync();
                                var output = line.Substring(12, 7).Trim();
                                if (!_iBrA.Contains(output)) continue;
                                output += ";" + line.Substring(8, 2) + "/" + line.Substring(6, 2) + "/" + line.Substring(2, 4) + ";";
                                if (dicquotes[output.Split(';')[0]] >= DateTime.Parse(output.Split(';')[1])) continue;
                                
                                output += double.Parse(line.Substring(56, 13)) / 100 + ";";
                                output += double.Parse(line.Substring(69, 13)) / 100 + ";";
                                output += double.Parse(line.Substring(82, 13)) / 100 + ";";
                                output += double.Parse(line.Substring(108, 13)) / 100 + ";";
                                output += double.Parse(line.Substring(170, 16)) + ";";
                                output += Int64.Parse(line.Substring(152, 18)) + ";";

                                var data = output.Split(';');

                                //var resultb = await table.ExecuteAsync(TableOperation
                                //    .InsertOrReplace(new Candle(data[0], data[1], data[2], data[3], data[4], data[5], data[6], data[7])));

                                line = @"C:\Users\alex\Documents\IBOV\RawData\" + output.Split(';')[0] + ".csv";
                                using (var writer = File.AppendText(line)) await writer.WriteLineAsync(output);
                            }
                        }
                        Console.WriteLine("Read " + entry.Name + (DateTime.Now - starttime).TotalMinutes.ToString(" after #.00 minutes."));
                    }
                }
            }
        }

        private static async void BuildPropTrading(DateTime cutoff)
        {
            var timer = DateTime.Now;
            Console.Write("\t" + timer);
            var bvspfiles = new DirectoryInfo(@"C:\Users\alex\Documents\IBOV\Stock\").GetFiles("NEG_*.zip");
            
            foreach (var bvspfile in bvspfiles)
            {
                var fileStartDate = DateTime.ParseExact(bvspfile.Name.Substring(4, 8), "yyyyMMdd", CultureInfo.InvariantCulture);
                if (fileStartDate < cutoff) continue;

                var yyyy = fileStartDate.Month == 1 ? fileStartDate.Year - 1 : fileStartDate.Year;

                var candles = new List<Candle>();
                Deserialize(bvspfile.DirectoryName + @"\COTAHIST_A" + yyyy + ".xml", out candles);

                yyyy = fileStartDate.Month == 1 ? 12 : fileStartDate.Month-1;
                candles.RemoveAll(c => c.OpenTime.Month != yyyy);

                var symbols = new List<string>();
                candles.ForEach(c => { if (!symbols.Contains(c.Symbol)) symbols.Add(c.Symbol); });
                
                using (var zip2open = new FileStream(bvspfile.FullName, FileMode.Open, FileAccess.Read))
                {
                    using (var archive = new ZipArchive(zip2open, ZipArchiveMode.Read))
                    {
                        foreach (var entry in archive.Entries)
                        {
                            using (var file = new StreamReader(entry.Open()))
                            {
                                var ticks = new List<Tick>();
                                var starttime = DateTime.Now;

                                Console.WriteLine("Start reading " + entry.Name + " at " + starttime);

                                while (!file.EndOfStream)
                                {
                                    var line = await file.ReadLineAsync();
                                    var tick = new Tick(line);

                                    if (symbols.Contains(tick.Symbol))
                                    {
                                        var test =
                                            ticks.Count == 0 ||
                                            ticks.Last().Value != tick.Value ||
                                            ticks.Last().Symbol != tick.Symbol ||
                                            ticks.Last().Time.Minute != tick.Time.Minute;

                                        if (test)
                                            ticks.Add(tick);
                                        else
                                            ticks.Last().Qnty += tick.Qnty;
                                    }
                                }

                                var filename = bvspfile.FullName.Replace(bvspfile.Name, entry.Name);
                                Serialize(filename, ticks.OrderBy(t => t.Time).ToList());
                                Console.WriteLine("Read " + entry.Name + " after " + (DateTime.Now - starttime).TotalMinutes.ToString("#00.00") + " minutes.");
                            }
                        }
                    }
                }
            }
        }

        private static void AdjustedPrice()
        {
            var timer = DateTime.Now;
            Console.Write("\t" + timer);

            var adjp = new List<Candle>();
            var earn = new List<CorporateEvents>();
            Deserialize(fileprov.FullName, out earn);

            var prix = new List<Candle>();
            var filecotas = new DirectoryInfo(_root + @"\Stock\").GetFiles("COTAHIST_A*xml");
            _iBrA = GetCodeAndName("Ibovespa", false).Keys.ToArray();
            foreach (var filecota in filecotas)
            {
                var candles = new List<Candle>(); ;
                Deserialize(filecota.FullName, out candles);
                if (candles.Count > 0) prix.AddRange(candles.FindAll(c => _iBrA.Contains(c.Symbol)));
            }

            foreach (var price in prix) price.AdjClValue = price.CloseValue;
            Console.WriteLine((DateTime.Now - timer).TotalSeconds.ToString("\tData read in #.000 seconds"));

            while (prix.Count > 0)
            {
                var symbol = prix.First().Symbol.Substring(0, 4);
                var subprix = prix.FindAll(c => c.Symbol.Contains(symbol));
                var subearn = earn.FindAll(e => e.Symbols.Contains(symbol));

                Console.WriteLine("Processando " + symbol +
                    prix.RemoveAll(c => c.Symbol.Contains(symbol)).ToString(": 0000 cotações com ") +
                    earn.RemoveAll(e => e.Symbols.Substring(0, 4) == symbol).ToString("000 proventos"));

                foreach (var e in subearn)
                {
                    subprix.ForEach(p => p.Indicators = e.Sector);

                    if (e.ProvShare.HasValue && e.ProvShare > 0)
                    {
                        subprix.FindAll(p => p.OpenTime <= e.Date)
                           .ForEach(p => p.AdjClValue = p.AdjClValue / e.ProvShare.Value);
                    }

                    if (e.ProvCashON.HasValue)
                    {
                        subprix.FindAll(p => p.OpenTime <= e.Date && p.Symbol.Contains("3"))
                            .ForEach(p => p.AdjClValue = p.AdjClValue * (1 - e.ProvCashON.Value));
                    }

                    if (e.ProvCashPN.HasValue)
                    {
                        subprix.FindAll(p => p.OpenTime <= e.Date && p.Symbol.Contains("4"))
                            .ForEach(p => p.AdjClValue = p.AdjClValue * (1 - e.ProvCashPN.Value));
                    }

                    if (e.ProvCashPNA.HasValue)
                    {
                        subprix.FindAll(p => p.OpenTime <= e.Date && p.Symbol.Contains("5"))
                            .ForEach(p => p.AdjClValue = p.AdjClValue * (1 - e.ProvCashPNA.Value));
                    }

                    if (e.ProvCashPNB.HasValue)
                    {
                        subprix.FindAll(p => p.OpenTime <= e.Date && p.Symbol.Contains("6"))
                            .ForEach(p => p.AdjClValue = p.AdjClValue * (1 - e.ProvCashPNB.Value));
                    }

                    if (e.ProvCashUNT.HasValue)
                    {
                        subprix.FindAll(p => p.OpenTime <= e.Date && p.Symbol.Contains("11"))
                            .ForEach(p => p.AdjClValue = p.AdjClValue * (1 - e.ProvCashUNT.Value));
                    }
                }

                adjp.AddRange(subprix);
            }

            for (var i = 1998; i <= 2014; i++)
            {
                Serialize(filecota.FullName.Replace("2014", i.ToString("0000")),
                    adjp.Where(a => a.OpenTime.Year == i).OrderBy(a => a.OpenTime).ThenBy(a => a.Symbol).ToList());
            }

            return;
        }

        private static void AdjustedPrice(string symbol, DateTime last)
        {
            var allticks = new List<Tick>();
            var timer = DateTime.Now;
            Console.WriteLine("\t" + timer + "\t" + symbol);

            var files = new DirectoryInfo(_root + @"\Stock").GetFiles("NEG_*xml").ToList();
            var index = files.FindIndex(b => b.Name.Contains("20141001"));

            for (var i = 0; false && i < index; i++)
            {
                var ttt = DateTime.Now;
                Console.Write(files[i].Name);
                var ticks = new List<Tick>();
                Deserialize(files[i].FullName, out ticks);
                if (ticks.First().Time > last) continue;
                allticks.AddRange(ticks.FindAll(t => t.Symbol == symbol && t.Time <= last));
                Console.WriteLine((DateTime.Now - ttt).TotalSeconds.ToString(" read in #.000 seconds"));
            }
            if (allticks.Count > 0)
                Serialize(_root + @"\Stock\" + symbol +
                    allticks.First().Time.ToString("_yyyyMMdd_") + "A" +
                    allticks.Last().Time.ToString("_yyyyMMdd") + ".xml", allticks);
            
            allticks.Clear();
            for (var i = index; i < files.Count; i++)
            {
                var ttt = DateTime.Now;
                Console.Write(files[i].Name);
                var ticks = new List<Tick>();
                Deserialize(files[i].FullName, out ticks);
                if (ticks.First().Time > last) continue;
                allticks.AddRange(ticks.FindAll(t => t.Symbol == symbol && t.Time <= last));
                Console.WriteLine((DateTime.Now - ttt).TotalSeconds.ToString(" read in #.000 seconds"));
            }
            if (allticks.Count > 0)
                Serialize(_root + @"\Stock\" + symbol +
                    allticks.First().Time.ToString("_yyyyMMdd_") + "A" +
                    allticks.Last().Time.ToString("_yyyyMMdd") + ".xml", allticks);

            Console.WriteLine(symbol + (DateTime.Now - timer).TotalSeconds.ToString(" . Done in #.000 seconds"));
        }

        private static void CreatePriceMatrix()
        {
            var timer = DateTime.Now;
            Console.Write("\t" + timer);

            var output = "Trading Day";
            var symbols = new List<string>();
            var sectors = new List<string>();
            var symbysec = new ConcurrentDictionary<string, List<string>>();
            var cutoff = new DateTime(1998, 1, 1);
            var file = @"C:\Users\alex\Documents\IBOV\Stock\ADJCLOSE_" + cutoff.Year + "_A_2014.xml";
            var quotes = new List<Candle>();
            Deserialize(file, out quotes);
            quotes = quotes.FindAll(q => q.OpenTime.Year >= 2009 && q.Symbol.Contains("BBDC"));
            quotes.Where(q => q.OpenTime.AddMonths(3) >= DateTime.Today).ToList()
                .ForEach(q => 
                {
                    var sector = q.Indicators.Split('/')[0].Trim();
                    if (!symbysec.TryAdd(sector, new List<string>()))
                    {
                        if (!symbysec[sector].Contains(q.Symbol)) symbysec[sector].Add(q.Symbol);
                    }
                    if (!sectors.Contains(sector)) sectors.Add(sector);
                    if (!symbols.Contains(q.Symbol)) symbols.Add(q.Symbol);
                });

            foreach (var kvp in symbysec)
            {
                var tmpsymbols = kvp.Value.OrderBy(s => s).ToList();
                var tmpoutput = "Trading Day";
                foreach (var symbol in tmpsymbols) tmpoutput = tmpoutput + ";" + symbol;

                var newfile = file.Replace(".xml", "_" + kvp.Key.Replace(" ", "_") + ".csv");
                if (File.Exists(newfile)) File.Delete(newfile);
                File.AppendAllText(newfile, tmpoutput);
                Console.WriteLine(newfile + " written!");
            }

            symbols = symbols.OrderBy(s => s).ToList();
            foreach (var symbol in symbols) output = output + ";" + symbol;
            
            Console.WriteLine((DateTime.Now - timer).TotalSeconds.ToString("\tData read in #.000 seconds"));

            while (quotes.Count > 0)
            {
                var today = quotes.First().OpenTime.Date;
                var subq = quotes.FindAll(q => q.OpenTime.Date == today);
                
                Console.WriteLine(quotes.RemoveAll(q => q.OpenTime.Date == today).ToString("000 cotações no dia ") + today.ToShortDateString());

                foreach (var kvp in symbysec)
                {
                    var tmpsymbols = kvp.Value.OrderBy(s => s).ToList();
                    var tmpoutput = "\r\n" + today.ToShortDateString();
                    foreach (var symbol in tmpsymbols)
                    {
                        var quote = subq.Find(q => q.Symbol == symbol);
                        tmpoutput = quote == null ? tmpoutput + ";" : tmpoutput + ";" + quote.AdjClValue +";" + quote.CloseValue;
                    }
                    File.AppendAllText(file.Replace(".xml", "_" + kvp.Key.Replace(" ", "_") + ".csv"), tmpoutput);
                }
            }
            //file = file.Replace(".xml", ".csv");
            //if (File.Exists(file)) File.Delete(file);
            //File.AppendAllText(file, output);
            //Console.WriteLine(file + " written!");
        }
       
        private static  void GetNEGfromFTP()
        {
            // Get the object used to communicate with the server.
            var request = (FtpWebRequest)WebRequest.Create("ftp://ftp.bmf.com.br/marketdata/Bovespa-Vista/NEG_20111001_A_20111031.zip");
            request.Method = WebRequestMethods.Ftp.DownloadFile;

            // This example assumes the FTP site uses anonymous logon.
            request.Credentials = new NetworkCredential("anonymous", "janeDoe@contoso.com");

            var response = (FtpWebResponse)request.GetResponse();

            var responseStream = response.GetResponseStream();
            var reader = new StreamReader(responseStream);
            Console.WriteLine(reader.ReadToEnd());

            Console.WriteLine("Download Complete, status {0}", response.StatusDescription);

            reader.Close();
            response.Close();  
        }

        #region Serialization
        private static void Serialize<T>(string file, List<T> data)
        {
            if (data.Count == 0) return;

            var xmlfile = file.ToUpper().Replace(".ZIP", ".xml").Replace(".TXT", ".xml");
            using (var tw = new StreamWriter(xmlfile))
                (new XmlSerializer(typeof(List<T>))).Serialize(tw, data);
            Console.WriteLine(data.Count + " elements written to\r\n" + xmlfile);
        }
        private static void Deserialize<T>(string file, out List<T> data)
        {
            data = new List<T>();
            if (!File.Exists(file)) return;

            using (var rw = new StreamReader(file)) 
                data = (List<T>)(new XmlSerializer(typeof(List<T>))).Deserialize(rw);
        }
        #endregion
    }
}
