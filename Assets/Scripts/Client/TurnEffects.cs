using System.Collections.Generic;
using UnityEngine;

namespace DustAndPowder
{
    public class TurnEffects:MonoBehaviour
    {
        class Active { public GameObject root;public ParticleSystem[] particles;public float age; }
        readonly List<Active> active=new List<Active>();
        readonly Dictionary<string,GameObject> prefabs=new Dictionary<string,GameObject>();
        float playback=1;
        public void SetPlayback(float speed,bool paused){playback=paused?0:speed;foreach(var fx in active)foreach(var p in fx.particles)if(p!=null){var main=p.main;main.simulationSpeed=playback;}}
        public void Play(string name,Vector3 position,Quaternion rotation,float scale=1)
        {
            if(!prefabs.ContainsKey(name))prefabs[name]=Resources.Load<GameObject>("TurnFX/"+name);
            var prefab=prefabs[name];if(prefab==null)return;
            var root=Instantiate(prefab,position,rotation*prefab.transform.localRotation,transform);root.transform.localScale*=scale;
            var particles=root.GetComponentsInChildren<ParticleSystem>(true);
            foreach(var p in particles){p.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);var main=p.main;main.simulationSpeed=playback;main.loop=false;p.Play(false);}
            active.Add(new Active{root=root,particles=particles});
        }
        void Update(){for(int i=active.Count-1;i>=0;i--){var fx=active[i];fx.age+=Time.deltaTime*playback;if(fx.age>5||fx.root==null){if(fx.root!=null)Destroy(fx.root);active.RemoveAt(i);}}}
        public void Clear(){foreach(var fx in active)if(fx.root!=null)Destroy(fx.root);active.Clear();}
    }
}
