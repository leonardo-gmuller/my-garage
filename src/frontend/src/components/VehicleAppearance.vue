<script setup lang="ts">
import { ref, watch, computed, nextTick } from 'vue'
import ColorPicker from 'primevue/colorpicker'
import InputText from 'primevue/inputtext'
import Select from 'primevue/select'
import Button from 'primevue/button'
import { Palette } from 'lucide-vue-next'
import { api } from '../services/api'
import { useGarage } from '../stores/garage'
import { finishLabels } from '../services/types'
import type { Vehicle, PaintFinish } from '../services/types'
const props = defineProps<{ vehicle: Vehicle }>()
const emit = defineEmits<{ preview: [value: { colorHex: string; paintFinish: PaintFinish } | null] }>()
const g = useGarage(), expanded = ref(false), name = ref(''), hex = ref(''), finish = ref<PaintFinish>('Metallic'), busy = ref(false), error = ref(''), success = ref('')
const valid = computed(() => /^#[0-9a-f]{6}$/i.test(hex.value) && !!name.value.trim())
const picker = computed({ get: () => hex.value.replace('#', ''), set: value => { hex.value = '#' + value } })
function reset() { name.value = props.vehicle.color ?? ''; hex.value = props.vehicle.colorHex ?? '#C0C0C0'; finish.value = props.vehicle.paintFinish ?? 'Metallic'; emit('preview', null); error.value = ''; success.value = '' }
watch(() => [props.vehicle.id, props.vehicle.colorHex, props.vehicle.paintFinish, props.vehicle.color], reset, { immediate: true })
watch([hex, finish], () => { if (/^#[0-9a-f]{6}$/i.test(hex.value)) emit('preview', { colorHex: hex.value, paintFinish: finish.value }); success.value = '' })
async function save() {
  if (!valid.value) return
  busy.value = true; error.value = ''
  try {
    const vehicle = await api<Vehicle>(`/vehicles/${props.vehicle.id}/paint`, { method: 'PUT', body: JSON.stringify({ color: name.value.trim(), colorHex: hex.value, paintFinish: finish.value }) })
    const index = g.vehicles.findIndex(v => v.id === vehicle.id)
    if (index >= 0) g.vehicles[index] = vehicle
    await nextTick()
    success.value = 'Cor salva no veículo.'
  } catch (e) { error.value = (e as Error).message }
  finally { busy.value = false }
}
</script>
<template>
  <section class="vehicle-appearance">
    <div class="appearance-heading">
      <span class="paint-swatch" :style="{ backgroundColor: vehicle.colorHex ?? 'var(--silver)' }" aria-hidden="true"></span>
      <span><strong>{{ vehicle.color || 'Cor não informada' }}</strong><small>{{ vehicle.colorHex || 'Sem pintura personalizada' }} · {{ vehicle.paintFinish ? finishLabels[vehicle.paintFinish] : 'Acabamento original' }}</small></span>
      <Button class="button" :aria-expanded="expanded" aria-controls="vehicle-paint-form" @click="expanded=!expanded"><Palette :size="16" />Personalizar cor</Button>
    </div>
    <form v-if="expanded" id="vehicle-paint-form" class="paint-form form-grid" @submit.prevent="save">
      <label for="paint-name">Nome da cor<InputText id="paint-name" v-model="name" maxlength="80" required /></label>
      <div class="form-field"><label for="paint-hex">Cor hexadecimal</label><div class="color-picker-field"><ColorPicker v-model="picker" input-id="paint-picker" :pt="{preview:{'aria-label':'Selecionar cor da carroceria'}}" format="hex" /><InputText id="paint-hex" v-model="hex" maxlength="7" pattern="#[0-9a-fA-F]{6}" required /></div></div>
      <label for="paint-finish">Acabamento<Select input-id="paint-finish" aria-label="Acabamento" v-model="finish" :options="Object.entries(finishLabels).map(([value,label])=>({value,label}))" option-label="label" option-value="value" /></label>
      <p class="full form-hint">Prévia imediata da pintura externa. Salve para manter a cor ao reabrir a garagem.</p>
      <p v-if="error" role="alert" class="full form-error">{{error}}</p><p v-if="success" role="status" class="full form-hint">{{success}}</p>
      <div class="full form-actions"><Button type="button" class="button" :disabled="busy" @click="reset">Restaurar cor salva</Button><Button type="submit" class="button primary" :disabled="busy || !valid">{{busy?'Salvando…':'Salvar cor'}}</Button></div>
    </form>
  </section>
</template>
