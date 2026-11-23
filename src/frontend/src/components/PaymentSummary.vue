<script setup lang="ts">
import { money,paymentMethods,paymentScopes } from '../services/types'
import type { RecordEntry } from '../services/types'
defineProps<{record:RecordEntry}>()
</script>
<template>
 <div v-if="record.payments?.length" class="payment-summary">
  <h4>Pagamentos</h4>
  <div v-for="p in record.payments" :key="p.id" class="payment-summary__row">
   <p><strong>{{paymentScopes[p.scope]}}</strong> · {{paymentMethods[p.method]}} · {{p.installments==null?'Parcelas não informadas':p.installments===1?'1 parcela':p.installments+' parcelas'}}</p>
   <small>Original {{money(p.principal)}} · {{p.interest==null?'Juros não informados':'Juros '+money(p.interest)}}</small>
   <strong v-if="p.totalCharged!=null">Total da cobrança: {{money(p.totalCharged)}}</strong>
  </div>
  <p v-if="record.pendingPaymentCount" class="form-hint payment-pending">Total conhecido: {{money(record.totalCost)}}. Ainda há juros não informados.</p>
 </div>
</template>
