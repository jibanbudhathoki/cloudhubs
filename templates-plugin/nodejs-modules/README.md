# {{PROJECT_NAME}}

Modern Node.js application built with a feature-based modular architecture and native ES Modules.

## Getting Started

### Prerequisites
- Node.js 18+ (with ES Modules support)
- npm

### Installation
```bash
npm install
```

### Running Locally
```bash
# Start server
npm start

# Development mode with watch
npm run dev
```

The application runs on `http://localhost:3000`.

## Architecture
This project uses a modular architecture where each feature is a self-contained module:
- `src/config/`: Application settings and environment variables
- `src/modules/health/`: System health check module
- `src/modules/users/`: User domain module (routes, controllers, services)
- `src/index.js`: Composition root registering domain modules

