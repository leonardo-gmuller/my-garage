import { defineStore } from 'pinia'
import { computed,ref } from 'vue'
import { api,allPages,clearCsrf,ApiError } from '../services/api'
import type { Vehicle,Component,Indicators,RecordEntry,Schedule,Page } from '../services/types'
export const useGarage=defineStore('garage',()=>{
 const user=ref<string|null>(null),ready=ref(false),vehicles=ref<Vehicle[]>([]),selectedId=ref(''),components=ref<Component[]>([]),indicators=ref<Indicators|null>(null),recent=ref<RecordEntry[]>([]),schedules=ref<Schedule[]>([]),loading=ref(false),error=ref('')
 const dataRevision=ref(0)
 const selected=computed(()=>vehicles.value.find(v=>v.id===selectedId.value)??null)
 let requestVersion=0
 async function init(){try{user.value=(await api<{email:string}>('/auth/me')).email;await loadVehicles()}catch(e){if(!(e instanceof ApiError&&e.status===401))error.value=(e as Error).message}finally{ready.value=true}}
 async function login(email:string,password:string){await api('/auth/login',{method:'POST',body:JSON.stringify({email,password})});clearCsrf();user.value=email;await loadVehicles()}
 async function logout(){await api('/auth/logout',{method:'POST'});clearCsrf();user.value=null;vehicles.value=[];selectedId.value='';requestVersion++;components.value=[];indicators.value=null;recent.value=[];schedules.value=[]}
 async function loadVehicles(){vehicles.value=await allPages<Vehicle>('/vehicles');if(!vehicles.value.some(v=>v.id===selectedId.value))selectedId.value=vehicles.value[0]?.id??'';await refresh()}
 async function select(id:string){selectedId.value=id;await refresh()}
 async function refresh(){const id=selectedId.value,version=++requestVersion;error.value='';indicators.value=null;recent.value=[];components.value=[];schedules.value=[];if(!id)return;loading.value=true
  try{const [c,i,r,s]=await Promise.all([api<Component[]>(`/vehicles/${id}/components`),api<Indicators>(`/vehicles/${id}/indicators`),api<Page<RecordEntry>>(`/vehicles/${id}/records?pageSize=5`),allPages<Schedule>(`/vehicles/${id}/schedules`)]);if(version!==requestVersion)return;components.value=c;indicators.value=i;recent.value=r.items;schedules.value=s;dataRevision.value++}
  catch(e){if(version===requestVersion)error.value=(e as Error).message}finally{if(version===requestVersion)loading.value=false}}
 return{dataRevision,user,ready,vehicles,selectedId,selected,components,indicators,recent,schedules,loading,error,init,login,logout,loadVehicles,select,refresh}
})
