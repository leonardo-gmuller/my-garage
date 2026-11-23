import { themeColor } from '../theme/sceneTheme'
import { computed, onScopeDispose, ref } from 'vue'
import type { ComputedRef } from 'vue'
import type { Variant } from '@vueuse/motion'

/** Small entrances respond to route/data changes; OS reduced motion stays reactive. */
export function useCardMotion() {
  const preference = window.matchMedia('(prefers-reduced-motion: reduce)')
  const reduced = ref(preference.matches)
  const update = () => { reduced.value = preference.matches }
  preference.addEventListener('change', update)
  onScopeDispose(() => preference.removeEventListener('change', update))
  const cardInitial = computed<Variant>(() => reduced.value
    ? { opacity: 1, y: 0, scale: 1 }
    : { opacity: 0, y: 7, scale: .992 })
  const entries = new Map<number, ComputedRef<Variant>>()
  const cardEnter = (delay = 0): Variant => {
    if (!entries.has(delay)) entries.set(delay, computed<Variant>(() => ({
      opacity: 1, y: 0, scale: 1,
      transition: { type: 'tween', duration: reduced.value ? 0 : parseFloat(themeColor('--duration-enter')), delay: reduced.value ? 0 : delay, ease: 'easeOut' },
    })))
    return entries.get(delay)!.value
  }
  return { cardInitial, cardEnter }
}
