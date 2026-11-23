import { Color, Mesh, MeshBasicMaterial, PlaneGeometry, PMREMGenerator, Scene, Vector3, WebGLRenderer } from 'three'

/** Static softboxes: reflections have light bands and dark gaps, rather than an all-white room. */
export function createStudioEnvironment(renderer: WebGLRenderer, background: string) {
  const room = new Scene()
  room.background = new Color(background)
  const geometry = new PlaneGeometry(1, 1)
  const material = new MeshBasicMaterial({ color: new Color(2.4, 2.4, 2.4), toneMapped: false })
  const target = new Vector3(0, 1, 0)
  const panels = [
    { position: [0, 6, 0], size: [4, 7] },
    { position: [6, 2, 0], size: [7, 3] },
    { position: [-6, 2, -1], size: [7, 3] },
    { position: [0, 3, 6], size: [4, 2] },
  ]
  for (const panel of panels) {
    const mesh = new Mesh(geometry, material)
    mesh.position.set(...panel.position as [number, number, number])
    mesh.scale.set(panel.size[0]!, panel.size[1]!, 1)
    mesh.lookAt(target); room.add(mesh)
  }
  const pmrem = new PMREMGenerator(renderer)
  try { return pmrem.fromScene(room, .04, .1, 30, { size: 128 }) }
  finally { geometry.dispose(); material.dispose(); pmrem.dispose() }
}
