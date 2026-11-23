<script setup lang="ts">
import { Wrench,ChevronRight } from 'lucide-vue-next'
import { date,money,serviceTypes } from '../services/types'
import type { RecordEntry } from '../services/types'
defineProps<{records:RecordEntry[]}>();defineEmits<{select:[record:RecordEntry]}>()
</script>
<template><div class="service-list"><button v-for="r in records" :key="r.id" class="service-row" @click="$emit('select',r)"><span class="service-icon"><Wrench :size="18"/></span><span class="service-description"><strong>{{r.description}}</strong><small>{{date(r.date)}} <span class="separator">/</span> {{serviceTypes[r.serviceType]}}</small><small v-if="r.pendingPaymentCount">Total conhecido · juros pendentes</small><small v-if="r.packageTotal!=null">Valor total fechado</small><small v-if="r.mechanicName">{{r.mechanicName}}</small><small v-if="r.subcomponents.length">{{r.subcomponents.map(s=>s.name).join(' · ')}}</small></span><strong class="service-cost">{{money(r.totalCost)}}</strong><ChevronRight :size="16"/></button></div></template>
