<script setup lang="ts">
import { reactive, ref, watch } from 'vue'
import Dialog from 'primevue/dialog'
import InputText from 'primevue/inputtext'
import Textarea from 'primevue/textarea'
import Button from 'primevue/button'
import { useToast } from 'primevue/usetoast'
import { useMechanics } from '../stores/mechanics'
import { api } from '../services/api'
import type { Mechanic } from '../services/types'
const visible=defineModel<boolean>('visible',{default:false})
const props=defineProps<{mechanic?:Mechanic|null}>(),emit=defineEmits<{saved:[mechanic:Mechanic]}>()
const busy=ref(false),error=ref(''),toast=useToast(),directory=useMechanics()
const empty=()=>({name:'',workshop:'',specialty:'',phone:'',email:'',address:'',notes:'',isArchived:false})
const form=reactive(empty())
watch(visible,value=>{if(value){error.value='';Object.assign(form,props.mechanic?{...props.mechanic}:empty())}})
async function save(){
 busy.value=true;error.value=''
 try{
  const saved=await api<Mechanic>('/mechanics'+(props.mechanic?'/'+props.mechanic.id:''),{method:props.mechanic?'PUT':'POST',body:JSON.stringify(form)})
  directory.changed();emit('saved',saved);visible.value=false;toast.add({severity:'success',summary:props.mechanic?'Contato atualizado':'Mecânico cadastrado',life:3000})
 }catch(e){error.value=(e as Error).message}finally{busy.value=false}
}
</script>
<template>
 <Dialog v-model:visible="visible" modal :header="mechanic?'Editar mecânico':'Cadastrar mecânico'" :style="{width:'650px'}" :breakpoints="{'700px':'95vw'}" :closable="!busy" :close-on-escape="!busy">
  <form class="form-grid" @submit.prevent="save">
   <p class="full form-hint">Um contato da sua agenda, disponível para os seus veículos.</p>
   <label class="full" for="mechanic-name">Nome do mecânico<InputText id="mechanic-name" v-model="form.name" maxlength="120" required autocomplete="off" placeholder="Como você conhece esse profissional" /></label>
   <label for="mechanic-workshop">Oficina<InputText id="mechanic-workshop" v-model="form.workshop" maxlength="200" placeholder="Opcional" /></label>
   <label for="mechanic-specialty">Especialidade<InputText id="mechanic-specialty" v-model="form.specialty" maxlength="120" placeholder="Ex.: suspensão e alinhamento" /></label>
   <label for="mechanic-phone">Telefone / WhatsApp<InputText id="mechanic-phone" v-model="form.phone" type="tel" maxlength="40" placeholder="DDD e número" autocomplete="off" /></label>
   <label for="mechanic-email">E-mail<InputText id="mechanic-email" v-model="form.email" type="email" maxlength="254" placeholder="Opcional" autocomplete="off" /></label>
   <label class="full" for="mechanic-address">Endereço<InputText id="mechanic-address" v-model="form.address" maxlength="300" placeholder="Rua, número, bairro e cidade" /></label>
   <label class="full" for="mechanic-notes">Observações<Textarea id="mechanic-notes" v-model="form.notes" rows="3" maxlength="4000" auto-resize placeholder="Referência, horário de atendimento ou outros detalhes úteis" /></label>
   <p v-if="error" class="full form-error" role="alert">{{error}}</p>
   <div class="full form-actions"><Button type="button" class="button" :disabled="busy" @click="visible=false">Cancelar</Button><Button type="submit" class="button primary" :disabled="busy">{{busy?'Salvando…':'Salvar mecânico'}}</Button></div>
  </form>
 </Dialog>
</template>
