$(function () {

    var hub = $.connection.tradeHub;   // Get a reference to our hub

    $('#noTradesToMonitorMessage').hide();

    function GridViewModel(trades) {
        var self = this;
        self.trades = ko.observableArray(trades);
    }

    function controller() {
        var self = this;
        self.model = new GridViewModel([]);
        self.siteMonitorHub = hub;
        
        self.toggleGrid = function () {
            if ($('.site').length == 0) {
                $('#noTradesToMonitorMessage').show();
                $('#trades').hide();
            }
            else {
                $('#noTradesToMonitorMessage').hide();
                $('#trades').show();
            }
        }

        self.toggleSpinner = function (isVisible) {
            if (isVisible == true)
                $('#spin').show();
            if (isVisible != true)
                $('#spin').hide();
        };
    }

    var c = new controller();

    c.siteMonitorHub
        .on('serviceIsUp', function () {
            c.toggleSpinner(true);
            c.siteMonitorHub.invoke('getTradeList');
        })
        .on('siteListObtained', function (sites) {
            $(sites).each(function (i, site) {
                c.addSite(site);
            });
            c.toggleSpinner(false);
            c.toggleGrid();

            $('.removeSite').on('click', function () {
                c.toggleSpinner(true);
                var url = $(this).data('url');

                $('.site[data-url="' + url + '"]').fadeOut('fast', function () {
                    $('.site[data-url="' + url + '"]').remove();
                });

                c.siteMonitorHub.invoke('removeSite', url);
            });
        })
        .on('siteStatusUpdated', function (monitorUpdate) {
            c.updateSiteStatus(monitorUpdate);
            c.toggleSpinner(false);
        })
        .on('siteAddedToGui', function (url) {
            $('#siteUrl').val('http://');
            $('#siteUrl').focus();
            c.toggleSpinner(false);
            c.toggleGrid();
        })
        .on('siteRemovedFromGui', function (url) {
            $('.site[data-url="' + url + '"]').remove();
            c.toggleGrid();
            c.toggleSpinner(false);
        })
        .on('checkingSite', function (url) {
            c.toggleSpinner(false);
            c.updateSite(url, 'btn-info', 'Checking');
        });

    $('#addSite').click(function () {
        var u = $('#siteUrl').val();
        c.addSite(u);
        c.toggleSpinner(true);
        c.siteMonitorHub.invoke('addSite', u);
    });

    c.connection.start().done(function () {
        c.toggleSpinner(true);
        c.siteMonitorHub.invoke('getTradeList');
    });

    ko.applyBindings(c.model);

//    var ViewModel = function () {
//        var self = this;
//        self.quotes = ko.observableArray();
//        self.trades = ko.observableArray();
//    };

//    var vm = new ViewModel();
//    ko.applyBindings(vm, $("#quoteInfo")[0]);
//    ko.applyBindings(vm, $("#stockTable")[0]);

//    var hub = $.connection.tradeHub;   // Get a reference to our hub

//    function initQuotes() {
//        return hub.savedQuotes().done(function (stocks) {
//            $.each(stocks, function () {
//                var stock = formatStock(this);

//                var model = ko.mapping.fromJS(stock);

//                // Check if we already have it:
//                var match = ko.utils.arrayFirst(vm.quotes(), function (item) {
//                    return item.Symbol() == stock.Symbol;
//                });

//                if (!match)
//                    vm.quotes.push(model);
//                else {
//                    var index = vm.quotes.indexOf(match);
//                    vm.quotes.replace(vm.quotes()[index], model);
//                }
//            });
//        });
//    }

//    function initTrades() {
//        return hub.savedTrades().done(function (trades) {
//            $.each(trades, function () {
//                var trade = formatTrade(this);

//                var model = ko.mapping.fromJS(trade);

//                // Check if we already have it:
//                var match = ko.utils.arrayFirst(vm.trades(), function (item) {
//                    return item.ID() == trade.ID;
//                });

//                if (!match)
//                    vm.trades.push(model);
//                else {
//                    var index = vm.trades.indexOf(match);
//                    vm.trades.replace(vm.trades()[index], model);
//                }
//            });
//        });
//    };


//    function formatStock(stock) {
//        return $.extend(stock, {
//            //PercentChange: (stock.PercentChange * 100).toFixed(2) + '%'
//            //Direction: stock.Change === 0 ? '' : stock.Change >= 0 ? up : down,
//            //DirectionClass: stock.Change === 0 ? 'even' : stock.Change >= 0 ? 'up' : 'down'
//        });
//    }

//    function formatTrade(trade) {
//        return $.extend(trade, {
//            //StartValue: trade.StartValue.toFixed(2)
//        });
//    }

//    // Add client-side hub methods that the server will call
//    hub.quoteMessage = function (stock) {
//        var displayStock = formatStock(stock);

//        var model = ko.mapping.fromJS(displayStock);

//        // Check if we already have it:
//        var match = ko.utils.arrayFirst(vm.quotes(), function (item) {
//            return item.Symbol() == displayStock.Symbol;
//        });

//        if (!match)
//            vm.quotes.push(model);
//        else {
//            var index = vm.quotes.indexOf(match);
//            vm.quotes.replace(vm.quotes()[index], model);
//        }
//        bg = stock.LastChange === 0
//                ? '255,216,0'        // yellow
//                : stock.LastChange > 0
//                    ? '154,240,117'  // green
//                    : '255,148,148'; // red
//    };

//    // Add client-side hub methods that the server will call
//    hub.tradeMessage = function (trade) {
//        var displayTrade = formatTrade(trade);

//        var model = ko.mapping.fromJS(displayTrade);

//        // Check if we already have it:
//        var match = ko.utils.arrayFirst(vm.trades(), function (item) {
//            return item.ID() == displayTrade.ID;
//        });

//        if (!match)
//            vm.trades.push(model);
//        else {
//            var index = vm.trades.indexOf(match);
//            vm.trades.replace(vm.trades()[index], model);
//        }
//        bg = stock.LastChange === 0
//                ? '255,216,0'        // yellow
//                : stock.LastChange > 0
//                    ? '154,240,117'  // green
//                    : '255,148,148'; // red
//    };


//    // Start the connection
//    $.connection.hub.start()
//        .done(function () { return initQuotes(); })
//        .done(function () { return initTrades(); });
});

///// <reference path="../scripts/jquery-1.9.1.js" />
///// <reference path="../scripts/jquery.signalR-1.1.2.js" />

///*!
//    ASP.NET SignalR Stock Ticker Sample
//*/

//// Crockford's supplant method (poor man's templating)
//if (!String.prototype.supplant) {
//    String.prototype.supplant = function (o) {
//        return this.replace(/{([^{}]*)}/g,
//            function (a, b) {
//                var r = o[b];
//                return typeof r === 'string' || typeof r === 'number' ? r : a;
//            }
//        );
//    };
//}

//// A simple background color flash effect that uses jQuery Color plugin
//jQuery.fn.flash = function (color, duration) {
//    var current = this.css('backgroundColor');
//    this.animate({ backgroundColor: 'rgb(' + color + ')' }, duration / 2)
//        .animate({ backgroundColor: current }, duration / 2);
//}

//$(function () {

//    var ticker = $.connection.TradeHub, // the generated client-side hub proxy
//        up = '▲',
//        down = '▼',
//        $stockTable = $('#stockTable'),
//        $stockTableBody = $stockTable.find('tbody'),
//        rowTemplate = '<tr data-symbol="{Symbol}"><td>{Symbol}</td><td>{Price}</td><td>{DayOpen}</td><td>{DayHigh}</td><td>{DayLow}</td><td><span class="dir {DirectionClass}">{Direction}</span> {Change}</td><td>{PercentChange}</td></tr>',
//        $stockTicker = $('#stockTicker'),
//        $stockTickerUl = $stockTicker.find('ul'),
//        liTemplate = '<li data-symbol="{Symbol}"><span class="symbol">{Symbol}</span> <span class="price">{Price}</span> <span class="change"><span class="dir {DirectionClass}">{Direction}</span> {Change} ({PercentChange})</span></li>';

//    function formatStock(stock) {
//        return $.extend(stock, {
//            Price: stock.Price.toFixed(2),
//            PercentChange: (stock.PercentChange * 100).toFixed(2) + '%',
//            Direction: stock.Change === 0 ? '' : stock.Change >= 0 ? up : down,
//            DirectionClass: stock.Change === 0 ? 'even' : stock.Change >= 0 ? 'up' : 'down'
//        });
//    }

//    function scrollTicker() {
//        var w = $stockTickerUl.width();
//        $stockTickerUl.css({ marginLeft: w });
//        $stockTickerUl.animate({ marginLeft: -w }, 15000, 'linear', scrollTicker);
//    }

//    function stopTicker() {
//        $stockTickerUl.stop();
//    }

//    function init() {
//        return ticker.server.getAllStocks().done(function (stocks) {
//            $stockTableBody.empty();
//            $stockTickerUl.empty();
//            $.each(stocks, function () {
//                var stock = formatStock(this);
//                $stockTableBody.append(rowTemplate.supplant(stock));
//                $stockTickerUl.append(liTemplate.supplant(stock));
//            });
//        });
//    }

//    // Add client-side hub methods that the server will call
//    $.extend(ticker.client, {
//        updateStockPrice: function (stock) {
//            var displayStock = formatStock(stock),
//                $row = $(rowTemplate.supplant(displayStock)),
//                $li = $(liTemplate.supplant(displayStock)),
//                bg = stock.LastChange < 0
//                        ? '255,148,148' // red
//                        : '154,240,117'; // green

//            $stockTableBody.find('tr[data-symbol=' + stock.Symbol + ']')
//                .replaceWith($row);
//            $stockTickerUl.find('li[data-symbol=' + stock.Symbol + ']')
//                .replaceWith($li);

//            $row.flash(bg, 1000);
//            $li.flash(bg, 1000);
//        },

//        marketOpened: function () {
//            $("#open").prop("disabled", true);
//            $("#close").prop("disabled", false);
//            $("#reset").prop("disabled", true);
//            scrollTicker();
//        },

//        marketClosed: function () {
//            $("#open").prop("disabled", false);
//            $("#close").prop("disabled", true);
//            $("#reset").prop("disabled", false);
//            stopTicker();
//        },

//        marketReset: function () {
//            return init();
//        }
//    });

//    // Start the connection
//    $.connection.hub.start()
//        .pipe(init)
//        .pipe(function () {
//            return ticker.server.getMarketState();
//        })
//        .done(function (state) {
//            if (state === 'Open') {
//                ticker.client.marketOpened();
//            } else {
//                ticker.client.marketClosed();
//            }

//            // Wire up the buttons
//            $("#open").click(function () {
//                ticker.server.openMarket();
//            });

//            $("#close").click(function () {
//                ticker.server.closeMarket();
//            });

//            $("#reset").click(function () {
//                ticker.server.reset();
//            });
//        });
//});