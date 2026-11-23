#!/usr/bin/env node
/** Local development orchestration. No shell evaluation of .env or credentials. */
import { spawn, spawnSync } from 'node:child_process'
import { randomBytes, createHash } from 'node:crypto'
import { existsSync, readFileSync, writeFileSync, mkdirSync, chmodSync, rmSync, readdirSync } from 'node:fs'
import { createServer } from 'node:net'
import { dirname, resolve, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { parseEnv } from 'node:util'

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..')
process.chdir(root)
const local = join(root, '.local')
const defaults = { API_PORT: '5081', VITE_PORT: '5175', DB_PORT: '55432', COMPOSE_PROJECT_NAME: 'my-garage', POSTGRES_DB: 'minha_garagem', POSTGRES_USER: 'garage', CONFIGURATION: 'Release' }
const config = { ...defaults }
if (existsSync('.env')) Object.assign(config, parseEnv(readFileSync('.env', 'utf8')))
for (const [key, value] of Object.entries(process.env)) if (value !== '') config[key] = value
const apiProject = config.API_PROJECT || 'src/backend/MinhaGaragem.Api/MinhaGaragem.Api.csproj'
const infraProject = config.INFRA_PROJECT || 'src/backend/MinhaGaragem.Infrastructure/MinhaGaragem.Infrastructure.csproj'
const frontend = config.FRONTEND_DIR || 'src/frontend'
const apiDirectory = dirname(resolve(apiProject))
const devProject = config.DEV_COMPOSE_PROJECT_NAME || config.COMPOSE_PROJECT_NAME + '-dev'
const apiUrl = `http://127.0.0.1:${config.API_PORT}`
const frontUrl = `http://127.0.0.1:${config.VITE_PORT}`
const dotnetScript = join(root, 'scripts/dotnet-local.sh')
const owned = new Set()
let closing = false
const sleep = ms => new Promise(r => setTimeout(r, ms))
const say = text => console.log(`[garagem] ${text}`)

function validateConfig() {
  for (const key of ['API_PORT', 'VITE_PORT', 'DB_PORT']) {
    if (!/^\d+$/.test(config[key]) || Number(config[key]) < 1024 || Number(config[key]) > 65535) throw new Error(`${key} deve ser uma porta entre 1024 e 65535.`)
  }
  if (new Set([config.API_PORT, config.VITE_PORT, config.DB_PORT]).size !== 3) throw new Error('API_PORT, VITE_PORT e DB_PORT devem usar portas diferentes.')
  if (!/^[a-z0-9][a-z0-9_-]*$/.test(config.COMPOSE_PROJECT_NAME) || !/^[a-z0-9][a-z0-9_-]*$/.test(devProject)) throw new Error('Nome de projeto Compose inválido.')
  for (const path of [apiProject, infraProject, join(frontend, 'package.json')]) if (!existsSync(path)) throw new Error(`Arquivo não encontrado: ${path}`)
}
function ensureEnv() {
  mkdirSync(local, { recursive: true })
  if (!existsSync('.env')) {
    const text = readFileSync('.env.example', 'utf8').replace(/^POSTGRES_PASSWORD=.*$/m, 'POSTGRES_PASSWORD=' + randomBytes(30).toString('base64url'))
    try { writeFileSync('.env', text, { flag: 'wx', mode: 0o600 }); say('.env criado com senha local aleatória, sem exibi-la.') } catch (e) { if (e.code !== 'EEXIST') throw e }
  }
  const values = parseEnv(readFileSync('.env', 'utf8'))
  if (!values.POSTGRES_PASSWORD && !config.POSTGRES_PASSWORD) {
    const password = randomBytes(30).toString('base64url')
    let text = readFileSync('.env', 'utf8')
    text = /^POSTGRES_PASSWORD=/m.test(text) ? text.replace(/^POSTGRES_PASSWORD=.*$/m, 'POSTGRES_PASSWORD=' + password) : text + '\nPOSTGRES_PASSWORD=' + password + '\n'
    writeFileSync('.env', text, { mode: 0o600 }); values.POSTGRES_PASSWORD = password
    say('Senha local preenchida no .env existente.')
  }
  for (const [key, value] of Object.entries(values)) if (!process.env[key]) config[key] = value
  chmodSync('.env', 0o600)
  validateConfig()
}
// Npgsql quotes protect semicolons, quotes and spaces without invoking a shell.
const pgValue = value => '"' + String(value).replaceAll('"', '""') + '"'
function connection() { return `Host=127.0.0.1;Port=${config.DB_PORT};Database=${pgValue(config.POSTGRES_DB)};Username=${pgValue(config.POSTGRES_USER)};Password=${pgValue(config.POSTGRES_PASSWORD)}` }
function hostEnv() {
  return { ...process.env, ...Object.fromEntries(["AI_ENABLED", "AI_PROVIDER", "GEMINI_API_KEY", "GEMINI_MODEL", "AI_TIMEOUT_SECONDS", "AI_MAX_INPUT_CHARS", "AI_MAX_OUTPUT_TOKENS", "AI_REQUESTS_PER_MINUTE", "AI_REQUESTS_PER_USER_PER_DAY", "AI_REQUESTS_PER_DAY"].filter(key=>config[key]!==undefined).map(key=>[key,config[key]])), ASPNETCORE_ENVIRONMENT: 'Development', ASPNETCORE_URLS: apiUrl,
    ConnectionStrings__Garage: connection(), Database__MigrateOnStartup: 'false',
    Bootstrap__Email: config.BOOTSTRAP_EMAIL || '', Bootstrap__Password: config.BOOTSTRAP_PASSWORD || '',
    Storage__Path: join(local, 'dev-uploads'), DataProtection__Path: join(local, 'dev-keys'),
    GARAGE_API_URL: apiUrl, GARAGE_VITE_PORT: config.VITE_PORT, GARAGE_ROOT: root,
    DOTNET_WATCH_RESTART_ON_RUDE_EDIT: 'true', DOTNET_CLI_TELEMETRY_OPTOUT: '1' }
}
function composeArgs(dev = false) { return ['compose', '--project-name', dev ? devProject : config.COMPOSE_PROJECT_NAME, '--env-file', join(root, '.env'), '-f', 'compose.yaml', ...(dev ? ['-f', 'compose.dev.yaml'] : [])] }
function launch(command, args, { cwd = root, env = hostEnv(), capture = false, managed = true } = {}) {
  const child = spawn(command, args, { cwd, env, detached: managed && process.platform !== 'win32', stdio: capture ? ['ignore', 'pipe', 'pipe'] : 'inherit' })
  let stdout = '', stderr = ''
  if (capture) { child.stdout.on('data', d => { stdout += d }); child.stderr.on('data', d => { stderr += d }) }
  const done = new Promise((res, rej) => {
    child.on('error', rej)
    child.on('exit', (code, signal) => {
      owned.delete(child)
      if (code === 0 || closing) res({ stdout, stderr })
      else rej(new Error(`${command} terminou com ${signal || 'código ' + code}.${capture ? ' ' + stderr.trim() : ''}`))
    })
  })
  // Managed long-running children are observed before the caller begins readiness polling.
  done.catch(() => {})
  if (managed) owned.add(child)
  return { child, done }
}
async function run(command, args, options) { if (closing) throw new Error('Execução encerrada.'); return launch(command, args, options).done }
const dotnet = (args, options) => run('bash', [dotnetScript, ...args], options)
const docker = (args, dev = false, options) => run('docker', [...composeArgs(dev), ...args], { env: { ...process.env, DB_PORT: config.DB_PORT }, ...options })
async function requireDocker() {
  try { await run('docker', ['info', '--format', '{{.ServerVersion}}'], { capture: true }) }
  catch { throw new Error('Docker indisponível. Inicie o Docker Desktop/Engine e tente novamente.') }
}
function fingerprint(paths) { const hash = createHash('sha256'); for (const p of paths) hash.update(readFileSync(p)); return hash.digest('hex') }
function backendProjects() {
  return [apiProject, infraProject, 'src/backend/MinhaGaragem.Application/MinhaGaragem.Application.csproj', 'src/backend/MinhaGaragem.Domain/MinhaGaragem.Domain.csproj', 'tests/MinhaGaragem.UnitTests/MinhaGaragem.UnitTests.csproj', 'tests/MinhaGaragem.IntegrationTests/MinhaGaragem.IntegrationTests.csproj']
}
async function ensureBackend(force = false) {
  let version
  try { version = (await dotnet(['--version'], { capture: true })).stdout.trim() } catch { throw new Error('Instale o SDK .NET 10. Consulte docs/development.md.') }
  if (!version.startsWith('10.')) throw new Error('O projeto exige SDK .NET 10. Consulte global.json.')
  const hash = fingerprint(['global.json', 'Directory.Build.props', 'dotnet-tools.json', ...backendProjects()]) + version
  const stamp = join(local, 'backend-dependencies.hash')
  if (force || !existsSync(stamp) || readFileSync(stamp, 'utf8') !== hash || backendProjects().some(p => !existsSync(join(dirname(p), 'obj/project.assets.json')))) {
    await dotnet(['restore', 'MinhaGaragem.sln'])
    await dotnet(['tool', 'restore'])
    writeFileSync(stamp, hash)
  }
}
async function ensureFrontend(force = false) {
  const hash = fingerprint([join(frontend, 'package.json'), join(frontend, 'package-lock.json')]), stamp = join(local, 'frontend-dependencies.hash')
  if (force || !existsSync(stamp) || readFileSync(stamp, 'utf8') !== hash || !existsSync(join(frontend, 'node_modules/vite/bin/vite.js'))) {
    await run('npm', ['ci', '--cache', join(local, 'npm-cache')], { cwd: resolve(frontend) }); writeFileSync(stamp, hash)
  }
}
async function freePort(port, label) {
  await new Promise((res, rej) => {
    const server = createServer()
    server.once('error', () => rej(new Error(`${label}: porta ${port} ocupada. Encerre a instância anterior ou altere a porta. Nenhum processo foi encerrado.`)))
    server.listen(Number(port), '127.0.0.1', () => server.close(res))
  })
}
async function get(url) { try { return await fetch(url, { signal: AbortSignal.timeout(1500) }) } catch { return null } }
async function waitFor(check, label, timeout = 60000) {
  const until = Date.now() + timeout
  while (!closing && Date.now() < until) { if (await check()) return; await sleep(250) }
  throw new Error(`Tempo esgotado aguardando ${label}.`)
}
async function database() {
  ensureEnv(); await requireDocker()
  await docker(['up', '-d', 'db'], true)
  await waitFor(async () => {
    try { await docker(['exec', '-T', 'db', 'pg_isready', '-U', config.POSTGRES_USER, '-d', config.POSTGRES_DB], true, { capture: true }); return true } catch { return false }
  }, 'PostgreSQL')
  say(`PostgreSQL pronto em 127.0.0.1:${config.DB_PORT} (projeto ${devProject}).`)
}
async function migrate(configuration = config.CONFIGURATION) {
  await ensureBackend()
  await dotnet(['ef', 'database', 'update', '--project', infraProject, '--startup-project', apiProject, '--configuration', configuration])
}
async function ownsVite() {
  const response = await get(frontUrl + '/__garage_dev_identity')
  if (!response?.ok) return false
  try { const identity = await response.json(); return identity.root === root && identity.apiUrl === apiUrl } catch { return false }
}
async function vite({ waitApi = false, debug = false } = {}) {
  await ensureFrontend()
  say('FRONTEND_START — preparando Vite.')
  let task
  if (await ownsVite()) say(`Reutilizando Vite deste projeto em ${frontUrl}.`)
  else {
    await freePort(config.VITE_PORT, 'Vite')
    task = launch(process.execPath, [resolve(frontend, 'node_modules/vite/bin/vite.js'), '--host', '127.0.0.1', '--port', config.VITE_PORT, '--strictPort'], { cwd: resolve(frontend), managed: true })
    if (debug) writeFileSync(join(local, 'debug-vite.json'), JSON.stringify({ pid: task.child.pid, root, port: config.VITE_PORT }), { mode: 0o600 })
    await Promise.race([waitFor(ownsVite, 'Vite'), task.done.then(() => { throw new Error('Vite encerrou antes de ficar pronto.') })])
  }
  if (waitApi) await waitFor(async () => (await get(apiUrl + '/health/ready'))?.ok, 'API no debugger', 90000)
  say(`FRONTEND_READY ${frontUrl}`)
  if (task) {
    try { await task.done }
    catch (error) {
      // postDebugTask deliberately stops this owned server; that is a clean exit.
      if (!debug || existsSync(join(local, 'debug-vite.json')) || await ownsVite()) throw error
      say('DEBUG_STOPPED — Vite encerrado pela sessão de debug.')
    }
    return
  }
  // Background task remains active without owning the reused server.
  await new Promise(res => { const interval = setInterval(() => {}, 60000); const stop = () => { clearInterval(interval); res() }; process.once('SIGTERM', stop); process.once('SIGINT', stop) })
}
async function apiWatch() {
  await freePort(config.API_PORT, 'API')
  const task = launch('bash', [dotnetScript, 'watch', '--non-interactive', '--project', resolve(apiProject), 'run', '--configuration', 'Debug', '--no-launch-profile', '--urls', apiUrl], { cwd: apiDirectory, managed: true })
  await Promise.race([waitFor(async () => (await get(apiUrl + '/health/ready'))?.ok, 'API'), task.done.then(() => { throw new Error('API encerrou antes de ficar pronta.') })])
  say(`API_READY ${apiUrl}`)
  return task.done
}
function stopOwned(signal = 'SIGTERM') {
  for (const child of owned) {
    try { if (process.platform === 'win32') child.kill(signal); else process.kill(-child.pid, signal) } catch (e) { if (e.code !== 'ESRCH') throw e }
  }
}
async function shutdown() {
  if (closing) return
  closing = true; process.exitCode = 130; stopOwned()
  const timeout = setTimeout(() => { stopOwned('SIGKILL'); process.exit(130) }, 4000); timeout.unref()
}
process.on('SIGINT', shutdown); process.on('SIGTERM', shutdown)
async function debugPrepare() {
  ensureEnv()
  // launch.json uses these defaults. A mismatch must not launch a browser at the wrong URL.
  const launchConfig = JSON.parse(readFileSync('.vscode/launch.json', 'utf8'))
  const browserConfig = launchConfig.configurations.find(c => c.name === 'Minha Garagem — Vue (Chrome)')
  if (browserConfig?.url !== frontUrl) throw new Error('VITE_PORT difere da URL em .vscode/launch.json. Ajuste as duas configurações juntas; o padrão é 5175.')
  await freePort(config.API_PORT, 'API')
  if (!await ownsVite()) await freePort(config.VITE_PORT, 'Vite')
  await database(); await ensureBackend()
  await dotnet(['build', apiProject, '--configuration', 'Debug', '--no-restore'])
  await dotnet(['ef', 'database', 'update', '--project', infraProject, '--startup-project', apiProject, '--configuration', 'Debug', '--no-build'])
  const sdkList = (await dotnet(['--list-sdks'], { capture: true })).stdout
  const match = [...sdkList.matchAll(/^10\.[^\n]+\[([^\]]+)\]/gm)].at(-1)
  if (!match) throw new Error('Não foi possível localizar o runtime .NET 10 para o debugger.')
  const sdkRoot = dirname(match[1])
  const env = { ...hostEnv(), DOTNET_ROOT: sdkRoot, DOTNET_CLI_HOME: join(local, 'dotnet-home'), NUGET_PACKAGES: join(local, 'nuget') }
  const keys = ['ASPNETCORE_ENVIRONMENT', 'ASPNETCORE_URLS', 'ConnectionStrings__Garage', 'Database__MigrateOnStartup', 'Storage__Path', 'DataProtection__Path', 'Bootstrap__Email', 'Bootstrap__Password', 'DOTNET_ROOT', 'DOTNET_CLI_HOME', 'NUGET_PACKAGES','AI_ENABLED','AI_PROVIDER','GEMINI_API_KEY','GEMINI_MODEL','AI_TIMEOUT_SECONDS','AI_MAX_INPUT_CHARS','AI_MAX_OUTPUT_TOKENS','AI_REQUESTS_PER_MINUTE','AI_REQUESTS_PER_USER_PER_DAY','AI_REQUESTS_PER_DAY']
  writeFileSync(join(local, 'vscode.env'), keys.filter(k=>env[k]!==undefined).map(k => `${k}='${env[k]}'`).join('\n') + '\n', { mode: 0o600 })
  chmodSync(join(local, 'vscode.env'), 0o600)
  say('DEBUG_READY — build Debug, migrations e ambiente privado preparados; API será iniciada pelo debugger.')
}
async function debugStop() {
  const file = join(local, 'debug-vite.json')
  if (!existsSync(file)) return
  const entry = JSON.parse(readFileSync(file, 'utf8'))
  if (entry.root !== root || entry.port !== config.VITE_PORT || !Number.isSafeInteger(entry.pid)) throw new Error('Registro do Vite não pertence a esta configuração.')
  if (await ownsVite()) {
    // Validate PID identity before signalling, so a stale PID cannot stop an unrelated process.
    const result = spawnSync('ps', ['-p', String(entry.pid), '-o', 'args='], { encoding: 'utf8' })
    if (result.status !== 0 || !result.stdout.includes(resolve(frontend, 'node_modules/vite/bin/vite.js'))) throw new Error('PID do Vite não pôde ser confirmado. Encerre sua tarefa no VS Code.')
    rmSync(file, { force: true })
    try { process.kill(-entry.pid, 'SIGTERM') } catch (e) { if (e.code !== 'ESRCH') throw e }
  }
  rmSync(file, { force: true })
}
async function main() {
  const command = process.argv[2]
  validateConfig()
  if (command === 'clean') {
    const paths = []
    for (const parent of ['src/backend', 'tests']) for (const entry of readdirSync(parent, { withFileTypes: true })) if (entry.isDirectory()) for (const artifact of ['bin', 'obj']) paths.push(join(parent, entry.name, artifact))
    paths.push(join(frontend, 'dist'), join(frontend, 'test-results'), join(frontend, 'playwright-report'), 'TestResults')
    for (const path of paths) {
      if (!resolve(path).startsWith(root + '/')) throw new Error('Clean aceita apenas artefatos dentro deste projeto.')
      rmSync(path, { recursive: true, force: true })
    }
    say('Artefatos removidos. .env, uploads, chaves, caches, modelos 3D e volumes foram preservados.'); return
  }
  if (command === 'debug-stop') return debugStop()
  ensureEnv()
  switch (command) {
    case 'setup': await ensureBackend(true); await ensureFrontend(true); say('SETUP_READY — dependências instaladas. Execute make dev ou F5.'); break
    case 'up': await requireDocker(); await docker(['up', '--build', '-d']); break
    case 'down': await requireDocker(); await docker(['down'], true); await docker(['down']); say('Containers parados. Volumes preservados.'); break
    case 'db-up': await database(); break
    case 'dev':
      await freePort(config.API_PORT, 'API'); if (!await ownsVite()) await freePort(config.VITE_PORT, 'Vite')
      await database(); await ensureBackend(); await ensureFrontend(); await migrate('Debug')
      say(`Iniciando API ${apiUrl} e frontend ${frontUrl}. Ctrl+C encerra os processos do host e mantém o banco.`)
      await Promise.all([apiWatch(), vite()]); break
    case 'dev-api': await freePort(config.API_PORT, 'API'); await database(); await migrate('Debug'); await apiWatch(); break
    case 'dev-front': await vite(); break
    case 'debug-vite': await vite({ debug: true }); break
    case 'debug-front': await vite({ waitApi: true, debug: true }); break
    case 'debug-prepare': await debugPrepare(); break
    case 'migrate': await database(); await migrate(); break
    case 'build': await ensureBackend(); await ensureFrontend(); await dotnet(['build', 'MinhaGaragem.sln', '-c', config.CONFIGURATION, '--no-restore']); await run('npm', ['run', 'build'], { cwd: resolve(frontend) }); break
    case 'format': await ensureBackend(); await dotnet(['csharpier','format','src/backend','tests']); break
    case 'format-check': await ensureBackend(); await dotnet(['csharpier','check','src/backend','tests']); break
    case 'ai-test': await database(); await ensureBackend(); await dotnet(['test','MinhaGaragem.sln','-c',config.CONFIGURATION,'--no-restore','--filter','Category=AI'],{env:{...hostEnv(),AI_ENABLED:'false',GEMINI_API_KEY:'',GARAGE_TEST_CONNECTION:config.GARAGE_TEST_CONNECTION||connection()}}); break
    case 'test': await database(); await ensureBackend(); await dotnet(['test', 'MinhaGaragem.sln', '-c', config.CONFIGURATION, '--no-restore'], { env: { ...hostEnv(), AI_ENABLED: 'false', GEMINI_API_KEY: '', GARAGE_TEST_CONNECTION: config.GARAGE_TEST_CONNECTION || connection() } }); break
    case 'test-e2e': await ensureFrontend(); await run('npm', ['run', 'test:e2e'], { cwd: resolve(frontend), env: { ...hostEnv(), GARAGE_BASE_URL: frontUrl } }); break
    case 'lint':
      await ensureBackend(); await ensureFrontend()
      await dotnet(['csharpier', 'check', 'src/backend', 'tests'])
      await run('npm', ['run', 'typecheck'], { cwd: resolve(frontend) })
      await run(process.execPath, ['scripts/validate-development.mjs']); break
    case 'logs': {
      await requireDocker(); const runningDev = (await docker(['ps', '-q'], true, { capture: true })).stdout.trim()
      await docker(['logs', '--follow', '--tail', '100'], !!runningDev); break
    }
    default: throw new Error(`Comando desconhecido: ${command}. Execute make help.`)
  }
}
main().catch(error => { console.error(`[garagem] ${error.message}`); closing = true; process.exitCode = 130; stopOwned(); process.exitCode = 1 })
