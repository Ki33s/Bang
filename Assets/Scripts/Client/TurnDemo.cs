using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using DustAndPowder.TurnPreview;

namespace DustAndPowder
{
    public class TurnDemo : MonoBehaviour
    {
        enum Phase { Plan, Film, Done }
        const float W=1440,H=900;
        TurnStage stage;List<TurnActor> initial,visible;List<TurnOrder> plan=new List<TurnOrder>();TurnResult result;
        Phase phase;int index,selected=-1;float elapsed,speed=1;bool applied,paused,reducedMotion,help,soundPlayed;
        string error="";Vector2 logScroll;GUIStyle text,small,title,centered;AudioSource source;AudioClip bang,heal;
        readonly List<string> history=new List<string>();
        Color ink=new Color(.95f,.89f,.75f),gold=new Color(.95f,.68f,.33f),muted=new Color(.59f,.66f,.69f),dark=new Color(.065f,.10f,.13f);
        TurnEvent Current {get{return result!=null&&index<result.events.Count?result.events[index]:null;}}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot(){if(SceneManager.GetActiveScene().name=="OneTurn")new GameObject("TurnDemo").AddComponent<TurnDemo>();}
        void Awake()
        {
            Application.targetFrameRate=60;Application.runInBackground=true;
            stage=new GameObject("Turn Stage").AddComponent<TurnStage>();stage.Initialize();
            source=gameObject.AddComponent<AudioSource>();bang=Sound(.2f,75,true);heal=Sound(.35f,540,false);
            Reset();plan=TurnRules.CoverExample(initial[0]);
            var arguments=Environment.GetCommandLineArgs();
            if(arguments.Contains("--play-one-turn"))PlayTurn();
#if !UNITY_WEBGL || UNITY_EDITOR
            int capture=Array.IndexOf(arguments,"--capture-one-turn");
            if(capture>=0&&capture+1<arguments.Length){Time.captureFramerate=10;PlayTurn();StartCoroutine(Capture(arguments[capture+1]));}
#endif
        }
#if !UNITY_WEBGL || UNITY_EDITOR
        System.Collections.IEnumerator Capture(string folder)
        {
            System.IO.Directory.CreateDirectory(folder);int number=0;
            while(phase==Phase.Film){yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(folder,"frame"+(number++).ToString("D4")+".png"));}
            yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(folder,"frame"+(number++).ToString("D4")+".png"));
            yield return null;yield return null;Debug.Log("ONE_TURN_CAPTURE_READY "+number);Application.Quit();
        }
#endif
        AudioClip Sound(float duration,float hz,bool noise)
        {int count=(int)(22050*duration);var data=new float[count];var random=new System.Random(11);for(int i=0;i<count;i++){float t=i/22050f;data[i]=(noise?((float)random.NextDouble()*2-1):Mathf.Sin(t*hz*6.283f))*Mathf.Pow(1-t/duration,3)*.33f;}var clip=AudioClip.Create("Turn sound",count,1,22050,false);clip.SetData(data,0);return clip;}
        void Reset()
        {initial=TurnRules.Create();visible=initial.Select(a=>a.Copy()).ToList();plan=new List<TurnOrder>();phase=Phase.Plan;index=0;elapsed=0;selected=-1;paused=applied=false;history.Clear();error="";stage.ResetActors();stage.SetOverview(true);stage.UseOverviewCamera=false;}
        void PlayTurn()
        {
            error=TurnRules.Validate(initial,0,plan);if(error.Length>0)return;
            var plans=new Dictionary<int,List<TurnOrder>>{{0,plan}};foreach(var actor in initial.Skip(1))plans.Add(actor.id,TurnRules.BotPlan(actor));
            result=TurnRules.Resolve(initial,plans);visible=initial.Select(a=>a.Copy()).ToList();stage.ResetActors();phase=Phase.Film;index=0;elapsed=0;applied=false;paused=false;selected=-1;history.Clear();
            if(Current!=null)stage.BeginAction(Current);else phase=Phase.Done;
        }
        float Duration(TurnEvent e){return stage.ActionLeadIn+(e.skipped ? .9f : e.killed ? 3.0f : e.kind==TurnCardKind.Cover ? 2.8f : e.kind==TurnCardKind.Aim ? 1.35f : e.kind==TurnCardKind.Guard ? 1.7f : e.kind==TurnCardKind.Heal ? 2.4f : 2.15f);}
        void Update()
        {
            stage.SetPlayback(speed,phase==Phase.Film&&paused);
            if(phase==Phase.Film&&!paused&&Current!=null)
            {
                elapsed+=Time.deltaTime*speed;
                foreach(var animator in stage.GetComponentsInChildren<Animator>())animator.speed=speed;
                if(!soundPlayed&&elapsed>=stage.ActionLeadIn+.43f&&Current.kind==TurnCardKind.Shot&&!Current.skipped){soundPlayed=true;source.PlayOneShot(bang,.5f);}
                if(!applied&&elapsed>=stage.ActionLeadIn+(Current.kind==TurnCardKind.Cover?2.05f:.85f))
                {
                    var e=Current;applied=true;history.Add(e.text);logScroll.y=100000;
                    if(!e.skipped)
                    {var a=visible[e.source];var t=visible[e.target];a.hand.RemoveAll(c=>c.id==e.card);a.aim=e.aimAfter;a.covers=e.sourceCoverAfter;t.covers=e.coverAfter;t.hp=e.hpAfter;t.guards=e.guardsAfter;t.range=e.rangeAfter;t.alive=t.hp>0;}
                    if(e.healing>0)source.PlayOneShot(heal,.5f);
                }
                if(elapsed>=Duration(Current))
                {index++;elapsed=0;applied=false;soundPlayed=false;if(Current!=null)stage.BeginAction(Current);else{phase=Phase.Done;stage.SetOverview(true);}}
            }
            else if(paused)foreach(var animator in stage.GetComponentsInChildren<Animator>())animator.speed=0;
            else foreach(var animator in stage.GetComponentsInChildren<Animator>())animator.speed=1;
            stage.Pose(visible,elapsed,phase==Phase.Film,reducedMotion);
        }
        void Styles()
        {
            var font=Resources.Load<Font>("BangFont");text=new GUIStyle(GUI.skin.label){font=font,fontSize=18,wordWrap=true};text.normal.textColor=ink;
            small=new GUIStyle(text){fontSize=14};small.normal.textColor=muted;title=new GUIStyle(text){fontSize=29,fontStyle=FontStyle.Bold};centered=new GUIStyle(text){alignment=TextAnchor.MiddleCenter};
        }
        void Fill(Rect r,Color c){Color old=GUI.color;GUI.color=c;GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=old;}
        void Label(Rect r,string value,GUIStyle style=null,Color? color=null)
        {var s=style??text;var old=s.normal.textColor;if(color.HasValue)s.normal.textColor=color.Value;GUI.Label(r,value,s);s.normal.textColor=old;}
        bool Button(Rect r,string value,bool enabled=true,bool primary=false,int size=16)
        {
            Fill(r,!enabled?new Color(.12f,.18f,.2f):primary?gold:r.Contains(Event.current.mousePosition)?new Color(.26f,.34f,.37f):new Color(.17f,.24f,.28f));
            var style=new GUIStyle(centered){fontSize=size,fontStyle=FontStyle.Bold,wordWrap=false};style.normal.textColor=primary?dark:enabled?ink:muted;
            bool old=GUI.enabled;GUI.enabled=enabled;bool click=GUI.Button(r,value,style);GUI.enabled=old;return click;
        }
        void Panel(Rect r,string heading)
        {Fill(r,new Color(.085f,.13f,.17f));Fill(new Rect(r.x,r.y,r.width,2),new Color(.28f,.36f,.38f));Label(new Rect(r.x+16,r.y+12,r.width-32,26),heading,small,gold);}
        void OnGUI()
        {
            if(text==null)Styles();float scale=Mathf.Min(Screen.width/W,Screen.height/H);var origin=new Vector2((Screen.width-W*scale)/2,(Screen.height-H*scale)/2);GUI.matrix=Matrix4x4.TRS(origin,Quaternion.identity,Vector3.one*scale);
            Fill(new Rect(0,0,W,H),dark);Label(new Rect(24,17,710,45),"ПЫЛЬ И ПОРОХ",title);Label(new Rect(26,64,830,26),phase==Phase.Plan?"ОДИН ХОД · СОВМЕСТНОЕ ПЛАНИРОВАНИЕ":phase==Phase.Film?"ФАЗА ДЕЙСТВИЙ · ПОСЛЕДОВАТЕЛЬНОЕ ИСПОЛНЕНИЕ":"ОДИН ХОД ЗАВЕРШЁН",small,gold);
            if(Button(new Rect(980,24,130,40),"Правила"))help=true;
            if(Button(new Rect(1124,24,136,40),"Скорость ×"+speed))speed=speed==1?2:1;
            if(Button(new Rect(1274,24,144,40),phase==Phase.Film?(paused?"Продолжить":"Пауза"):"Новый ход")){if(phase==Phase.Film)paused=!paused;else Reset();}
            if(phase==Phase.Plan)Planning();else Movie();
            if(help)Help();GUI.matrix=Matrix4x4.identity;
        }
        void Planning()
        {
            Rect frame=new Rect(20,108,1040,507);GUI.DrawTexture(frame,stage.Picture,ScaleMode.StretchToFill);
            Fill(new Rect(frame.x,frame.y,frame.width,29),new Color(.04f,.07f,.1f,.82f));Label(new Rect(37,112,900,26),"САЛУН · 3 БОТА ГОТОВЫ · выберите карты и посмотрите второй этап",small,ink);
            foreach(var a in visible)
            {
                var point=stage.OnScreen(a.id,frame,2.35f);Rect caption=new Rect(point.x-96,point.y-24,192,50);Fill(caption,new Color(.045f,.08f,.1f,.84f));Label(new Rect(caption.x+5,caption.y+2,182,23),a.name+(a.id==0?" · ВЫ":""),new GUIStyle(centered){fontSize=15},a.id==0?gold:ink);Label(new Rect(caption.x,caption.y+26,192,22),a.hp+" / "+a.maxHp+" здоровья",new GUIStyle(centered){fontSize=12},muted);
                if(selected>=0&&a.id!=0&&a.alive)
                {
                    var candidate=plan.Concat(new[]{new TurnOrder{card=selected,target=a.id}}).ToList();bool valid=TurnRules.Validate(initial,0,candidate).Length==0;
                    if(valid){Rect hit=new Rect(point.x-62,point.y+25,124,125);Fill(new Rect(hit.x,hit.y+hit.height-4,hit.width,3),gold);if(GUI.Button(hit,"",GUIStyle.none))AddCard(selected,a.id);}
                }
            }
            stage.Highlight(-1);
            Panel(new Rect(1080,108,340,507),"ВАША ЦЕПОЧКА КАРТ");
            int spent=plan.Sum(o=>TurnRules.Cost(initial[0].hand.First(c=>c.id==o.card).kind));Label(new Rect(1096,151,308,30),"ЭНЕРГИЯ "+(TurnRules.Energy-spent)+" / "+TurnRules.Energy,new GUIStyle(text){fontSize=23,fontStyle=FontStyle.Bold},gold);
            for(int i=0;i<TurnRules.MaxCards;i++)
            {
                Rect r=new Rect(1096,197+i*43,308,37);Fill(r,new Color(.12f,.20f,.24f));
                if(i<plan.Count){var order=plan[i];var card=initial[0].hand.First(c=>c.id==order.card);string target=TurnRules.NeedsTarget(card.kind)?" → "+initial[order.target].name:"";Label(new Rect(r.x+10,r.y+7,215,28),(i+1)+". "+TurnRules.Names[(int)card.kind]+target,new GUIStyle(text){fontSize=14});if(Button(new Rect(r.x+231,r.y+6,25,25),"↑",i>0,size:14)){var old=plan[i-1];plan[i-1]=plan[i];plan[i]=old;error=TurnRules.Validate(initial,0,plan);}if(Button(new Rect(r.x+266,r.y+6,30,25),"×",size:17))plan.RemoveAt(i);}
                else Label(new Rect(r.x+11,r.y+8,290,25),(i+1)+". —",small);
            }
            Label(new Rect(1096,462,308,55),error.Length>0?error:selected>=0?"Выберите персонажа-цель на сцене.":"Порядок карт важен. Укрытие блокирует один выстрел; перед атакой герой выходит.",small,error.Length>0?new Color(.96f,.46f,.36f):muted);
            if(Button(new Rect(1096,526,308,52),"СМОТРЕТЬ ФАЗУ ДЕЙСТВИЙ",true,true,14))PlayTurn();
            Panel(new Rect(20,630,1040,76),"ПРИМЕР КОМБИНАЦИИ");Label(new Rect(38,665,760,29),"В укрытие → Прицеливание → Выстрел → Выстрел · 3 энергии",new GUIStyle(text){fontSize=16});if(Button(new Rect(844,649,198,39),"Загрузить пример"))plan=TurnRules.CoverExample(initial[0]);
            Panel(new Rect(20,721,1040,157),"КАРТЫ · нажмите карту, затем цель · одна атака может повторяться разными картами");
            for(int i=0;i<initial[0].hand.Count;i++)
            {
                var card=initial[0].hand[i];Rect r=new Rect(33+i*127,761,119,105);bool used=plan.Any(o=>o.card==card.id);Fill(r,used?new Color(.23f,.32f,.32f):selected==card.id?gold:new Color(.81f,.76f,.63f));Color color=used?muted:dark;
                Label(new Rect(r.x+8,r.y+6,103,27),TurnRules.Names[(int)card.kind],new GUIStyle(text){fontSize=13,fontStyle=FontStyle.Bold},color);
                Label(new Rect(r.x+8,r.y+36,103,39),used?"В ЦЕПОЧКЕ":TurnRules.Descriptions[(int)card.kind],new GUIStyle(text){fontSize=11},color);Label(new Rect(r.x+8,r.y+79,103,20),"Энергия: "+TurnRules.Cost(card.kind),new GUIStyle(small){fontSize=11},color);
                if(!used&&GUI.Button(r,"",GUIStyle.none)){if(TurnRules.NeedsTarget(card.kind)){selected=card.id;error="";}else AddCard(card.id,0);}
            }
            Panel(new Rect(1080,630,340,248),"СЦЕНА ПЕРЕД БОЕМ");Label(new Rect(1096,670,304,169),"Сейчас проверяем один ход.\n\nПосле подтверждения карта за картой разыграются выстрелы, уклонения, лечение и реакции персонажей.\n\nВсе эффекты рассчитываются до показа.",small);
        }
        void AddCard(int card,int target)
        {var candidate=plan.Concat(new[]{new TurnOrder{card=card,target=target}}).ToList();error=TurnRules.Validate(initial,0,candidate);if(error.Length==0){plan=candidate;selected=-1;}}
        void Movie()
        {
            Rect frame=new Rect(20,108,1400,680);GUI.DrawTexture(frame,stage.Picture,ScaleMode.StretchToFill);
            Fill(new Rect(frame.x,frame.y,frame.width,39),new Color(.03f,.055f,.075f,.83f));Fill(new Rect(frame.x,frame.y+frame.height-88,frame.width,88),new Color(.025f,.045f,.06f,.9f));
            if(phase==Phase.Film&&Current!=null)
            {
                var e=Current;Label(new Rect(40,117,960,30),"ДЕЙСТВИЕ "+(index+1)+" / "+result.events.Count+" · "+visible[e.source].name+" · "+TurnRules.Names[(int)e.kind],new GUIStyle(text){fontSize=18,fontStyle=FontStyle.Bold},gold);
                if(Button(new Rect(1240,114,162,28),stage.UseOverviewCamera?"Крупный план":"Общий план",size:13))stage.UseOverviewCamera=!stage.UseOverviewCamera;
                string outcome=elapsed<stage.ActionLeadIn?visible[e.source].name+" выходит из укрытия":elapsed<stage.ActionLeadIn+(e.kind==TurnCardKind.Cover?2.02f:.82f)?visible[e.source].name+" использует «"+TurnRules.Names[(int)e.kind]+"»"+(e.source!=e.target?" → "+visible[e.target].name:""):e.text;
                Label(new Rect(42,722,1356,46),outcome,new GUIStyle(title){fontSize=24},e.blocked?new Color(.42f,.85f,.81f):e.hit?gold:ink);
                Fill(new Rect(20,782,1400,5),new Color(.16f,.22f,.25f));Fill(new Rect(20,782,1400*((index+Mathf.Clamp01(elapsed/Duration(e)))/result.events.Count),5),gold);
                if(paused){Fill(new Rect(569,330,302,72),new Color(.04f,.08f,.11f,.86f));Label(new Rect(575,343,290,49),"ПАУЗА",new GUIStyle(centered){fontSize=30,fontStyle=FontStyle.Bold},gold);}
            }
            else
            {
                Label(new Rect(40,117,980,30),"ХОД ЗАВЕРШЁН · "+history.Count+" действий",text,gold);
                Label(new Rect(42,716,890,52),"Повторите показ или измените свою комбинацию.",new GUIStyle(text){fontSize=22});
                if(Button(new Rect(974,720,198,46),"Повторить показ",true,true))PlayTurn();
                if(Button(new Rect(1187,720,214,46),"Изменить карты")){phase=Phase.Plan;visible=initial.Select(a=>a.Copy()).ToList();stage.ResetActors();stage.SetOverview(true);}
            }
            for(int i=0;i<visible.Count;i++)
            {var a=visible[i];Rect r=new Rect(20+i*354,805,338,72);Fill(r,new Color(.09f,.15f,.19f));Label(new Rect(r.x+16,r.y+9,305,27),a.name+(a.id==0?" · ВЫ":""),text,a.id==0?gold:ink);Label(new Rect(r.x+16,r.y+40,305,24),a.alive?a.hp+" / "+a.maxHp+" здоровья"+(a.covers>0?" · укрытие":a.guards>0?" · уклонение готово":""):"ВЫБЫЛ",small,a.alive?muted:new Color(.95f,.47f,.37f));}
        }
        void Help()
        {
            Fill(new Rect(0,0,W,H),new Color(0,0,0,.83f));Panel(new Rect(290,140,860,610),"ДЕМОНСТРАЦИЯ ОДНОГО ХОДА");
            Label(new Rect(326,197,782,42),"Сначала план. Затем сцена действий.",title);
            string[] lines={"3 энергии на несколько карт. Прицеливание стоит 0, остальные карты — 1.","Карты добавляются в цепочку. Стрелка ↑ меняет порядок, × удаляет карту.","Уклонение блокирует попадание. «В укрытие» прячет героя за ящиками от одного выстрела.","Прицеливаться можно в укрытии. Выстрел, разоружение, тоник и карабин требуют выхода.","В этом тесте порядок фиксирован: по карте от каждого персонажа, затем следующий круг карт.","Ловкость с рандомом для инициативы, сила и интеллект будут добавлены позднее.","Онлайн тоже остаётся следующим этапом. Сейчас работает локальная демонстрация с тремя ботами."};
            for(int i=0;i<lines.Length;i++)Label(new Rect(326,260+i*53,780,49),lines[i],new GUIStyle(text){fontSize=17});
            if(Button(new Rect(326,664,788,49),"ПОНЯТНО",true,true))help=false;
        }
        void OnDestroy(){if(stage!=null)Destroy(stage.gameObject);}
    }
}

