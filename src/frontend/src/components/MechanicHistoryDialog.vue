<script setup lang="ts">
import PaymentSummary from './PaymentSummary.vue'
import { ref, watch } from 'vue'
import Dialog from 'primevue/dialog'
import Button from 'primevue/button'
import { ChevronLeft,ChevronRight } from 'lucide-vue-next'
import { api } from '../services/api'
import { date,money } from '../services/types'
import type { Mechanic,Page,RecordEntry } from '../services/types'
import { useGarage } from '../stores/garage'
const visible=defineModel<boolean>('visible',{default:false}),props=defineProps<{mechanic:Mechanic|null}>()
const g=useGarage(),rows=ref<RecordEntry[]>([]),total=ref(0),page=ref(1),busy=ref(false),error=ref('')
let version=0
async function load(){
 if(!visible.value||!props.mechanic)return
 const current=++version;busy.value=true;error.value=''
 try{const result=await api<Page<RecordEntry>>(`/mechanics/${props.mechanic.id}/records?page=${page.value}&pageSize=10`);if(current===version){rows.value=result.items;total.value=result.total}}
 catch(e){if(current===version)error.value=(e as Error).message}finally{if(current===version)busy.value=false}
}
watch(visible,value=>{version++;rows.value=[];total.value=0;if(value){if(page.value===1)load();else page.value=1}})
watch(page,load)
const vehicleName=(id:string)=>{const v=g.vehicles.find(v=>v.id===id);return v?[v.nickname||v.model,v.modelYear].join(' · '):'Veículo'}
</script>
<template>
 <Dialog v-model:visible="visible" modal :header="'Serviços · '+(mechanic?.name??'')" :style="{width:'660px'}" :breakpoints="{'700px':'95vw'}">
  <p class="form-hint">Serviços vinculados a este contato em todos os seus veículos.</p>
  <p v-if="busy" role="status" class="muted">Carregando serviços…</p>
  <p v-if="error" role="alert" class="error-banner">{{error}} <Button class="text-button" @click="load">Tentar novamente</Button></p>
  <div class="mechanic-history">
   <article v-for="r in rows" :key="r.id" class="mechanic-history__record">
    <div class="section-header"><span class="muted">{{date(r.date)}} · {{vehicleName(r.vehicleId)}}</span><strong>{{money(r.totalCost)}}</strong></div>
    <h3>{{r.description}}</h3><PaymentSummary :record="r" /><p v-if="r.packageTotal!=null" class="form-hint">Valor total fechado · sem detalhamento</p><p v-if="r.mechanicName" class="muted">{{r.mechanicName}}{{r.workshop?' · '+r.workshop:''}}</p>
    <p v-if="r.subcomponents.length" class="form-hint">{{r.subcomponents.map(s=>s.name).join(' · ')}}</p>
    <p v-if="r.notes" class="mechanic-history__notes">{{r.notes}}</p>
   </article>
  </div>
  <p v-if="!busy&&!error&&!rows.length" class="muted">Nenhum serviço vinculado a este mecânico.</p>
  <div v-if="total>10" class="pagination"><Button class="button" aria-label="Serviços anteriores" :disabled="page===1||busy" @click="page--"><ChevronLeft :size="16"/></Button><span>{{page}} / {{Math.ceil(total/10)}}</span><Button class="button" aria-label="Próximos serviços" :disabled="page*10>=total||busy" @click="page++"><ChevronRight :size="16"/></Button></div>
 </Dialog>
</template>
