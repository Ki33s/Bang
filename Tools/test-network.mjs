import assert from 'node:assert/strict';
const url = 'ws://127.0.0.1:8081/ws';
function client() {
  const socket = new WebSocket(url), received = []; let sequence = 0;
  socket.addEventListener('message',e=>received.push(JSON.parse(e.data)));
  const open = new Promise((resolve,reject)=>{socket.addEventListener('open',resolve,{once:true});socket.addEventListener('error',reject,{once:true});});
  return {socket,received,open,setSequence(n){sequence=Math.max(sequence,n);},send(c){socket.send(JSON.stringify({...c,seq:++sequence}));},
    async wait(predicate, timeout=12000){let start=Date.now();while(Date.now()-start<timeout){let index=received.findIndex(predicate);if(index>=0)return received.splice(index,1)[0];await new Promise(r=>setTimeout(r,25));}throw new Error('Timed out waiting for state');}};
}
const a=client(),b=client();
try {
  await Promise.all([a.open,b.open]);
  a.send({op:'create',name:'Alice',seats:4,seconds:30});
  let first=await a.wait(m=>m.view?.phase===0); const code=first.code;
  b.send({op:'join',name:'Bob',code}); let joined=await b.wait(m=>m.view?.phase===0);
  assert.equal(joined.view.localId,1); assert.equal(joined.view.players.length,4);
  a.send({op:'lobbyReady',ready:true}); b.send({op:'lobbyReady',ready:true});
  await a.wait(m=>m.view?.phase===0&&m.view.players[0].ready&&m.view.players[1].ready);
  a.send({op:'start'});let state=await a.wait(m=>m.view?.phase===1);let other=await b.wait(m=>m.view?.phase===1);
  assert.equal(state.view.players[0].hand.length,6); assert.equal(other.view.players[1].hand.length,6);
  assert(state.view.players.slice(1).every(p=>p.hand.length===0));
  assert(other.view.players.filter(p=>p.id!==1).every(p=>p.hand.length===0));
  assert(!JSON.stringify(state).includes('randomState'));assert(!JSON.stringify(state).includes('deck'));
  const invalid={actions:[{card:99999,target:1},{card:-1,target:-1}],dodges:[],discard:[]};
  a.send({op:'plan',plan:invalid,revision:1,ready:true});assert((await a.wait(m=>m.type==='error')).error.length>0);
  const blank={actions:[{card:-1,target:-1},{card:-1,target:-1}],dodges:[],discard:[]};
  a.send({op:'plan',plan:blank,revision:2,ready:true}); await a.wait(m=>m.view?.phase===1&&m.view.players[0].ready);
  a.send({op:'plan',plan:blank,revision:3,ready:false});assert((await a.wait(m=>m.type==='error')).error.includes('подтверждён'));
  b.send({op:'plan',plan:blank,revision:1,ready:true});
  const resolving=await a.wait(m=>m.view?.phase===2);assert.equal(resolving.view.beat,1);
  b.socket.close();await new Promise(r=>setTimeout(r,350));
  const resumed=client();await resumed.open;resumed.send({op:'resume',code,token:joined.token});
  let restored=await resumed.wait(m=>m.view?.localId===1);resumed.setSequence(restored.ack);assert(restored.view.players[1].connected);assert.equal(restored.token,joined.token);
  assert.equal(restored.view.phase,2);assert.equal(restored.view.players[1].hand.length,6);
  resumed.send({op:'plan',plan:blank,revision:2,ready:true});assert((await resumed.wait(m=>m.type==='error')).error.includes('завершено'));
  const second=await a.wait(m=>m.view?.phase===2&&m.view.beat===2);assert.equal(second.view.round,1);
  resumed.socket.close();
  console.log('PASS: real WebSocket room, 2 clients + 2 bots, private hands/roles, validation, ready lock, both beats, authenticated reconnect, late-command rejection.');
} finally {a.socket.close();b.socket.close();}
