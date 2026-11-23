<script setup lang="ts">
import { useCardMotion } from '../composables/useCardMotion'
import { computed, ref } from 'vue'
import { ArrowUpRight, Gauge, Wallet, Wrench, CalendarCheck, Plus, ChevronRight, CalendarClock } from 'lucide-vue-next'
import Button from 'primevue/button'
import { useRouter } from 'vue-router'
import { useGarage } from '../stores/garage'
import { money, number, date, states } from '../services/types'
import type { RecordEntry } from '../services/types'
import VehicleScene from '../components/VehicleScene.vue'
import ExpenseChart from '../components/ExpenseChart.vue'
import EmptyState from '../components/EmptyState.vue'
import ServiceList from '../components/ServiceList.vue'
import RecordDialog from '../components/RecordDialog.vue'
import MileageDialog from '../components/MileageDialog.vue'
const g=useGarage(), router=useRouter(), mileageOpen=ref(false), recordOpen=ref(false), edit=ref<RecordEntry|null>(null)
const activeSchedules = computed(() => g.schedules.filter(s => !s.completed))
const next = computed(() => [...activeSchedules.value].sort((a,b) => ({Overdue:0,DueSoon:1,Current:2}[a.state]??3)-({Overdue:0,DueSoon:1,Current:2}[b.state]??3)).slice(0,3))
const nextDate = computed(() => activeSchedules.value.filter(s=>s.dueDate).sort((a,b)=>a.dueDate!.localeCompare(b.dueDate!))[0]?.dueDate??null)
function openRecord(r:RecordEntry){edit.value=r;recordOpen.value=true}
function component(key:string,group?:string){router.push({path:'/garagem-3d',query:{component:key,...(group?{group}:{})}})}
const { cardInitial, cardEnter } = useCardMotion()
</script>
<template>
  <section v-if="g.selected" class="dashboard-grid">
    <article v-motion :initial="cardInitial" :enter="cardEnter()" class="vehicle-stage panel dashboard-vehicle">
      <div class="stage-heading">
        <div><p>{{g.selected.manufacturer}} <span class="subtle-slash">/</span> {{g.selected.modelYear}}</p><h2>{{g.selected.nickname||g.selected.model}}</h2><span class="muted">{{g.selected.version||'Versão não informada'}}</span></div>
        <RouterLink to="/garagem-3d" class="button subtle">Explorar em 3D<ArrowUpRight :size="17" /></RouterLink>
      </div>
      <VehicleScene :vehicle="g.selected" :components="g.components" compact @select="component" />
      <div class="stage-footer"><span><span class="vehicle-presence-dot"></span>Sua garagem digital</span><RouterLink to="/veiculos">Detalhes do veículo<ChevronRight :size="14" /></RouterLink></div>
    </article>
    <div class="dashboard-side">
      <section class="dashboard-summary" aria-label="Indicadores do veículo">
        <article v-motion :initial="cardInitial" :enter="cardEnter(45)" class="panel summary-tile"><span><Wallet :size="16" />Manutenção</span><strong class="money-value">{{g.indicators?money(g.indicators.total):'—'}}</strong><small>{{g.indicators?.pendingPaymentCount?'Total conhecido · juros pendentes':'Total acumulado'}}</small><i class="tile-rule"></i></article>
        <article v-motion :initial="cardInitial" :enter="cardEnter(45)" class="panel summary-tile"><span><Wrench :size="16" />Serviços</span><strong>{{g.indicators?.serviceCount??'—'}}</strong><small>Registrados no veículo</small><i class="tile-rule"></i></article>
        <article v-motion :initial="cardInitial" :enter="cardEnter(45)" class="panel summary-tile"><span><CalendarClock :size="16" />Agendamentos</span><strong>{{activeSchedules.length}}</strong><small>Cuidados em aberto</small><i class="tile-rule"></i></article>
      </section>
      <div class="dashboard-care">
        <article v-motion :initial="cardInitial" :enter="cardEnter(80)" class="mileage-block panel">
          <span class="stat-label"><Gauge :size="18" />Hodômetro</span>
          <div class="mileage-value">{{g.selected.mileage==null?'—':number(g.selected.mileage)}}<small>km</small></div>
          <p>{{g.selected.mileage==null?'Informe a quilometragem para acompanhar os próximos cuidados.':'Cada quilômetro conta. Mantenha seus alertas em dia.'}}</p>
          <span class="manual-label">Atualização manual</span>
          <Button class="button" @click="mileageOpen=true">Atualizar quilometragem<ArrowUpRight :size="16" /></Button>
        </article>
        <article v-motion :initial="cardInitial" :enter="cardEnter(105)" class="panel schedules-panel">
          <div class="section-header"><h2>Próximos cuidados</h2><RouterLink to="/agenda" class="icon-button" aria-label="Ver agenda"><ArrowUpRight :size="19" /></RouterLink></div>
          <div v-if="next.length" class="upcoming-list"><RouterLink v-for="s in next" :key="s.id" to="/agenda" class="upcoming-item"><span class="status-dot" :class="s.state"></span><div><strong>{{s.description}}</strong><small>{{s.dueDate?date(s.dueDate):number(s.dueMileage??0)+' km'}}</small><span class="status-label" :class="s.state">{{states[s.state]}}</span></div></RouterLink></div>
          <EmptyState v-else title="Seu próximo cuidado" description="Programe uma revisão por data ou quilometragem."><RouterLink to="/agenda" class="text-button"><Plus :size="16" />Programar manutenção</RouterLink></EmptyState>
        </article>
      </div>
    </div>
    <article v-motion :initial="cardInitial" :enter="cardEnter(100)" class="panel expense-panel dashboard-expenses">
      <div class="section-header"><div><h2>Gastos ao longo do ano</h2><p class="muted">Todos os gastos, mês a mês.</p></div><span class="year-chip">{{new Date().getFullYear()}}</span></div>
      <ExpenseChart v-if="g.indicators" :data="g.indicators.monthly" />
    </article>
    <div class="dashboard-service-dates">
      <article v-motion :initial="cardInitial" :enter="cardEnter(125)" class="panel service-date-tile"><span><CalendarCheck :size="16" />Última manutenção</span><strong>{{g.indicators?date(g.indicators.lastMaintenance):'—'}}</strong><small>{{g.recent[0]?.description||'Seu primeiro registro começa aqui'}}</small></article>
      <article v-motion :initial="cardInitial" :enter="cardEnter(125)" class="panel service-date-tile"><span><CalendarClock :size="16" />Próxima data</span><strong>{{nextDate?date(nextDate):'A programar'}}</strong><RouterLink to="/agenda" class="text-button">Ver agenda<ChevronRight :size="14" /></RouterLink></article>
    </div>
    <article v-motion :initial="cardInitial" :enter="cardEnter(150)" class="panel history-panel dashboard-history">
      <div class="section-header"><h2>Últimos serviços</h2><RouterLink to="/historico" class="icon-button" aria-label="Ver histórico"><ArrowUpRight :size="18" /></RouterLink></div>
      <ServiceList v-if="g.recent.length" :records="g.recent.slice(0,3)" @select="openRecord" />
      <EmptyState v-else title="Uma história para começar" description="Ainda não há serviços registrados neste veículo." />
    </article>
  </section>
  <EmptyState v-else-if="!g.loading" title="Sua próxima história começa aqui" description="Adicione um veículo para acompanhar serviços, gastos e cuidados."><RouterLink to="/veiculos" class="button primary">Adicionar veículo</RouterLink></EmptyState>
  <MileageDialog v-model:visible="mileageOpen" />
  <RecordDialog v-model:visible="recordOpen" :record="edit" @saved="g.refresh" />
</template>
