import { test,expect } from '@playwright/test'
import { signIn,cleanup,hasLocalSaveiro } from './support/session'
test.afterEach(async({page})=>cleanup(page))
test('Saveiro JSON matches the GLB and highlights preserve original PBR materials',async({page})=>{
 test.skip(!hasLocalSaveiro,'Modelo local opcional não distribuído no repositório.')
 await page.goto('/')
 const result=await page.evaluate(async()=>{
  const loaderPath='/node_modules/three/examples/jsm/loaders/GLTFLoader.js',mappingPath='/src/features/three/modelConfig.ts',selectionPath='/src/features/three/materialSelection.ts'
  const [{GLTFLoader},{saveiroModel,validateMapping,groupForMesh},{MaterialSelection}]=await Promise.all([import(/* @vite-ignore */loaderPath),import(/* @vite-ignore */mappingPath),import(/* @vite-ignore */selectionPath)])
  const [gltf,document]=await Promise.all([new GLTFLoader().loadAsync(saveiroModel.url),fetch(saveiroModel.mappingUrl).then(r=>r.json())])
  const groups=validateMapping(document),found=new Map<string,number>(),meshes:any[]=[]
  gltf.scene.traverse((o:any)=>{if(o.isMesh){meshes.push(o);const group=groupForMesh(o.name,groups);if(group){found.set(group.name,(found.get(group.name)??0)+1);o.userData.groupName=group.name;o.userData.componentKey=saveiroModel.componentGroups[group.name]}}})
  const snapshot=meshes.map(o=>({mesh:o,material:o.material,description:JSON.stringify(o.material.toJSON())}))
  const selection=new MaterialSelection();selection.capture(gltf.scene);selection.select('wheels','wheel_1')
  const highlighted=meshes.filter(m=>m.userData.groupName==='wheel_1').every(m=>{const before=snapshot.find(s=>s.mesh===m)!;return m.material!==before.material&&m.material.roughness===before.material.roughness&&m.material.metalness===before.material.metalness&&m.material.opacity===before.material.opacity&&m.material.map===before.material.map})
  selection.select('body','door_rf_01')
  const wheelRestored=meshes.filter(m=>m.userData.groupName==='wheel_1').every(m=>m.material===snapshot.find(s=>s.mesh===m)!.material)
  selection.select(null,null)
  const originalsUntouched=snapshot.every(s=>s.mesh.material===s.material&&JSON.stringify(s.material.toJSON())===s.description)
  selection.dispose()
  return {meshCount:meshes.length,groupsMatch:groups.every((g:any)=>found.get(g.name)===g.meshes),highlighted,wheelRestored,originalsUntouched}
 })
 expect(result).toEqual({meshCount:649,groupsMatch:true,highlighted:true,wheelRestored:true,originalsUntouched:true})
})
test('raycasting selects a visible Saveiro part and registers a service from its panel',async({page,isMobile},testInfo)=>{
 test.skip(!hasLocalSaveiro,'Modelo local opcional não distribuído no repositório.')
 await signIn(page,'/garagem-3d')
 await expect(page.locator('.scene-wrap')).toHaveAttribute('data-model',modelKind,{timeout:45000})
 await expect(page.locator('.hotspot')).toHaveCount(0)
 const canvas=page.locator('canvas');await canvas.scrollIntoViewIfNeeded()
 const rect=await canvas.boundingBox();expect(rect).not.toBeNull()
 let selected:{x:number;y:number;name:string}|null=null
 // Inspect hover labels produced by the real raycaster; click only an observed visible part.
 for(const fy of [.55,.65,.45,.75,.35]){
  for(const fx of [.3,.4,.5,.6,.7]){
   const x=rect!.x+rect!.width*fx,y=rect!.y+rect!.height*fy
   await page.mouse.move(x,y)
   const name=await page.locator('.scene-hover').textContent().catch(()=>null)
   if(name&&/Portas|Capô|Pneus e rodas|Faróis dianteiros/.test(name)){selected={x,y,name};break}
  }
  if(selected)break
 }
 expect(selected).not.toBeNull()
 if(isMobile)await page.touchscreen.tap(selected!.x,selected!.y);else await page.mouse.click(selected!.x,selected!.y)
 await expect(page.locator('.selected-group')).toBeVisible()
 const label=await page.locator('.selected-group').textContent();expect(selected!.name).toContain(label!)
 await page.screenshot({path:`test-results/saveiro-selected-${testInfo.project.name}.png`,fullPage:testInfo.project.name==='desktop',scale:'css'})
 await page.getByRole('button',{name:'Registrar neste componente'}).click()
 const dialog=page.getByRole('dialog')
 await expect(dialog.locator('input[id^=record-component-]:checked')).toHaveCount(1)
 await dialog.getByLabel('Descrição',{exact:true}).fill('Inspeção da peça selecionada no modelo 3D')
 await dialog.getByLabel('Valor total cobrado (R$)',{exact:true}).fill('0')
 await dialog.getByRole('button',{name:'Salvar serviço'}).click()
 await expect(dialog).toBeHidden()
 await expect(page.getByRole('button',{name:/Inspeção da peça selecionada no modelo 3D/})).toBeVisible()
 // Tests leave no vehicles or maintenance records in the local database.
 const menu=page.getByRole('button',{name:'Abrir navegação'});if(await menu.isVisible())await menu.click()
 await page.getByRole('link',{name:/Meus veículos/}).click();page.once('dialog',d=>d.accept());await page.getByRole('button',{name:'Excluir Saveiro E2E'}).click()
 await expect(page.getByRole('button',{name:'Excluir Saveiro E2E',exact:true})).toHaveCount(0)
})
test('missing mapping or GLB falls back honestly and keeps the maintenance list usable',async({page},testInfo)=>{
 test.skip(!hasLocalSaveiro,'Modelo local opcional não distribuído no repositório.')
 await page.route('**/models/Saveiro_3D_grupos.json',r=>r.fulfill({status:404,body:'Not found'}))
 await signIn(page,'/garagem-3d')
 await expect(page.locator('.scene-wrap')).toHaveAttribute('data-model','generic',{timeout:45000})
 await expect(page.getByText('Não foi possível carregar a Saveiro ou seu mapeamento. A picape genérica está disponível.',{exact:true})).toBeVisible()
 await page.getByRole('button',{name:'Motor',exact:true}).click()
 await expect(page.locator('.component-panel').getByRole('heading',{name:'Motor',exact:true})).toBeVisible()
 await page.unroute('**/models/Saveiro_3D_grupos.json')
 await page.route('**/models/Saveiro_3D.glb',r=>r.fulfill({status:404,body:'Not found'}))
 await page.reload();await selectTestVehicle(page)
 await expect(page.locator('.scene-wrap')).toHaveAttribute('data-model','generic',{timeout:45000})
 await page.getByRole('button',{name:'Pneus e rodas',exact:true}).click()
 await expect(page.locator('.component-panel').getByRole('heading',{name:'Pneus e rodas',exact:true})).toBeVisible()
 const menu=page.getByRole('button',{name:'Abrir navegação'});if(await menu.isVisible())await menu.click()
 await page.getByRole('link',{name:/Meus veículos/}).click();page.once('dialog',d=>d.accept());await page.getByRole('button',{name:'Excluir Saveiro E2E'}).click()
 await expect(page.getByRole('button',{name:'Excluir Saveiro E2E',exact:true})).toHaveCount(0)
})
