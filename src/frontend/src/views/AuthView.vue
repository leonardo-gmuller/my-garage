<script setup lang="ts">
import { useCardMotion } from '../composables/useCardMotion'
import InputText from 'primevue/inputtext'
import Password from 'primevue/password'
import Button from 'primevue/button'

import { ref } from 'vue'
import { Box,History,ShieldCheck } from 'lucide-vue-next'
import BrandLogo from '../components/BrandLogo.vue'
import { useGarage } from '../stores/garage'
const g=useGarage(),email=ref(''),password=ref(''),busy=ref(false),error=ref('')
async function submit(){busy.value=true;error.value='';try{await g.login(email.value,password.value)}catch(e){error.value=(e as Error).message}finally{busy.value=false}}
const { cardInitial, cardEnter } = useCardMotion()
</script>
<template><main class="auth-layout"><section class="auth-story"><div class="brand"><BrandLogo variant="stacked" tagline /></div><div><p class="page-kicker">Para quem gosta de cuidar.</p><h1>Cada quilômetro<br>tem uma história.</h1><p>Conheça seu carro por inteiro. Organize serviços, acompanhe os gastos e saiba o que vem pela frente.</p><div class="auth-features"><span><Box/>Explore em 3D</span><span><History/>Construa seu histórico</span><span><ShieldCheck/>Seus dados, na sua conta</span></div></div><small>Feito para cuidar do seu próximo caminho.</small></section><section class="auth-form"><form v-motion :initial="cardInitial" :enter="cardEnter()" @submit.prevent="submit"><BrandLogo class="auth-brand" variant="symbol" decorative /><h2>Sua garagem está aqui.</h2><p>Entre com a conta configurada para esta garagem.</p><div class="form-grid single"><label for="auth-email">E-mail<InputText id="auth-email" v-model="email" type="email" autocomplete="username" required maxlength="254" /></label><div class="form-field"><label for="auth-password">Senha</label><Password input-id="auth-password" v-model="password" autocomplete="current-password" :feedback="false" toggle-mask :input-props="{'aria-label':'Senha',autocomplete:'current-password',required:true,minlength:1,maxlength:128}" /></div><p v-if="error||g.error" class="form-error" role="alert">{{error||g.error}}</p><Button type="submit" class="button primary" :disabled="busy">{{busy?'Abrindo…':'Entrar na minha garagem'}}</Button></div></form></section></main></template>
