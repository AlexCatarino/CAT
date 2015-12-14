// ## -*- coding: utf-8 -*-

/*jslint browser:true, sub:true, white:false */

function handledecimal(s) {
    var t = s;
    // replace all commas by points
    while (t.indexOf(",") != -1) {
        t = t.replace(",", ".");
    }
    // remove all points but the last one, if any
    while (t.indexOf(".") != t.lastIndexOf(".")) {
        t = t.substring(0, t.indexOf(".")) + t.substring(t.indexOf(".") + 1);
    }
    return t;
}

function trim(stringToTrim) {
    return stringToTrim.replace(/^\s+|\s+$/g, "");
}

function object_by_id(id) {
    var returnVar = undefined;
    if (document.getElementById) {
        returnVar = document.getElementById(id);
    } else if (document.all) {
        returnVar = document.all[id];
    } else if (document.layers) {
        returnVar = document.layers[id];
    }
    return returnVar;
}

var normal_year = [31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31];
var leap_year = [31, 29, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31];

function tzoffset(d) {
    // returns the time zone offset, expressed as "hours *behind* UTC".
    // that would be 180 minutes for Brazil (-0300) and -60 minutes for Germany (+0100)
    return d.getTimezoneOffset() * 60000;
}

function ctod(s) {
    var tks = s.split('/');
    if (tks.length != 3) {
        return null;
    }

    for (var i = 0; i < 3; ++i) {
        tks[i] = parseInt(tks[i], 10);
        if (isNaN(tks[i])) {
            return null;
        }
    }

    if (tks[0] < 100) {
        tks[0] += 2000;
    }

    if (tks[0] < 0 || tks[0] > 2099 || tks[1] < 1 || tks[1] > 12 || tks[2] < 1 || tks[2] > 31) {
        return null;
    }

    tks[1]--;

    var y = normal_year;
    if (((tks[0] % 4) === 0) && (((tks[0] % 100) !== 0) || ((tks[0] % 400) === 0))) {
        y = leap_year;
    }

    if (tks[2] > y[tks[1]]) {
        return null;
    }

    // at noon, to prevent a daylight saving timezone to change the date.
    return new Date(tks[0], tks[1], tks[2], 12, 0, 0);
}

function zeropad(s, n) {
    while (s.length < n) {
        s = "0" + s;
    }
    return s;
}

function dtoc(t) {
    var exp = new Date();
    // t comes from cookie memory; since it came straight from getTime(),
    // setTime() will remove the timezone offset added by getTime(),
    // and we get the original date anyway.
    exp.setTime(t);
    return "" + zeropad(exp.getFullYear().toFixed(0), 4) + "/" + zeropad((exp.getMonth() + 1).toFixed(0), 2) + "/" + zeropad(exp.getDate().toFixed(0), 2);
}


// Taken from book "Black Scholes and Beyond"

var kd1 = 0.0498673470;
var kd3 = 0.0032776263;
var kd5 = 0.0000488906;
var kd2 = 0.0211410061;
var kd4 = 0.0000380036;
var kd6 = 0.0000053830;

function normdistacum(x) {
    if (x < 0) {
        return 1 - normdistacum(-x);
    }
    var n = 1.0 - 0.5 * Math.pow(1 + kd1 * x + kd2 * Math.pow(x, 2) + kd3 * Math.pow(x, 3) + kd4 * Math.pow(x, 4) + kd5 * Math.pow(x, 5) + kd6 * Math.pow(x, 6), -16);
    return n;
}

function normdist(x) {
    var n = Math.exp(-(Math.pow(x, 2) / 2));
    n /= Math.pow((2 * Math.PI), 0.5);
    return n;
}

function d1(spot, strike, interest, time, volatility) {
    if (volatility < 0.0000001) {
        return 9999999999.9;
    }
    var x = Math.log(spot / strike) + (interest + Math.pow(volatility, 2) / 2) * time;
    x /= volatility * Math.pow(time, 0.5);
    return x;
}

function d2(spot, strike, interest, time, volatility) {
    var x = d1(spot, strike, interest, time, volatility) - volatility * Math.pow(time, 0.5);
    return x;
}

function opremium(spot, strike, interest, time, volatility) {
    var D1 = d1(spot, strike, interest, time, volatility);
    var nd1 = normdistacum(D1);
    var D2 = d2(spot, strike, interest, time, volatility);
    var nd2 = normdistacum(D2);
    return nd1 * spot - Math.exp(-interest * time) * nd2 * strike;
}

function putzopremium(spot, strike, interest, time, volatility) {
    var D1 = d1(spot, strike, interest, time, volatility);
    var nd1 = normdistacum(-D1);
    var D2 = d2(spot, strike, interest, time, volatility);
    var nd2 = normdistacum(-D2);
    return -nd1 * spot + Math.exp(-interest * time) * nd2 * strike;
}


function odelta(spot, strike, interest, time, volatility) {
    var x = normdistacum(d1(spot, strike, interest, time, volatility));
    return x;
}

function putzodelta(spot, strike, interest, time, volatility) {
    var x = normdistacum(d1(spot, strike, interest, time, volatility)) - 1;
    return x;
}

function ogamma(spot, strike, interest, time, volatility) {
    var D1 = d1(spot, strike, interest, time, volatility);
    var x = normdist(D1);
    x /= spot * volatility * Math.pow(time, 0.5);
    return x;
}

function putzogamma(spot, strike, interest, time, volatility) {
    var D1 = d1(spot, strike, interest, time, volatility);
    var x = normdist(D1);
    x /= spot * volatility * Math.pow(time, 0.5);
    return x;
}

function otheta(spot, strike, interest, time, volatility) {
    var D1 = d1(spot, strike, interest, time, volatility);
    var D2 = d2(spot, strike, interest, time, volatility);
    var x = -spot * normdist(D1) * volatility;
    x /= 2 * Math.pow(time, 0.5);
    x -= interest * strike * Math.exp(-interest * time) * normdistacum(D2);
    return x;
}

function putzotheta(spot, strike, interest, time, volatility) {
    var D1 = d1(spot, strike, interest, time, volatility);
    var D2 = d2(spot, strike, interest, time, volatility);
    var x = -spot * normdist(D1) * volatility;
    x /= 2 * Math.pow(time, 0.5);
    x += interest * strike * Math.exp(-interest * time) * normdistacum(-D2);
    return x;
}

function ovega(spot, strike, interest, time, volatility) {
    var D1 = d1(spot, strike, interest, time, volatility);
    var x = spot * Math.sqrt(time) * normdist(D1);
    return x;
}

function putzovega(spot, strike, interest, time, volatility) {
    var D1 = d1(spot, strike, interest, time, volatility);
    var x = spot * Math.sqrt(time) * normdist(D1);
    return x;
}

function orho(spot, strike, interest, time, volatility) {
    var D2 = d2(spot, strike, interest, time, volatility);
    var x = strike * time * Math.exp(-interest * time) * normdistacum(D2);
    return x;
}

function putzorho(spot, strike, interest, time, volatility) {
    var D2 = d2(spot, strike, interest, time, volatility);
    var x = -strike * time * Math.exp(-interest * time) * normdistacum(-D2);
    return x;
}


function normal_strike(spot, strike, interest, time, volatility) {
    // Returns a normalized strike price, with average=0 and standard dev=1
    if (volatility < 0.0000001) {
        return 9999999999.9;
    }
    var x = Math.log(strike / spot) - (interest - Math.pow(volatility, 2) / 2) * time;
    x /= volatility * Math.pow(time, 0.5);
    return x;
}

function stock_price_probability(strike1, strike2, spot, interest, volatility, time) {
    time = Math.max(time, 0.0001);
    volatility = Math.max(volatility, 0.001);
    strike1 = Math.max(strike1, 0.01);
    strike2 = Math.max(strike2, 0.01);
    var prob1 = normdistacum(normal_strike(spot, strike1, interest, time, volatility));
    var prob2 = normdistacum(normal_strike(spot, strike2, interest, time, volatility));
    return prob2 - prob1;
}

function stock_price_probability_max(spread, spot, interest, volatility, time) {
    // Determines the maximum probability that stock_price_probability() will return,
    // given a spread between strike prices (strike1 and strike2)

    // strike = average of expected future price
    var strike = spot * Math.exp((interest - Math.pow(volatility, 2) / 2) * time);
    var p = stock_price_probability(strike - spread, strike + spread, spot, interest, volatility, time);
    return p;
}


function blackscholes(params, dbook) {
    var d = [];

    d['spot'] = params['spot'];
    d['strike'] = params['strike'];
    d['putstrike'] = params['strike'];
    d['strikeh'] = 0;

    d['premium'] = Math.round(Math.max(params['spot'] - params['strike'], 0), 2);
    d['putpremium'] = Math.round(Math.max(params['strike'] - params['spot'], 0), 2);
    d['delta'] = d['gamma'] = d['vega'] = d['theta'] = d['rho'] = 0;
    d['putdelta'] = d['putgamma'] = d['putvega'] = d['puttheta'] = d['putrho'] = 0;
    d['spread_profit'] = 0;
    d['spread_maxprofit'] = 0;
    d['spread_maxloss'] = 0;
    d['spread_profit_p'] = 0;
    d['spread_loss_p'] = 0;
    d['covered_profit'] = 0;
    d['covered_maxprofit'] = 0;
    d['covered_assignment'] = 0;
    d['spread'] = d['premium_ant'] = 0;
    d['ppdfrom'] = 0;
    d['ppdto'] = 0;
    d['ppdprob'] = 0;
    d['ppdaccum'] = 0;

    if (params['time'] > 0.0000001 && params['volatility'] > 0.01) {
        d['premium'] = Math.round(opremium(params['spot'], params['strike'], params['interest'] / 100.0, params['time'], params['volatility'] / 100.0) * 100) / 100;
        d['delta'] = Math.round(odelta(params['spot'], params['strike'], params['interest'] / 100.0, params['time'], params['volatility'] / 100.0) * 1000) / 1000;
        d['gamma'] = Math.round(ogamma(params['spot'], params['strike'], params['interest'] / 100.0, params['time'], params['volatility'] / 100.0) * 1000) / 1000;
        d['vega'] = Math.round(ovega(params['spot'], params['strike'], params['interest'] / 100.0, params['time'], params['volatility'] / 100.0) * 100) / 100;
        d['rho'] = Math.round(orho(params['spot'], params['strike'], params['interest'] / 100.0, params['time'], params['volatility'] / 100.0) * 100) / 100;
        d['theta'] = Math.round(otheta(params['spot'], params['strike'], params['interest'] / 100.0, params['time'], params['volatility'] / 100.0) / 365.0 * 100) / 100;

        d['putpremium'] = Math.round(putzopremium(params['spot'], params['strike'], params['interest'] / 100.0, params['time'], params['volatility'] / 100.0) * 100) / 100;
        d['putdelta'] = Math.round(putzodelta(params['spot'], params['strike'], params['interest'] / 100.0, params['time'], params['volatility'] / 100.0) * 1000) / 1000;
        d['putgamma'] = Math.round(putzogamma(params['spot'], params['strike'], params['interest'] / 100.0, params['time'], params['volatility'] / 100.0) * 1000) / 1000;
        d['putvega'] = Math.round(putzovega(params['spot'], params['strike'], params['interest'] / 100.0, params['time'], params['volatility'] / 100.0) * 100) / 100;
        d['putrho'] = Math.round(putzorho(params['spot'], params['strike'], params['interest'] / 100.0, params['time'], params['volatility'] / 100.0) * 100) / 100;
        d['puttheta'] = Math.round(putzotheta(params['spot'], params['strike'], params['interest'] / 100.0, params['time'], params['volatility'] / 100.0) / 365.0 * 100) / 100;
    }

    d['intrinsic'] = Math.round(Math.max(params['spot'] - params['strike'], 0) * 100) / 100;
    d['putintrinsic'] = Math.round(Math.max(-(params['spot'] - params['strike']), 0) * 100) / 100;
    d['extrinsic'] = Math.round((d['premium'] - d['intrinsic']) * 100) / 100;
    d['putextrinsic'] = Math.round((-(d['premium'] - d['intrinsic'])) * 100) / 100;
    d['sell_index'] = Math.round((d['delta'] + d['gamma'] - d['theta']) * 100) / 100;

    d['good_to_sell'] = d['sell_index'] < d['extrinsic'];

    // in covered calls, it does not make sense to consider time < 1 month

    var time_frame = Math.max(params['time'], 1.0 / 12);
    var time_unit_covered = 1;

    if (params['covered_timeref'] === 0) {
        time_unit_covered = 1.0 / 12;
    }

    var cost;

    // COVERED SELL
    if (params['covered_baseprice'] === 0) {
        // Cost is the current spot price (a.k.a. we don't give a damn to original purchase cost)
        cost = d['spot'];
    } else {
        // Cost is the number given at covered calls tab, not the spot.
        cost = params['covered_cost'];
    }

    if (d['spot'] > 0 && d['premium'] > 0 && cost > 0) {
        var assignment_profit;

        if (d['spot'] > d['strike']) {
            // at spot, we may be assigned...
            assignment_profit = params['strike'] - cost;
        } else {
            // ... or not
            assignment_profit = 0;
        }

        // Profit at spot: premium plus profit of (strike - cost), if assigned
        d['covered_profit'] = Math.pow(1 + (d['premium'] + assignment_profit) / cost, time_unit_covered / time_frame);
        d['covered_profit'] = d['covered_profit'] - 1;

        // assuming that stock price goes to infinity and we are assigned anyway
        assignment_profit = params['strike'] - cost;

        d['covered_maxprofit'] = Math.pow(1 + (d['premium'] + assignment_profit) / cost, time_unit_covered / time_frame);

        d['covered_maxprofit'] = d['covered_maxprofit'] - 1;

        // Probability is calculated based on spot, always!
        d['covered_assignment'] = 1 - stock_price_probability(0, d['strike'], d['spot'], params['interest'] / 100.0, params['volatility'] / 100.0, params['time']);
    }

    var dprevious = null;

    if (dbook.length > 0) {
        d['premium_ant'] = dbook[dbook.length - 1]['premium'];
        d['spread'] = d['premium_ant'] - d["premium"];

        if (dbook.length >= (params['spread_pitch'] + 1)) {
            dprevious = dbook[dbook.length - 1 - params['spread_pitch']];
        }
    }

    // Final price probability

    if (dbook.length > 0) {
        d['ppdfrom'] = dbook[dbook.length - 1]['strike'];
    } else {
        d['ppdfrom'] = 0.00001;
    }
    d['ppdto'] = d['strike'];
    d['ppdprob'] = stock_price_probability(d['ppdfrom'], d['ppdto'], params['spot'], params['interest'] / 100, params['volatility'] / 100, params['time']);
    d['ppdaccum'] = stock_price_probability(0, d['ppdto'], params['spot'], params['interest'] / 100, params['volatility'] / 100, params['time']);

    if (dprevious) {
        d['spread_exists'] = 1;
    } else {
        d['spread_exists'] = 0;
    }

    if (dprevious && (d['spot'] > 0) && (d['premium'] > 0)) {
        // OPTION CREDIT SPREAD

        // Maximum profit is the premium received by K1 minus premium paid for K2 (strike of K2 > K1)
        // (i.e. the liquid premium received)

        d['strikeh'] = dprevious['strike'];
        d['spread_maxprofit'] = dprevious['premium'] - d['premium'];

        // Maximum loss at expiration: the spread, reduced by the liquid premium earned 
        d['spread_maxloss'] = d['strike'] - dprevious['strike'] - d['spread_maxprofit'];

        // Profit at current price: Liquid premium received, minus instrinsic value of K1 (that we sold),
        // plus intrinsic value of K2 (that we bought)
        d['spread_profit'] = d['spread_maxprofit'] - dprevious['intrinsic'] + d['intrinsic'];

        // Profit has a breakeven when stock price is equal to K1 plus the liquid premium we earned
        var spread_breakeven = dprevious['strike'] + d['spread_maxprofit'];

        d['spread_profit_p'] = Math.round(100 * stock_price_probability(0, spread_breakeven, d['spot'], params['interest'] / 100.0, params['volatility'] / 100.0, params['time'])) / 100;
        d['spread_loss_p'] = 1 - d['spread_profit_p'];

        cost = d['strike'] - dprevious['strike'];
        // margin = spread
        d['spread_rom'] = Math.pow(1 + d['spread_profit'] / cost, time_unit_covered / time_frame) - 1;
        d['spread_rommax'] = Math.pow(1 + d['spread_maxprofit'] / cost, time_unit_covered / time_frame) - 1;

        if (params['spread_type'] == 1) {
            // OPTION DEBIT SPREAD
            var tmp = d['spread_maxprofit'];
            d['spread_maxprofit'] = d['spread_maxloss'];
            d['spread_maxloss'] = tmp;
            d['spread_profit'] *= -1;
            tmp = d['spread_profit_p'];
            d['spread_profit_p'] = d['spread_loss_p'];
            d['spread_loss_p'] = tmp;

            cost = d['spread_maxloss'];
            // investment = the expenditure mounting spread = maximum loss
            d['spread_rom'] = Math.pow(1 + d['spread_profit'] / cost, time_unit_covered / time_frame) - 1;
            d['spread_rommax'] = Math.pow(1 + d['spread_maxprofit'] / cost, time_unit_covered / time_frame) - 1;
        }
    }

    return d;
}


function save_memory(params) {
    var expires = new Date();
    expires.setTime(expires.getTime() + 30 * 24 * 60 * 60 * 1000);
    // timezone irrelevant
    var sm = "bscalc=";
    for (var nam in params) {
        if (typeof params[nam] !== "function") {
            sm += nam + ":" + params[nam] + " ";
        }
    }
    sm += "; expires=" + expires.toGMTString() + "; path=/";
    document.cookie = sm;
}


function recover_memory() {
    var recovered = 0;
    var ck = document.cookie.split(';');

    for (var f = 0; f < ck.length; ++f) {
        var cv = ck[f].split('=');
        if (cv.length != 2) {
            continue;
        }
        cv[0] = trim(cv[0]);
        cv[1] = trim(cv[1]);
        if (cv[0] != 'bscalc') {
            continue;
        }
        var sm = cv[1].split(' ');
        for (var e = 0; e < sm.length; ++e) {
            var smpair = sm[e].split(':');
            if (smpair.length == 2) {
                if (document.f[smpair[0]] !== undefined) {
                    if (smpair[0] != 'expiration') {
                        document.f[smpair[0]].value = smpair[1];
                    } else {
                        document.f[smpair[0]].value = dtoc(parseFloat(smpair[1]));
                    }
                    recovered = 1;
                }
            }
        }
    }
    return recovered;
}

var book_items = ['strike', 'premium', 'intrinsic', 'delta', 'gamma', 'theta', 'vega', 'rho', 'putstrike', 'putpremium', 'putintrinsic', 'putdelta', 'putgamma', 'puttheta', 'putvega', 'putrho'];
var book_decimals = [2, 2, 2, 1, 1, 2, 2, 2, 2, 2, 2, 1, 1, 2, 2, 2];
var book_is100 = [0, 0, 0, 1, 1, 0, 0, 0, 0, 0, 0, 1, 1, 0, 0, 0];

var distrib_items = ['ppdfrom', 'ppdto', 'ppdprob', 'ppdaccum'];
var distrib_decimals = [2, 2, 1, 1];
var distrib_is100 = [0, 0, 1, 1];

var covered_items = ['strike', 'premium', 'covered_profit', 'covered_maxprofit', 'covered_assignment'];
var covered_decimals = [2, 2, 1, 1, 0];
var covered_is100 = [0, 0, 1, 1, 1];

var spread_items = ['strikeh', 'strike', 'spread_profit', 'spread_maxprofit', 'spread_maxloss', 'spread_profit_p', 'spread_rom', 'spread_rommax'];
var spread_decimals = [2, 2, 2, 2, 2, 0, 1, 1];
var spread_is100 = [0, 0, 0, 0, 0, 1, 1, 1];

function format_number(value, decimals, is_percentage) {
    var dp = decimals + (is_percentage ? 2 : 0);
    var s;
    if (Math.round(value * Math.pow(10, dp)) === 0) {
        s = "--";
    } else if (is_percentage) {
        s = (100 * value).toFixed(decimals) + "%";
    } else {
        s = value.toFixed(decimals);
    }
    return s;
}

function update_row(d, i) {
    var j, id, txt, o;
    for (j = 0; j < book_items.length; ++j) {
        id = book_items[j] + (i + 1);
        txt = format_number(d[book_items[j]], book_decimals[j], book_is100[j]);
        o = object_by_id(id);
        if (o) {
            if (o.innerHTML) {
                o.innerHTML = txt;
            } else {
                o.text = txt;
            }
        }
    }

    for (j = 0; j < distrib_items.length; ++j) {
        id = distrib_items[j] + (i + 1);
        txt = format_number(d[distrib_items[j]], distrib_decimals[j], distrib_is100[j]);
        o = object_by_id(id);
        if (o) {
            if (o.innerHTML) {
                o.innerHTML = txt;
            } else {
                o.text = txt;
            }
        }
    }

    for (j = 0; j < covered_items.length; ++j) {
        id = covered_items[j] + (covered_items[j] == 'strike' || covered_items[j] == 'premium' ? 'C' : '') + (i + 1);
        txt = format_number(d[covered_items[j]], covered_decimals[j], covered_is100[j]);
        o = object_by_id(id);
        if (o) {
            if (o.innerHTML) {
                o.innerHTML = txt;
            } else {
                o.text = txt;
            }
        }
    }

    for (j = 0; j < spread_items.length; ++j) {
        id = spread_items[j] + (spread_items[j] == 'strike' ? 'S' : '') + (i + 1);
        if (d['spread_exists']) {
            txt = format_number(d[spread_items[j]], spread_decimals[j], spread_is100[j]);
        } else {
            txt = ' &nbsp; ';
        }
        o = object_by_id(id);
        if (o) {
            if (o.innerHTML) {
                o.innerHTML = txt;
            } else {
                o.text = txt;
            }
        }
    }
}


function update_table(book) {
    for (var i = 0; i < book.length; ++i) {
        update_row(book[i], i);
    }
}



function bscalc(ptbr) {
    var x;
    var params = [];

    document.getElementById("recalc").style.visibility = "hidden";

    x = params['spot'] = parseFloat(handledecimal(document.f.spot.value));
    if (isNaN(x)) {
        document.f.spot.value = 'Error';
        return false;
    }
    params['strike1'] = parseFloat(handledecimal(document.f.strike1.value));
    x = params['strike'] = parseFloat(handledecimal(document.f.strike1.value));
    if (isNaN(x)) {
        document.f.strike1.value = 'Error';
        return false;
    }
    x = params['spread'] = parseFloat(handledecimal(document.f.spread.value));
    if (isNaN(x)) {
        document.f.spread.value = 'Error';
        return false;
    }
    x = params['interest'] = parseFloat(handledecimal(document.f.interest.value));
    if (isNaN(x)) {
        document.f.interest.value = 'Error';
        return false;
    }
    x = params['volatility'] = parseFloat(handledecimal(document.f.volatility.value));
    if (isNaN(x)) {
        document.f.volatility.value = 'Error';
        return false;
    }
    x = params['covered_cost'] = parseFloat(handledecimal(document.f.covered_cost.value));
    if (isNaN(x)) {
        document.f.covered_cost.value = 'Error';
        return false;
    }
    params['covered_baseprice'] = parseInt(document.f.covered_baseprice.value, 10);

    params['covered_timeref'] = 1;

    params['spread_type'] = parseInt(document.f.spread_type.value, 10);
    params['spread_pitch'] = parseInt(document.f.spread_pitch.value, 10);

    var today = new Date();
    // at noon, to prevent a daylight saving timezone to change the date.
    today = new Date(today.getFullYear(), today.getMonth(), today.getDate(), 12, 0, 0);

    params['days_year'] = parseInt(document.f.days_year.value);
    if ((!params['days_year']) || (params['days_year'] !== 252 && params['days_year'] !== 365)) {
        alert("Error");
        return false;
    }

    var expiration = ctod(document.f.expiration.value);
    if (!expiration) {
        document.f.expiration.value = 'Error';
        return false;
    }


    // no problem with timezone since just save and restore the jiffies
    // as an opaque number, and y.setTime(x.getTime()) == x
    params['expiration'] = expiration.getTime();

    // expiration and today may be at different timezones because of
    // daylight savings, so we need to compensate for.
    // Rounding by number of days should be enough, but we prefer to be twice as safe

    params['time'] = Math.round(((expiration.getTime() - tzoffset(expiration)) -
				 (today.getTime() - tzoffset(today))) / 1000 / 60 / 60 / 24);

    var bus_days = count_business_days(today.getTime() - tzoffset(today), params['time']);
    var bus_el = document.getElementById("business_days");
    var bus_txt = "" + bus_days + " " + (ptbr ? "dias úteis" : "business days");
    if (bus_el.innerHTML) {
        bus_el.innerHTML = bus_txt;
    } else {
        bus_el.text = bus_txt;
    }

    if (params['days_year'] === 252) {
        params['time'] = bus_days;
    }
    params['time'] /= params['days_year'];

    if (params['time'] < 0) {
        params['time'] = 0;
    }


    var book = [];
    var d;
    for (var i = 0; i < 7; ++i) {
        d = blackscholes(params, book);
        book[i] = d;
        params['strike'] += params['spread'];
    }
    update_table(book);
    // update_graph(params, book);
    save_memory(params);

    return false;
}


function Spot_plus(ptbr) {
    var spot = parseFloat(handledecimal(document.f.spot.value));
    if (isNaN(spot)) {
        document.f.spot.value = 'Error';
        return false;
    }
    spot += 0.01;
    document.f.spot.value = spot.toFixed(2);
    return bscalc(ptbr);
}

function Spot_minus(ptbr) {
    var spot = parseFloat(handledecimal(document.f.spot.value));
    if (isNaN(spot)) {
        document.f.spot.value = 'Error';
        return false;
    }
    if (spot > 0.01) {
        spot -= 0.01;
    }
    document.f.spot.value = spot.toFixed(2);
    return bscalc(ptbr);
}

function Volatility_plus(ptbr) {
    var volatility = parseFloat(handledecimal(document.f.volatility.value));
    if (isNaN(volatility)) {
        document.f.volatility.value = 'Error';
        return false;
    }
    volatility += 0.25;
    document.f.volatility.value = volatility.toFixed(2);
    return bscalc(ptbr);
}

function Volatility_minus(ptbr) {
    var volatility = parseFloat(handledecimal(document.f.volatility.value));
    if (isNaN(volatility)) {
        document.f.volatility.value = 'Error';
        return false;
    }
    if (volatility >= 0.01) {
        volatility -= 0.25;
    }
    document.f.volatility.value = volatility.toFixed(2);
    return bscalc(ptbr);
}



/* 
// var lines = ['premium', 'intrinsic', 'delta', 'gamma', 'theta'];
var lines = [];
var colors = {'premium': '#0a0', 'intrinsic': '#f00', 'delta': '#00f', 'gamma': '#0cc', 'theta': '#f0f'};
var T = 4;
var PROBABILITY_BAR = 50;
var PROBABILITY_BAR2 = PROBABILITY_BAR / 2;
function update_graph(params, book)
{
    var g = object_by_id('graphz');
    if (! g) {
        return;
    }
    if (! g.getContext) {
        return;
    }
    if (g.height != 250 || g.width != 600) {
        g.height = 250;
        g.width = 600;
    }

    var gc = g.getContext('2d');
    gc.fillStyle = "#fff";
    gc.fillRect(0, 0, g.width, g.height);

    var maxi = [];

    for(var i = 0; i < lines.length; ++i) {
        var item = lines[i];
        maxi[item] = 0;
        for(var j = 0; j < book.length; ++j) {
            if (maxi[item] < Math.abs(book[j][item])) {
                maxi[item] = Math.abs(book[j][item]);
            }
        }
        if (maxi[item] === 0) {
            maxi[item] = 1;
        }
    }

    gc.strokeStyle = "#ccc";
    gc.lineWidth = PROBABILITY_BAR - 2;
    gc.lineCap = 'butt';

    gc.beginPath();

    // probability bars
    if (params['spot'] > 0) {
        var x = PROBABILITY_BAR2;
        var spread = PROBABILITY_BAR2 * (book[book.length-1]['strike'] - book[0]['strike']) / g.width;
        var ymax = stock_price_probability_max(spread, params['spot'], params['interest']/100, params['volatility']/100, params['time']);
        if (ymax > 0) {
            var o = object_by_id("distribution");
            o.innerHTML = "Full height bar = " + (ymax*100).toFixed(1) + "%";

            while (x < (g.width + PROBABILITY_BAR2)) {
                var strike_x = book[0]['strike'] + x * (book[book.length-1]['strike'] - book[0]['strike']) / g.width;
                var y = (g.height - 1) * stock_price_probability(strike_x - spread, strike_x + spread, params['spot'], params['interest']/100, params['volatility']/100, params['time']) / (ymax);
                gc.moveTo(x, g.height - 1);
                gc.lineTo(x, g.height - y + 1);
                x += PROBABILITY_BAR;
            }
        }
    }

    gc.stroke();

    gc.lineWidth = 1;
    gc.strokeStyle = "#000";
    gc.strokeRect(0, 0, g.width, g.height);

    gc.lineCap = 'round';

    gc.beginPath();

    var div = 5;
    for(var i = 1; i < div; ++i) {
        var y = g.height * i / div;
        gc.moveTo(0, y);
        gc.lineTo(g.width, y);
    }

    div = book.length - 1;
    book[0]['x'] = T/2+1;
    book[div]['x'] = g.width - T/2 - 1;
    for(var i = 1; i < div; ++i) {
        var x = g.width * i / div;
        book[i]['x'] = x;
        gc.moveTo(x, 0);
        gc.lineTo(x, g.height-1);
    }

    // all monetary values in the same scale
    maxi['intrinsic'] = maxi['theta'] = maxi['premium'];
    maxi['delta'] = 1;
    maxi['gamma'] = maxi['delta'];

    gc.stroke();

    gc.lineWidth = T;
    gc.lineJoin = 'round';

    for(var i = 0; i < lines.length; ++i) {
        var item = lines[i];
        var multiplier = (g.height - T - 2)/maxi[item];
        gc.beginPath();
        gc.strokeStyle = colors[item];
        gc.moveTo(book[0]['x'], (g.height - T + 1) - Math.abs(book[0][item])*multiplier);
        for(var j = 0; j < book.length; ++j) {
            gc.lineTo(book[j]['x'], (g.height - T + 1) - Math.abs(book[j][item])*multiplier);
        }
        gc.stroke();
    }
}
*/

function close_calc() {
    if (window.opener) {
        if (window.opener.calc_closed) {
            window.opener.calc_closed();
        }
    }
}

/* Used by volatimp page */
function set_defaults(strike1, spread, spot, expiration, volatility, interest, covered_cost, recalc) {
    return set_defaults_in(1, strike1, spread, spot, expiration, volatility, interest,
				covered_cost, recalc);
}

function set_defaults_in(ptbr, strike1, spread, spot, expiration, volatility, interest, covered_cost,
			recalc) {
    document.f.strike1.value = strike1.toFixed(2);
    document.f.spread.value = spread.toFixed(2);
    document.f.spot.value = spot.toFixed(2);
    document.f.expiration.value = dtoc(expiration.getTime());
    document.f.volatility.value = volatility.toFixed(2);
    document.f.interest.value = interest.toFixed(2);
    document.f.covered_cost.value = covered_cost.toFixed(2);
    if (recalc) {
        bscalc(ptbr);
    }
}

function Init_calc(ptbr) {
    var future = new Date();
    future.setTime(future.getTime() + 30 * 24 * 60 * 60 * 1000);
    set_defaults_in(ptbr, 26.0, 2.0, 30.0, future, 25.0, 8.75, 30.0, false);
    recover_memory();
    bscalc(ptbr);
    window.onunload = close_calc;
    window.beforeunload = close_calc;

    if (window.opener) {
        if (window.opener.calc_opened) {
            window.opener.calc_opened();
        }
    }

    var today_str = ptbr ? "Hoje = " : "Today = ";
    var now = new Date();
    today_str += dtoc(now.getTime());

    var today_span = document.getElementById("today");
    if (today_span.innerHTML) {
        today_span.innerHTML = today_str;
    } else {
        today_span.text = today_str;
    }
}

function make_days(ptbr) {
    var days_year = parseInt(document.f.days_year.value);
    var business = (days_year === 252);
    var msg = ptbr ? "Digite o número de dias" : "Type the number of";
    if (business) {
        msg += ptbr ? " úteis" : " business days";
    } else {
        msg += ptbr ? " corridos" : " days";
    }
    msg += ":";
    var res = prompt(msg);
    var dys = parseInt(res);
    if (isNaN(dys) || (dys < 0) || (dys > 9999)) {
        alert(ptbr ? "Número inválido de dias" : "Invalid number of days");
        return;
    }

    var future = new Date();
    if (business) {
        future = advance_business_days(future, dys);
    } else {
        future.setTime(future.getTime() + dys * 24 * 60 * 60 * 1000);
    }
    document.f.expiration.value = dtoc(future.getTime());

    bscalc(ptbr);
}

// ported from http://www.assa.org.au/edm.html#Computer

function calc_good_friday(y) {
    var m, d;
    var FirstDig, Remain19, temp;
    var tA, tB, tC, tD, tE;

    FirstDig = Math.floor(y / 100);
    Remain19 = y % 19;

    // calculate Pascal Full Moon date
    temp = Math.floor((FirstDig - 15) / 2) + 202 - 11 * Remain19;

    if ([21, 24, 25, 27, 28, 29, 30, 31, 32, 34, 35, 38].indexOf(FirstDig) > -1) {
        temp = temp - 1;
    } else if ([33, 36, 37, 39, 40].indexOf(FirstDig) > -1) {
        temp = temp - 2;
    }

    temp = temp % 30;

    tA = temp + 21;

    if (temp == 29) {
        tA = tA - 1;
    }
    if (temp == 28 && Remain19 > 10) {
        tA = tA - 1;
    }

    // find the next Sunday
    tB = (tA - 19) % 7;

    tC = (40 - FirstDig) % 4;

    if (tC == 3) {
        tC = tC + 1;
    }
    if (tC > 1) {
        tC = tC + 1;
    }

    temp = y % 100;
    tD = (temp + Math.floor(temp / 4)) % 7;

    tE = ((20 - tB - tC - tD) % 7) + 1;
    d = tA + tE;

    // we want good friday, not easter (sunday)
    d -= 2;

    if (d > 31) {
        d -= 31;
        m = 4;
    } else {
        m = 3;
    }

    return [m, d];
}

function good_friday(y) {
    var gf = gf_cache[y];
    if (!gf) {
        gf = gf_cache[y] = calc_good_friday(y);
        // alert("" + y + " " + gf[0] + "/" + gf[1]);
    }
    return gf;
}

gf_cache = [];

function good_friday_month(y) {
    return good_friday(y)[0];
}

function good_friday_day(y) {
    return good_friday(y)[1];
}

function is_holiday(date) {
    var day = date.getDate();
    var month = date.getMonth() + 1;
    var year = date.getFullYear();

    // TODO: local holidays ? Moving holidays like Good Friday?

    if (month === 12 && day === 25) {
        // christmas
        return true;
    } else if (month === 12 && day === 24) {
        // christmas eve
        return true;
    } else if (month === 12 && day === 31) {
        // new year's eve
        return true;
    } else if (month === 1 && day === 1) {
        // new year
        return true;
    } else if (month === good_friday_month(year) && day === good_friday_day(year)) {
        // good friday
        return true;
    }

    return false;
}

function is_business_day(date) {
    var dow = date.getDay();
    return (dow >= 1 && dow <= 5 && !is_holiday(date));
}

function advance_business_days(dt, days) {
    for (var i = 0; i < days; ++i) {
        var business_day = false;
        while (!business_day) {
            dt.setTime(dt.getTime() + 24 * 60 * 60 * 1000);
            business_day = is_business_day(dt);
        }
    }

    return dt;
}

function count_business_days(dt, plain_days) {
    dt = new Date(dt);
    var days = 0;

    for (var i = 0; i < plain_days; ++i) {
        dt.setTime(dt.getTime() + 24 * 60 * 60 * 1000);

        if (is_business_day(dt)) {
            ++days;
        }
    }

    return days;
}

function changed() {
    document.getElementById("recalc").style.visibility = "visible";
}
