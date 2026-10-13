import assert from 'node:assert/strict';
const rooms=20,seats=7,clients=[],latencies=[];
const sleep=ms=>new Promise(r=>setTimeout(r,ms));
function connect(){let sequence=0;const socket=new WebSocket('ws://127.0.0.1:8081/ws'),messages=[];
const ready=new Promise((resolve,reject)=>{socket.addEventListener('open',resolve,{once:true});socket.addEventListener('error',reject,{once:true});});
socket.addEventListener('message',e=>messages.push(JSON.parse(e.data)));
const client={socket,ready,send(c){socket.send(JSON.stringify({...c,seq:++sequence}));},async wait(fn){let start=Date.now();while(Date.now()-start<20000){let index=messages.findIndex(fn);if(index>=0)return messages.splice(index,1)[0];await sleep(10);}throw new Error('Load test timeout');}};clients.push(client);return client;}
try{
const groups=await Promise.all(Array.from({length:rooms},async(_,roomIndex)=>{
const owner=connect();await owner.ready;owner.send({op:'create',name:'Owner'+roomIndex,seats,seconds:30});const first=await owner.wait(m=>m.view?.phase===0);
const joined=await Promise.all(Array.from({length:seats-1},async(_,i)=>{const c=connect();await c.ready;c.send({op:'join',name:'Test'+i,code:first.code});await c.wait(m=>m.view?.phase===0);return c;}));
return [owner,...joined];}));
await Promise.all(groups.map(async group=>{
for(const c of group)c.send({op:'lobbyReady',ready:true});
await group[0].wait(m=>m.view?.phase===0&&m.view.players.every(p=>p.ready));
group[0].send({op:'start'});
await Promise.all(group.map(c=>c.wait(m=>m.view?.phase===1)));
const start=Date.now();for(const c of group)c.send({op:'plan',revision:1,ready:true,plan:{actions:[{card:-1,target:-1},{card:-1,target:-1}],dodges:[],discard:[]}});
await Promise.all(group.map(async c=>{const m=await c.wait(m=>m.view?.phase===2&&m.view.beat===1);assert.equal(m.view.players.length,seats);assert.equal(m.view.players.filter(p=>p.hand.length>0).length,1);latencies.push(Date.now()-start);}));
await Promise.all(group.map(c=>c.wait(m=>m.view?.phase===2&&m.view.beat===2)));
}));
latencies.sort((a,b)=>a-b);console.log(JSON.stringify({result:'PASS',rooms,connections:clients.length,firstBeatLatencyMs:{median:latencies[Math.floor(latencies.length*.5)],p95:latencies[Math.floor(latencies.length*.95)],max:latencies.at(-1)},checks:'All rooms complete both beats, every client receives only its own hand.'},null,2));
}finally{for(const c of clients)c.socket.close();}
