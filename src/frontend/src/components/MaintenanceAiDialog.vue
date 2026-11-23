<script setup lang="ts">
import {ref,watch,onBeforeUnmount} from 'vue'
import Dialog from 'primevue/dialog'
import Textarea from 'primevue/textarea'
import Button from 'primevue/button'
import {WandSparkles,ArrowRight,LoaderCircle} from 'lucide-vue-next'
import {api} from '../services/api'
import {useGarage} from '../stores/garage'
import type {MaintenanceAiStatus,MaintenanceAiDraft} from '../services/types'
const visible=defineModel<boolean>('visible',{default:false})
const emit=defineEmits<{interpreted:[draft:MaintenanceAiDraft];manual:[]}>()
const g=useGarage(),message=ref(''),busy=ref(false),checking=ref(false),error=ref(''),status=ref<MaintenanceAiStatus|null>(null)
let controller:AbortController|null=null,version=0
function cancel(){version++;controller?.abort();controller=null;busy.value=false;checking.value=false}
watch(visible,async open=>{
 cancel();error.value=''
 if(!open)return
 const current=version;checking.value=true;status.value=null;controller=new AbortController()
 try{const result=await api<MaintenanceAiStatus>('/ai/maintenance/status',{signal:controller.signal});if(current===version)status.value=result}
 catch{if(current===version)error.value='Não foi possível consultar a IA. Você pode continuar pelo formulário manual.'}
 finally{if(current===version)checking.value=false}
})
onBeforeUnmount(cancel)
async function interpret(){
 if(!g.selectedId||!status.value?.available||!message.value.trim()||busy.value)return
 const current=++version,vehicleId=g.selectedId;controller=new AbortController();busy.value=true;error.value=''
 try{
  const draft=await api<MaintenanceAiDraft>('/ai/maintenance/interpret',{method:'POST',body:JSON.stringify({vehicleId,message:message.value}),signal:controller.signal})
  if(current!==version||!visible.value||g.selectedId!==vehicleId)return
  if(!draft||typeof draft.description!=='string'||!Array.isArray(draft.components)||!Array.isArray(draft.items)||!Array.isArray(draft.missingFields)||!Array.isArray(draft.warnings))throw new Error('A sugestão recebida é inválida. Tente novamente ou registre manualmente.')
  if(draft.vehicleId!==vehicleId)throw new Error('O veículo da sugestão não corresponde ao selecionado. Tente novamente.')
  emit('interpreted',draft);visible.value=false
 }catch(e){if(current===version)error.value=e instanceof SyntaxError?'A sugestão recebida é inválida. Tente novamente ou registre manualmente.':(e as Error).message}
 finally{if(current===version)busy.value=false}
}
function manual(){cancel();visible.value=false;emit('manual')}
</script>
<template>
 <Dialog v-model:visible="visible" modal header="Registrar com IA" class="maintenance-ai-dialog" :style="{width:'640px'}" :breakpoints="{'700px':'95vw'}">
  <form class="ai-intake" @submit.prevent="interpret">
   <div class="ai-intake-intro"><span class="ai-tool-icon"><WandSparkles :size="23"/></span><div><h3>Conte o que foi feito no carro.</h3><p>A IA organiza o relato. Você revisa e confirma cada informação antes de salvar.</p></div></div>
   <p class="ai-selected-vehicle">Veículo selecionado <strong>{{g.selected?.nickname||g.selected?.model}} · {{g.selected?.modelYear}}</strong></p>
   <label for="ai-maintenance-message">Descrição da manutenção<Textarea id="ai-maintenance-message" v-model="message" rows="6" auto-resize :maxlength="status?.maxMessageLength??4000" :disabled="busy" required placeholder="Ex.: troquei os amortecedores, os cabos e as velas. O serviço custou R$ 850 e o carro estava com 125 mil km." aria-describedby="ai-data-notice ai-message-limit" /></label>
   <small id="ai-message-limit" class="form-hint">{{message.length}} / {{status?.maxMessageLength??4000}} caracteres</small>
   <p id="ai-data-notice" class="form-hint">Ao interpretar, o relato e os dados básicos do veículo serão enviados ao Google Gemini. Não inclua documentos, telefone ou outros dados pessoais. Histórico e anexos não são enviados.</p>
   <p v-if="checking" role="status" class="ai-processing"><LoaderCircle :size="17"/>Verificando disponibilidade…</p>
   <p v-else-if="status&&!status.available" role="status" class="ai-unavailable">{{status.reason}}</p>
   <p v-if="busy" role="status" class="ai-processing"><LoaderCircle :size="17"/>Interpretando o relato. Nenhum serviço foi salvo.</p>
   <p v-if="error" role="alert" class="form-error">{{error}}</p>
   <div class="form-actions ai-intake-actions"><Button type="button" class="button" @click="manual">Continuar manualmente</Button><Button type="submit" class="button primary" :disabled="busy||checking||!status?.available||!message.trim()">{{busy?'Interpretando…':'Interpretar e revisar'}}<ArrowRight v-if="!busy" :size="16"/></Button></div>
  </form>
 </Dialog>
</template>
