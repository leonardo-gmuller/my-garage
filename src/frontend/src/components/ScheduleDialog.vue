<script setup lang="ts">
import InputText from 'primevue/inputtext'
import InputNumber from 'primevue/inputnumber'
import Select from 'primevue/select'
import Checkbox from 'primevue/checkbox'
import Button from 'primevue/button'

import { reactive,ref,watch,computed } from 'vue'
import Dialog from 'primevue/dialog'
import FormDatePicker from './FormDatePicker.vue'
import { useGarage } from '../stores/garage'
import { api } from '../services/api'
import type { Schedule } from '../services/types'
import { priorities } from '../services/types'
const visible=defineModel<boolean>('visible',{default:false}),props=defineProps<{schedule?:Schedule|null;componentId?:string;subcomponentId?:string|null}>(),g=useGarage(),busy=ref(false),error=ref('')
const emit=defineEmits<{saved:[]}>()
const subOptions=computed(()=>g.components.find(c=>c.id===form.componentId)?.subcomponents??[])
const form=reactive({componentId:'',subcomponentId:null as string|null,description:'',dueDate:'',dueMileage:null as number|null,priority:'Normal',completed:false})
watch(visible,v=>{if(!v)return;error.value='';Object.assign(form,props.schedule?{...props.schedule,dueDate:props.schedule.dueDate??''}:{componentId:props.componentId??g.components[0]?.id??'',subcomponentId:props.subcomponentId??null,description:'',dueDate:'',dueMileage:null,priority:'Normal',completed:false})})
watch(()=>form.componentId,()=>{if(!subOptions.value.some(s=>s.id===form.subcomponentId))form.subcomponentId=null})
async function save(){busy.value=true;try{await api(`/vehicles/${g.selectedId}/schedules`+(props.schedule?'/'+props.schedule.id:''),{method:props.schedule?'PUT':'POST',body:JSON.stringify({...form,dueDate:form.dueDate||null,dueMileage:form.dueMileage==null||String(form.dueMileage)===''?null:Number(form.dueMileage)})});await g.refresh();visible.value=false;emit('saved')}catch(e){error.value=(e as Error).message}finally{busy.value=false}}
</script>
<template><Dialog v-model:visible="visible" modal :header="schedule?'Editar manutenção programada':'Programar manutenção'" :style="{width:'580px'}" :breakpoints="{'650px':'95vw'}"><form class="form-grid" @submit.prevent="save"><label class="full" for="schedule-description">Descrição<InputText id="schedule-description" v-model="form.description" required maxlength="300" /></label><label for="schedule-component">Componente<Select aria-label="Componente" input-id="schedule-component" v-model="form.componentId" :options="g.components" option-label="name" option-value="id" required /></label><label for="schedule-subcomponent">Subcomponente (opcional)<Select input-id="schedule-subcomponent" aria-label="Subcomponente" v-model="form.subcomponentId" :options="subOptions" option-label="name" option-value="id" placeholder="Todo o componente" show-clear /></label><label for="schedule-priority">Prioridade<Select aria-label="Prioridade" input-id="schedule-priority" v-model="form.priority" :options="Object.entries(priorities).map(([value,label])=>({value,label}))" option-label="label" option-value="value" /></label><label for="schedule-date">Próxima data<FormDatePicker input-id="schedule-date" v-model="form.dueDate" /></label><label for="schedule-mileage">Próxima quilometragem<InputNumber input-id="schedule-mileage" v-model="form.dueMileage" :min="0" :max="10000000" :use-grouping="false" /></label><p class="full muted">Informe uma data, uma quilometragem ou ambas. O primeiro limite atingido determina o alerta.</p><label class="full checkbox" for="schedule-completed"><Checkbox input-id="schedule-completed" v-model="form.completed" binary />Concluída</label><p class="full muted">Concluir o lembrete não cria um serviço nem registra um gasto.</p><p v-if="error" class="full form-error" role="alert">{{error}}</p><div class="full form-actions"><Button type="button" class="button" @click="visible=false">Cancelar</Button><Button type="submit" class="button primary" :disabled="busy">{{busy?'Salvando…':'Salvar agendamento'}}</Button></div></form></Dialog></template>
