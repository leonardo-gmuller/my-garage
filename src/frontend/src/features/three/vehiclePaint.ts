import { Material, Mesh, MeshPhysicalMaterial, MeshStandardMaterial, Object3D } from 'three'
import paintMapping from './paintMapping.json'
import type { PaintFinish } from '../../services/types'

export const paintFinishes: Record<PaintFinish, { metalness: number; roughness: number; envMapIntensity: number; clearcoat: number; clearcoatRoughness: number }> = {
  Solid: { metalness: .15, roughness: .34, envMapIntensity: .8, clearcoat: .45, clearcoatRoughness: .22 },
  Metallic: { metalness: .78, roughness: .3, envMapIntensity: 1.1, clearcoat: .55, clearcoatRoughness: .2 },
  Pearlescent: { metalness: .4, roughness: .25, envMapIntensity: 1.1, clearcoat: .65, clearcoatRoughness: .18 },
  Matte: { metalness: .12, roughness: .72, envMapIntensity: .65, clearcoat: 0, clearcoatRoughness: .5 },
}

/** One cached PBR clone per eligible material, shared only by the paintable slots. */
export class VehiclePaint {
  private originals = new Map<Mesh, Material | Material[]>()
  private clones = new Map<Material, MeshPhysicalMaterial>()
  capture(root: Object3D) {
    root.traverse(object => {
      if (object instanceof Mesh && (object.userData.paintable===true || paintMapping.groups.includes(object.userData.groupName))) this.originals.set(object, object.material)
    })
  }
  apply(hex: string | null | undefined, finish: PaintFinish | null | undefined) {
    const valid = !!hex && /^#[0-9a-f]{6}$/i.test(hex)
    const settings = paintFinishes[finish ?? 'Metallic'] ?? paintFinishes.Metallic
    const paint = (material: Material, explicitlyPaintable: boolean) => {
      if (!valid || !(material instanceof MeshStandardMaterial) || material.transparent || (!explicitlyPaintable && !paintMapping.materials.includes(material.name))) return material
      let clone = this.clones.get(material)
      if (!clone) {
        clone = new MeshPhysicalMaterial()
        // Copy Standard properties (maps, sides, vertex colors) without replacing Physical defaults.
        MeshStandardMaterial.prototype.copy.call(clone, material)
        this.clones.set(material, clone)
      }
      clone.color.set(hex!); Object.assign(clone, settings)
      return clone
    }
    for (const [mesh, original] of this.originals) {
      const apply = (material: Material) => paint(material, mesh.userData.paintable===true)
      mesh.material = Array.isArray(original) ? original.map(apply) : apply(original)
    }
  }
  dispose() {
    for (const [mesh, original] of this.originals) mesh.material = original
    for (const material of this.clones.values()) material.dispose()
    this.originals.clear(); this.clones.clear()
  }
}
