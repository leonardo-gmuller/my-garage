import {test,expect} from '@playwright/test'
import {signIn,cleanup} from './support/session'
test.afterEach(async({page})=>cleanup(page))
const available={available:true,maxMessageLength:4000,reason:null}

test('assistant displays known totals and opens the actual source through a reloadable vehicle link',async({page},info)=>{
 await page.emulateMedia({reducedMotion:'reduce'})
 const errors:string[]=[];page.on('pageerror',e=>errors.push(e.message))
 await page.route('**/api/ai/maintenance/status',r=>r.fulfill({json:available}))
 await signIn(page,'/assistente')
 const vehicles=(await(await page.request.get('/api/vehicles')).json()).items
 const v=vehicles.find((x:{nickname:string})=>x.nickname==='Saveiro E2E')
 const catalogue=await(await page.request.get(`/api/vehicles/${v.id}/components`)).json()
 const component=catalogue.find((x:{key:string})=>x.key==='suspension')
 const csrf=(await(await page.request.get('/api/auth/csrf')).json()).token
 const created=await page.request.post(`/api/vehicles/${v.id}/records`,{headers:{'X-CSRF-TOKEN':csrf},data:{date:'2026-01-10',category:'Suspension',serviceType:'Replacement',description:'Amortecedores — origem do assistente',laborCost:0,componentIds:[component.id],items:[],packageTotal:1000,payments:[{scope:'Package',method:'CreditCard',installments:10,totalCharged:1100}]}})
 expect(created.ok()).toBeTruthy();const record=await created.json()
 let requests=0,writes=0
 page.on('request',r=>{if(['POST','PUT','DELETE'].includes(r.method())&&/\/api\/vehicles\//.test(r.url()))writes++})
 await page.route('**/api/ai/assistant/ask',r=>{
  requests++;expect(r.request().postDataJSON()).toEqual({vehicleId:v.id,question:'Quanto gastei com suspensão?'})
  return r.fulfill({json:{vehicleId:v.id,kind:'spending',message:'Total conhecido dos serviços encontrados.',scope:'Suspensão · Todo o período registrado',count:1,total:record.totalCost,pendingPaymentCount:0,records:[{id:record.id,description:record.description,date:record.date,mileage:null,totalCost:record.totalCost,closedTotal:true,pendingPaymentCount:0,url:'/ignored-model-link'}],schedules:[],warnings:['O total considera serviços completos vinculados ao componente, sem dividir entre peças.'],promptVersion:'history-v1'}})
 })
 await page.getByRole('button',{name:'Quanto gastei com suspensão?',exact:true}).click()
 await expect(page.getByLabel('O que você quer consultar?',{exact:true})).toHaveValue('Quanto gastei com suspensão?');expect(requests).toBe(0)
 await page.screenshot({path:`test-results/assistant-empty-${info.project.name}.png`,fullPage:true,animations:'disabled'})
 await page.getByRole('button',{name:'Consultar histórico',exact:true}).click()
 const answer=page.getByRole('region',{name:'Resultado da consulta'})
 await expect(answer.getByRole('heading',{name:'Gastos encontrados',exact:true})).toBeVisible()
 await expect(answer.locator('.assistant-amount')).toContainText('R$ 1.100,00')
 expect(requests).toBe(1);expect(writes).toBe(0)
 await page.screenshot({path:`test-results/assistant-result-${info.project.name}.png`,fullPage:true,animations:'disabled'})
 await answer.getByRole('link').click()
 await expect(page).toHaveURL(new RegExp(`record=${record.id}`))
 await expect(page.getByRole('heading',{name:record.description,exact:true})).toBeVisible()
 await page.reload()
 await expect(page.getByRole('heading',{name:record.description,exact:true})).toBeVisible()
 await expect(page.getByRole('combobox',{name:'Veículo selecionado',exact:true})).toContainText('Saveiro E2E')
 await page.getByRole('button',{name:'Ver histórico completo',exact:true}).click()
 await expect(page.getByRole('button',{name:'Filtrar',exact:true})).toBeVisible()
 expect(errors).toEqual([]);expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBeTruthy()
})

test('unavailable assistant exposes honest status and keeps manual history reachable',async({page})=>{
 await page.route('**/api/ai/maintenance/status',r=>r.fulfill({json:{...available,available:false,reason:'IA indisponível nesta instalação.'}}))
 await signIn(page,'/assistente')
 await expect(page.getByRole('status')).toContainText('IA indisponível')
 await expect(page.getByRole('button',{name:'Consultar histórico',exact:true})).toBeDisabled()
 await page.getByRole('link',{name:'Explorar histórico',exact:true}).click()
 await expect(page.getByRole('button',{name:'Registrar com IA',exact:true})).toBeVisible()
})

test('provider failure preserves question and cancellation prevents a stale answer',async({page})=>{
 await page.route('**/api/ai/maintenance/status',r=>r.fulfill({json:available}))
 await page.route('**/api/ai/assistant/ask',r=>r.fulfill({status:429,json:{title:'Limite da IA atingido. Aguarde e tente novamente.',status:429}}))
 await signIn(page,'/assistente')
 await page.getByLabel('O que você quer consultar?',{exact:true}).fill('Quando foi a última revisão?')
 await page.getByRole('button',{name:'Consultar histórico',exact:true}).click()
 await expect(page.getByRole('alert')).toContainText('Limite da IA atingido')
 await expect(page.getByLabel('O que você quer consultar?',{exact:true})).toHaveValue('Quando foi a última revisão?')
 await page.unroute('**/api/ai/assistant/ask')
 let release:()=>void=()=>{};const delayed=new Promise<void>(r=>release=r)
 await page.route('**/api/ai/assistant/ask',async r=>{await delayed;await r.abort().catch(()=>{})})
 await page.getByRole('button',{name:'Consultar histórico',exact:true}).click()
 await expect(page.getByRole('status')).toContainText('Consultando os registros')
 await page.getByRole('button',{name:'Cancelar',exact:true}).click();release()
 await expect(page.getByRole('alert')).toContainText('Consulta cancelada')
 await expect(page.getByRole('region',{name:'Resultado da consulta'})).toHaveCount(0)
})
