using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DustAndPowder
{
    public class BangGame : MonoBehaviour
    {
        const float W = 1440, H = 900;
        GameCore game; MatchView view; Plan draft = new Plan(); NetworkClient net;
        readonly Dictionary<int, BotBrain> brains = new Dictionary<int, BotBrain>();
        readonly List<string> journal = new List<string>();
        readonly HashSet<string> journalKeys = new HashSet<string>();
        readonly Dictionary<int, CardKind> rememberedCards = new Dictionary<int, CardKind>();
        GUIStyle label, small, title, button, input, centered; Texture2D disk;
        AudioSource audioSource; AudioClip shotSound, healSound, clickSound;
        float volume = .45f, receiveTime, deadline, nextStep, animationStart, reconnectAt, connectStart, lastSignal = -10;
        int seats = 5, timer = 45, slot, selected = -1, selectedTarget = -1, seq, revision, seenRound = -1, seenVersion = -1;
        bool online, simple, cleaned, showHelp, showSettings, lobbyReady, connecting, reduceMotion;
        string playerName = "Странник", code = "", server = "ws://127.0.0.1:8081/ws", sessionToken = "", error = "", pending = "";
        Vector2 handScroll, logScroll; float scale; Vector2 offset;
        Color ink = new Color(.94f, .88f, .72f), muted = new Color(.62f, .65f, .63f), gold = new Color(.93f, .64f, .28f), panel = new Color(.09f, .14f, .16f);
        static readonly Color[] coats = { new Color(.72f,.26f,.23f), new Color(.28f,.51f,.46f), new Color(.33f,.38f,.48f), new Color(.62f,.4f,.27f), new Color(.46f,.42f,.28f), new Color(.28f,.46f,.63f), new Color(.48f,.29f,.45f), new Color(.65f,.48f,.19f) };
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() { if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "OneTurn" && FindFirstObjectByType<BangGame>() == null) new GameObject("BangGame").AddComponent<BangGame>(); }
        void Awake()
        {
            Application.targetFrameRate = 60; Application.runInBackground = true;
            net = new GameObject("BangNetwork").AddComponent<NetworkClient>(); net.Received = Receive; net.Status = SocketStatus;
            audioSource = gameObject.AddComponent<AudioSource>();
            volume = PlayerPrefs.GetFloat("volume", .45f); playerName = PlayerPrefs.GetString("name", "Странник"); server = PlayerPrefs.GetString("server", server);
            reduceMotion = PlayerPrefs.GetInt("reduceMotion", 0) == 1;
            disk = new Texture2D(64, 64, TextureFormat.RGBA32, false); var pixels = new Color[4096];
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++) pixels[y * 64 + x] = new Color(1,1,1, Mathf.Clamp01((31 - Vector2.Distance(new Vector2(x,y), new Vector2(31.5f,31.5f))) * 2));
            disk.SetPixels(pixels); disk.Apply(); shotSound = Sound(.16f, 85, true); healSound = Sound(.35f, 550, false); clickSound = Sound(.065f, 270, false);
            var args = Environment.GetCommandLineArgs(); if (args.Contains("--demo")) StartLocal();
        }
        AudioClip Sound(float duration, float frequency, bool noise)
        {
            int count = (int)(22050 * duration); var values = new float[count]; var rng = new System.Random(43);
            for (int i = 0; i < count; i++) { float t = i / 22050f, fade = Mathf.Pow(1 - t / duration, 3); values[i] = (noise ? ((float)rng.NextDouble() * 2 - 1) * .65f + Mathf.Sin(t * frequency * 6.283f) * .35f : Mathf.Sin(t * frequency * 6.283f)) * fade * .4f; }
            var clip = AudioClip.Create("Original procedural sound", count, 1, 22050, false); clip.SetData(values, 0); return clip;
        }
        void Play(AudioClip clip) { if (volume > 0) audioSource.PlayOneShot(clip, volume); }
        void InitStyles()
        {
            var font = Resources.Load<Font>("BangFont") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label = new GUIStyle(GUI.skin.label) { font = font, fontSize = 18, wordWrap = true }; label.normal.textColor = ink;
            small = new GUIStyle(label) { fontSize = 14 }; small.normal.textColor = muted;
            title = new GUIStyle(label) { fontSize = 36, fontStyle = FontStyle.Bold };
            centered = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter };
            button = new GUIStyle(centered) { fontSize = 17, fontStyle = FontStyle.Bold, wordWrap = false };
            input = new GUIStyle(GUI.skin.textField) { font = font, fontSize = 19, padding = new RectOffset(12,12,10,10) }; input.normal.textColor = ink; input.focused.textColor = ink;
        }
        void Update()
        {
            if (online)
            {
                if (connecting && Time.realtimeSinceStartup - connectStart > 12) { connecting = false; net.Close(); error = "Сервер не отвечает. Проверьте адрес и запуск сервера."; }
                if (!net.Connected && !connecting && sessionToken.Length > 0 && Time.realtimeSinceStartup >= reconnectAt)
                { reconnectAt = Time.realtimeSinceStartup + 5; Connect("resume"); }
                return;
            }
            if (game == null || game.phase == MatchPhase.Finished) return;
            float now = Time.realtimeSinceStartup;
            if (game.phase == MatchPhase.Planning)
            {
                foreach (var p in game.players.Where(p => p.alive && !p.ready && (p.bot || !game.players[0].alive)).ToArray())
                { if (now < deadline - timer + 1 + p.id * .12f) continue; game.Submit(p.id, brains[p.id].Choose(game.View(p.id), simple), true); }
                if (now >= deadline || game.players.Where(p => p.alive).All(p => p.ready))
                { game.LockPlans(); game.ResolveBeat(); animationStart = now; nextStep = now + 3.5f; ObserveAndLog(); }
            }
            else if (now >= nextStep)
            {
                if (game.beat < 2) { game.ResolveBeat(); animationStart = now; nextStep = now + 3.5f; ObserveAndLog(); }
                else if (!cleaned) { game.FinishRound(); cleaned = true; nextStep = now + 1; ObserveAndLog(); }
                else { game.BeginRound(); cleaned = false; deadline = now + timer; draft = new Plan(); selected = -1; revision = 0; ObserveAndLog(); }
            }
            view = game.View(0); view.remaining = Mathf.Max(0, deadline - now);
        }
        void StartLocal()
        {
            Leave(); game = new GameCore(seats, (uint)Environment.TickCount, Name());
            foreach (var p in game.players) brains[p.id] = new BotBrain(Environment.TickCount + p.id * 13);
            deadline = Time.realtimeSinceStartup + timer; draft = new Plan(); view = game.View(0); ObserveAndLog(); Play(clickSound);
        }
        string Name() { var name = (playerName ?? "").Trim(); if (name.Length == 0) name = "Странник"; return name.Substring(0, Math.Min(20, name.Length)); }
        void ObserveAndLog() { foreach (var p in game.players) brains[p.id].Observe(game.View(p.id)); Log(game.View(0)); }
        void Log(MatchView v)
        {
            var own = v.players.FirstOrDefault(p => p.id == v.localId);
            if (own != null) foreach (var card in own.hand) rememberedCards[card.id] = card.kind;
            for (int i = 0; i < v.events.Length; i++)
            { string key = v.round + ":" + v.beat + ":" + i + ":" + v.events[i].kind + ":" + v.events[i].text; if (journalKeys.Add(key)) journal.Add(v.events[i].text); }
            while (journal.Count > 150) journal.RemoveAt(0);
            if (v.events.Any(e => e.kind == "shot") && v.version != seenVersion) Play(shotSound);
            else if (v.events.Any(e => e.kind == "heal") && v.version != seenVersion) Play(healSound);
            seenVersion = v.version; logScroll.y = 100000;
        }
        void Connect(string op)
        {
            pending = op; online = true; connecting = true; connectStart = Time.realtimeSinceStartup; error = ""; net.Connect(server);
            PlayerPrefs.SetString("name", Name()); PlayerPrefs.SetString("server", server); PlayerPrefs.Save();
        }
        void SocketStatus(string status)
        {
            if (status == "open")
            { connecting = false; Send(new Command { op = pending, name = Name(), code = code, token = sessionToken, seats = seats, seconds = timer, simple = simple }); }
            else if (status == "close" || status.StartsWith("error:"))
            { connecting = false; error = status.StartsWith("error:") ? status.Substring(6) : "Связь прервана. Пытаемся переподключиться…"; reconnectAt = Time.realtimeSinceStartup + 3; }
        }
        void Receive(Envelope e)
        {
            if (e.type == "error") { error = e.error; if (e.error.Contains("переподключения") || e.error.Contains("закрыта")) sessionToken = ""; return; }
            if (e.view == null) return;
            code = e.code; sessionToken = e.token; error = ""; receiveTime = Time.realtimeSinceStartup;
            bool newBeat = view == null || view.round != e.view.round || view.beat != e.view.beat || view.phase != e.view.phase;
            bool newRound = view == null || view.round != e.view.round;
            view = e.view;
            if (newRound || pending == "resume") { draft = view.myPlan.Copy(); revision = e.revision; selected = -1; pending = ""; }
            revision = Math.Max(revision, e.revision);
            if (newBeat && view.phase == MatchPhase.Resolving) animationStart = Time.realtimeSinceStartup;
            if (view.phase == MatchPhase.Lobby) lobbyReady = view.players.First(p => p.id == view.localId).ready;
            Log(view);
        }
        void Send(Command c) { c.seq = ++seq; net.Send(c); }
        void SaveDraft(bool ready)
        {
            if (view == null) return; string validation = GameCore.Validate(view, draft);
            if (validation.Length > 0) { error = validation; return; } error = "";
            if (online) Send(new Command { op = "plan", plan = draft.Copy(), ready = ready, revision = ++revision });
            else { error = game.Submit(0, draft, ready); view = game.View(0); }
        }
        void Leave()
        {
            if (net != null) net.Close(); game = null; view = null; online = false; connecting = false; sessionToken = ""; pending = ""; brains.Clear(); journal.Clear(); journalKeys.Clear(); rememberedCards.Clear();
            draft = new Plan(); selected = selectedTarget = -1; error = ""; cleaned = false; seenRound = seenVersion = -1; seq = revision = 0; lobbyReady = false;
        }
        void OnGUI()
        {
            if (label == null) InitStyles();
            scale = Mathf.Min(Screen.width / W, Screen.height / H); offset = new Vector2((Screen.width - W * scale) / 2, (Screen.height - H * scale) / 2);
            GUI.matrix = Matrix4x4.TRS(offset, Quaternion.identity, Vector3.one * scale);
            Box(new Rect(0,0,W,H), new Color(.055f,.09f,.11f));
            if (view == null) Menu(); else if (view.phase == MatchPhase.Lobby) Lobby(); else Match();
            if (showHelp) Help(); if (showSettings) Settings();
            GUI.matrix = Matrix4x4.identity;
        }
        void Box(Rect r, Color c) { Color old = GUI.color; GUI.color = c; GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = old; }
        void Circle(Vector2 p, float radius, Color c) { Color old = GUI.color; GUI.color = c; GUI.DrawTexture(new Rect(p.x-radius,p.y-radius,radius*2,radius*2), disk); GUI.color = old; }
        void Line(Vector2 a, Vector2 b, Color c, float width = 3)
        {
            Matrix4x4 old = GUI.matrix; Vector2 d = b-a; GUIUtility.RotateAroundPivot(Mathf.Atan2(d.y,d.x)*Mathf.Rad2Deg,a); Box(new Rect(a.x,a.y-width/2,d.magnitude,width),c); GUI.matrix = old;
        }
        void Text(Rect r, string text, GUIStyle style = null, Color? color = null)
        { style = style ?? label; Color old = style.normal.textColor; if (color.HasValue) style.normal.textColor = color.Value; GUI.Label(r,text,style); style.normal.textColor = old; }
        bool Button(Rect r, string text, bool enabled = true, bool primary = false, int fontSize = 17)
        {
            var mouse = (Event.current.mousePosition); bool hover = r.Contains(mouse);
            Box(r, !enabled ? new Color(.15f,.2f,.21f) : primary ? (hover ? new Color(.98f,.74f,.39f) : gold) : hover ? new Color(.23f,.31f,.32f) : new Color(.16f,.23f,.25f));
            bool old = GUI.enabled; GUI.enabled = enabled; Color color = button.normal.textColor; button.normal.textColor = primary && enabled ? new Color(.11f,.13f,.13f) : enabled ? ink : muted;
            int oldSize=button.fontSize;button.fontSize=fontSize;
            bool result = GUI.Button(r,text,button); button.fontSize=oldSize; button.normal.textColor = color; GUI.enabled = old; if (result) Play(clickSound); return result;
        }
        void Panel(Rect r, string heading = "") { Box(new Rect(r.x,r.y+4,r.width,r.height),new Color(0,0,0,.18f)); Box(r,panel); Box(new Rect(r.x,r.y,r.width,2),new Color(.28f,.34f,.33f)); if (heading.Length>0) Text(new Rect(r.x+18,r.y+13,r.width-36,25),heading,small,gold); }
        void Menu()
        {
            Desert(new Rect(0,0,760,H)); Text(new Rect(72,82,620,36),"ОДНОВРЕМЕННАЯ ПЕРЕСТРЕЛКА",small,gold);
            Text(new Rect(68,125,630,180),"ПЫЛЬ\nИ ПОРОХ",new GUIStyle(title){fontSize=68});
            Text(new Rect(74,307,530,90),"Скрытые роли. Два тайных действия.\nОдин момент, чтобы изменить всё.",new GUIStyle(label){fontSize=24});
            for (int i=0;i<3;i++) Avatar(new Vector2(200+i*190,615),i==0?0:i==1?2:5,true,0);
            Text(new Rect(74,775,590,60),"Выберите карты, приготовьте уклонение и наблюдайте,\nкак планы всех игроков сталкиваются на экране.",label);
            Panel(new Rect(810,72,550,754),"НОВАЯ ПАРТИЯ");
            Text(new Rect(838,117,470,28),"Ваше имя",small); playerName = GUI.TextField(new Rect(838,148,494,44),playerName,20,input);
            Text(new Rect(838,215,470,28),"Участников: "+seats+" · свободные места займут боты",small);
            for(int i=4;i<=7;i++) if(Button(new Rect(838+(i-4)*125,246,119,42),i.ToString(),true,seats==i)) seats=i;
            Text(new Rect(838,310,470,28),"Время планирования",small);
            int[] times={30,45,60}; for(int i=0;i<3;i++) if(Button(new Rect(838+i*168,339,158,40),times[i]+" сек",true,timer==times[i])) timer=times[i];
            if(Button(new Rect(838,394,494,38),"Боты: "+(simple?"простые":"обычные"))) simple=!simple;
            if(Button(new Rect(838,452,494,54),"ИГРАТЬ С БОТАМИ",true,true)) StartLocal();
            Text(new Rect(838,534,470,25),"МУЛЬТИПЛЕЕР · адрес сервера",small,gold);
            server=GUI.TextField(new Rect(838,562,494,40),server,240,input);
            if(Button(new Rect(838,617,238,46),connecting?"Подключение…":"Создать комнату",!connecting)) Connect("create");
            code=GUI.TextField(new Rect(1090,617,107,46),code.ToUpperInvariant(),6,input);
            if(Button(new Rect(1207,617,125,46),"Войти",!connecting&&code.Length>0)) Connect("join");
            Text(new Rect(838,678,494,67),error.Length>0?error:"Для онлайна запустите Server/start-server.ps1.\nВ одиночной игре подключение не требуется.",small,error.Length>0?new Color(.96f,.46f,.35f):muted);
            if(Button(new Rect(838,759,238,40),"Как играть")) showHelp=true;
            if(Button(new Rect(1090,759,242,40),"Настройки")) showSettings=true;
            Text(new Rect(30,864,1300,22),"Ранняя версия · оригинальные персонажи · Unity · 4–7 участников",small);
        }
        void Desert(Rect r)
        {
            Box(r,new Color(.11f,.2f,.24f)); Circle(new Vector2(r.x+r.width*.75f,r.y+r.height*.27f),80,new Color(.85f,.54f,.29f));
            for(int i=0;i<12;i++) Box(new Rect(r.x,r.y+r.height*(.42f+i*.025f),r.width,r.height*.035f),new Color(.24f+i*.008f,.27f-i*.006f,.24f-i*.005f));
            Box(new Rect(r.x,r.y+r.height*.72f,r.width,r.height*.28f),new Color(.18f,.2f,.2f));
            for(int i=0;i<8;i++){float x=r.x+20+i*100; Box(new Rect(x,r.y+r.height*.47f,74,r.height*.27f),new Color(.13f,.2f,.21f)); Box(new Rect(x-5,r.y+r.height*.465f,84,10),new Color(.1f,.17f,.19f)); Box(new Rect(x+21,r.y+r.height*.59f,30,r.height*.15f),new Color(.09f,.15f,.17f));}
            Text(new Rect(r.x+100,r.y+r.height*.5f,r.width-200,40),"S A L O O N",new GUIStyle(centered){fontSize=25},new Color(.54f,.47f,.35f));
        }
        void Lobby()
        {
            Desert(new Rect(0,0,W,H)); Panel(new Rect(310,70,820,740),"ПРИВАТНАЯ КОМНАТА");
            Text(new Rect(348,121,650,50),"Код: "+code,title,gold);
            Text(new Rect(348,180,650,30),"Пригласите друзей по коду. Остальные места займут боты.",small);
            for(int i=0;i<view.players.Length;i++)
            {var p=view.players[i];float y=230+i*59;Box(new Rect(348,y,744,50),new Color(.13f,.2f,.22f));Text(new Rect(368,y+12,440,30),p.name+(p.id==view.localId?" · вы":"")+(p.id==view.owner?" · владелец":""));Text(new Rect(830,y+12,240,30),p.bot?"БОТ":!p.connected?"НЕТ СВЯЗИ":p.ready?"ГОТОВ":"ПЛАНИРУЕТ",small,p.ready?gold:muted);}
            if(Button(new Rect(348,677,350,50),lobbyReady?"Я не готов":"Я готов",net.Connected,true)) Send(new Command{op="lobbyReady",ready=!lobbyReady});
            if(Button(new Rect(714,677,378,50),"НАЧАТЬ МАТЧ",view.owner==view.localId&&view.players.All(p=>p.ready&&p.connected),true)) Send(new Command{op="start"});
            if(Button(new Rect(348,745,180,40),"В меню")) Leave();
            Text(new Rect(555,745,530,40),error,small,new Color(.96f,.46f,.35f));
        }
        Vector2 Position(int id)
        {
            if(view.players.Length==7)
            {Vector2[] seats7={new Vector2(525,458),new Vector2(305,458),new Vector2(145,305),new Vector2(335,205),new Vector2(715,205),new Vector2(905,305),new Vector2(745,458)};return seats7[id];}
            int n=view.players.Length;float a=Mathf.PI/2+id*2*Mathf.PI/n;return new Vector2(525+Mathf.Cos(a)*400,330+Mathf.Sin(a)*142);
        }
        void Match()
        {
            var me=view.players.First(p=>p.id==view.localId); bool planning=view.phase==MatchPhase.Planning; bool canPlan=planning&&me.alive&&!me.ready&&(!online||net.Connected);
            Text(new Rect(24,18,600,48),"ПЫЛЬ И ПОРОХ",new GUIStyle(title){fontSize=28});
            Text(new Rect(26, sixty(),690,25),"РАУНД "+view.round+"  /  "+(view.phase==MatchPhase.Finished?"МАТЧ ЗАВЕРШЁН":planning?"ТАЙНОЕ ПЛАНИРОВАНИЕ":"ОБЩИЙ ТАКТ "+view.beat),small,gold);
            float remaining=online?Mathf.Max(0,(float)view.remaining-(Time.realtimeSinceStartup-receiveTime)):Mathf.Max(0,deadline-Time.realtimeSinceStartup);
            if(planning) Text(new Rect(800,22,245,50),Mathf.CeilToInt(remaining)+" сек",new GUIStyle(title){alignment=TextAnchor.MiddleRight},gold);
            if(Button(new Rect(1070,25,110,40),"Правила"))showHelp=true;
            if(Button(new Rect(1192,25,110,40),"Настройки"))showSettings=true;
            if(Button(new Rect(1314,25,106,40),"В меню"))Leave();
            if(view==null)return;
            Panel(new Rect(20,103,1020,475)); Desert(new Rect(22,105,1016,471));
            Box(new Rect(24,449,1012,125),new Color(.17f,.19f,.18f));
            for(int i=0;i<10;i++)Line(new Vector2(24,458+i*13),new Vector2(1036,458+i*13),new Color(.24f,.25f,.21f),1);
            for(int i=0;i<view.players.Length;i++) DrawPlayer(view.players[i],canPlan);
            if(view.phase==MatchPhase.Resolving || view.phase==MatchPhase.Finished) Effects();
            if(planning)
            {Text(new Rect(347,326,355,27),(me.ready?"ПЛАН ПОДТВЕРЖДЁН · ":"ГОТОВЫ · ")+view.players.Count(p=>p.alive&&p.ready)+" / "+view.players.Count(p=>p.alive),new GUIStyle(centered){fontSize=18},gold);}
            Planner(me,canPlan);
            Panel(new Rect(20,592,1020,84)); Text(new Rect(40,607,205,30),Content.RoleName(me.role).ToUpperInvariant(),new GUIStyle(label){fontStyle=FontStyle.Bold},gold);Text(new Rect(244,607,775,30),Content.Goal(me.role),label);
            Text(new Rect(40,642,700,24),Content.Names[me.character]+": "+Content.Abilities[me.character]+" · дальность "+me.weaponRange+" · рука "+(me.character==3?8:6),small);
            string[] signals={"Защита","Подозрение","Помощь"};
            for(int i=0;i<3;i++)if(Button(new Rect(742+i*95,637,90,30),signals[i],planning&&me.alive&&Time.realtimeSinceStartup-lastSignal>3,fontSize:13))
            {lastSignal=Time.realtimeSinceStartup;if(online)Send(new Command{op="signal",signal=i.ToString(),target=selectedTarget});else{game.Signal(0,i.ToString(),selectedTarget);ObserveAndLog();}}
            Panel(new Rect(20,693,1020,187),"ВАША РУКА · выберите слот, карту и цель · ПКМ по карте: приоритет сброса");
            DrawHand(me,canPlan); Journal();
            if(view.phase==MatchPhase.Finished&&Time.realtimeSinceStartup-animationStart>2.8f) Results();
        }
        float sixty(){return 61;}
        void DrawPlayer(PlayerView p,bool canPlan)
        {
            Vector2 pos=Position(p.id); float elapsed=Time.realtimeSinceStartup-animationStart;
            var relevant=view.events.Where(e=>e.target==p.id).ToArray();float wobble=!reduceMotion&&elapsed<.7f&&relevant.Any(e=>e.kind=="damage")?Mathf.Sin(elapsed*65)*5:0;
            bool targetable=false;
            if(canPlan&&selected>=0)
            {var me=view.players.First(x=>x.id==view.localId);var c=me.hand.FirstOrDefault(x=>x.id==selected);if(c!=null&&(c.kind==CardKind.Shot||c.kind==CardKind.Disarm||c.kind==CardKind.Theft)){var test=draft.Copy();test.actions[slot]=new ActionOrder{card=c.id,target=p.id};targetable=GameCore.Validate(view,test).Length==0;}}
            if(targetable) { Circle(pos+new Vector2(0,-25),58,new Color(.89f,.62f,.25f,.23f)); if(GUI.Button(new Rect(pos.x-63,pos.y-91,126,174),"",GUIStyle.none)){selectedTarget=p.id;Assign(selected,p.id);}}
            else if(canPlan&&selected<0&&p.alive&&p.id!=view.localId&&GUI.Button(new Rect(pos.x-63,pos.y-91,126,174),"",GUIStyle.none))selectedTarget=p.id;
            if(selectedTarget==p.id)Line(pos+new Vector2(-38,100),pos+new Vector2(38,100),gold,2);
            bool active=view.phase==MatchPhase.Resolving&&elapsed>.18f&&elapsed<1.1f;
            int pose=active&&view.events.Any(e=>e.source==p.id&&e.kind=="shot")?1:active&&relevant.Any(e=>e.kind=="heal")?2:0;
            float dodge=active&&relevant.Any(e=>e.kind=="dodge")&&!reduceMotion?Mathf.Sin((elapsed-.18f)/.92f*Mathf.PI)*13:0;
            Avatar(pos+new Vector2(wobble+dodge,0),p.character,p.alive,p.ready?1:0,pose);
            Text(new Rect(pos.x-94,pos.y-104,188,25),p.role==Role.Sheriff?"ШЕРИФ":!p.alive?Content.RoleName(p.role).ToUpperInvariant():p.id==view.localId?"ВЫ":p.bot?"БОТ":!p.connected?"НЕТ СВЯЗИ":"",new GUIStyle(centered){fontSize=13},p.role==Role.Sheriff?gold:muted);
            Text(new Rect(pos.x-101,pos.y+37,202,27),p.name,new GUIStyle(centered){fontSize=16,fontStyle=FontStyle.Bold},p.alive?ink:muted);
            float start=pos.x-p.maxHp*6;for(int i=0;i<p.maxHp;i++)Box(new Rect(start+i*12,pos.y+68,9,9),i<p.hp?new Color(.89f,.47f,.3f):new Color(.24f,.29f,.28f));
            Text(new Rect(pos.x-96,pos.y+84,192,23),p.alive?"Карт: "+p.cards+" · дальность "+p.weaponRange+(p.armor?" · броня":""):"ВЫБЫЛ",new GUIStyle(centered){fontSize=12},muted);
            if(targetable)Text(new Rect(pos.x-30,pos.y+108,60,24),"d="+GameCore.Distance(view.players,view.localId,p.id),new GUIStyle(centered){fontSize=13},gold);
        }
        void Avatar(Vector2 p,int character,bool alive,int ready,int pose=0)
        {
            Color coat=alive?coats[character]:new Color(.29f,.31f,.3f),skin=alive?new Color(.78f,.54f,.35f):new Color(.4f,.42f,.4f);
            Circle(p+new Vector2(0,28),43,new Color(0,0,0,.23f));
            Box(new Rect(p.x-27,p.y-2,54,33),new Color(.1f,.13f,.13f));
            Box(new Rect(p.x-30,p.y-15,60,39),coat);Box(new Rect(p.x-38,p.y-10,12,35),coat);Box(new Rect(p.x+26,p.y-10,12,35),coat);
            Box(new Rect(p.x-12,p.y-15,24,39),new Color(.15f,.2f,.21f));Box(new Rect(p.x-2,p.y-9,4,30),new Color(.7f,.58f,.35f));
            Circle(p+new Vector2(0,-36),24,skin);Box(new Rect(p.x-20,p.y-50,40,12),new Color(.22f,.18f,.16f));
            Box(new Rect(p.x-4,p.y-24,8,12),skin);
            Color hat=character%3==0?new Color(.65f,.44f,.24f):character%3==1?new Color(.23f,.29f,.28f):new Color(.4f,.31f,.22f);
            Box(new Rect(p.x-39,p.y-58,78,9),hat);Box(new Rect(p.x-24,p.y-84,48,28),hat);Box(new Rect(p.x-24,p.y-65,48,6),coat);
            Circle(p+new Vector2(-8,-37),2,new Color(.08f,.11f,.12f));Circle(p+new Vector2(8,-37),2,new Color(.08f,.11f,.12f));
            if(character==1){Line(p+new Vector2(-16,-39),p+new Vector2(16,-39),ink,1);Circle(p+new Vector2(-8,-39),7,new Color(.72f,.76f,.7f,.4f));Circle(p+new Vector2(8,-39),7,new Color(.72f,.76f,.7f,.4f));}
            if(character==2||character==4||character==6)Box(new Rect(p.x-11,p.y-28,22,4),new Color(.2f,.17f,.14f));
            if(character==0||character==3||character==5||character==7){Box(new Rect(p.x-23,p.y-41,5,23),new Color(.27f,.18f,.14f));Box(new Rect(p.x+18,p.y-41,5,23),new Color(.27f,.18f,.14f));}
            Box(new Rect(p.x-29,p.y+13,58,6),new Color(.3f,.21f,.16f));Box(new Rect(p.x-5,p.y+12,10,8),gold);
            float arm=pose==1?-18:pose==2?-10:2;
            if(pose>0)Box(new Rect(p.x+27,p.y+arm+2,20,9),coat);
            Box(new Rect(p.x+32,p.y+arm,23,5),new Color(.57f,.62f,.57f));Box(new Rect(p.x+35,p.y+arm+3,6,14),new Color(.3f,.24f,.2f));
            if(pose==1)Circle(p+new Vector2(57,arm+2),5,new Color(.97f,.72f,.29f));
            if(pose==2){Box(new Rect(p.x+46,p.y+arm-10,9,17),new Color(.35f,.68f,.5f));Box(new Rect(p.x+48,p.y+arm-14,5,6),gold);}
            if(ready==1)Circle(p+new Vector2(51,-58),6,gold);
            if(!alive){Line(p+new Vector2(-18,-52),p+new Vector2(18,-16),muted,3);Line(p+new Vector2(18,-52),p+new Vector2(-18,-16),muted,3);}
        }
        void Effects()
        {
            float time=Time.realtimeSinceStartup-animationStart;
            if(time>3.5f)return;
            foreach(var e in view.events)
            {
                if(e.kind=="shot"&&e.source>=0&&e.target>=0)
                {var a=Position(e.source)+new Vector2(0,-20);var b=Position(e.target)+new Vector2(0,-20);float t=Mathf.Clamp01((time-.2f)/.42f);if(time>.2f&&time<.7f){Vector2 point=Vector2.Lerp(a,b,t);Line(point,Vector2.Lerp(a,b,Mathf.Clamp01(t-.13f)),gold,3);Circle(a,6,new Color(1,.81f,.4f));}}
                if(e.target>=0&&(e.kind=="damage"||e.kind=="heal"||e.kind=="dodge"||e.kind=="block")&&time>.6f)
                {var p=Position(e.target);string text=e.kind=="damage"?"−"+e.value:e.kind=="heal"?"+"+e.value:e.kind=="dodge"?"УКЛОНЕНИЕ":"БРОНЯ";Text(new Rect(p.x-82,p.y-135-Mathf.Min(time*6,18),164,30),text,new GUIStyle(centered){fontSize=19,fontStyle=FontStyle.Bold},e.kind=="damage"?new Color(1,.44f,.32f):e.kind=="heal"?new Color(.4f,.85f,.65f):gold);}
            }
        }
        void Planner(PlayerView me,bool enabled)
        {
            Panel(new Rect(1060,103,360,475),"ВАШ ТАЙНЫЙ ПЛАН");
            for(int i=0;i<2;i++)
            {
                Rect r=new Rect(1078,148+i*84,324,72);Box(r,slot==i?new Color(.27f,.3f,.26f):new Color(.13f,.21f,.23f));
                var a=draft.actions[i];var c=me.hand.FirstOrDefault(x=>x.id==a.card);
                if(c==null&&rememberedCards.TryGetValue(a.card,out var oldKind))c=new Card{id=a.card,kind=oldKind};
                string text=c==null?"Ожидание":Content.CardNames[(int)c.kind];var target=view.players.FirstOrDefault(x=>x.id==a.target);
                if(c!=null&&(c.kind==CardKind.Shot||c.kind==CardKind.Theft||c.kind==CardKind.Disarm)&&target!=null)text+=" → "+target.name;
                Text(new Rect(r.x+12,r.y+8,r.width-52,22),"ТАКТ "+(i+1),small,gold);Text(new Rect(r.x+12,r.y+33,r.width-52,27),text,new GUIStyle(label){fontSize=16});
                if(enabled&&GUI.Button(new Rect(r.x,r.y,r.width-36,r.height),"",GUIStyle.none)){slot=i;selected=-1;}
                if(Button(new Rect(r.x+r.width-32,r.y+22,24,27),"×",enabled)){draft.actions[i]=new ActionOrder();SaveDraft(false);}
            }
            Text(new Rect(1078,330,322,25),"РЕЗЕРВ · до двух уклонений",small,gold);
            for(int i=0;i<2;i++)
            {bool filled=i<draft.dodges.Length;Rect r=new Rect(1078+i*166,360,158,45);if(Button(r,filled?"Уклонение ×":"Пусто",enabled&&filled)){draft.dodges=draft.dodges.Where((id,index)=>index!=i).ToArray();SaveDraft(false);}}
            string instruction=!me.alive?"Вы выбыли и наблюдаете за матчем.":view.phase!=MatchPhase.Planning?"Все действия исполняются одновременно.":me.ready?"План заблокирован. Ждём остальных.":selected>=0?"Выберите подсвеченного персонажа.":"Выберите слот, затем карту в руке. Защиту добавьте в резерв.";
            Text(new Rect(1078,423,324,57),instruction,small);
            if(Button(new Rect(1078,494,324,51),me.ready?"ГОТОВ":!me.alive?"НАБЛЮДЕНИЕ":"ПОДТВЕРДИТЬ ПЛАН",enabled,true)){selected=-1;SaveDraft(true);}
        }
        void Assign(int id,int target)
        {
            var old=draft.actions[slot];draft.actions[slot]=new ActionOrder{card=id,target=target};string validation=GameCore.Validate(view,draft);
            if(validation.Length>0){draft.actions[slot]=old;error=validation;return;}selected=-1;SaveDraft(false);
        }
        void DrawHand(PlayerView me,bool enabled)
        {
            float width=Mathf.Max(979,me.hand.Length*157);handScroll=GUI.BeginScrollView(new Rect(34,732,992,143),handScroll,new Rect(0,0,width,126));
            for(int i=0;i<me.hand.Length;i++)
            {
                var c=me.hand[i];bool used=draft.actions.Any(a=>a.card==c.id)||draft.dodges.Contains(c.id);Rect r=new Rect(i*157,0,145,126);
                Box(r,selected==c.id?gold:used?new Color(.25f,.35f,.3f):new Color(.81f,.75f,.59f));
                Text(new Rect(r.x+10,7,125,23),Content.CardNames[(int)c.kind],new GUIStyle(label){fontSize=15,fontStyle=FontStyle.Bold},new Color(.12f,.19f,.21f));
                CardIcon(new Vector2(r.x+72,48),c.kind,new Color(.2f,.28f,.27f));
                int discardIndex=Array.IndexOf(draft.discard,c.id);
                Text(new Rect(r.x+9,72,127,51),used?"В ПЛАНЕ":discardIndex>=0?"Сброс: приоритет "+(discardIndex+1):Content.CardText[(int)c.kind],new GUIStyle(label){fontSize=11},new Color(.15f,.21f,.22f));
                if(enabled&&Event.current.type==EventType.MouseDown&&Event.current.button==1&&r.Contains(Event.current.mousePosition))
                {draft.discard=discardIndex>=0?draft.discard.Where(id=>id!=c.id).ToArray():draft.discard.Concat(new[]{c.id}).ToArray();SaveDraft(false);Event.current.Use();}
                if(enabled&&!used&&GUI.Button(r,"",GUIStyle.none))
                {
                    error="";Play(clickSound);
                    if(c.kind==CardKind.Dodge){if(draft.dodges.Length<2){draft.dodges=draft.dodges.Concat(new[]{c.id}).ToArray();SaveDraft(false);}else error="В резерве уже два уклонения.";}
                    else if(c.kind==CardKind.Shot||c.kind==CardKind.Disarm||c.kind==CardKind.Theft){selected=c.id;}
                    else Assign(c.id,me.id);
                }
            }
            GUI.EndScrollView();
        }
        void CardIcon(Vector2 p,CardKind kind,Color color)
        {
            if(kind==CardKind.Shot||kind==CardKind.Carbine||kind==CardKind.Rifle||kind==CardKind.Volley)
            {float length=kind==CardKind.Rifle?58:kind==CardKind.Carbine?49:38;Box(new Rect(p.x-length/2,p.y-5,length,9),color);Box(new Rect(p.x-length/2+5,p.y+2,9,16),color);if(kind==CardKind.Volley){Line(p+new Vector2(-22,-14),p+new Vector2(22,-14),color,3);Line(p+new Vector2(-22,15),p+new Vector2(22,15),color,3);}}
            else if(kind==CardKind.Tonic){Box(new Rect(p.x-4,p.y-20,8,39),color);Box(new Rect(p.x-20,p.y-4,40,8),color);}
            else if(kind==CardKind.Dodge){Line(p+new Vector2(-25,0),p+new Vector2(22,0),color,5);Line(p+new Vector2(22,0),p+new Vector2(6,-13),color,5);Line(p+new Vector2(22,0),p+new Vector2(6,13),color,5);}
            else if(kind==CardKind.Armor){Box(new Rect(p.x-21,p.y-17,42,30),color);Line(p+new Vector2(-20,13),p+new Vector2(0,24),color,5);Line(p+new Vector2(0,24),p+new Vector2(20,13),color,5);}
            else if(kind==CardKind.Supplies){for(int i=0;i<3;i++)Box(new Rect(p.x-26+i*12,p.y-16+i*2,28,34),color);}
            else if(kind==CardKind.Disarm){Line(p+new Vector2(-20,-15),p+new Vector2(20,15),color,5);Line(p+new Vector2(20,-15),p+new Vector2(-20,15),color,5);}
            else {Circle(p,17,color);Box(new Rect(p.x-11,p.y-2,38,6),color);}
        }
        void Journal()
        {
            Panel(new Rect(1060,592,360,288),"ЖУРНАЛ СОБЫТИЙ");
            logScroll=GUI.BeginScrollView(new Rect(1078,632,324,184),logScroll,new Rect(0,0,299,Mathf.Max(180,journal.Count*40)));
            for(int i=0;i<journal.Count;i++)Text(new Rect(0,i*40,293,38),journal[i],small,i==journal.Count-1?ink:muted);GUI.EndScrollView();
            if(error.Length>0)Text(new Rect(1078,821,324,49),error,small,new Color(.96f,.46f,.35f));
            else if(online)Text(new Rect(1078,829,324,33),"Комната "+code+" · "+(net.Connected?"на связи":"переподключение"),small);
            else Text(new Rect(1078,829,324,33),"Одиночная игра · без сервера",small);
        }
        void Results()
        {
            Box(new Rect(0,0,W,H),new Color(0,0,0,.7f));Panel(new Rect(388,128,664,643),"МАТЧ ЗАВЕРШЁН");
            Text(new Rect(419,180,602,86),view.winner=="Ничья"?"НИЧЬЯ":"ПОБЕДА\n"+view.winner.ToUpperInvariant(),new GUIStyle(title){fontSize=30,alignment=TextAnchor.MiddleCenter},gold);
            for(int i=0;i<view.players.Length;i++){var p=view.players[i];float y=290+i*46;Text(new Rect(420,y,330,34),p.name+(p.id==view.localId?" · вы":""));Text(new Rect(735,y,280,34),Content.RoleName(p.role)+(p.alive?" · жив":" · выбыл"),small);}
            if(Button(new Rect(420,654,282,48),"Реванш",!online||view.owner==view.localId,true)){if(online)Send(new Command{op="rematch"});else StartLocal();}
            if(Button(new Rect(720,654,299,48),"В меню"))Leave();
            Text(new Rect(420,720,600,30),"Планы были тайными. Теперь все роли раскрыты.",small);
        }
        void Help()
        {
            Box(new Rect(0,0,W,H),new Color(0,0,0,.8f));Panel(new Rect(265,65,910,770),"КАК ИГРАТЬ");
            Text(new Rect(305,115,830,50),"Думайте вместе. Стреляйте одновременно.",title);
            string[] lines={"1. Прочитайте цель своей роли. Роль шерифа открыта, остальные скрыты.","2. Выберите первый или второй слот, затем карту. Для выстрела выберите цель.","3. Положите до двух уклонений в резерв — они сработают автоматически.","4. Нажмите «Подтвердить план». После готовности изменить его нельзя.","5. Все исполняют первый такт вместе, затем второй. Только одна атака за раунд.","6. Лечение и экипировка происходят раньше выстрелов. Урон применяется одновременно.","7. Дальность считается по кругу живых мест. Винтовка достаёт дальше револьвера.","8. Если погибнуть в первом такте, второе действие отменяется.","9. По таймеру выполняется сохранённый план; пустые слоты — ожидание.","10. С раунда 16 растёт истощение. Пассивно переждать матч не получится."};
            for(int i=0;i<lines.Length;i++)Text(new Rect(305,192+i*49,826,43),lines[i],label);
            Text(new Rect(305,702,830,42),"Совет: поставьте оружие в первый слот, а выстрел — во второй. Чужое разоружение может изменить дальность.",small,gold);
            if(Button(new Rect(305,766,826,44),"ПОНЯТНО",true,true))showHelp=false;
        }
        void Settings()
        {
            Box(new Rect(0,0,W,H),new Color(0,0,0,.78f));Panel(new Rect(460,235,520,410),"НАСТРОЙКИ");
            Text(new Rect(494,291,450,38),"Громкость эффектов: "+Mathf.RoundToInt(volume*100)+"%");volume=GUI.HorizontalSlider(new Rect(494,342,448,24),volume,0,1);
            if(Button(new Rect(494,388,448,44),"Уменьшить движение: "+(reduceMotion?"да":"нет")))reduceMotion=!reduceMotion;
            Text(new Rect(494,455,448,64),"Готовность блокирует план. Таймер общий для всех игроков. Карты защиты работают без дополнительных нажатий.",small);
            if(Button(new Rect(494,553,448,49),"СОХРАНИТЬ",true,true)){PlayerPrefs.SetFloat("volume",volume);PlayerPrefs.SetInt("reduceMotion",reduceMotion?1:0);PlayerPrefs.Save();showSettings=false;}
        }
    }
}
