#!/usr/bin/env node
/** Explicit, local-only demo seed through the authenticated API. Never runs on app startup. */
import { createHash } from 'node:crypto'
import { existsSync,readFileSync,mkdirSync,writeFileSync,renameSync,openSync,closeSync,unlinkSync,chmodSync } from 'node:fs'
import { dirname,resolve,join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { parseEnv } from 'node:util'

const root=resolve(dirname(fileURLToPath(import.meta.url)),'..')
const args=process.argv.slice(2)
if(args.includes('--help')){
 console.log('Uso: make seed\nOu: node scripts/seed-demo.mjs --url http://127.0.0.1:8080\nSEED_BASE_URL é opcional. O acesso vem de BOOTSTRAP_EMAIL e BOOTSTRAP_PASSWORD no .env. Somente loopback e contas demo@minhagaragem.local (ou demo+nome) são aceitos.\nO seed preserva ajustes feitos na demonstração e não altera contas pessoais.')
 process.exit(0)
}
const urlIndex=args.indexOf('--url')
if(args.length&&!(args.length===2&&args[0]==='--url'&&args[1]))throw new Error('Argumentos inválidos. Execute com --help.')
const env=existsSync(join(root,'.env'))?parseEnv(readFileSync(join(root,'.env'),'utf8')):{}
const address=new URL(urlIndex>=0?args[urlIndex+1]:process.env.SEED_BASE_URL||`http://127.0.0.1:${env.FRONTEND_PORT||8080}`)
if(!['http:','https:'].includes(address.protocol)||!['localhost','127.0.0.1','[::1]'].includes(address.hostname)||address.username||address.password||address.pathname!=='/'||address.search||address.hash)
 throw new Error('O seed aceita apenas a origem de uma aplicação local, sem caminho, credenciais ou parâmetros.')
const base=address.origin,email=process.env.BOOTSTRAP_EMAIL||env.BOOTSTRAP_EMAIL||'demo@minhagaragem.local'
const password=process.env.BOOTSTRAP_PASSWORD||env.BOOTSTRAP_PASSWORD
if(!password)throw new Error('Defina BOOTSTRAP_PASSWORD no .env e inicie a aplicação para criar o acesso inicial.')
const fixture=JSON.parse(readFileSync(join(root,'scripts/seeds/garage-demo.json'),'utf8'))
if(!/^demo(?:[.+-][a-z0-9_-]+)?@minhagaragem\.local$/i.test(email))throw new Error('Use uma conta exclusiva de demonstração: demo@minhagaragem.local ou demo+nome@minhagaragem.local.')
const directory=join(root,'.local','seeds'),key=createHash('sha256').update(base+'|'+email.toLowerCase()+'|'+fixture.key).digest('hex').slice(0,16)
mkdirSync(directory,{recursive:true,mode:0o700})
const stateFile=join(directory,`demo-${key}.json`),lockFile=stateFile+'.lock'
const marker=`[seed:${fixture.key}]`,money=n=>new Intl.NumberFormat('pt-BR',{style:'currency',currency:'BRL'}).format(n)
const today=()=>new Intl.DateTimeFormat('en-CA',{timeZone:'America/Sao_Paulo',year:'numeric',month:'2-digit',day:'2-digit'}).format(new Date())
const addDays=(date,days)=>{const value=new Date(date+'T12:00:00Z');value.setUTCDate(value.getUTCDate()+days);return value.toISOString().slice(0,10)}
const state=existsSync(stateFile)?JSON.parse(readFileSync(stateFile,'utf8')):{version:2,baseUrl:base,email,fixtureKey:fixture.key,date:today()}
if(state.baseUrl!==base||state.email.toLowerCase()!==email.toLowerCase()||state.version!==2||state.fixtureKey!==fixture.key)throw new Error('O arquivo de acesso não corresponde a esta demonstração. Nenhuma conta foi alterada.')
function checkpoint(){const temp=stateFile+'.tmp';writeFileSync(temp,JSON.stringify(state,null,2)+'\n',{mode:0o600});chmodSync(temp,0o600);renameSync(temp,stateFile);chmodSync(stateFile,0o600)}
class Api{
 cookies=new Map();token=null
 async request(path,{method='GET',body,optional=false}={}){
  if(!path.startsWith('/api/'))throw new Error('Caminho de API inválido.')
  if(method!=='GET'&&!this.token)this.token=(await this.request('/api/auth/csrf')).token
  const headers={Accept:'application/json'}
  if(this.cookies.size)headers.Cookie=[...this.cookies].map(([name,value])=>name+'='+value).join('; ')
  if(body!==undefined)headers['Content-Type']='application/json'
  if(method!=='GET')headers['X-CSRF-TOKEN']=this.token
  let response
  try{response=await fetch(base+path,{method,headers,body:body===undefined?undefined:JSON.stringify(body),redirect:'manual',signal:AbortSignal.timeout(20000)})}
  catch{throw new Error(`Aplicação indisponível em ${base}. Inicie com make up ou informe SEED_BASE_URL.`)}
  for(const cookie of response.headers.getSetCookie()){
   const first=cookie.split(';',1)[0],split=first.indexOf('=');if(split<1)continue
   const name=first.slice(0,split),value=first.slice(split+1)
   if(!value||/max-age=0(?:;|$)/i.test(cookie))this.cookies.delete(name);else this.cookies.set(name,value)
  }
  if(optional&&response.status===404)return null
  if(!response.ok){const error=new Error(`A API recusou ${method} ${path} (HTTP ${response.status}). Nenhuma conta pessoal foi alterada.`);error.status=response.status;throw error}
  if(response.status===204)return null
  try{return await response.json()}catch{throw new Error('Resposta inesperada. A URL deve apontar para o frontend com proxy /api ou para a API local.')}
 }
 async all(path){const rows=[];for(let page=1;page<=1000;page++){const result=await this.request(path+(path.includes('?')?'&':'?')+`pageSize=100&page=${page}`);rows.push(...result.items);if(rows.length>=result.total)return rows}throw new Error('Paginação da demonstração excedeu o limite.')}
}
async function find(api,path,id){
 if(id&&!path.endsWith('/schedules')){const existing=await api.request(path+'/'+id,{optional:true});if(existing)return existing}
 const rows=await api.all(path+(path==='/api/mechanics'?'?status=all':''))
 const matches=rows.filter(row=>row.id===id||row.notes?.includes(marker)||path.endsWith('/schedules')&&row.description===fixture.schedule.description)
 if(matches.length>1)throw new Error('Mais de um registro corresponde ao seed. Nenhum deles foi sobrescrito.')
 return matches[0]??null
}
let lock
try{
 try{lock=openSync(lockFile,'wx',0o600);writeFileSync(lock,String(process.pid))}catch{throw new Error(`Outro seed está em execução ou deixou um bloqueio em ${lockFile}. Nenhum processo foi encerrado.`)}
 checkpoint()
 const api=new Api()
 await api.request('/api/auth/login',{method:'POST',body:{email,password}})
 api.token=null
 const me=await api.request('/api/auth/me');if(me.email.toLowerCase()!==email.toLowerCase())throw new Error('Conta autenticada diferente da demonstração.')
 const vehicles=await api.all('/api/vehicles')
 let vehicle=vehicles.find(v=>v.id===state.vehicleId)||vehicles.find(v=>v.nickname===fixture.vehicleNickname)
 if(!vehicle&&vehicles.length===1&&vehicles[0].model==='Saveiro')vehicle=vehicles[0]
 if(!vehicle)throw new Error('A conta demo não possui uma Saveiro identificável. Nenhum veículo foi alterado.')
 state.vehicleId=vehicle.id;checkpoint()
 if(!state.vehiclePrepared){vehicle=await api.request('/api/vehicles/'+vehicle.id,{method:'PUT',body:{...vehicle,nickname:fixture.vehicleNickname}});state.vehiclePrepared=true;checkpoint()}
 const components=await api.request(`/api/vehicles/${vehicle.id}/components`)
 const component=key=>{const value=components.find(c=>c.key===key);if(!value)throw new Error('Componente não encontrado: '+key);return value}
 let mechanic=await find(api,'/api/mechanics',state.mechanicId)
 if(!mechanic)mechanic=await api.request('/api/mechanics',{method:'POST',body:{...fixture.mechanic,notes:marker+' Contato fictício para visualizar o vínculo com os serviços.'}})
 state.mechanicId=mechanic.id;checkpoint()
 let record=await find(api,`/api/vehicles/${vehicle.id}/records`,state.recordId)
 if(!record){
  const service=fixture.service,parts=service.items.reduce((sum,p)=>sum+Math.round(p.quantity*p.unitCost*100),0)/100
  const partsPayment=service.payments.find(p=>p.scope==='Parts'),laborPayment=service.payments.find(p=>p.scope==='Labor')
  const notes=[marker,'DADOS FICTÍCIOS — orçamento de peças incompleto. Data do serviço fictícia, escolhida apenas para visualizar o sistema.',`${service.items.length} itens conhecidos: ${money(parts)}. Ainda faltam peças; não classificar a diferença como juros.`,`Cobrança das peças informada: ${money(service.reportedPartsCharge)}, em ${partsPayment.installments} parcelas de ${money(service.reportedPartsCharge/partsPayment.installments)}. Esse valor foi preservado aqui; no pagamento das peças, o total com juros fica pendente até completar o orçamento.`,`Mão de obra: ${money(service.laborCost)}, cartão ${money(laborPayment.totalCharged)}, em ${laborPayment.installments} parcelas. Juros confirmados da mão de obra: ${money(Math.round((laborPayment.totalCharged-service.laborCost)*100)/100)}.`,'O total conhecido desta demonstração não representa o custo final completo.'].join('\n')
  record=await api.request(`/api/vehicles/${vehicle.id}/records`,{method:'POST',body:{date:state.date,category:service.category,serviceType:service.serviceType,description:service.description,laborCost:service.laborCost,mechanicId:mechanic.id,notes,items:service.items.map(p=>({name:p.name+(p.reference?' · '+p.reference:''),quantity:p.quantity,unitCost:p.unitCost})),componentIds:service.componentKeys.map(key=>component(key).id),primaryComponentId:component(service.primaryComponentKey).id,subcomponentIds:Object.entries(service.subcomponentKeys).flatMap(([key,keys])=>keys.map(subKey=>{const sub=component(key).subcomponents.find(s=>s.key===subKey);if(!sub)throw new Error('Subcomponente não encontrado: '+subKey);return sub.id})),payments:service.payments}})
 }
 state.recordId=record.id;checkpoint()
 let schedule=await find(api,`/api/vehicles/${vehicle.id}/schedules`,state.scheduleId)
 if(!schedule)schedule=await api.request(`/api/vehicles/${vehicle.id}/schedules`,{method:'POST',body:{componentId:component(fixture.schedule.componentKey).id,description:fixture.schedule.description,dueDate:addDays(state.date,fixture.schedule.daysFromSeed),priority:'Normal',completed:false}})
 state.scheduleId=schedule.id;state.complete=true;checkpoint()
 const saved=await api.request(`/api/vehicles/${vehicle.id}/records/${record.id}`)
 if(saved.vehicleId!==vehicle.id)throw new Error('Vínculo do serviço de demonstração inválido.')
 console.log(`[seed] Demonstração disponível em ${base}/historico`)
 console.log(`[seed] Acesso: ${email}. Use a senha configurada no .env.`)
 console.log(`[seed] ${saved.items.length} peças no registro. Total conhecido: ${money(saved.totalCost)}. Cobranças pendentes: ${saved.pendingPaymentCount}.`)
 console.log(`[seed] Acesso e IDs salvos em ${stateFile}. Reexecutar preserva ajustes e não duplica os registros.`)
}catch(error){console.error('[seed] '+error.message);process.exitCode=1}
finally{if(lock!==undefined){closeSync(lock);unlinkSync(lockFile)}}
