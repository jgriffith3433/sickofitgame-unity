// The bridge from HawkesNet.cs to Hawkes Net's web SDK (hawkes-net.mjs, Hawkes Gen docs/net.md), which runs in the page.
// Rooms live in window.__hawkesNet by handle. Events go to the C# side with SendMessage(<receiver>, 'OnNet', json); avatar
// poses are kept per room and read once a frame (HawkesNet_Poses), so many players don't mean many SendMessage calls.
// Nothing here throws into the game.
mergeInto(LibraryManager.library, {
  HawkesNet_Join: function (handle, roomPtr, optsPtr, receiverPtr) {
    var room = UTF8ToString(roomPtr), receiver = UTF8ToString(receiverPtr), o = {};
    try { o = JSON.parse(UTF8ToString(optsPtr) || '{}'); } catch (e) {}
    var S = window.__hawkesNet = window.__hawkesNet || { rooms: {} };
    var tell = function (e, x) {
      x = x || {};
      x.h = handle; x.e = e;
      try { SendMessage(receiver, 'OnNet', JSON.stringify(x)); } catch (err) {}
    };
    var who = function (p) { return p ? { peer: p.id || '', name: p.name || '' } : { peer: '', name: '' }; };
    var slot = S.rooms[handle] = { net: null, poses: {}, dirty: false, gone: false, kinds: {} };
    var sdk = o.sdkUrl || 'https://net.hawkesgen.com/sdk/hawkes-net@1.mjs';
    var opts = { name: o.name || undefined, transport: o.transport || 'auto', role: o.role || 'player', log: o.log !== false };
    if (o.project) opts.project = o.project;
    if (o.endpoint) opts.endpoint = o.endpoint;
    if (o.config) opts.config = o.config;
    if (o.leaveOnHide === false) opts.leaveOnHide = false;
    if (o.ticketUrl) {
      // The game's own server hands out tickets (it asks hawkesgen.com with its API key): POST { room, name } -> Gen's answer.
      opts.ticket = function () {
        return fetch(o.ticketUrl, { method: 'POST', headers: { 'Content-Type': 'application/json' }, credentials: o.ticketCredentials ? 'include' : 'same-origin', body: JSON.stringify({ room: room, name: o.name || null }) })
          .then(function (r) { if (!r.ok) throw new Error('ticket refused (' + r.status + ')'); return r.json(); });
      };
    }
    // import() through Function: some Unity versions' build step can't parse a dynamic import in a jslib.
    (new Function('u', 'return import(u)'))(sdk).then(function (m) { return m.join(room, opts); }).then(function (net) {
      if (slot.gone) { net.leave(); return; }
      slot.net = net;
      net.on('*', function (data, from, meta) {
        var w = who(from);
        tell('msg', { kind: meta.kind, data: JSON.stringify(data === undefined ? null : data), peer: w.peer, name: w.name, reliable: meta.reliable });
      });
      net.onPose(function (peer, s) {
        var p = s && s.p;
        if (!p) return;
        slot.poses[peer] = [p[0], p[1], p[2], s.y || 0];
        slot.dirty = true;
      });
      net.when('peer:join', function (p) { tell('join', who(p)); });
      net.when('peer:leave', function (x) { delete slot.poses[x.peer.id]; slot.dirty = true; tell('leave', { peer: x.peer.id, name: x.peer.name || '', reason: x.reason || '' }); });
      net.when('host', function (x) { tell('host', { peer: x.host || '', reason: x.reason || '' }); });
      net.when('transport', function (x) { tell('transport', { transport: x.to, reason: x.why || '' }); });
      net.when('status', function (x) { tell('status', { status: x.status, transport: x.transport }); });
      net.when('problem', function (x) { tell('problem', { reason: x.code, data: String(x.detail || '') }); });
      var peers = [];
      net.peers.forEach(function (p) { peers.push(who(p)); });
      tell('ready', { peer: net.id, name: net.name || '', transport: net.transport, host: net.host || '', data: JSON.stringify(peers) });
    }).catch(function (e) {
      tell('error', { reason: (e && e.code) || 'join', data: String((e && e.message) || e) });
    });
  },

  HawkesNet_Send: function (handle, kindPtr, dataPtr, reliable, toPtr) {
    try {
      var slot = (window.__hawkesNet || { rooms: {} }).rooms[handle];
      if (!slot || !slot.net) return 0;
      var data = null, raw = UTF8ToString(dataPtr), to = UTF8ToString(toPtr);
      if (raw) data = JSON.parse(raw);
      var opt = { reliable: !!reliable };
      if (to) opt.to = to;
      slot.net.send(UTF8ToString(kindPtr), data, opt);
      return 1;
    } catch (e) { return 0; }
  },

  HawkesNet_Pose: function (handle, x, y, z, yaw) {
    var slot = (window.__hawkesNet || { rooms: {} }).rooms[handle];
    if (slot && slot.net) try { slot.net.pose([x, y, z], yaw); } catch (e) {}
  },

  // The newest pose of every peer as "id,x,y,z,yaw;..." when any changed since the last read, else null.
  HawkesNet_Poses: function (handle) {
    var slot = (window.__hawkesNet || { rooms: {} }).rooms[handle];
    if (!slot || !slot.dirty) return null;
    slot.dirty = false;
    var out = [];
    for (var id in slot.poses) { var p = slot.poses[id]; out.push(id + ',' + p[0] + ',' + p[1] + ',' + p[2] + ',' + p[3]); }
    var s = out.join(';');
    var n = lengthBytesUTF8(s) + 1, buf = _malloc(n);
    stringToUTF8(s, buf, n);
    return buf;
  },

  HawkesNet_Now: function (handle) {
    var slot = (window.__hawkesNet || { rooms: {} }).rooms[handle];
    return slot && slot.net ? slot.net.now() : Date.now();
  },

  HawkesNet_Migrate: function (handle) {
    var slot = (window.__hawkesNet || { rooms: {} }).rooms[handle];
    if (slot && slot.net) try { slot.net.migrate('game asked'); } catch (e) {}
  },

  HawkesNet_Leave: function (handle) {
    var S = window.__hawkesNet || { rooms: {} }, slot = S.rooms[handle];
    if (!slot) return;
    slot.gone = true;
    if (slot.net) try { slot.net.leave(); } catch (e) {}
    delete S.rooms[handle];
  },
});
