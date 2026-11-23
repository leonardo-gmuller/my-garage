<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import Toast from 'primevue/toast'
import Select from 'primevue/select'
import Drawer from 'primevue/drawer'
import Button from 'primevue/button'
import { useToast } from 'primevue/usetoast'
import { CarFront, LayoutDashboard, Box, History, Wallet, CalendarClock, LogOut, Plus, Menu, UsersRound } from 'lucide-vue-next'
import { useGarage } from './stores/garage'
import AuthView from './views/AuthView.vue'
import RecordDialog from './components/RecordDialog.vue'
import BrandLogo from './components/BrandLogo.vue'
const g = useGarage(), route = useRoute(), router = useRouter(), toast = useToast()
const menu = ref(false), recordOpen = ref(false)
const links = [
  { to: '/', label: 'Visão geral', icon: LayoutDashboard },
  { to: '/garagem-3d', label: 'Garagem 3D', icon: Box },
  { to: '/mecanicos', label: 'Mecânicos', icon: UsersRound },
  { to: '/historico', label: 'Histórico de serviços', icon: History },
  { to: '/financeiro', label: 'Financeiro', icon: Wallet },
  { to: '/agenda', label: 'Próximas manutenções', icon: CalendarClock },
]
const vehicleOptions = computed(() => g.vehicles.map(v => ({ id: v.id, label: `${v.nickname || v.model} · ${v.modelYear}` })))
onMounted(() => g.init())
async function logout() {
  try { await g.logout(); await router.push('/') }
  catch (e) { toast.add({ severity: 'error', summary: (e as Error).message, life: 5000 }) }
}
</script>
<template>
  <div class="dark">
    <Toast position="bottom-right" />
    <div v-if="!g.ready" class="boot" role="status"><BrandLogo variant="symbol" decorative /><p>Abrindo sua garagem…</p></div>
    <AuthView v-else-if="!g.user" />
    <div v-else class="app-shell">
      <div class="application-frame">
        <header class="app-header">
          <RouterLink to="/" class="brand" aria-label="Minha Garagem — página inicial" @click="menu=false">
            <BrandLogo tagline decorative />
          </RouterLink>
          <nav class="primary-nav" aria-label="Navegação principal">
            <RouterLink v-for="link in links" :key="link.to" :to="link.to" :aria-label="link.label"
              :class="{active:route.path===link.to}" :aria-current="route.path===link.to?'page':undefined">
              <component :is="link.icon" :size="20" /><span class="nav-tooltip" aria-hidden="true">{{link.label}}</span>
            </RouterLink>
          </nav>
          <div class="header-account">
            <RouterLink to="/veiculos" class="header-link" aria-label="Meus veículos" v-tooltip.bottom="'Meus veículos'"><CarFront :size="20" /></RouterLink>
            <span class="account-avatar" :title="g.user" :aria-label="'Conta: '+g.user">{{g.user[0]?.toUpperCase()}}</span>
            <Button class="icon-button" aria-label="Sair da conta" @click="logout"><LogOut :size="18" /></Button>
            <Button class="icon-button nav-menu" aria-label="Abrir navegação" :aria-expanded="menu" aria-controls="mobile-navigation" @click="menu=true"><Menu /></Button>
          </div>
        </header>
        <div class="workspace">
          <main id="main">
            <div class="page-heading">
              <div><p class="page-kicker">{{route.meta.kicker??'Seu veículo, por inteiro.'}}</p><h1>{{route.meta.title}}</h1></div>
              <div class="heading-actions">
                <Select :model-value="g.selectedId" :options="vehicleOptions" option-label="label" option-value="id"
                  aria-label="Veículo selecionado" class="vehicle-select" placeholder="Nenhum veículo" @change="g.select($event.value)" />
                <Button v-if="g.selected" class="button primary" @click="recordOpen=true"><Plus :size="17" />Registrar serviço</Button>
              </div>
            </div>
            <div v-if="g.error" class="error-banner" role="alert">{{g.error}}<Button class="button" @click="g.loadVehicles">Tentar novamente</Button></div>
            <div v-if="g.loading" class="loading-line" role="status" aria-label="Carregando dados"></div>
            <RouterView v-slot="{ Component, route: viewRoute }">
              <Transition name="page" mode="out-in">
                <div :key="viewRoute.path+(viewRoute.meta.vehicleScoped===false?'':'-'+g.selectedId)" class="route-surface"><component :is="Component" /></div>
              </Transition>
            </RouterView>
            <footer class="page-footer"><BrandLogo variant="wordmark" /><span>Mais cuidado. Mais estrada.</span></footer>
          </main>
        </div>
      </div>
      <Drawer v-model:visible="menu" header="Minha garagem" position="right" class="mobile-navigation">
        <nav aria-label="Navegação principal">
          <RouterLink v-for="link in links" :key="link.to" :to="link.to" :class="{active:route.path===link.to}" @click="menu=false"><component :is="link.icon" :size="20" />{{link.label}}</RouterLink>
        </nav>
        <RouterLink to="/veiculos" @click="menu=false"><CarFront :size="20" />Meus veículos</RouterLink>
        <small class="muted">{{g.user}}</small>
      </Drawer>
      <RecordDialog v-model:visible="recordOpen" @saved="g.refresh" />
    </div>
  </div>
</template>
