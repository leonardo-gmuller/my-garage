<script setup lang="ts">
import PaymentSummary from '../components/PaymentSummary.vue'
import { useCardMotion } from '../composables/useCardMotion'
import { computed, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import Button from 'primevue/button'
import { Box, Plus, ArrowUpRight, Layers, CalendarClock } from 'lucide-vue-next'
import { groupLabels, saveiroModel } from '../features/three/modelConfig'
import { useGarage } from '../stores/garage'
import { api } from '../services/api'
import { money, date, number } from '../services/types'
import type { RecordEntry, ComponentSummary, PaintFinish } from '../services/types'
import VehicleScene from '../components/VehicleScene.vue'
import VehicleAppearance from '../components/VehicleAppearance.vue'
import EmptyState from '../components/EmptyState.vue'
import ServiceList from '../components/ServiceList.vue'
import RecordDialog from '../components/RecordDialog.vue'
import ScheduleDialog from '../components/ScheduleDialog.vue'
const g=useGarage(), route=useRoute(), router=useRouter()
const selected=ref(typeof route.query.component==='string'?route.query.component:'wheels')
const selectedGroup=ref<string|null>(typeof route.query.group==='string'?route.query.group:null)
const selectedSub=ref<string|null>(typeof route.query.subcomponent==='string'?route.query.subcomponent:null)
const modelKind=ref('saveiro'), summary=ref<ComponentSummary|null>(null), error=ref(''), loading=ref(false), open=ref(false), scheduleOpen=ref(false), edit=ref<RecordEntry|null>(null)
const preview=ref<{colorHex:string;paintFinish:PaintFinish}|null>(null)
const sceneVehicle=computed(()=>g.selected?{...g.selected,...preview.value}:null)
const component=computed(()=>g.components.find(c=>c.key===selected.value))
const subcomponent=computed(()=>component.value?.subcomponents.find(s=>s.id===selectedSub.value))
const historyQuery=computed(()=>({component:component.value?.id,...(subcomponent.value?{subcomponent:subcomponent.value.id}:{})}))
let request=0
async function load(){
 const current=++request
 if(!component.value){summary.value=null;return}
 loading.value=true;error.value=''
 const query=subcomponent.value?'?subcomponent='+subcomponent.value.id:''
 try{const result=await api<ComponentSummary>(`/vehicles/${g.selectedId}/components/${component.value.id}/summary${query}`);if(current===request)summary.value=result}
 catch(e){if(current===request){summary.value=null;error.value=(e as Error).message}}
 finally{if(current===request)loading.value=false}
}
watch([()=>component.value?.id,()=>subcomponent.value?.id],load,{immediate:true})
watch(()=>[route.query.component,route.query.group,route.query.subcomponent],()=>{
 selected.value=typeof route.query.component==='string'?route.query.component:'wheels'
 selectedGroup.value=typeof route.query.group==='string'?route.query.group:null
 selectedSub.value=typeof route.query.subcomponent==='string'?route.query.subcomponent:null
})
function updateQuery(){router.replace({query:{component:selected.value,...(selectedGroup.value?{group:selectedGroup.value}:{}),...(selectedSub.value?{subcomponent:selectedSub.value}:{})}})}
function select(key:string,group?:string){
 selected.value=key;selectedGroup.value=group??null
 const subKey=group?saveiroModel.groupSubcomponents[group]:undefined
 selectedSub.value=subKey?g.components.find(c=>c.key===key)?.subcomponents.find(s=>s.key===subKey)?.id??null:null
 updateQuery()
}
function selectSub(id:string|null){selectedSub.value=id;updateQuery()}
function newService(){edit.value=null;open.value=true}
const {cardInitial,cardEnter}=useCardMotion()
</script>
<template>
  <template v-if="g.selected">
    <p class="view-description">Explore o veículo e selecione um conjunto. Os detalhes técnicos ficam disponíveis mesmo sem uma malha interna no modelo.</p>
    <section class="garage-layout">
      <div class="garage-main panel">
        <div class="garage-title"><Box :size="20"/><span>{{g.selected.manufacturer}} {{g.selected.model}}<small>{{g.selected.modelYear}} · {{modelKind==='saveiro'?'modelo da Saveiro':'representação genérica'}}</small></span></div>
        <VehicleScene :vehicle="sceneVehicle" :components="g.components" :selected="selected" :selected-group="selectedGroup" @loaded="kind=>modelKind=kind" @select="select"/>
        <VehicleAppearance :vehicle="g.selected" @preview="preview=$event" />
        <div class="component-selector" aria-label="Componentes do veículo"><Button v-for="c in g.components" :key="c.id" :class="{active:selected===c.key}" :aria-pressed="selected===c.key" @click="select(c.key)">{{c.name}}</Button></div>
        <section v-if="component" class="system-detail" aria-label="Detalhamento do sistema">
          <div class="section-header"><div><span class="component-eyebrow"><Layers :size="15"/>Detalhamento do sistema</span><h3>{{selected==='engine'?'Motor / ignição':component.name}}</h3></div></div>
          <p class="form-hint">Lista técnica complementar. Selecione uma peça para consultar o histórico e registrar o cuidado específico; a seleção 3D representa o conjunto.</p>
          <div class="technical-list"><Button class="technical-part" :class="{active:!subcomponent}" :aria-pressed="!subcomponent" @click="selectSub(null)">Todo o componente</Button><Button v-for="sub in component.subcomponents" :key="sub.id" class="technical-part" :class="{active:subcomponent?.id===sub.id}" :aria-pressed="subcomponent?.id===sub.id" @click="selectSub(sub.id)">{{sub.name}}</Button></div>
        </section>
      </div>
      <aside v-motion :initial="cardInitial" :enter="cardEnter(70)" class="component-panel panel">
        <span class="component-eyebrow">Componente selecionado</span><h2>{{component?.name||'Selecione um componente'}}</h2>
        <p v-if="selectedGroup" class="selected-group">{{groupLabels[selectedGroup]||selectedGroup}}</p>
        <p v-if="subcomponent" class="selected-subcomponent">{{subcomponent.name}}</p>
        <p v-if="modelKind==='saveiro'" class="form-hint">{{selected==='engine'?'O modelo representa o cofre. As peças internas são acessadas pela lista técnica.':['battery','brakes','cooling','transmission','exhaust'].includes(selected)?'Sem malha específica identificada. Use os detalhes técnicos ou os pontos auxiliares opcionais.':'Selecione as peças diretamente no modelo ou use a lista da cena.'}}</p>
        <div v-if="loading" role="status" class="muted">Buscando cuidados…</div><p v-if="error" role="alert" class="form-error">{{error}}</p>
        <template v-if="summary&&!loading">
          <p v-if="summary.pendingPaymentCount" class="form-hint payment-pending">Há juros ainda não informados nos serviços deste componente.</p><div class="component-summary"><div><span>Serviços registrados</span><strong>{{summary.serviceCount}}</strong></div><div><span>{{summary.pendingPaymentCount?'Total conhecido dos serviços':'Total dos serviços vinculados'}}</span><strong>{{money(summary.totalCost)}}</strong></div></div>
          <p class="form-hint">Valor dos serviços vinculados, contado uma vez por registro. Valores de diferentes componentes podem compartilhar o mesmo serviço.</p>
        </template>
        <Button class="button primary" :disabled="!component" @click="newService"><Plus :size="17"/>Registrar neste componente</Button>
        <section v-if="summary?.lastService&&!loading" class="component-last-service">
          <span class="component-eyebrow">Último serviço · {{date(summary.lastService.date)}}</span><h3>{{summary.lastService.description}}</h3>
          <p v-if="summary.lastService.mechanicName" class="muted">Mecânico: {{summary.lastService.mechanicName}}</p>
          <p v-if="summary.lastService.workshop" class="muted">{{summary.lastService.workshop}}</p>
          <PaymentSummary :record="summary.lastService" /><h4>Peças trocadas</h4><div v-if="summary.lastService.items.length" class="record-parts"><div v-for="item in summary.lastService.items" :key="item.id" class="data-row"><span>{{item.quantity}} × {{item.name}}</span><span>{{item.total==null?'Incluído no total':money(item.total)}}</span></div></div><p v-else class="muted">Nenhuma peça informada neste serviço.</p>
          <h4>Observações</h4><p class="record-notes">{{summary.lastService.notes||'Nenhuma observação informada.'}}</p>
        </section>
        <section class="component-next-maintenance"><h3><CalendarClock :size="17"/>Próxima manutenção sugerida</h3>
          <template v-if="summary?.nextMaintenance&&!loading"><strong>{{summary.nextMaintenance.description}}</strong><p class="muted">{{summary.nextMaintenance.dueDate?date(summary.nextMaintenance.dueDate):'Sem data definida'}}{{summary.nextMaintenance.dueMileage!=null?' · '+number(summary.nextMaintenance.dueMileage)+' km':''}}</p><p class="form-hint">Baseada no agendamento cadastrado{{summary.nextMaintenance.subcomponentName?' para '+summary.nextMaintenance.subcomponentName:''}}.</p></template>
          <p v-else class="muted">Sem previsão cadastrada. Defina a próxima revisão conforme o plano do veículo ou a orientação da oficina.</p>
          <Button class="text-button" :disabled="!component" @click="scheduleOpen=true">Programar para este componente<ArrowUpRight :size="16"/></Button>
        </section>
        <div class="section-header"><h3>Histórico {{subcomponent?'do subcomponente':'do componente'}}</h3></div>
        <ServiceList v-if="summary?.recent.length&&!loading" :records="summary.recent" @select="r=>{edit=r;open=true}"/>
        <EmptyState v-else-if="!loading&&!error" title="Nenhum cuidado registrado" description="Registre o primeiro serviço deste componente."/>
        <RouterLink v-if="summary?.serviceCount" :to="{path:'/historico',query:historyQuery}" class="text-button">Ver histórico completo<ArrowUpRight :size="17"/></RouterLink>
      </aside>
    </section>
  </template>
  <EmptyState v-else title="Escolha um veículo" description="Adicione um veículo para explorar a garagem 3D."/>
  <RecordDialog v-model:visible="open" :record="edit" :component-key="selected" :subcomponent-id="subcomponent?.id" @saved="load();g.refresh()"/>
  <ScheduleDialog v-model:visible="scheduleOpen" :component-id="component?.id" :subcomponent-id="subcomponent?.id" @saved="load" />
</template>
