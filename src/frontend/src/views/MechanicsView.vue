<script setup lang="ts">
import { ref,watch } from 'vue'
import InputText from 'primevue/inputtext'
import Select from 'primevue/select'
import Button from 'primevue/button'
import Dialog from 'primevue/dialog'
import { Plus,UserRound,Phone,Mail,MapPin,Pencil,Archive,RotateCcw,History,ChevronLeft,ChevronRight,Search } from 'lucide-vue-next'
import { useToast } from 'primevue/usetoast'
import { useCardMotion } from '../composables/useCardMotion'
import { useMechanics } from '../stores/mechanics'
import { api } from '../services/api'
import type { Mechanic,Page } from '../services/types'
import { date } from '../services/types'
import EmptyState from '../components/EmptyState.vue'
import MechanicDialog from '../components/MechanicDialog.vue'
import MechanicHistoryDialog from '../components/MechanicHistoryDialog.vue'
const rows=ref<Mechanic[]>([]),total=ref(0),page=ref(1),search=ref(''),status=ref('active'),busy=ref(false),error=ref('')
const open=ref(false),editing=ref<Mechanic|null>(null),confirming=ref<Mechanic|null>(null),actionBusy=ref(false),history=ref<Mechanic|null>(null),historyOpen=ref(false)
const toast=useToast(),directory=useMechanics(),{cardInitial,cardEnter}=useCardMotion()
const statuses=[{value:'active',label:'Ativos'},{value:'archived',label:'Arquivados'},{value:'all',label:'Todos'}]
let version=0
async function load(){
 const current=++version;busy.value=true;error.value=''
 try{const query=new URLSearchParams({page:String(page.value),pageSize:'12',search:search.value,status:status.value});const result=await api<Page<Mechanic>>('/mechanics?'+query);if(current===version){rows.value=result.items;total.value=result.total}}
 catch(e){if(current===version)error.value=(e as Error).message}finally{if(current===version)busy.value=false}
}
function filter(){if(page.value===1)load();else page.value=1}
watch([page,()=>directory.revision],load,{immediate:true});watch(status,filter)
function saved(){page.value=1}
async function archive(){
 if(!confirming.value)return
 actionBusy.value=true;error.value=''
 try{await api('/mechanics/'+confirming.value.id,{method:'DELETE'});confirming.value=null;toast.add({severity:'success',summary:'Contato arquivado',life:3000});directory.changed();saved()}
 catch(e){error.value=(e as Error).message;confirming.value=null}finally{actionBusy.value=false}
}
async function restore(m:Mechanic){actionBusy.value=true;error.value='';try{await api('/mechanics/'+m.id+'/restore',{method:'POST'});toast.add({severity:'success',summary:'Contato restaurado',life:3000});directory.changed();saved()}catch(e){error.value=(e as Error).message}finally{actionBusy.value=false}}
const dial=(phone:string)=>'tel:'+phone.replace(/[^+\d]/g,'')
</script>
<template>
 <div class="section-header mechanics-heading"><p class="view-description">Os profissionais que ajudam a cuidar do seu carro, sempre por perto.</p><Button class="button primary" @click="editing=null;open=true"><Plus :size="17"/>Cadastrar mecânico</Button></div>
 <form class="panel mechanics-filters" @submit.prevent="filter">
  <label for="mechanic-search">Buscar contato<InputText id="mechanic-search" v-model="search" maxlength="200" placeholder="Nome, oficina, especialidade ou telefone" /></label>
  <label for="mechanic-status">Situação<Select input-id="mechanic-status" aria-label="Situação" v-model="status" :options="statuses" option-label="label" option-value="value" /></label>
  <Button type="submit" class="button" :disabled="busy"><Search :size="17"/>Buscar</Button>
 </form>
 <div class="section-header"><p class="muted">{{total}} {{total===1?'contato encontrado':'contatos encontrados'}}</p><span v-if="busy" role="status">Carregando contatos…</span></div>
 <p v-if="error" class="error-banner" role="alert">{{error}} <Button class="text-button" @click="load">Tentar novamente</Button></p>
 <div class="mechanics-grid" :aria-busy="busy">
  <article v-for="m in rows" :key="m.id" v-motion :initial="cardInitial" :enter="cardEnter(30)" class="panel mechanic-card" :class="{'mechanic-card--archived':m.isArchived}">
   <div class="mechanic-card__heading"><span class="mechanic-avatar"><UserRound :size="24"/></span><div><h2>{{m.name}}</h2><p class="muted">{{m.workshop||'Oficina não informada'}}</p></div><span v-if="m.isArchived" class="mechanic-archived">Arquivado</span></div>
   <p v-if="m.specialty" class="mechanic-specialty">{{m.specialty}}</p>
   <div class="mechanic-contact">
    <a v-if="m.phone" :href="dial(m.phone)"><Phone :size="16"/><span>{{m.phone}}</span></a>
    <a v-if="m.email" :href="'mailto:'+m.email"><Mail :size="16"/><span>{{m.email}}</span></a>
    <p v-if="m.address"><MapPin :size="16"/><span>{{m.address}}</span></p>
    <p v-if="!m.phone&&!m.email&&!m.address" class="muted">Você pode adicionar os dados de contato ao editar.</p>
   </div>
   <p v-if="m.notes" class="mechanic-notes">{{m.notes}}</p>
   <div class="mechanic-service-summary"><span><strong>{{m.serviceCount}}</strong> {{m.serviceCount===1?'serviço':'serviços'}} na sua garagem</span><small>{{m.lastServiceDate?'Último em '+date(m.lastServiceDate):'Sem serviços vinculados'}}</small></div>
   <div class="mechanic-card__actions"><Button class="button" @click="history=m;historyOpen=true"><History :size="16"/>Ver serviços</Button><Button class="icon-button" :aria-label="'Editar mecânico '+m.name" @click="editing=m;open=true"><Pencil :size="17"/></Button><Button v-if="!m.isArchived" class="icon-button" :aria-label="'Arquivar mecânico '+m.name" :disabled="actionBusy" @click="confirming=m"><Archive :size="17"/></Button><Button v-else class="icon-button" :aria-label="'Restaurar mecânico '+m.name" :disabled="actionBusy" @click="restore(m)"><RotateCcw :size="17"/></Button></div>
  </article>
 </div>
 <EmptyState v-if="!busy&&!error&&!rows.length" :title="status==='archived'?'Nenhum mecânico arquivado':search?'Nenhum contato encontrado':'Sua agenda de confiança começa aqui'" :description="search?'Tente outro nome, oficina ou especialidade.':status==='archived'?'Contatos arquivados aparecerão aqui.':'Cadastre um mecânico para guardar os contatos e vinculá-lo aos próximos serviços.'" />
 <div v-if="total>12" class="pagination"><Button class="button" aria-label="Página anterior de mecânicos" :disabled="page===1||busy" @click="page--"><ChevronLeft :size="16"/></Button><span>Página {{page}} de {{Math.ceil(total/12)}}</span><Button class="button" aria-label="Próxima página de mecânicos" :disabled="page*12>=total||busy" @click="page++"><ChevronRight :size="16"/></Button></div>
 <MechanicDialog v-model:visible="open" :mechanic="editing" @saved="saved" />
 <MechanicHistoryDialog v-model:visible="historyOpen" :mechanic="history" />
 <Dialog :visible="!!confirming" modal header="Arquivar mecânico" :style="{width:'440px'}" :breakpoints="{'500px':'95vw'}" :closable="!actionBusy" @update:visible="value=>{if(!value)confirming=null}"><p>Arquivar {{confirming?.name}}? O contato sairá das opções para novos serviços. O histórico e os vínculos existentes serão mantidos.</p><div class="form-actions"><Button class="button" :disabled="actionBusy" @click="confirming=null">Cancelar</Button><Button class="button primary" :disabled="actionBusy" @click="archive">{{actionBusy?'Arquivando…':'Arquivar contato'}}</Button></div></Dialog>
</template>
