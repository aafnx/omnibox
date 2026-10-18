/*
 * OmniBox для LG webOS: Wake-on-LAN + переключение входа HDMI.
 * ES5, без библиотек: webOS 4.x работает на Chromium 53.
 */
(function () {
  'use strict';

  var APP_ID = 'com.omnibox.launcher';
  var SVC = 'luna://' + APP_ID + '.service/';
  var STORE_KEY = 'omnibox.config.v1';
  var SWITCH_DELAY = 1500; // мс между отправкой сигнала и переключением входа

  var KEY = { LEFT: 37, UP: 38, RIGHT: 39, DOWN: 40, ENTER: 13, BACK: 461, ESC: 27, BACKSPACE: 8 };

  /* ── Знак и надпись (по конструкции дизайн-системы) ── */

  var WORD_PATH = 'M48.37 104.58Q33.72 104.58 24.28 102.6Q14.84 100.61 9.54 95.27Q4.23 89.93 2.12 79.91Q0 69.88 0 53.8V51.2Q0 37.92 1.3 28.9Q2.6 19.88 5.79 14.22Q8.98 8.55 14.49 5.56Q19.99 2.57 28.32 1.49Q36.65 0.42 48.37 0.42H74.61Q86.33 0.42 94.66 1.49Q102.99 2.57 108.5 5.56Q114 8.55 117.19 14.22Q120.38 19.88 121.68 28.9Q122.98 37.92 122.98 51.2V53.8Q122.98 69.88 120.87 79.91Q118.75 89.93 113.44 95.27Q108.14 100.61 98.7 102.6Q89.26 104.58 74.61 104.58ZM48.37 93.78H74.61Q83.92 93.78 90.3 93.12Q96.68 92.47 100.68 90.33Q104.69 88.18 106.8 83.82Q108.92 79.45 109.7 72.16Q110.48 64.87 110.48 53.8V51.2Q110.48 39.74 109.7 32.32Q108.92 24.9 106.8 20.6Q104.69 16.3 100.68 14.32Q96.68 12.33 90.3 11.78Q83.92 11.22 74.61 11.22H48.37Q39.06 11.22 32.68 11.78Q26.3 12.33 22.3 14.32Q18.29 16.3 16.18 20.6Q14.06 24.9 13.28 32.32Q12.5 39.74 12.5 51.2V53.8Q12.5 64.87 13.28 72.16Q14.06 79.45 16.18 83.82Q18.29 88.18 22.3 90.33Q26.3 92.47 32.68 93.12Q39.06 93.78 48.37 93.78Z M176.08 102.5V2.5H194.7L255.18 84.08L314.75 2.5H333.37V102.5H320.87V13.83L261.17 95.92H249.84L188.58 13.83V102.5Z M390.63 102.5V2.5H403.65L504.1 87.07H504.23V2.5H516.73V102.5H503.71L403.26 17.21H403.13V102.5Z M573.73 102.5V2.5H586.23V102.5Z M643.23 102.5V2.5H722.92Q734.25 2.5 741.51 4.62Q748.77 6.73 752.25 12.1Q755.73 17.47 755.73 27.3Q755.73 36.16 751.63 41.95Q747.53 47.75 739.07 48.33Q750.14 49.44 755.02 56.24Q759.9 63.05 759.9 73.33Q759.9 82.25 757.85 87.98Q755.8 93.71 750.98 96.87Q746.16 100.03 737.93 101.26Q729.69 102.5 717.39 102.5ZM655.73 44.17H714Q724.09 44.17 730.54 43.45Q736.98 42.73 740.11 39.22Q743.23 35.7 743.23 27.3Q743.23 20.73 741.38 17.67Q739.52 14.61 735.1 13.76Q730.67 12.92 722.92 12.92H655.73ZM655.73 92.08H717.39Q726.31 92.08 732.13 91.63Q737.96 91.17 741.31 89.41Q744.67 87.66 746.03 83.85Q747.4 80.04 747.4 73.33Q747.4 66.43 744.83 62.59Q742.26 58.75 737.67 57.06Q733.08 55.36 727.02 54.97Q720.97 54.58 714 54.58H655.73Z M856.94 104.58Q842.29 104.58 832.85 102.6Q823.41 100.61 818.11 95.27Q812.8 89.93 810.68 79.91Q808.57 69.88 808.57 53.8V51.2Q808.57 37.92 809.87 28.9Q811.17 19.88 814.36 14.22Q817.55 8.55 823.05 5.56Q828.55 2.57 836.89 1.49Q845.22 0.42 856.94 0.42H883.18Q894.9 0.42 903.23 1.49Q911.56 2.57 917.06 5.56Q922.57 8.55 925.76 14.22Q928.95 19.88 930.25 28.9Q931.55 37.92 931.55 51.2V53.8Q931.55 69.88 929.43 79.91Q927.32 89.93 922.01 95.27Q916.71 100.61 907.27 102.6Q897.83 104.58 883.18 104.58ZM856.94 93.78H883.18Q892.49 93.78 898.87 93.12Q905.25 92.47 909.25 90.33Q913.26 88.18 915.37 83.82Q917.49 79.45 918.27 72.16Q919.05 64.87 919.05 53.8V51.2Q919.05 39.74 918.27 32.32Q917.49 24.9 915.37 20.6Q913.26 16.3 909.25 14.32Q905.25 12.33 898.87 11.78Q892.49 11.22 883.18 11.22H856.94Q847.63 11.22 841.25 11.78Q834.87 12.33 830.87 14.32Q826.86 16.3 824.75 20.6Q822.63 24.9 821.85 32.32Q821.07 39.74 821.07 51.2V53.8Q821.07 64.87 821.85 72.16Q822.63 79.45 824.75 83.82Q826.86 88.18 830.87 90.33Q834.87 92.47 841.25 93.12Q847.63 93.78 856.94 93.78Z M976.05 102.5 1032.89 49.77 980.22 2.5H997.92L1041.54 41.69L1083.8 2.5H1101.51L1050.59 49.77L1109.38 102.5H1091.67L1041.87 57.84L993.76 102.5Z';

  var markSeq = 0;
  function markSvg(size, lit, breathe) {
    var id = 'obl' + (++markSeq);
    var cls = 'ob-mark' + (lit ? ' ob-mark--lit' : '') + (breathe ? ' ob-mark--breathe' : '');
    return '<svg class="' + cls + '" width="' + size + '" height="' + size + '" viewBox="0 0 100 100" aria-hidden="true">' +
      '<defs><linearGradient id="' + id + '" gradientUnits="userSpaceOnUse" x1="0" y1="0" x2="100" y2="100">' +
      '<stop offset="0.08" stop-color="#beefff"/><stop offset="0.5" stop-color="#7c6cff"/>' +
      '<stop offset="0.75" stop-color="#ff9654"/><stop offset="0.95" stop-color="#ffbe78"/></linearGradient></defs>' +
      '<circle cx="50" cy="50" r="46" fill="none" stroke="url(#' + id + ')" stroke-width="8"/>' +
      '<circle cx="50" cy="50" r="33.58" fill="none" stroke="url(#' + id + ')" stroke-width="1.5" stroke-opacity="0.55"/></svg>';
  }
  function wordSvg(h) {
    var w = (h * 1109 / 105).toFixed(1);
    return '<svg class="logo__word" width="' + w + '" height="' + h + '" viewBox="0 0 1109 105" aria-hidden="true"><path d="' + WORD_PATH + '"/></svg>';
  }

  var ICONS = {
    power: '<path d="M12 3v8"/><path d="M7.05 6.05a7.5 7.5 0 1 0 9.9 0"/>',
    settings: '<circle cx="12" cy="12" r="3"/><path d="M12 3v2.5M12 18.5V21M3 12h2.5M18.5 12H21M5.6 5.6l1.8 1.8M16.6 16.6l1.8 1.8M5.6 18.4l1.8-1.8M16.6 7.4l1.8-1.8"/>',
    server: '<rect x="4" y="4.5" width="16" height="6" rx="1.5"/><rect x="4" y="13.5" width="16" height="6" rx="1.5"/><circle cx="8" cy="7.5" r="0.6"/><circle cx="8" cy="16.5" r="0.6"/>',
    monitor: '<rect x="3" y="4" width="18" height="12" rx="2"/><path d="M8 20h8M12 16v4"/>',
    search: '<circle cx="11" cy="11" r="6.5"/><path d="m20 20-4.2-4.2"/>',
    moon: '<path d="M20 14.5A8 8 0 0 1 9.5 4a8 8 0 1 0 10.5 10.5z"/>',
    restart: '<path d="M4 12a8 8 0 1 0 2.35-5.65"/><path d="M4 4.5V8h3.5"/>',
    lock: '<rect x="5" y="11" width="14" height="9" rx="2"/><path d="M8 11V8a4 4 0 0 1 8 0v3"/>',
    router: '<rect x="3" y="13" width="18" height="7" rx="2"/><path d="M7 16.5h.01M11 16.5h.01M15 9.5a4 4 0 0 1 4 3.5M15 6a7.5 7.5 0 0 1 7.5 7"/>'
  };
  function iconSvg(name) {
    return '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">' + (ICONS[name] || ICONS.server) + '</svg>';
  }

  function $(id) { return document.getElementById(id); }
  function qsa(sel, root) { return Array.prototype.slice.call((root || document).querySelectorAll(sel)); }
  function esc(s) { return String(s == null ? '' : s).replace(/[&<>"]/g, function (c) { return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]; }); }

  function hydrate(root) {
    qsa('[data-mark]', root).forEach(function (el) {
      el.outerHTML = markSvg(+el.getAttribute('data-mark'), el.getAttribute('data-lit') === '1', el.getAttribute('data-breathe') === '1');
    });
    qsa('[data-word]', root).forEach(function (el) { el.innerHTML = wordSvg(+el.getAttribute('data-word')); el.removeAttribute('data-word'); });
    qsa('[data-icon]', root).forEach(function (el) {
      var svg = iconSvg(el.getAttribute('data-icon'));
      if (el.classList.contains('ring-icon')) el.innerHTML = svg; else el.outerHTML = svg;
    });
  }

  /* ── Luna: вызовы системы и своего сервиса ──────────── */

  var isTV = typeof window.PalmServiceBridge !== 'undefined';
  var bridges = [];

  function luna(uri, params, cb) {
    cb = cb || function () {};
    if (!isTV) { setTimeout(function () { cb(demoLuna(uri, params || {})); }, demoDelay(uri)); return; }
    var bridge = new window.PalmServiceBridge();
    bridges.push(bridge); // держим ссылку, иначе ответ может не прийти
    var done = false;
    bridge.onservicecallback = function (raw) {
      if (done) return;
      done = true;
      var i = bridges.indexOf(bridge);
      if (i >= 0) bridges.splice(i, 1);
      var res;
      try { res = JSON.parse(raw); } catch (e) { res = { returnValue: false, errorText: 'Некорректный ответ' }; }
      cb(res);
    };
    bridge.call(uri, JSON.stringify(params || {}));
  }

  /* Ответы для предпросмотра в обычном браузере. */
  function demoDelay(uri) {
    if (/discover$/.test(uri)) return 2600;
    if (/agentPair$/.test(uri)) return 700;
    if (/resolveMac$/.test(uri)) return 900;
    return 300;
  }
  function demoLuna(uri, p) {
    if (/wake$/.test(uri)) return { returnValue: true, mac: p.mac, sent: 18, targets: ['255.255.255.255', '192.168.1.255', p.ip] };
    if (/probe$/.test(uri)) return { returnValue: true, online: false };
    if (/resolveMac$/.test(uri)) return { returnValue: true, ip: p.ip, mac: '00:11:22:33:44:55', name: 'OMNIBOX' };
    if (/discover$/.test(uri)) {
      return { returnValue: true, self: '192.168.1.50', subnet: '192.168.1.0/24', devices: [
        { ip: '192.168.1.1', mac: '50:FF:20:11:02:A1', name: 'router.lan', source: 'dns' },
        { ip: '192.168.1.20', mac: '90:09:D0:3C:5E:12', name: 'DS224', source: 'netbios' },
        { ip: '192.168.1.45', mac: 'A4:83:E7:09:6B:C4' },
        { ip: '192.168.1.10', mac: '00:11:22:33:44:55', name: 'OMNIBOX', source: 'netbios' }
      ] };
    }
    if (/agentInfo$/.test(uri)) return { returnValue: true, app: 'win-cli', version: '1.4.0', name: 'OMNIBOX', paired: !!cfg.agentToken };
    if (/agentPair$/.test(uri)) return String(p.code).replace(/\D/g, '').length === 6 ? { returnValue: true, token: 'demo-token', name: 'OMNIBOX' } : { returnValue: false, errorText: 'Код — шесть цифр' };
    if (/agentPower$/.test(uri)) return { returnValue: true, ok: true, action: p.action };
    if (/applicationManager\/launch$/.test(uri)) return { returnValue: true };
    return { returnValue: true };
  }

  /* ── Настройки ──────────────────────────────────────── */

  var AGENT_PORT = 7798;
  var DEFAULTS = { name: 'OMNIBOX', ip: '', mac: '', hdmi: 3, autoRun: true, closeAfter: true, agentPort: AGENT_PORT, agentToken: '' };
  var cfg = load();

  function load() {
    var c = {};
    try { c = JSON.parse(localStorage.getItem(STORE_KEY) || '{}') || {}; } catch (e) { c = {}; }
    var out = {};
    Object.keys(DEFAULTS).forEach(function (k) { out[k] = c[k] !== undefined ? c[k] : DEFAULTS[k]; });
    return out;
  }
  function save() {
    try { localStorage.setItem(STORE_KEY, JSON.stringify(cfg)); } catch (e) { /* без хранилища — до перезапуска */ }
  }

  function isIp(s) {
    var p = String(s || '').trim().split('.');
    if (p.length !== 4) return false;
    for (var i = 0; i < 4; i++) if (!/^\d{1,3}$/.test(p[i]) || +p[i] > 255) return false;
    return true;
  }
  function normMac(s) {
    var hex = String(s || '').replace(/[^0-9a-fA-F]/g, '');
    return hex.length === 12 ? hex.toUpperCase().match(/.{2}/g).join(':') : null;
  }
  function hdmiApp(n) { return 'com.webos.app.hdmi' + n; }

  /* ── Экраны ─────────────────────────────────────────── */

  var current = null;
  var history = [];
  var focusMemory = {};

  function show(name, opts) {
    opts = opts || {};
    if (current === name) return;
    if (current) {
      var prev = $('screen-' + current);
      focusMemory[current] = focused;
      prev.classList.remove('is-shown', 'is-active');
      if (!opts.replace) history.push(current);
    }
    current = name;
    var el = $('screen-' + name);
    el.classList.add('is-active');
    setTimeout(function () { el.classList.add('is-shown'); }, 20);
    if (screens[name] && screens[name].enter) screens[name].enter(opts);
    var target = opts.focus || (focusMemory[name] && document.body.contains(focusMemory[name]) ? focusMemory[name] : null) || el.querySelector('[data-focus]');
    setFocus(target);
  }
  function back() {
    if (modalOpen) { closeModal(); return; }
    if (current === 'launch') { cancelSwitch(); show('home', { replace: true }); return; }
    var prev = history.pop();
    if (prev && prev !== 'launch') show(prev, { replace: true, keepForm: true });
    else closeApp();
  }

  /* ── Навигация пультом ──────────────────────────────── */

  var focused = null;

  function setFocus(el) {
    if (focused) focused.classList.remove('is-focus');
    focused = el || null;
    if (focused) focused.classList.add('is-focus');
  }

  function focusables() {
    if (!current) return [];
    var root = modalOpen ? $('modal') : $('screen-' + current);
    return qsa('[data-focus]', root).filter(function (el) { return el.offsetParent !== null; });
  }

  function move(dir) {
    var list = focusables();
    if (!focused || list.indexOf(focused) < 0) { setFocus(list[0]); return; }
    var a = focused.getBoundingClientRect();
    var ax = a.left + a.width / 2, ay = a.top + a.height / 2;
    var best = null, bestScore = Infinity;
    list.forEach(function (el) {
      if (el === focused) return;
      var b = el.getBoundingClientRect();
      var bx = b.left + b.width / 2, by = b.top + b.height / 2;
      var main, side;
      if (dir === 'left') { main = a.left - b.right; side = Math.abs(by - ay); if (bx >= ax) return; }
      if (dir === 'right') { main = b.left - a.right; side = Math.abs(by - ay); if (bx <= ax) return; }
      if (dir === 'up') { main = a.top - b.bottom; side = Math.abs(bx - ax); if (by >= ay) return; }
      if (dir === 'down') { main = b.top - a.bottom; side = Math.abs(bx - ax); if (by <= ay) return; }
      var score = Math.max(main, 0) + side * 2.2;
      if (score < bestScore) { bestScore = score; best = el; }
    });
    if (best) setFocus(best);
  }

  function activate(el) {
    if (!el) return;
    if (el.tagName === 'INPUT') { el.focus(); return; }
    var action = el.getAttribute('data-action');
    if (action && actions[action]) { actions[action](el); return; }
    if (el.hasAttribute('data-hdmi')) { form.hdmi = +el.getAttribute('data-hdmi'); renderForm(); return; }
    if (el.hasAttribute('data-toggle')) { var k = el.getAttribute('data-toggle'); form[k] = !form[k]; renderForm(); return; }
    if (el.hasAttribute('data-device')) { pickDevice(+el.getAttribute('data-device')); return; }
    if (el.hasAttribute('data-power')) { powerAction(el.getAttribute('data-power')); }
  }

  document.addEventListener('keydown', function (e) {
    var k = e.keyCode;
    if (current === 'launch' && switchTimer) {
      // Любая кнопка во время отсчёта — остаться в приложении.
      e.preventDefault();
      cancelSwitch();
      show('home', { replace: true });
      return;
    }
    var inInput = document.activeElement && document.activeElement.tagName === 'INPUT';
    if (inInput) {
      if (k === KEY.ENTER || k === KEY.UP || k === KEY.DOWN || k === KEY.BACK || k === KEY.ESC) {
        document.activeElement.blur();
        e.preventDefault();
        if (k === KEY.UP) move('up');
        if (k === KEY.DOWN) move('down');
      }
      return;
    }
    switch (k) {
      case KEY.LEFT: move('left'); break;
      case KEY.RIGHT: move('right'); break;
      case KEY.UP: move('up'); break;
      case KEY.DOWN: move('down'); break;
      case KEY.ENTER: activate(focused); break;
      case KEY.BACK: case KEY.ESC: case KEY.BACKSPACE: back(); break;
      default: return;
    }
    e.preventDefault();
  });

  // Указка Magic Remote и мышь.
  document.addEventListener('mouseover', function (e) {
    var el = e.target.closest ? e.target.closest('[data-focus]') : null;
    if (el && modalOpen && !$('modal').contains(el)) return;
    if (el && el !== focused) setFocus(el);
  });
  document.addEventListener('click', function (e) {
    if (current === 'launch' && switchTimer) { cancelSwitch(); show('home', { replace: true }); return; }
    var el = e.target.closest ? e.target.closest('[data-focus]') : null;
    if (el && modalOpen && !$('modal').contains(el)) return;
    if (el) { setFocus(el); activate(el); }
  });

  /* ── Уведомления ────────────────────────────────────── */

  var toastTimer = null;
  var TONE_WORD = { ok: 'Готово', warn: 'Внимание', danger: 'Ошибка', info: 'Сведения' };
  function toast(tone, text, word) {
    $('toast-badge').className = 'ob-badge ob-badge--' + tone;
    $('toast-word').textContent = word || TONE_WORD[tone];
    $('toast-text').textContent = text;
    $('toast').classList.add('is-shown');
    clearTimeout(toastTimer);
    toastTimer = setTimeout(function () { $('toast').classList.remove('is-shown'); }, 4200);
  }

  function badge(el, tone, word) {
    el.className = 'ob-badge ob-badge--' + tone;
    el.innerHTML = '<span class="ob-badge__dot"></span>' + esc(word);
  }

  /* ── Пробуждение и переключение ─────────────────────── */

  var switchTimer = null;
  var closeTimer = null;

  function wake(cb) {
    luna(SVC + 'wake', { mac: cfg.mac, ip: cfg.ip }, cb);
  }

  function switchInput(cb) {
    luna('luna://com.webos.applicationManager/launch', { id: hdmiApp(cfg.hdmi) }, function (res) {
      if (cb) cb(res);
    });
  }

  function cancelSwitch() {
    clearTimeout(switchTimer); switchTimer = null;
    clearTimeout(closeTimer); closeTimer = null;
    $('launch-bar').classList.remove('is-running');
  }

  function run() {
    if (!normMac(cfg.mac)) {
      show('settings', { focus: $('f-mac') });
      toast('warn', 'Укажите MAC-адрес или найдите его по IP');
      return;
    }
    cancelSwitch();
    show('launch', { replace: current === 'launch' });
    $('launch-status').textContent = 'Пробуждение ' + cfg.name;
    $('launch-sub').innerHTML = 'Сигнал на <code>' + esc(cfg.mac) + '</code> · вход HDMI ' + cfg.hdmi;
    $('launch-hint').textContent = 'Нажмите любую кнопку, чтобы остаться в приложении';

    var bar = $('launch-bar');
    bar.classList.remove('is-running');
    bar.querySelector('span').style.transitionDuration = SWITCH_DELAY + 'ms';
    void bar.offsetWidth;
    bar.classList.add('is-running');

    var wakeOk = null;
    wake(function (res) {
      wakeOk = !!res.returnValue;
      if (!wakeOk) toast('danger', res.errorText || 'Сигнал не отправлен. Проверьте, что сервис приложения установлен.');
    });

    switchTimer = setTimeout(function () {
      switchTimer = null;
      $('launch-status').textContent = 'Переключение на HDMI ' + cfg.hdmi;
      $('launch-hint').textContent = '';
      switchInput(function (res) {
        if (!res.returnValue) {
          show('home', { replace: true });
          toast('danger', 'Не удалось открыть HDMI ' + cfg.hdmi + (res.errorText ? ': ' + res.errorText : ''));
          return;
        }
        if (cfg.closeAfter && isTV) closeTimer = setTimeout(closeApp, 900);
        else closeTimer = setTimeout(function () { show('home', { replace: true }); }, isTV ? 900 : 1600);
      });
    }, SWITCH_DELAY);
  }

  function closeApp() {
    try { window.close(); } catch (e) { /* в браузере закрыть нельзя */ }
  }

  /* ── Главный экран ──────────────────────────────────── */

  var probeSeq = 0;
  function refreshHome() {
    $('home-title').textContent = cfg.name;
    $('device-name').textContent = cfg.name;
    $('hdmi-only').textContent = 'Только HDMI ' + cfg.hdmi;
    badge($('hdmi-badge'), 'neutral', 'Вход HDMI ' + cfg.hdmi);
    var mac = normMac(cfg.mac);
    $('device-meta').innerHTML = (isIp(cfg.ip) ? esc(cfg.ip) : '<span class="missing">IP не указан</span>') +
      '<span class="sep">·</span>' + (mac ? esc(mac) : '<span class="missing">MAC не указан</span>');
    $('home-lead').textContent = 'Пробуждение по сети и переключение на HDMI ' + cfg.hdmi + ' одной кнопкой.';

    refreshAgentState();
    var st = $('device-status');
    if (!isIp(cfg.ip)) { badge(st, 'neutral', 'Неизвестно'); return; }
    badge(st, 'neutral', 'Проверяем');
    var seq = ++probeSeq;
    luna(SVC + 'probe', { ip: cfg.ip }, function (res) {
      if (seq !== probeSeq) return;
      if (!res.returnValue) badge(st, 'neutral', 'Неизвестно');
      else if (res.online) badge(st, 'ok', 'В сети');
      else badge(st, 'neutral', 'Не в сети');
    });
  }

  /* ── Форма настроек ─────────────────────────────────── */

  var form = {};
  // Поля, которые правит форма. Токен и порт агента WinCLI сюда не входят:
  // их пишет только экран подключения, иначе «Сохранить» затрёт токен.
  var FORM_KEYS = ['name', 'ip', 'mac', 'hdmi', 'autoRun', 'closeAfter'];

  function openForm() {
    form = {};
    FORM_KEYS.forEach(function (k) { form[k] = cfg[k]; });
    $('f-name').value = form.name;
    $('f-ip').value = form.ip;
    $('f-mac').value = form.mac;
    clearErrors();
    renderForm();
  }

  function renderForm() {
    qsa('[data-hdmi]').forEach(function (c) {
      c.classList.toggle('is-selected', +c.getAttribute('data-hdmi') === form.hdmi);
    });
    $('sw-autoRun').classList.toggle('is-on', !!form.autoRun);
    $('sw-closeAfter').classList.toggle('is-on', !!form.closeAfter);
    badge($('agent-badge'), cfg.agentToken ? 'ok' : 'neutral', cfg.agentToken ? 'Подключено' : 'Не подключено');
    $('pair-btn').textContent = cfg.agentToken ? 'Подключить заново' : 'Подключить';
  }

  function fieldError(fld, hint, text) {
    $(fld).classList.add('field--error');
    $(hint).setAttribute('data-default', $(hint).getAttribute('data-default') || $(hint).textContent);
    $(hint).textContent = text;
  }
  function clearErrors() {
    [['fld-ip', 'h-ip'], ['fld-mac', 'h-mac']].forEach(function (p) {
      $(p[0]).classList.remove('field--error');
      var d = $(p[1]).getAttribute('data-default');
      if (d) $(p[1]).textContent = d;
    });
  }

  function readForm() {
    clearErrors();
    var ok = true;
    var name = $('f-name').value.trim() || 'OMNIBOX';
    var ip = $('f-ip').value.trim();
    var macRaw = $('f-mac').value.trim();
    var mac = normMac(macRaw);
    if (ip && !isIp(ip)) { fieldError('fld-ip', 'h-ip', 'Формат: четыре числа от 0 до 255, например 192.168.1.10'); ok = false; }
    if (!mac) { fieldError('fld-mac', 'h-mac', macRaw ? 'Формат: шесть пар символов, например 00:11:22:33:44:55' : 'Укажите MAC-адрес или нажмите «Найти MAC»'); ok = false; }
    if (mac) $('f-mac').value = mac;
    form.name = name; form.ip = ip; form.mac = mac || macRaw;
    return ok;
  }

  /* ── Поиск устройств ────────────────────────────────── */

  var found = [];

  function scan() {
    found = [];
    $('scan-wait').hidden = false;
    $('tiles').hidden = true;
    $('tiles').innerHTML = '';
    $('scan-text').textContent = 'Опрашиваем подсеть, около 5 секунд';
    $('scan-note').textContent = '';
    setFocus(qsa('[data-action="rescan"]', $('screen-discover'))[0]);
    luna(SVC + 'discover', {}, function (res) {
      if (current !== 'discover') return;
      if (!res.returnValue) {
        $('scan-text').textContent = res.errorText || 'Поиск не удался. Проверьте, что сервис приложения установлен.';
        return;
      }
      found = res.devices || [];
      $('scan-lead').innerHTML = 'Подсеть <code>' + esc(res.subnet) + '</code>. Находятся только включённые устройства.';
      if (!found.length) {
        $('scan-text').textContent = 'Устройств не найдено. Включите OMNIBOX и повторите поиск.';
        return;
      }
      $('scan-wait').hidden = true;
      $('tiles').hidden = false;
      $('tiles').innerHTML = found.map(function (d, i) {
        var isCurrent = normMac(cfg.mac) && normMac(cfg.mac) === d.mac;
        var icon = /router|gateway|keenetic|mikrotik|\.1$/i.test((d.name || '') + ' ' + d.ip) && /\.1$/.test(d.ip) ? 'router' : 'server';
        return '<div class="tile" data-focus data-device="' + i + '">' +
          '<div class="ring-icon">' + iconSvg(icon) + '</div>' +
          '<div class="tile__body"><div class="tile__name' + (d.name ? '' : ' is-unknown') + '">' + esc(d.name || 'Без имени') +
          (isCurrent ? '<span class="tile__mark">выбрано</span>' : '') + '</div>' +
          '<div class="tile__addr">' + esc(d.ip) + '</div><div class="tile__addr">' + esc(d.mac) + '</div></div></div>';
      }).join('');
      $('scan-note').textContent = 'Найдено: ' + found.length + '. Выберите устройство для пробуждения.';
      var firstNamed = qsa('[data-device]', $('tiles')).filter(function (el) {
        return /omnibox/i.test(found[+el.getAttribute('data-device')].name || '');
      })[0];
      setFocus(firstNamed || $('tiles').querySelector('[data-device]'));
    });
  }

  function pickDevice(i) {
    var d = found[i];
    if (!d) return;
    if (!history.length || history[history.length - 1] !== 'settings') openForm();
    form.ip = d.ip;
    form.mac = d.mac;
    if (d.name && d.source === 'netbios') form.name = d.name.toUpperCase() === 'OMNIBOX' ? 'OMNIBOX' : d.name;
    var prev = history.pop();
    if (prev !== 'settings') history.push(prev);
    show('settings', { replace: true, keepForm: true, focus: qsa('[data-action="save"]')[0] });
    $('f-name').value = form.name;
    $('f-ip').value = form.ip;
    $('f-mac').value = form.mac;
    clearErrors();
    toast('ok', d.ip + ' · ' + d.mac + ' — сохраните настройки', 'Выбрано');
  }

  /* ── Действия ───────────────────────────────────────── */

  var actions = {
    run: run,
    hdmi: function () {
      switchInput(function (res) {
        if (!res.returnValue) toast('danger', 'Не удалось открыть HDMI ' + cfg.hdmi + (res.errorText ? ': ' + res.errorText : ''));
        else if (!isTV) toast('info', 'Вход HDMI ' + cfg.hdmi + ' открыт');
      });
    },
    settings: function () { show('settings'); },
    discover: function () { show('discover'); },
    rescan: scan,
    back: back,
    save: function () {
      if (!readForm()) { toast('warn', 'Исправьте отмеченные поля'); return; }
      FORM_KEYS.forEach(function (k) { cfg[k] = form[k]; });
      cfg.mac = normMac(cfg.mac);
      save();
      history = [];
      show('home', { replace: true });
      toast('ok', 'Настройки сохранены');
    },
    pair: function () {
      var ip = current === 'settings' ? $('f-ip').value.trim() : cfg.ip;
      if (!isIp(ip)) {
        if (current !== 'settings') show('settings', { focus: $('f-ip') });
        else fieldError('fld-ip', 'h-ip', 'Сначала укажите IP-адрес OMNIBOX');
        toast('warn', 'Для подключения WinCLI нужен IP-адрес OMNIBOX');
        return;
      }
      pairIp = ip;
      show('pair');
    },
    doPair: doPair,
    unpair: function () {
      cfg.agentToken = '';
      save();
      renderPair();
      toast('info', 'Подключение забыто. Для нового нужен код: win pair', 'Отключено');
    },
    confirm: function () { var f = modalOk; closeModal(); if (f) f(); },
    dismiss: function () { closeModal(); },
    resolve: function (btn) {
      var ip = $('f-ip').value.trim();
      clearErrors();
      if (!isIp(ip)) { fieldError('fld-ip', 'h-ip', 'Введите IP-адрес, например 192.168.1.10'); return; }
      btn.classList.add('is-busy');
      btn.textContent = 'Ищем…';
      luna(SVC + 'resolveMac', { ip: ip }, function (res) {
        btn.classList.remove('is-busy');
        btn.textContent = 'Найти MAC';
        if (!res.returnValue) { fieldError('fld-mac', 'h-mac', res.errorText || 'Устройство не ответило. Включите его и повторите.'); return; }
        $('f-mac').value = res.mac;
        if (res.name && !$('f-name').value.trim()) $('f-name').value = res.name;
        toast('ok', ip + ' → ' + res.mac, 'Найдено');
      });
    }
  };

  var screens = {
    home: { enter: refreshHome },
    settings: { enter: function (opts) { if (!opts.keepForm) openForm(); } },
    discover: { enter: scan },
    pair: { enter: enterPair }
  };

  /* ── Питание через агент WinCLI ─────────────────────── */

  var POWER = {
    sleep: { done: 'засыпает', toast: 'Сон' },
    reboot: { done: 'перезагружается', toast: 'Перезагрузка', confirm: 'Перезагрузить', text: 'Несохранённые данные в открытых программах могут пропасть.' },
    shutdown: { done: 'выключается', toast: 'Выключение', confirm: 'Выключить', text: 'Включить снова можно кнопкой «Включить» — по сети.' },
    lock: { done: 'заблокирован', toast: 'Блокировка' }
  };

  function agentParams(extra) {
    var p = { ip: cfg.ip, port: cfg.agentPort || AGENT_PORT, token: cfg.agentToken };
    Object.keys(extra || {}).forEach(function (k) { p[k] = extra[k]; });
    return p;
  }

  var agentSeq = 0;
  function refreshAgentState() {
    var el = $('agent-state');
    if (!cfg.agentToken) { badge(el, 'neutral', 'Не подключено'); return; }
    if (!isIp(cfg.ip)) { badge(el, 'neutral', 'Нет IP-адреса'); return; }
    badge(el, 'neutral', 'Проверяем');
    var seq = ++agentSeq;
    luna(SVC + 'agentInfo', agentParams(), function (res) {
      if (seq !== agentSeq) return;
      if (res.returnValue) badge(el, 'ok', 'На связи · win ' + (res.version || ''));
      else badge(el, 'neutral', 'Недоступен');
    });
  }

  function powerAction(action) {
    var meta = POWER[action];
    if (!meta) return;
    if (!cfg.agentToken) {
      toast('warn', 'Подключите агент WinCLI, чтобы управлять питанием');
      actions.pair();
      return;
    }
    if (meta.confirm) {
      openModal(meta.confirm + ' ' + cfg.name + '?', meta.text, meta.confirm, function () { sendPower(action); });
      return;
    }
    sendPower(action);
  }

  function sendPower(action) {
    var meta = POWER[action];
    luna(SVC + 'agentPower', agentParams({ action: action }), function (res) {
      if (res.returnValue) {
        toast('ok', cfg.name + ' ' + meta.done, meta.toast);
        if (action !== 'lock') badge($('device-status'), 'neutral', action === 'reboot' ? 'Перезагрузка' : 'Отключается');
        return;
      }
      if (res.status === 401) {
        cfg.agentToken = '';
        save();
        refreshAgentState();
        toast('danger', 'Агент не узнал телевизор. Подключите заново: win pair');
        return;
      }
      toast('danger', res.errorText || 'Команда не выполнена');
    });
  }

  /* ── Экран подключения ──────────────────────────────── */

  var pairIp = '';

  function renderPair() {
    $('unpair-btn').hidden = !cfg.agentToken;
  }

  function pairPort() {
    var v = $('f-port').value.trim();
    var n = +v;
    return v === '' ? AGENT_PORT : (/^\d+$/.test(v) && n > 0 && n < 65536 ? n : 0);
  }

  function enterPair() {
    pairIp = pairIp || cfg.ip;
    $('f-code').value = '';
    $('f-port').value = String(cfg.agentPort || AGENT_PORT);
    $('fld-code').classList.remove('field--error');
    $('h-code').textContent = 'Действует 10 минут, один раз';
    renderPair();
    checkAgent();
    setFocus($('f-code'));
  }

  function checkAgent() {
    var st = $('pair-status');
    var port = pairPort() || AGENT_PORT;
    $('pair-addr').textContent = pairIp + ':' + port;
    badge(st, 'neutral', 'Ищем агент');
    luna(SVC + 'agentInfo', { ip: pairIp, port: port }, function (res) {
      if (current !== 'pair') return;
      if (res.returnValue) badge(st, 'ok', 'Найден · ' + (res.name || 'ПК') + ' · win ' + (res.version || ''));
      else badge(st, 'warn', res.errorText || 'Агент не отвечает');
    });
  }

  function doPair() {
    var code = $('f-code').value.replace(/\D/g, '');
    var port = pairPort();
    $('fld-code').classList.remove('field--error');
    if (code.length !== 6) { codeError('Введите шесть цифр с экрана OMNIBOX'); return; }
    if (!port) { codeError('Порт — число от 1 до 65535'); return; }
    luna(SVC + 'agentPair', { ip: pairIp, port: port, code: code, name: 'Телевизор LG' }, function (res) {
      if (!res.returnValue) { codeError(res.errorText || 'Не удалось подключиться'); return; }
      cfg.agentToken = res.token;
      cfg.agentPort = port;
      cfg.ip = pairIp;
      save();
      if (form && history[history.length - 1] === 'settings') form.ip = pairIp;
      toast('ok', 'Питание ' + (res.name || cfg.name) + ' — с пульта', 'Подключено');
      back();
      if (current === 'settings') { $('f-ip').value = pairIp; renderForm(); }
    });
  }

  function codeError(text) {
    $('fld-code').classList.add('field--error');
    $('h-code').textContent = text;
  }

  /* ── Окно подтверждения ─────────────────────────────── */

  var modalOpen = false;
  var modalOk = null;
  var modalReturn = null;

  function openModal(title, text, okLabel, onOk) {
    $('modal-title').textContent = title;
    $('modal-text').textContent = text || '';
    $('modal-ok').textContent = okLabel;
    modalOk = onOk;
    modalReturn = focused;
    modalOpen = true;
    $('modal').hidden = false;
    setFocus(qsa('[data-action="dismiss"]', $('modal'))[0]); // по умолчанию — «Отмена»
  }

  function closeModal() {
    modalOpen = false;
    modalOk = null;
    $('modal').hidden = true;
    setFocus(modalReturn);
  }

  /* ── Масштаб: макет 1200×675 растягивается на экран ── */

  function fit() {
    var s = Math.min(window.innerWidth / 1200, window.innerHeight / 675);
    var app = $('app');
    app.style.transformOrigin = '0 0';
    app.style.transform = 'scale(' + s + ')';
    app.style.marginLeft = Math.max(0, (window.innerWidth - 1200 * s) / 2) + 'px';
    app.style.marginTop = Math.max(0, (window.innerHeight - 675 * s) / 2) + 'px';
  }

  /* ── Старт ──────────────────────────────────────────── */

  function start() {
    hydrate(document);
    fit();
    window.addEventListener('resize', fit);
    if (!isTV) $('demo-flag').hidden = false;

    var params = {};
    try { params = window.PalmSystem && window.PalmSystem.launchParams ? JSON.parse(window.PalmSystem.launchParams || '{}') : {}; } catch (e) { params = {}; }
    var query = location.search;

    if (/[?&]screen=(\w+)/.test(query)) { show(RegExp.$1); return; }
    if (params.action === 'settings') { show('settings'); return; }
    if (!normMac(cfg.mac)) { show('home', { replace: true }); show('settings', { focus: $('f-mac') }); toast('info', 'Укажите устройство для пробуждения', 'Первый запуск'); return; }
    if (cfg.autoRun || params.action === 'wake') { run(); return; }
    show('home');
  }

  // Повторный выбор иконки, когда приложение уже открыто в фоне.
  document.addEventListener('webOSRelaunch', function () {
    if (cfg.autoRun && normMac(cfg.mac)) run();
    else show('home', { replace: true });
  });

  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', start);
  else start();
})();
