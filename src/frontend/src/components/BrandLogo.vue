<script setup lang="ts">
withDefaults(defineProps<{
  variant?: 'horizontal' | 'stacked' | 'symbol' | 'wordmark'
  tagline?: boolean
  decorative?: boolean
}>(), { variant: 'horizontal', tagline: false, decorative: false })
</script>

<template>
  <span class="brand-logo" :class="`brand-logo--${variant}`" :role="decorative ? undefined : 'img'"
    :aria-label="decorative ? undefined : 'Minha Garagem'" :aria-hidden="decorative ? true : undefined">
    <span v-if="variant !== 'wordmark'" class="brand-logo__emblem" aria-hidden="true">
      <img src="/brand/minha-garagem-cobre-v1.png" width="1254" height="1254" alt="" decoding="async">
    </span>
    <span v-if="variant !== 'symbol'" class="brand-logo__signature" aria-hidden="true">
      <span class="brand-logo__wordmark">minhagaragem</span>
      <small v-if="tagline" class="brand-logo__tagline">Cuide de cada quilômetro.</small>
    </span>
  </span>
</template>
