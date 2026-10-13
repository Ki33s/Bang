using System.Collections.Generic;
using UnityEngine;
using DustAndPowder.TurnPreview;

namespace DustAndPowder
{
    public class TurnStage : MonoBehaviour
    {
        public RenderTexture Picture { get; private set; }
        public Camera FilmCamera { get; private set; }
        public int FocusSource { get; private set; } = -1;
        public int FocusTarget { get; private set; } = -1;
        public bool UseOverviewCamera;
        public float ActionLeadIn { get; private set; }
        class ActorRig
        {
            public Transform root,body,arm,otherArm,head;public GameObject flash,glow,ring;public Vector3 spot;
            public bool alive=true,hidden,leaving,actionStarted,coverMoved,coverBack,coverLeft,coverSettled,exitRising;public Quaternion facing;public Material ringMaterial;
            public Vector3 coverSpot,coverWaypoint,coverCorner,lastPosition;public float dustClock;
            public Animator animator;public int coverLayer;public bool asset, fired, reacted;public Transform muzzle;
        }
        readonly List<ActorRig> rigs=new List<ActorRig>();
        readonly List<Material> materials=new List<Material>();
        GameObject environment,actorsRoot,bullet; LineRenderer tracer; Material template;
        TurnEffects effects;bool muzzlePlayed,impactPlayed,healingPlayed;
        TurnEvent current;float elapsed;bool overview=true;int highlighted=-1;
        Vector3 cameraGoal,lookGoal;bool snapCamera,impactFocused;
        Color sand=new Color(.5f,.37f,.29f),wood=new Color(.35f,.21f,.18f),gold=new Color(.97f,.67f,.28f),teal=new Color(.29f,.66f,.64f);
        static readonly Color[] Coats={new Color(.62f,.22f,.19f),new Color(.25f,.28f,.36f),new Color(.19f,.47f,.42f),new Color(.52f,.35f,.18f)};
        Material Mat(Color color)
        {var m=new Material(template);m.color=color;materials.Add(m);return m;}
        GameObject Shape(string name,PrimitiveType type,Transform parent,Vector3 pos,Vector3 size,Color color)
        {
            var g=GameObject.CreatePrimitive(type);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=pos;g.transform.localScale=size;
            var collider=g.GetComponent<Collider>();if(collider!=null)Destroy(collider);
            var renderer=g.GetComponent<MeshRenderer>();renderer.sharedMaterial=Mat(color);renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;return g;
        }
        public void Initialize()
        {
            template=Resources.Load<Material>("BangToon");if(template==null)template=new Material(Shader.Find("Bang/Toon"));
            effects=new GameObject("Combat Effects").AddComponent<TurnEffects>();effects.transform.SetParent(transform,false);
            Picture=new RenderTexture(1536,748,24,RenderTextureFormat.ARGB32);Picture.antiAliasing=2;Picture.Create();
            FilmCamera=new GameObject("Action Camera").AddComponent<Camera>();FilmCamera.transform.SetParent(transform,false);FilmCamera.targetTexture=Picture;
            FilmCamera.clearFlags=CameraClearFlags.Skybox;FilmCamera.backgroundColor=new Color(.72f,.54f,.4f);FilmCamera.fieldOfView=43;FilmCamera.nearClipPlane=.08f;FilmCamera.farClipPlane=180;
            BuildEnvironment();ResetActors();
            SetOverview(true);UpdateCamera(1,true);
        }
        void BuildEnvironment()
        {
            environment=new GameObject("Western Film Set");environment.transform.SetParent(transform,false);var root=environment.transform;
            RenderSettings.skybox=Resources.Load<Material>("WesternSky");
            Shape("Desert floor",PrimitiveType.Cube,root,new Vector3(0,-.14f,0),new Vector3(160,.2f,160),new Color(.69f,.52f,.36f));
            Shape("Dusty main street",PrimitiveType.Cube,root,new Vector3(0,-.025f,0),new Vector3(13,.03f,58),new Color(.63f,.46f,.30f));
            for(int side=-1;side<=1;side+=2)
            {
                Shape("Wooden boardwalk",PrimitiveType.Cube,root,new Vector3(side*7.8f,.08f,0),new Vector3(2.5f,.16f,43),wood);
                for(int j=0;j<32;j++)Shape("Boardwalk plank",PrimitiveType.Cube,root,new Vector3(side*7.8f,.175f,-20+j*1.3f),new Vector3(2.48f,.04f,1.23f),new Color(.47f+j%3*.02f,.34f,.23f));
                Scenery("Store",new Vector3(side*13,0,-13),6.2f,side<0?90:270);
                Scenery(side<0?"Jail":"House",new Vector3(side*13,0,1),7.2f,side<0?90:270);
                Scenery("House",new Vector3(side*13,0,17),7.6f,side<0?90:270);
                Scenery("Hitch",new Vector3(side*8,.18f,6),1.2f,0);
                for(int j=0;j<3;j++)Scenery("Barrel",new Vector3(side*7.1f,.18f,-8+j*6),.95f,j*23);
                for(int j=0;j<3;j++)Scenery("Fence",new Vector3(side*22,0,-12+j*12),1.4f,90);
            }
            Scenery("Saloon",new Vector3(0,0,14),7.5f,180);
            Scenery("Store",new Vector3(-8,0,-27),5.8f,0);
            Scenery("House",new Vector3(8,0,-29),6.5f,0);
            Scenery("Windmill",new Vector3(-22,0,28),13,30);
            Scenery("Cart",new Vector3(8.5f,0,-19),2.1f,25);
            Scenery("Hay",new Vector3(-7.3f,.18f,11),.9f,25);
            Scenery("Hay",new Vector3(-8.3f,.18f,11.4f),.7f,7);
            Scenery("Lantern",new Vector3(-7.8f,1.65f,6),.5f,0);
            for(int i=0;i<12;i++)
            {
                float angle=i*Mathf.PI*2/12;var p=new Vector3(Mathf.Sin(angle)*52,0,Mathf.Cos(angle)*52);
                Scenery("Butte",p,9+i%4*3,i*29);
                Scenery("Cactus",p*.65f,2.4f+i%3*.45f,i*43);
            }
            for(int i=0;i<4;i++)
            {
                var spot=Seat(i);var outward=spot.normalized;var right=Quaternion.LookRotation(-outward)*Vector3.right;
                Scenery("Crate",spot+outward*.8f+right*.38f,1.4f,i*90);
                Scenery("Crate",spot+outward*.8f-right*.65f,1.05f,i*90+4);
            }
        }
        static Vector3 Seat(int id){float angle=Mathf.PI*2*id/4;return new Vector3(Mathf.Sin(angle)*3.8f,0,-Mathf.Cos(angle)*3.8f);}
        void Scenery(string asset,Vector3 at,float height,float yaw)
        {
            var prefab=Resources.Load<GameObject>("Turn/"+asset);if(prefab==null)return;
            var g=Instantiate(prefab,environment.transform);g.name=asset;g.transform.localRotation=Quaternion.Euler(0,yaw,0);
            var bounds=BoundsOf(g);g.transform.localScale*=height/Mathf.Max(.01f,bounds.size.y);bounds=BoundsOf(g);
            g.transform.position+=at-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
        }
        void House(Transform root,Vector3 origin,float width,float height,string sign,Color color)
        {
            var h=new GameObject("Frontier building").transform;h.SetParent(root,false);h.localPosition=origin;
            Shape("Wooden facade",PrimitiveType.Cube,h,new Vector3(0,height/2,0),new Vector3(width,height,1.5f),color);
            Shape("Roof",PrimitiveType.Cube,h,new Vector3(0,height+.1f,0),new Vector3(width+.3f,.2f,1.8f),wood);
            for(int i=0;i<(int)width;i++)Shape("Facade board",PrimitiveType.Cube,h,new Vector3(-width/2+i+.5f,height/2,-.77f),new Vector3(.025f,height,.035f),wood);
            Shape("Door",PrimitiveType.Cube,h,new Vector3(0,.9f,-.82f),new Vector3(1.3f,1.8f,.08f),new Color(.15f,.19f,.22f));
            for(int i=-1;i<=1;i+=2)
            {Shape("Window frame",PrimitiveType.Cube,h,new Vector3(i*width*.32f,1.8f,-.83f),new Vector3(1.3f,1.4f,.08f),wood);Shape("Lit window",PrimitiveType.Cube,h,new Vector3(i*width*.32f,1.8f,-.90f),new Vector3(1.1f,1.2f,.06f),new Color(.8f,.6f,.29f));Shape("Window cross",PrimitiveType.Cube,h,new Vector3(i*width*.32f,1.8f,-.95f),new Vector3(.08f,1.2f,.04f),wood);}
            if(sign.Length>0)
            {
                Shape("Saloon sign",PrimitiveType.Cube,h,new Vector3(0,3.05f,-.88f),new Vector3(4.2f,.7f,.15f),new Color(.16f,.23f,.25f));
                var text=new GameObject("Saloon lettering").AddComponent<TextMesh>();text.transform.SetParent(h,false);text.transform.localPosition=new Vector3(0,3.06f,-.99f);text.transform.localRotation=Quaternion.Euler(0,180,0);
                text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.text=sign;text.fontSize=54;text.characterSize=.11f;text.color=new Color(.99f,.78f,.48f);
                var font=Resources.Load<Font>("BangFont");text.font=font;text.GetComponent<MeshRenderer>().sharedMaterial=font.material;
                Shape("Porch roof",PrimitiveType.Cube,h,new Vector3(0,2.45f,-1.5f),new Vector3(width+.3f,.16f,2.0f),wood);
                for(int i=-1;i<=1;i+=2)Shape("Porch post",PrimitiveType.Cylinder,h,new Vector3(i*(width/2-.35f),1.25f,-2.25f),new Vector3(.11f,1.25f,.11f),new Color(.52f,.36f,.25f));
            }
        }
        void Barrel(Transform root,Vector3 at)
        {Shape("Barrel",PrimitiveType.Cylinder,root,at+Vector3.up*.44f,new Vector3(.6f,.44f,.6f),wood);for(int i=0;i<2;i++)Shape("Barrel hoop",PrimitiveType.Cylinder,root,at+Vector3.up*(.22f+i*.42f),new Vector3(.64f,.025f,.64f),new Color(.19f,.22f,.24f));}
        public void ResetActors()
        {
            if(effects!=null)effects.Clear();
            if(actorsRoot!=null)Destroy(actorsRoot);rigs.Clear();actorsRoot=new GameObject("Cast");actorsRoot.transform.SetParent(transform,false);
            for(int i=0;i<4;i++)rigs.Add(CreateActor(i));
            if(bullet!=null)Destroy(bullet);if(tracer!=null)Destroy(tracer.gameObject);
            bullet=Shape("Bullet",PrimitiveType.Sphere,transform,Vector3.zero,Vector3.one*.1f,gold);bullet.SetActive(false);
            var line=new GameObject("Shot trace");line.transform.SetParent(transform,false);tracer=line.AddComponent<LineRenderer>();tracer.sharedMaterial=Mat(gold);tracer.positionCount=2;tracer.startWidth=tracer.endWidth=.035f;tracer.enabled=false;
            current=null;FocusSource=FocusTarget=-1;overview=true;highlighted=-1;ActionLeadIn=0;
        }
        ActorRig CreateActor(int id)
        {
            var rig=new ActorRig();rig.spot=Seat(id);
            rig.root=new GameObject("Character "+id).transform;rig.root.SetParent(actorsRoot.transform,false);rig.root.localPosition=rig.spot;rig.facing=Quaternion.LookRotation(-rig.spot.normalized);rig.root.localRotation=rig.facing;
            rig.coverSpot=rig.spot+rig.spot.normalized*2.0f+(rig.facing*Vector3.right)*.12f;rig.coverWaypoint=rig.spot+(rig.facing*Vector3.right)*1.55f;rig.coverCorner=rig.coverWaypoint+rig.spot.normalized*2.0f;
            rig.lastPosition=rig.spot;
            var supplied=Resources.Load<GameObject>("Turn/Actor"+id);
            if(supplied!=null)
            {
                var model=Instantiate(supplied,rig.root);model.transform.localPosition=Vector3.zero;model.transform.localRotation=Quaternion.identity;
                var bounds=BoundsOf(model);model.transform.localScale*=2.1f/Mathf.Max(.1f,bounds.size.y);
                rig.animator=model.GetComponentInChildren<Animator>();rig.animator.runtimeAnimatorController=Resources.Load<RuntimeAnimatorController>("Turn/ActionController");rig.animator.applyRootMotion=false;rig.animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;rig.animator.Update(0);
                rig.coverLayer=rig.animator.GetLayerIndex("CoverLowReady");
                rig.asset=true;rig.arm=rig.animator.GetBoneTransform(HumanBodyBones.RightHand);
                var revolver=Resources.Load<GameObject>("Turn/Revolver");
                // Calibrate the socket in the imported aiming pose, then keep it attached to the wrist.
                // Humanoid hand axes differ from the weapon's +Z barrel / +Y top axes.
                rig.animator.Play("Aim",0,.2f);rig.animator.Update(0);
                var grip=new GameObject("Revolver grip").transform;grip.SetParent(rig.arm,false);
                grip.localPosition=new Vector3(.085f,.015f,-.03f);
                grip.rotation=Quaternion.LookRotation(rig.root.forward,rig.root.up);
                Transform weaponRoot=grip;
                if(revolver!=null){var weapon=Instantiate(revolver,grip);weapon.transform.localPosition=Vector3.zero;weapon.transform.localRotation=Quaternion.identity;weapon.transform.localScale=Vector3.one*.85f;weaponRoot=weapon.transform;}
                rig.muzzle=new GameObject("Weapon muzzle").transform;rig.muzzle.SetParent(weaponRoot,false);rig.muzzle.localPosition=new Vector3(0,.065f,.333f);
                rig.animator.Play("Idle",0,0);rig.animator.Update(0);
                rig.flash=Shape("Muzzle flash",PrimitiveType.Sphere,rig.muzzle,Vector3.zero,Vector3.one*.18f,gold);rig.flash.SetActive(false);
                rig.glow=Shape("Healing effect",PrimitiveType.Sphere,rig.root,new Vector3(0,1.1f,.5f),Vector3.one*.15f,teal);rig.glow.SetActive(false);
                rig.ring=Shape("Action marker",PrimitiveType.Cylinder,rig.root,new Vector3(0,.016f,0),new Vector3(1.4f,.008f,1.4f),gold);rig.ringMaterial=rig.ring.GetComponent<MeshRenderer>().sharedMaterial;rig.ring.SetActive(false);
                return rig;
            }
            Color coat=Coats[id],skin=id==3?new Color(.55f,.34f,.25f):new Color(.83f,.59f,.4f),hair=new Color(.18f,.15f,.15f),pants=new Color(.18f,.23f,.28f);
            Shape("Foot shadow",PrimitiveType.Cylinder,rig.root,new Vector3(0,.015f,0),new Vector3(1.0f,.012f,.8f),new Color(.24f,.23f,.23f));
            rig.ring=Shape("Action marker",PrimitiveType.Cylinder,rig.root,new Vector3(0,.005f,0),new Vector3(1.22f,.009f,1.0f),gold);rig.ring.SetActive(false);rig.ringMaterial=rig.ring.GetComponent<MeshRenderer>().sharedMaterial;
            for(int side=-1;side<=1;side+=2)
            {Shape("Boot",PrimitiveType.Cube,rig.root,new Vector3(side*.18f,.16f,.12f),new Vector3(.23f,.3f,.42f),new Color(.2f,.16f,.15f));Shape("Trouser leg",PrimitiveType.Capsule,rig.root,new Vector3(side*.18f,.57f,0),new Vector3(.24f,.65f,.24f),pants);}
            rig.body=new GameObject("Torso pivot").transform;rig.body.SetParent(rig.root,false);rig.body.localPosition=new Vector3(0,.88f,0);
            Shape("Coat",PrimitiveType.Capsule,rig.body,new Vector3(0,.35f,0),new Vector3(.73f,.57f,.43f),coat);
            Shape("Vest",PrimitiveType.Cube,rig.body,new Vector3(0,.35f,.205f),new Vector3(.32f,.62f,.09f),new Color(.2f,.25f,.27f));
            Shape("Belt",PrimitiveType.Cube,rig.body,new Vector3(0,.06f,0),new Vector3(.69f,.1f,.45f),hair);Shape("Buckle",PrimitiveType.Cube,rig.body,new Vector3(0,.06f,.25f),new Vector3(.14f,.12f,.03f),gold);
            if(id==0)
            {var star=Shape("Sheriff badge",PrimitiveType.Cube,rig.body,new Vector3(-.17f,.52f,.28f),new Vector3(.12f,.12f,.02f),gold);star.transform.localRotation=Quaternion.Euler(0,0,45);}
            Shape("Neck",PrimitiveType.Cylinder,rig.body,new Vector3(0,.77f,0),new Vector3(.19f,.11f,.19f),skin);
            rig.head=new GameObject("Head").transform;rig.head.SetParent(rig.body,false);rig.head.localPosition=new Vector3(0,1.03f,0);
            Shape("Face",PrimitiveType.Sphere,rig.head,Vector3.zero,new Vector3(.5f,.57f,.47f),skin);
            Shape("Hair back",PrimitiveType.Sphere,rig.head,new Vector3(0,.035f,-.13f),new Vector3(.51f,.51f,.3f),hair);
            for(int side=-1;side<=1;side+=2)
            {Shape("Eye",PrimitiveType.Sphere,rig.head,new Vector3(side*.105f,.04f,.205f),new Vector3(.055f,.038f,.032f),new Color(.05f,.07f,.1f));var brow=Shape("Brow",PrimitiveType.Cube,rig.head,new Vector3(side*.105f,.108f,.21f),new Vector3(.12f,.026f,.03f),hair);brow.transform.localRotation=Quaternion.Euler(0,0,-side*10);}
            Shape("Nose",PrimitiveType.Sphere,rig.head,new Vector3(0,-.03f,.245f),new Vector3(.09f,.115f,.11f),skin);
            Shape("Mouth",PrimitiveType.Cube,rig.head,new Vector3(0,-.13f,.20f),new Vector3(.1f,.017f,.025f),hair);
            if(id==1||id==3)Shape("Moustache",PrimitiveType.Cube,rig.head,new Vector3(0,-.09f,.24f),new Vector3(.22f,.045f,.04f),hair);
            if(id==0){Shape("Hair left",PrimitiveType.Capsule,rig.head,new Vector3(-.24f,-.08f,-.06f),new Vector3(.12f,.32f,.12f),hair);Shape("Hair right",PrimitiveType.Capsule,rig.head,new Vector3(.24f,-.08f,-.06f),new Vector3(.12f,.32f,.12f),hair);}
            Color hat=id==0?new Color(.58f,.38f,.23f):id==2?new Color(.22f,.35f,.33f):new Color(.23f,.24f,.28f);
            Shape("Hat brim",PrimitiveType.Cylinder,rig.head,new Vector3(0,.27f,0),new Vector3(.88f,.035f,.69f),hat);Shape("Hat crown",PrimitiveType.Cylinder,rig.head,new Vector3(0,.43f,0),new Vector3(.52f,.15f,.44f),hat);Shape("Hat band",PrimitiveType.Cylinder,rig.head,new Vector3(0,.33f,0),new Vector3(.53f,.035f,.45f),coat);
            rig.arm=Arm(rig.body,.4f,skin,coat);rig.otherArm=Arm(rig.body,-.4f,skin,coat);
            Shape("Revolver barrel",PrimitiveType.Cube,rig.arm,new Vector3(0,-.64f,.14f),new Vector3(.09f,.12f,.40f),new Color(.57f,.62f,.64f));Shape("Revolver grip",PrimitiveType.Cube,rig.arm,new Vector3(0,-.72f,-.04f),new Vector3(.10f,.20f,.1f),wood);
            rig.flash=Shape("Muzzle flash",PrimitiveType.Sphere,rig.arm,new Vector3(0,-.64f,.4f),Vector3.one*.17f,gold);rig.flash.SetActive(false);
            rig.glow=Shape("Medicine glow",PrimitiveType.Sphere,rig.root,new Vector3(0,1.1f,.65f),Vector3.one*.16f,teal);rig.glow.SetActive(false);
            return rig;
        }
        static Bounds BoundsOf(GameObject g){var rs=g.GetComponentsInChildren<Renderer>();var b=rs.Length>0?rs[0].bounds:new Bounds(Vector3.zero,Vector3.one);foreach(var r in rs)b.Encapsulate(r.bounds);return b;}
        Transform Arm(Transform body,float x,Color skin,Color coat)
        {
            var arm=new GameObject("Shoulder").transform;arm.SetParent(body,false);arm.localPosition=new Vector3(x,.62f,0);
            Shape("Sleeve",PrimitiveType.Capsule,arm,new Vector3(0,-.22f,0),new Vector3(.23f,.31f,.23f),coat);Shape("Hand",PrimitiveType.Sphere,arm,new Vector3(0,-.56f,0),Vector3.one*.18f,skin);return arm;
        }
        public void SetOverview(bool snap=false){overview=true;current=null;FocusSource=FocusTarget=-1;cameraGoal=new Vector3(0,10.8f,-12.8f);lookGoal=new Vector3(0,.7f,0);snapCamera=snap;}
        public void Highlight(int id){highlighted=id;}
        public Vector2 OnScreen(int id,Rect rect,float height=1.25f)
        {Vector3 p=FilmCamera.WorldToViewportPoint(rigs[id].spot+Vector3.up*height);return new Vector2(rect.x+p.x*rect.width,rect.y+(1-p.y)*rect.height);}
        public void BeginAction(TurnEvent e)
        {
            current=e;elapsed=0;overview=false;FocusSource=e.source;FocusTarget=e.target;snapCamera=true;impactFocused=muzzlePlayed=impactPlayed=healingPlayed=false;
            foreach(var rig in rigs){rig.fired=rig.reacted=rig.actionStarted=rig.coverMoved=rig.coverBack=rig.coverLeft=rig.coverSettled=rig.exitRising=false;if(rig.asset&&rig.alive)rig.animator.CrossFadeInFixedTime(rig.hidden?"CoverIdle":"Idle",.07f,0,0);}
            var sourceRig=rigs[e.source];
            sourceRig.leaving=!e.skipped&&sourceRig.hidden&&e.kind!=TurnCardKind.Aim&&e.kind!=TurnCardKind.Cover&&e.kind!=TurnCardKind.Guard;
            ActionLeadIn=sourceRig.leaving?1.6f:0;
            if(sourceRig.asset&&!e.skipped){if(sourceRig.leaving)sourceRig.animator.CrossFadeInFixedTime("CoverMove",.08f,0,0);else StartSourceAction(sourceRig,e);}
            var a=rigs[e.source].spot;var b=rigs[e.target].spot;
            if(e.source!=e.target)
            {Vector3 forward=(b-a).normalized,right=Vector3.Cross(Vector3.up,forward);cameraGoal=(a+b)*.5f+right*4.5f+Vector3.up*2.45f;lookGoal=(a+b)*.5f+Vector3.up*1.15f;}
            else if(e.kind==TurnCardKind.Cover||sourceRig.hidden){var outwards=a.normalized;cameraGoal=a+(sourceRig.facing*Vector3.right)*3.8f+outwards*1.8f+Vector3.up*2.0f;lookGoal=Vector3.Lerp(a,sourceRig.coverSpot,.55f)+Vector3.up*.9f;}
            else{Vector3 outwards=a.normalized;cameraGoal=a-outwards*3.0f+Vector3.Cross(Vector3.up,outwards)*.8f+Vector3.up*1.8f;lookGoal=a+Vector3.up*1.05f;}
            UpdateCamera(1,true);
        }
        void StartSourceAction(ActorRig rig,TurnEvent e)
        {
            rig.actionStarted=true;
            string clip=e.kind==TurnCardKind.Cover?"CoverEnter":rig.hidden?"CoverIdle":e.kind==TurnCardKind.Shot||e.kind==TurnCardKind.Aim?"Aim":e.kind==TurnCardKind.Guard?"Guard":e.kind==TurnCardKind.Heal?"Heal":"Equip";
            rig.animator.CrossFadeInFixedTime(clip,.07f,0,0);
        }
        static float Ease(float value){return Mathf.SmoothStep(0,1,Mathf.Clamp01(value));}
        public void SetPlayback(float speed,bool paused){effects.SetPlayback(speed,paused);}
        Vector3 EnterCoverPosition(ActorRig rig,float time)
        {return time<1?Vector3.Lerp(rig.spot,rig.coverWaypoint,Ease((time-.55f)/.45f)):time<1.5f?Vector3.Lerp(rig.coverWaypoint,rig.coverCorner,Ease((time-1)/.5f)):Vector3.Lerp(rig.coverCorner,rig.coverSpot,Ease((time-1.5f)/.45f));}
        public void Pose(List<TurnActor> actors,float actionTime,bool film,bool reducedMotion)
        {
            float totalTime=actionTime;actionTime=Mathf.Max(0,totalTime-ActionLeadIn);elapsed=actionTime;
            if(film&&current!=null&&current.kind==TurnCardKind.Shot&&!current.skipped&&actionTime>.65f&&!impactFocused)
            {
                impactFocused=true;var a=rigs[current.source].spot;var b=rigs[current.target].spot;var forward=(b-a).normalized;
                if(current.coverBlocked){var target=rigs[current.target];cameraGoal=target.spot+(target.facing*Vector3.right)*3.6f-b.normalized*1.8f+Vector3.up*2.0f;lookGoal=Vector3.Lerp(target.spot,target.coverSpot,.5f)+Vector3.up*.9f;}
                else{cameraGoal=b-forward*3.4f+Vector3.Cross(Vector3.up,forward)*1.25f+Vector3.up*1.8f;lookGoal=b+Vector3.up*1.05f;}snapCamera=true;
            }
            for(int i=0;i<rigs.Count;i++)
            {
                var rig=rigs[i];var actor=actors[i];rig.alive=actor.alive;rig.root.localPosition=rig.hidden?rig.coverSpot:rig.spot;rig.root.localRotation=rig.facing;
                if(rig.asset)
                {
                    rig.flash.SetActive(false);rig.glow.SetActive(false);rig.ring.SetActive(i==highlighted||film&&current!=null&&(i==current.source||i==current.target));rig.ringMaterial.color=i==FocusTarget&&FocusTarget!=FocusSource?new Color(.72f,.34f,.29f):gold;
                    if(film&&current!=null)
                    {
                        if(i==current.source&&!current.skipped)
                        {
                            if(rig.leaving)
                            {
                                rig.root.localPosition=totalTime<.3f?Vector3.Lerp(rig.coverSpot,rig.coverCorner,Ease(totalTime/.3f)):totalTime<.65f?Vector3.Lerp(rig.coverCorner,rig.coverWaypoint,Ease((totalTime-.3f)/.35f)):Vector3.Lerp(rig.coverWaypoint,rig.spot,Ease((totalTime-.65f)/.3f));
                                if(totalTime>.95f&&!rig.exitRising){rig.exitRising=true;rig.animator.CrossFadeInFixedTime("CoverExit",.08f,0,0);}
                                if(totalTime>=ActionLeadIn){rig.hidden=rig.leaving=false;StartSourceAction(rig,current);}
                            }
                            if(current.kind==TurnCardKind.Cover)
                            {
                                rig.root.localPosition=EnterCoverPosition(rig,totalTime);
                                if(totalTime>.55f&&!rig.coverMoved){rig.coverMoved=true;rig.animator.CrossFadeInFixedTime("CoverMove",.08f,0,0);}
                                if(totalTime>1&&!rig.coverBack){rig.coverBack=true;rig.animator.CrossFadeInFixedTime("CoverBack",.08f,0,0);}
                                if(totalTime>1.5f&&!rig.coverLeft){rig.coverLeft=true;rig.animator.CrossFadeInFixedTime("CoverLeft",.08f,0,0);}
                                if(totalTime>1.95f&&!rig.coverSettled){rig.coverSettled=rig.hidden=true;rig.animator.CrossFadeInFixedTime("CoverIdle",.1f,0,0);}
                            }
                        }
                        if(i==current.source&&current.source!=current.target)rig.root.rotation=Quaternion.LookRotation((rigs[current.target].spot-rig.spot).normalized);
                        if(i==current.source&&current.kind==TurnCardKind.Shot&&!current.skipped&&totalTime>=ActionLeadIn){if(actionTime>.2f&&!rig.fired){rig.animator.CrossFadeInFixedTime("Fire",.02f,0,0);rig.fired=true;}if(actionTime>.42f&&!muzzlePlayed){muzzlePlayed=true;effects.Play("Muzzle",rig.muzzle.position,rig.muzzle.rotation,.32f);}}
                        if(i==current.source&&current.kind==TurnCardKind.Heal)rig.glow.SetActive(actionTime>.4f&&actionTime<1.2f);
                        if(i==current.target&&current.kind==TurnCardKind.Shot&&!current.skipped&&actionTime>.65f&&!rig.reacted)
                        {rig.reacted=true;if(current.coverBlocked)rig.animator.CrossFadeInFixedTime("CoverIdle",.05f,0,0);else if(current.blocked)rig.animator.CrossFadeInFixedTime("Dodge",.05f,0,0);else if(current.hit)rig.animator.CrossFadeInFixedTime(current.killed?"Death":"Hit",.05f,0,0);}
                        if(i==current.target&&current.blocked&&!current.coverBlocked&&!reducedMotion&&actionTime>.65f&&actionTime<1.3f)rig.root.localPosition=(rig.hidden?rig.coverSpot:rig.spot)+rig.root.right*Mathf.Sin((actionTime-.65f)/.65f*Mathf.PI)*.35f;
                    }
                    if(actor.alive&&Vector3.Distance(rig.lastPosition,rig.root.position)>.004f)
                    {rig.dustClock+=Time.deltaTime;if(rig.dustClock>.16f){rig.dustClock=0;effects.Play("Footstep",rig.root.position+Vector3.up*.04f,Quaternion.identity,.7f);}}
                    rig.lastPosition=rig.root.position;
                    if(rig.coverLayer>=0){float low=rig.hidden&&!rig.leaving||film&&current!=null&&i==current.source&&current.kind==TurnCardKind.Cover&&totalTime>.6f?1:0;rig.animator.SetLayerWeight(rig.coverLayer,Mathf.MoveTowards(rig.animator.GetLayerWeight(rig.coverLayer),low,Time.deltaTime*8));}
                    continue;
                }
                rig.body.localRotation=Quaternion.identity;rig.arm.localRotation=Quaternion.Euler(5,0,-7);rig.otherArm.localRotation=Quaternion.Euler(5,0,7);rig.flash.SetActive(false);rig.glow.SetActive(false);
                rig.ring.SetActive(i==highlighted||film&&current!=null&&(i==current.source||i==current.target));rig.ringMaterial.color=i==FocusTarget&&FocusTarget!=FocusSource?new Color(.72f,.34f,.29f):gold;
                if(!actor.alive){rig.root.localRotation=rig.facing*Quaternion.Euler(75,0,0);rig.root.localPosition=rig.spot+Vector3.up*.1f;continue;}
                if(!film||current==null){if(!reducedMotion)rig.body.localRotation=Quaternion.Euler(0,0,Mathf.Sin(Time.time*1.4f+i)*.6f);continue;}
                if(i==current.source)
                {
                    if(current.source!=current.target)rig.root.rotation=Quaternion.LookRotation((rigs[current.target].spot-rig.spot).normalized);
                    switch(current.kind)
                    {
                        case TurnCardKind.Shot:
                            float aim=Mathf.Clamp01(actionTime/.3f);rig.arm.localRotation=Quaternion.Euler(-80*aim,0,-7);rig.otherArm.localRotation=Quaternion.Euler(-30*aim,0,7);
                            rig.flash.SetActive(actionTime>.42f&&actionTime<.57f&&!current.skipped);
                            if(!reducedMotion&&actionTime>.42f&&actionTime<.7f)rig.body.localRotation=Quaternion.Euler(-Mathf.Sin((actionTime-.42f)/.28f*Mathf.PI)*8,0,0);break;
                        case TurnCardKind.Guard:rig.body.localRotation=Quaternion.Euler(13,0,0);rig.arm.localRotation=Quaternion.Euler(-60,0,35);rig.otherArm.localRotation=Quaternion.Euler(-60,0,-35);break;
                        case TurnCardKind.Aim:rig.arm.localRotation=Quaternion.Euler(-78,0,-6);rig.head.localRotation=Quaternion.Euler(0,0,-6);break;
                        case TurnCardKind.Heal:rig.arm.localRotation=Quaternion.Euler(-118,0,-6);rig.glow.SetActive(actionTime>.4f&&actionTime<1.1f);break;
                        case TurnCardKind.Disarm:rig.arm.localRotation=Quaternion.Euler(-65,0,-6);break;
                        case TurnCardKind.Weapon:rig.arm.localRotation=Quaternion.Euler(-46,0,-6);rig.otherArm.localRotation=Quaternion.Euler(-46,0,6);break;
                    }
                }
                else rig.head.localRotation=Quaternion.identity;
                if(i==current.target&&current.source!=current.target&&actionTime>.6f&&actionTime<1.2f)
                {
                    if(current.blocked){rig.body.localRotation=Quaternion.Euler(0,0,-22);if(!reducedMotion)rig.root.localPosition=rig.spot+rig.root.right*Mathf.Sin((actionTime-.6f)/.6f*Mathf.PI)*.35f;}
                    else if(current.hit&&!reducedMotion)rig.body.localRotation=Quaternion.Euler(-Mathf.Sin((actionTime-.6f)/.6f*Mathf.PI)*16,0,0);
                }
            }
            tracer.enabled=false;bullet.SetActive(false);
            if(film&&current!=null&&!current.skipped)
            {
                var src=rigs[current.source];var target=rigs[current.target];
                if(current.kind==TurnCardKind.Heal&&actionTime>.4f&&!healingPlayed){healingPlayed=true;effects.Play("Heal",src.root.position,Quaternion.identity,.55f);}
                if(current.kind==TurnCardKind.Shot&&actionTime>.68f&&!impactPlayed)
                {
                    impactPlayed=true;var direction=(target.spot-src.spot).normalized;
                    if(current.hit)effects.Play("Blood",target.root.position+Vector3.up*1.25f,Quaternion.LookRotation(direction),.6f);
                    else if(current.coverBlocked)effects.Play("Wood",target.spot+target.spot.normalized*.2f+Vector3.up*1.05f,Quaternion.LookRotation(-direction),.65f);
                    else effects.Play("Dirt",target.spot+Vector3.Cross(Vector3.up,direction)*1.15f+direction*.9f+Vector3.up*.03f,Quaternion.identity,.55f);
                }
            }
            if(film&&current!=null&&current.kind==TurnCardKind.Shot&&!current.skipped&&actionTime>.43f&&actionTime<.78f)
            {
                var source=rigs[current.source];var a=source.asset?source.muzzle.position:source.arm.TransformPoint(new Vector3(0,-.64f,.4f));var b=rigs[current.target].spot+Vector3.up*1.4f;
                if(current.coverBlocked)b=rigs[current.target].spot+rigs[current.target].spot.normalized*.2f+Vector3.up*1.05f;
                else if(!current.hit)b+=Vector3.right*1.1f;
                float t=(actionTime-.43f)/.35f;Vector3 point=Vector3.Lerp(a,b,t);bullet.SetActive(true);bullet.transform.position=point;
                tracer.enabled=true;tracer.SetPosition(0,Vector3.Lerp(a,b,Mathf.Max(0,t-.16f)));tracer.SetPosition(1,point);
            }
            UpdateCamera(Time.deltaTime,snapCamera);snapCamera=false;
        }
        void UpdateCamera(float dt,bool snap)
        {
            Vector3 destination=overview||UseOverviewCamera?new Vector3(0,10.8f,-12.8f):cameraGoal;
            Vector3 target=overview||UseOverviewCamera?new Vector3(0,.7f,0):lookGoal;
            FilmCamera.transform.position=snap?destination:Vector3.Lerp(FilmCamera.transform.position,destination,dt*7);
            var rotation=Quaternion.LookRotation(target-FilmCamera.transform.position);FilmCamera.transform.rotation=snap?rotation:Quaternion.Slerp(FilmCamera.transform.rotation,rotation,dt*9);
        }
        void OnDestroy(){if(Picture!=null){Picture.Release();Destroy(Picture);}foreach(var m in materials)Destroy(m);}
    }
}
