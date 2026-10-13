mergeInto(LibraryManager.library, {
  BangConnect: function(urlPtr, namePtr) {
    var name = UTF8ToString(namePtr), url = UTF8ToString(urlPtr);
    try {
      var socket = new WebSocket(url); window.bangSocket = socket;
      socket.onopen = function() { if (window.bangSocket === socket) SendMessage(name, 'OnSocketStatus', 'open'); };
      socket.onmessage = function(e) { if (window.bangSocket === socket) SendMessage(name, 'OnSocketMessage', e.data); };
      socket.onerror = function() { if (window.bangSocket === socket) SendMessage(name, 'OnSocketStatus', 'error:Не удалось подключиться к серверу.'); };
      socket.onclose = function() { if (window.bangSocket === socket) SendMessage(name, 'OnSocketStatus', 'close'); };
    } catch(e) { SendMessage(name, 'OnSocketStatus', 'error:' + e.message); }
  },
  BangSend: function(ptr) { var socket = window.bangSocket; if (socket && socket.readyState === 1) socket.send(UTF8ToString(ptr)); },
  BangClose: function() { var socket = window.bangSocket; window.bangSocket = null; if (socket) socket.close(); }
});
