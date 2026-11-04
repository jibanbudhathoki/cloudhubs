# {{PROJECT_NAME}}

Idiomatic Go HTTP service following standard Go project structure with clean cmd/ and internal/ packages.

## Getting Started

### Prerequisites
- Go 1.22 or higher

### Running Locally
```bash
# Run the server
go run cmd/server/main.go
```

The application runs on `http://localhost:8080`.

### Building Binary
```bash
go build -o bin/server cmd/server/main.go
./bin/server
```

## Structure
- `cmd/server/main.go` - Application entrypoint & HTTP server configuration
- `internal/handlers/` - Request handlers and routing endpoints
- `go.mod` - Go module definition

