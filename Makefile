SHELL := /bin/sh
.DEFAULT_GOAL := help
NODE ?= node
DEV_SCRIPT := scripts/dev.mjs
API_PROJECT ?= src/backend/MinhaGaragem.Api/MinhaGaragem.Api.csproj
INFRA_PROJECT ?= src/backend/MinhaGaragem.Infrastructure/MinhaGaragem.Infrastructure.csproj
FRONTEND_DIR ?= src/frontend
CONFIGURATION ?= Release
# Host development ports; override with make dev API_PORT=5090 VITE_PORT=5175.
API_PORT ?= 5081
VITE_PORT ?= 5175
DB_PORT ?= 55432
COMPOSE_PROJECT_NAME ?= my-garage
export API_PROJECT INFRA_PROJECT FRONTEND_DIR CONFIGURATION API_PORT VITE_PORT DB_PORT COMPOSE_PROJECT_NAME
export SEED_BASE_URL
.PHONY: help setup up down dev dev-api dev-front build test lint migrate logs clean db-up debug-prepare debug-front debug-vite debug-stop test-e2e seed format format-check ai-test
help: ## Listar os comandos disponíveis
	@awk 'BEGIN {FS = ":.*## "; print "Minha Garagem — desenvolvimento\n"} /^[a-zA-Z0-9_-]+:.*## / {printf "  make %-17s %s\n", $$1, $$2}' $(MAKEFILE_LIST)
setup: ## Preparar .env e instalar dependências .NET/npm
	@$(NODE) $(DEV_SCRIPT) setup
up: ## Construir e iniciar a stack Docker completa
	@$(NODE) $(DEV_SCRIPT) up
seed: ## Popular conta de demonstração na aplicação local já iniciada
	@$(NODE) scripts/seed-demo.mjs
down: ## Parar containers das stacks completa e dev, preservando volumes
	@$(NODE) $(DEV_SCRIPT) down
dev: ## Iniciar PostgreSQL no Docker e API/Vite no host, com hot reload
	@$(NODE) $(DEV_SCRIPT) dev
dev-api: ## Iniciar API com dotnet watch e PostgreSQL no Docker
	@$(NODE) $(DEV_SCRIPT) dev-api
dev-front: ## Iniciar ou reutilizar Vite com hot reload
	@$(NODE) $(DEV_SCRIPT) dev-front
build: ## Compilar backend e frontend para produção
	@$(NODE) $(DEV_SCRIPT) build
test: ## Executar xUnit e integração com PostgreSQL real
	@$(NODE) $(DEV_SCRIPT) test
lint: ## Verificar formatação C#, TypeScript e configurações de debug
	@$(NODE) $(DEV_SCRIPT) lint
migrate: ## Aplicar migrations no banco Docker de desenvolvimento
	@$(NODE) $(DEV_SCRIPT) migrate
logs: ## Acompanhar logs dos containers (Ctrl+C para sair)
	@$(NODE) $(DEV_SCRIPT) logs
clean: ## Remover apenas bin/obj/dist e relatórios de testes
	@$(NODE) $(DEV_SCRIPT) clean
db-up: ## Iniciar somente PostgreSQL de desenvolvimento e aguardar prontidão
	@$(NODE) $(DEV_SCRIPT) db-up
debug-prepare: ## Preparar banco, migrations e build Debug para o VS Code
	@$(NODE) $(DEV_SCRIPT) debug-prepare
debug-front: ## Iniciar Vite e aguardar a API antes do navegador do compound
	@$(NODE) $(DEV_SCRIPT) debug-front
debug-stop: ## Encerrar somente o Vite iniciado pelo debug
	@$(NODE) $(DEV_SCRIPT) debug-stop
test-e2e: ## Executar Playwright com ambiente de desenvolvimento já iniciado
	@$(NODE) $(DEV_SCRIPT) test-e2e

debug-vite: ## Iniciar Vite pertencente à sessão de debug
	@$(NODE) $(DEV_SCRIPT) debug-vite

format: ## Formatar C# com CSharpier
	@$(NODE) $(DEV_SCRIPT) format
format-check: ## Conferir formatação C# com CSharpier
	@$(NODE) $(DEV_SCRIPT) format-check
ai-test: ## Executar testes de IA sem chamadas externas
	@$(NODE) $(DEV_SCRIPT) ai-test
