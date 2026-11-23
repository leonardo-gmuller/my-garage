import Aura from '@primeuix/themes/aura'
import { definePreset } from '@primeuix/themes'

/** PrimeVue resolves these CSS variables for controls and body-mounted overlays. */
export const GarageTheme = definePreset(Aura, {
  semantic: {
    primary: Object.fromEntries([50,100,200,300,400,500,600,700,800,900,950].map(step => [step, 'var(--accent-color)'])),
    formField: { borderRadius: 'var(--radius-control)', paddingX: '.9rem', paddingY: '.75rem' },
    focusRing: { width: '2px', style: 'solid', color: 'var(--focus-ring)', offset: '3px', shadow: 'none' },
    colorScheme: { dark: {
      primary: { color: 'var(--accent-color)', inverseColor: 'var(--text-inverse)', hoverColor: 'var(--accent-hover)', activeColor: 'var(--accent-hover)' },
      highlight: { background: 'var(--glass-active)', focusBackground: 'var(--accent-pressed)', color: 'var(--accent-hover)', focusColor: 'var(--text-primary)' },
      content: { background: 'var(--glass-surface)', hoverBackground: 'var(--glass-hover)', borderColor: 'var(--glass-border)', color: 'var(--text-primary)', hoverColor: 'var(--text-primary)' },
      text: { color: 'var(--text-primary)', hoverColor: 'var(--text-primary)', mutedColor: 'var(--text-muted)', hoverMutedColor: 'var(--text-secondary)' },
      formField: { background: 'var(--control-surface)', borderColor: 'var(--control-border)', hoverBorderColor: 'var(--glass-hover-border)', focusBorderColor: 'var(--accent-color)', color: 'var(--text-primary)', placeholderColor: 'var(--text-muted)', iconColor: 'var(--text-secondary)', disabledBackground: 'var(--control-disabled)', disabledColor: 'var(--text-muted)' },
      overlay: { select: { background: 'var(--elevated-glass)', borderColor: 'var(--glass-border)', color: 'var(--text-primary)' }, modal: { background: 'var(--elevated-glass)', borderColor: 'var(--glass-border)', color: 'var(--text-primary)' } },
    } },
  },
  components: { button: { root: { borderRadius: 'var(--radius-pill)' } } },
})
