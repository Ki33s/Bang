using System;
using System.Collections.Generic;
using System.Linq;

namespace DustAndPowder.TurnPreview
{
    public enum TurnCardKind { Shot, Guard, Aim, Heal, Disarm, Weapon, Cover }
    [Serializable] public class TurnCard { public int id; public TurnCardKind kind; }
    [Serializable] public class TurnOrder { public int card, target = -1; }
    [Serializable] public class TurnActor
    {
        public int id, hp, maxHp, guards, covers, aim, range = 2; public string name;
        public bool alive = true; public Role role;
        public List<TurnCard> hand = new List<TurnCard>();
        public TurnActor Copy() { return new TurnActor { id=id,name=name,hp=hp,maxHp=maxHp,guards=guards,covers=covers,aim=aim,range=range,alive=alive,role=role,hand=hand.Select(c=>new TurnCard{id=c.id,kind=c.kind}).ToList() }; }
    }
    [Serializable] public class TurnEvent
    {
        public int source, target, card, lane, damage, healing, hpAfter, guardsAfter, coverAfter, sourceCoverAfter, rangeAfter, aimAfter;
        public TurnCardKind kind; public bool hit, blocked, coverBlocked, skipped, killed;
        public string text, reason;
    }
    public class TurnResult
    {
        public List<TurnEvent> events = new List<TurnEvent>(); public List<TurnActor> finalActors;
    }
    public static class TurnRules
    {
        public const int Energy = 3, MaxCards = 6;
        public static readonly string[] Names={"Выстрел","Уклонение","Прицеливание","Тоник","Разоружение","Карабин","В укрытие"};
        public static readonly string[] Descriptions={"2 урона цели","Защита от 1 попадания\nДо конца хода","Следующий выстрел: +2 урона","Восстановить 3 здоровья","Дальность цели становится 1","Ваша дальность становится 3","Блок 1 выстрела\nАтака снимает укрытие"};
        public static int Cost(TurnCardKind k){return k==TurnCardKind.Aim?0:1;}
        public static bool NeedsTarget(TurnCardKind k){return k==TurnCardKind.Shot||k==TurnCardKind.Disarm;}
        public static int Distance(List<TurnActor> actors,int a,int b)
        {var ids=actors.Where(x=>x.alive).Select(x=>x.id).ToList();int x=ids.IndexOf(a),y=ids.IndexOf(b);return x<0||y<0||x==y?999:Math.Min(Math.Abs(x-y),ids.Count-Math.Abs(x-y));}
        public static List<TurnActor> Create()
        {
            string[] names={"Шериф","Коул","Мэй","Сэм"}; Role[] roles={Role.Sheriff,Role.Outlaw,Role.Deputy,Role.Renegade};
            var actors=new List<TurnActor>();int id=1;
            for(int i=0;i<4;i++)
            {
                var a=new TurnActor{id=i,name=names[i],role=roles[i],hp=i==0?7:i==3?4:8,maxHp=i==0?10:8};
                foreach(var kind in new[]{TurnCardKind.Shot,TurnCardKind.Shot,TurnCardKind.Guard,TurnCardKind.Aim,TurnCardKind.Heal,TurnCardKind.Disarm,TurnCardKind.Weapon,TurnCardKind.Cover})a.hand.Add(new TurnCard{id=id++,kind=kind});
                actors.Add(a);
            }
            return actors;
        }
        public static string Validate(List<TurnActor> actors,int id,List<TurnOrder> plan)
        {
            var a=actors.FirstOrDefault(x=>x.id==id);if(a==null||!a.alive)return "Персонаж выбыл.";
            if(plan==null||plan.Count>MaxCards)return "Не больше 6 карт в цепочке.";
            var used=new HashSet<int>();int energy=0,range=a.range;
            foreach(var order in plan)
            {
                if(order==null)return "Некорректная карта.";var card=a.hand.FirstOrDefault(c=>c.id==order.card);
                if(card==null||!used.Add(card.id))return "Карта уже использована или отсутствует.";
                energy+=Cost(card.kind);if(energy>Energy)return "Не хватает энергии: на ход доступно 3.";
                if(card.kind==TurnCardKind.Weapon)range=3;
                if(NeedsTarget(card.kind))
                {
                    var t=actors.FirstOrDefault(x=>x.id==order.target);
                    if(t==null||!t.alive||t.id==id)return "Выберите чужую живую цель.";
                    if(card.kind==TurnCardKind.Shot&&Distance(actors,id,t.id)>range)return "Цель вне дальности оружия.";
                }
            }
            return "";
        }
        public static List<TurnOrder> Example(TurnActor a)
        {return Orders(a,new[]{TurnCardKind.Guard,TurnCardKind.Aim,TurnCardKind.Shot,TurnCardKind.Shot},new[]{a.id,a.id,1,1});}
        public static List<TurnOrder> CoverExample(TurnActor a)
        {return Orders(a,new[]{TurnCardKind.Cover,TurnCardKind.Aim,TurnCardKind.Shot,TurnCardKind.Shot},new[]{a.id,a.id,1,1});}
        static List<TurnOrder> Orders(TurnActor a,TurnCardKind[] kinds,int[] targets)
        {
            var available=a.hand.ToList();var result=new List<TurnOrder>();
            for(int i=0;i<kinds.Length;i++){var c=available.First(x=>x.kind==kinds[i]);available.Remove(c);result.Add(new TurnOrder{card=c.id,target=targets[i]});}return result;
        }
        public static List<TurnOrder> BotPlan(TurnActor own)
        {
            // Only the bot's own role and hand are inspected. Plans never read the human's queued cards.
            if(own.role==Role.Outlaw)return Orders(own,new[]{TurnCardKind.Aim,TurnCardKind.Shot,TurnCardKind.Shot},new[]{own.id,0,0});
            if(own.role==Role.Deputy)return Orders(own,new[]{TurnCardKind.Guard,TurnCardKind.Shot},new[]{own.id,1});
            return Orders(own,new[]{TurnCardKind.Aim,TurnCardKind.Shot,TurnCardKind.Heal},new[]{own.id,2,own.id});
        }
        public static TurnResult Resolve(List<TurnActor> initial,Dictionary<int,List<TurnOrder>> plans,bool wholeChains=false)
        {
            foreach(var pair in plans){string error=Validate(initial,pair.Key,pair.Value);if(error.Length>0)throw new ArgumentException(error);}
            var result=new TurnResult{finalActors=initial.Select(a=>a.Copy()).ToList()};var state=result.finalActors;
            var schedule=new List<Tuple<int,int,TurnOrder>>();int lanes=plans.Count==0?1:Math.Max(1,plans.Values.Max(p=>p.Count));
            if(wholeChains)
            {foreach(var actor in initial)if(plans.ContainsKey(actor.id))for(int lane=0;lane<plans[actor.id].Count;lane++)schedule.Add(Tuple.Create(actor.id,lane,plans[actor.id][lane]));}
            else
            {for(int lane=0;lane<lanes;lane++)foreach(var actor in initial)if(plans.ContainsKey(actor.id)&&plans[actor.id].Count>lane)schedule.Add(Tuple.Create(actor.id,lane,plans[actor.id][lane]));}
            foreach(var item in schedule)
            {
                var a=state.First(x=>x.id==item.Item1);var order=item.Item3;var card=a.hand.First(c=>c.id==order.card);
                var e=new TurnEvent{source=a.id,target=NeedsTarget(card.kind)?order.target:a.id,card=card.id,kind=card.kind,lane=item.Item2};
                if(!a.alive){e.skipped=true;e.reason="Персонаж выбыл раньше своего действия.";e.text=a.name+": действие отменено — персонаж выбыл.";result.events.Add(e);continue;}
                a.hand.Remove(card);var t=state.First(x=>x.id==e.target);
                if(NeedsTarget(card.kind)&&!t.alive){e.skipped=true;e.reason="Цель уже выбыла.";e.text=a.name+": "+Names[(int)card.kind]+" без эффекта — цель выбыла.";result.events.Add(e);continue;}
                if(card.kind==TurnCardKind.Shot||card.kind==TurnCardKind.Disarm||card.kind==TurnCardKind.Heal||card.kind==TurnCardKind.Weapon)a.covers=0;
                switch(card.kind)
                {
                    case TurnCardKind.Guard:a.guards++;e.text=a.name+" готовит уклонение.";break;
                    case TurnCardKind.Cover:a.covers=1;e.text=a.name+" спрятался за ящиками: укрытие готово.";break;
                    case TurnCardKind.Aim:a.aim+=2;e.text=a.name+" прицеливается: следующий выстрел +2.";break;
                    case TurnCardKind.Heal:e.healing=Math.Min(3,a.maxHp-a.hp);a.hp+=e.healing;e.text=a.name+": лечение +"+e.healing;break;
                    case TurnCardKind.Weapon:a.range=3;e.text=a.name+" берёт карабин: дальность 3.";break;
                    case TurnCardKind.Disarm:t.range=1;e.text=a.name+" разоружает "+t.name+".";break;
                    case TurnCardKind.Shot:
                        int potential=2+a.aim;a.aim=0;
                        if(Distance(state,a.id,t.id)>a.range){e.reason="Цель вне дальности после предыдущих действий.";e.text=a.name+" → "+t.name+": ПРОМАХ — вне дальности.";}
                        else if(t.covers>0){t.covers=0;e.blocked=e.coverBlocked=true;e.reason="Цель спряталась за укрытием.";e.text=a.name+" → "+t.name+": УКРЫТИЕ — выстрел заблокирован.";}
                        else if(t.guards>0){t.guards--;e.blocked=true;e.reason="Подготовленное уклонение.";e.text=a.name+" → "+t.name+": ПРОМАХ — уклонение.";}
                        else{e.hit=true;e.damage=potential;t.hp=Math.Max(0,t.hp-potential);t.alive=t.hp>0;e.killed=!t.alive;e.text=a.name+" → "+t.name+": ПОПАДАНИЕ −"+potential+(e.killed?" · персонаж выбыл":"");}
                        break;
                }
                e.hpAfter=t.hp;e.guardsAfter=t.guards;e.coverAfter=t.covers;e.sourceCoverAfter=a.covers;e.rangeAfter=t.range;e.aimAfter=a.aim;result.events.Add(e);
            }
            return result;
        }
    }
}
