<script setup lang="ts">
import { nextTick, onBeforeUnmount, ref, watch } from 'vue'
import Textarea from 'primevue/textarea'
import Button from 'primevue/button'
import { ArrowUpRight, ArrowRight, BookOpenText, CircleHelp, LoaderCircle, MessageSquareText } from 'lucide-vue-next'
import { useGarage } from '../stores/garage'
import { api } from '../services/api'
import type { HistoryAnswer, MaintenanceAiStatus } from '../services/types'
import { date, money, number } from '../services/types'
import EmptyState from '../components/EmptyState.vue'
const g=useGarage(),question=ref(''),asked=ref(''),busy=ref(false),checking=ref(false),error=ref(''),status=ref<MaintenanceAiStatus|null>(null),answer=ref<HistoryAnswer|null>(null),answerHeading=ref<HTMLElement|null>(null)
const suggestions=['Quando troquei as velas?','Quanto gastei com suspensão?','Quais manutenções estão pendentes?','Quando foi a última revisão?']
let version=0,controller:AbortController|null=null
function cancel(){version++;controller?.abort();controller=null;busy.value=false;checking.value=false}
watch(()=>g.selectedId,async()=>{
 cancel();question.value='';asked.value='';answer.value=null;error.value='';status.value=null
 if(!g.selectedId)return
 const current=version;checking.value=true;controller=new AbortController()
 try{const result=await api<MaintenanceAiStatus>('/ai/maintenance/status',{signal:controller.signal});if(current===version)status.value=result}
 catch{if(current===version)error.value='Não foi possível verificar a disponibilidade. Tente abrir esta tela novamente.'}
 finally{if(current===version)checking.value=false}
},{immediate:true})
onBeforeUnmount(cancel)
async function ask(){
 if(!g.selectedId||!status.value?.available||!question.value.trim()||busy.value)return
 const current=++version,vehicleId=g.selectedId;controller=new AbortController();busy.value=true;error.value='';answer.value=null;asked.value=question.value.trim()
 try{
  const result=await api<HistoryAnswer>('/ai/assistant/ask',{method:'POST',body:JSON.stringify({vehicleId,question:asked.value}),signal:controller.signal})
  if(current!==version||g.selectedId!==vehicleId)return
  if(!result||result.vehicleId!==vehicleId||!['latest','spending','pending','clarification'].includes(result.kind)||typeof result.message!=='string'||!Array.isArray(result.records)||!Array.isArray(result.schedules)||!Array.isArray(result.warnings))throw new Error('A consulta recebida é inválida. Tente novamente.')
  answer.value=result;await nextTick();answerHeading.value?.focus({preventScroll:true});answerHeading.value?.closest('section')?.scrollIntoView({block:'start',behavior:window.matchMedia('(prefers-reduced-motion: reduce)').matches?'auto':'smooth'})
 }catch(e){if(current===version)error.value=(e as Error).message}
 finally{if(current===version)busy.value=false}
}
function suggest(text:string){question.value=text;document.getElementById('assistant-question')?.focus()}
function stop(){cancel();error.value='Consulta cancelada. Nenhum dado foi alterado.'}
const stateLabels:Record<string,string>={Current:'Programada',DueSoon:'Em breve',Overdue:'Atrasada',Completed:'Concluída'}
</script>
<template>
 <div v-if="g.selected" class="history-assistant">
  <section class="assistant-intro"><p class="assistant-eyebrow"><MessageSquareText :size="15"/>Assistente do histórico <span>Leitura</span></p><h2>Seu histórico.<br><span>Uma pergunta de cada vez.</span></h2><p>Encontre os cuidados de {{g.selected.nickname||g.selected.model}} nos serviços e agendamentos que você registrou.</p></section>
  <div class="assistant-layout">
   <section class="assistant-query panel" aria-label="Consultar histórico">
    <form @submit.prevent="ask">
     <div class="assistant-section-label"><span>01 / Consulta</span><small>{{g.selected.model}} · {{g.selected.modelYear}}</small></div>
     <label for="assistant-question">O que você quer consultar?</label>
     <Textarea id="assistant-question" v-model="question" rows="4" auto-resize :maxlength="status?.maxMessageLength??4000" :disabled="busy" required placeholder="Ex.: quanto gastei com suspensão em 2026?" aria-describedby="assistant-privacy assistant-limit"/>
     <small id="assistant-limit" class="form-hint">{{question.length}} / {{status?.maxMessageLength??4000}} caracteres</small>
     <div class="assistant-suggestions" aria-label="Sugestões de perguntas"><Button v-for="text in suggestions" :key="text" type="button" class="button" :disabled="busy" @click="suggest(text)">{{text}}<ArrowUpRight :size="14"/></Button></div>
     <p id="assistant-privacy" class="form-hint">A pergunta e o catálogo do veículo são enviados ao Google Gemini. Seu histórico permanece na garagem e não é enviado ao modelo. Evite dados pessoais.</p>
     <p v-if="checking" class="ai-processing" role="status"><LoaderCircle :size="17"/>Verificando disponibilidade…</p>
     <p v-else-if="status&&!status.available" class="ai-unavailable" role="status">{{status.reason}} A consulta manual continua disponível no histórico e na agenda.</p>
     <p v-if="error" class="form-error" role="alert">{{error}}</p>
     <div class="assistant-submit"><span v-if="busy" class="ai-processing" role="status"><LoaderCircle :size="17"/>Consultando os registros…</span><small v-else class="muted">Nenhum dado será alterado.</small><Button v-if="busy" type="button" class="button" @click="stop">Cancelar</Button><Button v-else type="submit" class="button primary" :disabled="checking||!status?.available||!question.trim()">Consultar histórico<ArrowRight :size="16"/></Button></div>
    </form>
   </section>
   <aside class="assistant-guide"><BookOpenText :size="25"/><h3>A resposta começa<br>nos seus registros.</h3><p>Últimos serviços, gastos e agendamentos em aberto. Cada resultado leva à origem da informação.</p><ul><li>Um componente por pergunta.</li><li>Todo o histórico ou um ano completo.</li><li>Valores fechados continuam fechados, sem divisão entre peças.</li></ul><RouterLink :to="{path:'/historico',query:{vehicle:g.selectedId}}" class="text-button">Explorar histórico<ArrowUpRight :size="15"/></RouterLink></aside>
  </div>
  <section v-if="answer" class="assistant-answer panel" aria-label="Resultado da consulta">
   <div class="assistant-section-label"><span>02 / Resultado</span><span>Fonte: registros da sua garagem</span></div>
   <p class="assistant-question-echo">{{asked}}</p><h2 ref="answerHeading" tabindex="-1">{{answer.kind==='clarification'?'Vamos ajustar a pergunta':answer.kind==='spending'?'Gastos encontrados':answer.kind==='pending'?'Agendamentos em aberto':'Último serviço encontrado'}}</h2>
   <p>{{answer.message}}</p><p class="assistant-scope">{{answer.scope}}</p>
   <div v-if="answer.kind==='spending'" class="assistant-amount"><small>{{answer.pendingPaymentCount?'Total conhecido · cobranças pendentes':'Total conhecido'}}</small><strong>{{money(answer.total??0)}}</strong><span>{{answer.count}} {{answer.count===1?'serviço registrado':'serviços registrados'}}</span></div>
   <p v-if="answer.kind==='pending'&&answer.count" class="muted">{{answer.count}} {{answer.count===1?'agendamento encontrado':'agendamentos encontrados'}} · exibindo {{answer.schedules.length}}</p>
   <ul v-if="answer.warnings.length" class="assistant-warnings"><li v-for="warning in answer.warnings" :key="warning"><CircleHelp :size="16"/><span>{{warning}}</span></li></ul>
   <div v-if="answer.records.length" class="assistant-sources"><p class="assistant-source-heading">{{answer.kind==='spending'?'Serviços mais recentes do recorte · '+answer.records.length+' de '+answer.count:'Registro de origem'}}</p><RouterLink v-for="record in answer.records" :key="record.id" :to="{path:'/historico',query:{vehicle:answer.vehicleId,record:record.id}}" class="assistant-source"><div><small>{{date(record.date)}} · {{record.mileage==null?'Km não informada':number(record.mileage)+' km'}}</small><h3>{{record.description}}</h3><span v-if="record.closedTotal" class="muted">Valor total fechado</span><span v-if="record.pendingPaymentCount" class="muted"> · Cobrança pendente</span></div><strong>{{money(record.totalCost)}}</strong><ArrowUpRight :size="20"/></RouterLink></div>
   <div v-if="answer.schedules.length" class="assistant-sources"><RouterLink v-for="{schedule} in answer.schedules" :key="schedule.id" :to="{path:'/agenda',query:{vehicle:answer.vehicleId}}" class="assistant-source"><div><small>{{schedule.componentName}}{{schedule.subcomponentName?' · '+schedule.subcomponentName:''}}</small><h3>{{schedule.description}}</h3><span class="muted">{{schedule.dueDate?date(schedule.dueDate):'Sem data'}}{{schedule.dueMileage!=null?' · '+number(schedule.dueMileage)+' km':''}}</span></div><span class="badge" :class="schedule.state==='Overdue'?'danger':''">{{stateLabels[schedule.state]}}</span><ArrowUpRight :size="20"/></RouterLink></div>
  </section>
 </div>
 <EmptyState v-else title="Selecione um veículo" description="O assistente consulta os registros do veículo selecionado."/>
</template>
