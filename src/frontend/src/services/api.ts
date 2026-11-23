export class ApiError extends Error { constructor(public status:number,message:string){super(message)} }
let csrf:string|null=null
export function clearCsrf(){csrf=null}
export async function api<T>(path:string, options:RequestInit={}):Promise<T>{
 const method=options.method??'GET';const headers=new Headers(options.headers)
 if(method!=='GET'){
  if(!csrf){const response=await fetch('/api/auth/csrf',{credentials:'same-origin'});if(!response.ok)throw new ApiError(response.status,'Não foi possível iniciar a sessão.');csrf=(await response.json()).token}
  headers.set('X-CSRF-TOKEN',csrf!)
  if(options.body && !(options.body instanceof FormData))headers.set('Content-Type','application/json')
 }
 let response:Response
 try{response=await fetch('/api'+path,{...options,headers,credentials:'same-origin'})}catch{throw new Error('Não foi possível conectar à garagem. Verifique se a API está disponível.')}
 if(!response.ok){const data=await response.json().catch(()=>({}));throw new ApiError(response.status,Object.values(data.errors??{}).flat().join(' ')||data.title||(response.status===429?'Muitas tentativas. Aguarde um minuto.':'Não foi possível concluir a operação.'))}
 if(response.status===204)return undefined as T
 return response.json()
}
export async function allPages<T>(path:string):Promise<T[]>{const values:T[]=[];let page=1;while(true){const result=await api<import('./types').Page<T>>(path+(path.includes('?')?'&':'?')+'pageSize=100&page='+page);values.push(...result.items);if(values.length>=result.total||result.items.length===0)return values;page++}}
