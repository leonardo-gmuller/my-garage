export interface GroupMapping {name:string;mesh_prefix:string;meshes?:number;triangles?:number}
export interface MappingDocument {groups:GroupMapping[]}
// JSON supplied with the GLB remains authoritative for group names and prefixes.
// This adapter connects geometric groups to stable business component keys.
export const saveiroModel={
 url:'/models/Saveiro_3D.glb',mappingUrl:'/models/Saveiro_3D_grupos.json',displayName:'Volkswagen Saveiro · modelo 3D',
 componentGroups:{
  wheel_1:'wheels',suspensao:'suspension',farol:'lights',lanterna:'lights',
  chassis:'body',bonnet_ok:'body',door_rf_01:'body',boot_ok:'body',
  bump_fro01:'body',bump_rea01:'body',rack:'body',rack01:'body',Friso:'body',underbody:'body',SAVEIRO__CROSS:'body',
  cofre:'engine'
 } as Record<string,string>,
 groupSubcomponents:{door_rf_01:'doors',bonnet_ok:'bonnet'} as Record<string,string>,
 // Under-bonnet components need a helper because they are occluded or absent in the asset.
 helperKeys:new Set(['engine','battery','brakes','cooling','transmission','exhaust']),
 // Original asset space: Y up, front +Z; transformed with the normalized GLTF root.
 hotspots:{engine:[-.02,1.5,2.05],battery:[-.6,1.45,1.75],brakes:[1.08,.55,1.77],cooling:[-.02,1.1,2.72],transmission:[.02,.65,.8],exhaust:[.65,.35,-2.65]} as Record<string,[number,number,number]>
}
export const groupLabels:Record<string,string>={wheel_1:'Pneus e rodas',suspensao:'Suspensão',farol:'Faróis dianteiros',lanterna:'Lanternas traseiras',chassis:'Estrutura da carroceria',bonnet_ok:'Capô',door_rf_01:'Portas',boot_ok:'Tampa da caçamba',bump_fro01:'Para-choque dianteiro',bump_rea01:'Para-choque traseiro',rack:'Rack',rack01:'Rack',Friso:'Frisos',underbody:'Parte inferior da carroceria',SAVEIRO__CROSS:'Acabamentos da carroceria',cofre:'Região do cofre do motor'}
export const genericHotspots:Record<string,[number,number,number]>={engine:[1.3,1.5,0],battery:[1.2,1.5,.55],wheels:[1.38,.55,1],brakes:[1.38,.65,-1],suspension:[-1.4,.85,.9],lights:[2.22,1.08,.62],cooling:[2,1.3,0],transmission:[.3,.8,0],exhaust:[-2,.4,.5],body:[-.9,1.35,-.85]}
export function validateMapping(value:unknown):GroupMapping[]{
 if(!value||typeof value!=='object'||!('groups'in value)||!Array.isArray(value.groups))throw new Error('Mapeamento de grupos inválido.')
 const groups=value.groups.filter((g):g is GroupMapping=>!!g&&typeof g.name==='string'&&typeof g.mesh_prefix==='string'&&g.mesh_prefix.length>0)
 if(groups.length!==value.groups.length||new Set(groups.map(g=>g.name)).size!==groups.length||new Set(groups.map(g=>g.mesh_prefix)).size!==groups.length)throw new Error('Grupos 3D inválidos ou duplicados.')
 return groups.sort((a,b)=>b.mesh_prefix.length-a.mesh_prefix.length)
}
export function groupForMesh(name:string,groups:GroupMapping[]):GroupMapping|undefined{return groups.find(g=>name.startsWith(g.mesh_prefix)||name===g.name)}
