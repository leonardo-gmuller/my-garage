<script setup lang="ts">
import { computed,onMounted,onBeforeUnmount,ref,watch } from 'vue'
import * as T from 'three'
import { OrbitControls } from 'three/addons/controls/OrbitControls.js'
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js'
import { createStudioEnvironment } from '../features/three/studioEnvironment'
import { SaveiroSurfaces } from '../features/three/saveiroSurfaces'
import { RotateCcw,Focus,Plus,Minus,MousePointer2 } from 'lucide-vue-next'
import { createPickup } from '../features/three/genericPickup'
import { saveiroModel,genericHotspots,validateMapping,groupForMesh,groupLabels } from '../features/three/modelConfig'
import { sceneTheme } from '../theme/sceneTheme'
import { MaterialSelection } from '../features/three/materialSelection'
import { VehiclePaint } from '../features/three/vehiclePaint'
import type { Component,Vehicle } from '../services/types'
const props=defineProps<{components:Component[];selected?:string|null;selectedGroup?:string|null;compact?:boolean;vehicle?:Vehicle|null}>()
const emit=defineEmits<{select:[key:string,group?:string];loaded:[kind:'saveiro'|'generic']}>()
const host=ref<HTMLDivElement>(),failure=ref(''),loading=ref(true),progress=ref(0),showPoints=ref(false),kind=ref<'saveiro'|'generic'>('generic'),hover=ref('')
const points=ref<{key:string;name:string;x:number;y:number;visible:boolean}[]>([])
const wantsSaveiro=computed(()=>import.meta.env.VITE_SAVEIRO_MODEL_AVAILABLE==='true'&&props.vehicle?.manufacturer.toLowerCase().includes('volkswagen')&&props.vehicle?.model.toLowerCase().includes('saveiro'))
const modelLabel=computed(()=>loading.value&&wantsSaveiro.value?'Carregando Volkswagen Saveiro…':kind.value==='saveiro'?saveiroModel.displayName:'Picape genérica · modelo demonstrativo')
let renderer:T.WebGLRenderer|undefined,scene:T.Scene,camera:T.PerspectiveCamera,controls:OrbitControls,model:T.Object3D|undefined,hotspotRoot:T.Object3D|undefined,environment:T.WebGLRenderTarget|undefined,frame=0,visible=true,disposed=false,dirty=true,resize:ResizeObserver,observer:IntersectionObserver
const selection=new MaterialSelection(),paint=new VehiclePaint(),surfaces=new SaveiroSurfaces(),raycaster=new T.Raycaster(),pointer=new T.Vector2(),bounds=new T.Box3(),center=new T.Vector3(),size=new T.Vector3()
let fitDistance=8,down={x:0,y:0},width=0,height=0
const currentMeshes: T.Mesh[]=[]
watch([()=>props.selected,()=>props.selectedGroup,()=>props.components,showPoints],()=>{selection.select(props.selected,kind.value==='saveiro'?props.selectedGroup:null);dirty=true})
watch([()=>props.vehicle?.colorHex,()=>props.vehicle?.paintFinish],()=>{
 if(!model)return
 selection.dispose();paint.apply(props.vehicle?.colorHex,props.vehicle?.paintFinish);selection.capture(model)
 selection.select(props.selected,kind.value==='saveiro'?props.selectedGroup:null);dirty=true
})
function fit(reset=true){
 if(!camera||!model)return
 bounds.setFromObject(model);bounds.getCenter(center);bounds.getSize(size)
 if(kind.value==='saveiro')center.y=bounds.min.y+size.y*.33
 const direction=kind.value==='saveiro'?new T.Vector3(1.65,.65,1).normalize():new T.Vector3(1,.55,1.1).normalize()
 const right=new T.Vector3().crossVectors(new T.Vector3(0,1,0),direction).normalize()
 const up=new T.Vector3().crossVectors(direction,right).normalize()
 const tanV=Math.tan(T.MathUtils.degToRad(camera.fov)/2),tanH=tanV*Math.max(camera.aspect,.1)
 // Fit every corner in camera space, accounting for depth toward the camera.
 fitDistance=0
 for(const mesh of currentMeshes){
  if(!mesh.geometry.boundingBox)mesh.geometry.computeBoundingBox()
  const box=mesh.geometry.boundingBox
  if(!box)continue
  for(const x of [box.min.x,box.max.x])for(const y of [box.min.y,box.max.y])for(const z of [box.min.z,box.max.z]){
   const corner=new T.Vector3(x,y,z).applyMatrix4(mesh.matrixWorld).sub(center),depth=corner.dot(direction)
   fitDistance=Math.max(fitDistance,Math.abs(corner.dot(right))/tanH+depth,Math.abs(corner.dot(up))/tanV+depth)
  }
 }
 fitDistance=Math.max(fitDistance*1.12,4.5)
 controls.minDistance=Math.max(size.length()*.38,1.4);controls.maxDistance=fitDistance*2.7;camera.near=.03;camera.far=Math.max(100,fitDistance*5);camera.updateProjectionMatrix()
 if(reset){controls.target.copy(center);camera.position.copy(center).addScaledVector(direction,fitDistance);controls.update()}
}
function zoom(factor:number){if(!camera)return;const offset=camera.position.clone().sub(controls.target);offset.setLength(T.MathUtils.clamp(offset.length()*factor,controls.minDistance,controls.maxDistance));camera.position.copy(controls.target).add(offset);controls.update()}
function disposeObject(object:T.Object3D){const geometries=new Set<T.BufferGeometry>(),materials=new Set<T.Material>(),textures=new Set<T.Texture>();object.traverse(o=>{if(o instanceof T.Mesh){geometries.add(o.geometry);for(const m of Array.isArray(o.material)?o.material:[o.material]){materials.add(m);for(const value of Object.values(m))if(value instanceof T.Texture)textures.add(value)}}});for(const t of textures)t.dispose();for(const m of materials)m.dispose();for(const g of geometries)g.dispose()}
function install(object:T.Object3D,modelKind:'saveiro'|'generic'){
 selection.dispose();paint.dispose();surfaces.dispose();if(model){scene.remove(model);disposeObject(model)}model=object;scene.add(object);kind.value=modelKind;currentMeshes.length=0;object.traverse(o=>{if(o instanceof T.Mesh){o.castShadow=true;o.receiveShadow=true;currentMeshes.push(o)}});if(modelKind==='saveiro')surfaces.apply(object);paint.capture(object);paint.apply(props.vehicle?.colorHex,props.vehicle?.paintFinish);selection.capture(object);selection.select(props.selected,kind.value==='saveiro'?props.selectedGroup:null);if(renderer)renderer.shadowMap.needsUpdate=true;dirty=true;fit();emit('loaded',modelKind)
}
function hitAt(x:number,y:number){if(!renderer||loading.value)return;const rect=renderer.domElement.getBoundingClientRect();pointer.set((x-rect.left)/rect.width*2-1,-(y-rect.top)/rect.height*2+1);raycaster.setFromCamera(pointer,camera);const hits=raycaster.intersectObjects(currentMeshes,false)
 // Respect the nearest visible surface: do not select a wheel through a door or glass.
 const hit=hits.find(h=>{const mesh=h.object as T.Mesh;const material=Array.isArray(mesh.material)?mesh.material[h.face?.materialIndex??0]:mesh.material;return material?.visible&&(!material.transparent||material.opacity>.05)})
 return hit?.object.userData.componentKey?hit.object:undefined
}
function pointerDown(e:PointerEvent){down={x:e.clientX,y:e.clientY}}
function pointerMove(e:PointerEvent){if(e.pointerType!=='mouse'||e.buttons)return;const hit=hitAt(e.clientX,e.clientY);hover.value=hit?(groupLabels[hit.userData.groupName]??props.components.find(c=>c.key===hit.userData.componentKey)?.name??''):'';if(renderer)renderer.domElement.style.cursor=hit?'pointer':'grab'}
function pointerLeave(){hover.value=''}
function click(e:MouseEvent){if(Math.hypot(e.clientX-down.x,e.clientY-down.y)>6)return;const hit=hitAt(e.clientX,e.clientY);if(hit)emit('select',hit.userData.componentKey,hit.userData.groupName)}
function contextLost(e:Event){e.preventDefault();failure.value='A visualização 3D foi interrompida. Recarregue a página ou use a lista de componentes.';cancelAnimationFrame(frame)}
function updatePoints(){
 if(!showPoints.value||loading.value){points.value=[];return}
 const config=kind.value==='saveiro'?saveiroModel.hotspots:genericHotspots
 points.value=props.components.filter(c=>kind.value!=='saveiro'||saveiroModel.helperKeys.has(c.key)).map(c=>{const xyz=config[c.key];if(!xyz)return {key:c.key,name:c.name,x:0,y:0,visible:false};const p=new T.Vector3(...xyz);if(kind.value==='saveiro'&&hotspotRoot)hotspotRoot.localToWorld(p);p.project(camera);return{key:c.key,name:c.name,x:(p.x+1)*width/2,y:(-p.y+1)*height/2,visible:p.z>-1&&p.z<1&&Math.abs(p.x)<1&&Math.abs(p.y)<1}})
}
onMounted(async()=>{
 try{
  const studio=sceneTheme()
  renderer=new T.WebGLRenderer({antialias:true,alpha:true});renderer.setPixelRatio(Math.min(devicePixelRatio,2));renderer.shadowMap.enabled=true;renderer.shadowMap.type=T.PCFSoftShadowMap;renderer.shadowMap.autoUpdate=false;renderer.shadowMap.needsUpdate=true;renderer.toneMapping=T.ACESFilmicToneMapping;renderer.toneMappingExposure=studio.exposure;renderer.outputColorSpace=T.SRGBColorSpace
  renderer.domElement.setAttribute('aria-label','Veículo 3D. Arraste para girar. Selecione as peças com um toque ou use a lista de componentes.');renderer.domElement.setAttribute('role','img');renderer.domElement.style.cursor='grab';host.value!.prepend(renderer.domElement)
  scene=new T.Scene();camera=new T.PerspectiveCamera(36,1,.03,100);controls=new OrbitControls(camera,renderer.domElement);controls.enableDamping=!matchMedia('(prefers-reduced-motion: reduce)').matches;controls.enablePan=false;controls.addEventListener('change',()=>{dirty=true});controls.minPolarAngle=.12;controls.maxPolarAngle=Math.PI*.56;controls.target.set(0,1,0);camera.position.set(6,4,7)
  environment=createStudioEnvironment(renderer,studio.reflectionBackground);scene.environment=environment.texture
  scene.add(new T.HemisphereLight(studio.hemisphere,studio.ground,.45));const key=new T.DirectionalLight(studio.key,2.2);key.position.set(3,7,5);key.castShadow=true;key.shadow.mapSize.set(1024,1024);key.shadow.camera.left=-5;key.shadow.camera.right=5;key.shadow.camera.top=5;key.shadow.camera.bottom=-5;key.shadow.normalBias=.04;scene.add(key)
  const fill=new T.DirectionalLight(studio.fill,.65);fill.position.set(-4,3,-4);scene.add(fill)
  const rim=new T.DirectionalLight(studio.key,.8);rim.position.set(-3,4,3);scene.add(rim)
  const floor=new T.Mesh(new T.CircleGeometry(3.45,80),new T.ShadowMaterial({color:studio.floor,opacity:.4}));floor.rotation.x=-Math.PI/2;floor.position.y=-.025;floor.receiveShadow=true;scene.add(floor)
  resize=new ResizeObserver(()=>{if(!host.value||!renderer)return;const rect=host.value.getBoundingClientRect();if(!rect.width||!rect.height)return;const changed=width!==rect.width||height!==rect.height;width=rect.width;height=rect.height;dirty=true;renderer.setSize(width,height);camera.aspect=width/height;camera.updateProjectionMatrix();if(changed)fit()});resize.observe(host.value!)
  observer=new IntersectionObserver(([entry])=>{visible=entry?.isIntersecting??false;dirty=true});observer.observe(host.value!)
  const canvas=renderer.domElement;canvas.addEventListener('pointerdown',pointerDown);canvas.addEventListener('pointermove',pointerMove);canvas.addEventListener('pointerleave',pointerLeave);canvas.addEventListener('click',click);canvas.addEventListener('webglcontextlost',contextLost)
  function animate(){if(disposed)return;frame=requestAnimationFrame(animate);if(!visible||!renderer)return;const changed=controls.update();if(dirty||changed){renderer.render(scene,camera);updatePoints();dirty=false}}
  animate()
  if(wantsSaveiro.value){
   try{
    const [modelResult,mappingResult]=await Promise.allSettled([new GLTFLoader().loadAsync(saveiroModel.url,e=>{if(e.total)progress.value=Math.round(e.loaded/e.total*100)}),fetch(saveiroModel.mappingUrl).then(async r=>{if(!r.ok)throw new Error('Mapeamento indisponível.');return validateMapping(await r.json())})])
    if(modelResult.status!=='fulfilled')throw modelResult.reason
    const loaded=modelResult.value
    if(disposed){disposeObject(loaded.scene);return}
    if(mappingResult.status!=='fulfilled'){disposeObject(loaded.scene);throw mappingResult.reason}
    const mapping=mappingResult.value
    let mapped=0
    loaded.scene.traverse(o=>{if(o instanceof T.Mesh){const group=groupForMesh(o.name,mapping);if(group){o.userData.groupName=group.name;const component=saveiroModel.componentGroups[group.name];if(component){o.userData.componentKey=component;mapped++}}}})
    if(!mapped){disposeObject(loaded.scene);throw new Error('Nenhuma malha corresponde ao mapeamento.')}
    const sourceBounds=new T.Box3().setFromObject(loaded.scene),sourceSize=sourceBounds.getSize(new T.Vector3()),sourceCenter=sourceBounds.getCenter(new T.Vector3());const maxSize=Math.max(sourceSize.x,sourceSize.y,sourceSize.z)
    if(!Number.isFinite(maxSize)||maxSize<=0){disposeObject(loaded.scene);throw new Error('Dimensões inválidas.')}
    const normalized=new T.Group();loaded.scene.position.sub(new T.Vector3(sourceCenter.x,sourceBounds.min.y,sourceCenter.z));normalized.add(loaded.scene);normalized.scale.setScalar(4.7/maxSize);hotspotRoot=loaded.scene;install(normalized,'saveiro')
   }catch{if(disposed)return;failure.value='Não foi possível carregar a Saveiro ou seu mapeamento. A picape genérica está disponível.';install(createPickup(),'generic')}
  }else install(createPickup(),'generic')
  loading.value=false;dirty=true
 }catch{loading.value=false;failure.value='Visualização 3D indisponível neste dispositivo. Use a lista para consultar todos os componentes.'}
})
onBeforeUnmount(()=>{disposed=true;cancelAnimationFrame(frame);resize?.disconnect();observer?.disconnect();controls?.dispose();if(renderer){const c=renderer.domElement;c.removeEventListener('pointerdown',pointerDown);c.removeEventListener('pointermove',pointerMove);c.removeEventListener('pointerleave',pointerLeave);c.removeEventListener('click',click);c.removeEventListener('webglcontextlost',contextLost)}selection.dispose();paint.dispose();surfaces.dispose();if(scene)disposeObject(scene);environment?.dispose();renderer?.dispose();renderer?.domElement.remove()})
</script>
<template><div class="scene-wrap" :class="{compact}" :data-model="loading?'loading':kind"><div ref="host" class="scene-host"><div v-if="showPoints&&!loading" class="hotspot-layer"><button v-for="p in points.filter(p=>p.visible)" :key="p.key" class="hotspot" :class="{selected:p.key===selected}" :style="{left:p.x+'px',top:p.y+'px'}" :aria-label="'Selecionar '+p.name" :aria-pressed="p.key===selected" @click="emit('select',p.key)"><span></span><b>{{p.name}}</b></button></div></div><div class="scene-label"><span class="live-dot"></span>{{modelLabel}}</div><div v-if="loading" class="scene-loading" role="status">Carregando modelo 3D{{progress?' · '+progress+'%':'…'}}</div><div v-if="failure" class="scene-error" role="status">{{failure}}</div><div v-if="hover&&!loading" class="scene-hover">{{hover}}<small>Toque para consultar o histórico</small></div><div class="scene-controls"><button class="icon-button" aria-label="Aproximar câmera" :disabled="loading" @click="zoom(.85)"><Plus :size="18"/></button><button class="icon-button" aria-label="Afastar câmera" :disabled="loading" @click="zoom(1.15)"><Minus :size="18"/></button><span></span><button class="icon-button" aria-label="Restaurar câmera" :disabled="loading" @click="fit()"><RotateCcw :size="17"/></button><button class="icon-button" :aria-label="showPoints?'Ocultar pontos auxiliares':'Mostrar pontos auxiliares'" :aria-pressed="showPoints" @click="showPoints=!showPoints"><Focus :size="19"/></button></div><div class="scene-hint"><MousePointer2 :size="14"/>Arraste para girar · toque nas peças para selecionar</div></div></template>
