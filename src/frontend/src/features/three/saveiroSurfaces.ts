import { Material, Mesh, MeshStandardMaterial, Object3D } from 'three'
import mapping from './saveiroSurfaces.json'

type Profile = keyof typeof mapping.profiles

/** Reversible model-specific finish layer, beneath custom paint and selection. */
export class SaveiroSurfaces {
  private originals = new Map<Mesh, Material | Material[]>()
  private clones = new Map<Material, Map<Profile, MeshStandardMaterial>>()

  apply(root: Object3D) {
    this.dispose()
    root.traverse(object => {
      if (!(object instanceof Mesh)) return
      const original = object.material as Material | Material[]
      const finish = (source: Material) => {
        if (!(source instanceof MeshStandardMaterial)) return source
        const rule = mapping.meshOverrides.find(rule => rule.meshes.includes(object.name)) ?? mapping.rules.find(rule => rule.groups.includes(object.userData.groupName) && rule.materials.includes(source.name))
        if (!rule) return source
        const profile = rule.profile as Profile
        let variants = this.clones.get(source)
        if (!variants) { variants = new Map(); this.clones.set(source, variants) }
        let clone = variants.get(profile)
        if (!clone) {
          clone = source.clone()
          const { color, ...settings } = mapping.profiles[profile]
          clone.color.set(color)
          Object.assign(clone, settings)
          variants.set(profile, clone)
        }
        return clone
      }
      const adjusted = Array.isArray(original) ? original.map(finish) : finish(original)
      const changed = Array.isArray(original) ? original.some((material, index) => material !== (adjusted as Material[])[index]) : original !== adjusted
      if (changed) { this.originals.set(object, original); object.material = adjusted }
    })
  }

  dispose() {
    for (const [mesh, original] of this.originals) mesh.material = original
    for (const variants of this.clones.values()) for (const material of variants.values()) material.dispose()
    this.originals.clear(); this.clones.clear()
  }
}
