import { Color,Material,Mesh,MeshStandardMaterial,Object3D } from 'three'
import { themeColor } from '../../theme/sceneTheme'
/** Highlight clones keep textures, transparency and PBR properties. Originals are never mutated. */
export class MaterialSelection {
 private originals=new Map<Mesh,Material|Material[]>()
 private clones=new Map<Material,Material>()
 capture(root:Object3D){root.traverse(o=>{if(o instanceof Mesh)this.originals.set(o,o.material)})}
 private highlighted(original:Material):Material{
  const cached=this.clones.get(original);if(cached)return cached
  const clone=original.clone()
  if(clone instanceof MeshStandardMaterial){clone.emissive=new Color(themeColor('--scene-selection-emissive'));clone.emissiveIntensity=.35;clone.color.lerp(new Color(themeColor('--scene-selection')),.6)}
  this.clones.set(original,clone);return clone
 }
 select(component:string|null|undefined,group:string|null|undefined){
  for(const [mesh,original]of this.originals){
   const active=!!component&&mesh.userData.componentKey===component&&(!group||mesh.userData.groupName===group)
   mesh.material=active?(Array.isArray(original)?original.map(m=>this.highlighted(m)):this.highlighted(original)):original
  }
 }
 restore(){for(const [mesh,original]of this.originals)mesh.material=original}
 dispose(){this.restore();for(const clone of this.clones.values())clone.dispose();this.clones.clear();this.originals.clear()}
}
