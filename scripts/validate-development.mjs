import assert from 'node:assert/strict'
import { readFileSync, existsSync } from 'node:fs'
const debugReady = process.argv.includes('--debug-ready')
const read = name => JSON.parse(readFileSync('.vscode/' + name + '.json', 'utf8'))
const launch = read('launch'), tasks = read('tasks'), settings = read('settings'), extensions = read('extensions')
assert.equal(launch.version, '0.2.0')
assert.equal(tasks.version, '2.0.0')
const labels = new Set(tasks.tasks.map(t => t.label)), names = new Set(launch.configurations.map(c => c.name))
assert.equal(labels.size, tasks.tasks.length)
assert.equal(names.size, launch.configurations.length)
for (const config of launch.configurations) {
  for (const key of ['preLaunchTask', 'postDebugTask']) if (config[key]) assert(labels.has(config[key]), `Missing task ${config[key]}`)
  if (config.type === 'coreclr') {
    assert.equal(config.request, 'launch')
    assert.equal(config.env.ASPNETCORE_ENVIRONMENT, 'Development')
    assert.equal(config.envFile, '${workspaceFolder}/.local/vscode.env')
    assert(existsSync(config.cwd.replace('${workspaceFolder}', process.cwd())))
    if (debugReady) assert(existsSync(config.program.replace('${workspaceFolder}', process.cwd())), 'Build Debug first: make debug-prepare')
    assert.equal(config.args.length, 0, 'Runtime URLs must come from the generated environment')
    const pdb = config.program.replace('${workspaceFolder}', process.cwd()).replace(/\.dll$/, '.pdb')
    if (debugReady) assert(existsSync(pdb), `Missing symbols: ${pdb}`)
  }
  if (config.type === 'pwa-chrome') {
    assert.equal(config.sourceMaps, true)
    assert(existsSync(config.webRoot.replace('${workspaceFolder}', process.cwd())))
    assert.equal(new URL(config.url).hostname, '127.0.0.1')
  }
}
const compound = launch.compounds.find(c => c.name === 'Minha Garagem — Full Stack Debug')
assert(compound?.stopAll)
assert(labels.has(compound.preLaunchTask))
for (const name of compound.configurations) assert(names.has(name))
assert.equal(launch.configurations.find(c => c.name === compound.configurations[0]).preLaunchTask, undefined, 'Compound must not build or start the API twice')
for (const task of tasks.tasks) {
  assert.equal(task.type, 'process')
  assert.equal(task.command, 'make')
  assert.equal(task.options.cwd, '${workspaceFolder}')
  if (task.isBackground) {
    const pattern = task.problemMatcher[0]
    assert(pattern.background.activeOnStart)
    assert(new RegExp(pattern.background.endsPattern).test('[garagem] FRONTEND_READY http://127.0.0.1:5174'))
    assert(!new RegExp(pattern.background.beginsPattern).test('[garagem] FRONTEND_READY http://127.0.0.1:5174'))
    assert(!new RegExp(pattern.background.endsPattern).test('VITE compiling...'))
  }
}
assert.equal(settings['debug.onTaskErrors'], 'abort')
for (const name of ['ms-dotnettools.csharp', 'Vue.volar', 'ms-azuretools.vscode-containers', 'EditorConfig.EditorConfig']) assert(extensions.recommendations.includes(name))
if (debugReady) {
const env = readFileSync('.local/vscode.env', 'utf8')
const parsed = Object.fromEntries(env.trimEnd().split('\n').map(line => {
  const match = line.match(/^\s*([\w.-]+)\s*=\s*(.*)?\s*$/)
  assert(match)
  return [match[1], match[2].replace(/(^['"]|['"]$)/g, '')]
}))
assert.equal(parsed.ASPNETCORE_ENVIRONMENT, 'Development')
assert(parsed.ConnectionStrings__Garage.startsWith('Host=127.0.0.1;'))
assert(existsSync(parsed.DOTNET_ROOT + '/dotnet'))
assert.equal(parsed.Database__MigrateOnStartup, 'false')
}
console.log(debugReady ? 'Debug preparation passed: DLL/PDB, private envFile, source maps and background readiness.' : 'Development configuration passed: launch/tasks references, source maps and background readiness.')
