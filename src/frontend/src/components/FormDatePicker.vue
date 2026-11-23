<script setup lang="ts">
import { computed } from 'vue'
import DatePicker from 'primevue/datepicker'
const model = defineModel<string>({ default: '' })
const props = defineProps<{ inputId: string; monthOnly?: boolean; maxDate?: Date; required?: boolean }>()
const selected = computed({
  get: () => model.value ? new Date((props.monthOnly ? model.value + '-01' : model.value) + 'T12:00:00') : null,
  set: (value: Date | null) => {
    if (!value || Number.isNaN(value.getTime())) { model.value = ''; return }
    const year = value.getFullYear(), month = String(value.getMonth() + 1).padStart(2, '0')
    model.value = props.monthOnly ? `${year}-${month}` : `${year}-${month}-${String(value.getDate()).padStart(2, '0')}`
  },
})
</script>
<template>
  <DatePicker v-model="selected" :input-id="inputId" :view="monthOnly ? 'month' : 'date'"
    :date-format="monthOnly ? 'mm/yy' : 'dd/mm/yy'" :max-date="maxDate" :required="required"
    :show-on-focus="false" show-icon icon-display="input" show-button-bar :placeholder="monthOnly ? 'mm/aaaa' : 'dd/mm/aaaa'" />
</template>
