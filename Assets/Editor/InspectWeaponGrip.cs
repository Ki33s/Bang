using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class InspectWeaponGrip
{
    public static void Inspect()
    {
        var lines=new List<string>();
        var clip=AssetDatabase.GetAllAssetPaths().Where(p=>p.StartsWith("Assets/PistolAnimsetPro/Animations/")&&p.EndsWith(".fbx",StringComparison.OrdinalIgnoreCase)).SelectMany(p=>AssetDatabase.LoadAllAssetsAtPath(p).OfType<AnimationClip>()).First(c=>c.name=="Pistol_Idle");
        var gun=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Turn/Revolver.prefab");
        foreach(var filter in gun.GetComponentsInChildren<MeshFilter>())lines.Add("GUN "+filter.name+" bounds="+filter.sharedMesh.bounds.ToString("F4"));
        for(int id=0;id<4;id++)
        {
            var model=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Turn/Actor"+id+".prefab"));
            model.transform.position=Vector3.zero;model.transform.rotation=Quaternion.identity;
            var animator=model.GetComponentInChildren<Animator>();
            AnimationMode.StartAnimationMode();AnimationMode.BeginSampling();AnimationMode.SampleAnimationClip(model,clip,.3f);AnimationMode.EndSampling();
            var hand=animator.GetBoneTransform(HumanBodyBones.RightHand);
            lines.Add("ACTOR "+id+" scale="+model.transform.lossyScale.ToString("F4")+" hand="+hand.position.ToString("F4")+" euler="+hand.eulerAngles.ToString("F4")+" forwardLocal="+hand.InverseTransformDirection(Vector3.forward).ToString("F4")+" upLocal="+hand.InverseTransformDirection(Vector3.up).ToString("F4"));
            foreach(var bone in new[]{HumanBodyBones.RightThumbProximal,HumanBodyBones.RightIndexProximal,HumanBodyBones.RightMiddleProximal,HumanBodyBones.RightMiddleIntermediate,HumanBodyBones.RightLittleProximal})
            {var t=animator.GetBoneTransform(bone);if(t!=null)lines.Add(bone+" world="+t.position.ToString("F4")+" handLocal="+hand.InverseTransformPoint(t.position).ToString("F4"));}
            AnimationMode.StopAnimationMode();UnityEngine.Object.DestroyImmediate(model);
        }
        var reference=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PistolAnimsetPro/Prefabs/Enemy.prefab");
        foreach(var t in reference.GetComponentsInChildren<Transform>(true).Where(t=>t.name.Contains("RightHandProp")||t.name=="RightHand"||t.name.ToLower().Contains("pistol")))lines.Add("REFERENCE "+t.name+" local="+t.localPosition.ToString("F4")+" euler="+t.localEulerAngles.ToString("F4"));
        Directory.CreateDirectory("Logs");File.WriteAllLines("Logs/WeaponGrip.txt",lines);Debug.Log("WEAPON_GRIP_INSPECTED");
    }
}
