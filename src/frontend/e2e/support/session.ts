import { expect,type Page } from '@playwright/test'
import { existsSync,readFileSync } from 'node:fs'
import { parseEnv } from 'node:util'
import { fileURLToPath } from 'node:url'
const environment=fileURLToPath(new URL('../../../../.env',import.meta.url))
const local=existsSync(environment)?parseEnv(readFileSync(environment,'utf8')):{}
const email=process.env.GARAGE_E2E_EMAIL??process.env.BOOTSTRAP_EMAIL??local.BOOTSTRAP_EMAIL
const password=process.env.GARAGE_E2E_PASSWORD??process.env.BOOTSTRAP_PASSWORD??local.BOOTSTRAP_PASSWORD
const sessions=new WeakMap<Page,{vehicle:string;mechanics:Set<string>}>()
export const hasLocalSaveiro=process.env.GARAGE_DISABLE_LOCAL_MODELS!=='true'&&existsSync(fileURLToPath(new URL('../../public/models/Saveiro_3D.glb',import.meta.url)))&&existsSync(fileURLToPath(new URL('../../public/models/Saveiro_3D_grupos.json',import.meta.url)))
export const modelKind=hasLocalSaveiro?'saveiro':'generic'
async function token(page:Page){const response=await page.request.get('/api/auth/csrf');return(await response.json()).token as string}
export async function signIn(page:Page,path='/'){
 if(!email||!password)throw new Error('Configure GARAGE_E2E_EMAIL e GARAGE_E2E_PASSWORD para uma instalação de testes isolada.')
 await page.goto('/historico')
 await page.getByLabel('E-mail',{exact:true}).fill(email);await page.getByLabel('Senha',{exact:true}).fill(password)
 await page.getByRole('button',{name:'Entrar na minha garagem',exact:true}).click()
 await expect(page.getByRole('combobox',{name:'Veículo selecionado',exact:true})).toBeVisible()
 const csrf=await token(page)
 const response=await page.request.post('/api/vehicles',{headers:{'X-CSRF-TOKEN':csrf},data:{manufacturer:'Volkswagen',model:'Saveiro',manufactureYear:2013,modelYear:2013,color:'Prata',colorHex:'#C0C0C0',paintFinish:'Metallic',nickname:'Saveiro E2E'}})
 expect(response.ok()).toBeTruthy();const vehicle=await response.json()
 const state={vehicle:vehicle.id as string,mechanics:new Set<string>()};sessions.set(page,state)
 page.on('response',async response=>{if(response.request().method()==='POST'&&/\/api\/mechanics\/?$/.test(response.url())&&response.ok()){const m=await response.json().catch(()=>null);if(m?.id)state.mechanics.add(m.id)}})
 await page.goto('/veiculos')
 await page.getByRole('button',{name:'Selecionar Saveiro E2E',exact:true}).click()
 // Persist the selected vehicle by changing routes inside the SPA.
 if(path!=='/veiculos'){
  const names:Record<string,string>={'/':'Visão geral','/historico':'Histórico de serviços','/assistente':'Assistente do histórico','/garagem-3d':'Garagem 3D','/mecanicos':'Mecânicos','/financeiro':'Financeiro','/agenda':'Próximas manutenções'}
  const menu=page.getByRole('button',{name:'Abrir navegação'});if(await menu.isVisible())await menu.click()
  await page.getByRole('link',{name:names[path],exact:true}).click()
 }
}
export async function cleanup(page:Page){
 const state=sessions.get(page);if(!state)return
 const csrf=await token(page)
 const response=await page.request.delete('/api/vehicles/'+state.vehicle,{headers:{'X-CSRF-TOKEN':csrf}})
 expect([204,404]).toContain(response.status())
 for(const id of state.mechanics){const r=await page.request.delete('/api/mechanics/'+id,{headers:{'X-CSRF-TOKEN':csrf}});expect([204,404]).toContain(r.status())}
 sessions.delete(page)
}
export async function selectTestVehicle(page:Page){
 const chooser=page.getByRole('combobox',{name:'Veículo selecionado',exact:true})
 await chooser.click()
 await page.getByRole('option',{name:'Saveiro E2E · 2013',exact:true}).click()
}
