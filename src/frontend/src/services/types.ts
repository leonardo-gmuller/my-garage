export interface Page<T>{items:T[];total:number;pageNumber:number;pageSize:number}
export type PaintFinish = 'Solid'|'Metallic'|'Pearlescent'|'Matte'
export const finishLabels:Record<PaintFinish,string>={Solid:'Sólida',Metallic:'Metálica',Pearlescent:'Perolizada',Matte:'Fosca'}
export interface Vehicle {id:string;manufacturer:string;model:string;manufactureYear:number;modelYear:number;version:string|null;color:string|null;colorHex:string|null;paintFinish:PaintFinish|null;mileage:number|null;nickname:string|null;plate:string|null;photoUrl:string|null}
export interface Subcomponent {id:string;componentId:string;key:string;name:string}
export interface Component {id:string;key:string;name:string;subcomponents:Subcomponent[]}
export interface Item {id:string;name:string;quantity:number;unitCost:number|null;total:number|null}
export interface Attachment {id:string;fileName:string;contentType:string;size:number;url:string}
export interface Mechanic {id:string;name:string;workshop:string|null;specialty:string|null;phone:string|null;email:string|null;address:string|null;notes:string|null;isArchived:boolean;serviceCount:number;lastServiceDate:string|null}
export type PaymentScope='Parts'|'Labor'|'Package'
export type PaymentMethod='CreditCard'|'DebitCard'|'Pix'|'Cash'|'Other'
export const paymentScopes:Record<PaymentScope,string>={Parts:'Peças',Labor:'Mão de obra',Package:'Valor total fechado'}
export const paymentMethods:Record<PaymentMethod,string>={CreditCard:'Cartão de crédito',DebitCard:'Cartão de débito',Pix:'Pix',Cash:'Dinheiro',Other:'Outra forma'}
export interface PaymentDraft{scope:PaymentScope;method:PaymentMethod;installments:number|null;totalCharged:number|null}
export interface Payment extends PaymentDraft{id:string;principal:number;interest:number|null}
export interface RecordEntry {id:string;vehicleId:string;date:string;category:string;serviceType:string;description:string;mileage:number|null;laborCost:number|null;partsCost:number|null;totalCost:number;packageTotal:number|null;payments:Payment[];baseCost:number;interestCost:number;pendingPaymentCount:number;workshop:string|null;notes:string|null;components:Pick<Component,'id'|'key'|'name'>[];items:Item[];attachments:Attachment[];subcomponents:Subcomponent[];primaryComponentId:string|null;mechanicId:string|null;mechanicName:string|null}
export interface Schedule {id:string;componentId:string;componentName:string;subcomponentId:string|null;subcomponentName:string|null;description:string;dueDate:string|null;dueMileage:number|null;priority:string;completed:boolean;state:string}
export interface Indicators {total:number;parts:number;labor:number;unallocated:number;interest:number;pendingPaymentCount:number;serviceCount:number;lastMaintenance:string|null;monthly:{month:string;parts:number;labor:number;total:number;unallocated:number;interest:number}[];annual:{year:number;total:number}[];categories:{category:string;total:number}[];mostExpensive:RecordEntry[]}
export interface Mileage {id:string;mileage:number;recordedAt:string}
export const categories:Record<string,string>={Engine:'Motor / ignição',Electrical:'Elétrica',Tires:'Pneus e rodas',Brakes:'Freios',Suspension:'Suspensão',Cooling:'Arrefecimento',Transmission:'Transmissão',Exhaust:'Escapamento',Body:'Carroceria',General:'Geral'}
export const serviceTypes:Record<string,string>={Preventive:'Preventivo',Corrective:'Corretivo',Inspection:'Inspeção',Replacement:'Substituição'}
export const states:Record<string,string>={Current:'Em dia',DueSoon:'Próxima do vencimento',Overdue:'Vencida',Completed:'Concluída'}
export const priorities:Record<string,string>={Low:'Baixa',Normal:'Normal',High:'Alta'}
export const money=(v:number)=>new Intl.NumberFormat('pt-BR',{style:'currency',currency:'BRL'}).format(v)
export const number=(v:number)=>new Intl.NumberFormat('pt-BR').format(v)
export const date=(v:string|null)=>v ? new Intl.DateTimeFormat('pt-BR').format(new Date(v.length===10?v+'T12:00:00':v)) : 'Ainda não registrada'
export const today=()=>new Intl.DateTimeFormat('en-CA',{timeZone:'America/Sao_Paulo',year:'numeric',month:'2-digit',day:'2-digit'}).format(new Date())

export interface ComponentSummary {pendingPaymentCount:number;serviceCount:number;totalCost:number;lastService:RecordEntry|null;recent:RecordEntry[];nextMaintenance:Schedule|null}

export interface MaintenanceAiStatus {available:boolean;maxMessageLength:number;reason:string|null}
export interface MaintenanceAiDraft {
 vehicleId:string;description:string;category:string|null;serviceType:string|null;mileage:number|null;totalCost:number|null;serviceDate:string|null;workshop:string|null;notes:string|null;
 components:{id:string;key:string;name:string;subcomponents:Subcomponent[]}[];
 items:{name:string;quantity:number|null}[];missingFields:string[];warnings:string[];promptVersion:string;
}
export const aiFieldLabels:Record<string,string>={serviceDate:'Data do serviço',mileage:'Quilometragem (opcional)',totalCost:'Valor total',category:'Categoria',serviceType:'Tipo de serviço',workshop:'Oficina (opcional)',components:'Componentes envolvidos',itemQuantities:'Quantidade das peças'}

export interface AssistantRecord { id:string; description:string; date:string; mileage:number|null; totalCost:number; closedTotal:boolean; pendingPaymentCount:number; url:string }
export interface HistoryAnswer { vehicleId:string; kind:'latest'|'spending'|'pending'|'clarification'; message:string; scope:string; count:number; total:number|null; pendingPaymentCount:number; records:AssistantRecord[]; schedules:{schedule:Schedule;url:string}[]; warnings:string[]; promptVersion:string }
