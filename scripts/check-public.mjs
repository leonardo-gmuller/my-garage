#!/usr/bin/env node
// Check only versionable files; local data and optional licensed assets stay private.
import {execFileSync} from 'node:child_process'
import {existsSync,readFileSync} from 'node:fs'
import {dirname,resolve} from 'node:path'
import {fileURLToPath} from 'node:url'
import {parseEnv} from 'node:util'
process.chdir(resolve(dirname(fileURLToPath(import.meta.url)),'..'))
const files=[...new Set(execFileSync('git',['ls-files','-z','--cached','--others','--exclude-standard'],{encoding:'utf8'}).split('\0').filter(Boolean))]
const failures=[]
const privatePaths=/(^|\/)(?:\.env(?:\.(?!example$)[^/]*)?|\.local|\.tools|node_modules|bin|obj|dist|test-results|playwright-report)(?:\/|$)|(?:Saveiro_3D\.glb|Saveiro_3D_grupos\.json)$/
const local=existsSync('.env')?parseEnv(readFileSync('.env','utf8')):{}
const secrets=[local.POSTGRES_PASSWORD,local.BOOTSTRAP_PASSWORD,local.GEMINI_API_KEY,process.env.GEMINI_API_KEY].filter(value=>value&&value.length>=8)
for(const file of files){
 if(privatePaths.test(file)){failures.push('Arquivo privado ou gerado: '+file);continue}
 if(!existsSync(file))continue
 const bytes=readFileSync(file)
 if(secrets.some(secret=>bytes.includes(Buffer.from(secret))))failures.push('Credencial local encontrada: '+file)
 if(!bytes.includes(0)&&/\/Users\/[^/\s]+\//.test(bytes.toString()))failures.push('Caminho pessoal encontrado: '+file)
}
const sample=parseEnv(readFileSync('.env.example','utf8'))
for(const key of ['POSTGRES_PASSWORD','BOOTSTRAP_PASSWORD','GEMINI_API_KEY'])if(sample[key])failures.push('A senha de exemplo deve ficar vazia: '+key)
if(failures.length){console.error(failures.join('\n'));process.exit(1)}
console.log(`Publicação: ${files.length} arquivos verificados; sem credenciais locais ou assets privados.`)
