import { createRouter,createWebHistory } from 'vue-router'
export const router=createRouter({history:createWebHistory(),scrollBehavior:(to,from,saved)=>to.path===from.path?false:saved??{left:0,top:0},routes:[
 {path:'/',component:()=>import('../views/DashboardView.vue'),meta:{title:'Visão geral'}},
 {path:'/garagem-3d',component:()=>import('../views/GarageView.vue'),meta:{title:'Garagem 3D'}},
 {path:'/mecanicos',component:()=>import('../views/MechanicsView.vue'),meta:{title:'Mecânicos',vehicleScoped:false,kicker:'Sua agenda de confiança.'}},
 {path:'/veiculos',component:()=>import('../views/VehiclesView.vue'),meta:{title:'Meus veículos'}},
 {path:'/historico',component:()=>import('../views/HistoryView.vue'),meta:{title:'Histórico de serviços'}},
 {path:'/assistente',component:()=>import('../views/AssistantView.vue'),meta:{title:'Assistente do histórico',kicker:'Respostas que vêm dos seus registros.'}},
 {path:'/financeiro',component:()=>import('../views/FinanceView.vue'),meta:{title:'Controle financeiro'}},
 {path:'/agenda',component:()=>import('../views/SchedulesView.vue'),meta:{title:'Próximas manutenções'}},
 {path:'/:pathMatch(.*)*',redirect:'/'}
]})
