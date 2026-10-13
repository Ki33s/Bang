import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../Builds/Web');
const types = {'.html':'text/html; charset=utf-8','.js':'application/javascript','.wasm':'application/wasm','.json':'application/json','.data':'application/octet-stream','.png':'image/png'};
http.createServer((req,res)=> {
  let file; try { file = path.resolve(root, '.' + decodeURIComponent((req.url||'/').split('?')[0] === '/' ? '/index.html' : (req.url||'/').split('?')[0])); } catch {res.writeHead(400).end();return;}
  if (!file.startsWith(root + path.sep)) {res.writeHead(403).end();return;}
  fs.stat(file,(err,stat)=>{if(err||!stat.isFile()){res.writeHead(404).end('Not found');return;}res.writeHead(200,{'Content-Type':types[path.extname(file)]||'application/octet-stream','Content-Length':stat.size,'Cache-Control':'no-store'});fs.createReadStream(file).pipe(res);});
}).listen(8080,'127.0.0.1',()=>console.log('Пыль и порох: http://127.0.0.1:8080'));
