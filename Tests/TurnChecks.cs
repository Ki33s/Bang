using DustAndPowder.TurnPreview;
using System.Text.Json;
public static class TurnChecks
{
    public static void Run()
    {
        int checks=0;void Assert(bool yes,string reason){checks++;if(!yes)throw new Exception("Turn demo: "+reason);}
        var actors=TurnRules.Create();var example=TurnRules.Example(actors[0]);
        Assert(example.Count==4,"four cards fit one turn");Assert(example.Sum(o=>TurnRules.Cost(actors[0].hand.First(c=>c.id==o.card).kind))==3,"three energy");Assert(TurnRules.Validate(actors,0,example)=="","multi-card combo valid");
        var extra=example.Concat(new[]{new TurnOrder{card=actors[0].hand.First(c=>c.kind==TurnCardKind.Heal).id}}).ToList();Assert(TurnRules.Validate(actors,0,extra)!="","over-budget combo rejected");
        var duplicate=example.Concat(new[]{example[0]}).ToList();Assert(TurnRules.Validate(actors,0,duplicate)!="","duplicate card rejected");
        var plans=new Dictionary<int,List<TurnOrder>>{{0,example}};foreach(var a in actors.Skip(1))plans.Add(a.id,TurnRules.BotPlan(a));
        string before=JsonSerializer.Serialize(actors,new JsonSerializerOptions{IncludeFields=true});var result=TurnRules.Resolve(actors,plans);
        Assert(JsonSerializer.Serialize(actors,new JsonSerializerOptions{IncludeFields=true})==before,"resolver preserves initial state");
        Assert(result.events.Take(4).Select(e=>e.source).SequenceEqual(new[]{0,1,2,3}),"one card each in fixed demo order");
        Assert(result.events.Any(e=>e.blocked&&e.target==0),"guard causes an actual miss");Assert(result.events.Any(e=>e.hit&&e.damage==4),"aim buffs next shot");Assert(result.events.Any(e=>e.healing==3),"medicine heals");Assert(result.events.Any(e=>e.killed),"death produced");
        for(int i=0;i<100;i++)
        {var replay=TurnRules.Resolve(actors,plans);Assert(JsonSerializer.Serialize(replay.events,new JsonSerializerOptions{IncludeFields=true})==JsonSerializer.Serialize(result.events,new JsonSerializerOptions{IncludeFields=true}),"deterministic event stream");}
        actors=TurnRules.Create();actors[1].hp=2;
        var killer=new List<TurnOrder>{new TurnOrder{card=actors[0].hand.First(c=>c.kind==TurnCardKind.Shot).id,target=1}};
        var deadPlan=new List<TurnOrder>{new TurnOrder{card=actors[1].hand.First(c=>c.kind==TurnCardKind.Shot).id,target=0}};
        var cancellation=TurnRules.Resolve(actors,new Dictionary<int,List<TurnOrder>>{{0,killer},{1,deadPlan}});
        Assert(cancellation.events[0].killed&&cancellation.events[1].skipped,"death cancels later actor action");Assert(cancellation.finalActors[0].hp==7,"cancelled shot causes no damage");
        actors=TurnRules.Create();var disarm=new List<TurnOrder>{new TurnOrder{card=actors[0].hand.First(c=>c.kind==TurnCardKind.Disarm).id,target=1}};
        var farShot=new List<TurnOrder>{new TurnOrder{card=actors[1].hand.First(c=>c.kind==TurnCardKind.Shot).id,target=3}};
        var miss=TurnRules.Resolve(actors,new Dictionary<int,List<TurnOrder>>{{0,disarm},{1,farShot}});
        Assert(!miss.events[1].hit&&!miss.events[1].blocked&&miss.events[1].reason.Contains("дальности"),"disarm causes range miss");
        Assert(TurnRules.Resolve(actors,new Dictionary<int,List<TurnOrder>>()).events.Count==0,"empty plans handled");
        actors=TurnRules.Create();var coverCard=actors[0].hand.First(c=>c.kind==TurnCardKind.Cover);
        var coverPlan=new List<TurnOrder>{new TurnOrder{card=coverCard.id},new TurnOrder{card=actors[0].hand.First(c=>c.kind==TurnCardKind.Aim).id}};
        var incoming=actors[1].hand.Where(c=>c.kind==TurnCardKind.Shot).Select(c=>new TurnOrder{card=c.id,target=0}).ToList();
        var covered=TurnRules.Resolve(actors,new Dictionary<int,List<TurnOrder>>{{0,coverPlan},{1,incoming}});
        var shots=covered.events.Where(e=>e.kind==TurnCardKind.Shot).ToList();
        Assert(shots[0].blocked&&shots[0].coverBlocked&&!shots[0].hit,"cover intercepts first shot");
        Assert(shots[0].hpAfter==7&&shots[0].coverAfter==0,"cover consumed without damage");
        Assert(shots[1].hit&&shots[1].hpAfter==5,"cover protects only one shot");
        Assert(covered.finalActors[0].aim==2,"aim works while sheltered");
        Assert(actors[0].covers==0&&actors[0].hp==7,"cover resolution preserves input");
        var shelterOnly=TurnRules.Resolve(actors,new Dictionary<int,List<TurnOrder>>{{0,coverPlan}});
        Assert(shelterOnly.finalActors[0].covers==1,"aim retains unused cover");
        var attack=actors[0].hand.First(c=>c.kind==TurnCardKind.Shot);
        var exits=new List<TurnOrder>{new TurnOrder{card=coverCard.id},new TurnOrder{card=attack.id,target=1}};
        var response=new List<TurnOrder>{new TurnOrder{card=actors[1].hand.First(c=>c.kind==TurnCardKind.Aim).id},incoming[0]};
        var exited=TurnRules.Resolve(actors,new Dictionary<int,List<TurnOrder>>{{0,exits},{1,response}});
        Assert(exited.events[2].sourceCoverAfter==0,"outgoing attack leaves cover");
        Assert(exited.events[3].hit&&!exited.events[3].blocked,"shot after cover exit causes damage");
        var twoDefenses=new List<TurnOrder>{new TurnOrder{card=coverCard.id},new TurnOrder{card=actors[0].hand.First(c=>c.kind==TurnCardKind.Guard).id}};
        var layered=TurnRules.Resolve(actors,new Dictionary<int,List<TurnOrder>>{{0,twoDefenses},{1,incoming}});
        Assert(layered.events[1].coverBlocked&&layered.events[3].blocked&&!layered.events[3].coverBlocked,"cover and dodge have distinct consumption");
        Assert(layered.finalActors[0].hp==7,"cover and dodge block two shots");
        var demo=TurnRules.CoverExample(actors[0]);
        Assert(TurnRules.Validate(actors,0,demo)==""&&demo.Sum(o=>TurnRules.Cost(actors[0].hand.First(c=>c.id==o.card).kind))==3,"cover demo fits energy budget");
        foreach(var kind in new[]{TurnCardKind.Heal,TurnCardKind.Weapon,TurnCardKind.Disarm})
        {
            var leave=new List<TurnOrder>{new TurnOrder{card=coverCard.id},new TurnOrder{card=actors[0].hand.First(c=>c.kind==kind).id,target=1}};
            Assert(TurnRules.Resolve(actors,new Dictionary<int,List<TurnOrder>>{{0,leave}}).finalActors[0].covers==0,"other exposed action leaves cover");
        }
        Console.WriteLine("PASS: "+checks+" one-turn checks; energy, four-card combos, ordering, hit/miss, aim, healing, death cancellation, determinism.");
    }
}
