using System;
using System.Collections.Generic;
using System.Linq;

namespace DustAndPowder
{
    public enum Role { Hidden, Sheriff, Deputy, Outlaw, Renegade }
    public enum CardKind { Shot, Dodge, Tonic, Supplies, Carbine, Rifle, Armor, Disarm, Theft, Volley }
    public enum MatchPhase { Lobby, Planning, Resolving, Finished }
    [Serializable] public class Card { public int id; public CardKind kind; public Card Copy() { return new Card { id = id, kind = kind }; } }
    [Serializable] public class ActionOrder { public int card = -1; public int target = -1; public ActionOrder Copy() { return new ActionOrder { card = card, target = target }; } }
    [Serializable] public class Plan
    {
        public ActionOrder[] actions = { new ActionOrder(), new ActionOrder() };
        public int[] dodges = new int[0]; public int[] discard = new int[0];
        public Plan Copy() { return new Plan { actions = actions.Select(a => a.Copy()).ToArray(), dodges = (int[])dodges.Clone(), discard = (int[])discard.Clone() }; }
    }
    [Serializable] public class Player
    {
        public int id, character, hp, maxHp; public string name; public Role role;
        public bool alive = true, bot, connected = true, ready, armorUsed;
        public Card weapon, armor; public List<Card> hand = new List<Card>();
        public Plan plan = new Plan(); public int damageThisRound;
    }
    [Serializable] public class PlayerView
    {
        public int id, character, hp, maxHp, cards, weaponRange; public string name;
        public Role role; public bool alive, bot, connected, ready, armor, armorUsed;
        public Card[] hand = new Card[0];
    }
    [Serializable] public class GameEvent
    {
        public string kind, text; public int source = -1, target = -1, value; public CardKind cardKind;
    }
    [Serializable] public class MatchView
    {
        public int round, beat, localId, marker, version; public MatchPhase phase;
        public string winner = ""; public PlayerView[] players = new PlayerView[0];
        public Plan myPlan = new Plan(); public GameEvent[] events = new GameEvent[0];
        public double remaining; public string room = ""; public int owner; public int capacity = 5; public int timer = 45;
    }
    public static class Content
    {
        public static readonly string[] CardNames = { "Выстрел", "Уклонение", "Тоник", "Запасы", "Карабин", "Винтовка", "Бронежилет", "Разоружение", "Кража", "Залп" };
        public static readonly string[] CardText = { "1 урон одной цели\nДальность оружия", "Отменяет 1 попадание\nПоместите в резерв", "+1 здоровье себе\nСрабатывает до урона", "Получите 2 карты\nДля следующего раунда", "Оружие: дальность 2\nЗаменяет старое", "Оружие: дальность 3\nЗаменяет старое", "Блокирует 1 попадание\nКаждый раунд", "Снимает оружие цели\nЛюбая дальность", "Крадёт 1 карту из руки\nДальность 1", "1 урон всем остальным\nВключая союзников" };
        public static readonly string[] Names = { "Роза", "Док Мэй", "Итан", "Нора", "Лео", "Вера", "Сэм", "Ада" };
        public static readonly string[] Abilities = { "Выстрел: дальность +1", "Тоник лечит на 2", "Максимум здоровья +1", "Лимит руки: 8 карт", "После потери здоровья: +1 карта", "Улучшенное оружие: дальность +1", "После кражи: +1 карта", "Запасы дают 3 карты" };
        public static string RoleName(Role r) { return new[] { "Неизвестно", "Шериф", "Помощник", "Бандит", "Ренегат" }[(int)r]; }
        public static string Goal(Role r)
        {
            switch (r) { case Role.Sheriff: case Role.Deputy: return "Защитите шерифа. Устраните бандитов и ренегата.";
                case Role.Outlaw: return "Устраните шерифа. Остальные бандиты — ваши союзники.";
                case Role.Renegade: return "Останьтесь единственным живым. Берегите баланс сторон."; default: return "Наблюдайте за публичными действиями."; }
        }
        public static bool Attack(CardKind k) { return k == CardKind.Shot || k == CardKind.Volley; }
    }
    public class GameCore
    {
        public readonly List<Player> players = new List<Player>();
        public readonly List<Card> deck = new List<Card>(), discard = new List<Card>();
        public readonly List<GameEvent> events = new List<GameEvent>();
        public MatchPhase phase; public int round, beat, marker, version; public string winner = "";
        uint randomState; int nextCard = 1;
        public GameCore(int seats, uint seed, string humanName = "Игрок")
        {
            if (seats < 4 || seats > 7) throw new ArgumentOutOfRangeException("seats");
            randomState = seed == 0 ? 1234567u : seed;
            var roles = new List<Role> { Role.Sheriff, Role.Renegade, Role.Outlaw, Role.Outlaw };
            if (seats >= 5) roles.Add(Role.Deputy); if (seats >= 6) roles.Add(Role.Outlaw); if (seats == 7) roles.Add(Role.Deputy);
            Shuffle(roles); var characters = Enumerable.Range(0, 8).ToList(); Shuffle(characters);
            for (int i = 0; i < seats; i++)
            {
                int hp = 4 + (characters[i] == 2 ? 1 : 0) + (roles[i] == Role.Sheriff ? 1 : 0);
                players.Add(new Player { id = i, name = i == 0 ? humanName : Content.Names[characters[i]], character = characters[i], role = roles[i], hp = hp, maxHp = hp, bot = i != 0 });
            }
            int[] quantities = { 22, 12, 6, 4, 3, 3, 3, 3, 2, 2 };
            for (int k = 0; k < quantities.Length; k++) for (int i = 0; i < quantities[k]; i++) deck.Add(new Card { id = nextCard++, kind = (CardKind)k });
            Shuffle(deck); foreach (var p in players) Draw(p, 4); BeginRound();
        }
        public uint RandomState { get { return randomState; } }
        int Random(int max) { randomState ^= randomState << 13; randomState ^= randomState >> 17; randomState ^= randomState << 5; return (int)(randomState % (uint)max); }
        void Shuffle<T>(List<T> list) { for (int i = list.Count - 1; i > 0; i--) { int j = Random(i + 1); T x = list[i]; list[i] = list[j]; list[j] = x; } }
        public int Range(Player p) { int range = p.weapon == null ? 1 : (p.weapon.kind == CardKind.Carbine ? 2 : 3); return range + (p.character == 0 ? 1 : 0) + (p.character == 5 && p.weapon != null ? 1 : 0); }
        public static int Distance(PlayerView[] ps, int a, int b)
        {
            var alive = ps.Where(p => p.alive).Select(p => p.id).ToList(); int x = alive.IndexOf(a), y = alive.IndexOf(b);
            if (x < 0 || y < 0 || x == y) return 999;
            return Math.Min(Math.Abs(x - y), alive.Count - Math.Abs(x - y));
        }
        public int Distance(int a, int b) { return Distance(View(a).players, a, b); }
        List<Player> Ordered() { return Enumerable.Range(0, players.Count).Select(i => players[(marker + i) % players.Count]).Where(p => p.alive).ToList(); }
        void Emit(string kind, string text, int source = -1, int target = -1, int value = 0, CardKind cardKind = CardKind.Shot)
        { events.Add(new GameEvent { kind = kind, text = text, source = source, target = target, value = value, cardKind = cardKind }); }
        void Draw(Player p, int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (deck.Count == 0 && discard.Count > 0) { deck.AddRange(discard); discard.Clear(); Shuffle(deck); }
                if (deck.Count == 0) break;
                var c = deck[deck.Count - 1]; deck.RemoveAt(deck.Count - 1); p.hand.Add(c);
            }
        }
        public void BeginRound()
        {
            if (phase == MatchPhase.Finished) return;
            round++; beat = 0; phase = MatchPhase.Planning; version++; events.Clear();
            foreach (var p in Ordered()) { p.ready = false; p.armorUsed = false; p.damageThisRound = 0; p.plan = new Plan(); Draw(p, 2); }
            Emit("round", "Раунд " + round + ". Планируйте действия одновременно.");
        }
        public static string Validate(MatchView view, Plan plan)
        {
            if (view.phase != MatchPhase.Planning) return "Планирование уже завершено.";
            var p = view.players.FirstOrDefault(x => x.id == view.localId);
            if (p == null || !p.alive) return "Вы наблюдаете за матчем.";
            if (plan == null || plan.actions == null || plan.actions.Length != 2 || plan.actions.Any(a => a == null) || plan.dodges == null || plan.discard == null) return "Некорректный формат плана.";
            if (plan.dodges.Length > 2) return "В резерве не больше двух уклонений.";
            var used = new HashSet<int>(); int attacks = 0, range = p.weaponRange;
            foreach (var a in plan.actions)
            {
                if (a.card < 0) continue;
                var card = p.hand.FirstOrDefault(c => c.id == a.card);
                if (card == null || !used.Add(a.card)) return "Карта отсутствует или использована дважды.";
                var k = card.kind;
                if (k == CardKind.Dodge) return "Уклонение используется в резерве.";
                if (Content.Attack(k) && ++attacks > 1) return "Только одна атака за раунд.";
                if (k == CardKind.Carbine || k == CardKind.Rifle) range = (k == CardKind.Carbine ? 2 : 3) + (p.character == 0 ? 1 : 0) + (p.character == 5 ? 1 : 0);
                if (k == CardKind.Shot || k == CardKind.Theft || k == CardKind.Disarm)
                {
                    var t = view.players.FirstOrDefault(x => x.id == a.target);
                    if (t == null || !t.alive || t.id == p.id) return "Выберите живую чужую цель.";
                    int d = Distance(view.players, p.id, t.id);
                    if (k == CardKind.Shot && d > range) return "Цель вне дальности оружия.";
                    if (k == CardKind.Theft && d > 1) return "Кража действует на расстоянии 1.";
                }
            }
            foreach (int id in plan.dodges) { var c = p.hand.FirstOrDefault(x => x.id == id); if (c == null || c.kind != CardKind.Dodge || !used.Add(id)) return "Некорректная карта резерва."; }
            if (plan.discard.Distinct().Count() != plan.discard.Length || plan.discard.Any(id => !p.hand.Any(c => c.id == id))) return "Некорректный список сброса.";
            return "";
        }
        public string Submit(int id, Plan plan, bool ready)
        {
            if (id < 0 || id >= players.Count) return "Нет такого места.";
            if (players[id].ready) return "План уже подтверждён.";
            string error = Validate(View(id), plan); if (error.Length > 0) return error;
            players[id].plan = plan.Copy(); players[id].ready = ready; version++; return "";
        }
        public void LockPlans()
        { if (phase != MatchPhase.Planning) return; foreach (var p in players.Where(x => x.alive)) p.ready = true; phase = MatchPhase.Resolving; beat = 0; events.Clear(); version++; }
        public void ResolveBeat()
        {
            if (phase != MatchPhase.Resolving || beat >= 2) return;
            events.Clear(); var living = Ordered(); var uses = new List<Tuple<Player, ActionOrder, Card>>();
            var attackDistance = new Dictionary<string, int>();
            foreach (var p in living) foreach (var t in living) if (p != t) attackDistance[p.id + ":" + t.id] = Distance(p.id, t.id);
            foreach (var p in living)
            {
                var a = p.plan.actions[beat]; var c = p.hand.FirstOrDefault(x => x.id == a.card); if (c == null) continue;
                p.hand.Remove(c); uses.Add(Tuple.Create(p, a, c));
                if (c.kind != CardKind.Carbine && c.kind != CardKind.Rifle && c.kind != CardKind.Armor) discard.Add(c);
                Emit("action", p.name + ": " + Content.CardNames[(int)c.kind], p.id, a.target, 0, c.kind);
            }
            foreach (var u in uses)
            {
                var p = u.Item1; var c = u.Item3;
                if (c.kind == CardKind.Carbine || c.kind == CardKind.Rifle) { if (p.weapon != null) discard.Add(p.weapon); p.weapon = c; Emit("equip", p.name + " меняет оружие.", p.id); }
                if (c.kind == CardKind.Armor) { if (p.armor != null) discard.Add(p.armor); p.armor = c; Emit("equip", p.name + " надевает бронежилет.", p.id); }
                if (c.kind == CardKind.Tonic) { int heal = Math.Min(p.maxHp - p.hp, p.character == 1 ? 2 : 1); p.hp += heal; Emit("heal", p.name + ": здоровье +" + heal, p.id, p.id, heal); }
            }
            foreach (var u in uses)
            {
                var p = u.Item1; var a = u.Item2; var c = u.Item3;
                if (c.kind == CardKind.Supplies) { Draw(p, p.character == 7 ? 3 : 2); Emit("draw", p.name + " получает запасы.", p.id); }
                if (c.kind != CardKind.Theft && c.kind != CardKind.Disarm) continue;
                var t = living.FirstOrDefault(x => x.id == a.target);
                if (t == null) { Emit("miss", "Цель уже выбыла; действие потрачено.", p.id, a.target); continue; }
                if (c.kind == CardKind.Disarm) { if (t.weapon != null) { discard.Add(t.weapon); t.weapon = null; Emit("disarm", t.name + " остаётся с револьвером.", p.id, t.id); } }
                else
                {
                    if (attackDistance[p.id + ":" + t.id] > 1) continue;
                    var protectedIds = new HashSet<int>(t.plan.actions.Skip(beat + 1).Select(x => x.card).Concat(t.plan.dodges));
                    var available = t.hand.Where(x => !protectedIds.Contains(x.id)).ToList();
                    if (available.Count > 0) { var stolen = available[Random(available.Count)]; t.hand.Remove(stolen); p.hand.Add(stolen); if (p.character == 6) Draw(p, 1); Emit("theft", p.name + " забирает карту у " + t.name, p.id, t.id); }
                    else Emit("miss", t.name + ": нет доступных для кражи карт.", p.id, t.id);
                }
            }
            var hits = living.ToDictionary(p => p.id, p => new List<int>());
            foreach (var u in uses)
            {
                var p = u.Item1; var a = u.Item2; var c = u.Item3;
                if (c.kind == CardKind.Volley) foreach (var t in living.Where(x => x != p)) { hits[t.id].Add(p.id); Emit("shot", p.name + " → " + t.name, p.id, t.id, 1, c.kind); }
                if (c.kind == CardKind.Shot)
                {
                    var t = living.FirstOrDefault(x => x.id == a.target);
                    if (t == null || attackDistance[p.id + ":" + t.id] > Range(p)) { Emit("miss", p.name + ": выстрел без эффекта — цель недоступна.", p.id, a.target); continue; }
                    hits[t.id].Add(p.id); Emit("shot", p.name + " → " + t.name, p.id, t.id, 1, c.kind);
                }
            }
            foreach (var p in living)
            {
                int damage = hits[p.id].Count;
                if (damage > 0 && p.armor != null && !p.armorUsed) { p.armorUsed = true; damage--; Emit("block", p.name + ": броня блокирует попадание.", p.id, p.id, 1); }
                foreach (int id in p.plan.dodges)
                {
                    if (damage == 0) break; var dodge = p.hand.FirstOrDefault(c => c.id == id); if (dodge == null) continue;
                    p.hand.Remove(dodge); discard.Add(dodge); p.plan.dodges = p.plan.dodges.Where(x => x != id).ToArray(); damage--; Emit("dodge", p.name + " уклоняется.", p.id, p.id, 1);
                }
                p.hp -= damage; p.damageThisRound += damage;
                if (damage > 0) Emit("damage", p.name + ": −" + damage + " здоровья", -1, p.id, damage);
            }
            Eliminate(); beat++; version++; CheckVictory();
        }
        void Eliminate()
        {
            foreach (var p in players.Where(x => x.alive && x.hp <= 0))
            { p.alive = false; p.hp = 0; discard.AddRange(p.hand); p.hand.Clear(); if (p.weapon != null) discard.Add(p.weapon); if (p.armor != null) discard.Add(p.armor); p.weapon = p.armor = null;
                Emit("death", p.name + " выбывает. Роль: " + Content.RoleName(p.role), p.id, p.id); }
        }
        public void FinishRound()
        {
            if (phase != MatchPhase.Resolving || beat != 2) return; events.Clear();
            foreach (var p in Ordered())
            {
                if (p.character == 4 && p.damageThisRound > 0) Draw(p, 1);
                int max = p.character == 3 ? 8 : 6;
                var order = p.plan.discard.Concat(p.hand.Select(c => c.id)).Distinct().ToArray();
                foreach (int id in order) { if (p.hand.Count <= max) break; var c = p.hand.FirstOrDefault(x => x.id == id); if (c != null) { p.hand.Remove(c); discard.Add(c); } }
            }
            if (round >= 16)
            {
                int fatigue = 1 + (round - 16) / 2;
                foreach (var p in Ordered()) { p.hp -= fatigue; Emit("damage", p.name + ": истощение −" + fatigue, -1, p.id, fatigue); }
                Eliminate(); CheckVictory();
            }
            if (phase != MatchPhase.Finished) { marker = (marker + 1) % players.Count; while (!players[marker].alive) marker = (marker + 1) % players.Count; }
            version++;
        }
        public void CheckVictory()
        {
            var alive = players.Where(p => p.alive).ToList();
            if (alive.Count == 0) winner = "Ничья";
            else if (alive.Count == 1 && alive[0].role == Role.Renegade) winner = "Ренегат";
            else if (!alive.Any(p => p.role == Role.Sheriff)) winner = "Бандиты";
            else if (!alive.Any(p => p.role == Role.Outlaw || p.role == Role.Renegade)) winner = "Шериф и помощники";
            if (winner.Length > 0) { phase = MatchPhase.Finished; Emit("victory", "Победа: " + winner); }
        }
        public MatchView View(int localId)
        {
            return new MatchView { round = round, beat = beat, localId = localId, marker = marker, version = version, phase = phase, winner = winner,
                myPlan = localId >= 0 && localId < players.Count ? players[localId].plan.Copy() : new Plan(), events = events.ToArray(),
                players = players.Select(p => new PlayerView { id = p.id, name = p.name, character = p.character, hp = p.hp, maxHp = p.maxHp, alive = p.alive,
                    bot = p.bot, connected = p.connected, ready = p.ready, armor = p.armor != null, armorUsed = p.armorUsed, cards = p.hand.Count, weaponRange = Range(p),
                    role = p.id == localId || p.role == Role.Sheriff || !p.alive || phase == MatchPhase.Finished ? p.role : Role.Hidden,
                    hand = p.id == localId ? p.hand.Select(c => c.Copy()).ToArray() : new Card[0] }).ToArray() };
        }
        public void Signal(int id, string signal, int target)
        {
            if (phase != MatchPhase.Planning || !players[id].alive) return;
            string[] labels = { "Защищаю шерифа", "Подозреваю игрока", "Нужна помощь" }; int number;
            if (!int.TryParse(signal, out number) || number < 0 || number >= labels.Length) return;
            Emit("signal", players[id].name + ": «" + labels[number] + "»" + (number == 1 && target >= 0 && target < players.Count ? " — " + players[target].name : ""), id, target);
        }
    }
    public class BotBrain
    {
        readonly Dictionary<int, float> suspicion = new Dictionary<int, float>();
        int observedRound = -1, observedBeat = -1; readonly Random random;
        public BotBrain(int seed) { random = new Random(seed); }
        public void Observe(MatchView view)
        {
            if (observedRound == view.round && observedBeat == view.beat) return;
            observedRound = view.round; observedBeat = view.beat;
            var sheriff = view.players.FirstOrDefault(p => p.role == Role.Sheriff);
            if (sheriff == null) return;
            foreach (var e in view.events.Where(e => e.kind == "shot" && e.source >= 0))
            { if (!suspicion.ContainsKey(e.source)) suspicion[e.source] = 0; suspicion[e.source] += e.target == sheriff.id ? 3 : -0.1f; }
        }
        public Plan Choose(MatchView v, bool simple = false)
        {
            Observe(v); var me = v.players.First(p => p.id == v.localId); var plan = new Plan();
            var cards = me.hand.ToList(); int slot = 0; bool attacked = false; int range = me.weaponRange;
            Action<Card, int> use = (c, t) => { plan.actions[slot++] = new ActionOrder { card = c.id, target = t }; cards.Remove(c); };
            var heal = cards.FirstOrDefault(c => c.kind == CardKind.Tonic);
            if (heal != null && me.hp < me.maxHp) use(heal, me.id);
            var targets = v.players.Where(p => p.alive && p.id != me.id).OrderByDescending(p => Score(me, p, v) + (simple ? random.NextDouble() * 4 : random.NextDouble() * .3)).ToList();
            var shot = cards.FirstOrDefault(c => c.kind == CardKind.Shot);
            var weapon = cards.Where(c => c.kind == CardKind.Carbine || c.kind == CardKind.Rifle).OrderByDescending(c => c.kind == CardKind.Rifle ? 3 : 2).FirstOrDefault();
            if (slot == 0 && shot != null && weapon != null && targets.Count > 0 && GameCore.Distance(v.players, me.id, targets[0].id) > range)
            { range = (weapon.kind == CardKind.Rifle ? 3 : 2) + (me.character == 0 || me.character == 5 ? 1 : 0); use(weapon, me.id); }
            var target = targets.FirstOrDefault(p => GameCore.Distance(v.players, me.id, p.id) <= range && Score(me, p, v) > -3);
            if (slot < 2 && shot != null && target != null) { use(shot, target.id); attacked = true; }
            while (slot < 2)
            {
                Card c = cards.FirstOrDefault(x => x.kind == CardKind.Armor && !me.armor);
                if (c == null) c = cards.FirstOrDefault(x => x.kind == CardKind.Supplies);
                if (c == null) c = cards.FirstOrDefault(x => (x.kind == CardKind.Rifle ? 3 : x.kind == CardKind.Carbine ? 2 : 0) > range);
                if (c != null) { use(c, me.id); continue; }
                var t = targets.FirstOrDefault(p => p.weaponRange > (p.character == 0 ? 2 : 1) && Score(me, p, v) > 0);
                c = cards.FirstOrDefault(x => x.kind == CardKind.Disarm); if (c != null && t != null) { use(c, t.id); continue; }
                t = targets.FirstOrDefault(p => GameCore.Distance(v.players, me.id, p.id) == 1 && p.cards > 2 && Score(me, p, v) > 0);
                c = cards.FirstOrDefault(x => x.kind == CardKind.Theft); if (c != null && t != null) { use(c, t.id); continue; }
                c = cards.FirstOrDefault(x => x.kind == CardKind.Volley);
                if (!attacked && c != null && (me.role == Role.Outlaw || v.players.Count(p => p.alive) == 2 && me.role == Role.Renegade)) { use(c, -1); attacked = true; continue; }
                break;
            }
            plan.dodges = cards.Where(c => c.kind == CardKind.Dodge).Take(2).Select(c => c.id).ToArray();
            plan.discard = cards.OrderBy(c => c.kind == CardKind.Dodge ? 10 : c.kind == CardKind.Shot ? 8 : c.kind == CardKind.Tonic && me.hp < me.maxHp ? 9 : 1).Select(c => c.id).ToArray();
            return GameCore.Validate(v, plan).Length == 0 ? plan : new Plan();
        }
        float Score(PlayerView me, PlayerView t, MatchView v)
        {
            float s; suspicion.TryGetValue(t.id, out s);
            if (me.role == Role.Outlaw) return t.role == Role.Sheriff ? 10 : s > 1 ? -4 : 1 + (t.maxHp - t.hp) * .2f;
            if (me.role == Role.Sheriff || me.role == Role.Deputy) return t.role == Role.Sheriff ? -100 : s + 1 + (t.maxHp - t.hp) * .3f;
            if (me.role == Role.Renegade) return t.role == Role.Sheriff && v.players.Count(p => p.alive) > 2 ? -10 : 2 + s + (t.maxHp - t.hp) * .5f;
            return 0;
        }
    }
}
