/*
 * OmniBox — фоновый сервис webOS (Node.js).
 * Веб-часть приложения не умеет отправлять UDP, поэтому Wake-on-LAN,
 * поиск устройств и проверка связи живут здесь.
 *
 * Методы (luna://com.omnibox.launcher.service/<метод>):
 *   wake       { mac, ip? }  — magic packet на все broadcast-адреса, 3 повтора, порты 9 и 7
 *   discover   {}            — опрос своей подсети /24, список устройств из ARP-таблицы
 *   resolveMac { ip }        — MAC и имя устройства по IP (устройство должно быть в сети)
 *   probe      { ip }        — отвечает ли устройство (TCP 22/135/139/445/3389/5357/47989)
 *   info       {}            — сетевые интерфейсы телевизора
 *   agentInfo  { ip, port? }                 — агент WinCLI: имя ПК и версия
 *   agentPair  { ip, port?, code, name? }    — код из `win pair` → токен
 *   agentPower { ip, port?, token, action }  — sleep | hibernate | reboot | shutdown | lock | cancel
 *
 * Код намеренно в стиле ES5: на webOS 4.x стоит старый Node.js.
 */
'use strict';

var Service = require('webos-service');
var dgram = require('dgram');
var net = require('net');
var os = require('os');
var fs = require('fs');
var dns = require('dns');
var exec = require('child_process').exec;

var SERVICE_ID = 'com.omnibox.launcher.service';
var service = new Service(SERVICE_ID);

/* ── Адреса ─────────────────────────────────────────── */

function isIp(s) {
  if (typeof s !== 'string') return false;
  var p = s.trim().split('.');
  if (p.length !== 4) return false;
  for (var i = 0; i < 4; i++) {
    if (!/^\d{1,3}$/.test(p[i]) || +p[i] > 255) return false;
  }
  return true;
}

function ipToInt(ip) {
  var p = ip.split('.');
  return ((+p[0]) * 16777216 + (+p[1]) * 65536 + (+p[2]) * 256 + (+p[3])) >>> 0;
}

function intToIp(n) {
  return [(n >>> 24) & 255, (n >>> 16) & 255, (n >>> 8) & 255, n & 255].join('.');
}

function isPrivate(ip) {
  return /^10\./.test(ip) || /^192\.168\./.test(ip) || /^172\.(1[6-9]|2\d|3[01])\./.test(ip);
}

/* IPv4-интерфейсы телевизора (Ethernet и Wi-Fi). */
function localNets() {
  var res = [];
  var ifs = os.networkInterfaces();
  Object.keys(ifs).forEach(function (name) {
    (ifs[name] || []).forEach(function (a) {
      var v4 = a.family === 'IPv4' || a.family === 4;
      if (!v4 || a.internal || !a.netmask) return;
      var ip = ipToInt(a.address);
      var mask = ipToInt(a.netmask);
      res.push({
        iface: name,
        address: a.address,
        netmask: a.netmask,
        broadcast: intToIp(((ip & mask) | (~mask >>> 0)) >>> 0)
      });
    });
  });
  res.sort(function (x, y) { return (isPrivate(y.address) ? 1 : 0) - (isPrivate(x.address) ? 1 : 0); });
  return res;
}

function normalizeMac(s) {
  if (typeof s !== 'string') return null;
  var hex = s.replace(/[^0-9a-fA-F]/g, '');
  if (hex.length !== 12) return null;
  return hex.toUpperCase().match(/.{2}/g).join(':');
}

function uniq(list) {
  var seen = {};
  return list.filter(function (x) { if (seen[x]) return false; seen[x] = true; return true; });
}

/* ── ARP-таблица ────────────────────────────────────── */

function parseProcArp(text) {
  var out = [];
  text.split('\n').slice(1).forEach(function (line) {
    var c = line.trim().split(/\s+/);
    if (c.length < 6) return;
    var flags = parseInt(c[2], 16);
    if (!(flags & 2)) return; // 0x2 — запись завершена
    out.push({ ip: c[0], mac: c[3], iface: c[5] });
  });
  return out;
}

function parseIpNeigh(text) {
  var out = [];
  text.split('\n').forEach(function (line) {
    var m = line.match(/^(\d+\.\d+\.\d+\.\d+)\s+dev\s+(\S+)\s+lladdr\s+([0-9a-fA-F:]{17})\s+(\S+)/);
    if (m && m[4] !== 'FAILED' && m[4] !== 'INCOMPLETE') out.push({ ip: m[1], mac: m[3], iface: m[2] });
  });
  return out;
}

function parseArpAn(text) {
  var out = [];
  text.split('\n').forEach(function (line) {
    var m = line.match(/\((\d+\.\d+\.\d+\.\d+)\)\s+at\s+([0-9a-fA-F:]{17})(?:.*\son\s(\S+))?/);
    if (m) out.push({ ip: m[1], mac: m[2], iface: m[3] || '' });
  });
  return out;
}

function cleanArp(list) {
  return list.filter(function (e) {
    var mac = normalizeMac(e.mac);
    if (!mac || mac === '00:00:00:00:00:00' || mac === 'FF:FF:FF:FF:FF:FF') return false;
    e.mac = mac;
    return true;
  });
}

function readArp(cb) {
  fs.readFile('/proc/net/arp', 'utf8', function (err, text) {
    var list = err ? [] : parseProcArp(text);
    if (list.length) return cb(cleanArp(list));
    exec('ip neigh show', { timeout: 3000 }, function (e2, out2) {
      var l2 = e2 ? [] : parseIpNeigh(String(out2));
      if (l2.length) return cb(cleanArp(l2));
      exec('arp -an', { timeout: 3000 }, function (e3, out3) {
        cb(cleanArp(e3 ? [] : parseArpAn(String(out3))));
      });
    });
  });
}

/* ── NetBIOS: имя компьютера Windows (UDP 137) ──────── */

var NBSTAT_QUERY = (function () {
  var b = Buffer.alloc(50);
  b.writeUInt16BE(0x4f42, 0);  // id
  b.writeUInt16BE(0x0000, 2);  // flags
  b.writeUInt16BE(1, 4);       // qdcount
  b[12] = 0x20;                // длина закодированного имени
  var name = '*';
  for (var i = 0; i < 16; i++) {
    var ch = i < name.length ? name.charCodeAt(i) : 0;
    b[13 + i * 2] = 0x41 + (ch >> 4);
    b[14 + i * 2] = 0x41 + (ch & 0x0f);
  }
  b[45] = 0x00;
  b.writeUInt16BE(0x0021, 46); // NBSTAT
  b.writeUInt16BE(0x0001, 48); // IN
  return b;
})();

function parseNbstat(buf) {
  try {
    var off = 56;
    var count = buf[off];
    off += 1;
    for (var i = 0; i < count && off + 18 <= buf.length; i++, off += 18) {
      var name = buf.toString('ascii', off, off + 15).replace(/\s+$/, '');
      var suffix = buf[off + 15];
      var flags = buf.readUInt16BE(off + 16);
      if (suffix === 0x00 && !(flags & 0x8000) && name) return name;
    }
  } catch (e) { /* ответ не разобрался — имени не будет */ }
  return null;
}

/*
 * Отправляет на каждый адрес запрос NBSTAT (137) и пустой пакет (9).
 * Это заставляет ядро разрешить ARP, а Windows-машины заодно сообщают своё имя.
 */
function poke(ips, waitMs, cb) {
  var names = {};
  var sock = dgram.createSocket('udp4');
  var done = false;
  var nudge = Buffer.from ? Buffer.from([0]) : new Buffer([0]);
  sock.on('error', function () { /* недоступные адреса просто пропускаем */ });
  sock.on('message', function (msg, rinfo) {
    if (rinfo.port === 137) {
      var n = parseNbstat(msg);
      if (n) names[rinfo.address] = n;
    }
  });
  function sendRound() {
    ips.forEach(function (ip) {
      try {
        sock.send(NBSTAT_QUERY, 0, NBSTAT_QUERY.length, 137, ip);
        sock.send(nudge, 0, 1, 9, ip);
      } catch (e) { /* сокет мог закрыться */ }
    });
  }
  sock.bind(function () {
    sendRound();
    setTimeout(function () { if (!done) sendRound(); }, Math.min(1000, waitMs / 2));
    setTimeout(function () {
      done = true;
      try { sock.close(); } catch (e) { /* уже закрыт */ }
      cb(names);
    }, waitMs);
  });
}

function reverseName(ip, cb) {
  var finished = false;
  var timer = setTimeout(function () { if (!finished) { finished = true; cb(null); } }, 1200);
  try {
    dns.reverse(ip, function (err, hosts) {
      if (finished) return;
      finished = true;
      clearTimeout(timer);
      cb(!err && hosts && hosts.length ? hosts[0].replace(/\.$/, '') : null);
    });
  } catch (e) {
    finished = true;
    clearTimeout(timer);
    cb(null);
  }
}

/* ── Проверка связи ─────────────────────────────────── */

var PROBE_PORTS = [22, 135, 139, 445, 3389, 5357, 47989];

function probe(ip, cb) {
  var left = PROBE_PORTS.length;
  var answered = false;
  PROBE_PORTS.forEach(function (port) {
    var s = new net.Socket();
    var settled = false;
    function finish(alive) {
      if (settled) return;
      settled = true;
      s.destroy();
      if (alive && !answered) { answered = true; cb(true, port); }
      if (--left === 0 && !answered) cb(false, null);
    }
    s.setTimeout(900);
    s.once('connect', function () { finish(true); });
    s.once('timeout', function () { finish(false); });
    // ECONNREFUSED — машина ответила RST, значит она включена.
    s.once('error', function (e) { finish(e && e.code === 'ECONNREFUSED'); });
    s.connect(port, ip);
  });
}

/* ── Методы ─────────────────────────────────────────── */

function safe(handler) {
  return function (msg) {
    try {
      handler(msg, msg.payload || {});
    } catch (e) {
      msg.respond({ returnValue: false, errorCode: -1, errorText: String(e && e.message || e) });
    }
  };
}

service.register('info', safe(function (msg) {
  msg.respond({ returnValue: true, nets: localNets() });
}));

service.register('wake', safe(function (msg, p) {
  var mac = normalizeMac(p.mac);
  if (!mac) {
    msg.respond({ returnValue: false, errorCode: 1, errorText: 'Неверный MAC-адрес' });
    return;
  }
  var macBytes = mac.split(':').map(function (h) { return parseInt(h, 16); });
  var pkt = Buffer.alloc(102, 0xff);
  for (var i = 0; i < 16; i++) {
    for (var j = 0; j < 6; j++) pkt[6 + i * 6 + j] = macBytes[j];
  }

  var targets = ['255.255.255.255'];
  localNets().forEach(function (n) { targets.push(n.broadcast); });
  if (isIp(p.ip)) {
    var parts = p.ip.trim().split('.');
    targets.push(parts[0] + '.' + parts[1] + '.' + parts[2] + '.255');
    targets.push(p.ip.trim());
  }
  targets = uniq(targets);

  var ports = [9, 7];
  var jobs = [];
  for (var round = 0; round < 3; round++) {
    targets.forEach(function (t) { ports.forEach(function (port) { jobs.push([t, port, round]); }); });
  }

  var sent = 0;
  var errors = [];
  var pending = jobs.length;
  var sock = dgram.createSocket('udp4');
  sock.on('error', function (e) { errors.push(e.message); });
  sock.bind(function () {
    try { sock.setBroadcast(true); } catch (e) { errors.push('broadcast: ' + e.message); }
    jobs.forEach(function (job) {
      setTimeout(function () {
        sock.send(pkt, 0, pkt.length, job[1], job[0], function (err) {
          if (err) errors.push(job[0] + ':' + job[1] + ' ' + err.message);
          else sent++;
          if (--pending === 0) {
            try { sock.close(); } catch (e) { /* уже закрыт */ }
            msg.respond({
              returnValue: sent > 0,
              mac: mac,
              sent: sent,
              targets: targets,
              errors: uniq(errors).slice(0, 5),
              errorText: sent > 0 ? undefined : 'Пакет не отправлен'
            });
          }
        });
      }, job[2] * 150);
    });
  });
}));

service.register('discover', safe(function (msg) {
  var nets = localNets().filter(function (n) { return isPrivate(n.address); });
  if (!nets.length) {
    msg.respond({ returnValue: false, errorCode: 2, errorText: 'Телевизор не подключён к локальной сети' });
    return;
  }
  var self = nets[0].address;
  var base = self.split('.').slice(0, 3).join('.');
  var ips = [];
  for (var i = 1; i < 255; i++) {
    var ip = base + '.' + i;
    if (ip !== self) ips.push(ip);
  }

  poke(ips, 2600, function (nbNames) {
    readArp(function (arp) {
      var devices = arp.filter(function (e) { return e.ip.indexOf(base + '.') === 0 && e.ip !== self; });
      var seen = {};
      devices = devices.filter(function (d) { if (seen[d.ip]) return false; seen[d.ip] = true; return true; });
      var left = devices.length;
      if (!left) {
        msg.respond({ returnValue: true, self: self, subnet: base + '.0/24', devices: [] });
        return;
      }
      devices.forEach(function (d) {
        if (nbNames[d.ip]) {
          d.name = nbNames[d.ip];
          d.source = 'netbios';
          if (--left === 0) reply();
          return;
        }
        reverseName(d.ip, function (name) {
          if (name) { d.name = name; d.source = 'dns'; }
          if (--left === 0) reply();
        });
      });
      function reply() {
        devices.sort(function (a, b) { return ipToInt(a.ip) - ipToInt(b.ip); });
        msg.respond({ returnValue: true, self: self, subnet: base + '.0/24', devices: devices });
      }
    });
  });
}));

service.register('resolveMac', safe(function (msg, p) {
  if (!isIp(p.ip)) {
    msg.respond({ returnValue: false, errorCode: 3, errorText: 'Неверный IP-адрес' });
    return;
  }
  var ip = p.ip.trim();
  poke([ip], 900, function (nbNames) {
    readArp(function (arp) {
      var hit = arp.filter(function (e) { return e.ip === ip; })[0];
      if (!hit) {
        msg.respond({ returnValue: false, errorCode: 4, errorText: 'Устройство не ответило. Включите его и повторите.' });
        return;
      }
      msg.respond({ returnValue: true, ip: ip, mac: hit.mac, name: nbNames[ip] || null });
    });
  });
}));

/* ── Агент WinCLI: питание по сети ──────────────────── */

var http = require('http');
var AGENT_PORT = 7798;

/* HTTP-запрос к агенту WinCLI (`win --agent`) в локальной сети. */
function agentRequest(p, method, path, body, cb) {
  if (!isIp(p.ip)) { cb({ returnValue: false, errorCode: 3, errorText: 'Неверный IP-адрес' }); return; }
  var port = +p.port || AGENT_PORT;
  var data = body ? JSON.stringify(body) : null;
  var headers = { 'Accept': 'application/json' };
  if (data) {
    headers['Content-Type'] = 'application/json';
    headers['Content-Length'] = Buffer.byteLength(data);
  }
  if (p.token) headers['Authorization'] = 'Bearer ' + p.token;

  var finished = false;
  function done(res) { if (!finished) { finished = true; cb(res); } }

  var req = http.request({ host: p.ip.trim(), port: port, method: method, path: path, headers: headers }, function (res) {
    var chunks = '';
    res.setEncoding('utf8');
    res.on('data', function (c) { if (chunks.length < 65536) chunks += c; });
    res.on('end', function () {
      var json = {};
      try { json = JSON.parse(chunks || '{}'); } catch (e) { json = {}; }
      if (res.statusCode >= 200 && res.statusCode < 300) {
        json.returnValue = true;
        json.status = res.statusCode;
        done(json);
      } else {
        done({ returnValue: false, status: res.statusCode, errorCode: res.statusCode, errorText: json.error || ('HTTP ' + res.statusCode) });
      }
    });
  });
  req.setTimeout(4000, function () { req.abort(); });
  req.on('error', function (e) {
    var code = e && e.code;
    var text = 'OMNIBOX не отвечает';
    if (code === 'ECONNREFUSED') text = 'Агент WinCLI не запущен на ' + p.ip + ':' + port;
    done({ returnValue: false, errorCode: 'NET', netError: code || 'TIMEOUT', errorText: text });
  });
  if (data) req.write(data);
  req.end();
}

service.register('agentInfo', safe(function (msg, p) {
  agentRequest(p, 'GET', '/v1/info', null, function (res) { msg.respond(res); });
}));

service.register('agentPair', safe(function (msg, p) {
  var code = String(p.code || '').replace(/\D/g, '');
  if (code.length !== 6) {
    msg.respond({ returnValue: false, errorCode: 5, errorText: 'Код — шесть цифр' });
    return;
  }
  agentRequest(p, 'POST', '/v1/pair', { code: code, name: p.name || 'Телевизор LG' }, function (res) { msg.respond(res); });
}));

service.register('agentPower', safe(function (msg, p) {
  var allowed = { sleep: 1, hibernate: 1, reboot: 1, shutdown: 1, lock: 1, cancel: 1 };
  if (!allowed[p.action]) {
    msg.respond({ returnValue: false, errorCode: 6, errorText: 'Неизвестное действие' });
    return;
  }
  if (!p.token) {
    msg.respond({ returnValue: false, errorCode: 401, status: 401, errorText: 'Подключите WinCLI в настройках' });
    return;
  }
  agentRequest(p, 'POST', '/v1/power', { action: p.action }, function (res) { msg.respond(res); });
}));

service.register('probe', safe(function (msg, p) {
  if (!isIp(p.ip)) {
    msg.respond({ returnValue: false, errorCode: 3, errorText: 'Неверный IP-адрес' });
    return;
  }
  probe(p.ip.trim(), function (online, port) {
    msg.respond({ returnValue: true, online: online, port: port });
  });
}));
