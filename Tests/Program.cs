using DustAndPowder;
using System.Text.Json;

int checks = 0;
void Assert(bool success,string message) { checks++; if(!success) throw new Exception(message); }
Card Give(GameCore g,int player,CardKind kind)
{
    var c=g.deck.FirstOrDefault(c=>c.kind==kind)??g.discard.FirstOrDefault(c=>c.kind==kind);
    if(c!=null){g.deck.Remove(c);g.discard.Remove(c);}else
    {var owner=g.players.First(p=>p.hand.Any(c=>c.kind==kind));c=owner.hand.First(c=>c.kind==kind);owner.hand.Remove(c);}
    g.players[player].hand.Add(c);return c;
}
GameCore Fresh(int count=4)
{
    var g=new GameCore(count,1234);foreach(var p in g.players){g.deck.AddRange(p.hand);p.hand.Clear();p.character=3;p.hp=p.maxHp=4;}
    g.players[0].role=Role.Sheriff;g.players[1].role=Role.Outlaw;g.players[2].role=Role.Outlaw;g.players[3].role=Role.Renegade;return g;
}
void PlanShot(GameCore g,int actor,int target,int slot=0)
{var c=Give(g,actor,CardKind.Shot);var p=new Plan();p.actions[slot]=new ActionOrder{card=c.id,target=target};Assert(g.Submit(actor,p,true)=="","shot plan valid");}
void Inventory(GameCore g)
{
    var cards=g.deck.Concat(g.discard).Concat(g.players.SelectMany(p=>p.hand)).Concat(g.players.Where(p=>p.weapon!=null).Select(p=>p.weapon)).Concat(g.players.Where(p=>p.armor!=null).Select(p=>p.armor)).ToArray();
    Assert(cards.Length==60,"60 cards conserved");Assert(cards.Select(c=>c.id).Distinct().Count()==60,"unique card ownership");
}
{
    var g=Fresh();g.players[0].hp=g.players[1].hp=1;PlanShot(g,0,1);PlanShot(g,1,0);g.LockPlans();g.ResolveBeat();Assert(!g.players[0].alive&&!g.players[1].alive,"mutual death");Assert(g.winner=="Бандиты","dead sheriff awards outlaws");Inventory(g);
}
{
    var g=Fresh();foreach(var p in g.players)p.hp=1;
    PlanShot(g,0,1);PlanShot(g,1,2);PlanShot(g,2,3);PlanShot(g,3,0);g.LockPlans();g.ResolveBeat();Assert(g.winner=="Ничья","all die -> draw");Inventory(g);
}
{
    var g=Fresh();g.players[1].hp=1;
    PlanShot(g,0,1);var c=Give(g,1,CardKind.Tonic);var p=new Plan();p.actions[0]=new ActionOrder{card=c.id};g.Submit(1,p,true);g.LockPlans();g.ResolveBeat();Assert(g.players[1].alive&&g.players[1].hp==1,"heal before damage");Inventory(g);
}
{
    var g=Fresh();g.players[0].hp=4;foreach(var p in g.players)p.weapon=Give(g,p.id,p.id==0?CardKind.Rifle:CardKind.Carbine);foreach(var p in g.players)p.hand.Remove(p.weapon);
    PlanShot(g,1,0);PlanShot(g,2,0);PlanShot(g,3,0);
    var d1=Give(g,0,CardKind.Dodge);var d2=Give(g,0,CardKind.Dodge);g.Submit(0,new Plan{dodges=new[]{d1.id,d2.id}},true);g.LockPlans();g.ResolveBeat();Assert(g.players[0].hp==3,"three hits minus two dodges");Inventory(g);
}
{
    var g=Fresh();var shot=Give(g,0,CardKind.Shot);var weapon=Give(g,0,CardKind.Rifle);var p=new Plan();p.actions[0]=new ActionOrder{card=weapon.id};p.actions[1]=new ActionOrder{card=shot.id,target=2};Assert(g.Submit(0,p,true)=="","equipment extends projected range");g.LockPlans();g.ResolveBeat();g.ResolveBeat();Assert(g.players[2].hp==3,"second beat uses equipped range");Inventory(g);
}
{
    var g=Fresh();g.players[0].weapon=Give(g,0,CardKind.Rifle);g.players[0].hand.Remove(g.players[0].weapon);
    PlanShot(g,0,2);var c=Give(g,1,CardKind.Disarm);var p=new Plan();p.actions[0]=new ActionOrder{card=c.id,target=0};g.Submit(1,p,true);g.LockPlans();g.ResolveBeat();Assert(g.players[2].hp==4,"disarm invalidates far shot");Inventory(g);
}
{
    var g=Fresh();var c=Give(g,1,CardKind.Shot);var d=Give(g,1,CardKind.Dodge);var p=new Plan{dodges=new[]{d.id}};p.actions[1]=new ActionOrder{card=c.id,target=0};g.Submit(1,p,true);
    var theft=Give(g,0,CardKind.Theft);var plan=new Plan();plan.actions[0]=new ActionOrder{card=theft.id,target=1};g.Submit(0,plan,true);g.LockPlans();g.ResolveBeat();Assert(g.players[1].hand.Any(x=>x.id==c.id)&&g.players[1].hand.Any(x=>x.id==d.id),"reserved plans protected from theft");Inventory(g);
}
{
    var g=Fresh();var c=Give(g,0,CardKind.Shot);var p=new Plan();p.actions[0]=new ActionOrder{card=c.id,target=1};p.actions[1]=new ActionOrder{card=c.id,target=3};Assert(g.Submit(0,p,true)!="","duplicate rejected");
    p.actions[1]=new ActionOrder();Assert(g.Submit(0,p,true)=="","valid accepted");Assert(g.Submit(0,p,true)!="","ready locked");g.LockPlans();Assert(g.Submit(0,p,true)!="","late submission rejected");Inventory(g);
}
{
    var g=Fresh();var v=g.View(0);Assert(v.players[0].hand.Length==g.players[0].hand.Count,"own hand shown");Assert(v.players.Skip(1).All(p=>p.hand.Length==0),"foreign hands hidden");Assert(v.players.Skip(1).All(p=>p.role==Role.Hidden),"foreign secret roles hidden");
    var serialized=JsonSerializer.Serialize(v,new JsonSerializerOptions{IncludeFields=true});Assert(!serialized.Contains("randomState")&&!serialized.Contains("deck"),"seed and deck absent from wire view");
}
{
    var g=Fresh();g.players[0].alive=g.players[1].alive=g.players[2].alive=false;g.CheckVictory();Assert(g.winner=="Ренегат","lone renegade victory");
    g=Fresh();g.players[1].alive=g.players[2].alive=g.players[3].alive=false;g.CheckVictory();Assert(g.winner=="Шериф и помощники","sheriff victory");
}
{
    var g=Fresh();int iterations=0;
    while(g.phase!=MatchPhase.Finished&&iterations++<30){g.LockPlans();g.ResolveBeat();if(g.phase==MatchPhase.Finished)break;g.ResolveBeat();if(g.phase==MatchPhase.Finished)break;g.FinishRound();if(g.phase!=MatchPhase.Finished)g.BeginRound();Inventory(g);}
    Assert(g.phase==MatchPhase.Finished&&g.round<=26,"fatigue bounds passive match");
}
string Replay(uint seed)
{
    var g=new GameCore(7,seed);var brains=g.players.Select(p=>new BotBrain((int)seed+p.id)).ToArray();int iterations=0;
    while(g.phase!=MatchPhase.Finished&&iterations++<27)
    {
        foreach(var p in g.players.Where(p=>p.alive)){var plan=brains[p.id].Choose(g.View(p.id));Assert(g.Submit(p.id,plan,true)=="","bot emits valid plan");}
        g.LockPlans();g.ResolveBeat();foreach(var p in g.players)brains[p.id].Observe(g.View(p.id));Inventory(g);if(g.phase==MatchPhase.Finished)break;
        g.ResolveBeat();foreach(var p in g.players)brains[p.id].Observe(g.View(p.id));Inventory(g);if(g.phase==MatchPhase.Finished)break;
        g.FinishRound();if(g.phase!=MatchPhase.Finished)g.BeginRound();Inventory(g);
    }
    Assert(g.phase==MatchPhase.Finished,"bot game finishes");return g.round+"/"+g.winner+"/"+g.RandomState+"/"+string.Join(",",g.players.Select(p=>p.hp));
}
{
    var g=Fresh();var dodge=Give(g,0,CardKind.Dodge);var supplies=Give(g,0,CardKind.Supplies);
    var plan=new Plan{dodges=new[]{dodge.id}};plan.actions[1]=new ActionOrder{card=supplies.id};g.Submit(0,plan,true);
    PlanShot(g,1,0);PlanShot(g,3,0,1);g.LockPlans();g.ResolveBeat();Assert(g.players[0].hp==4,"first reserved dodge blocks");
    g.discard.AddRange(g.deck);g.deck.Clear();g.discard.Remove(dodge);g.deck.Add(dodge);
    g.ResolveBeat();Assert(g.players[0].hp==3,"redrawn spent dodge cannot react again this round");Inventory(g);
}
for(uint seed=1;seed<=200;seed++) {string a=Replay(seed),b=Replay(seed);Assert(a==b,"deterministic replay "+seed);}
Console.WriteLine("PASS: "+checks+" assertions; 400 complete bot matches; simultaneous damage, healing, defense, equipment, theft, privacy, fatigue, determinism.");
TurnChecks.Run();
