using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class PrepareTurnAssets
{
    public static void Inventory()
    {
        var lines=new List<string>();
        foreach(string path in AssetDatabase.GetAllAssetPaths().Where(p=>(p.StartsWith("Assets/PistolAnimsetPro")||p.StartsWith("Assets/RifleAnimsetPro"))&&p.EndsWith(".fbx",StringComparison.OrdinalIgnoreCase)))
        {
            lines.Add("FBX "+path);
            foreach(var clip in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview__")))lines.Add("CLIP "+clip.name+" | "+clip.length+" | human="+clip.isHumanMotion);
        }
        foreach(string path in AssetDatabase.GetAllAssetPaths().Where(p=>p.StartsWith("Assets/PolygonWestern/Prefabs/Characters/")&&p.EndsWith(".prefab")))
        {var g=AssetDatabase.LoadAssetAtPath<GameObject>(path);var anim=g.GetComponentInChildren<Animator>();lines.Add("CHAR "+path+" | animator="+(anim!=null)+" | avatar="+(anim!=null&&anim.avatar!=null?anim.avatar.name+" valid="+anim.avatar.isValid+" human="+anim.avatar.isHuman:"none"));}
        Directory.CreateDirectory("Logs");File.WriteAllLines("Logs/AssetInventory.txt",lines);Debug.Log("TURN_ASSET_INVENTORY_READY");
    }
    public static void Prepare()
    {
        Directory.CreateDirectory("Assets/Resources/Turn");
        var shader=Shader.Find("Bang/Toon");
        if(shader==null)throw new Exception("Bang/Toon shader missing");
        if(AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/BangToon.mat")==null)AssetDatabase.CreateAsset(new Material(shader),"Assets/Resources/BangToon.mat");
        if(AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/WesternSky.mat")==null)AssetDatabase.CreateAsset(new Material(Shader.Find("Bang/WesternSky")),"Assets/Resources/WesternSky.mat");
        // The original packages remain intact; these prefabs are copies for the action-phase demo.
        MakeController();
        string[] cast={"Character_Sheriff_01","Character_Badguy_01","Character_Cowgirl_01","Character_Cowboy_01"};
        for(int i=0;i<cast.Length;i++)MakePrefab("Assets/PolygonWestern/Prefabs/Characters/"+cast[i]+".prefab","Actor"+i,shader);
        var props=new Dictionary<string,string>{{"Saloon","SM_Bld_Saloon_01"},{"Revolver","SM_Wep_Revolver_01"},{"Barrel","SM_Prop_Barrel_01"},{"Crate","SM_Prop_Crate_01"},{"Store","SM_Bld_Single_Preset_01"},{"Jail","SM_Bld_Jail_01"},{"House","SM_Bld_Double_Preset_01"},{"Windmill","SM_Bld_Windmill_01"},{"Fence","SM_Bld_Fence_01"},{"Cart","SM_Veh_Cart_01"},{"Cactus","SM_Env_Cactus_01"},{"Butte","SM_Env_Butte_03"},{"Hay","SM_Prop_Hay_Bale_01"},{"Hitch","SM_Prop_HitchingPost_01"},{"Lantern","SM_Prop_Lantern_Lit_01"}};
        foreach(var pair in props)
        {string source=AssetDatabase.GetAllAssetPaths().FirstOrDefault(p=>p.StartsWith("Assets/PolygonWestern/")&&p.EndsWith("/"+pair.Value+".prefab"));if(source!=null)MakePrefab(source,pair.Key,shader);}
        MakeEffects();
        AssetDatabase.SaveAssets();Inventory();Debug.Log("TURN_PREFABS_READY");
    }
    static void MakeEffects()
    {
        Directory.CreateDirectory("Assets/Resources/TurnFX");var shader=Shader.Find("Bang/Particle");
        var mapping=new Dictionary<string,string>{{"Blood","FX_BloodSplat_Small_01"},{"Wood","FX_Impact_Wood_01"},{"Dirt","FX_Impact_Dirt_01"},{"Footstep","FX_Dust_Small_01"},{"Muzzle","FX_Gunshot_01"},{"Heal","FX_Heal_01"}};
        foreach(var pair in mapping)
        {
            string path="Assets/Synty/PolygonParticleFX/Prefabs/"+pair.Value+".prefab";var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(prefab==null){Debug.LogWarning("Turn FX missing: "+path);continue;}
            var g=UnityEngine.Object.Instantiate(prefab);g.name=pair.Key;
            foreach(var t in g.GetComponentsInChildren<Transform>(true))GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
            foreach(var renderer in g.GetComponentsInChildren<Renderer>(true))
            {
                var mats=new List<Material>();
                foreach(var old in renderer.sharedMaterials)
                {
                    string matPath="Assets/Resources/TurnFX/"+(old!=null?old.name:"Default")+".mat";
                    var mat=AssetDatabase.LoadAssetAtPath<Material>(matPath);
                    if(mat==null)
                    {
                        mat=new Material(shader);mat.color=old!=null&&old.HasProperty("_Color")?old.GetColor("_Color"):Color.white;
                        if(old!=null)
                        {
                            var serialized=new SerializedObject(old);var env=serialized.FindProperty("m_SavedProperties.m_TexEnvs");
                            if(env!=null)for(int i=0;i<env.arraySize;i++){var entry=env.GetArrayElementAtIndex(i);var texture=entry.FindPropertyRelative("second.m_Texture").objectReferenceValue as Texture;string key=entry.FindPropertyRelative("first").stringValue;if(texture!=null&&(key=="_MainTex"||key=="_BaseMap")){mat.mainTexture=texture;break;}}
                        }
                        AssetDatabase.CreateAsset(mat,matPath);
                    }
                    if(old!=null&&(old.name=="Default-Particle"||old.name=="Sprites-Default"))mat.SetFloat("_SoftRound",1);
                    EditorUtility.SetDirty(mat);
                    mats.Add(mat);
                }
                renderer.sharedMaterials=mats.ToArray();
            }
            foreach(var particle in g.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main=particle.main;main.loop=false;main.stopAction=ParticleSystemStopAction.None;main.scalingMode=ParticleSystemScalingMode.Hierarchy;
                if(pair.Key=="Footstep")
                {
                    main.duration=.3f;main.startLifetime=.9f;main.startSize=new ParticleSystem.MinMaxCurve(.55f,.85f);main.startSpeed=.45f;main.startColor=new Color(.76f,.58f,.38f,.45f);
                    var shape=particle.shape;shape.shapeType=ParticleSystemShapeType.Hemisphere;shape.radius=.16f;shape.position=Vector3.zero;shape.scale=Vector3.one;shape.rotation=new Vector3(-90,0,0);
                    var velocity=particle.velocityOverLifetime;velocity.enabled=false;var force=particle.forceOverLifetime;force.enabled=false;
                    var emission=particle.emission;emission.rateOverTime=0;emission.rateOverDistance=0;emission.SetBursts(new[]{new ParticleSystem.Burst(0,3)});
                }
            }
            PrefabUtility.SaveAsPrefabAsset(g,"Assets/Resources/TurnFX/"+pair.Key+".prefab");UnityEngine.Object.DestroyImmediate(g);Debug.Log("TURN_EFFECT "+pair.Key+" = "+pair.Value);
        }
    }
    static void MakeController()
    {
        string path="Assets/Resources/Turn/ActionController.controller";
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if(controller==null)controller=AnimatorController.CreateAnimatorControllerAtPath(path);
        var clips=AssetDatabase.GetAllAssetPaths().Where(p=>(p.StartsWith("Assets/PistolAnimsetPro/Animations/")||p.StartsWith("Assets/RifleAnimsetPro/Animations/"))&&p.EndsWith(".fbx",StringComparison.OrdinalIgnoreCase)).SelectMany(p=>AssetDatabase.LoadAllAssetsAtPath(p).OfType<AnimationClip>()).Where(c=>!c.name.StartsWith("__preview__")).GroupBy(c=>c.name).ToDictionary(g=>g.Key,g=>g.First());
        var mapping=new Dictionary<string,string>{{"Idle","Pistol_Idle_Relaxed"},{"Aim","Pistol_Idle"},{"Fire","Pistol_ShootOnce"},{"Guard","Pistol_Idle2Crouch"},{"Dodge","Pistol_Crouch_WalkRtStart"},{"Heal","Pistol_Reload_2"},{"Equip","Pistol_Equip"},{"Hit","Pistol_Hit_C_1"},{"Death","Rifle_Death_L"},{"CoverEnter","Pistol_Idle2Crouch"},{"CoverMove","Pistol_Crouch_WalkRt"},{"CoverBack","Pistol_Crouch_WalkBwd"},{"CoverLeft","Pistol_Crouch_WalkLt"},{"CoverIdle","Pistol_CrouchLoop"},{"CoverExit","Pistol_Crouch2Idle"}};
        var states=new Dictionary<string,AnimatorState>();
        foreach(var pair in mapping){if(!clips.ContainsKey(pair.Value))throw new Exception("Missing action clip "+pair.Value);var state=controller.layers[0].stateMachine.states.Select(s=>s.state).FirstOrDefault(s=>s.name==pair.Key)??controller.layers[0].stateMachine.AddState(pair.Key);state.motion=clips[pair.Value];states.Add(pair.Key,state);Debug.Log("TURN_CLIP "+pair.Key+" = "+pair.Value);}
        controller.layers[0].stateMachine.defaultState=states["Idle"];
        foreach(string name in new[]{"Fire","Hit","Dodge","Heal","Equip"})if(states[name].transitions.Length==0){var transition=states[name].AddTransition(states["Idle"]);transition.hasExitTime=true;transition.exitTime=1;transition.duration=.1f;}
        string maskPath="Assets/Resources/Turn/CoverUpperBody.mask";var mask=AssetDatabase.LoadAssetAtPath<AvatarMask>(maskPath);
        if(mask==null)
        {
            mask=new AvatarMask();for(int i=0;i<(int)AvatarMaskBodyPart.LastBodyPart;i++)mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i,false);
            foreach(var part in new[]{AvatarMaskBodyPart.LeftArm,AvatarMaskBodyPart.RightArm,AvatarMaskBodyPart.LeftFingers,AvatarMaskBodyPart.RightFingers})mask.SetHumanoidBodyPartActive(part,true);
            AssetDatabase.CreateAsset(mask,maskPath);
        }
        if(!controller.layers.Any(l=>l.name=="CoverLowReady"))controller.AddLayer("CoverLowReady");
        var layers=controller.layers;var layer=layers.First(l=>l.name=="CoverLowReady");layer.avatarMask=mask;layer.defaultWeight=0;
        var low=layer.stateMachine.states.Select(s=>s.state).FirstOrDefault(s=>s.name=="Lowered weapon")??layer.stateMachine.AddState("Lowered weapon");low.motion=clips["Pistol_Idle_Relaxed"];layer.stateMachine.defaultState=low;controller.layers=layers;
        EditorUtility.SetDirty(controller);
    }
    static void MakePrefab(string source,string name,Shader shader)
    {
        var original=AssetDatabase.LoadAssetAtPath<GameObject>(source);if(original==null)throw new Exception("Missing model: "+source);
        var g=UnityEngine.Object.Instantiate(original);g.name=name;
        foreach(var t in g.GetComponentsInChildren<Transform>(true))GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
        foreach(var collider in g.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(collider);
        foreach(var r in g.GetComponentsInChildren<Renderer>(true))
        {
            var converted=new List<Material>();
            foreach(var old in r.sharedMaterials)
            {
                string matPath="Assets/Resources/Turn/"+name+"_"+(old!=null?old.name:"Default")+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if(m==null)
                {
                    m=new Material(shader);m.color=Color.white;
                    if(old!=null)
                    {
                        var so=new SerializedObject(old);var env=so.FindProperty("m_SavedProperties.m_TexEnvs");
                        if(env!=null)for(int j=0;j<env.arraySize;j++){var entry=env.GetArrayElementAtIndex(j);var tex=entry.FindPropertyRelative("second.m_Texture").objectReferenceValue as Texture;if(tex!=null&&(entry.FindPropertyRelative("first").stringValue=="_MainTex"||entry.FindPropertyRelative("first").stringValue=="_BaseMap")){m.mainTexture=tex;break;}}
                        if(old.HasProperty("_Color"))m.color=old.color;
                    }
                    AssetDatabase.CreateAsset(m,matPath);
                }
                converted.Add(m);
            }
            r.sharedMaterials=converted.ToArray();
        }
        PrefabUtility.SaveAsPrefabAsset(g,"Assets/Resources/Turn/"+name+".prefab");UnityEngine.Object.DestroyImmediate(g);
    }
}

