import { test,expect } from '@playwright/test'
// Original, minimal triangle GLB, authored here solely to verify the real loader.
function glb(){
 const positions=new Float32Array([0,0,0,1,0,0,0,1,0]);const binary=Buffer.from(positions.buffer)
 const doc={asset:{version:'2.0'},scene:0,scenes:[{nodes:[0]}],nodes:[{mesh:0,name:'EngineMesh'}],meshes:[{primitives:[{attributes:{POSITION:0}}]}],buffers:[{byteLength:binary.length}],bufferViews:[{buffer:0,byteOffset:0,byteLength:binary.length}],accessors:[{bufferView:0,componentType:5126,count:3,type:'VEC3',min:[0,0,0],max:[1,1,0]}]}
 let json=Buffer.from(JSON.stringify(doc));const padding=(4-json.length%4)%4;json=Buffer.concat([json,Buffer.alloc(padding,32)])
 const header=Buffer.alloc(12);header.writeUInt32LE(0x46546c67,0);header.writeUInt32LE(2,4);header.writeUInt32LE(12+8+json.length+8+binary.length,8)
 const jsonHeader=Buffer.alloc(8);jsonHeader.writeUInt32LE(json.length,0);jsonHeader.writeUInt32LE(0x4e4f534a,4)
 const binHeader=Buffer.alloc(8);binHeader.writeUInt32LE(binary.length,0);binHeader.writeUInt32LE(0x004e4942,4)
 return Buffer.concat([header,jsonHeader,json,binHeader,binary])
}
test('real GLTFLoader imports an original GLB with a named component mesh',async({page})=>{
 await page.route('**/models/test.glb',r=>r.fulfill({body:glb(),contentType:'model/gltf-binary'}))
 await page.goto('/')
 const result=await page.evaluate(async()=>{
  const path='/node_modules/three/examples/jsm/loaders/GLTFLoader.js'
  const {GLTFLoader}=await import(/* @vite-ignore */path)
  const loaded=await new GLTFLoader().loadAsync('/models/test.glb')
  return loaded.scene.getObjectByName('EngineMesh')?.isMesh===true
 })
 expect(result).toBeTruthy()
})

test('original pickup recolors only tagged body panels and restores its materials',async({page})=>{
 await page.goto('/')
 const result=await page.evaluate(async()=>{
  const [{createPickup},{VehiclePaint}]=await Promise.all(['/src/features/three/genericPickup.ts','/src/features/three/vehiclePaint.ts'].map(path=>import(/* @vite-ignore */path)))
  const pickup=createPickup(),originals=new Map<any,any>()
  pickup.traverse((mesh:any)=>{if(mesh.isMesh)originals.set(mesh,mesh.material)})
  const paint=new VehiclePaint();paint.capture(pickup);paint.apply('#214B80','Metallic')
  const panels=[...originals.keys()].filter(mesh=>mesh.userData.paintable)
  const blue=panels.length>0&&panels.every(mesh=>mesh.material.color.getHexString()==='214b80')
  const trimPreserved=[...originals].filter(([mesh])=>!mesh.userData.paintable).every(([mesh,material])=>mesh.material===material)
  const cached=panels[0].material;paint.apply('#C0C0C0','Metallic')
  const reused=panels.every(mesh=>mesh.material===cached&&mesh.material.color.getHexString()==='c0c0c0')
  paint.dispose()
  return{blue,trimPreserved,reused,restored:[...originals].every(([mesh,material])=>mesh.material===material)}
 })
 expect(result).toEqual({blue:true,trimPreserved:true,reused:true,restored:true})
})
