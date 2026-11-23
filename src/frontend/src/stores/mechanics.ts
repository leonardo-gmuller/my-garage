import { defineStore } from 'pinia'
import { ref } from 'vue'

/** Invalidate open views without caching private contact data across sessions. */
export const useMechanics=defineStore('mechanics',()=>{
 const revision=ref(0)
 function changed(){revision.value++}
 return{revision,changed}
})
