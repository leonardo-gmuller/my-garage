/** Read the same design tokens used by CSS and PrimeVue, once when a scene is built. */
export function themeColor(name: string): string {
  const value = getComputedStyle(document.documentElement).getPropertyValue(name).trim()
  if (!value) throw new Error(`Missing design token: ${name}`)
  return value
}
export function sceneTheme() {
  return {
    key: themeColor('--scene-key'), fill: themeColor('--scene-fill'),
    hemisphere: themeColor('--scene-hemisphere'), ground: themeColor('--scene-ground'),
    floor: themeColor('--scene-floor'), ring: themeColor('--scene-ring'),
    reflectionBackground: themeColor('--scene-environment'),
    exposure: Number(themeColor('--scene-exposure')),
  }
}
