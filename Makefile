.PHONY: build test lint clean docker-build docker-run help
.DEFAULT_GOAL := help

SOLUTION := nem.Mimir.slnx
IMAGE := $(notdir $(CURDIR))

build:
	dotnet build $(SOLUTION)

test:
	dotnet test $(SOLUTION)

lint:
	dotnet format $(SOLUTION) --verify-no-changes

clean:
	dotnet clean $(SOLUTION)
	find . -type d \( -name bin -o -name obj \) -prune -exec rm -rf {} + 2>/dev/null || true

docker-build:
	docker build -t $(IMAGE) .

docker-run:
	docker compose up -d

help:
	@echo "Available targets:"
	@echo "  build        - Build the solution"
	@echo "  test         - Run tests"
	@echo "  lint         - Verify formatting"
	@echo "  clean        - Clean build artifacts"
	@echo "  docker-build - Build Docker image"
	@echo "  docker-run   - Run via Docker"
	@echo "  help         - Show this help message"
