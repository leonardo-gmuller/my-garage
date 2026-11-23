<script setup lang="ts">
import { computed,reactive } from 'vue'
import Select from 'primevue/select'
import InputNumber from 'primevue/inputnumber'
import Button from 'primevue/button'
import { Plus,Trash2 } from 'lucide-vue-next'
import { money,paymentMethods,paymentScopes } from '../services/types'
import type { PaymentDraft,PaymentScope } from '../services/types'
const payments=defineModel<PaymentDraft[]>({required:true})
const props=defineProps<{parts:number;labor:number;packageTotal:number|null;packageMode:boolean}>()
const modes=reactive<Partial<Record<PaymentScope,'total'|'interest'>>>({})
const options=[{value:'total',label:'Total com juros'},{value:'interest',label:'Somente os juros em R$'}]
const scopes=computed<PaymentScope[]>(()=>props.packageMode?['Package']:['Parts','Labor'])
const principal=(scope:PaymentScope)=>scope==='Parts'?props.parts:scope==='Labor'?props.labor:props.packageTotal??0
const mode=(scope:PaymentScope)=>modes[scope]??'total'
const value=(p:PaymentDraft)=>p.totalCharged==null?null:mode(p.scope)==='total'?p.totalCharged:Math.round((p.totalCharged-principal(p.scope))*100)/100
function update(p:PaymentDraft,amount:number|null){p.totalCharged=amount==null?null:Math.round((amount+(mode(p.scope)==='interest'?principal(p.scope):0))*100)/100}
function add(scope:PaymentScope){payments.value=[...payments.value,{scope,method:'CreditCard',installments:null,totalCharged:null}];modes[scope]='total'}
const target=(scope:PaymentScope)=>scope==='Parts'?'das peças':scope==='Labor'?'da mão de obra':'do valor fechado'
</script>
<template>
 <section class="full payment-editor form-section" aria-label="Pagamentos e juros">
  <h3>Pagamentos e juros</h3>
  <p class="form-hint">Peças e mão de obra podem ter cobranças, parcelas e juros diferentes. Informe apenas o que souber; o restante fica pendente.</p>
  <div class="payment-add-actions"><template v-for="scope in scopes" :key="scope"><Button v-if="!payments.some(p=>p.scope===scope)" type="button" class="button" @click="add(scope)"><Plus :size="15"/>Pagamento {{target(scope)}}</Button></template></div>
  <fieldset v-for="p in payments" :key="p.scope" class="payment-fields">
   <legend>{{paymentScopes[p.scope]}}</legend>
   <div class="section-header"><p class="form-hint">Valor original: <strong>{{money(principal(p.scope))}}</strong></p><Button type="button" class="icon-button" :aria-label="'Remover pagamento '+target(p.scope)" @click="payments=payments.filter(row=>row!==p)"><Trash2 :size="17"/></Button></div>
   <p v-if="!scopes.includes(p.scope)" class="form-error" role="alert">Este pagamento pertence a outra forma de informar o valor. Volte à modalidade anterior ou remova este pagamento para continuar.</p>
   <div class="payment-fields__grid">
    <label :for="'payment-method-'+p.scope">Forma de pagamento<Select :input-id="'payment-method-'+p.scope" :aria-label="'Forma de pagamento '+target(p.scope)" v-model="p.method" :options="Object.entries(paymentMethods).map(([value,label])=>({value,label}))" option-label="label" option-value="value" /></label>
    <label :for="'payment-installments-'+p.scope">Quantidade de parcelas (opcional)<InputNumber :input-id="'payment-installments-'+p.scope" :aria-label="'Quantidade de parcelas '+target(p.scope)" v-model="p.installments" :min="1" :max="120" :use-grouping="false" placeholder="Não informada" /></label>
    <label :for="'payment-mode-'+p.scope">Valor disponível<Select :input-id="'payment-mode-'+p.scope" :aria-label="'Informar pagamento '+target(p.scope)" :model-value="mode(p.scope)" @update:model-value="modes[p.scope]=$event" :options="options" option-label="label" option-value="value" /></label>
    <label :for="'payment-charge-'+p.scope">{{mode(p.scope)==='total'?'Total com juros (R$)':'Juros e acréscimos (R$)'}}<InputNumber :input-id="'payment-charge-'+p.scope" :aria-label="(mode(p.scope)==='total'?'Total com juros ':'Juros e acréscimos ')+target(p.scope)" :model-value="value(p)" @update:model-value="update(p,$event)" :disabled="!scopes.includes(p.scope)" :min="0" :max="100000000" :max-fraction-digits="2" :use-grouping="false" locale="pt-BR" placeholder="Não informado" /></label>
   </div>
   <p v-if="p.totalCharged!=null&&p.totalCharged<principal(p.scope)" class="form-error" role="alert">O total com juros deve ser igual ou maior que o valor original. Confira os valores.</p>
   <p v-else-if="p.totalCharged==null" class="form-hint payment-pending" role="status">Juros ainda não informados. O total conhecido considera o valor original desta cobrança.</p>
   <p v-else class="payment-calculation">{{money(principal(p.scope))}} de valor original + {{money(Math.round((p.totalCharged-principal(p.scope))*100)/100)}} de juros = <strong>{{money(p.totalCharged)}}</strong></p>
  </fieldset>
  <small v-if="payments.length" class="form-hint">As parcelas ficam registradas como referência. O custo total é contado uma vez, na data do serviço.</small>
 </section>
</template>
