using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using DustAndPowder;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(Environment.GetEnvironmentVariable("BANG_URL") ?? "http://127.0.0.1:8081");
var app = builder.Build();
var json = new JsonSerializerOptions { IncludeFields = true };
var rooms = new Dictionary<string, Room>();
var gate = new object();
var allowed = (Environment.GetEnvironmentVariable("BANG_ORIGINS") ?? "http://localhost:8080,http://127.0.0.1:8080").Split(',').Select(x => x.Trim()).ToHashSet();
app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(20) });
app.MapGet("/health", () => Results.Json(new { status = "ok", rooms = rooms.Count }));
app.Map("/ws", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest) { context.Response.StatusCode = 400; return; }
    string origin = context.Request.Headers.Origin.ToString();
    if (origin.Length > 0 && !allowed.Contains(origin)) { context.Response.StatusCode = 403; return; }
    using var ws = await context.WebSockets.AcceptWebSocketAsync();
    var client = new Connection(ws, json);
    var sender = client.WriteLoop();
    var buffer = new byte[16384]; var text = new MemoryStream();
    try
    {
        while (ws.State == WebSocketState.Open)
        {
            var result = await ws.ReceiveAsync(buffer, CancellationToken.None);
            if (result.MessageType == WebSocketMessageType.Close) break;
            if (result.MessageType != WebSocketMessageType.Text || text.Length + result.Count > 16384) { await ws.CloseAsync(WebSocketCloseStatus.MessageTooBig, "Invalid command", CancellationToken.None); break; }
            text.Write(buffer, 0, result.Count); if (!result.EndOfMessage) continue;
            Command command;
            try { command = JsonSerializer.Deserialize<Command>(text.ToArray(), json); }
            catch { client.Error("Некорректная команда."); text.SetLength(0); continue; }
            text.SetLength(0);
            lock (gate)
            {
                if (command == null) { client.Error("Пустая команда."); continue; }
                if (!client.AllowCommand()) { client.Error("Слишком много команд."); continue; }
                try { Handle(command, client); } catch (Exception e) { app.Logger.LogWarning(e, "Rejected command"); client.Error("Команда отклонена."); }
            }
        }
    }
    catch (WebSocketException) { }
    catch (OperationCanceledException) { }
    finally
    {
        lock (gate)
        {
            if (client.session != null && client.session.connection == client)
            {
                var s = client.session; s.connection = null; s.disconnected = DateTime.UtcNow;
                var r = s.room; if (r.core != null) r.core.players[s.id].connected = false;
                if (r.core == null && r.owner == s.id)
                { var next = r.sessions.Values.FirstOrDefault(x => x.connection != null); if (next != null) r.owner = next.id; }
                r.Publish();
            }
        }
        client.outgoing.Writer.TryComplete();
        try { await sender; } catch { }
        text.Dispose();
    }
});

void Handle(Command c, Connection client)
{
    if (c.op == "create")
    {
        if (client.session != null) { client.Error("Вы уже в комнате."); return; }
        if (rooms.Count >= 100) { client.Error("Сервер заполнен."); return; }
        if (c.seats < 4 || c.seats > 7 || (c.seconds != 30 && c.seconds != 45 && c.seconds != 60)) { client.Error("Некорректные настройки комнаты."); return; }
        string code; do { code = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(3)); } while (rooms.ContainsKey(code));
        var r = new Room(code, c.seats, c.seconds, c.simple, json); rooms.Add(code, r); Attach(r, client, Name(c.name)); r.owner = client.session.id; r.Publish(); return;
    }
    if (c.op == "join")
    {
        if (client.session != null) { client.Error("Вы уже в комнате."); return; }
        Room r; if (!rooms.TryGetValue((c.code ?? "").Trim().ToUpperInvariant(), out r)) { client.Error("Комната не найдена."); return; }
        if (r.core != null) { client.Error("Матч уже начался."); return; }
        if (r.sessions.Count >= r.capacity) { client.Error("Комната заполнена."); return; }
        Attach(r, client, Name(c.name)); r.Publish(); return;
    }
    if (c.op == "resume")
    {
        Room r; if (!rooms.TryGetValue((c.code ?? "").Trim().ToUpperInvariant(), out r)) { client.Error("Комната закрыта."); return; }
        var s = r.sessions.Values.FirstOrDefault(x => x.token == c.token);
        if (s == null || s.connection != null || DateTime.UtcNow - s.disconnected > TimeSpan.FromSeconds(60)) { client.Error("Время переподключения истекло."); return; }
        client.session = s; s.connection = client;
        if (r.core != null) { r.core.players[s.id].connected = true; r.core.players[s.id].bot = false; }
        r.Publish(); return;
    }
    if (client.session == null) { client.Error("Сначала войдите в комнату."); return; }
    var session = client.session; var room = session.room;
    if (c.seq <= session.lastSeq) { room.Send(session); return; } session.lastSeq = c.seq;
    switch (c.op)
    {
        case "lobbyReady": session.ready = c.ready; room.Publish(); break;
        case "start":
            if (session.id != room.owner || room.core != null) { client.Error("Старт доступен владельцу лобби."); break; }
            if (room.sessions.Values.Any(s => s.connection == null || !s.ready)) { client.Error("Все люди должны подтвердить готовность."); break; }
            room.Start(); room.Publish(); break;
        case "plan":
            if (room.core == null || room.core.phase != MatchPhase.Planning || DateTime.UtcNow >= room.deadline) { client.Error("Планирование завершено."); break; }
            if (c.revision <= session.revision) { room.Send(session); break; }
            string err = room.core.Submit(session.id, c.plan, c.ready);
            if (err.Length > 0) { client.Error(err); break; }
            session.revision = c.revision; room.Publish(); break;
        case "signal":
            if (room.core != null && DateTime.UtcNow - session.lastSignal > TimeSpan.FromSeconds(3)) { room.core.Signal(session.id, c.signal, c.target); session.lastSignal = DateTime.UtcNow; room.core.version++; room.Publish(); } break;
        case "rematch":
            if (session.id != room.owner || room.core == null || room.core.phase != MatchPhase.Finished) { client.Error("Реванш доступен владельцу после матча."); break; }
            room.core = null; foreach (var s in room.sessions.Values) { s.ready = false; s.revision = 0; } room.Publish(); break;
        default: client.Error("Неизвестная команда."); break;
    }
}
void Attach(Room r, Connection client, string name)
{
    int id = Enumerable.Range(0, r.capacity).First(i => !r.sessions.ContainsKey(i));
    var s = new Session { id = id, name = name, token = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24)), room = r, connection = client };
    r.sessions.Add(id, s); client.session = s;
}
string Name(string name) { name = (name ?? "Игрок").Trim(); name = new string(name.Where(c => !char.IsControl(c) && c != '<' && c != '>').Take(20).ToArray()); return name.Length == 0 ? "Игрок" : name; }

var timer = new Timer(_ =>
{
    lock (gate)
    {
        foreach (var room in rooms.Values.ToArray())
        {
            foreach (var s in room.sessions.Values.Where(s => s.connection == null && DateTime.UtcNow - s.disconnected > TimeSpan.FromSeconds(60)).ToArray())
            {
                if (room.core == null) { room.sessions.Remove(s.id); if (room.owner == s.id && room.sessions.Count > 0) room.owner = room.sessions.Values.First().id; }
                else room.core.players[s.id].bot = true;
            }
            if (!room.sessions.Values.Any(s => s.connection != null) && DateTime.UtcNow - room.lastActive > TimeSpan.FromMinutes(2)) { rooms.Remove(room.code); continue; }
            if (room.sessions.Values.Any(s => s.connection != null)) room.lastActive = DateTime.UtcNow;
            try { room.Tick(); } catch (Exception e) { app.Logger.LogError(e, "Room tick {Code}", room.code); }
        }
    }
}, null, 100, 100);
app.Lifetime.ApplicationStopping.Register(() => timer.Dispose());
app.Run();

class Session
{
    public int id, lastSeq, revision; public string name, token; public Room room; public Connection connection;
    public bool ready; public DateTime disconnected, lastSignal;
}
class Connection
{
    public readonly Channel<string> outgoing = Channel.CreateBounded<string>(new BoundedChannelOptions(32) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });
    readonly WebSocket ws; readonly JsonSerializerOptions json; public Session session;
    DateTime rateStart = DateTime.UtcNow; int commandCount;
    public Connection(WebSocket socket, JsonSerializerOptions options) { ws = socket; json = options; }
    public bool AllowCommand() { if (DateTime.UtcNow - rateStart > TimeSpan.FromSeconds(1)) { rateStart = DateTime.UtcNow; commandCount = 0; } return ++commandCount <= 20; }
    public void Send(Envelope e) { outgoing.Writer.TryWrite(JsonSerializer.Serialize(e, json)); }
    public void Error(string text) { Send(new Envelope { type = "error", error = text }); }
    public async Task WriteLoop() { await foreach (string data in outgoing.Reader.ReadAllAsync()) { if (ws.State != WebSocketState.Open) break; await ws.SendAsync(Encoding.UTF8.GetBytes(data), WebSocketMessageType.Text, true, CancellationToken.None); } }
}
class Room
{
    public string code; public int capacity, seconds, owner; public bool simple, cleaned;
    public GameCore core; public DateTime deadline, planningStart, lastActive = DateTime.UtcNow;
    public Dictionary<int, Session> sessions = new Dictionary<int, Session>();
    readonly Dictionary<int, BotBrain> brains = new Dictionary<int, BotBrain>();
    public Room(string code, int count, int time, bool easy, JsonSerializerOptions json) { this.code = code; capacity = count; seconds = time; simple = easy; }
    public void Start()
    {
        uint seed = BitConverter.ToUInt32(System.Security.Cryptography.RandomNumberGenerator.GetBytes(4)); core = new GameCore(capacity, seed);
        foreach (var p in core.players)
        { Session s; if (sessions.TryGetValue(p.id, out s)) { p.name = s.name; p.bot = false; p.connected = s.connection != null; s.revision = 0; }
            else { p.bot = true; p.name = Content.Names[p.character]; } brains[p.id] = new BotBrain((int)(seed & int.MaxValue) + p.id); }
        planningStart = DateTime.UtcNow; deadline = planningStart.AddSeconds(seconds);
    }
    public void Tick()
    {
        if (core == null || core.phase == MatchPhase.Finished) return; var now = DateTime.UtcNow; bool changed = false;
        if (core.phase == MatchPhase.Planning)
        {
            foreach (var p in core.players.Where(p => p.alive && !p.ready && (p.bot || !p.connected && now >= deadline)).ToArray())
            { if (now - planningStart < TimeSpan.FromSeconds(1 + p.id * .12)) continue; core.Submit(p.id, brains[p.id].Choose(core.View(p.id), simple), true); changed = true; }
            if (now >= deadline || core.players.Where(p => p.alive).All(p => p.ready))
            {
                core.LockPlans(); core.ResolveBeat(); foreach (var p in core.players) brains[p.id].Observe(core.View(p.id)); deadline = now.AddSeconds(3.5); changed = true;
            }
        }
        else if (now >= deadline)
        {
            if (core.beat < 2) { core.ResolveBeat(); foreach (var p in core.players) brains[p.id].Observe(core.View(p.id)); deadline = now.AddSeconds(3.5); }
            else if (!cleaned) { core.FinishRound(); cleaned = true; deadline = now.AddSeconds(1); }
            else { core.BeginRound(); cleaned = false; foreach (var s in sessions.Values) s.revision = 0; planningStart = now; deadline = now.AddSeconds(seconds); }
            changed = true;
        }
        if (changed) Publish();
    }
    public void Publish() { foreach (var s in sessions.Values) Send(s); }
    public void Send(Session s)
    {
        if (s.connection == null) return;
        MatchView v;
        if (core != null) v = core.View(s.id);
        else v = new MatchView { phase = MatchPhase.Lobby, localId = s.id, players = Enumerable.Range(0, capacity).Select(id =>
        { Session other; return sessions.TryGetValue(id, out other) ? new PlayerView { id = id, name = other.name, ready = other.ready, connected = other.connection != null, alive = true } : new PlayerView { id = id, name = "Бот", bot = true, ready = true, connected = true, alive = true }; }).ToArray() };
        v.room = code; v.owner = owner; v.capacity = capacity; v.timer = seconds; v.remaining = Math.Max(0, (deadline - DateTime.UtcNow).TotalSeconds);
        s.connection.Send(new Envelope { type = "state", token = s.token, code = code, ack = s.lastSeq, revision = s.revision, view = v });
    }
}
