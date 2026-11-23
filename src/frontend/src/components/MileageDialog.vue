<script setup lang="ts">
import InputNumber from 'primevue/inputnumber'
import Button from 'primevue/button'

import { ref,watch } from 'vue'
import Dialog from 'primevue/dialog'
import { useGarage } from '../stores/garage'
import { api } from '../services/api'
import { number,date } from '../services/types'
import type { Page,Mileage } from '../services/types'
const visible=defineModel<boolean>('visible',{default:false}),g=useGarage(),value=ref<number|null>(null),busy=ref(false),error=ref(''),history=ref<Mileage[]>([])
watch(visible,async v=>{if(!v)return;value.value=g.selected?.mileage??null;error.value='';try{history.value=(await api<Page<Mileage>>(`/vehicles/${g.selectedId}/mileage?pageSize=5`)).items}catch(e){error.value=(e as Error).message}})
async function save(){busy.value=true;try{await api(`/vehicles/${g.selectedId}/mileage`,{method:'POST',body:JSON.stringify({mileage:Number(value.value)})});await g.loadVehicles();visible.value=false}catch(e){error.value=(e as Error).message}finally{busy.value=false}}
</script>
<template><Dialog v-model:visible="visible" modal header="Atualizar quilometragem" :style="{width:'480px'}" :breakpoints="{'600px':'95vw'}"><form class="form-grid single" @submit.prevent="save"><p class="muted">Informe o valor exibido no hodômetro. Os alertas por distância serão recalculados.</p><label for="mileage-current">Quilometragem atual (km)<InputNumber input-id="mileage-current" v-model="value" :min="g.selected?.mileage!=null?g.selected.mileage+1:0" :max="10000000" :use-grouping="false" required /></label><p v-if="error" class="form-error" role="alert">{{error}}</p><Button type="submit" class="button primary" :disabled="busy">{{busy?'Salvando…':'Atualizar quilometragem'}}</Button><h3 v-if="history.length">Últimas atualizações</h3><div v-for="h in history" :key="h.id" class="data-row"><span>{{date(h.recordedAt)}}</span><strong>{{number(h.mileage)}} km</strong></div></form></Dialog></template>
