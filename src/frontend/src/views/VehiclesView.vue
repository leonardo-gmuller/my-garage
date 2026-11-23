<script setup lang="ts">
import { useCardMotion } from '../composables/useCardMotion'
import { ref } from 'vue'
import { CarFront,Plus,Pencil,Trash2,Gauge } from 'lucide-vue-next'
import { useToast } from 'primevue/usetoast'
import { useGarage } from '../stores/garage'
import { api } from '../services/api'
import { number } from '../services/types'
import type { Vehicle } from '../services/types'
import VehicleDialog from '../components/VehicleDialog.vue'
import EmptyState from '../components/EmptyState.vue'
const g=useGarage(),toast=useToast(),open=ref(false),edit=ref<Vehicle|null>(null),deleting=ref('')
async function remove(v:Vehicle){if(!confirm(`Excluir ${v.nickname||v.model} e todo seu histórico, anexos e agendamentos? Esta ação não pode ser desfeita.`))return;deleting.value=v.id;try{await api('/vehicles/'+v.id,{method:'DELETE'});await g.loadVehicles();toast.add({severity:'success',summary:'Veículo excluído',life:3000})}catch(e){toast.add({severity:'error',summary:(e as Error).message,life:5000})}finally{deleting.value=''}}
const { cardInitial, cardEnter } = useCardMotion()
</script>
<template><div class="section-header"><p class="view-description">Cada veículo com sua própria história.</p><button class="button" @click="edit=null;open=true"><Plus :size="17"/>Adicionar veículo</button></div><div class="vehicle-grid"><article v-for="v in g.vehicles" :key="v.id" v-motion :initial="cardInitial" :enter="cardEnter(40)" class="panel vehicle-card" :class="{selected:g.selectedId===v.id}"><div class="vehicle-photo"><img v-if="v.photoUrl" :src="v.photoUrl" :alt="v.manufacturer+' '+v.model"><CarFront v-else :size="88" :stroke-width=".8"/><span v-if="g.selectedId===v.id" class="selected-tag">Selecionado</span></div><div class="vehicle-card-body"><p class="muted">{{v.manufacturer}} · {{v.modelYear}}</p><h2>{{v.nickname||v.model}}</h2><p class="muted">{{v.nickname?v.model:v.version||'Versão não informada'}}</p><div class="vehicle-details"><span><Gauge :size="17"/>{{v.mileage==null?'Quilometragem não informada':number(v.mileage)+' km'}}</span><span>{{v.color||'Cor não informada'}} · {{v.plate||'Placa não informada'}}</span></div><div class="vehicle-actions"><button class="button" :aria-label="'Selecionar '+(v.nickname||v.model)" :disabled="g.selectedId===v.id" @click="g.select(v.id)">{{g.selectedId===v.id?'Veículo selecionado':'Selecionar veículo'}}</button><button class="icon-button" :aria-label="'Editar '+(v.nickname||v.model)" @click="edit=v;open=true"><Pencil :size="17"/></button><button class="icon-button danger" :aria-label="'Excluir '+(v.nickname||v.model)" :disabled="deleting===v.id" @click="remove(v)"><Trash2 :size="17"/></button></div></div></article></div><EmptyState v-if="!g.vehicles.length" title="Um lugar para seu carro" description="Cadastre seu primeiro veículo e comece a cuidar de cada detalhe."/><VehicleDialog v-model:visible="open" :vehicle="edit"/></template>
